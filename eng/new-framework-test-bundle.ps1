#requires -Version 7.2
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$OutputDirectory,
    [Parameter(Mandatory)][string]$TestPlatformPackage,
    [Parameter(Mandatory)][string]$JavaHome
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if (!$IsWindows -or [Runtime.InteropServices.RuntimeInformation]::OSArchitecture -ne 'X64') { throw 'Build the offline source bundle on Windows x64.' }
$root = Split-Path $PSScriptRoot -Parent
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0 -or (& git -C $root status --porcelain)) { throw 'Build a clean, committed source snapshot before bundling.' }
$destination = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $destination) { throw 'Use a fresh bundle directory.' }
$package = [IO.Path]::GetFullPath($TestPlatformPackage)
$packageHash = '0845b73d09563c5d7c4c19ad8d55db52df427aa395140437f546d4487900e4f1'
if ((Get-FileHash -LiteralPath $package -Algorithm SHA256).Hash.ToLowerInvariant() -cne $packageHash) { throw 'Expected the pinned Microsoft.TestPlatform 18.11.0 package.' }
& dotnet nuget verify $package --all
if ($LASTEXITCODE -ne 0) { throw 'Test platform signature verification failed.' }
$java = [IO.Path]::GetFullPath($JavaHome)
$tests = Join-Path $root 'tests/Fluent.Pdf.Contract.Tests/bin/Release/net472'
$native = Join-Path $root 'artifacts/native-fixtures'
$archival = Join-Path $root 'artifacts/archival-fixtures'
foreach ($path in @((Join-Path $tests 'Fluent.Pdf.Contract.Tests.dll'),(Join-Path $java 'bin/java.exe'),(Join-Path $native 'win-x64/worker.exe'),(Join-Path $native 'win-x86/worker.exe'),(Join-Path $archival 'greenfield-apps-1.28.2.jar'))) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Build/provision the complete source prerequisites first: $path" }
}
$nativeEvidence = Get-Content -LiteralPath (Join-Path $native 'native-fixtures.json') -Raw | ConvertFrom-Json
if ($nativeEvidence.sourceCommit -cne $commit) { throw 'Native source fixtures were built from a different commit.' }
foreach ($deployment in $nativeEvidence.deployments) {
    foreach ($file in $deployment.files) {
        if ((Get-FileHash -LiteralPath (Join-Path $native ($deployment.rid + '/' + $file.name))).Hash -ine $file.sha256) { throw 'Native fixture digest changed after its build.' }
    }
}
if ((Get-FileHash -LiteralPath (Join-Path $archival 'greenfield-apps-1.28.2.jar')).Hash -ine '687d4d8bcfec48c9f7e931cf78ed061f1eeccfba55d4cdc000ba9b4d8aa46ce6') { throw 'Independent validator digest mismatch.' }
$null = [IO.Directory]::CreateDirectory($destination)
Copy-Item -LiteralPath $tests -Destination (Join-Path $destination 'tests') -Recurse
$null = [IO.Directory]::CreateDirectory((Join-Path $destination 'artifacts'))
Copy-Item -LiteralPath $native,$archival -Destination (Join-Path $destination 'artifacts') -Recurse
Copy-Item -LiteralPath $java -Destination (Join-Path $destination 'java') -Recurse
Copy-Item -LiteralPath (Join-Path $root 'Fluent.Pdf.slnx'),(Join-Path $PSScriptRoot 'run-framework-contracts.ps1'),(Join-Path $PSScriptRoot 'run-framework-bundle.ps1') -Destination $destination
# Retain upstream notices together with analysis-only native and Java assets.
Copy-Item -LiteralPath (Join-Path $root 'third-party') -Destination (Join-Path $destination 'third-party') -Recurse
Expand-Archive -LiteralPath $package -DestinationPath (Join-Path $destination 'testplatform')
$files = @(Get-ChildItem -LiteralPath $destination -Recurse -File | Sort-Object FullName | ForEach-Object {
    [ordered]@{path=[IO.Path]::GetRelativePath($destination,$_.FullName).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()}
})
[ordered]@{schemaVersion=1;purpose='offline-framework-source-bundle';sourceCommit=$commit;testPlatformVersion='18.11.0';testPlatformPackageSha256=$packageHash;files=$files;exactPackageConsumers=$false;productionQualified=$false} | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $destination 'bundle.json') -Encoding utf8
Write-Output "Offline source bundle prepared at $destination. It contains no qualified SDK package."
