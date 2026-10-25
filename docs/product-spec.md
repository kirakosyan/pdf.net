# Fluent.Pdf production specification

## Objective and settled decisions

Build an original, coherent PDF SDK whose required capability set covers the combined reference surfaces and adds measurable quality controls. Its public identity is Fluent.Pdf; MIT is the repository license. Windows, Linux and macOS are required. Full .NET Framework applies to Windows; modern .NET is the cross-platform runtime. Optional native engines are allowed. Stable publication waits for production readiness.

The project includes a separately scoped PDF Reader placeholder. Its reference analysis, feature specification, UI technology and implementation are deferred.

## Normative scope

The [112-item capability catalogue](feature-catalogue.md) indexes the detailed requirement contracts. [specs/requirements.json](../specs/requirements.json) is the machine-readable source. Requirement IDs are stable and must remain in issue titles, evidence and documentation. SDK requirements are mandatory; sequencing does not imply optional scope. The user explicitly deferred FP-089/#93 as a blocker for version 1.0.0; the [release exception](release-exceptions.md) preserves its unqualified status and the automated security gates. Reader remains the only catalogue-level non-release requirement.

The scope includes:
- PDF object/stream syntax, PDF 1.2–2.0, full and incremental writes, repair and low-level operators.
- Fluent generation, text shaping and fonts, graphics/color, tables, lists, pagination, templates and page organization.
- Annotations, bookmarks, links/actions, attachments/portfolios, metadata, layers, forms, XFA and bounded script evaluation.
- Text/geometry/table/resource extraction, search/replace, structured export, document comparison and irreversible redaction.
- Encryption, signatures, certificate trust, hardware/external signing, timestamps, PAdES and revision-aware editing.
- Headless rendering, color-managed output, viewing primitives and platform printing.
- HTML/CSS/JavaScript conversion, resource/auth policies, readiness, print layout, framework adapters and isolated workers.
- The complete directional conversion inventory below, including less common printer/vector/fixed-layout formats.
- Independent PDF/A, PDF/UA, PDF/X, PDF/E, electronic invoice and preflight/remediation workflows.
- Local multilingual OCR, searchable PDF, scan preprocessing, barcode workflows and opt-in intelligence/retrieval adapters.
- Qualified deployments, security/resource/concurrency guarantees, executable documentation, maintainability and stable packages.

## Directional format inventory

These are planned production obligations. Provider selection never substitutes a differently named format silently.

| Format family | Input to PDF | Output from PDF | Primary requirement |
| --- | --- | --- | --- |
| PDF 1.2–2.0 | Open/edit | Full/incremental | FP-003–007, FP-098 |
| HTML/files/URLs, MHTML | Yes, including dynamic assets | Fixed/reflow/self-contained HTML | FP-044–048, FP-056 |
| TXT, Markdown | Yes | Yes | FP-050, FP-108 |
| Native XML schema, JSON data | Generation | Structured content JSON/XML; PDF XML interchange | FP-050, FP-100, FP-104 |
| DOC, DOCX, RTF | Optional wordnet adapter | Optional editable/fixed-layout adapter | FP-053, FP-112 |
| XLS, XLSX, ODS, CSV, SpreadsheetML | Optional sheets/ranges | Tables/pages and typed data | FP-054 |
| PPTX | Optional adapter | Editable or raster slides | FP-055 |
| EPUB | Yes | Yes; MobiXML export additionally | FP-057 |
| JPEG, PNG, BMP, GIF, TIFF | Yes; frame controls | Yes; multipage TIFF included | FP-051, FP-064 |
| WebP, HEIF/HEIC, extra codecs | Explicit qualified optional codecs | Only declared/qualified encoders | FP-051, FP-064 |
| SVG | Yes | Vector-first export | FP-052 |
| PS/EPS/PCL | PS/EPS; PCL command/version matrix | PostScript | FP-058 |
| XPS/OpenXPS | Yes | Yes | FP-059 |
| TeX/LaTeX | Isolated optional typesetting | Explicit reconstructed subset | FP-060 |
| CGM, WMF/EMF/EMF+ | Explicit version/record matrices | EMF where qualified | FP-061 |
| XSL-FO/XML+XSLT | Yes | No general reverse transformation promised | FP-062 |
| OFD | Yes | Yes | FP-063 |
| DjVu, CDR | Explicit qualified import adapters | No export obligation | FP-107 |
| FDF, XFDF, XML/JSON form data | Field data | Field data | FP-037 |
| hOCR, ALTO, OCR JSON | Optional OCR interchange | Yes | FP-076 |
| PDF/A, PDF/UA, PDF/X, PDF/E | Inspect/convert/remediate | Exact advertised profiles | FP-066–071 |

