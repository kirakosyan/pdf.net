[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$readiness = Get-Content (Join-Path $root 'specs/release-readiness.json') -Raw | ConvertFrom-Json
if ($readiness.productionReady) { Write-Output 'Negative baseline check does not apply to a qualified release.'; exit 0 }
$output = & pwsh -NoProfile -File (Join-Path $PSScriptRoot 'assert-production-ready.ps1') -Stage Publish 2>&1 | Out-String
if ($LASTEXITCODE -eq 0 -or !$output.Contains('readiness is false')) { throw 'Unqualified publication was not blocked for the expected reason.' }
Write-Output 'Verified: unqualified production publication is blocked.'
$packageDirectory = Join-Path $root ('artifacts/blocked-pack-check/' + [Guid]::NewGuid().ToString('N'))
$packOutput = & dotnet pack (Join-Path $root 'src/Fluent.Pdf/Fluent.Pdf.csproj') -c Release --no-build --no-restore -o $packageDirectory 2>&1 | Out-String
if ($LASTEXITCODE -eq 0 -or !$packOutput.Contains('readiness is false')) { throw 'The actual package target did not enforce production readiness.' }
if ((Test-Path -LiteralPath $packageDirectory) -and @(Get-ChildItem -LiteralPath $packageDirectory -Filter '*.nupkg').Count) { throw 'An unqualified package was produced.' }
Write-Output 'Verified: the package target rejects unqualified packing and produces no package.'
exit 0
