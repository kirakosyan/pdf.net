#requires -Version 7.2
[CmdletBinding()]
param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
if (!$OutputDirectory) { $OutputDirectory = Join-Path $root ('artifacts/release-policy/' + [Guid]::NewGuid().ToString('N')) }
$directory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $directory) { throw 'Use a fresh policy regression output directory.' }
$null = [IO.Directory]::CreateDirectory($directory)
$readiness = Get-Content -LiteralPath (Join-Path $root 'specs/release-readiness.json') -Raw | ConvertFrom-Json
$spec = Get-Content -LiteralPath (Join-Path $root 'specs/requirements.json') -Raw | ConvertFrom-Json
. (Join-Path $PSScriptRoot 'release-policy.ps1')
. (Join-Path $PSScriptRoot 'windows-framework-policy.ps1')
$passed = [Collections.Generic.List[string]]::new()
function Copy-Value($value) { $value | ConvertTo-Json -Depth 25 | ConvertFrom-Json }
function Check([string]$name,[scriptblock]$action) {
    & $action
    $passed.Add($name)
}
function Reject-Policy([string]$name,[scriptblock]$mutate) {
    $r = Copy-Value $readiness
    $s = Copy-Value $spec
    & $mutate $r $s
    $rejected = $false
    try { $null = Get-FluentPdfReleasePolicy -Readiness $r -Specification $s } catch { $rejected = $true }
    if (!$rejected) { throw "Invalid policy accepted: $name" }
    $passed.Add($name)
}
Check 'only-authorized-requirement-deferred' {
    $policy = Get-FluentPdfReleasePolicy -Readiness $readiness -Specification $spec
    if ($policy.RequiredFeatures.Count -ne 110 -or $policy.Exceptions.Count -ne 1 -or
        'FP-089' -cin @($policy.RequiredFeatures.id) -or @($spec.features | Where-Object releaseRequired).Count -ne 111) {
        throw 'The catalogue or active requirement scope changed unexpectedly.'
    }
}
Check 'no-exception-restores-entire-catalogue' {
    $r = Copy-Value $readiness
    $r.PSObject.Properties.Remove('requirementExceptions')
    $policy = Get-FluentPdfReleasePolicy -Readiness $r -Specification $spec
    if ($policy.RequiredFeatures.Count -ne 111 -or $policy.Exceptions.Count -ne 0) { throw 'Missing exception silently exempted a requirement.' }
}
Reject-Policy 'unapproved-requirement' { param($r,$s) $r.requirementExceptions[0].requirement = 'FP-003' }
Reject-Policy 'duplicate-exception' { param($r,$s) $r.requirementExceptions = @($r.requirementExceptions) + @($r.requirementExceptions[0]) }
Reject-Policy 'next-version-cannot-inherit-exception' { param($r,$s) $r.packageVersion = '1.0.1' }
Reject-Policy 'exception-version-cannot-expand' { param($r,$s) $r.requirementExceptions[0].packageVersion = '1.0.1' }
Reject-Policy 'wrong-issue' { param($r,$s) $r.requirementExceptions[0].issue = 94 }
Reject-Policy 'missing-reason' { param($r,$s) $r.requirementExceptions[0].reason = ' ' }
Reject-Policy 'missing-authorization' { param($r,$s) $r.requirementExceptions[0].PSObject.Properties.Remove('authorizedOn') }
Reject-Policy 'false-qualified-status' { param($r,$s) ($s.features | Where-Object id -CEQ 'FP-089').status = 'qualified' }
Reject-Policy 'catalogue-scope-cannot-disappear' { param($r,$s) ($s.features | Where-Object id -CEQ 'FP-089').releaseRequired = $false }
Reject-Policy 'automated-security-remains-mandatory' { param($r,$s) $r.requiredSuites = @($r.requiredSuites | Where-Object { $_ -cne 'security' }) }
Reject-Policy 'resource-checks-remain-mandatory' { param($r,$s) $r.requiredSuites = @($r.requiredSuites | Where-Object { $_ -cne 'resource-limits' }) }
Reject-Policy 'duplicate-security-suite' { param($r,$s) $r.requiredSuites += 'security' }
foreach ($case in @(
    @{release=461808;arch='X64';expected=@('win-x86-framework472','win-x64-framework472')},
    @{release=461814;arch='X64';expected=@('win-x86-framework472','win-x64-framework472')},
    @{release=528040;arch='X64';expected=@('win-x86-framework-current','win-x64-framework-current')},
    @{release=533320;arch='X64';expected=@('win-x86-framework-current','win-x64-framework-current')},
    @{release=461308;arch='X64';expected=@()},
    @{release=0;arch='X64';expected=@()},
    @{release=533320;arch='Arm64';expected=@()}
)) {
    $actual = @(Get-FluentPdfFrameworkHostLanes -Release $case.release -OSArchitecture $case.arch)
    if (($actual -join ',') -cne ($case.expected -join ',')) { throw 'Framework host classification is incorrect.' }
    $passed.Add("synthetic-framework-classification-$($case.release)-$($case.arch)")
}
function Reject-Script([string]$name,[string]$scriptPath,[string[]]$arguments,[string]$message) {
    $output = & pwsh -NoProfile -File $scriptPath @arguments 2>&1 | Out-String
    $exitCode = $LASTEXITCODE
    $output | Set-Content -LiteralPath (Join-Path $directory ($name + '.log')) -Encoding utf8
    if ($exitCode -eq 0 -or !$output.Contains($message)) { throw "Expected rejection missing: $name" }
    $passed.Add($name)
}
foreach ($stage in @('BeforePack','Publish')) {
    $expected = if (!$readiness.productionReady) { 'readiness is false' } else { 'Production evidence directory is required' }
    Reject-Script "actual-$stage-without-evidence-remains-blocked" (Join-Path $PSScriptRoot 'assert-production-ready.ps1') @('-Stage',$stage) $expected
}
# These isolated policy fixtures contain no SDK binaries, test reports or packages.
# Copying the entry point tests real gate ordering without changing repository state.
$fixture = Join-Path $directory 'synthetic-policy-fixture'
$null = [IO.Directory]::CreateDirectory((Join-Path $fixture 'eng'))
$null = [IO.Directory]::CreateDirectory((Join-Path $fixture 'specs'))
$null = [IO.Directory]::CreateDirectory((Join-Path $fixture 'evidence'))
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'assert-production-ready.ps1'),(Join-Path $PSScriptRoot 'release-policy.ps1') -Destination (Join-Path $fixture 'eng')
$r = Copy-Value $readiness
$r.productionReady = $true
$r | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath (Join-Path $fixture 'specs/release-readiness.json') -Encoding utf8
$s = Copy-Value $spec
foreach ($feature in $s.features) { $feature.status = 'planned' }
$s | ConvertTo-Json -Depth 25 | Set-Content -LiteralPath (Join-Path $fixture 'specs/requirements.json') -Encoding utf8
$gate = Join-Path $fixture 'eng/assert-production-ready.ps1'
$gateArguments = @('-Stage','BeforePack','-EvidenceDirectory',(Join-Path $fixture 'evidence'))
Reject-Script 'synthetic-other-110-requirements-still-block' $gate $gateArguments '110 SDK requirements are not qualified'
$s = Copy-Value $spec
foreach ($feature in $s.features) { if ($feature.id -cne 'FP-089') { $feature.status = 'qualified' } }
$s | ConvertTo-Json -Depth 25 | Set-Content -LiteralPath (Join-Path $fixture 'specs/requirements.json') -Encoding utf8
$commit = (& git -C $root rev-parse HEAD).Trim()
if ($LASTEXITCODE -ne 0) { throw 'Cannot identify the policy fixture parent commit.' }
$evidence = [ordered]@{schemaVersion=1;purpose='production-qualification';sourceCommit=$commit;packageVersion='1.0.0';stage='source';lanes=@();requirements=@()}
$evidencePath = Join-Path $fixture 'evidence/release-evidence.json'
$evidence | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $evidencePath -Encoding utf8
Reject-Script 'synthetic-undisclosed-exception-rejected' $gate $gateArguments 'must disclose the release exception'
$evidence.requirementExceptions = @($r.requirementExceptions)
$evidence | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $evidencePath -Encoding utf8
Reject-Script 'synthetic-missing-lanes-still-rejected' $gate $gateArguments 'Missing required qualification lane'
$evidence.requirementExceptions[0].reason = 'different'
$evidence | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $evidencePath -Encoding utf8
Reject-Script 'synthetic-changed-disclosure-rejected' $gate $gateArguments 'differs from policy'
$evidence.requirementExceptions = @($r.requirementExceptions) + @($r.requirementExceptions)
$evidence | ConvertTo-Json -Depth 20 | Set-Content -LiteralPath $evidencePath -Encoding utf8
Reject-Script 'synthetic-extra-disclosure-rejected' $gate $gateArguments 'inventory differs from policy'
$inventoryPath = Join-Path $directory 'release-status.json'
& (Join-Path $PSScriptRoot 'get-release-status.ps1') -OutputPath $inventoryPath
$inventory = Get-Content -LiteralPath $inventoryPath -Raw | ConvertFrom-Json
if ($inventory.activeReleaseRequirements -ne 110 -or $inventory.requirements.Count -ne 110 -or
    $inventory.qualificationEvidenceValidated -or $inventory.sourceTestsExecuted -or $inventory.packageConsumersExecuted -or
    $inventory.requiredLanes.Count -ne 22 -or $inventory.requiredSuites.Count -ne 7 -or
    @($inventory.requirements | Where-Object { $null -eq $_.issue }).Count) { throw 'Release inventory lost required scope or misrepresented execution.' }
$passed.Add('actual-inventory-preserves-scope-and-execution-boundary')
$originalHash = (Get-FileHash -LiteralPath $inventoryPath).Hash
Reject-Script 'inventory-cannot-overwrite-evidence' (Join-Path $PSScriptRoot 'get-release-status.ps1') @('-OutputPath',$inventoryPath) 'already exists'
if ((Get-FileHash -LiteralPath $inventoryPath).Hash -cne $originalHash) { throw 'The earlier inventory was overwritten.' }
[ordered]@{schemaVersion=1;purpose='release-policy-and-synthetic-boundary-regressions';passed=@($passed);total=$passed.Count;failed=0;sdkQualificationExecuted=$false;productionQualified=$false} |
    ConvertTo-Json -Depth 6 | Set-Content -LiteralPath (Join-Path $directory 'regressions.json') -Encoding utf8
Write-Output "Release policy and inventory regressions passed: $($passed.Count). No SDK qualification inferred."
# Expected negative subprocesses leave LASTEXITCODE nonzero. GitHub Actions'
# PowerShell wrapper propagates it unless this successful script clears it.
exit 0
