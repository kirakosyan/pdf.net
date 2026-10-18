# Source contract testing and evidence

Use PowerShell 7, .NET SDK 10, the .NET 8 runtime, an explicit Java 21 runtime for independent archival source fixtures and NativeAOT platform prerequisites (C/C++ compiler, linker and development libraries). Windows x64 also needs installed current .NET Framework and its x86 runtime. Restore uses committed lock files; API tooling is pinned in the repository tool manifest.

```powershell
pwsh -File eng/validate-spec.ps1
pwsh -File eng/build-native-fixtures.ps1
pwsh -File eng/build-archival-fixture.ps1
dotnet restore Fluent.Pdf.slnx --locked-mode
dotnet tool restore
dotnet build Fluent.Pdf.slnx -c Release --no-restore
pwsh -File eng/check-api.ps1
pwsh -File eng/test-contracts.ps1
pwsh -File eng/test-release-block.ps1
```

Run the build before each full contract script invocation. The fallback lane deliberately rebuilds the net10.0 test consumer and both examples against the **netstandard2.0 library asset**, so the last test output directory contains that asset. A runtime identity test checks the actual loaded assembly attribute; merely selecting a compatible TFM is insufficient. The next ordinary build restores the normal per-TFM references.

The test script fails on a failed test, zero tests, skipped/not-executed tests, absent identity output or process architecture mismatch. It runs .NET 8, .NET 10 and the explicit fallback lane everywhere. On Windows x64 it additionally runs current Framework in separate x64 and x86 processes. Test applicability is declared by the script; other OS/architectures do not report Framework tests as skips. Passing source tests do not populate production release evidence.

The HTML protocol peer covers shared contexts, exact engine/session/command identities, bounded JSON/chunks, explicit resources, failures and ownership; it is not a browser-layout implementation. `eng/HtmlBrowserProbe` separately requires an explicit pinned local-asset manifest and runs trusted original fixtures on Linux/.NET 10. Retain its generated/edited PDFs, SDK extraction and independent reader/raster reports. This source observation does not replace any browser production lane or qualify an OS isolation host.

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

FP-012 adds normal/rich paragraph flow, exact logical provenance, hard separators/tabs, explicit overlong-word/clipping policy, float exclusions, widow/orphan/keep chains, page/resource limits, cancellation and independent jobs. Native source tests verify RTL paragraph context on Latin continuation lines. A separate Python metric/wrap/alignment/pagination model and direct PDF encoder verify development output with independent readers/rendering. Full multilingual/profile/runtime/final-package qualification remains pending.

FP-013 adds fixed/automatic column and merged-grid answers, repeated/stacked nested headers, safe row splits and exact body-placement identity, culture/null binding, lazy finite/infinite sources, formatting limits, cancellation/budgets and C#/VB table PDFs. Independent grid/PDF reference models check invoice rendering and logical row/header order. Complete table corpora and final qualification remain pending.

FP-014 adds invariant list marker/restart answers, nested list pagination, physical/logical column ordering, actual structure parent-tree/MCID round trips, footnote reservation/repeated associations, endnotes, parity, immutable components, cancellation/resource errors and C#/VB composition PDFs. An independent direct PDF/metric model verifies column and note geometry plus logical/structure order. Complete legal/booklet/accessibility/runtime/final-package qualification remains pending.

FP-015 tests bounded contents/index convergence, final totals and header variants, real link destination objects, numbering restarts, artifact decoration, nested geometry, cancellation and explicit provider/overflow errors. Source tests and independent development inspection do not qualify the complete template or HTML corpus.

FP-016 exercises mixed selections, detached ownership, actual link/outline identities, form default-appearance resource namespaces, basic structure/parent-tree and form-MCID associations, signature/omitted-target policies, collection/geometry operations, imposition and resource limits. Complete interactive/structure/encryption profiles and final-package qualification remain mandatory.

FP-017 tests cover exact PNG predictor/JPEG/container preservation, RGBA/palette masks, Adam7, 16-bit samples, monochrome and CMYK, all eight orientations, original ICC data, safe deduplication, nested Form matrices and occurrence-specific edits. Signed/tagged edits, hostile XML, cancellation and pixel/stream boundaries fail explicitly. Both language consumers emit actual image PDFs; full production image/color and final-package gates remain pending.

FP-019 source tests cover all authored annotation subtypes, generated vector/shaped text appearances, Unicode/dates/states/replies/popups, passive valid PCM payloads, imports, screen/print flattening, transformed BBox/Matrix mapping, XML/byte round trips and finite depth/output/cancellation boundaries. Full viewer/XFDF/conformance and all production lanes remain required.

