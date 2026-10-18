# Implementation order and production milestones

## Assignment rule

Use the dependency graph in [requirements.json](../specs/requirements.json) and [GitHub roadmap #113](https://github.com/kirakosyan/pdf.net/issues/113). Assign one issue at a time in the concrete order below unless the user requests parallel work. A task is ready only when its dependencies have delivered reviewed public contracts and the evidence its implementation needs. Do not equate a closed issue with evidence if the implementation or release claims disagree.

Each issue is unassigned at creation and has scope, dependencies, acceptance criteria, non-goals and validation. [github-backlog.md](github-backlog.md) provides the issue mapping. This sequence is topologically sorted; it intentionally brings native deployment and a useful managed document workflow forward.

FP-001 source contracts are merged through [PR #115](https://github.com/kirakosyan/pdf.net/pull/115). FP-002 is merged through [PR #116](https://github.com/kirakosyan/pdf.net/pull/116) and implements [operation policies and budgets](operation-policies.md), with [runtime/API tests](testing.md). Both tracking issues remain open for production qualification, including actual minimum-runtime and final-package consumers. The foundation milestone and all later SDK requirements remain incomplete; passing source tests does not authorize publication.

FP-085 (#3) now has an original [offline deployment and isolated worker implementation](native-deployment.md), including actual ABI/version/dependency probes, platform loading restrictions, process supervision and crash recovery. [Source CI](evidence/FP-085-source.json) passed its native boundaries on all six hosts; production qualification and final-package consumers remain incomplete. Managed syntax, filters, writer and typed content/version handling now have source implementations. The writer baseline passed [6,740 source test executions](evidence/FP-005-source.json); complete SDK qualification remains pending. Issues #3–#20 were merged in [PR #117](https://github.com/kirakosyan/pdf.net/pull/117) at `8df53a3922fdabfb095ca5565b03898c3c93d54e`. Its final source CI passed 679 tests in each of 20 runtime lanes; an earlier Framework x64 worker deadline observation remains unresolved for production performance qualification. Their tracking issues remain open for complete qualification. Issues #21–#50 continue in dependency order in [PR #118](https://github.com/kirakosyan/pdf.net/pull/118). Metadata source CI passed 698 tests per runtime lane (13,960 executions); later work requires its own evidence.

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
