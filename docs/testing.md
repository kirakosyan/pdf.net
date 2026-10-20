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

FP-018 adds owned stream/scope, exact page, persistence, update/removal cycle, resource alias, malformed provenance, tagged decoration, captured-source/provider/latch/ownership and budget/cancellation contracts, plus C#/VB consumers. Original analytic/vector/image/text/page/HTML fixtures use independent pypdf/MuPDF/Poppler checks. Source tests do not qualify final packages or the complete production corpus. The phase-deadline CI101 failure and timing-measurement correction are separately retained without changing the one-second signal/task-completion requirement.

FP-028 source contracts exercise shared resources/occurrences, nested/mixed transforms, exact encoded/decoded exports, explicit font restrictions, artifacts/stamps, unknown resources/operators, cycles and budget/cancellation/atomic-output boundaries, and both language consumers. Independent original-fixture observations do not constitute production qualification.

FP-026 source contracts cover semantic/cross-stream/Form/regex/region/style search, ligature/ActualText provenance, denial deadlines, match/resource/cancellation bounds, source-position-preserving replacement, missing glyphs, overflow, explicit unsupported owners/reflow and C#/VB execution. Independent original outputs do not qualify the full mandatory corpus.

FP-027 source contracts cover ruled/borderless/merged cells, explicit culture, source geometry/provenance, matching/mismatched page headers, ActualText, CSV/JSON, malformed merges, resource/cancellation boundaries, atomic files and C#/VB execution. Independent measurements cover 32 small pre-labeled cells; full precision/recall/continuation qualification remains mandatory.

The macOS Intel source CI job allows 40 minutes for setup/build/API comparison and all three required runtime lanes; other jobs allow 20 minutes. CI104 completed two Intel runtime lanes before cancellation near the previous 20-minute cap. This is a CI job duration setting; SDK operation limits, deadline assertions, required checks and production qualification remain mandatory.

FP-099 adds 14 source contracts for event order/root context identity, real pagination, stale/thread/reentrant guards, exceptions, finite/cyclic/large sources, explicit culture, typed binding, both language consumers and atomic failures including outstanding workers. Five original outputs/seven pages have independent reader/text/trace checks; full production qualification remains mandatory.

CI106 retained a 30-second default-operation deadline during a 64-process decode MEMORY fixture and an unrelated API-tool exit137. That memory fixture now keeps all64decodes, the same shared context/default worker and byte/object/pixel/depth/worker ceilings, with an explicit two-minute batch test deadline; it makes no default-time throughput claim. The existing below/at/above tests assert the actual SDK default30-second deadline directly. SDK defaults/per-worker deadlines and production thresholds are unchanged. CI releases completed compiler/MSBuild servers before all27API comparisons; the exit137cause remains unproven and no comparison is skipped or retried.

FP-050 source checks cover BOM encodings, source layout/roles and nested URI bounds, exact scalar/schema binding, malformed/unknown inputs, XML entity denial, explicit resource/culture/ownership/atomic/budget/concurrent-job behavior and executable C#/VB consumers. Independent original outputs use pypdf/MuPDF/Poppler. These checks do not qualify full formats or final packages.

FP-053 source fixtures exercise actual published-dependency DOC/DOCX/RTF round trips, Unicode/style/table/pixel content, passive package and malformed/active input denial, strict/permissive diagnostics, cancellation/budgets/atomic ownership and executable C#/VB consumers. Independent readers and Open XML validator inspect original outputs. The complete SDK assembly inventory requires cross-framework API comparisons and actual portable-asset identity checks.

FP-112 fixtures exercise invoice tables, culture, scalar/repeat/boolean/image binding, split DOCX runs, XML escaping/no reevaluation, missing/type policies, finite limits, atomic/cancellation behavior and executable C#/VB consumers. Independent Open XML and unrelated PDF readers inspect original outputs. Full mandatory qualification remains pending.

