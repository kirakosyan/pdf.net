[CmdletBinding()]
param([Parameter(Mandatory)][string]$EvidenceDirectory)
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
& (Join-Path $PSScriptRoot 'assert-production-ready.ps1') -Stage BeforePack -EvidenceDirectory $EvidenceDirectory
dotnet pack (Join-Path $root 'src/Fluent.Pdf/Fluent.Pdf.csproj') -c Release -o (Join-Path $root 'artifacts/packages') "-p:FluentPdfEvidenceDirectory=$([IO.Path]::GetFullPath($EvidenceDirectory))"
if ($LASTEXITCODE -ne 0) { throw 'Pack failed.' }
