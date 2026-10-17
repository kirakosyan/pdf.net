# Source contract testing and evidence

Use PowerShell 7, .NET SDK 10 and the .NET 8 runtime. Windows x64 also needs installed current .NET Framework and its x86 runtime. Restore uses committed lock files; API tooling is pinned in the repository tool manifest.

```powershell
pwsh -File eng/validate-spec.ps1
pwsh -File eng/build-native-fixtures.ps1
dotnet restore Fluent.Pdf.slnx --locked-mode
dotnet tool restore
dotnet build Fluent.Pdf.slnx -c Release --no-restore
pwsh -File eng/check-api.ps1
pwsh -File eng/test-contracts.ps1
pwsh -File eng/test-release-block.ps1
```

Run the build before each full contract script invocation. The fallback lane deliberately rebuilds the net10.0 test consumer and both examples against the **netstandard2.0 library asset**, so the last test output directory contains that asset. A runtime identity test checks the actual loaded assembly attribute; merely selecting a compatible TFM is insufficient. The next ordinary build restores the normal per-TFM references.

The test script fails on a failed test, zero tests, skipped/not-executed tests, absent identity output or process architecture mismatch. It runs .NET 8, .NET 10 and the explicit fallback lane everywhere. On Windows x64 it additionally runs current Framework in separate x64 and x86 processes. Test applicability is declared by the script; other OS/architectures do not report Framework tests as skips. Passing source tests do not populate production release evidence.

CI runs these checks on Windows, Linux and macOS, each with x64 and ARM64 native runners. `-ExpectedArchitecture X64|Arm64` asserts the intended process architecture. Each CI job retains its own `source-contracts-<platform>` artifact containing:

- TRX reports with test totals and actual runtime/OS/process architecture/library TFM output.
- `source-contract-evidence.json` with tested commit, working-tree state, core source digests, report/loaded-assembly digests and explicit pending qualification.

The aggregate report is invalidated at the start of a rerun and written only after all applicable lanes pass. In CI, the tested commit may be the pull request merge commit. Retain the workflow run and its exact artifacts together. Local dirty-tree reports identify source digests and are development evidence, not clean release qualification.

The contract fixture reads a byte count and writes a labeled text record. It is deliberately test-only and emits no PDF. The suite covers normal edits, invalid geometry/registration, page-count boundaries, disposal, directional expiring stream leases, failed/canceled callbacks, provider seekability, atomic output preservation, examples and independent document isolation. FP-002 adds below/at/above checks for all seven resource kinds, diagnostic retention and elapsed time; 64-bit overflow/pixel multiplication; real bounded Deflate expansion; strict/permissive losses with page/object provenance; latched failures; parallel accounting; scoped ownership; deadlines and cooperative cancellation. The elapsed-time boundary uses an internal monotonic-clock test seam, alongside actual timer/token integration. API analyzers enforce the checked-in public declarations during compilation; APICompat compares actual target assemblies.

FP-085 adds inert original PE/ELF/Mach-O header fixtures, current-host pinned-file hashing, missing dependency/size/digest/RID diagnostics, portable-name validation, cumulative byte/count boundaries and overflow, read-only assets, cancellation at hash-read boundaries, C#/VB consumers and latched preflight failures preserving atomic destinations. These fixtures are not native engines. Modern Unix source runtimes additionally check read-only directories and root/file symbolic links; Windows privileged links and minimum Framework behavior require separate evidence. The original native bridge fixtures additionally exercise actual loaders, ABI/version/export/dependency failures, crashes, deadlines/cancellation, memory/transport ceilings, denied document read/write/network/process access and recovery. See [native deployment](native-deployment.md) for host prerequisites and remaining qualification.

FP-003 adds original lexical/revision/xref fixtures, an independently inspected page fixture, fixed/dynamic Flate regressions, truncation checks and deterministic parser mutations. It exercises actual syntax inspection, including C#/VB consumers; it does not qualify rendering, output conformance or the complete fuzz workload.

FP-004 adds lossless encode/decode chains, independent LZW bytes, PNG/TIFF predictor answers and matrices, compressed-object membership checks, indirect parameters, cancellation/limits and detached decoded results. Optional image codecs are still in progress.

## Remaining production work

Actual minimum Framework 4.7.2 execution on isolated x86/x64 hosts is absent. An assembly targeting net472 on a newer Framework is evidence for that installed runtime only. Exact final NuGet consumers are also absent: candidate creation is intentionally blocked until all source qualification is complete. Convert the examples into package-only consumers at that gate, without project references or direct assembly injection.

PDF codecs, native providers, actual format validation, visual/semantic fidelity, fuzzing, integration of these policies into future engines, minimum OS/libc versions and complete release workloads remain separate required issues. The complete [production gates](production-quality.md) apply before qualification. Do not mark FP-001/FP-002 qualified or close their tracking issues merely because this source suite passes.

`eng/test-release-block.ps1` verifies both the publication checker and the actual package target reject unqualified release attempts and produce no NuGet package. Keep `specs/release-readiness.json` false. CI artifacts here contain reports only and cannot be used as release candidates.

## Development-only dependencies

The managed library introduces no runtime native, browser, OCR or GUI dependency. Public API analyzers 3.3.4 and Framework reference assemblies 1.0.3 are private build assets. APICompat 10.0.402 is a local tool. Microsoft.NET.Test.Sdk 18.11.0, xUnit 2.9.3 and its Visual Studio runner 4.0.1 belong only to the non-packable test project. The C#/VB consumers are also non-packable. Lock files pin their transitive dependencies. Shipping asset/license/SBOM review remains required FP-095 work.
