#requires -Version 5.1
# Synthetic launcher regressions only. These do not execute or qualify the SDK.
[CmdletBinding()]
param([Parameter(Mandatory)][string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$directory = [IO.Path]::GetFullPath($OutputDirectory)
if (Test-Path -LiteralPath $directory) { throw 'Use a fresh regression directory.' }
$null = [IO.Directory]::CreateDirectory($directory)
$source = @'
using System;
using System.IO;
using System.Threading;
class Fixture {
    static int Main(string[] args) {
        if (args.Length == 1 && !args[0].StartsWith("/")) {
            Directory.CreateDirectory(args[0]);
            File.WriteAllText(Path.Combine(args[0], "synthetic-core-consumer.txt"), "synthetic launcher only");
            return Environment.GetEnvironmentVariable("FP_CORE_CONSUMER_EXIT_CODE") == "17" ? 17 : 0;
        }
        string mode = Environment.GetEnvironmentVariable("FP_RUNNER_FIXTURE_MODE");
        if (mode == "timeout") { Thread.Sleep(60000); return 0; }
        string directory = null;
        foreach (string arg in args) if (arg.StartsWith("/ResultsDirectory:")) directory = arg.Substring(18);
        File.Copy(Environment.GetEnvironmentVariable("FP_RUNNER_FIXTURE_TRX"), Path.Combine(directory, "runtime-contracts.trx"));
        return mode == "exit-failure" ? 1 : 0;
    }
}
'@
$code = Join-Path $directory 'fixture.cs'
[IO.File]::WriteAllText($code,$source)
$compiler = Join-Path $env:SystemRoot 'Microsoft.NET/Framework/v4.0.30319/csc.exe'
& $compiler /nologo /target:exe ('/out:' + (Join-Path $directory 'vstest.console.exe')) $code
if ($LASTEXITCODE -ne 0) { throw 'Synthetic runner fixture build failed.' }
$script = Join-Path $PSScriptRoot 'run-framework-contracts.ps1'
$release = Get-ItemPropertyValue -LiteralPath 'HKLM:\SOFTWARE\Microsoft\NET Framework Setup\NDP\v4\Full' -Name Release
$previousMode = $env:FP_RUNNER_FIXTURE_MODE
$previousTrx = $env:FP_RUNNER_FIXTURE_TRX
$previousArch = $env:FLUENT_PDF_EXPECTED_ARCH
$previousCoreExit = $env:FP_CORE_CONSUMER_EXIT_CODE
$passed = @()
try {
    foreach ($case in @('pass','exit-failure','failed','skipped','missing-result','missing-identity','wrong-architecture','timeout')) {
        $identity = @{osFamily='win';processArchitecture=$(if($case -eq 'wrong-architecture'){'X86'}else{'X64'});coreLibrary=@{name='mscorlib';fileVersion='synthetic'};frameworkRelease=$release;runtime='.NET Framework synthetic'} | ConvertTo-Json -Compress
        $name = if ($case -eq 'missing-identity') {'SyntheticFixture'} else {'RuntimeIdentityTests.TheRequestedLibraryAssetIsActuallyLoaded'}
        $result = if ($case -eq 'failed') {'Failed'} elseif ($case -eq 'skipped') {'NotExecuted'} else {'Passed'}
        $results = if ($case -eq 'missing-result') {''} else { '<UnitTestResult testName="' + $name + '" outcome="' + $result + '"><Output><StdOut>Runtime evidence: ' + [Security.SecurityElement]::Escape($identity) + '</StdOut></Output></UnitTestResult>' }
        $xml = '<TestRun xmlns="http://microsoft.com/schemas/VisualStudio/TeamTest/2010"><Results>' + $results + '</Results><ResultSummary outcome="Completed"><Counters total="1" passed="1" failed="0" notExecuted="0" /></ResultSummary></TestRun>'
        $env:FP_RUNNER_FIXTURE_TRX = Join-Path $directory ($case + '.trx')
        [IO.File]::WriteAllText($env:FP_RUNNER_FIXTURE_TRX,$xml)
        $env:FP_RUNNER_FIXTURE_MODE = $case
        $env:FLUENT_PDF_EXPECTED_ARCH = 'preserved-parent-value'
        $output = Join-Path $directory $case
        $failed = $false
        try { & $script -TestAssembly $code -TestPlatformDirectory $directory -Architecture x64 -OutputDirectory $output -TimeoutSeconds 10 } catch { $failed = $true }
        $report = Get-Content -LiteralPath (Join-Path $output 'framework-execution.json') -Raw | ConvertFrom-Json
        if ($failed -ne ($case -ne 'pass') -or $report.executionPassed -ne ($case -eq 'pass') -or $report.productionQualified -ne $false -or $report.runtimeConsistencyChecked -ne $false -or $env:FLUENT_PDF_EXPECTED_ARCH -cne 'preserved-parent-value') { throw "Synthetic launcher regression failed: $case" }
        if ($case -eq 'timeout' -and !$report.timedOut) { throw 'External deadline was not recorded.' }
        if ($case -ne 'timeout' -and !(Test-Path -LiteralPath (Join-Path $output 'runtime-contracts.trx'))) { throw 'Failure evidence was not retained.' }
        $passed += $case
    }
    $existing = Join-Path $directory 'pass'
    $hash = (Get-FileHash -LiteralPath (Join-Path $existing 'framework-execution.json')).Hash
    $rejected = $false
    try { & $script -TestAssembly $code -TestPlatformDirectory $directory -Architecture x64 -OutputDirectory $existing } catch { $rejected=$true }
    if (!$rejected -or (Get-FileHash -LiteralPath (Join-Path $existing 'framework-execution.json')).Hash -cne $hash) { throw 'Existing evidence was overwritten.' }
    $passed += 'existing-evidence'
    if ($release -notin @(461808,461814)) {
        $rejected=$false
        $minimum=Join-Path $directory 'false-minimum'
        try { & $script -TestAssembly $code -TestPlatformDirectory $directory -Architecture x64 -OutputDirectory $minimum -RequireFramework472 } catch { $rejected=$true }
        if (!$rejected -or (Test-Path -LiteralPath $minimum)) { throw 'A newer Framework was accepted as the minimum.' }
        $passed += 'newer-framework-rejection'
    }
    # Exercise the immutable transfer boundary with a synthetic runner, including
    # repeat execution into separate workspaces and rejection before any launch.
    $bundle=Join-Path $directory 'bundle'
    $runnerDirectory=Join-Path $bundle 'testplatform/tools/net462/Common7/IDE/Extensions/TestPlatform'
    $null=[IO.Directory]::CreateDirectory($runnerDirectory)
    $null=[IO.Directory]::CreateDirectory((Join-Path $bundle 'tests'))
    Copy-Item -LiteralPath (Join-Path $directory 'vstest.console.exe') -Destination $runnerDirectory
    Copy-Item -LiteralPath $code -Destination (Join-Path $bundle 'tests/Fluent.Pdf.Contract.Tests.dll')
    Copy-Item -LiteralPath $script,(Join-Path $PSScriptRoot 'run-framework-bundle.ps1') -Destination $bundle
    $bundlePrefix=$bundle.TrimEnd('\')+'\'
    $files=@(Get-ChildItem -LiteralPath $bundle -Recurse -File | ForEach-Object { @{path=$_.FullName.Substring($bundlePrefix.Length).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()} })
    $manifest=@{schemaVersion=1;purpose='offline-framework-source-bundle';sourceCommit=('a'*40);files=$files;productionQualified=$false;exactPackageConsumers=$false}
    $manifestPath=Join-Path $bundle 'bundle.json'
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    $env:FP_RUNNER_FIXTURE_MODE='pass'
    $env:FP_RUNNER_FIXTURE_TRX=Join-Path $directory 'pass.trx'
    foreach($attempt in 1..2) {
        & (Join-Path $bundle 'run-framework-bundle.ps1') -Architecture x64 -WorkspaceDirectory (Join-Path $directory "bundle-work-$attempt") -OutputDirectory (Join-Path $directory "bundle-output-$attempt")
    }
    $passed+='repeat-immutable-bundle'
    # Exercise the actual supplemental launch path with a fast synthetic
    # success and a nonzero exit. Neither executes or qualifies the SDK.
    $coreDirectory=Join-Path $bundle 'core-release/framework/x64'
    $null=[IO.Directory]::CreateDirectory($coreDirectory)
    Copy-Item -LiteralPath (Join-Path $directory 'vstest.console.exe') -Destination (Join-Path $coreDirectory 'CoreConsumer.exe')
    $files=@(Get-ChildItem -LiteralPath $bundle -Recurse -File | Where-Object FullName -ne $manifestPath | ForEach-Object { @{path=$_.FullName.Substring($bundlePrefix.Length).Replace('\','/');bytes=$_.Length;sha256=(Get-FileHash -LiteralPath $_.FullName).Hash.ToLowerInvariant()} })
    $manifest.files=$files
    $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    $env:FP_CORE_CONSUMER_EXIT_CODE='0'
    & (Join-Path $bundle 'run-framework-bundle.ps1') -Architecture x64 -WorkspaceDirectory (Join-Path $directory 'core-success-work') -OutputDirectory (Join-Path $directory 'core-success-output')
    $passed+='core-fast-success'
    $env:FP_CORE_CONSUMER_EXIT_CODE='17'
    $rejected=$false
    try { & (Join-Path $bundle 'run-framework-bundle.ps1') -Architecture x64 -WorkspaceDirectory (Join-Path $directory 'core-failure-work') -OutputDirectory (Join-Path $directory 'core-failure-output') } catch { $rejected=$_.Exception.Message -like '*consumer failed: exit code 17*' }
    if (!$rejected -or !(Test-Path -LiteralPath (Join-Path $directory 'core-failure-output/core-consumer/synthetic-core-consumer.txt'))) { throw 'A nonzero supplemental consumer exit was not retained/rejected.' }
    $passed+='core-nonzero-rejection'
    $env:FP_CORE_CONSUMER_EXIT_CODE='0'
    foreach($case in @('changed-input','additional-input','escaping-path','duplicate-path')) {
        $manifest.files=$files
        $target=Join-Path $bundle 'tests/Fluent.Pdf.Contract.Tests.dll'
        if($case -eq 'changed-input'){[IO.File]::AppendAllText($target,'changed')}
        if($case -eq 'additional-input'){[IO.File]::WriteAllText((Join-Path $bundle 'extra.dll'),'unmanifested')}
        if($case -eq 'escaping-path'){$manifest.files=@(@{path='../escape';bytes=0;sha256=('a'*64)})}
        if($case -eq 'duplicate-path'){$manifest.files=@($files)+@($files[0])}
        $manifest | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
        $workspace=Join-Path $directory ('rejected-'+$case)
        $rejected=$false
        try { & (Join-Path $bundle 'run-framework-bundle.ps1') -Architecture x64 -WorkspaceDirectory $workspace -OutputDirectory (Join-Path $directory ('rejected-output-'+$case)) } catch { $rejected=$true }
        if(!$rejected -or (Test-Path -LiteralPath $workspace)){throw "Bundle boundary failed: $case"}
        Copy-Item -LiteralPath $code -Destination $target
        if($case -eq 'additional-input'){[IO.File]::Delete((Join-Path $bundle 'extra.dll'))}
        $passed+=$case
    }
} finally {
    $env:FP_RUNNER_FIXTURE_MODE=$previousMode
    $env:FP_RUNNER_FIXTURE_TRX=$previousTrx
    $env:FLUENT_PDF_EXPECTED_ARCH=$previousArch
    $env:FP_CORE_CONSUMER_EXIT_CODE=$previousCoreExit
}
[ordered]@{purpose='synthetic-offline-launcher-regressions';passed=$passed;productionQualified=$false} | ConvertTo-Json | Set-Content -LiteralPath (Join-Path $directory 'regressions.json') -Encoding UTF8
Write-Output "Synthetic offline launcher regressions passed: $($passed.Count). No SDK qualification inferred."
