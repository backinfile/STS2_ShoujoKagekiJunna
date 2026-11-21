function Get-CorePackage([string]$Path, [string]$MinimumVersion) {
    if ([string]::IsNullOrWhiteSpace($Path)) {
        throw 'Set -CorePackage or SHOUJO_CORE_PACKAGE to an official Core distribution directory.'
    }
    $root = (Resolve-Path -LiteralPath $Path).Path
    if (-not (Test-Path -LiteralPath (Join-Path $root 'ShoujoKagekiCore.json') -PathType Leaf)) {
        throw "Select a ShoujoKagekiCore $MinimumVersion or newer distribution directory."
    }
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
    $destinations = @('ShoujoKagekiCore', 'ShoujoKagekiAijoCore') | ForEach-Object { Join-Path $packageRootPath $_ }
    foreach ($destination in $destinations) {
        if ($Core.Root.Equals($destination, [StringComparison]::OrdinalIgnoreCase) -or
            $Core.Root.StartsWith($destination + [IO.Path]::DirectorySeparatorChar, [StringComparison]::OrdinalIgnoreCase)) {
            throw 'The source Core package must be separate from the build destination.'
        }
    }
    foreach ($destination in $destinations) {
        if (-not (Test-Path -LiteralPath $destination)) { continue }
        $stale = Get-Item -LiteralPath $destination
        if ($stale.FullName -ne $destination -or -not $stale.PSIsContainer -or $stale.Attributes.HasFlag([IO.FileAttributes]::ReparsePoint)) {
            throw 'The stale Core build is not a regular directory.'
        }
        $backup = Join-Path (Split-Path -Parent $packageRootPath) 'artifacts'
        New-Item -ItemType Directory -Force -Path $backup | Out-Null
        [IO.Directory]::Move($stale.FullName, (Join-Path $backup ('Core-backup-' + [Guid]::NewGuid().ToString('N'))))
    }
    & (Join-Path $Core.Root 'sdk/pack-character.ps1') -CorePackage $Core.Root -CharacterDirectory $RoleDirectory -OutputDirectory $packageRootPath -ModId $RoleId -RequiredFeatures @('player-state-events@1', 'card-clone-events@1', 'model-method-scopes@1', 'card-staging@1', 'card-payloads@1', 'player-choices@1', 'card-picking@1', 'card-upgrades@1', 'shine@1', 'card-effects@1', 'card-execution@1', 'card-options@1', 'card-snapshots@1', 'card-play-routing@1', 'model-execution@1', 'card-copies@1', 'presentation@1')
}
