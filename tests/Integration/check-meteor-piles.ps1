param([Parameter(Mandatory)][int]$Port)
$ErrorActionPreference = 'Stop'
$root = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$report = Join-Path $root 'out/tmp/meteor-pile/results.json'
$previousWrite = if (Test-Path -LiteralPath $report) { (Get-Item -LiteralPath $report).LastWriteTimeUtc } else { [datetime]::MinValue }
$response = Invoke-RestMethod "http://127.0.0.1:$Port/api/v1/singleplayer" -Method Post -ContentType 'application/json' -Body '{"action":"run_command","command":"junna_meteor_piles check"}'
if ($response.status -ne 'ok') { throw ($response | ConvertTo-Json -Compress) }
$deadline = [datetime]::UtcNow.AddSeconds(30)
do {
    if ((Test-Path -LiteralPath $report) -and (Get-Item -LiteralPath $report).LastWriteTimeUtc -gt $previousWrite) {
        $rows = @(Get-Content -LiteralPath $report -Raw | ConvertFrom-Json)
        foreach ($row in $rows) { Write-Output ($(if ($row.pass) { 'PASS' } else { 'FAIL' }) + ': ' + $row.name + ' ' + ($row.actual | ConvertTo-Json -Compress -Depth 5)) }
        if ($rows.Count -eq 0 -or @($rows | Where-Object { !$_.pass }).Count -gt 0) { throw 'Meteor pile regression failed' }
        return
    }
    Start-Sleep -Milliseconds 200
} while ([datetime]::UtcNow -lt $deadline)
throw 'Meteor pile probe timed out'