# Implementation order and production milestones

## Assignment rule

Use the dependency graph in [requirements.json](../specs/requirements.json) and [GitHub roadmap #113](https://github.com/kirakosyan/pdf.net/issues/113). Assign one issue at a time in the concrete order below unless the user requests parallel work. A task is ready only when its dependencies have delivered reviewed public contracts and the evidence its implementation needs. Do not equate a closed issue with evidence if the implementation or release claims disagree.

Each issue is unassigned at creation and has scope, dependencies, acceptance criteria, non-goals and validation. [github-backlog.md](github-backlog.md) provides the issue mapping. This sequence is topologically sorted; it intentionally brings native deployment and a useful managed document workflow forward.

FP-001 source contracts are merged through [PR #115](https://github.com/kirakosyan/pdf.net/pull/115). FP-002 is merged through [PR #116](https://github.com/kirakosyan/pdf.net/pull/116) and implements [operation policies and budgets](operation-policies.md), with [runtime/API tests](testing.md). Both tracking issues remain open for production qualification, including actual minimum-runtime and final-package consumers. The foundation milestone and all later SDK requirements remain incomplete; passing source tests does not authorize publication.

FP-085 (#3) now has an original [offline deployment and isolated worker implementation](native-deployment.md), including actual ABI/version/dependency probes, platform loading restrictions, process supervision and crash recovery. [Source CI](evidence/FP-085-source.json) passed its native boundaries on all six hosts; production qualification and final-package consumers remain incomplete. Managed syntax, filters, writer and typed content/version handling now have source implementations. The writer baseline passed [6,740 source test executions](evidence/FP-005-source.json); complete SDK qualification remains pending. Issues #3–#20 were merged in [PR #117](https://github.com/kirakosyan/pdf.net/pull/117) at `8df53a3922fdabfb095ca5565b03898c3c93d54e`. Its final source CI passed 679 tests in each of 20 runtime lanes; an earlier Framework x64 worker deadline observation remains unresolved for production performance qualification. Their tracking issues remain open for complete qualification. Issues #21–#50 were merged in [PR #118](https://github.com/kirakosyan/pdf.net/pull/118); their complete requirement profiles and production qualification remain open. Metadata source CI passed 698 tests per runtime lane (13,960 executions); later work requires its own evidence.

## Milestones

1. **Foundation:** public contracts, budgets, deployment probes, syntax/filters/writer, fonts and graphics.
2. **First adoption checkpoint:** after FP-013, FP-015, FP-025, FP-040, FP-064 and FP-066 plus their dependencies, generate an invoice with logo and repeated line items, extract its text, independently render it and validate PDF/A-2b. Retain the outputs and diagnostics. This is an internal capability checkpoint, not permission to publish a partial SDK.
3. **Document workflows:** editing, forms, extraction, browser conversion, OCR, redaction, signatures and accessible output.
4. **Complete capability coverage:** every conversion, standards profile and optional integration meets its requirement contract.
5. **Production release:** all required runtime/provider lanes, resource/security/fidelity/performance gates, exact final package consumers, stable publishing and public verification.

Milestones are sequencing aids. They do not remove later capabilities from the required 1.0.0 scope. The Reader placeholder is already established; its future product work is deferred.

## Concrete assignment sequence

| Order | Requirement | Capability |
| --- | --- | --- |
| 1 | [FP-001](requirements/FP-001.md) | Public API and provider contracts |
| 2 | [FP-002](requirements/FP-002.md) | Operation policies, diagnostics and resource budgets |
| 3 | [FP-085](requirements/FP-085.md) | Native provider deployment and isolation |
| 4 | [FP-003](requirements/FP-003.md) | PDF syntax and cross-reference reader |
| 5 | [FP-004](requirements/FP-004.md) | Stream filters, object streams and codecs |
| 6 | [FP-005](requirements/FP-005.md) | PDF writer and incremental transactions |
| 7 | [FP-007](requirements/FP-007.md) | Content operators and PDF version negotiation |
| 8 | [FP-008](requirements/FP-008.md) | Fonts, embedding, subsetting and fallback |
| 9 | [FP-009](requirements/FP-009.md) | Unicode, bidirectional text and shaping |
| 10 | [FP-010](requirements/FP-010.md) | Vector graphics, color and transparency |
| 11 | [FP-011](requirements/FP-011.md) | Page canvas and geometry |
| 12 | [FP-012](requirements/FP-012.md) | Flow layout and pagination |
| 13 | [FP-013](requirements/FP-013.md) | Tables and data binding |
| 14 | [FP-014](requirements/FP-014.md) | Lists, columns and document composition |
| 15 | [FP-015](requirements/FP-015.md) | Headers, footers, page numbers and contents |
| 16 | [FP-016](requirements/FP-016.md) | Page organization and imposition |
| 17 | [FP-017](requirements/FP-017.md) | Images and reusable page graphics |
| 18 | [FP-019](requirements/FP-019.md) | Annotation model and appearance streams |
| 19 | [FP-020](requirements/FP-020.md) | Links, destinations, actions and navigation |
| 20 | [FP-021](requirements/FP-021.md) | Outlines, page labels and viewer preferences |
| 21 | [FP-022](requirements/FP-022.md) | Metadata and document identity |
| 22 | [FP-023](requirements/FP-023.md) | Attachments, portfolios and associated files |
| 23 | [FP-024](requirements/FP-024.md) | Optional-content layers |
| 24 | [FP-025](requirements/FP-025.md) | Text extraction with layout geometry |
| 25 | [FP-036](requirements/FP-036.md) | AcroForm creation and editing |
| 26 | [FP-037](requirements/FP-037.md) | Form filling, data exchange and flattening |
| 27 | [FP-040](requirements/FP-040.md) | PDF page rendering |
| 28 | [FP-031](requirements/FP-031.md) | Password and certificate encryption |
| 29 | [FP-066](requirements/FP-066.md) | PDF/A archival profiles |
| 30 | [FP-006](requirements/FP-006.md) | Repair, recovery and structural validation |
| 31 | [FP-098](requirements/FP-098.md) | PDF 2.0 and extension preservation |
| 32 | [FP-044](requirements/FP-044.md) | HTML, CSS and JavaScript to PDF |
| 33 | [FP-045](requirements/FP-045.md) | Resource loading, authentication and network policy |
| 34 | [FP-046](requirements/FP-046.md) | Dynamic-page readiness and browser lifecycle |
| 35 | [FP-047](requirements/FP-047.md) | Print CSS, HTML pagination and accessibility |
| 36 | [FP-018](requirements/FP-018.md) | Watermarks, stamps and artifacts |
| 37 | [FP-028](requirements/FP-028.md) | Graphics, image, font and artifact extraction |
| 38 | [FP-026](requirements/FP-026.md) | Search, regex and text replacement |
| 39 | [FP-027](requirements/FP-027.md) | Table and structured-data extraction |
| 40 | [FP-099](requirements/FP-099.md) | Document event hooks and data-source generation |
| 41 | [FP-050](requirements/FP-050.md) | Text, Markdown and structured input |
| 42 | [FP-053](requirements/FP-053.md) | Word document conversion adapter |
| 43 | [FP-112](requirements/FP-112.md) | Document templates and mail merge |
| 44 | [FP-067](requirements/FP-067.md) | Tagged PDF and PDF/UA accessibility |
| 45 | [FP-074](requirements/FP-074.md) | Local OCR provider and wordnet reuse |
| 46 | [FP-075](requirements/FP-075.md) | Multilingual OCR and language models |
| 47 | [FP-076](requirements/FP-076.md) | Searchable PDF and structured OCR output |
| 48 | [FP-077](requirements/FP-077.md) | Scan preprocessing and reading order |
| 49 | [FP-030](requirements/FP-030.md) | True redaction and comprehensive sanitization |
| 50 | [FP-032](requirements/FP-032.md) | Digital signatures and certification |
| 51 | [FP-033](requirements/FP-033.md) | Signature verification and trust decisions |
| 52 | [FP-034](requirements/FP-034.md) | HSM and external signing providers |
| 53 | [FP-035](requirements/FP-035.md) | PAdES, timestamps and long-term validation |
| 54 | [FP-102](requirements/FP-102.md) | Signature-preserving editing and revision audit |
| 55 | [FP-029](requirements/FP-029.md) | Document comparison and change reports |
| 56 | [FP-038](requirements/FP-038.md) | XFA forms and conversion |
| 57 | [FP-039](requirements/FP-039.md) | Document JavaScript and sandboxed calculations |
| 58 | [FP-041](requirements/FP-041.md) | Color-managed production rendering |
| 59 | [FP-064](requirements/FP-064.md) | PDF raster export and image containers |
| 60 | [FP-042](requirements/FP-042.md) | Headless viewing primitives |
| 61 | [FP-043](requirements/FP-043.md) | Cross-platform printing |
| 62 | [FP-048](requirements/FP-048.md) | Web framework and XAML adapters |
| 63 | [FP-049](requirements/FP-049.md) | Isolated rendering service and container deployment |
| 64 | [FP-051](requirements/FP-051.md) | Raster documents to PDF |
| 65 | [FP-052](requirements/FP-052.md) | SVG import and vector export |
| 66 | [FP-054](requirements/FP-054.md) | Spreadsheet and tabular conversion |
| 67 | [FP-055](requirements/FP-055.md) | Presentation conversion |
| 68 | [FP-056](requirements/FP-056.md) | PDF to HTML and semantic web output |
| 69 | [FP-057](requirements/FP-057.md) | EPUB and electronic-book conversion |
| 70 | [FP-058](requirements/FP-058.md) | PostScript, EPS and printer-language import |
| 71 | [FP-059](requirements/FP-059.md) | XPS and OpenXPS conversion |
| 72 | [FP-060](requirements/FP-060.md) | TeX and LaTeX conversion |
| 73 | [FP-061](requirements/FP-061.md) | CGM and platform metafile conversion |
| 74 | [FP-062](requirements/FP-062.md) | XSL-FO and XML transformation |
| 75 | [FP-063](requirements/FP-063.md) | OFD fixed-layout conversion |
| 76 | [FP-065](requirements/FP-065.md) | Conversion orchestration and format discovery |
| 77 | [FP-068](requirements/FP-068.md) | PDF/X prepress profiles |
| 78 | [FP-069](requirements/FP-069.md) | PDF/E engineering and advanced content |
| 79 | [FP-070](requirements/FP-070.md) | Electronic invoices and associated XML |
| 80 | [FP-071](requirements/FP-071.md) | Preflight, remediation and audit reports |
| 81 | [FP-072](requirements/FP-072.md) | Optimization, compression and deduplication |
| 82 | [FP-073](requirements/FP-073.md) | Linearization and progressive access |
| 83 | [FP-078](requirements/FP-078.md) | Barcode and QR document workflows |
| 84 | [FP-079](requirements/FP-079.md) | Opt-in document intelligence providers |
| 85 | [FP-080](requirements/FP-080.md) | Semantic chunking and retrieval adapters |
| 86 | [FP-081](requirements/FP-081.md) | Windows COM interoperability |
| 87 | [FP-082](requirements/FP-082.md) | Application integrations and viewer controls |
| 88 | [FP-083](requirements/FP-083.md) | Command-line and batch automation |
| 89 | [FP-084](requirements/FP-084.md) | Developer documentation and executable examples |
| 90 | [FP-086](requirements/FP-086.md) | Runtime and architecture qualification |
| 91 | [FP-087](requirements/FP-087.md) | Async processing, concurrency and cancellation |
| 92 | [FP-088](requirements/FP-088.md) | Performance, streaming and resource envelopes |
| 93 | [FP-089](requirements/FP-089.md) | Security, fuzzing and malformed-content robustness |
| 94 | [FP-090](requirements/FP-090.md) | Independent fidelity and interoperability corpus |
| 95 | [FP-091](requirements/FP-091.md) | Deterministic output, caching and tenancy |
| 96 | [FP-092](requirements/FP-092.md) | Observability and support diagnostics |
| 97 | [FP-093](requirements/FP-093.md) | Reproducible NuGet packages and API compatibility |
| 98 | [FP-095](requirements/FP-095.md) | Dependency licensing and supply-chain inventory |
| 99 | [FP-096](requirements/FP-096.md) | PDF Reader placeholder boundary |
| 100 | [FP-097](requirements/FP-097.md) | Production document workflow acceptance |
| 101 | [FP-100](requirements/FP-100.md) | Document collections, packages and interchange |
| 102 | [FP-101](requirements/FP-101.md) | Accessibility remediation and semantic authoring |
| 103 | [FP-103](requirements/FP-103.md) | Image processing and document cleanup |
| 104 | [FP-104](requirements/FP-104.md) | Business data extraction and export schemas |
| 105 | [FP-105](requirements/FP-105.md) | Cross-platform native distribution packages |
| 106 | [FP-106](requirements/FP-106.md) | Compatibility policy and long-term maintenance |
| 107 | [FP-107](requirements/FP-107.md) | DjVu and CDR document import |
| 108 | [FP-108](requirements/FP-108.md) | PDF to Markdown and text interchange |
| 109 | [FP-109](requirements/FP-109.md) | Task-oriented facades and plugin composition |
| 110 | [FP-110](requirements/FP-110.md) | HTML element and block extraction |
| 111 | [FP-111](requirements/FP-111.md) | Hyphenation and advanced text-fit controls |
| 112 | [FP-094](requirements/FP-094.md) | Production release and Trusted Publishing |

## After every merge

Reconcile delivered code, public API/capability baseline, requirement status, issue acceptance checkboxes, evidence links and version-specific README claims. Run the affected source/package checks and the specification validator. Keep other requirements planned until their own evidence exists.

A milestone or roadmap issue closes only when its constituent evidence is reconciled. Publication remains blocked until the complete SDK scope and final-package gate pass.

Source progress through #35 now includes [bounded HTML print layout and structure associations](html-printing.md). Full FP-047 scope and production qualification remain mandatory; no issue or dependent requirement is completed by association.

Source progress through #36 now includes [owned watermarks/stamps and artifact classification](stamps.md). Full FP-018 profiles and production qualification remain mandatory; dependent requirements remain independently scoped.

Source progress through #37 now includes [resource extraction](resource-extraction.md). Full FP-028 scope and production qualification remain mandatory; dependent requirements remain independently scoped.

Source progress through #38 now includes [search and text replacement](text-search.md). Full FP-026 scope and production qualification remain mandatory; dependent requirements remain independently scoped.

Source progress through #39 now includes [table/structured-data extraction](table-extraction.md). Full FP-027 scope and production qualification remain mandatory; dependent requirements remain independently scoped.

Source progress through #40 now includes [document events and typed generation](data-generation.md). Full FP-099 scope and production qualification remain mandatory; dependent requirements remain independently scoped.

Source progress through #41 now includes [text and structured conversion](input-conversion.md). Full FP-050 scope and production qualification remain mandatory; dependent requirements remain independently scoped.

Source progress through #42 now includes [optional Word interchange](word-conversion.md). Full FP-053 scope and production qualification remain mandatory; dependent requirements remain independently scoped.

Source progress through #43 includes [document templates/mail merge](mail-merge.md). Full FP-112 scope and production qualification remain mandatory; #44–#50 remain in progress.

Source progress through #44 includes [logical structure/accessibility](accessibility.md). Full FP-067 scope and production qualification remain mandatory; #45–#50 remain in progress.

Source progress through #45 includes [explicit local OCR](local-ocr.md). Full FP-074 scope and production qualification remain mandatory; #46–#50 remain in progress.

Explicit local model catalogs, exact mixed-model binding and bounded Unicode CER/WER measurement now have a source profile. Complete multilingual/model/native-host and production qualification remain pending; a retained mixed-language accuracy case fails its prior threshold. See [multilingual OCR](ocr-models.md).

Explicit raster-aligned invisible OCR overlays, finite existing-digital-text handling, shared-context render/recognize/write and bounded text/JSON/hOCR/ALTO exports now have a source profile. Complete structure, real OCR/native/host and production qualification remain pending. See [searchable OCR](searchable-ocr.md).

Opt-in bounded scan correction, original-image preservation, inverse word-boundary geometry and separate prose/table/form reading-order evidence now have a source profile. Complete automatic curve/semantic/native/corpus and production qualification remain pending. See [scan processing](scan-processing.md).

Queue issues #51–#60 now have documented source profiles: [signature verification and explicit trust](signature-verification.md), [external and hardware signing sessions](external-signing.md), [pades upgrades and offline timestamp verification](pades.md), [signed revisions and conservative edit preflight](revision-audit.md), [document comparison and change reports](document-comparison.md), [passive xfa packets and explicit static conversion](xfa.md), [passive scripts and opt-in bounded calculations](scripts.md), [explicit icc color operations and production-rendering contracts](color-rendering.md), [bounded lossless raster containers](raster-export.md), [headless viewing sessions and exact tiles](viewing.md). Full requirement scope and actual minimum/runtime/provider/final-package qualification remain mandatory. PR #118 is merged; this batch follows it in the existing dependency order.

Issues #51–#60 were merged in [PR #119](https://github.com/kirakosyan/pdf.net/pull/119) at `d9fa26d0fbfe93e524b25dcb6712ac8ffa51c502`, after all 14 review threads were addressed and the final source CI run 39482424587 passed on all six hosts. Complete requirement profiles and production qualification remain open. Queue #61–#80 continues from that merged base in the existing dependency order.

Queue #61–#80 is merged through [PR #120](https://github.com/kirakosyan/pdf.net/pull/120) at `0ad7a08`. All15 review threads were resolved and six source CI hosts passed; an unchanged Windows retry passed after a retained loopback TLS fixture failure. Full requirement/production status remains open. The remaining SDK assignment sequence continues at #81; its [optimization implementation](optimization.md) and [source evidence](evidence/FP-072-source.json) retain the mandatory qualification gaps.

The remaining queue is tracked in draft [PR #121](https://github.com/kirakosyan/pdf.net/pull/121). Source implementations and exact evidence are recorded for [#81 optimization](evidence/FP-072-source.json) and [#82 progressive access](evidence/FP-073-source.json); the next assignment is #83. Every original production acceptance gate remains mandatory and open where evidence is incomplete.

[#83 barcode source implementation](barcodes.md) and [exact evidence](evidence/FP-078-source.json) are recorded in draft PR #121. The next assignment is #84; original full production gates and incomplete feature qualifications remain open.

[#84 optional intelligence adapters](document-intelligence.md) and [source evidence](evidence/FP-079-source.json) are recorded in draft PR #121. Source CI run137 passed all six hosts for the preceding #81–#83 checkpoint. The next assignment is #85. Every complete feature/production gate remains mandatory.

[#85 explicit semantic chunks and scoped retrieval](semantic-retrieval.md) and [source evidence](evidence/FP-080-source.json) are recorded in draft PR #121. Source CI run138 passed all six hosts for the preceding #81–#84 checkpoint. The next assignment is #86. Complete tagged semantics, actual model/store/relevance/security/runtime/final-package and all original production gates remain mandatory.

[#86 bounded Windows automation](windows-automation.md) and [source evidence](evidence/FP-081-source.json) are recorded in draft PR #121 with actual native/VBScript x86/x64 and independent-output observations. The earlier #85 Framework assertion correction passed all six source hosts in run140. The next assignment is #87. Full feature, runtime/host/apartment/installer/final-package and all original production qualification gates remain mandatory.

[#87 application views](application-views.md) and [source evidence](evidence/FP-082-source.json) now include actual six-application Windows and two-runtime Chromium observations in draft PR #121. The next assignment is #88. Complete feature, native rendering, manual accessibility, minimum runtime and final-package qualification remains mandatory.

[#88 CLI and batch automation](command-line.md) and [source evidence](evidence/FP-083-source.json) now include actual process/pipe/protected-input/atomic-output/batch checks on both modern runtimes across all six hosts, with independent reader and exact original-vector observations. A Windows Python consumer decoding correction is recorded separately from unchanged product code. The next assignment is #89. Full feature/provider/host/security/performance/fuzz/final-distribution qualification remains mandatory.

[#89 developer documentation and recipes](developer-guide.md) and [source evidence](evidence/FP-084-source.json) now include a metadata-only 15-library reference and actual source recipes across all six hosts (19 profiles/152 workflows). The next assignment is #90. All 33 narrative-fragment caller/provider bindings, exact published-package, minimum-runtime and complete production qualification gates remain mandatory.

At the cloud checkpoint, [#90 runtime evidence tooling](runtime-qualification.md) and [source evidence](evidence/FP-086-source.json) bound an unchanged 22-lane declaration to actual process/core-library/live-registry/loaded SDK identities, complete TRX checks and exact package/feed/test-inventory consistency tools. Clean source had 1,660 selected passing cases on each local modern/fallback profile, with five native classes explicitly excluded because local Landlock was unavailable. A Windows Docker launcher provided an unqualified actual-image preflight; no Windows container or minimum lane was executed in that cloud session. #91 is the next independent source assignment; no requirement is closed by these tooling results.

The local Windows continuation adds an offline Framework source runner and a complete, hashed CI transfer bundle for isolated Windows VMs. All five source workflows passed on `dec0d2c`; the exact tested merge snapshot is `519a016`. Windows PowerShell 5.1 executed the complete 1,734-case suite on current Framework and actual minimum Framework 4.7.2, at both x64 and x86, with zero failures/skips in those four runs. Minimum execution used a separate offline Hyper-V guest, with same-process identities and all loaded SDK hashes independently checked. The [Windows source evidence](evidence/FP-086-windows-source.json) also retains the failed software-emulated/local/earlier CI observations and archive hashes. FP-086/#90 remains open for qualified host provenance, exact final-package consumers and complete production suites; see the [runtime observations](runtime-qualification.md#windows-source-observations-2026-10-23). #91 remains the next independent source assignment.

All five final PR #121 workflows passed on `2669cc5`, including the six-host source-contract workflow [run 40029635310](https://github.com/kirakosyan/pdf.net/actions/runs/40029635310). Subsequent [#91 asynchronous processing](async-processing.md) adds a bounded pool, private thread-bound documents, shared contexts, admission backpressure, aggregate progress and cancellation-aware cleanup. Its [source evidence](evidence/FP-087-source.json) records each actual local observation separately from that earlier CI checkpoint. #92 is the next independent source assignment after these checks; #91 remains open for complete provider/runtime/package and endurance qualification.

All five source workflows also passed for #91 at `de94644`, including [run 40033103252](https://github.com/kirakosyan/pdf.net/actions/runs/40033103252). [#92 performance measurement](performance.md) adds a pinned original corpus, bounded source consumer, retained-sample percentile verification and reviewed regression envelopes enforced by the existing release gate. [Source evidence](evidence/FP-088-source.json) records 320 measured operations across two local modern runtimes and eleven checker tests; proposed envelopes remain explicitly unreviewed. All five #92 workflows passed at `f749ac4`, including [run 40077426801](https://github.com/kirakosyan/pdf.net/actions/runs/40077426801). Full performance/provider/runtime/package and endurance qualification remains mandatory.

#93 is skipped at the user's request and remains a mandatory release blocker. Its unfinished local fuzzing harness and reports are retained outside tracked source; no complete campaign or qualification is claimed. [#94 fidelity corpus](fidelity-corpus.md) introduces four original fixtures, reproducible authoring, bounded source round trips and independent semantic/pixel checks. [Evidence](evidence/FP-090-source.json) distinguishes these controlled observations from the complete required corpus.

All five #94 source workflows passed at `0d801a6`, including [run 40079164964](https://github.com/kirakosyan/pdf.net/actions/runs/40079164964). [#95 deterministic output and tenancy](determinism-caching.md) adds private bounded SHA-256 content caches, operation-budgeted detached copies and explicit repeated-process metadata/compression profiles. [Source evidence](evidence/FP-091-source.json) records actual runtime, concurrency, process and independent-reader observations. Complete provider profiles, corpus, minimum runtimes, final packages and endurance remain mandatory.

[#96 observations and support diagnostics](observability.md) adds opt-in bounded events, numeric progress, worker correlation, caller-drained sinks and sanitized support JSON. [Source evidence](evidence/FP-092-source.json) records selected policy/processing/cache/native/runtime regressions and independent bundle/PDF checks, with complete provider/privacy/runtime/package qualification still pending. #97 is next in the requested sequence.

All five #95 workflows passed at `48f3f11`, including [run 40080488148](https://github.com/kirakosyan/pdf.net/actions/runs/40080488148). [#97 package provenance](package-reproducibility.md) adds guarded shared metadata, exact SDK dependency versions, fresh source rebuild checks and offline symbol/archive inspection. [Evidence](evidence/FP-093-source.json) records source observations only; final candidates, independent clean-checkout rebuilds and full package qualification remain pending. #98 is next; #93 remains skipped and release-blocking.

[#98 supply-chain inventory](supply-chain.md) adds locked NuGet content verification, a structurally checked incomplete CycloneDX source SBOM, notice/asset observations and fail-closed advisory checks. [Evidence](evidence/FP-095-source.json) retains initial unavailable-package observations, final inspection of all locked packages and unresolved license/provenance scope. It also fixes the preceding notice filter for uppercase and extensionless licenses. #99 is the excluded Reader; #100 is next. The #97 Linux report-directory CI correction is recorded in its evidence and awaits the corrected hosted result. Publication remains blocked.

[#100 production workflow acceptance](workflow-acceptance.md) is blocked by missing complete providers and qualified final packages; [the blocker record](evidence/FP-097-blocked.json) claims zero acceptance runs. Source fixtures cannot replace that requirement. Continue independent source issue #101, then revisit #100 when its prerequisites are satisfied.

[#101 document collections and object XML](document-collections.md) has a source checkpoint in draft PR #121. [Exact evidence](evidence/FP-100-source.json) distinguishes bounded collection/XML behavior from untrusted adapter isolation and production qualification. The next source assignment is #102. #100 remains blocked and #93 remains user-skipped; no candidate or publication is authorized.

[#102 semantic review and remediation](semantic-remediation.md) adds exact-source review plans, explicit ordered decisions, per-fix structural revalidation, role mapping and retained [source evidence](evidence/FP-101-source.json). Human/screen-reader and complete production qualification remain open. The next source assignment is #103; #100 remains blocked and #93 user-skipped.

[#103 image cleanup](image-cleanup.md) adds explicit loss authority, retained source rasters, bounded transforms and separate measured blank observations. [Evidence](evidence/FP-103-source.json) keeps real-corpus/OCR/final-package qualification open. The next independent source assignment is #104; #100 remains blocked and #93 user-skipped.

[#104 business observations](business-data.md) adds exact-source versioned JSON/XML, passive declared fields/structure, inferred tables and separate source-bound OCR under shared budgets. [Evidence](evidence/FP-104-source.json) retains source-only limits and real-corpus qualification gaps.

[#105 native distribution](native-distribution.md) is blocked by missing complete hosts and unqualified distribution closure; [evidence](evidence/FP-105-blocked.json) records zero offline installation acceptance runs. The next independent assignment is #106; #100 remains blocked and #93 user-skipped.

[#106 maintenance](maintenance.md) adds compatibility, deprecation, lifecycle, security servicing and rollback policy plus an executable source API/release-guard drill. [Evidence](evidence/FP-106-source.json) distinguishes tool observations from unapproved support commitments and missing final-package qualification. #107 is next; #100/#105 remain blocked and #93 remains user-skipped.

[#107 legacy-format host integration](legacy-format-import.md) adds bounded explicit DjVu/CDR host contracts and failure handling. [Evidence](evidence/FP-107-source.json) covers original host fixtures only, with no actual DjVu/CDR decoding or version support claimed. Decoder/independent corpus and production qualification remain open. #108 is next.

[#108 text/Markdown interchange](text-interchange.md) adds exact-source explicit reading-order projections, caller-assigned roles, inferred tables and generated page-image bundles. [Evidence](evidence/FP-108-source.json) retains finite parser/Unicode/asset observations and complete semantic/corpus/runtime qualification gaps. #109 is next.

[#109 task facades](task-facades.md) compose existing engines with explicit immutable instance configuration and shared generation budgets. [Source evidence](evidence/FP-109-source.json) separates equivalence and language-consumer checks from full underlying qualification. #110 is next.

[#110 HTML element extraction](html-element-extraction.md) is blocked by the complete browser-host prerequisite. [Evidence](evidence/FP-110-blocked.json) records zero element-capture acceptance runs and the precise geometry/resource/dynamic-DOM work still required. Continue independent managed #111 work; no element capture is claimed.
