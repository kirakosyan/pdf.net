#requires -Version 5.1
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$ownedRoot = 'C:\FluentPdfSourceVM'
$results = Join-Path $ownedRoot 'results'
if (Test-Path -LiteralPath $ownedRoot) { throw 'Use a fresh disposable guest; existing VM evidence is not overwritten.' }
$null = [IO.Directory]::CreateDirectory($results)
$status = [ordered]@{
    schemaVersion=1;purpose='framework472-vm-source-execution';sourceCommit=$null
    passed=$false;stage='setup';error=$null;installedFrameworkRelease=$null;runtimeOverrideNames=@()
    cppRuntimes=@();lanes=@();productionQualified=$false;exactPackageConsumers=$false
}
$exitCode = 1
Start-Transcript -LiteralPath (Join-Path $results 'setup.log') | Out-Null
try {
    $manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'setup.json') -Raw | ConvertFrom-Json
    if ($manifest.schemaVersion -ne 1 -or $manifest.purpose -cne 'framework472-vm-source-setup' -or $manifest.productionQualified -ne $false -or $manifest.exactPackageConsumers -ne $false) { throw 'Not a source VM setup.' }
    $status.sourceCommit = $manifest.sourceCommit
    foreach ($file in $manifest.files) {
        if ($file.path -notin @('source-bundle.zip','qualify.ps1','install.bat','baseline-x86.trx','baseline-x64.trx','baseline-x86-execution.json','baseline-x64-execution.json','vc_redist.x86.exe','vc_redist.x64.exe')) { throw 'Unexpected setup file.' }
        $path = Join-Path $PSScriptRoot $file.path
        $actualBytes = (Get-Item -LiteralPath $path).Length
        $actualHash = (Get-FileHash -LiteralPath $path).Hash.ToLowerInvariant()
        if ($actualBytes -ne $file.bytes -or $actualHash -ine $file.sha256) { throw "VM setup input hash mismatch: $($file.path), bytes $actualBytes/$($file.bytes), SHA256 $actualHash/$($file.sha256)." }
    }
    $status.runtimeOverrideNames = @([Environment]::GetEnvironmentVariables().Keys | Where-Object { ([string]$_).StartsWith('COMPlus_',[StringComparison]::OrdinalIgnoreCase) })
    if ($status.runtimeOverrideNames.Count) { throw 'Runtime/JIT overrides are not allowed for this source execution.' }
    $release = [int](Get-ItemPropertyValue -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release)
    $status.installedFrameworkRelease = $release
    if ($release -notin @(461808,461814)) { throw 'Actual Framework 4.7.2 is required; do not change its registry identity.' }
    foreach ($architecture in @('x86','x64')) {
        $installer = Join-Path $PSScriptRoot "vc_redist.$architecture.exe"
        $signature = Get-AuthenticodeSignature -LiteralPath $installer
        if ($signature.Status -ne 'Valid' -or $signature.SignerCertificate.Subject -notmatch '(^|, )CN=Microsoft Corporation(,|$)') { throw 'C++ prerequisite signature verification failed.' }
        $process = Start-Process -FilePath $installer -ArgumentList '/install /quiet /norestart' -PassThru -Wait
        try {
            if ($process.ExitCode -notin @(0,1638)) { throw "C++ prerequisite installation failed or requires a reboot: $($process.ExitCode)" }
            $status.cppRuntimes += [ordered]@{architecture=$architecture;exitCode=$process.ExitCode;sha256=(Get-FileHash -LiteralPath $installer).Hash.ToLowerInvariant()}
        } finally { $process.Dispose() }
    }
    if ([int](Get-ItemPropertyValue -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release) -ne $release) { throw 'The minimum Framework installation changed during setup.' }
    # Enable the source worker's existing Windows network-isolation prerequisite.
    Set-NetFirewallProfile -Profile Domain,Public,Private -Enabled True
    $bundle = Join-Path $ownedRoot 'immutable-source-bundle'
    Expand-Archive -LiteralPath (Join-Path $PSScriptRoot 'source-bundle.zip') -DestinationPath $bundle
    if ((Get-FileHash -LiteralPath (Join-Path $bundle 'bundle.json')).Hash -ine $manifest.bundleManifestSha256) { throw 'Source bundle manifest mismatch.' }
    $status.stage = 'tests'
    $failures = @()
    foreach ($architecture in @('x86','x64')) {
        $workspace = Join-Path $ownedRoot "workspace-$architecture"
        $laneOutput = Join-Path $results $architecture
        try {
            & (Join-Path $bundle 'run-framework-bundle.ps1') -Architecture $architecture -RequireFramework472 -WorkspaceDirectory $workspace -OutputDirectory $laneOutput -TimeoutSeconds 1800
            $status.lanes += [ordered]@{architecture=$architecture;passed=$true}
        } catch {
            $failures += $architecture
            $status.lanes += [ordered]@{architecture=$architecture;passed=$false;error=$_.Exception.Message}
        }
        # Preserve source fixture outputs separately from the immutable input.
        $fixtures = Join-Path $workspace 'artifacts/contracts'
        if (Test-Path -LiteralPath $fixtures -PathType Container) {
            Copy-Item -LiteralPath $fixtures -Destination (Join-Path $results "fixtures-$architecture") -Recurse
        }
    }
    if ($failures.Count) { throw "Source execution failed for: $($failures -join ', ')" }
    $status.passed = $true
    $status.stage = 'complete'
    $exitCode = 0
} catch {
    $status.error = $_.Exception.Message
    Write-Output $_
} finally {
    $status | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath (Join-Path $results 'status.json') -Encoding UTF8
    Stop-Transcript | Out-Null
    $share = '\\host.lan\Data'
    $deadline = [Diagnostics.Stopwatch]::StartNew()
    while (!(Test-Path -LiteralPath $share) -and $deadline.Elapsed.TotalSeconds -lt 120) { Start-Sleep -Seconds 2 }
    if (Test-Path -LiteralPath $share) {
        $export = Join-Path $share 'results'
        if (Test-Path -LiteralPath $export) { throw 'The evidence export already exists.' }
        $null = [IO.Directory]::CreateDirectory($export)
        Get-ChildItem -LiteralPath $results -Force | ForEach-Object { Copy-Item -LiteralPath $_.FullName -Destination $export -Recurse }
        $marker = Join-Path $export 'export-complete.tmp'
        [IO.File]::WriteAllText($marker,(@{exitCode=$exitCode;sourceCommit=$status.sourceCommit} | ConvertTo-Json),(New-Object Text.UTF8Encoding($false)))
        Move-Item -LiteralPath $marker -Destination (Join-Path $export 'export-complete.json')
    } else { $exitCode = 1 }
}
exit $exitCode
