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
    & (Join-Path $Core.Root 'sdk/pack-character.ps1') -CorePackage $Core.Root -CharacterDirectory $RoleDirectory -OutputDirectory $packageRootPath -ModId $RoleId -RequiredFeatures @('shine@1', 'card-play-routing@1', 'model-execution@1', 'card-copies@1', 'presentation@1')
}
