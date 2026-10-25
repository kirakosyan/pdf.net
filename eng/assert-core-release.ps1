#requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$ProjectName, [Parameter(Mandatory)][string]$PackageVersion)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$policy = Get-Content -LiteralPath (Join-Path $root 'specs/core-release.json') -Raw | ConvertFrom-Json
if ($ProjectName -cne 'Fluent.Pdf' -or $PackageVersion -cne '1.0.0' -or $policy.packageId -cne $ProjectName -or $policy.packageVersion -cne $PackageVersion -or $policy.schemaVersion -ne 1 -or $policy.scope -cne 'implemented-managed-core-profiles' -or $policy.authorizedOn -cne '2026-10-25' -or $policy.fullRoadmapQualified -ne $false -or $policy.publishCompanionPackages -ne $false -or [string]::IsNullOrWhiteSpace($policy.authorization)) { throw 'This authorization permits only the explicitly scoped Fluent.Pdf 1.0.0 core candidate.' }
if ($policy.scopeDocument -cne 'docs/releases/1.0.0.md' -or !(Test-Path -LiteralPath (Join-Path $root $policy.scopeDocument))) { throw 'The core release scope must be documented.' }
if (& git -C $root status --porcelain) { throw 'Core candidates require clean committed source.' }
Write-Output 'Core candidate creation permitted by the version-specific user scope. Actual package verification is still required before publication.'