FP-067 source fixtures exercise explicit page/Form/annotation MCID ownership, parent trees, language/description/table IDs/cycles, reading-order identity preservation, missing/duplicate associations, cancellation/budgets, actual pinned UA1/UA2 positive and negative validation, report binding/DTD/completeness denial and executable C#/VB consumers. Human/screen-reader and complete accessibility qualification remain pending.

FP-074 cases exercise exact raster capture/EOF/geometry/ownership, contexts, provider binding, Unicode/word budgets, latched failures and C#/VB consumers. Original isolated C API fixtures check lifetime/error/cancel boundaries without claiming recognition. One independently labeled actual Tesseract5.5.0 C-API tool fixture checks text/boxes and same-engine CLI agreement. Five native-positive classes require unavailable local Landlock; CI stays unfiltered and required real-engine/RID/language qualification remains blocked.

Explicit local model catalogs, exact mixed-model binding and bounded Unicode CER/WER measurement now have a source profile. Complete multilingual/model/native-host and production qualification remain pending; a retained mixed-language accuracy case fails its prior threshold. See [multilingual OCR](ocr-models.md).

Explicit raster-aligned invisible OCR overlays, finite existing-digital-text handling, shared-context render/recognize/write and bounded text/JSON/hOCR/ALTO exports now have a source profile. Complete structure, real OCR/native/host and production qualification remain pending. See [searchable OCR](searchable-ocr.md).

Opt-in bounded scan correction, original-image preservation, inverse word-boundary geometry and separate prose/table/form reading-order evidence now have a source profile. Complete automatic curve/semantic/native/corpus and production qualification remain pending. See [scan processing](scan-processing.md).

Explicit raster/full-rewrite redaction and sanitization now have a source profile: selected samples are replaced before encoding and no original graph/history is adopted. Complete selective editable-content preservation, independent renderer/security corpora and production qualification remain mandatory. See [redaction](redaction.md).

Explicit pre-created signature fields, exact incremental detached CMS signing, bounded PKCS12/borrowed-key providers and conservative DocMDP/FieldMDP edit barriers now have a source profile. Complete signature/trust/revocation/PAdES/edit-class/native/runtime qualification remains mandatory. See [signatures](signatures.md).

The historical final-signing CI run has all20runtime lanes executed and one actual Windowsfallback phase signal beyond1second. The deadline observer follow-up retains that failure and keeps the original assertions. Root/phase cases require cancellation outside the pool and a joined observer at cleanup. See [retained timing](evidence/PR-118-CI-118-source.json); new exact-head CI and all production gates remain mandatory.

The [deadline observer source regression](evidence/phase-deadline-observer.json) binds the current policy implementation, lifecycle cases and completed actual-runtime/API reports. It is source regression evidence, with complete production qualification still pending.

The first observer implementation also consumed a logical object, exhausting the native JPX decoder's exact tile/component inventory. The [retained six-host failure](evidence/PR-118-CI-119-source.json) records all six decode cases on each host and the unexecuted later lanes. The correction retains the original codec limits and fixtures, charges observer managed infrastructure separately and adds an exact-inventory policy regression. A six-case managed bridge probe is limited source evidence; the full unfiltered native CI matrix remains mandatory.

## Queue #51–#60 source checks

The signature verification/external signing/timestamp/revision, comparison/XFA/calculation and color/raster/view cases run in the ordinary source contract suite on each applicable runtime. `TimestampTests` and `RasterViewTests` emit original public timestamp/certificate and lossless image fixtures beside the test assembly. To independently check those outputs with caller-provisioned Pillow/OpenSSL:

```bash
python3 eng/verify-queue-fixtures.py tests/Fluent.Pdf.Contract.Tests/bin/Release/net10.0 artifacts/contracts/queue-independent.json
```

This checks the original pixel formula, effective DPI, TIFF frame order and exact RFC3161 imprint/signature under the fixture certificate. It does not supply general PKIX/revocation, complete PAdES or renderer qualification. See [the exact source evidence](evidence/issues-51-60-source.json) for counts, hashes, commands, observed environment failures and pending runtime/final-package work.

