#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][ValidateSet('x86','x64')][string]$Architecture,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$WorkspaceDirectory,
    [switch]$RequireFramework472,
    [ValidateRange(10,14400)][int]$TimeoutSeconds = 3600
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = [IO.Path]::GetFullPath($PSScriptRoot).TrimEnd('\') + '\'
$manifestPath = Join-Path $root 'bundle.json'
$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
if ($manifest.schemaVersion -ne 1 -or $manifest.purpose -cne 'offline-framework-source-bundle' -or $manifest.productionQualified -ne $false -or $manifest.exactPackageConsumers -ne $false -or $manifest.sourceCommit -cnotmatch '^[a-f0-9]{40}$') { throw 'Not an unqualified source bundle.' }
$seen = @{}
foreach ($file in $manifest.files) {
    if (!$file.path -or [IO.Path]::IsPathRooted($file.path) -or $file.path.Contains(':') -or ($file.path -split '[/\\]') -contains '..') { throw 'Invalid relative bundle path.' }
    $path = [IO.Path]::GetFullPath((Join-Path $root $file.path))
    if (!$path.StartsWith($root,[StringComparison]::OrdinalIgnoreCase) -or $seen.ContainsKey($path)) { throw 'Duplicate or escaping bundle path.' }
    $seen[$path] = $true
    $item = Get-Item -LiteralPath $path
    if ($item.PSIsContainer -or $item.Length -ne $file.bytes -or (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ine $file.sha256) { throw "Bundle input changed: $($file.path)" }
}
if ($seen.Count -eq 0) { throw 'An empty bundle cannot execute tests.' }
foreach ($file in Get-ChildItem -LiteralPath $root -Recurse -File) {
    if ($file.FullName -ine $manifestPath -and !$seen.ContainsKey($file.FullName)) { throw 'Unmanifested file in source bundle. Keep results outside the bundle.' }
}
$output = [IO.Path]::GetFullPath($OutputDirectory)
if ($output.StartsWith($root,[StringComparison]::OrdinalIgnoreCase)) { throw 'Keep evidence outside the immutable source bundle.' }
$workspace = [IO.Path]::GetFullPath($WorkspaceDirectory).TrimEnd('\') + '\'
if ($workspace.StartsWith($root,[StringComparison]::OrdinalIgnoreCase) -or $root.StartsWith($workspace,[StringComparison]::OrdinalIgnoreCase) -or $output.StartsWith($workspace,[StringComparison]::OrdinalIgnoreCase) -or $workspace.StartsWith($output.TrimEnd('\') + '\',[StringComparison]::OrdinalIgnoreCase) -or (Test-Path -LiteralPath $workspace) -or (Test-Path -LiteralPath $output)) { throw 'Use fresh, separate workspace and evidence directories outside the bundle.' }
$null = [IO.Directory]::CreateDirectory($workspace)
foreach ($file in $manifest.files) {
    $target = Join-Path $workspace $file.path
    $null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($target))
    Copy-Item -LiteralPath (Join-Path $root $file.path) -Destination $target
    if ((Get-FileHash -LiteralPath $target -Algorithm SHA256).Hash -ine $file.sha256) { throw 'Workspace copy differs from the bundle.' }
}
$previousPath = $env:PATH
try {
    $env:PATH = (Join-Path $workspace 'java/bin') + ';' + $env:PATH
    & (Join-Path $workspace 'run-framework-contracts.ps1') -TestAssembly (Join-Path $workspace 'tests/Fluent.Pdf.Contract.Tests.dll') -TestPlatformDirectory (Join-Path $workspace 'testplatform/tools/net462/Common7/IDE/Extensions/TestPlatform') -Architecture $Architecture -OutputDirectory $output -RequireFramework472:$RequireFramework472 -TimeoutSeconds $TimeoutSeconds
} finally {
    $env:PATH = $previousPath
    if (Test-Path -LiteralPath $output -PathType Container) {
        [ordered]@{sourceCommit=$manifest.sourceCommit;bundleManifestSha256=(Get-FileHash -LiteralPath $manifestPath -Algorithm SHA256).Hash.ToLowerInvariant();productionQualified=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $output 'bundle-origin.json') -Encoding UTF8
    }
}
