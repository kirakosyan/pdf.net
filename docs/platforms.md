# Platform and runtime contract

## Required deployment profile

| Runtime/profile | Windows | Linux | macOS |
| --- | --- | --- | --- |
| .NET 10 | x64, ARM64 | x64, ARM64 | x64, ARM64 |
| .NET 8 while upstream-supported | x64, ARM64 | x64, ARM64 | x64, ARM64 |
| .NET Framework 4.7.2 minimum | x86, x64 | Not applicable | Not applicable |
| Current .NET Framework | x86, x64 | Not applicable | Not applicable |
| netstandard2.0 fallback assembly | Explicit fallback consumer tests | Explicit fallback consumer tests | Explicit fallback consumer tests |

The required package targets are net472, netstandard2.0, net8.0 and net10.0. netstandard2.0 is an API compatibility surface, not a runtime. Do not infer support for every runtime that can load it. Full Framework runs on Windows. Linux is the intended Unix target; FreeBSD and other Unix variants are outside the current scope.

At release time, record exact supported OS versions/distributions, kernel/libc, runtime patches, architecture and provider ABI. A production release cannot leave minimum OS/libc versions unspecified. Qualify representative supported Debian/Ubuntu and enterprise Linux families; musl/Alpine requires a separate explicit provider qualification before advertising support. Serverless and container variants are deployment profiles tested by their adapters.

.NET 8 reaches its upstream support boundary soon after this planning date; verify the current Microsoft lifecycle at release and make any change through the documented compatibility policy, not an unnoticed removal.

## Applicability

Core parse/edit/write/generate/extract operations are cross-platform. Native rendering, OCR and browser operations require their exact optional engines on each supported RID. Missing engines produce capability diagnostics; they do not reduce the required production qualification matrix.

COM, legacy ASPX/WebForms and Windows-specific UI/printing integrations are Windows-only by design. Linux/macOS printing uses platform adapters. Do not use skipped tests to represent these applicability rules: declare applicability in the test plan and require complete reports for every applicable case.

Reader is currently only a buildable project placeholder. No Reader OS/runtime support claim is made.

## Native deployment preflight

The partial FP-085 [deployment inspector](native-deployment.md) recognizes the current OS/process RID for the six modern lanes and Windows x86. It checks caller-pinned PE, ELF or thin 64-bit Mach-O header architecture without loading files. RID equality does not establish libc, minimum OS, exports, calling convention, runtime dependencies, code signing or sandbox compatibility. Read-only asset probes run in source lanes; modern Unix lanes also exercise read-only directories and rejected root/file symbolic links. Windows privileged link scenarios and actual minimum Framework remain unqualified. No provider capability or native engine support is enabled.

## Actual runtime evidence

Building net472 or running it on Framework 4.8.1 does not qualify actual 4.7.2. Require an isolated 4.7.2 host, verify runtime identity, execute the exact final package and retain complete reports. The prior wordnet legacy-JIT requirement is historical evidence about that package, not a configuration requirement to copy into Fluent.Pdf without reproducing it.

ARM64 qualification must execute on ARM64 hardware/OS. Cross compilation, emulation, successful installation or a container label is insufficient. Linux containers do not qualify macOS or Windows. A hosted build runner proves only the lanes it actually executes.

Source contract CI executes .NET 8, .NET 10 and an explicit netstandard2.0 fallback consumer on native Windows/Linux/macOS x64 and ARM64 runners. The Windows x64 runner also executes the net472 contracts in current Framework x86/x64 processes. Runtime identity and exact loaded assembly target are asserted. Reports describe only the contract layer; they are not final-package, minimum-OS or provider qualification. See [testing.md](testing.md).

Actual Framework 4.7.2 execution and the complete production runtime/provider matrix remain pending FP-086 work and block release.

FP-002 policy, decoder-lease, deadline and accounting tests run on these same source lanes, with no new platform/native dependency. They do not qualify a PDF parser/renderer or forced termination of native work; each future provider must integrate and qualify the mandatory operation context.

The optional isolated worker now has [passing source boundary evidence](evidence/FP-085-source.json) on the six modern hosts and current Framework x86/x64. Its [OS prerequisites and limits](native-deployment.md) apply; source success does not qualify an engine, minimum runtime or package.

FP-022 metadata inspection/editing is managed and targets all four core frameworks on the applicable Windows/Linux/macOS lanes. No XML schema is downloaded. Full production qualification and exact final package consumers remain pending.

FP-023 attachments/collections use managed APIs on all four target frameworks. Payload processing is offline and bounded; native rendering and a portfolio UI are not dependencies. Source validation does not qualify complete archival or final-package behavior.

FP-024 layer graph/evaluation/flattening is managed on all four target frameworks. Independent Poppler output checks emitted configuration behavior; SDK rendering is a separate provider capability with its own qualification.

FP-025 page/Form extraction is managed on the four core frameworks. Optional standard-font encoding/metrics come from explicitly registered providers; complete source mappings and metrics need no machine fonts or native renderer. Full production geometry/linguistic/package qualification remains pending.

The FP-036 AcroForm source profile uses only the managed core on all four TFMs. Shaped appearances use explicitly selected font/shaping providers; the form API adds no native dependency. Interactive viewer regeneration, additional tagged creation/relocation profiles and full production qualification remain pending.

FP-037 field-data and static workflow profiles run in the managed core on all four TFMs. Automatic text appearances use explicitly provisioned font/shaping services; no new native/runtime dependency is added. Full corpus, minimum-runtime and final-package qualification remains pending.

FP-040 rendering contracts work on all four target frameworks. The optional non-packable adapter targets explicit 64-bit Windows, Linux and macOS workers with separately pinned candidate assets. Framework x86 callers use the 64-bit OS worker. CI integration and headless source probes do not qualify actual minimum OS/runtime versions, transitive platform dependencies, complete fidelity or final packages. See [rendering](rendering.md) and the candidate dependency inventory.

