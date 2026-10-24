[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$Lane,
    [Parameter(Mandatory)][string]$ConsumerProject,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [ValidateSet('source','package')][string]$Mode = 'source',
    [string]$PackageManifest = '',
    [string]$TestInventory = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$matrix = Get-Content -LiteralPath (Join-Path $root 'specs/runtime-lanes.json') -Raw | ConvertFrom-Json
$record = @($matrix.lanes | Where-Object id -CEQ $Lane)
if ($record.Count -ne 1) { throw 'Unknown or duplicate required lane.' }
$record = $record[0]
$family = if ($IsWindows) { 'win' } elseif ($IsLinux) { 'linux' } elseif ($IsMacOS) { 'osx' } else { throw 'Unsupported runtime host.' }
if ($record.os -cne $family -or $record.osArchitecture -ine [Runtime.InteropServices.RuntimeInformation]::OSArchitecture.ToString()) { throw 'The declared lane requires its actual native OS/architecture host.' }
if ($record.runtimeFamily -eq 'framework') {
    $release = Get-ItemPropertyValue -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release
    if ($record.minimumFramework -and $release -notin $record.frameworkRelease) { throw 'Actual minimum Framework4.7.2 is required; a newer in-place Framework is not a minimum-runtime lane.' }
    if (!$record.minimumFramework -and $release -lt 528040) { throw 'Current Framework lane requires an actual serviced Framework4.8-or-later installation.' }
}
if ($Mode -eq 'package' -and (!$PackageManifest -or !$TestInventory)) { throw 'Exact package manifest and reviewed full test inventory are mandatory.' }
$directory = [IO.Path]::GetFullPath($OutputDirectory)
$null = [IO.Directory]::CreateDirectory($directory)
$report = Join-Path $directory 'runtime-contracts.trx'
if ([IO.File]::Exists($report) -or [IO.File]::Exists((Join-Path $directory 'runtime-identity.json'))) { throw 'Use a fresh owned output directory for each invocation.' }
$previous = $env:FLUENT_PDF_EXPECTED_ARCH
$env:FLUENT_PDF_EXPECTED_ARCH = $record.processArchitecture
$tfm = if ($record.runtimeFamily -eq 'framework') { 'net472' } elseif ($record.runtimeMajor -eq 8) { 'net8.0' } else { 'net10.0' }
try {
    # The caller supplies an already built consumer. No filtering, restoring,
    # retargeting, registry downgrade, runtime installation or fallback is inferred.
    $arguments = @('test',[IO.Path]::GetFullPath($ConsumerProject),'-c','Release','-f',$tfm,'--no-build','--no-restore','--logger','trx;LogFileName=runtime-contracts.trx','--results-directory',$directory)
    if ($record.processArchitecture -eq 'x86') { $arguments += @('--','RunConfiguration.TargetPlatform=x86') }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw 'The required runtime test process failed.' }
    $python = if ($IsWindows) { 'python' } else { 'python3' }
    $check = @((Join-Path $root 'eng/check-runtime-evidence.py'),'--lane',$Lane,'--trx',$report,'--mode',$Mode,'--output',(Join-Path $directory 'runtime-identity.json'))
    if ($PackageManifest) { $check += @('--package-manifest',[IO.Path]::GetFullPath($PackageManifest)) }
    if ($TestInventory) { $check += @('--test-inventory',[IO.Path]::GetFullPath($TestInventory)) }
    & $python @check
    if ($LASTEXITCODE -ne 0) { throw 'The actual process, target, package or complete-test inventory does not match the declared lane.' }
} finally { $env:FLUENT_PDF_EXPECTED_ARCH = $previous }
Write-Output 'One runtime evidence consistency check passed; complete suite/corpus/trusted-host/release qualification remains separate.'
