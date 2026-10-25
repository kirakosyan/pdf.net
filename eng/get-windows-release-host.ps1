#requires -Version 5.1
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'Run this preflight in the local Windows session.' }
$root = Split-Path $PSScriptRoot -Parent
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve the local checkout commit.' }
$dirty = @(& git -C $root status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Cannot inspect the local checkout.' }
$registry = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,[Microsoft.Win32.RegistryView]::Registry64)
try {
    $key = $registry.OpenSubKey('SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full')
    if ($null -eq $key) { throw 'No installed full Framework release was found.' }
    try { $release = [int]$key.GetValue('Release') } finally { $key.Dispose() }
} finally { $registry.Dispose() }
$os = Get-CimInstance Win32_OperatingSystem
$nativeArchitecture = if ($env:PROCESSOR_ARCHITEW6432) { $env:PROCESSOR_ARCHITEW6432 } else { $env:PROCESSOR_ARCHITECTURE }
$osArchitecture = switch ($nativeArchitecture) { 'AMD64' { 'X64' } 'ARM64' { 'Arm64' } 'x86' { 'X86' } default { 'Unknown' } }
. (Join-Path $PSScriptRoot 'windows-framework-policy.ps1')
$lanes = @(Get-FluentPdfFrameworkHostLanes -Release $release -OSArchitecture $osArchitecture)
$report = [ordered]@{
    schemaVersion = 1; purpose = 'windows-release-host-preflight'; sourceCommit = $commit
    workingTreeDirty = ($dirty.Count -gt 0); os = $os.Caption; osVersion = $os.Version
    osArchitecture = $osArchitecture; installedFrameworkRelease = $release
    eligibleFrameworkSourceLanes = $lanes
    minimumFramework472Installed = ($release -in @(461808,461814))
    sdkTestsExecuted = $false; packageConsumersExecuted = $false; productionQualified = $false
    minimumRuntimeGuidance = 'Framework 4.x uses in-place updates. A 4.7.2 targeting pack does not supply a 4.7.2 runtime. Use an existing isolated 4.7.2 host for minimum-lane execution; do not downgrade the physical PC.'
}
$path = [IO.Path]::GetFullPath($OutputPath)
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
$stream = [IO.File]::Open($path,[IO.FileMode]::CreateNew,[IO.FileAccess]::Write,[IO.FileShare]::None)
try {
    $bytes = (New-Object Text.UTF8Encoding($false)).GetBytes(($report | ConvertTo-Json -Depth 6) + [Environment]::NewLine)
    $stream.Write($bytes,0,$bytes.Length)
} finally { $stream.Dispose() }
Write-Output "Windows preflight saved to $path. Eligible source lanes: $($lanes -join ', '). No tests were executed."