FP-020 tests cover all destination modes, Unicode names, page moves/imports and removed targets, passive document/page actions, URI permissions, chains/cycles/shared nodes, indirect additional actions, widgets/outlines/scripts/orphans, sanitization, budgets and C#/VB output. Full qualification remains pending.

FP-021 source contracts cover nested/deep/cyclic outlines, visible counts/link consistency, Unicode/style, target remapping, label restarts and number-tree cycles, all twelve transition names and parameter profiles, preferences/initial view/version floors, unsupported-setting diagnostics, C#/VB examples and operation limits.

Source functional test collections use two concurrent collection threads per test host, matching local source validation and avoiding uncontrolled oversubscription of shared runners. Test-contained parallel workloads (including 64-way stream/policy checks, 32-way text/graphics checks and concurrent native staging) retain their own explicit concurrency. No required test or SDK deadline is removed. This scheduler setting does not satisfy or replace the separate mandatory production 64-worker × 200-iteration and endurance gates. Template failures retain bounded stage/phase timing metadata without document contents.

FP-022 source contracts cover Unicode/custom Info and XMP, namespace/structured-property and translation retention, explicit deterministic timestamps/IDs, authority selection, malformed/prohibited XML, cancellation/resource limits, editor ownership, revision removal, embedded-file decisions and C#/VB examples. Exact final package and full production qualification remain blocked.

FP-023 tests cover Unicode names and explicit metadata, AF persistence, caller stream position/ownership, duplicate names, unsafe portable paths, atomic no-overwrite extraction, Size/CheckSum mismatches before output, cancellation/resource limits, collection schema/sort/subitems, external-file passivity, orphan/related stream and revision removal, unrelated alias retention and C#/VB consumers.

FP-024 source contracts exercise Unicode groups, immutable inventories, all membership policies, nested expressions/depth, intents, configurations/usage, hidden text/state/path behavior, unsupported dependencies, Form/annotation removal, page extraction, passive actions, resource limits/cancellation and executable C#/VB canvas examples. Full renderer and optional-content qualification remains pending.

FP-025 source fixtures cover mapped Unicode/ligatures/supplementary characters, explicit/inferred/unmapped text, source order versus line/paragraph inference, ActualText visibility, spacing/scaling/TJ/quotes, CTM/Forms/state restoration, crop/rotation/UserUnit, Identity-V W2 metrics, hidden advances/invisible text, declared-encoding standard widths, embedded multilingual fonts, cancellation/resource failure and C#/VB consumers.

The archival source fixtures use a SHA-256-pinned veraPDF 1.28.2 distribution and an explicit test-only external host. Reports, exact candidate PDFs and Java/runtime identity are retained under artifacts/contracts/archival. They exercise actual independent profile validation; they do not qualify a shipping isolated validation host, full semantic/fidelity corpus, actual minimum runtimes or final packages. The SDK report bridge itself installs/launches no engine and creates no staging files.

FP-045 source fixtures cover origin/address admission, DNS rebinding, redirect identity/hops, private authentication, decoding/path/data boundaries and redaction. Modern actual loopback fixtures exercise HTTP, mutual TLS, wrong pin/name, CONNECT and stalled-body cancellation; legacy/fallback assets explicitly assert unsupported built-in transport before any connection. Those applicability assertions do not qualify legacy HTTP. The optional trusted-original browser observer adds authenticated redirects and final-base-relative SVG, with independent output observations. See [resource evidence and mandatory gaps](html-resources.md).

FP-046 adds eleven shared phase-deadline cases and fifteen HTML readiness/transport/lifecycle cases, plus C#/VB options consumers. The actual trusted browser observer covers delayed assets, explicit promise/environment, infinite polling, failed image, bounded never-promise, caller cancellation, owned host termination and fresh next job. Four positive outputs have independent text/geometry/color observations. Its original per-message 25 ms polling delay was replaced with completion/cancellation signaling; exact diff/hashes and failed runs are retained without changing SDK limits. The observer remains outside production host qualification.

FP-047 adds explicit print-policy/template/structure fixtures and C#/VB snapshot consumers. Seven actual browser outputs cover 14 pages, final counters, repeated headers, CSS/fallback paper, landscape, full/half vector scale and page-MCID structures. Independent readers retain Poppler default reading-order differences and a managed Type 3 matrix extraction limit. Complete standards/visual/runtime/package and production gates remain mandatory.
