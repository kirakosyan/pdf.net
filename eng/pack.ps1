[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory, [string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'assert-production-ready.ps1') -Stage BeforePack -EvidenceDirectory $EvidenceDirectory
if ($LASTEXITCODE -ne 0) { throw 'Source qualification did not permit candidates.' }
if (& git -C $root status --porcelain) { throw 'Candidate packing requires a clean committed checkout.' }
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root 'artifacts/packages' }
if (Test-Path -LiteralPath $OutputDirectory) { throw 'Use a new candidate directory.' }
dotnet build (Join-Path $root 'Fluent.Pdf.slnx') -c Release --property:RestoreLockedMode=true -p:ContinuousIntegrationBuild=true
if ($LASTEXITCODE -ne 0) { throw 'Candidate source build failed.' }
$commit = (& git -C $root rev-parse HEAD).Trim()
$projects = @(Get-ChildItem -LiteralPath (Join-Path $root 'src') -Directory | Where-Object { Test-Path -LiteralPath (Join-Path $_.FullName 'PublicAPI.Shipped.txt') } | Sort-Object Name)
foreach ($project in $projects) {
    foreach ($tfm in @('net472','netstandard2.0','net8.0','net10.0')) {
        $binary = Join-Path $project.FullName "bin/Release/$tfm/$($project.Name)"
        & dotnet (Join-Path $root 'eng/PackageProbe/bin/Release/net10.0/PackageProbe.dll') "$binary.dll" "$binary.pdb" $commit "$OutputDirectory-probes/$($project.Name)-$tfm.json"
        if ($LASTEXITCODE -ne 0) { throw 'Candidate symbols/source binding failed.' }
    }
}
$python = if ($IsWindows) { 'python' } else { 'python3' }
& $python (Join-Path $root 'eng/new-package-manifest.py') --root $root --output "$OutputDirectory-manifest.json"
if ($LASTEXITCODE -ne 0) { throw 'Candidate byte inventory failed.' }
foreach ($project in $projects) {
    dotnet pack (Join-Path $project.FullName ($project.Name + '.csproj')) -c Release --no-build --no-restore -o $OutputDirectory "-p:FluentPdfEvidenceDirectory=$([IO.Path]::GetFullPath($EvidenceDirectory))"
    if ($LASTEXITCODE -ne 0) { throw 'Pack failed; retain partial outputs for investigation, never publish them.' }
}
Write-Output 'Unpublished candidates require independent rebuild inspection, SourceLink probes and full exact-package runtime qualification.'
