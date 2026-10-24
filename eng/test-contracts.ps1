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
$python = if ($IsWindows) { 'python' } else { 'python3' }
& $python (Join-Path $root 'eng/test-runtime-evidence.py')
if ($LASTEXITCODE -ne 0) { throw 'Runtime evidence checker boundary tests failed.' }
& $python (Join-Path $root 'eng/test-performance.py')
if ($LASTEXITCODE -ne 0) { throw 'Performance envelope/evidence boundary tests failed.' }
& $python (Join-Path $root 'eng/test-fidelity.py')
if ($LASTEXITCODE -ne 0) { throw 'Independent fidelity corpus/checker boundary tests failed.' }
& $python (Join-Path $root 'eng/test-package-audit.py')
if ($LASTEXITCODE -ne 0) { throw 'Candidate archive boundary tests failed.' }
& $python (Join-Path $root 'eng/test-supply-chain.py')
if ($LASTEXITCODE -ne 0) { throw 'Supply-chain evidence boundary tests failed.' }
function Run-ContractLane([string]$tfm, [string]$name, [string]$architecture, [bool]$fallback = $false) {
    $env:FLUENT_PDF_EXPECTED_ARCH = $architecture
    $directory = Join-Path $resultsRoot $name
    $null = [IO.Directory]::CreateDirectory($directory)
    [IO.File]::Delete((Join-Path $directory 'runtime-identity.json'))
    $arguments = @('test', $project, '-c', $Configuration, '-f', $tfm, '--no-restore',
        '--logger', 'trx;LogFileName=contracts.trx', '--results-directory', $directory)
    if ($fallback) { $arguments += @('-p:UseNetStandardAssembly=true','-m:1','-p:UseSharedCompilation=false') }
    else { $arguments += '--no-build' }
    if ($architecture -eq 'X86') { $arguments += @('--', 'RunConfiguration.TargetPlatform=x86') }
    & dotnet @arguments
    if ($LASTEXITCODE -ne 0) { throw "Contract tests failed: $name" }
    $reportPath = Join-Path $directory 'contracts.trx'
    $invoiceEvidence = Join-Path $directory 'invoice-producer'
    $null = [IO.Directory]::CreateDirectory($invoiceEvidence)
    foreach ($fixture in @('EUR-direct','JPY-direct','BHD-direct','EUR-csharp','EUR-visual-basic')) {
        foreach ($extension in @('.pdf','.xml')) {
            Copy-Item -LiteralPath (Join-Path $root "tests/Fluent.Pdf.Contract.Tests/bin/$Configuration/$tfm/artifacts/invoice-producer/$fixture$extension") -Destination $invoiceEvidence
        }
    }
    $validatedInvoiceEvidence = Join-Path $directory 'validated-invoices'
    $null = [IO.Directory]::CreateDirectory($validatedInvoiceEvidence)
    foreach ($fixture in @('EUR','JPY','BHD','EUR-csharp','EUR-visual-basic')) {
        Copy-Item -LiteralPath (Join-Path $root "tests/Fluent.Pdf.Contract.Tests/bin/$Configuration/$tfm/artifacts/validated-invoices/$fixture.pdf") -Destination $validatedInvoiceEvidence
    }
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
    $family = if ($IsWindows) { 'win' } elseif ($IsLinux) { 'linux' } elseif ($IsMacOS) { 'osx' } else { throw 'Unsupported runtime qualification host.' }
    $kind = if ($tfm -eq 'net472') { 'framework-current' } elseif ($fallback) { 'netstandard2.0-fallback' } else { $tfm }
    $declaredLane = "$family-$($architecture.ToLowerInvariant())-$kind"
    $identityPath = Join-Path $directory 'runtime-identity.json'
    & $python (Join-Path $root 'eng/check-runtime-evidence.py') --lane $declaredLane --trx $reportPath --mode source --output $identityPath
    if ($LASTEXITCODE -ne 0) { throw "Actual runtime evidence consistency failed: $name" }
    $interchange = Join-Path $root "tests/Fluent.Pdf.Contract.Tests/bin/$Configuration/$tfm/artifacts/text-interchange"
    & $python (Join-Path $root 'eng/verify-text-interchange.py') --directory $interchange --output (Join-Path $directory 'text-interchange-readers.json')
    if ($LASTEXITCODE -ne 0) { throw "Interchange fixture integrity checks failed: $name" }
    $interchangeEvidence = Join-Path $directory 'text-interchange'
    $null = [IO.Directory]::CreateDirectory($interchangeEvidence)
    foreach ($fixture in @('roles.pdf','roles.md','roles.txt','table-0.md','table-1.md','table.pdf','ranges.txt','images.zip','csharp.md','visual-basic.md')) {
        Copy-Item -LiteralPath (Join-Path $interchange $fixture) -Destination (Join-Path $interchangeEvidence $fixture)
    }
    $collections = Join-Path $root "tests/Fluent.Pdf.Contract.Tests/bin/$Configuration/$tfm/artifacts/collections"
    & $python (Join-Path $root 'eng/verify-collection-fixtures.py') --directory $collections --output (Join-Path $directory 'collection-readers.json')
    if ($LASTEXITCODE -ne 0) { throw "Independent collection archive/XML observations failed: $name" }
    $collectionEvidence = Join-Path $directory 'collections'
    $null = [IO.Directory]::CreateDirectory($collectionEvidence)
    foreach ($fixture in @('unicode.zip','csharp.zip','visual-basic.zip','original.pdf','csharp.pdf','visual-basic.pdf','objects-Table.xml','objects-Stream.xml','objects-Table.pdf','objects-Stream.pdf')) {
        Copy-Item -LiteralPath (Join-Path $collections $fixture) -Destination (Join-Path $collectionEvidence $fixture)
    }
    $semantic = Join-Path $root "tests/Fluent.Pdf.Contract.Tests/bin/$Configuration/$tfm/artifacts/semantic-review"
    & $python (Join-Path $root 'eng/verify-semantic-review.py') --directory $semantic --output (Join-Path $directory 'semantic-review-readers.json')
    if ($LASTEXITCODE -ne 0) { throw "Independent semantic ledger checks failed: $name" }
    $semanticEvidence = Join-Path $directory 'semantic-review'
    $null = [IO.Directory]::CreateDirectory($semanticEvidence)
    foreach ($fixture in @('original.pdf','reviewed.pdf','ledger.json','role-map.pdf','author-Ua1.pdf','author-Ua1.json','author-Ua2.pdf','author-Ua2.json','csharp.pdf','csharp.json','visual-basic.pdf','visual-basic.json','validator-Ua1.pdf','validator-Ua1.json','validator-Ua2.pdf','validator-Ua2.json')) {
        Copy-Item -LiteralPath (Join-Path $semantic $fixture) -Destination (Join-Path $semanticEvidence $fixture)
    }
    $cleanup = Join-Path $root "tests/Fluent.Pdf.Contract.Tests/bin/$Configuration/$tfm/artifacts/image-cleanup"
    & $python (Join-Path $root 'eng/verify-image-cleanup.py') --directory $cleanup --output (Join-Path $directory 'image-cleanup-readers.json')
    if ($LASTEXITCODE -ne 0) { throw "Independent cleanup pixel observations failed: $name" }
    $cleanupEvidence = Join-Path $directory 'image-cleanup'
    $null = [IO.Directory]::CreateDirectory($cleanupEvidence)
    foreach ($fixture in @('white','near-white','noise','faint-text','border','color','resize')) {
        foreach ($stage in @('before','after')) { Copy-Item -LiteralPath (Join-Path $cleanup "$fixture-$stage.raw") -Destination (Join-Path $cleanupEvidence "$fixture-$stage.raw") }
    }
    Copy-Item -LiteralPath (Join-Path $cleanup 'observations.json') -Destination (Join-Path $cleanupEvidence 'observations.json')
    $business = Join-Path $root "tests/Fluent.Pdf.Contract.Tests/bin/$Configuration/$tfm/artifacts/business-data"
    & $python (Join-Path $root 'eng/verify-business-data.py') --directory $business --output (Join-Path $directory 'business-data-readers.json')
    if ($LASTEXITCODE -ne 0) { throw "Independent business JSON/XML provenance checks failed: $name" }
    $businessEvidence = Join-Path $directory 'business-data'
    $null = [IO.Directory]::CreateDirectory($businessEvidence)
    foreach ($fixture in @('digital','table','fields','structure','mixed','csharp','visual-basic')) {
        foreach ($extension in @('pdf','json','xml')) { Copy-Item -LiteralPath (Join-Path $business "$fixture.$extension") -Destination (Join-Path $businessEvidence "$fixture.$extension") }
    }
    $assembly = Join-Path $root "tests/Fluent.Pdf.Contract.Tests/bin/$Configuration/$tfm/Fluent.Pdf.dll"
    $records.Add([pscustomobject]@{
        lane=$name;libraryTfm=$library;runtime=$runtime;os=$os;architecture=$actualArchitecture
        total=[int]$counters.total;passed=[int]$counters.passed;failed=0;skipped=0
        report="$name/contracts.trx";reportSha256=(Get-FileHash -LiteralPath $reportPath).Hash
        assemblySha256=(Get-FileHash -LiteralPath $assembly).Hash
        runtimeIdentityEvidence="$name/runtime-identity.json";runtimeIdentityEvidenceSha256=(Get-FileHash -LiteralPath $identityPath -Algorithm SHA256).Hash
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
    foreach ($framework in @('net8.0','net10.0')) {
        $fidelityOutput = Join-Path $resultsRoot ('fidelity/' + $framework + '/' + [Guid]::NewGuid().ToString('N'))
        & dotnet (Join-Path $root "eng/Fidelity/bin/$Configuration/$framework/Fidelity.dll") (Join-Path $root 'tests/FidelityCorpus') $fidelityOutput
        if ($LASTEXITCODE -ne 0) { throw "Fidelity corpus source consumer failed: $framework" }
        $determinismOutput = Join-Path $resultsRoot ('determinism/' + $framework + '/' + [Guid]::NewGuid().ToString('N'))
        & $python (Join-Path $root 'eng/test-determinism.py') --assembly (Join-Path $root "eng/Determinism/bin/$Configuration/$framework/Determinism.dll") --corpus (Join-Path $root 'tests/FidelityCorpus') --output $determinismOutput
        if ($LASTEXITCODE -ne 0) { throw "Repeated-process determinism failed: $framework" }
    }
    & dotnet run --project (Join-Path $root 'examples/Fluent.Pdf.WebSample/Fluent.Pdf.WebSample.csproj') -c $Configuration --no-build --no-restore -- --self-test
    if ($LASTEXITCODE -ne 0) { throw 'Hosted compiled Razor/MVC/Blazor boundary checks failed.' }
    & $python (Join-Path $root 'eng/test-cli.py') --assemblies (Join-Path $root "apps/Fluent.Pdf.Cli/bin/$Configuration/net8.0/fluent-pdf.dll") (Join-Path $root "apps/Fluent.Pdf.Cli/bin/$Configuration/net10.0/fluent-pdf.dll") --output (Join-Path $resultsRoot 'cli/processes.json')
    if ($LASTEXITCODE -ne 0) { throw 'Actual CLI pipe/configuration/atomic output/protected input/batch boundaries failed.' }
    $sourceFiles = @(& git ls-files --cached --others --exclude-standard src/Fluent.Pdf src/Fluent.Pdf.Workers src/Fluent.Pdf.Codecs src/Fluent.Pdf.Fonts src/Fluent.Pdf.Text src/Fluent.Pdf.Rendering src/Fluent.Pdf.Cryptography src/Fluent.Pdf.Compliance src/Fluent.Pdf.Html src/Fluent.Pdf.Word src/Fluent.Pdf.Ocr src/Fluent.Pdf.Formats src/Fluent.Pdf.Intelligence src/Fluent.Pdf.Automation src/Fluent.Pdf.Applications apps/Fluent.Pdf.Cli native/worker native/codecs native/text native/rendering native/ocr tests/native tests/native-aot tests/Fluent.Pdf.Contract.Tests examples eng .github/workflows third-party | Sort-Object -Unique | ForEach-Object {
        [pscustomobject]@{path=$_;sha256=(Get-FileHash -LiteralPath (Join-Path $root $_)).Hash}
    })
    $summary = [ordered]@{
        schemaVersion=1;purpose='source-contract-tests';requirements=@('FP-001','FP-002','FP-085','FP-003','FP-004','FP-005','FP-007','FP-008','FP-009','FP-010','FP-011','FP-012','FP-013','FP-014','FP-015','FP-016','FP-017','FP-019','FP-020','FP-021','FP-022','FP-023','FP-024','FP-025','FP-036','FP-037','FP-040','FP-031','FP-066','FP-006','FP-098','FP-044','FP-045','FP-046','FP-047','FP-018','FP-028','FP-026','FP-027','FP-099','FP-050','FP-053','FP-112','FP-067','FP-074','FP-030','FP-032','FP-075','FP-076','FP-077','FP-033','FP-034','FP-035','FP-102','FP-029','FP-038','FP-039','FP-041','FP-064','FP-042','FP-043','FP-048','FP-049','FP-051','FP-052','FP-071','FP-070','FP-069','FP-068','FP-065','FP-063','FP-062','FP-061','FP-060','FP-059','FP-058','FP-057','FP-056','FP-055','FP-054','FP-072','FP-073','FP-078','FP-079','FP-080','FP-081','FP-082','FP-083')
        sourceCommit=(& git rev-parse HEAD).Trim();workingTreeDirty=[bool](& git status --porcelain)
        sourceFiles=$sourceFiles;lanes=@($records.ToArray());cliProcessEvidence='cli/processes.json';cliProcessEvidenceSha256=(Get-FileHash -LiteralPath (Join-Path $resultsRoot 'cli/processes.json') -Algorithm SHA256).Hash;productionQualified=$false
        pending=@('FP-085 complete native security/endurance and reviewed engine qualification','FP-074 complete actual OCR engine/model/RID and language corpus qualification','Actual minimum .NET Framework 4.7.2 execution','Exact final NuGet package consumers','Complete SDK capability and production qualification')
    }
    $summary | ConvertTo-Json -Depth 8 | Set-Content (Join-Path $resultsRoot 'source-contract-evidence.json') -Encoding utf8
} finally {
    $env:FLUENT_PDF_EXPECTED_ARCH = $previousArchitecture
    Pop-Location
}
