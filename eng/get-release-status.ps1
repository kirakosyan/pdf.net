#requires -Version 7.2
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputPath)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$readiness = Get-Content -LiteralPath (Join-Path $root 'specs/release-readiness.json') -Raw | ConvertFrom-Json
$spec = Get-Content -LiteralPath (Join-Path $root 'specs/requirements.json') -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'release-policy.ps1')
$policy = Get-FluentPdfReleasePolicy -Readiness $readiness -Specification $spec
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the source commit.' }
$dirty = @(& git -C $root status --porcelain)
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify source changes.' }
$backlog = Get-Content -LiteralPath (Join-Path $root 'specs/github-issues.json') -Raw | ConvertFrom-Json
$requirements = @($policy.RequiredFeatures | ForEach-Object {
    $feature = $_
    $pending = @()
    if ($feature.PSObject.Properties.Name -contains 'implementationProgress' -and
        $feature.implementationProgress.PSObject.Properties.Name -contains 'pending') {
        $pending = @($feature.implementationProgress.pending)
    }
    $mapping = @($backlog.issues | Where-Object requirement -CEQ $feature.id)
    [ordered]@{
        id = $feature.id; title = $feature.title; status = $feature.status
        issue = $(if ($mapping.Count -eq 1) { $mapping[0].number } else { $null })
        pending = $pending
        acceptance = @($feature.acceptance)
        unqualifiedDependencies = @($feature.dependencies | Where-Object {
            $dependencyId = $_
            @($policy.RequiredFeatures | Where-Object { $_.id -ceq $dependencyId -and $_.status -cne 'qualified' }).Count -gt 0
        })
    }
})
$workflow = Join-Path $root ('.github/workflows/' + $readiness.qualificationWorkflow)
$report = [ordered]@{
    schemaVersion = 1; purpose = 'release-status-inventory'; sourceCommit = $commit
    workingTreeDirty = ($dirty.Count -gt 0); packageVersion = $readiness.packageVersion
    productionReady = $readiness.productionReady; qualificationEvidenceValidated = $false
    sourceTestsExecuted = $false; packageConsumersExecuted = $false
    catalogueReleaseRequirements = @($spec.features | Where-Object releaseRequired).Count
    activeReleaseRequirements = $policy.RequiredFeatures.Count
    qualifiedRequirements = @($policy.RequiredFeatures | Where-Object status -CEQ 'qualified').Count
    requirementExceptions = @($policy.Exceptions)
    qualificationWorkflow = $readiness.qualificationWorkflow
    qualificationWorkflowPresent = (Test-Path -LiteralPath $workflow -PathType Leaf)
    requiredLanes = @($readiness.requiredLanes); requiredSuites = @($readiness.requiredSuites)
    publicationBlockedReasons = @($readiness.publicationBlockedReasons)
    requirements = $requirements
}
$path = [IO.Path]::GetFullPath($OutputPath)
$null = [IO.Directory]::CreateDirectory([IO.Path]::GetDirectoryName($path))
# CreateNew preserves earlier evidence even when two invocations race.
$stream = [IO.File]::Open($path, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write, [IO.FileShare]::None)
try {
    $bytes = [Text.UTF8Encoding]::new($false).GetBytes(($report | ConvertTo-Json -Depth 12) + [Environment]::NewLine)
    $stream.Write($bytes, 0, $bytes.Length)
} finally { $stream.Dispose() }
Write-Output "Release inventory saved to $path. This is a status report, not qualification evidence."
