[CmdletBinding()]
param()
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$spec = Get-Content (Join-Path $root 'specs/requirements.json') -Raw | ConvertFrom-Json
$readiness = Get-Content (Join-Path $root 'specs/release-readiness.json') -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'release-policy.ps1')
$policy = Get-FluentPdfReleasePolicy -Readiness $readiness -Specification $spec
$ids = @{}
foreach ($feature in $spec.features) {
    if ($feature.id -notmatch '^FP-\d{3}$' -or $ids.ContainsKey($feature.id)) { throw "Invalid or duplicate requirement: $($feature.id)" }
    $ids[$feature.id] = $feature
    if ($feature.status -notin @('planned','implemented','qualified')) { throw "Invalid status: $($feature.id)" }
    if (@($feature.acceptance).Count -lt 3 -or [string]::IsNullOrWhiteSpace($feature.validation)) { throw "Incomplete acceptance: $($feature.id)" }
    $path = Join-Path $root "docs/requirements/$($feature.id).md"
    if (!(Test-Path -LiteralPath $path)) { throw "Missing requirement: $path" }
    $text = Get-Content -LiteralPath $path -Raw
    foreach ($expected in @($feature.scope) + @($feature.acceptance) + @($feature.validation)) {
        if (!$text.Contains($expected)) { throw "Requirement document drift: $($feature.id)" }
    }
    foreach ($dependency in $feature.dependencies) {
        if (!$text.Contains("[$dependency]($dependency.md)")) { throw "Dependency document drift: $($feature.id)" }
    }
}
$visits = @{}
function Visit-Requirement([string]$id) {
    if (!$ids.ContainsKey($id)) { throw "Unknown dependency: $id" }
    if ($visits.ContainsKey($id)) {
        if ($visits[$id] -eq 1) { throw "Dependency cycle at $id" }
        return
    }
    $visits[$id] = 1
    foreach ($dependency in $ids[$id].dependencies) { Visit-Requirement $dependency }
    $visits[$id] = 2
}
foreach ($id in $ids.Keys) { Visit-Requirement $id }
$assignmentOrder = (Get-Content (Join-Path $root 'specs/assignment-order.json') -Raw | ConvertFrom-Json).order
if ($assignmentOrder.Count -ne $ids.Count -or @($assignmentOrder | Sort-Object -Unique).Count -ne $ids.Count) { throw 'Assignment order is incomplete or duplicated.' }
$assigned = @{}
foreach ($id in $assignmentOrder) {
    if (!$ids.ContainsKey($id)) { throw "Unknown assignment: $id" }
    foreach ($dependency in $ids[$id].dependencies) {
        if (!$assigned.ContainsKey($dependency)) { throw "Assignment precedes dependency: $id before $dependency" }
    }
    $assigned[$id] = $true
}
$research = Get-Content (Join-Path $root 'specs/research-evidence.json') -Raw | ConvertFrom-Json
foreach ($record in @($research.packages) + @($research.reports) + @($research.documentation)) {
    if ($record.sha256 -notmatch '^[a-fA-F0-9]{64}$') { throw 'Missing research artifact digest.' }
}
foreach ($report in $research.reports) {
    if ($report.coverage -cne 'complete') { throw "Incomplete retained primary research selection: $($report.id)" }
}
$requirementFiles = @(Get-ChildItem (Join-Path $root 'docs/requirements') -Filter 'FP-*.md')
if ($requirementFiles.Count -ne $ids.Count) { throw 'Stale or missing requirement documents.' }
$reader = @($spec.features | Where-Object { !$_.releaseRequired })
if ($reader.Count -ne 1 -or $reader[0].id -ne 'FP-096') { throw 'Unexpected SDK release exclusion.' }
if ($readiness.productionReady -and @($policy.RequiredFeatures | Where-Object { $_.status -ne 'qualified' }).Count) { throw 'Readiness contradicts requirement status.' }

# Encode excluded source names so this check does not introduce those names into tracked text.
$excludedTerms = @(
    [string](-join (65,115,112,111,115,101 | ForEach-Object { [char]$_ })),
    [string](-join (73,114,111,110,80,100,102 | ForEach-Object { [char]$_ })),
    [string](-join (65,112,115,111,115,101 | ForEach-Object { [char]$_ }))
)
$paths = @(& git -C $root ls-files --cached --others --exclude-standard | Sort-Object -Unique)
if ($LASTEXITCODE -ne 0) { throw 'Cannot enumerate repository files.' }
foreach ($relative in $paths) {
    $path = Join-Path $root $relative
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { continue }
    $text = [IO.File]::ReadAllText($path)
    foreach ($term in $excludedTerms) {
        if ($text.IndexOf($term, [StringComparison]::OrdinalIgnoreCase) -ge 0 -or $relative.IndexOf($term, [StringComparison]::OrdinalIgnoreCase) -ge 0) {
            throw "Excluded reference identity in tracked content: $relative"
        }
    }
    if ($relative.EndsWith('.md')) {
        foreach ($match in [regex]::Matches($text, '\[[^\]]+\]\(([^)]+)\)')) {
            $target = $match.Groups[1].Value
            if ($target -match '^[a-zA-Z][a-zA-Z0-9+.-]*:|^#') { continue }
            $target = ($target -split '#', 2)[0].Trim('<','>')
            if (!$target) { continue }
            $resolved = [IO.Path]::GetFullPath((Join-Path (Split-Path $path -Parent) $target))
            if (!(Test-Path -LiteralPath $resolved)) { throw "Broken local link in $($relative): $target" }
        }
    }
}
$backlogPath = Join-Path $root 'specs/github-issues.json'
if (Test-Path -LiteralPath $backlogPath) {
    $backlog = Get-Content $backlogPath -Raw | ConvertFrom-Json
    $mapped = @($backlog.issues | ForEach-Object { $_.requirement })
    if ($mapped.Count -ne $ids.Count -or @($mapped | Sort-Object -Unique).Count -ne $ids.Count) { throw 'GitHub issue mapping is incomplete or duplicated.' }
    foreach ($id in $ids.Keys) { if ($id -notin $mapped) { throw "Missing issue mapping: $id" } }
}
Write-Output ("Specification validated: {0} requirements; {1} SDK release requirements; {2} repository files checked." -f $ids.Count, @($spec.features | Where-Object releaseRequired).Count, $paths.Count)
