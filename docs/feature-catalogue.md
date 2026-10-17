# Fluent.Pdf capability catalogue

This is the required SDK production scope. FP-001 has an [implemented source contract layer](api-contract.md), and FP-002 implements [operation policies and budgets](operation-policies.md), both with production qualification pending. Other SDK capabilities below remain planned. The separate Reader placeholder exists. Requirement details and acceptance criteria are normative; this table is an index. [Machine-readable requirements](../specs/requirements.json).

| ID | Capability | Area | Release required |
| --- | --- | --- | --- |
| [FP-001](requirements/FP-001.md) | Public API and provider contracts | foundation | Yes |
| [FP-002](requirements/FP-002.md) | Operation policies, diagnostics and resource budgets | foundation | Yes |
| [FP-003](requirements/FP-003.md) | PDF syntax and cross-reference reader | foundation | Yes |
| [FP-004](requirements/FP-004.md) | Stream filters, object streams and codecs | foundation | Yes |
| [FP-005](requirements/FP-005.md) | PDF writer and incremental transactions | foundation | Yes |
| [FP-006](requirements/FP-006.md) | Repair, recovery and structural validation | foundation | Yes |
| [FP-007](requirements/FP-007.md) | Content operators and PDF version negotiation | foundation | Yes |
| [FP-008](requirements/FP-008.md) | Fonts, embedding, subsetting and fallback | composition | Yes |
| [FP-009](requirements/FP-009.md) | Unicode, bidirectional text and shaping | composition | Yes |
| [FP-010](requirements/FP-010.md) | Vector graphics, color and transparency | composition | Yes |
| [FP-011](requirements/FP-011.md) | Page canvas and geometry | composition | Yes |
| [FP-012](requirements/FP-012.md) | Flow layout and pagination | composition | Yes |
| [FP-013](requirements/FP-013.md) | Tables and data binding | composition | Yes |
| [FP-014](requirements/FP-014.md) | Lists, columns and document composition | composition | Yes |
| [FP-015](requirements/FP-015.md) | Headers, footers, page numbers and contents | composition | Yes |
| [FP-016](requirements/FP-016.md) | Page organization and imposition | composition | Yes |
| [FP-017](requirements/FP-017.md) | Images and reusable page graphics | composition | Yes |
| [FP-018](requirements/FP-018.md) | Watermarks, stamps and artifacts | composition | Yes |
| [FP-019](requirements/FP-019.md) | Annotation model and appearance streams | interaction | Yes |
| [FP-020](requirements/FP-020.md) | Links, destinations, actions and navigation | interaction | Yes |
| [FP-021](requirements/FP-021.md) | Outlines, page labels and viewer preferences | interaction | Yes |
| [FP-022](requirements/FP-022.md) | Metadata and document identity | interaction | Yes |
| [FP-023](requirements/FP-023.md) | Attachments, portfolios and associated files | interaction | Yes |
| [FP-024](requirements/FP-024.md) | Optional-content layers | interaction | Yes |
| [FP-025](requirements/FP-025.md) | Text extraction with layout geometry | extraction | Yes |
| [FP-026](requirements/FP-026.md) | Search, regex and text replacement | extraction | Yes |
| [FP-027](requirements/FP-027.md) | Table and structured-data extraction | extraction | Yes |
| [FP-028](requirements/FP-028.md) | Graphics, image, font and artifact extraction | extraction | Yes |
| [FP-029](requirements/FP-029.md) | Document comparison and change reports | extraction | Yes |
| [FP-030](requirements/FP-030.md) | True redaction and comprehensive sanitization | security | Yes |
| [FP-031](requirements/FP-031.md) | Password and certificate encryption | security | Yes |
| [FP-032](requirements/FP-032.md) | Digital signatures and certification | security | Yes |
| [FP-033](requirements/FP-033.md) | Signature verification and trust decisions | security | Yes |
| [FP-034](requirements/FP-034.md) | HSM and external signing providers | security | Yes |
| [FP-035](requirements/FP-035.md) | PAdES, timestamps and long-term validation | security | Yes |
| [FP-036](requirements/FP-036.md) | AcroForm creation and editing | interaction | Yes |
| [FP-037](requirements/FP-037.md) | Form filling, data exchange and flattening | interaction | Yes |
| [FP-038](requirements/FP-038.md) | XFA forms and conversion | interaction | Yes |
| [FP-039](requirements/FP-039.md) | Document JavaScript and sandboxed calculations | security | Yes |
| [FP-040](requirements/FP-040.md) | PDF page rendering | rendering | Yes |
| [FP-041](requirements/FP-041.md) | Color-managed production rendering | rendering | Yes |
| [FP-042](requirements/FP-042.md) | Headless viewing primitives | rendering | Yes |
| [FP-043](requirements/FP-043.md) | Cross-platform printing | rendering | Yes |
| [FP-044](requirements/FP-044.md) | HTML, CSS and JavaScript to PDF | web | Yes |
| [FP-045](requirements/FP-045.md) | Resource loading, authentication and network policy | web | Yes |
| [FP-046](requirements/FP-046.md) | Dynamic-page readiness and browser lifecycle | web | Yes |
| [FP-047](requirements/FP-047.md) | Print CSS, HTML pagination and accessibility | web | Yes |
| [FP-048](requirements/FP-048.md) | Web framework and XAML adapters | web | Yes |
| [FP-049](requirements/FP-049.md) | Isolated rendering service and container deployment | web | Yes |
| [FP-050](requirements/FP-050.md) | Text, Markdown and structured input | conversion | Yes |
| [FP-051](requirements/FP-051.md) | Raster documents to PDF | conversion | Yes |
| [FP-052](requirements/FP-052.md) | SVG import and vector export | conversion | Yes |
| [FP-053](requirements/FP-053.md) | Word document conversion adapter | conversion | Yes |
| [FP-054](requirements/FP-054.md) | Spreadsheet and tabular conversion | conversion | Yes |
| [FP-055](requirements/FP-055.md) | Presentation conversion | conversion | Yes |
| [FP-056](requirements/FP-056.md) | PDF to HTML and semantic web output | conversion | Yes |
| [FP-057](requirements/FP-057.md) | EPUB and electronic-book conversion | conversion | Yes |
| [FP-058](requirements/FP-058.md) | PostScript, EPS and printer-language import | conversion | Yes |
| [FP-059](requirements/FP-059.md) | XPS and OpenXPS conversion | conversion | Yes |
| [FP-060](requirements/FP-060.md) | TeX and LaTeX conversion | conversion | Yes |
| [FP-061](requirements/FP-061.md) | CGM and platform metafile conversion | conversion | Yes |
| [FP-062](requirements/FP-062.md) | XSL-FO and XML transformation | conversion | Yes |
| [FP-063](requirements/FP-063.md) | OFD fixed-layout conversion | conversion | Yes |
| [FP-064](requirements/FP-064.md) | PDF raster export and image containers | conversion | Yes |
| [FP-065](requirements/FP-065.md) | Conversion orchestration and format discovery | conversion | Yes |
| [FP-066](requirements/FP-066.md) | PDF/A archival profiles | compliance | Yes |
| [FP-067](requirements/FP-067.md) | Tagged PDF and PDF/UA accessibility | compliance | Yes |
| [FP-068](requirements/FP-068.md) | PDF/X prepress profiles | compliance | Yes |
| [FP-069](requirements/FP-069.md) | PDF/E engineering and advanced content | compliance | Yes |
| [FP-070](requirements/FP-070.md) | Electronic invoices and associated XML | compliance | Yes |
| [FP-071](requirements/FP-071.md) | Preflight, remediation and audit reports | compliance | Yes |
| [FP-072](requirements/FP-072.md) | Optimization, compression and deduplication | production | Yes |
| [FP-073](requirements/FP-073.md) | Linearization and progressive access | production | Yes |
| [FP-074](requirements/FP-074.md) | Local OCR provider and wordnet reuse | ocr | Yes |
| [FP-075](requirements/FP-075.md) | Multilingual OCR and language models | ocr | Yes |
| [FP-076](requirements/FP-076.md) | Searchable PDF and structured OCR output | ocr | Yes |
| [FP-077](requirements/FP-077.md) | Scan preprocessing and reading order | ocr | Yes |
| [FP-078](requirements/FP-078.md) | Barcode and QR document workflows | ocr | Yes |
| [FP-079](requirements/FP-079.md) | Opt-in document intelligence providers | intelligence | Yes |
| [FP-080](requirements/FP-080.md) | Semantic chunking and retrieval adapters | intelligence | Yes |
| [FP-081](requirements/FP-081.md) | Windows COM interoperability | integration | Yes |
| [FP-082](requirements/FP-082.md) | Application integrations and viewer controls | integration | Yes |
| [FP-083](requirements/FP-083.md) | Command-line and batch automation | integration | Yes |
| [FP-084](requirements/FP-084.md) | Developer documentation and executable examples | integration | Yes |
| [FP-085](requirements/FP-085.md) | Native provider deployment and isolation | production | Yes |
| [FP-086](requirements/FP-086.md) | Runtime and architecture qualification | production | Yes |
| [FP-087](requirements/FP-087.md) | Async processing, concurrency and cancellation | production | Yes |
| [FP-088](requirements/FP-088.md) | Performance, streaming and resource envelopes | production | Yes |
| [FP-089](requirements/FP-089.md) | Security, fuzzing and malformed-content robustness | production | Yes |
| [FP-090](requirements/FP-090.md) | Independent fidelity and interoperability corpus | production | Yes |
| [FP-091](requirements/FP-091.md) | Deterministic output, caching and tenancy | production | Yes |
| [FP-092](requirements/FP-092.md) | Observability and support diagnostics | production | Yes |
| [FP-093](requirements/FP-093.md) | Reproducible NuGet packages and API compatibility | production | Yes |
| [FP-094](requirements/FP-094.md) | Production release and Trusted Publishing | production | Yes |
| [FP-095](requirements/FP-095.md) | Dependency licensing and supply-chain inventory | production | Yes |
| [FP-096](requirements/FP-096.md) | PDF Reader placeholder boundary | reader | No |
| [FP-097](requirements/FP-097.md) | Production document workflow acceptance | production | Yes |
| [FP-098](requirements/FP-098.md) | PDF 2.0 and extension preservation | foundation | Yes |
| [FP-099](requirements/FP-099.md) | Document event hooks and data-source generation | composition | Yes |
| [FP-100](requirements/FP-100.md) | Document collections, packages and interchange | integration | Yes |
| [FP-101](requirements/FP-101.md) | Accessibility remediation and semantic authoring | compliance | Yes |
| [FP-102](requirements/FP-102.md) | Signature-preserving editing and revision audit | security | Yes |
| [FP-103](requirements/FP-103.md) | Image processing and document cleanup | rendering | Yes |
| [FP-104](requirements/FP-104.md) | Business data extraction and export schemas | extraction | Yes |
| [FP-105](requirements/FP-105.md) | Cross-platform native distribution packages | production | Yes |
| [FP-106](requirements/FP-106.md) | Compatibility policy and long-term maintenance | production | Yes |
| [FP-107](requirements/FP-107.md) | DjVu and CDR document import | conversion | Yes |
| [FP-108](requirements/FP-108.md) | PDF to Markdown and text interchange | conversion | Yes |
| [FP-109](requirements/FP-109.md) | Task-oriented facades and plugin composition | integration | Yes |
| [FP-110](requirements/FP-110.md) | HTML element and block extraction | web | Yes |
| [FP-111](requirements/FP-111.md) | Hyphenation and advanced text-fit controls | composition | Yes |
| [FP-112](requirements/FP-112.md) | Document templates and mail merge | composition | Yes |
