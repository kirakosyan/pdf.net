# Source contract testing and evidence

Use PowerShell 7, .NET SDK 10, the .NET 8 runtime and NativeAOT platform prerequisites (C/C++ compiler, linker and development libraries). Windows x64 also needs installed current .NET Framework and its x86 runtime. Restore uses committed lock files; API tooling is pinned in the repository tool manifest.

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

FP-004 adds lossless encode/decode chains, independent LZW bytes, PNG/TIFF predictor answers and matrices, compressed-object membership checks, indirect parameters, cancellation/limits and detached decoded results. The optional isolated DCT/JPX/JBIG2 provider has passing source tests on all six actual CI hosts; complete profile/security and production qualification remains pending.

The original self-contained NativeAOT fixture exercises sandboxed managed thread creation and native OS architecture from current Framework x86, alongside opt-in diagnostic disclosure and repeated setup/execution cancellation. Its compiler/runtime packages are pinned in its own lock file. It supplies no PDF/image capability.

## Remaining production work

Actual minimum Framework 4.7.2 execution on isolated x86/x64 hosts is absent. An assembly targeting net472 on a newer Framework is evidence for that installed runtime only. Exact final NuGet consumers are also absent: candidate creation is intentionally blocked until all source qualification is complete. Convert the examples into package-only consumers at that gate, without project references or direct assembly injection.

PDF codecs, native providers, actual format validation, visual/semantic fidelity, fuzzing, integration of these policies into future engines, minimum OS/libc versions and complete release workloads remain separate required issues. The complete [production gates](production-quality.md) apply before qualification. Do not mark FP-001/FP-002 qualified or close their tracking issues merely because this source suite passes.

`eng/test-release-block.ps1` verifies both the publication checker and the actual package target reject unqualified release attempts and produce no NuGet package. Keep `specs/release-readiness.json` false. CI artifacts here contain reports only and cannot be used as release candidates.

## Development-only dependencies

The managed library introduces no runtime native, browser, OCR or GUI dependency. Public API analyzers 3.3.4 and Framework reference assemblies 1.0.3 are private build assets. APICompat 10.0.402 is a local tool. Microsoft.NET.Test.Sdk 18.11.0, xUnit 2.9.3 and its Visual Studio runner 4.0.1 belong only to the non-packable test project. BitMiracle.LibTiff.NET 2.4.660 is a private test-only independent CCITT verifier (package SHA-256 `2779c539262f5c68b37eb19d62220e17fccabcc409fca6948be9529a60fff57a`, permissive TIFF license recorded by its [upstream license](https://bitmiracle.github.io/libtiff.net/help/articles/license.html)). It is not referenced by the managed core or examples. The C#/VB consumers are also non-packable. Lock files pin their transitive dependencies. Shipping asset/license/SBOM review remains required FP-095 work.

FP-005 tests exercise real managed PDF output, table/stream xrefs, historical generations, trailer IDs, inherited page geometry, every output-byte fault boundary, atomic failure, and actual detached-CMS signed-byte preservation. Independent pypdf and Poppler probes check emitted text/pages/rotation and retained signatures. Final-package, minimum-runtime and complete production qualification remain blocked.

FP-007 adds every standard operator spelling, state/grammar errors, immutable content edits, multi-stream page content, binary inline image boundaries (including independent JPEG/fax inputs), version/extension matrices and output fault injection. These checks exercise syntax and preservation, not complete rendering or PDF 2.0 conformance.

FP-008 adds original static font fixtures and a licensed renamed CJK fixture, dependency-complete subsets, glyph and Unicode maps, collection selection, exact vertical metrics, Type 3 procedures, source extraction, explicit encodings/fallback, embedding restrictions, output recovery, independent jobs and deterministic mutations. FontTools and independent PDF readers/renderers verify used outlines and logical text. The small source corpus is not complete production font/profile or final-package qualification.

FP-009 adds complete pinned official bidi/grapheme fixtures, explicit cluster fallback, glyph/cluster validation, positioned logical text, cancellation/budget checks and shared immutable font jobs. Eight independent HarfBuzz known answers cover Arabic, Indic, ligatures, marks and vertical CJK. The original native bridge runs in isolated workers across source CI. This does not replace the complete multilingual fidelity/copy/search, minimum-runtime, final-package, security/fuzz/endurance gates.

FP-010 adds original ICC fixtures validated with independent LittleCMS, immutable paths/geometry, color-state/resource scope validation, all standard shading/mesh types, patterns, compositing states, vector masks/groups, source resource merging, snapshots, budgets and independent jobs. Separate original Python reference PDFs and Poppler raster comparisons exercise output semantics. These checks do not qualify all profiles, complete fidelity/corpus, final packages or long workloads.

FP-011 adds all-rotation/nonzero-origin/UserUnit coordinate answers, five-box round trips, curve extrema/winding, transformed quad and clip hit tests, placement/z-order, resource batching beyond 4096 layers, atomic application, builder/thread ownership and C#/VB canvas consumers. Independent direct PDFs check text/images/paths and all page boxes. Minimum runtimes, final packages and complete production geometry/fidelity qualification remain pending.
