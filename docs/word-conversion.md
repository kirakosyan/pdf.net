# Word conversion adapter

FP-053 has a managed **source-validation profile**. Complete bidirectional DOC/DOCX/RTF fidelity, fixed/editable reconstruction, source-format security, corpus/runtime/package qualification and every production gate remain mandatory. Source checks do not authorize candidate creation or publication.

`PdfWordConversion.Import` captures a borrowed DOC, DOCX or RTF source at its current position, converts privately through an explicit `IPdfWordConverter`, validates the resulting PDF under the same operation context and returns a caller-owned editable PDF model. `Write` exports an immutable PDF snapshot to a borrowed Word output. `WriteFile` serializes privately before atomic destination replacement. Caller streams stay open. Failures preserve an existing destination; a caller-stream write failure may leave a prefix.

Core contains only contracts and shared-budget boundaries. The optional, non-packable `Fluent.Pdf.Word` assembly references core and the exact published `wordnet [1.1.0]` package. Core references neither optional adapter nor wordnet. Source dependency inventory, hashes and notices are in [the dependency inventory](../third-party/word/components.json).

## Exact dependency and explicit assets

The adopted package SHA256 is `073bc06d91367c340f6f031e8cf2066f8a18d735cd93968d37f346d77c7bd4e5`; its published source revision is `bc6ada467dd4626dd23dfbee5f848b21e9f67dc5`. Actual package-only probes verify DOC↔DOCX and DOC/DOCX→PDF entry points. The later reviewed OCR/source snapshot is a different revision. Its newer document-editing API is absent from this package and is not used by this adapter.

The published package has no RTF format. RTF conversion uses original bounded parsing/writing and an original WordprocessingML bridge, followed by the actual pinned DOCX/PDF or DOCX/DOC conversion when needed. It does not advertise fictitious upstream RTF support or silently switch to a newer source assembly.

Construct `WordnetPdfConverter` explicitly. Its required bundled-font adoption parameter reflects a real dependency constraint: the pinned package cannot disable availability of its bundled Noto Sans faces. False rejects with `Word.BundledFontPolicy`. True adopts that reviewed font pack and its supplied license; it does not permit OS font discovery. Select an explicit fallback family and optional `PdfWordnetFont` faces. Faces are immutable caller-selected TrueType programs, emitted under the root context with embedding restrictions checked. Font substitutions and dependency losses remain diagnostic.

No renderer, OCR module, model, remote resource, font search path or native binary is installed or downloaded by the adapter. Ordinary conversion never activates source document actions.

## Reconstruction and fidelity

`PdfWordReconstruction.Editable` currently recovers mapped Unicode into explicit page-level paragraphs with the selected fallback font. `Word.RecoveredTextProvenance` distinguishes recovered Unicode from reconstructed authoring structure. `Word.EditableTextReconstruction` reports omitted original geometry, mixed styles, graphics, tables, annotations and semantic/interactive structure. A painted page with no recoverable Unicode fails with `Word.NoRecoverableText`, allowing the caller to choose fixed layout or a separately configured OCR workflow. It does not invent recognized text.

`PdfWordReconstruction.FixedLayout` requires an explicitly registered `IPdfPageRenderer`. It renders the selected page CropBox/rotation at explicit DPI under the same context, verifies exact opaque BGRA output and writes bounded original RGB PNGs. DOCX uses page-sized anchored images and explicit section geometry; RTF uses PNG picture destinations. `Word.FixedLayoutRasterization` reports the resulting text/vector/semantic losses. Actual Word pagination and complete visual fidelity remain qualification obligations.

These losses are strict-mode failures. Permissive mode records them before publication. Complete editable reconstruction and additional fixed-layout profiles remain mandatory; these source profiles do not redefine the requirement's scope. Upstream diagnostics retain bounded machine codes and occurrence counts with neutral messages, and strict/permissive behavior is enforced by the SDK context.

## RTF and package source profiles

The original RTF profile supports version-one grouped ANSI/Windows1252 text, explicit Unicode escapes/fallback, declared fonts, size/bold/italic, paragraphs/line breaks/tabs, explicit page geometry, rectangular table grids and bounded PNG picture destinations. Unknown controls and unsupported metadata/style/color/list destinations are diagnostic omissions. Malformed groups/numbers/Unicode fallbacks, unsupported codepages, external fields, embedded objects/file destinations and binary payload controls reject. General RTF stories, fields, colors/lists, complex/nested/merged tables, drawing formats and full Unicode/font/fidelity profiles remain mandatory pending scope.

SDK-owned DOCX preflight checks bounded archive metadata/entry counts/expansion, unique relative part names, XML depth and DTD/resolver/processing-instruction denial. Macros, active controls, embedded executable/OLE parts, external resources and traversal outside the private package root reject. Passive absolute HTTP/HTTPS/mailto hyperlinks use the ordinary URI policy; they are not fetched. Explicit PNG/JPEG media use existing bounded metadata/pixel validation. No package part is extracted to a filesystem.

Each opaque conversion phase has explicit input/output, character, archive, nesting, page and expansion ceilings. The SDK charges its phase envelope before calling the dependency; captured IO, XML/media preflight, font emission, SDK extraction/rendering, original serialization and final writing share the root context and cancellation token. No nested public SDK operation resets the budget. Dependency-internal heap/callback behavior, minimum runtimes and complete hostile-input/endurance/concurrency qualification remain mandatory.

## Validation and applicability

Core and adapter contracts target net472, netstandard2.0, net8.0 and net10.0. Actual supported provider/runtime/format behavior needs independent qualification on every required lane. Source API/runtime checks cover the complete SDK assembly inventory and cross-framework comparisons; portable consumers verify the actual loaded asset. C# and VB `ExportWord` consumers execute through the public API.

Original source observations include real DOC/DOCX/RTF round trips, RTF Unicode/style/table fixtures and synthetic fixed-page rendering. Independent readers inspect Word97 piece tables, WordprocessingML text, RTF text, emitted PNG pixels and the resulting PDF Unicode. Open XML validator 3.5.1 checks Office2007 schema output. Synthetic pixels are not a PDF-rendering or Word-pagination fidelity claim.

See [source evidence](evidence/FP-053-source.json), [standards observations](evidence/FP-053-standards.json), [independent observations](evidence/FP-053-independent.json) and [production gates](production-quality.md). Exact final SDK package consumers remain blocked until full qualification.
