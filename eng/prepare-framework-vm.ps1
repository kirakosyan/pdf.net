#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$BundleDirectory,
    [Parameter(Mandatory)][string]$BaselineDirectory,
    [Parameter(Mandatory)][string]$OutputDirectory
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'Prepare this source setup on Windows x64.' }
$root = Split-Path $PSScriptRoot -Parent
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or (& git -C $root status --porcelain)) { throw 'Use a clean, committed source snapshot.' }
$bundle = [IO.Path]::GetFullPath($BundleDirectory)
$baseline = [IO.Path]::GetFullPath($BaselineDirectory)
$output = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $output) { throw 'Use a fresh VM setup directory.' }
foreach ($inputDirectory in @($bundle,$baseline)) {
    $inputPrefix = $inputDirectory.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    $outputPrefix = $output.TrimEnd([IO.Path]::DirectorySeparatorChar) + [IO.Path]::DirectorySeparatorChar
    if ($outputPrefix.StartsWith($inputPrefix,[StringComparison]::OrdinalIgnoreCase) -or $inputPrefix.StartsWith($outputPrefix,[StringComparison]::OrdinalIgnoreCase)) { throw 'Keep VM setup separate from immutable inputs and earlier evidence.' }
}
$origin = Get-Content -LiteralPath (Join-Path $bundle 'bundle.json') -Raw | ConvertFrom-Json
if ($origin.sourceCommit -cne $commit -or $origin.purpose -cne 'offline-framework-source-bundle' -or
    $origin.productionQualified -ne $false -or $origin.exactPackageConsumers -ne $false) { throw 'Expected the current unqualified offline source bundle.' }
foreach ($architecture in @('x86','x64')) {
    $report = Join-Path $baseline "offline-framework-$architecture/runtime-contracts.trx"
    if (!(Test-Path -LiteralPath $report -PathType Leaf) -or !(Test-Path -LiteralPath (Join-Path $baseline "offline-framework-$architecture/framework-execution.json") -PathType Leaf)) { throw 'Complete current-Framework baseline and process reports are required.' }
}
$null = [IO.Directory]::CreateDirectory($output)
[IO.Compression.ZipFile]::CreateFromDirectory($bundle,(Join-Path $output 'source-bundle.zip'),[IO.Compression.CompressionLevel]::Fastest,$false)
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'framework-vm/qualify.ps1') -Destination (Join-Path $output 'qualify.ps1')
# The pinned OEM transport applies unix2dos to its batch hook. Stage those exact
# wire bytes before hashing; the guest still checks the unchanged expected hash.
$batch = [regex]::Replace([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'framework-vm/install.bat')), '\r?\n', [Environment]::NewLine)
[IO.File]::WriteAllText((Join-Path $output 'install.bat'), $batch, [Text.UTF8Encoding]::new($false))
foreach ($architecture in @('x86','x64')) {
    Copy-Item -LiteralPath (Join-Path $baseline "offline-framework-$architecture/runtime-contracts.trx") -Destination (Join-Path $output "baseline-$architecture.trx")
    Copy-Item -LiteralPath (Join-Path $baseline "offline-framework-$architecture/framework-execution.json") -Destination (Join-Path $output "baseline-$architecture-execution.json")
    $installer = Join-Path $output "vc_redist.$architecture.exe"
    Invoke-WebRequest "https://aka.ms/vs/17/release/vc_redist.$architecture.exe" -OutFile $installer
    $signature = Get-AuthenticodeSignature -LiteralPath $installer
    if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|, )CN=Microsoft Corporation(,|$)') { throw 'The explicit C++ guest prerequisite is not Microsoft-signed.' }
}
$files = @(Get-ChildItem -LiteralPath $output -File | Sort-Object Name | ForEach-Object {
    [ordered]@{path=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
[ordered]@{
    schemaVersion=1;purpose='framework472-vm-source-setup';sourceCommit=$commit
    bundleManifestSha256=(Get-FileHash -LiteralPath (Join-Path $bundle 'bundle.json') -Algorithm SHA256).Hash.ToLowerInvariant()
    workflowRun=$env:GITHUB_RUN_ID;files=$files;productionQualified=$false;exactPackageConsumers=$false
} | ConvertTo-Json -Depth 8 | Set-Content -LiteralPath (Join-Path $output 'setup.json') -Encoding utf8
& python (Join-Path $PSScriptRoot 'framework-vm.py') --setup $output --commit $commit --check-setup
if ($LASTEXITCODE -ne 0) { throw 'The complete VM setup differs from the current source.' }
Write-Output 'Prepared current-source VM inputs and actual current-Framework baselines. No minimum-runtime execution is inferred.'
exit 0
