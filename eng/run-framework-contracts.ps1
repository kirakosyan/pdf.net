#requires -Version 5.1
[CmdletBinding()]
param(
    [Parameter(Mandatory)][string]$TestAssembly,
    [Parameter(Mandatory)][string]$TestPlatformDirectory,
    [Parameter(Mandatory)][ValidateSet('x86','x64')][string]$Architecture,
    [Parameter(Mandatory)][string]$OutputDirectory,
    [switch]$RequireFramework472,
    [ValidateRange(10,14400)][int]$TimeoutSeconds = 3600
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
# Windows PowerShell is available on an offline minimum-Framework guest. A
# modern SDK/PowerShell installation must not upgrade that guest's Framework.
if ([Environment]::OSVersion.Platform -ne [PlatformID]::Win32NT) { throw 'An actual Windows Framework host is required.' }
$assembly = [IO.Path]::GetFullPath($TestAssembly)
$runner = Join-Path ([IO.Path]::GetFullPath($TestPlatformDirectory)) 'vstest.console.exe'
foreach ($path in @($assembly,$runner)) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw "Missing offline test input: $path" }
    if ($path.Contains('"')) { throw 'Double quotes are not allowed in test input paths.' }
}
$registry = [Microsoft.Win32.RegistryKey]::OpenBaseKey([Microsoft.Win32.RegistryHive]::LocalMachine,[Microsoft.Win32.RegistryView]::Registry64)
try {
    $key = $registry.OpenSubKey('SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full')
    if ($null -eq $key) { throw 'No installed Framework release was found.' }
    try { $release = [int]$key.GetValue('Release') } finally { $key.Dispose() }
} finally { $registry.Dispose() }
if ($RequireFramework472 -and $release -notin @(461808,461814)) { throw "Actual Framework 4.7.2 is required; installed release is $release." }
if (!$RequireFramework472 -and $release -lt 528040) { throw 'Use -RequireFramework472 for the minimum lane; the current lane requires Framework 4.8 or later.' }
if ($Architecture -eq 'x64' -and ![Environment]::Is64BitOperatingSystem) { throw 'The x64 lane requires a 64-bit Windows host.' }
$directory = [IO.Path]::GetFullPath($OutputDirectory)
if ($directory.Contains('"') -or (Test-Path -LiteralPath $directory)) { throw 'Use a fresh owned output directory without double quotes.' }
$null = [IO.Directory]::CreateDirectory($directory)
$report = Join-Path $directory 'runtime-contracts.trx'
$statusPath = Join-Path $directory 'framework-execution.json'
$status = [ordered]@{
    schemaVersion=1; purpose='offline-framework-source-execution'; startedUtc=[DateTime]::UtcNow.ToString('o')
    architecture=$Architecture; requireFramework472=[bool]$RequireFramework472; installedFrameworkRelease=$release
    testAssemblySha256=(Get-FileHash -LiteralPath $assembly -Algorithm SHA256).Hash.ToLowerInvariant()
    runnerSha256=(Get-FileHash -LiteralPath $runner -Algorithm SHA256).Hash.ToLowerInvariant()
    timeoutSeconds=$TimeoutSeconds; filtered=$false; executionPassed=$false; exitCode=$null; timedOut=$false
    counters=$null; runtimeIdentity=$null; error=$null; reports=@()
    runtimeConsistencyChecked=$false; exactPackageConsumersExecuted=$false; trustedHostProvenanceQualified=$false; productionQualified=$false
}
$previous = $env:FLUENT_PDF_EXPECTED_ARCH
$env:FLUENT_PDF_EXPECTED_ARCH = $Architecture
$process = $null
try {
    # No test filter, restore, SDK, registry edits, or implicit JIT override.
    $arguments = @('"' + $assembly + '"', '/Platform:' + $Architecture,
        '/Framework:.NETFramework,Version=v4.7.2', '/InIsolation',
        '/TestAdapterPath:"' + [IO.Path]::GetDirectoryName($assembly) + '"',
        '/ResultsDirectory:"' + $directory + '"', '/Logger:"trx;LogFileName=runtime-contracts.trx"')
    $process = Start-Process -FilePath $runner -ArgumentList ($arguments -join ' ') -WorkingDirectory ([IO.Path]::GetDirectoryName($assembly)) -WindowStyle Hidden -PassThru -RedirectStandardOutput (Join-Path $directory 'stdout.log') -RedirectStandardError (Join-Path $directory 'stderr.log')
    # Retain the handle before waiting: Windows PowerShell 5.1 can otherwise
    # lose ExitCode for a process returned by Start-Process.
    $null = $process.Handle
    if (!$process.WaitForExit($TimeoutSeconds * 1000)) {
        $status.timedOut = $true
        # Only this invocation's process tree; retain every partial report.
        & "$env:SystemRoot\System32\taskkill.exe" /PID $process.Id /T /F | Out-Null
        if ($LASTEXITCODE -ne 0 -and !$process.HasExited) { throw 'The owned test process could not be terminated after its deadline.' }
        throw 'The complete Framework suite exceeded its external deadline.'
    }
    $process.WaitForExit()
    $status.exitCode = $process.ExitCode
    if (!(Test-Path -LiteralPath $report -PathType Leaf)) { throw 'The test runner produced no TRX report.' }
    if ((Get-Item -LiteralPath $report).Length -gt 67108864) { throw 'TRX report exceeds the evidence extent limit.' }
    $settings = New-Object Xml.XmlReaderSettings
    $settings.DtdProcessing = [Xml.DtdProcessing]::Prohibit
    $settings.XmlResolver = $null
    $reader = [Xml.XmlReader]::Create($report,$settings)
    try { $trx = New-Object Xml.XmlDocument; $trx.XmlResolver=$null; $trx.Load($reader) } finally { $reader.Dispose() }
    $namespace = New-Object Xml.XmlNamespaceManager($trx.NameTable)
    $namespace.AddNamespace('t','http://microsoft.com/schemas/VisualStudio/TeamTest/2010')
    $summary = $trx.SelectSingleNode('/t:TestRun/t:ResultSummary',$namespace)
    $counts = $trx.SelectSingleNode('/t:TestRun/t:ResultSummary/t:Counters',$namespace)
    if ($null -eq $counts -or $null -eq $summary) { throw 'Missing complete test counters.' }
    $status.counters = [ordered]@{}
    foreach ($attribute in $counts.Attributes) { $status.counters[$attribute.Name] = [int]$attribute.Value }
    $results = @($trx.SelectNodes('/t:TestRun/t:Results/t:UnitTestResult',$namespace))
    $identities = @($results | Where-Object { $_.GetAttribute('testName').EndsWith('RuntimeIdentityTests.TheRequestedLibraryAssetIsActuallyLoaded') })
    if ($identities.Count -eq 1) {
        $output = $identities[0].SelectSingleNode('t:Output/t:StdOut',$namespace)
        if ($null -ne $output) {
            $lines = @($output.InnerText -split '\r?\n' | Where-Object { $_.StartsWith('Runtime evidence: ') })
            if ($lines.Count -eq 1) { $status.runtimeIdentity = $lines[0].Substring(18) | ConvertFrom-Json }
        }
    }
    if ($process.ExitCode -ne 0 -or $summary.GetAttribute('outcome') -notin @('Passed','Completed') -or [int]$counts.total -le 0 -or [int]$counts.passed -ne [int]$counts.total -or $results.Count -ne [int]$counts.total -or @($results | Where-Object { $_.GetAttribute('outcome') -ne 'Passed' }).Count -ne 0) { throw 'The full Framework source suite did not pass; retain the TRX and logs.' }
    foreach ($name in @('failed','notExecuted','aborted','inconclusive','passedButRunAborted','error','timeout','notRunnable','disconnected')) {
        if ($counts.HasAttribute($name) -and [int]$counts.GetAttribute($name) -ne 0) { throw 'A required failure or skip was recorded.' }
    }
    $identity = $status.runtimeIdentity
    if ($null -eq $identity -or $identity.osFamily -cne 'win' -or $identity.processArchitecture -ine $Architecture -or $identity.coreLibrary.name -cne 'mscorlib' -or $identity.frameworkRelease -ne $release -or !$identity.runtime.StartsWith('.NET Framework ')) { throw 'The actual test-process Framework identity does not match the requested lane.' }
    if ($RequireFramework472 -and (!$identity.runtime.StartsWith('.NET Framework 4.7.') -or !$identity.coreLibrary.fileVersion.StartsWith('4.7.'))) { throw 'The actual test process is not minimum Framework 4.7.2.' }
    $status.executionPassed = $true
} catch {
    $status.error = $_.Exception.Message
    throw
} finally {
    $env:FLUENT_PDF_EXPECTED_ARCH = $previous
    if ($null -ne $process) { $process.Dispose() }
    $status.reports = @(Get-ChildItem -LiteralPath $directory -File | ForEach-Object { [ordered]@{name=$_.Name;bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant()} })
    $status | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $statusPath -Encoding UTF8
}
Write-Output "Offline Framework source execution passed. Run check-runtime-evidence.py against $report on the analysis host; production qualification remains separate."
