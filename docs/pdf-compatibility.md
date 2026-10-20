# PDF versions and extension preservation

FP-098 has a managed source profile on all four library targets. `PdfCompatibility.Inspect` returns the physical header, effective header/catalog version, raw developer declarations, catalog associated files and structure namespaces. The captured snapshot retains indirect identities after the caller disposes its document. Inspection is passive and does not fetch documentation URLs, execute actions, interpret vendor payloads or establish PDF conformance.

## Read/write/preserve matrix

| Structure | Read | Write | Preservation and output negotiation |
| --- | --- | --- | --- |
| PDF 1.2–1.7, 2.0 headers/catalog versions | Normal strict syntax/page reader; separate explicit recovery | Full rewrite and incremental revisions | Automatic maximum declared/known feature floor; exact lower targets reject. Incremental writes retain original bytes/header and use catalog Version when needed |
| Classic, stream/hybrid cross references; object streams | Existing syntax reader | Classic or stream index; full writer expands active compressed objects | Active identity/generation retained; history preserved only in incremental mode |
| PDFDocEncoding, UTF-16BE text strings | Strict typed decoding, undefined legacy bytes and malformed Unicode reject | Explicit `PdfTextString.Create`; UTF-16BE default | Exact original bytes retained by typed read; no normalization. UTF-8 requires its BOM and PDF 2.0 |
| Info text encoded in UTF-8 | Metadata reader decodes declared text | `SetInformationText` sets catalog floor 2.0 | Untouched strings preserve bytes; exact lower output version rejects. Trapped remains a name through `SetInformation` |
| Raw byte/font-code strings | Uninterpreted immutable bytes | Raw value writer | BOM-shaped binary strings are never globally recoded or treated as evidence of text/version requirements |
| Developer Extensions, reserved Type | Raw inventory plus explicit source diagnostics | Legacy prefix/base/level or new URL/revision declaration | `/Type /Extensions` is separate from vendor prefixes. Unknown direct keys and bytes preserved on merge; unknown-prefix reject policy is explicit; existing level/base cannot downgrade |
| PDF 2.0 extension URL/revision | Raw immutable values, no network request | Explicit absolute ASCII URL and optional Unicode revision | Typed URL/revision authoring requires 2.0. URL uses direct ASCII bytes; ordinary string encryption applies. Legacy declarations missing required URL are retained and reported by inventory, without a conformance claim |
| Opaque unknown catalog/page/resource dictionaries and streams | Exact raw immutable values; unsupported decoding stays explicit | Full/incremental low-level write and identity-preserving edits | Unknown filter payloads are copied without decoding. Typed operations may reject structures whose semantics cannot be preserved |
| Associated files/AFRelationship | Passive attachment and raw catalog inventories | Explicit attachment authoring | Existing PDF 2.0 feature floor and relationships retained; encrypted payloads require explicit security operation |
| Structure namespaces and unknown namespace keys | Raw structure-root inventory | Low-level values preserve source graph | Metadata/identity edits retain the graph. Unsupported organization/remapping rejects `Organization.StructureProfile` before output; semantic accessibility support belongs to FP-067 |
| Password/certificate encryption | Explicit authentication, separate from normal open | Explicit full protection rewrites | See security profiles; no implicit Unicode password preparation, key-store discovery or network trust operation. Extension strings receive the selected ordinary string protection |

The matrix describes observable source profiles, not universal PDF 2.0 implementation or qualification. A source extension's declared requirements are not evidence that its semantics are implemented. PDF 2.0 extensions require direct data; metadata streams and associated-file streams cannot live inside the Extensions dictionary. Preserve general opaque stream references in their proper source graph locations. The compatibility inventory reports raw metadata rather than silently repairing a malformed vendor declaration. Complete normative directness/schema validation and extension-specific semantics remain mandatory qualification work.

For low-level `PdfTextString.Value` placement, carry its `MinimumVersion` into `PdfWriteOptions.minimumVersion`; the raw value alone deliberately carries no text interpretation. Typed Info authoring applies the floor automatically. A text string prepared in one operation is immutable; the editing operation applies its own shared resource policy. Metadata synchronization or an explicit caller replacement may choose a different text encoding.

```csharp
var title = PdfTextString.Create("Original title 一😀", PdfTextStringEncoding.Utf8);
using var changed = PdfMetadata.Edit(source, editor => editor.SetInformationText("Title", title));
changed.Save(output); // Version floor 2.0, borrowed output.
var inventory = PdfCompatibility.Inspect(changed.CreateSnapshot());
```

## Evidence and remaining qualification

Original source fixtures cover every supported version, all three encodings, malformed Unicode, reserved extension Type, URL/revision negotiation, exact opaque binary/unknown-filter payloads across metadata/geometry/full/incremental writes, namespace and associated-file retention, unsafe-remapping rejection, security integration, resource bounds and cancellation. Independent readers examine retained exact outputs and raw payloads. MuPDF 1.26.6 and Poppler 26.05.0 return the exact expected PDF 2.0 UTF-8 title. On those same bytes, pypdf 6.10.0 and PDFBox 3.0.8 return incorrect title Unicode; their negative observations are retained alongside successful raw payload and ordinary-text checks. This is an interoperability limitation, and the API keeps UTF-16BE as its default rather than silently recoding an explicitly requested UTF-8 string. C# and VB consumers compile on every library target; no package has been packed or qualified.

Complete PDF 1.x/2.0 and extension corpus, normative schema/directness audit, extension-specific semantics, multilingual language-escape interpretation, source text-placement coverage, fuzz/concurrency/endurance, actual minimum runtimes and final-package consumers remain mandatory. Missing work keeps FP-098 planned/source-validation and packing/publication blocked.
