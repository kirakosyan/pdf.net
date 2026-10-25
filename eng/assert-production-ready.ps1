[CmdletBinding()]
param(
    [ValidateSet('BeforePack','Publish')][string]$Stage = 'BeforePack',
    [string]$EvidenceDirectory = '',
    [string]$PackageDirectory = ''
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$readiness = Get-Content (Join-Path $root 'specs/release-readiness.json') -Raw | ConvertFrom-Json
$spec = Get-Content (Join-Path $root 'specs/requirements.json') -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'release-policy.ps1')
$policy = Get-FluentPdfReleasePolicy -Readiness $readiness -Specification $spec
if (!$readiness.productionReady) { throw 'Production publication blocked: readiness is false. Complete the production requirements and qualification evidence first.' }
if ($readiness.packageVersion -notmatch '^\d+\.\d+\.\d+$') { throw 'Only stable package versions are allowed.' }
$incomplete = @($policy.RequiredFeatures | Where-Object { $_.status -ne 'qualified' })
if ($incomplete.Count) { throw "Production publication blocked: $($incomplete.Count) SDK requirements are not qualified." }
if ([string]::IsNullOrWhiteSpace($EvidenceDirectory)) { throw 'Production evidence directory is required.' }
$evidenceRoot = [IO.Path]::GetFullPath($EvidenceDirectory)
$evidencePath = Join-Path $evidenceRoot 'release-evidence.json'
if (!(Test-Path -LiteralPath $evidencePath -PathType Leaf)) { throw 'Missing release-evidence.json.' }
$evidence = Get-Content -LiteralPath $evidencePath -Raw | ConvertFrom-Json
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot resolve source commit.' }
if ($evidence.schemaVersion -ne 1 -or $evidence.purpose -ne 'production-qualification') { throw 'Unrecognized qualification evidence.' }
if ($policy.Exceptions.Count) {
    if ($evidence.PSObject.Properties.Name -cnotcontains 'requirementExceptions') { throw 'Qualification evidence must disclose the release exception.' }
    $declaredExceptions = @($evidence.requirementExceptions)
    if ($declaredExceptions.Count -ne $policy.Exceptions.Count) { throw 'Qualification release exception inventory differs from policy.' }
    foreach ($exception in $policy.Exceptions) {
        $declared = @($declaredExceptions | Where-Object requirement -CEQ $exception.requirement)
        if ($declared.Count -ne 1) { throw 'Missing or duplicate qualification release exception.' }
        foreach ($field in @('requirement','issue','packageVersion','disposition','authorizedOn','reason')) {
            if ($declared[0].PSObject.Properties.Name -cnotcontains $field -or $declared[0].$field -cne $exception.$field) { throw 'Qualification release exception differs from policy.' }
        }
    }
} elseif ($evidence.PSObject.Properties.Name -contains 'requirementExceptions' -and @($evidence.requirementExceptions).Count) {
    throw 'Qualification evidence contains an unapproved release exception.'
}
if ($evidence.sourceCommit -cne $commit -or $evidence.packageVersion -cne $readiness.packageVersion) { throw 'Qualification source/version mismatch.' }
$expectedStage = if ($Stage -eq 'Publish') { 'package' } else { 'source' }
if ($evidence.stage -cne $expectedStage) { throw "Expected $expectedStage qualification evidence." }
function Assert-HashedFile([string]$baseDirectory, [string]$relativePath, [string]$sha256) {
    if ([IO.Path]::IsPathRooted($relativePath) -or [string]::IsNullOrWhiteSpace($relativePath) -or $sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Invalid report path/hash.' }
    $baseFull = [IO.Path]::GetFullPath($baseDirectory).TrimEnd([IO.Path]::DirectorySeparatorChar)
    $full = [IO.Path]::GetFullPath((Join-Path $baseFull $relativePath))
    $comparison = if ($IsWindows) { [StringComparison]::OrdinalIgnoreCase } else { [StringComparison]::Ordinal }
    if (!$full.StartsWith($baseFull + [IO.Path]::DirectorySeparatorChar, $comparison)) { throw 'Qualification path escapes its artifact root.' }
    if (!(Test-Path -LiteralPath $full -PathType Leaf)) { throw "Missing qualified artifact: $relativePath" }
    if ((Get-FileHash -LiteralPath $full -Algorithm SHA256).Hash -ine $sha256) { throw "Qualification artifact hash mismatch: $relativePath" }
}
$laneIds = @($evidence.lanes | ForEach-Object { $_.id })
if (@($laneIds | Sort-Object -Unique).Count -ne $laneIds.Count) { throw 'Duplicate qualification lanes.' }
foreach ($requiredLane in $readiness.requiredLanes) {
    $lane = @($evidence.lanes | Where-Object id -CEQ $requiredLane)
    if ($lane.Count -ne 1) { throw "Missing required qualification lane: $requiredLane" }
    if ([string]::IsNullOrWhiteSpace($lane[0].runtime) -or [string]::IsNullOrWhiteSpace($lane[0].os) -or [string]::IsNullOrWhiteSpace($lane[0].architecture)) { throw "Incomplete runtime identity: $requiredLane" }
    $suiteNames = @($lane[0].suites | ForEach-Object { $_.name })
    if (@($suiteNames | Sort-Object -Unique).Count -ne $suiteNames.Count) { throw "Duplicate suites: $requiredLane" }
    foreach ($requiredSuite in $readiness.requiredSuites) {
        $suite = @($lane[0].suites | Where-Object name -CEQ $requiredSuite)
        if ($suite.Count -ne 1) { throw "Missing suite $($requiredSuite): $requiredLane" }
        $s = $suite[0]
        if ($s.total -le 0 -or $s.passed -ne $s.total -or $s.failed -ne 0 -or $s.skipped -ne 0) { throw "Incomplete or failing suite $($requiredSuite): $requiredLane" }
        Assert-HashedFile $evidenceRoot $s.reportPath $s.sha256
        if ($requiredSuite -ceq 'performance') {
            Assert-HashedFile $evidenceRoot $s.baselinePath $s.baselineSha256
            $python = if ($IsWindows) { 'python' } else { 'python3' }
            & $python (Join-Path $PSScriptRoot 'performance.py') check --report (Join-Path $evidenceRoot $s.reportPath) --baseline (Join-Path $evidenceRoot $s.baselinePath) --production --source-commit $commit --lane $requiredLane --stage $expectedStage
            if ($LASTEXITCODE -ne 0) { throw "Performance regression, incomplete workload or invalid baseline: $requiredLane" }
        }
    }
}
$requirementIds = @($evidence.requirements | ForEach-Object { $_.id })
if (@($requirementIds | Sort-Object -Unique).Count -ne $requirementIds.Count) { throw 'Duplicate requirement evidence.' }
foreach ($exception in $policy.Exceptions) {
    if ($exception.requirement -cin $requirementIds) { throw 'A deferred requirement cannot be represented as qualified evidence.' }
}
foreach ($feature in $policy.RequiredFeatures) {
    $record = @($evidence.requirements | Where-Object id -CEQ $feature.id)
    if ($record.Count -ne 1 -or @($record[0].reports).Count -eq 0) { throw "Missing requirement evidence: $($feature.id)" }
    foreach ($report in $record[0].reports) { Assert-HashedFile $evidenceRoot $report.path $report.sha256 }
}
if ($Stage -eq 'Publish') {
    if ([string]::IsNullOrWhiteSpace($PackageDirectory)) { throw 'Exact package directory is required.' }
    $packages = @($evidence.packages)
    if ($packages.Count -eq 0) { throw 'No qualified packages.' }
    foreach ($package in $packages) { Assert-HashedFile $PackageDirectory $package.file $package.sha256 }
    $actual = @(Get-ChildItem -LiteralPath $PackageDirectory -File | Where-Object { $_.Extension -in @('.nupkg','.snupkg') } | ForEach-Object Name)
    $declared = @($packages | ForEach-Object file)
    if (@(Compare-Object $actual $declared).Count) { throw 'Qualified package inventory differs from candidate files.' }
    $core = "Fluent.Pdf.$($readiness.packageVersion).nupkg"
    $symbols = "Fluent.Pdf.$($readiness.packageVersion).snupkg"
    if ($core -notin $declared -or $symbols -notin $declared) { throw 'Qualified core package and symbols are required.' }
}
Write-Output "Production $Stage gate passed for $commit."
