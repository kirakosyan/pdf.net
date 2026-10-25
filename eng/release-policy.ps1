# Shared policy for the production gate, specification checks and release inventory.
function Get-FluentPdfReleasePolicy {
    [CmdletBinding()]
    param([Parameter(Mandatory)]$Readiness, [Parameter(Mandatory)]$Specification)
    $exceptions = @()
    if ($Readiness.PSObject.Properties.Name -contains 'requirementExceptions') {
        $exceptions = @($Readiness.requirementExceptions)
    }
    $seen = @{}
    foreach ($exception in $exceptions) {
        # This is the single user-authorized exception, not a general waiver switch.
        $fields = @('requirement','issue','packageVersion','disposition','authorizedOn','reason')
        if (@($exception.PSObject.Properties.Name).Count -ne $fields.Count) { throw 'Invalid release exception fields.' }
        foreach ($field in $fields) {
            if ($exception.PSObject.Properties.Name -cnotcontains $field) { throw "Incomplete release exception: $field" }
        }
        if ($exception.requirement -cne 'FP-089' -or $exception.issue -ne 93 -or
            $exception.packageVersion -cne '1.0.0' -or $Readiness.packageVersion -cne '1.0.0' -or
            $exception.disposition -cne 'deferred' -or $exception.authorizedOn -cne '2026-10-25' -or
            [string]::IsNullOrWhiteSpace($exception.reason)) { throw 'Unapproved or out-of-version release exception.' }
        if ($seen.ContainsKey($exception.requirement)) { throw 'Duplicate release exception.' }
        $feature = @($Specification.features | Where-Object id -CEQ $exception.requirement)
        if ($feature.Count -ne 1 -or !$feature[0].releaseRequired -or $feature[0].status -ceq 'qualified') {
            throw 'A deferred release requirement must remain required in the catalogue and unqualified.'
        }
        $seen[$exception.requirement] = $true
    }
    # Deferring issue #93 never removes automated security or resource checks.
    foreach ($suite in @('functional','fidelity','security','resource-limits','concurrency','performance','consumer')) {
        if (@($Readiness.requiredSuites | Where-Object { $_ -ceq $suite }).Count -ne 1) {
            throw "Missing or duplicate required production suite: $suite"
        }
    }
    [pscustomobject]@{
        Exceptions = $exceptions
        RequiredFeatures = @($Specification.features | Where-Object { $_.releaseRequired -and !$seen.ContainsKey($_.id) })
    }
}
