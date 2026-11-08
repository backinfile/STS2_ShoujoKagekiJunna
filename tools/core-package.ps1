function Get-CorePackage([string]$Path, [string]$MinimumVersion) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw 'Set -CorePackage or SHOUJO_CORE_PACKAGE to an official Core distribution directory.'
    }
    $root = (Resolve-Path -LiteralPath $Path).Path
    $checked = & (Join-Path $root 'sdk/validate-package.ps1') -CorePackage $root
    if ([Version]($checked.Bundle.releaseVersion.Split('-')[0]) -lt [Version]($MinimumVersion.Split('-')[0])) {
        throw "Core $MinimumVersion or newer is required; package contains $($checked.Bundle.releaseVersion)."
    }
    return [pscustomobject]@{
        Root = $checked.Root
        Bundle = $checked.Bundle
        ApiPath = Join-Path $checked.Root $checked.Bundle.apiAssembly
    }
}

function Write-CoreRoleMetadata($Core, [string]$RoleDirectory, [string]$PackageRoot, [string]$RoleId) {
    $packageRootPath = [IO.Path]::GetFullPath($PackageRoot)
    $destination = [IO.Path]::GetFullPath((Join-Path $packageRootPath 'ShoujoKagekiAijoCore'))
    if (-not $destination.StartsWith($packageRootPath + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase) -or
        $Core.Root.Equals($destination, [StringComparison]::OrdinalIgnoreCase) -or
        $Core.Root.StartsWith($destination + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
        throw 'The source Core package must be separate from the build destination.'
    }
    if ($Core.Bundle.features -notcontains 'shine@1') { throw 'Core lacks required feature shine@1' }
    if (Test-Path -LiteralPath $destination) { Remove-Item -LiteralPath $destination -Recurse -Force }
    # Older Core SDK packers embed Core; write the V1 metadata without invoking them.
    [ordered]@{
        format=1; modId=$RoleId; apiMajor=1; minimumApiVersion='1.0.0'
        requiredFeatures=@('shine@1'); optionalFeatures=@('diagnostics@1', 'music@1'); characterFamily='ShoujoKageki'
    } | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $RoleDirectory 'core.requirements') -Encoding UTF8
    $lockFiles = [ordered]@{}
    Get-ChildItem -LiteralPath $Core.Root -Recurse -File | Sort-Object FullName | ForEach-Object {
        $relative = $_.FullName.Substring($Core.Root.TrimEnd('\', '/').Length + 1).Replace('\','/')
        $lockFiles[$relative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()
    }
    [ordered]@{ format=1; releaseVersion=$Core.Bundle.releaseVersion; files=$lockFiles } | ConvertTo-Json -Depth 10 |
        Set-Content -LiteralPath (Join-Path $RoleDirectory 'core.package.lock') -Encoding UTF8
}