The managed password/file-security profile and optional CMS contracts target all four TFMs. CMS uses guarded RSACng public-key operations on Windows and platform RSA elsewhere; caller private handles must implement the selected transport. Source builds and CI do not qualify actual minimum OS/provider/FIPS/hardware-key behavior. See [security applicability and remaining profiles](security.md).

FP-066 archival contracts and the optional report bridge target all four library TFMs. The source corpus provisions a pinned external Java validator on the six modern OS lanes and current Framework/fallback consumers. A caller-owned test host does not qualify a deployable SDK isolation adapter, actual minimum runtimes or complete standards/semantic fidelity. See [archival applicability](archival.md).

The FP-006 managed structural repair/ledger profile targets all four library TFMs and introduces no platform/native dependency. Independent source observations use unrelated PDF readers; complete damaged-corpus, actual minimum runtime and final-package qualification remain mandatory.

FP-098 text encoding and passive compatibility inventory use only managed core on all four target frameworks. Independent source fixtures do not qualify complete PDF 2.0, actual minimum runtimes or final packages. See the [version matrix](pdf-compatibility.md).

FP-044 HTML contracts and optional managed DevTools adapter target all four library frameworks. Protocol source tests exercise every applicable source CI lane; actual browser observations currently use a pinned Linux x64 Chromium candidate on .NET 10. Complete browser/host behavior on all six RIDs, actual minimum Framework/fallback and final packages remains mandatory. A connection factory and a source build are not platform qualification. See [HTML applicability](html-rendering.md).

FP-045 resource policy, immutable file/data capture and provider contracts target all four library frameworks. Built-in cancellable pinned HTTP/TLS is currently a modern .NET source profile; legacy/fallback assets fail before network work unless explicit qualified providers are supplied. Complete legacy/portable transports, race-safe file adapters and every actual runtime/RID/package lane remain mandatory. See [resource applicability](html-resources.md).

The FP-045 built-in Windows HTTPS source profile now rejects certificate-less and general PKI-chain TLS before connection. Pinned runtime source retains an upstream credential-cache defect; explicit verified direct self-signed identities avoid the affected API. Full Windows TLS requires a qualified isolated transport/runtime and remains mandatory. Source Windows loopback fixtures provision only disposable original test identities; this is not production TLS qualification.

FP-046 readiness/options and shared phase-deadline contracts target all four library frameworks. Source fixtures cover those contracts; actual pinned browser observations currently use Linux x64/.NET10 only. Complete actual browser/host behavior on all required RIDs/minimum runtimes/fallback and final packages remains mandatory. Child-frame, worker and replacement-navigation readiness profiles remain explicit pending scope.

FP-047 print contracts target all four library frameworks. Actual pinned trusted-browser print observations currently cover Linux x64/.NET10; complete browser/host/font, minimum/current/fallback runtime, all mandatory RID and final-package qualification remains required. Generic template font selection and browser Type3 text extraction are explicitly unqualified.

FP-018 managed stamping/source contracts target all four library frameworks. The optional HTML source retains its explicit provider/host applicability. Actual original visual/browser observations currently cover Linux x64/.NET10; complete owner/color/content profiles, mandatory RIDs/minimum/current/fallback runtimes and final packages remain required.

FP-028 managed contracts target all four library frameworks. Actual independent original-resource observations cover Linux x64/.NET10; complete resource/provider/RID/minimum/current/fallback runtime and final-package profiles remain mandatory.

FP-026 managed contracts target all four library frameworks. Independent original text/raster observations currently cover Linux x64/.NET10; complete runtime/provider/profile and final-package qualification remain mandatory.

FP-027 managed contracts target all four library frameworks. Independent original labeled table observations currently cover Linux x64/.NET10; all required runtime/profile/corpus and exact-package qualification remain mandatory.

FP-099 managed contracts target all four library frameworks. Independent original generation observations currently cover Linux x64/.NET10; complete callback/provider/runtime/corpus and exact-package qualification remain mandatory.

FP-050 managed conversion contracts target all four library frameworks. Original independent input observations cover Linux x64/.NET10; complete source-format/provider/corpus, all required runtime/RID and exact-package qualification remain mandatory.

FP-053 core and optional Word contracts target all four library frameworks. Source observations and independent readers currently cover Linux x64/.NET10; full format/host/provider, minimum/current/fallback runtime/RID and exact final-package qualification remain mandatory.

FP-112 managed/native-template and optional Word-template APIs target all four frameworks. Actual Word/provider/minimum-runtime and final-package qualification remain pending; see [mail merge](mail-merge.md).

FP-067 core and optional compliance contracts target all four SDK frameworks. Actual minimum/provider/OS/package and complete automated/human accessibility qualification remain pending; see [accessibility](accessibility.md).

FP-074 core/optional OCR contracts target all four frameworks. A path, header or original C API fixture is not actual Tesseract/model/RID qualification. Complete actual-engine/minimum-runtime/final-package evidence remains mandatory; see [local OCR](local-ocr.md).

Explicit local model catalogs, exact mixed-model binding and bounded Unicode CER/WER measurement now have a source profile. Complete multilingual/model/native-host and production qualification remain pending; a retained mixed-language accuracy case fails its prior threshold. See [multilingual OCR](ocr-models.md).

Explicit raster-aligned invisible OCR overlays, finite existing-digital-text handling, shared-context render/recognize/write and bounded text/JSON/hOCR/ALTO exports now have a source profile. Complete structure, real OCR/native/host and production qualification remain pending. See [searchable OCR](searchable-ocr.md).
