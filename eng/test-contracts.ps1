[CmdletBinding()]
param(
    [string]$Configuration = 'Release',
    [ValidateSet('X64','Arm64')][string]$ExpectedArchitecture = [Runtime.InteropServices.RuntimeInformation]::ProcessArchitecture.ToString()
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$root = Split-Path $PSScriptRoot -Parent
$resultsRoot = Join-Path $root 'artifacts/contracts'
$project = Join-Path $root 'tests/Fluent.Pdf.Contract.Tests/Fluent.Pdf.Contract.Tests.csproj'
$records = [Collections.Generic.List[object]]::new()
$previousArchitecture = $env:FLUENT_PDF_EXPECTED_ARCH
function Run-ContractLane([string]$tfm, [string]$name, [string]$architecture, [bool]$fallback = $false) {
    $env:FLUENT_PDF_EXPECTED_ARCH = $architecture
    $directory = Join-Path $resultsRoot $name
    $arguments = @('test', $project, '-c', $Configuration, '-f', $tfm, '--no-restore',
        '--logger', 'trx;LogFileName=contracts.trx', '--results-directory', $directory)
    if ($fallback) { $arguments += '-p:UseNetStandardAssembly=true' }
    else { $arguments += '--no-build' }
    if ($architecture -eq 'X86') { $arguments += @('--', 'RunConfiguration.TargetPlatform=x86') }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Contract tests failed: $name" }
    $reportPath = Join-Path $directory 'contracts.trx'
    $report = [xml](Get-Content -LiteralPath $reportPath -Raw)
    $counters = $report.TestRun.ResultSummary.Counters
    if ([int]$counters.total -le 0 -or [int]$counters.passed -ne [int]$counters.total -or
        [int]$counters.failed -ne 0 -or [int]$counters.notExecuted -ne 0) { throw "Incomplete contract report: $name" }
    $identity = @($report.TestRun.Results.UnitTestResult | Where-Object testName -Match 'TheRequestedLibraryAssetIsActuallyLoaded')
    if ($identity.Count -ne 1) { throw "Missing runtime/asset identity: $name" }
    $output = [string]$identity[0].Output.StdOut
    $runtime = [regex]::Match($output, '(?m)^Actual runtime: (.+)').Groups[1].Value.Trim()
    $os = [regex]::Match($output, '(?m)^Actual OS: (.+)').Groups[1].Value.Trim()
    $actualArchitecture = [regex]::Match($output, '(?m)^Process architecture: (.+)').Groups[1].Value.Trim()
    $library = [regex]::Match($output, '(?m)^Library TFM: (.+)').Groups[1].Value.Trim()
    if (!$runtime -or !$os -or !$library -or $actualArchitecture -ine $architecture) { throw "Unverified runtime identity: $name" }
    $assembly = Join-Path $root "tests/Fluent.Pdf.Contract.Tests/bin/$Configuration/$tfm/Fluent.Pdf.dll"
    $records.Add([pscustomobject]@{
        lane=$name;libraryTfm=$library;runtime=$runtime;os=$os;architecture=$actualArchitecture
        total=[int]$counters.total;passed=[int]$counters.passed;failed=0;skipped=0
        report="$name/contracts.trx";reportSha256=(Get-FileHash -LiteralPath $reportPath).Hash
        assemblySha256=(Get-FileHash -LiteralPath $assembly).Hash
    })
}
Push-Location $root
try {
    $null = [IO.Directory]::CreateDirectory($resultsRoot)
    # A failed rerun must not leave an earlier passing aggregate report in this location.
    [IO.File]::Delete((Join-Path $resultsRoot 'source-contract-evidence.json'))
    Run-ContractLane 'net8.0' 'net8' $ExpectedArchitecture
    Run-ContractLane 'net10.0' 'net10' $ExpectedArchitecture
    if ($IsWindows -and $ExpectedArchitecture -eq 'X64') {
        Run-ContractLane 'net472' 'framework-current-x64' 'X64'
        Run-ContractLane 'net472' 'framework-current-x86' 'X86'
    }
    Run-ContractLane 'net10.0' 'netstandard-fallback' $ExpectedArchitecture $true
    $sourceFiles = @(& git ls-files --cached --others --exclude-standard src/Fluent.Pdf src/Fluent.Pdf.Workers native/worker tests/native | Sort-Object -Unique | ForEach-Object {
        [pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_)).Hash}
    })
    $summary = [ordered]@{
        schemaVersion=1;purpose='source-contract-tests';requirements=@('FP-001','FP-002','FP-085')
        sourceCommit=(& git rev-parse HEAD).Trim();workingTreeDirty=[bool](& git status --porcelain)
        sourceFiles=$sourceFiles;lanes=@($records.ToArray());productionQualified=$false
        pending=@('FP-085 complete native security/endurance and reviewed engine qualification','Actual minimum .NET Framework 4.7.2 execution','Exact final NuGet package consumers','Complete SDK capability and production qualification')
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $resultsRoot 'source-contract-evidence.json') -Encoding utf8
} finally {
    $env:FLUENT_PDF_EXPECTED_ARCH = $previousArchitecture
    Pop-Location
}
