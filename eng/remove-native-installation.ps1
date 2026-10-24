[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$InstallationDirectory,
    [Parameter(Mandatory)][ValidatePattern('^[0-9a-f]{64}$')][string]$ManifestSha256,
    [Parameter(Mandatory)][string]$PythonExecutable,
    [Parameter(Mandatory)][string]$ReportDirectory
)
$ErrorActionPreference='Stop'
Set-StrictMode -Version Latest
if (![IO.Path]::IsPathFullyQualified($InstallationDirectory) -or ![IO.Path]::IsPathFullyQualified($PythonExecutable) -or ![IO.Path]::IsPathFullyQualified($ReportDirectory)) { throw 'Explicit absolute installation, Python and report paths are required.' }
$installation=[IO.Path]::GetFullPath($InstallationDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
if (!$installation -or $installation -eq [IO.Path]::GetPathRoot($installation).TrimEnd([IO.Path]::DirectorySeparatorChar)) { throw 'A filesystem root cannot be removed.' }
$prefix=$installation+[IO.Path]::DirectorySeparatorChar
$reportRoot=[IO.Path]::GetFullPath($ReportDirectory)
if ($reportRoot.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase) -or $reportRoot -eq $installation) { throw 'Retained reports must be outside the installation.' }
$null=[IO.Directory]::CreateDirectory($reportRoot)
$report=Join-Path $reportRoot ('removal-'+[Guid]::NewGuid().ToString('N')+'.json')
$manifest=Join-Path $installation 'provision-manifest.json'
& $PythonExecutable (Join-Path $PSScriptRoot 'native-distribution.py') verify --manifest $manifest --manifest-sha256 $ManifestSha256 --source $installation --report $report
if ($LASTEXITCODE -ne 0) { throw 'Pinned installed inventory verification failed; nothing was removed.' }
$inventory=Get-Content -LiteralPath $manifest -Raw | ConvertFrom-Json
$files=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$directories=[Collections.Generic.HashSet[string]]::new([StringComparer]::OrdinalIgnoreCase)
$null=$files.Add($manifest)
foreach ($asset in $inventory.assets) {
    $path=[IO.Path]::GetFullPath((Join-Path $installation $asset.path))
    if (!$path.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'An installed path escaped the intended directory.' }
    $null=$files.Add($path)
    $parent=[IO.Path]::GetDirectoryName($path)
    while ($parent -ne $installation) {
        if (!$parent.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'A directory escaped the intended inventory.' }
        $null=$directories.Add($parent)
        $parent=[IO.Path]::GetDirectoryName($parent)
    }
}
$actual=@(Get-ChildItem -LiteralPath $installation -Recurse -Force)
foreach ($item in $actual) {
    if (($item.Attributes -band [IO.FileAttributes]::ReparsePoint) -ne 0) { throw 'A linked installed path prevents removal.' }
    if (!$item.FullName.StartsWith($prefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Removal escaped the intended installation.' }
    if ($item.PSIsContainer) { if (!$directories.Contains($item.FullName)) { throw 'An unowned directory prevents removal.' } }
    elseif (!$files.Contains($item.FullName)) { throw 'An unowned file prevents removal.' }
}
# Each removal uses a verified exact owned path. No recursive deletion or shell switching occurs.
foreach ($path in $files) { Remove-Item -LiteralPath $path -Force }
foreach ($path in ($directories | Sort-Object Length -Descending)) { Remove-Item -LiteralPath $path }
Remove-Item -LiteralPath $installation
$record=Get-Content -LiteralPath $report -Raw | ConvertFrom-Json
$record | Add-Member -NotePropertyName removed -NotePropertyValue $true
$record | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $report -Encoding utf8
Write-Output "NATIVE_SOURCE_REMOVAL_PASS;$report"