Archive containers and MHTML resources require expansion and path traversal controls. HEIF and other patent/licensing-sensitive codecs remain optional providers with a documented distribution decision and evidence; optional installation does not make a requirement optional.

## API and behavioral contract

A fluent facade and editable document model share one processing context. Every operation identifies providers and dependencies, propagates cancellation and stable diagnostics, reports fidelity loss, and respects resource/network policies. Safe inspection is distinct from rendering/evaluating active content. Lossy conversion and inferred reconstruction are explicit.

Stream APIs preserve caller ownership and define seekability requirements. Paths use atomic replacement. Sensitive data never enters logs by default. Credentials and private keys remain under caller control. Mutation of a single document is serialized; independent documents support bounded parallel operation.

The implemented FP-001 surface and executable C#/VB examples are described in [api-contract.md](api-contract.md); FP-002 behavior is defined in [operation-policies.md](operation-policies.md). Other examples shown before implementation remain design proposals. Public API analyzers and cross-TFM compatibility checks enforce the checked-in contract; pre-release API changes require explicit review.

## Definition of better

The improvement target is testable rather than a blanket superiority claim:
- Predictable deployment: optional engines, offline provisioning, explicit ABI/RID probes, no surprise downloads.
- Reliable operations: strict diagnostics, cancellation, atomic writes, tenant isolation and documented resource envelopes.
- Fidelity: independently compared visual, textual, geometric and structural output, including multilingual documents.
- Trustworthy standards/security: validators, real redaction recovery checks, explicit certificate trust and signed revision handling.
- Consistent developer experience: stable contracts, small common-task APIs, executable samples and useful error reports.

Per-workload numerical gates and release evidence are defined in [production-quality.md](production-quality.md). Performance targets must be measured on pinned hardware/corpus before claiming an improvement.

## Delivery status

Requirements discovery and issue preparation are complete. The canonical manifest distinguishes reviewed status from source implementation progress. Document/provider contracts and operation policies have source implementations, as do the dependency-ordered issues #3–#20: isolated workers, syntax/filters/writing, content/fonts/Unicode, graphics/canvas/layout, tables/composition/templates, page organization, images, annotations, navigation and outlines/presentation. Managed metadata/identity, embedded-file/collection editing, optional-content state/flattening and page/Form text extraction and passive AcroForm creation/editing and bounded form data/workflows now also have source profiles under validation. Their source contracts and independent profile reports are recorded in the feature documentation and evidence ledger. Optional headless rendering, password/certificate security, independently gated archival conversion, explicit structural recovery, PDF version/extension preservation and optional HTML conversion/resource authentication/readiness/print layout and owned stamp/artifact editing now also have source profiles. Complete profiles, later SDK requirements and all missing production qualification remain mandatory. Source CI, newer-runtime tests and small independent fixtures do not qualify final packages or permit packing/publication.

FP-028 has an original [resource extraction profile](resource-extraction.md); full required resource/program profiles and production qualification remain mandatory.

FP-026 has a [search/replacement source profile](text-search.md); complete profiles and production qualification remain mandatory.

FP-027 has a [table extraction source profile](table-extraction.md); complete detection/reconstruction, typed interchange and production qualification remain mandatory.

FP-099 has a [typed generation/event source profile](data-generation.md); complete event/model/callback/data coverage and production qualification remain mandatory.

Explicit raster/full-rewrite redaction and sanitization now have a source profile: selected samples are replaced before encoding and no original graph/history is adopted. Complete selective editable-content preservation, independent renderer/security corpora and production qualification remain mandatory. See [redaction](redaction.md).

Explicit pre-created signature fields, exact incremental detached CMS signing, bounded PKCS12/borrowed-key providers and conservative DocMDP/FieldMDP edit barriers now have a source profile. Complete signature/trust/revocation/PAdES/edit-class/native/runtime qualification remains mandatory. See [signatures](signatures.md).
