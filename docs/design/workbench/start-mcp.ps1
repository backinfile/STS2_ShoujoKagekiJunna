$ErrorActionPreference = 'Stop'
$taskDirectory = $PSScriptRoot
$nodePath = (Get-Command node -ErrorAction Stop).Source
$runtimePath = Join-Path $taskDirectory 'local-save.runtime.json'
$configPath = Join-Path $taskDirectory 'local-save.config.json'
if (Test-Path -LiteralPath $configPath) {
    $configuration = Get-Content -LiteralPath $configPath -Raw -Encoding UTF8 | ConvertFrom-Json
    try {
        $response = Invoke-RestMethod -Uri "http://127.0.0.1:$($configuration.port)/health" -Headers @{ Authorization = "Bearer $($configuration.token)" } -TimeoutSec 2
    } catch { $response = $null }
    if ($response.connected -and $response.serverType -ne 'junna-workbench-mcp') {
        if (!(Test-Path -LiteralPath $runtimePath)) { throw '旧服务进程记录不存在' }
        $oldRuntime = Get-Content -LiteralPath $runtimePath -Raw -Encoding UTF8 | ConvertFrom-Json
        $oldProcess = Get-CimInstance Win32_Process -Filter "ProcessId = $($oldRuntime.pid)"
        $expectedScript = Join-Path $taskDirectory 'local-save-server.cjs'
        if (!$oldProcess -or !$oldProcess.CommandLine.Contains($expectedScript)) { throw '旧服务进程路径不匹配' }
        Stop-Process -Id $oldRuntime.pid
    }
}
& $nodePath (Join-Path $taskDirectory 'mcp-server.cjs') --start
if ($LASTEXITCODE -ne 0) { throw 'MCP 启动失败' }
