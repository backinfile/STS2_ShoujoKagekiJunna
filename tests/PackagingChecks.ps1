#requires -Version 7.0
param([Parameter(Mandatory)][string]$CorePackage)
$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
. (Join-Path $repo 'tools/core-package.ps1')
$results = [Collections.Generic.List[string]]::new()
function Check([string]$Name, [bool]$Passed) {
    if (-not $Passed) { throw "Failed: $Name" }
    $results.Add($Name)
}
function Expect-Failure([string]$Name, [scriptblock]$Action, [string]$Message) {
    try { & $Action } catch {
        Check $Name ($_.Exception.Message -like "*$Message*")
        return
    }
    throw "Expected failure: $Name"
}
$root = Join-Path $repo ('out/tmp/core-workshop-checks-' + [Guid]::NewGuid().ToString('N'))
$role = Join-Path $root 'dist/ShoujoKagekiJunna'
New-Item -ItemType Directory -Path (Join-Path $role 'lib') -Force | Out-Null
Copy-Item -LiteralPath (Join-Path $repo 'ShoujoKagekiJunna.json') -Destination $role
foreach ($file in @('ShoujoKagekiJunna.dll', 'ShoujoKagekiJunna.pck', 'lib/ShoujoKagekiJunna.Stable.dll', 'lib/ShoujoKagekiJunna.Beta.dll')) {
    Set-Content -LiteralPath (Join-Path $role $file) -Value 'packaging fixture' -Encoding utf8
}
$preview = Join-Path $root 'preview.png'
[IO.File]::WriteAllBytes($preview, [Convert]::FromBase64String('iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+jGZkAAAAASUVORK5CYII='))
$manifest = Get-Content -LiteralPath (Join-Path $role 'ShoujoKagekiJunna.json') -Raw | ConvertFrom-Json
$minimum = ($manifest.dependencies | Where-Object id -eq 'ShoujoKagekiAijoCore').min_version
$core = Get-CorePackage $CorePackage $minimum
$bundleHash = (Get-FileHash -LiteralPath (Join-Path $core.Root 'core.bundle')).Hash
Expect-Failure 'missing Core package' { Get-CorePackage '' $minimum } 'Set -CorePackage'
Expect-Failure 'Core minimum version' { Get-CorePackage $CorePackage '9999.0.0' } 'or newer is required'
$stale = Join-Path $root 'dist/ShoujoKagekiAijoCore'
New-Item -ItemType Directory -Path $stale -Force | Out-Null
Set-Content -LiteralPath (Join-Path $stale 'old-runtime.dll') -Value 'stale'
Write-CoreRoleMetadata $core $role (Join-Path $root 'dist') 'ShoujoKagekiJunna'
Check 'stale bundled Core removed' (-not (Test-Path -LiteralPath $stale))
Check 'only Junna in distribution' (@(Get-ChildItem (Join-Path $root 'dist')).Count -eq 1)
$requirements = Get-Content -LiteralPath (Join-Path $role 'core.requirements') -Raw | ConvertFrom-Json
Check 'Core runtime requirements' ($requirements.modId -eq $manifest.id -and $requirements.apiMajor -eq 1 -and
    $requirements.minimumApiVersion -eq '1.0.0' -and $requirements.requiredFeatures -contains 'shine@1' -and
    $requirements.characterFamily -eq 'ShoujoKageki')
$lock = Get-Content -LiteralPath (Join-Path $role 'core.package.lock') -Raw | ConvertFrom-Json
Check 'Core SDK release locked' ($lock.releaseVersion -eq $core.Bundle.releaseVersion)
Check 'Core API hash locked' ($lock.files.($core.Bundle.apiAssembly) -eq $core.Bundle.files.($core.Bundle.apiAssembly))
Check 'Core package unchanged' ((Get-FileHash -LiteralPath (Join-Path $core.Root 'core.bundle')).Hash -eq $bundleHash)
$unsafe = [pscustomobject]@{ Root=$stale; Bundle=$core.Bundle }
Expect-Failure 'Core source preserved' { Write-CoreRoleMetadata $unsafe $role (Join-Path $root 'dist') $manifest.id } 'must be separate'
$prepare = Join-Path $repo 'tools/prepare-workshop.ps1'
$workspace = Join-Path $root 'workshop'
# These numeric IDs are offline fixtures, never sent to Steam.
& $prepare -CoreWorkshopId 9876543210 -PreviewImage $preview -OutputDirectory $workspace -ModDirectory $role -WorkshopId 9876543211
$config = Get-Content -LiteralPath (Join-Path $workspace 'workshop.json') -Raw | ConvertFrom-Json
Check 'BaseLib and Core Workshop dependencies' ($config.dependencies.Count -eq 2 -and
    $config.dependencies -contains 3737335127 -and $config.dependencies -contains 9876543210)
$entries = @(Get-ChildItem -LiteralPath (Join-Path $workspace 'content'))
Check 'Workshop uploads only Junna' ($entries.Count -eq 1 -and $entries[0].Name -eq 'ShoujoKagekiJunna')
Check 'Workshop update identity' ((Get-Content -LiteralPath (Join-Path $workspace 'mod_id.txt') -Raw).Trim() -eq '9876543211')
Check 'Workshop preview copied' ((Get-FileHash -LiteralPath (Join-Path $workspace 'image.png')).Hash -eq (Get-FileHash -LiteralPath $preview).Hash)
Expect-Failure 'zero Core Workshop ID' { & $prepare -CoreWorkshopId 0 -PreviewImage $preview -OutputDirectory (Join-Path $root 'zero') -ModDirectory $role } 'Cannot validate argument'
Expect-Failure 'BaseLib cannot be Core' { & $prepare -CoreWorkshopId 3737335127 -PreviewImage $preview -OutputDirectory (Join-Path $root 'baselib') -ModDirectory $role } 'not BaseLib'
Expect-Failure 'existing workspace preserved' { & $prepare -CoreWorkshopId 9876543210 -PreviewImage $preview -OutputDirectory $workspace -ModDirectory $role } 'already exists'
Expect-Failure 'nested output rejected' { & $prepare -CoreWorkshopId 9876543210 -PreviewImage $preview -OutputDirectory (Join-Path $role 'nested') -ModDirectory $role } 'outside the source'
Set-Content -LiteralPath (Join-Path $role 'ShoujoKagekiAijoCore.Api.V1.dll') -Value 'stale Core'
Expect-Failure 'embedded Core rejected' { & $prepare -CoreWorkshopId 9876543210 -PreviewImage $preview -OutputDirectory (Join-Path $root 'embedded') -ModDirectory $role } 'independent Core dependency'
@{ passed=$results.Count; checks=@($results) } | ConvertTo-Json -Depth 4 |
    Set-Content -LiteralPath (Join-Path $root 'results.json') -Encoding utf8
Write-Output "$($results.Count) packaging checks passed. Results: $([IO.Path]::GetRelativePath($repo, $root))/results.json"
