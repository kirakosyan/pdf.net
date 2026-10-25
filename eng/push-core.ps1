#requires -Version 7.2
[CmdletBinding()]
param([string]$Directory = 'qualified')
$ErrorActionPreference = 'Stop'
if ([string]::IsNullOrWhiteSpace($env:NUGET_API_KEY)) { throw 'Missing short-lived OIDC publishing credential.' }
$packages = Join-Path $Directory 'packages'
$names = @(Get-ChildItem -LiteralPath $packages -File | ForEach-Object Name)
if ($names.Count -ne 2 -or 'Fluent.Pdf.1.0.0.nupkg' -cnotin $names -or 'Fluent.Pdf.1.0.0.snupkg' -cnotin $names) { throw 'Only the scoped core package and symbols may be pushed.' }
# NuGet CLI also pushes the adjacent same-name snupkg. A retry still requires
# independent public signature/payload and fresh-consumer verification afterward.
dotnet nuget push (Join-Path $packages 'Fluent.Pdf.1.0.0.nupkg') --api-key $env:NUGET_API_KEY --source https://api.nuget.org/v3/index.json --skip-duplicate
if ($LASTEXITCODE -ne 0) { throw 'Core package/symbol publication failed.' }