Queue #61–#80 format fixtures are controlled original sources. `eng/verify-format-fixtures.py` requires explicitly provisioned CairoSVG 2.8.2, PyMuPDF 1.26.6, Pillow, openpyxl 3.1.5 and odfpy 1.4.1 as analysis tools only; none is a runtime/package dependency. It independently compares SVG/PDF pixels and opens typed spreadsheet outputs, rejects failed checks even under Python `-O`, archives prior reports by digest and records exact artifact hashes/tool versions. These checks do not qualify arbitrary formats or final packages.

`eng/format-checks/OpenXmlCheck.csproj` is a separate development-only consumer of DocumentFormat.OpenXml 3.3.0 (MIT), its Framework 3.3.0 component and System.IO.Packaging 8.0.1 (MIT), pinned in its lock file. It checks controlled PPTX/XLSX schema/part rules against Office2019 and writes exact-byte reports with retained history. It is outside the product solution/package graph. Python-pptx 1.0.2 independently checks ordered raster slide geometry and embedded pixels. No office application fidelity or production qualification is inferred.

EPUB source checks use an explicitly provisioned EPUBCheck 5.3.0 development installation, pinned by the complete JAR inventory in `eng/format-checks/epubcheck/tool-jars.json`; Maven 3.9.9 resolves the tool and actual OpenJDK identity is recorded in reports. `eng/verify-epub-fixtures.py` requires exact binaries and zero actual validator errors/warnings for the controlled fixed/reflow books. `eng/test-epub-validation-boundary.py` proves that an invalid book, mismatched validator and stale aggregate fail under Python `-O`, retaining independent error reports and previous pointers. The SDK has no Maven/JVM/EPUBCheck dependency. Complete reader/visual/RTL/font/accessibility and final package qualification remain pending.

`eng/verify-eps-fixtures.py` invokes the explicitly installed development Ghostscript10.05.1 with batch/SAFER flags on controlled original static EPS files only (the system tool is AGPL-3+, without an SDK runtime dependency or engine adoption). It records actual version/front-end digest, input/output hashes, exact native EPS/PDF raster equality for vectors, shifted boxes and both stroke-adjustment settings, exact managed MuPDF round-trip equality, and separate native PDF-rewrite metrics. It retains prior reports/rendered artifacts and fails under Python `-O` when a required comparison fails. This checks controlled source interoperability; the system library/assets and complete interpreter sandbox/RID/package qualification remain pending.

## Queue61–80 source checks

[The queue61–80 evidence](evidence/issues-61-80-source.json) records the exact source checkpoint, source file hashes, actual commands/reports/tool identities and implementation limits. All twenty requirements retain their full mandatory scope and Planned status. Source fixture passes, optional provider contracts and independent controlled-format observations do not qualify complete standards, native/browser/printing hosts or final NuGet packages.

After building and running the controlled contract fixtures, explicitly provision the pinned development tools described in `eng/format-checks/README.md`. Run the independent wrappers with their fixture/report directories: `verify-format-fixtures.py`, `verify-eps-fixtures.py`, `verify-xps-fixtures.py --reader <explicit-binary>`, `verify-tex-fixture.py`, `verify-emf-fixture.py`, `verify-ofd-fixture.py --lib-directory <explicit-jars>`, `verify-epub-fixtures.py --lib-directory <explicit-jars>`, `verify-invoice-source-fixture.py` and `verify-preflight-fixture.py` under `eng`. Wrappers use actual process/reader output and fail checks under Python optimization too. The EPUB negative boundary checks also exercise a genuinely invalid fixture, changed JAR catalog and stale aggregate handling.

The current managed execution host rejects the required Linux native sandbox with `Native.SandboxUnavailable` (Landlock syscall unavailable). Record that lane as failed/unqualified; never weaken isolation or turn its native tests into fixture-only passes. Full native contract lanes remain required on actual supported hosts, alongside four-target API/source builds, locked restore, release blocking, compiled C#/VB consumers and hosted Razor/Blazor boundary checks.
