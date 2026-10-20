# Graphics, image, font and artifact extraction

FP-028 has an original source profile. Full profiles and production qualification remain mandatory; NuGet packing/publication remain blocked.

`PdfResourceExtraction.ReadPage` returns an immutable snapshot-backed inventory. `Resources` identifies shared resolved definitions; `Declarations` records effective page/Form bindings, including unused resources; `Occurrences` records actual invocation/selection operators. Byte-equal independent definitions are not merged. Source locations identify page, container, operator index, Form identity path and parent call indices, distinguishing repeated calls of one shared Form.

Transforms map into native page user coordinates, including nested Form matrices. Literal paths preserve each construction operator's own CTM, retaining paths constructed across transform changes. End operators and requested clip/winding flags remain explicit. Declared image dimensions do not consume decoded-pixel budgets. Stamp inventory visits only the selected page and path-only table traversal omits stamp inventory. This inventory does not infer rendered ink, effective clipping, color, conditional visibility or text glyph outlines. All conditional content is inventoried. Explicit Artifact scopes retain properties and path/use associations, including invoked Forms. Owned stamps use the separately validated private ownership contract; other decorations are not guessed.

```csharp
var page = PdfResourceExtraction.ReadPage(snapshot, 0);
foreach (var resource in page.Resources)
    if (resource.Kind == PdfExtractedResourceKind.Image && resource.HasStream)
        resource.WriteFile(selectedPath, decoded: false);
```

Export defaults to exact encoded PDF stream bytes. They are not necessarily an original image file/container or reconstructed PNG. Explicit decoded export completes privately before writing a borrowed stream or atomically replacing a file. The captured snapshot retains referenced filter declarations after the document is disposed. Font dictionaries link embedded font programs; Type3 glyph procedures remain explicitly distinguished from standalone font files. Explicit `ReadFontProgram` uses a caller-selected provider and reports actual format, identity and embedding restrictions, with its operation report. Enumeration never discovers machine fonts, substitutes missing programs or infers a license from missing bytes. Extended graphics-state font selection is inventoried even without a named Font binding.

Unknown resource roles retain their raw definitions and information diagnostics. Unknown content operators reject by default; explicit preservation retains them with diagnostics and no invented semantics. Invoked Form cycles, absent bindings, malformed dimensions and unsupported direct invoked Form identities reject. Parsing, decoding, font inspection and export each use one mandatory operation context; allocations/pixels/objects/depth/output and cancellation remain bounded, and borrowed streams remain open.

The current profile expands invoked page/Form programs. Pattern, soft-mask and glyph programs remain available as declarations/raw definitions but are not expanded into page paint occurrences. Complete program/resource/visibility/color/clip profiles, all font/image types, complete stamp classification and all production corpora remain mandatory. Source tests cover shared/unused definitions, mixed CTMs, artifacts, exact bytes, font restrictions, graphics-state font selection, decode failures, atomic output, cycles and budgets/cancellation, and execute both C#/VB consumers. Independent pypdf, MuPDF and FontTools observations cross-check an original hand-authored fixture's bytes, operator transforms, paths, text, display boxes and font restrictions. Small source observations do not qualify final packages or production behavior.

See [source evidence](evidence/FP-028-source.json), [standards observations](evidence/FP-028-standards.json), [independent observations](evidence/FP-028-independent.json) and [production quality](production-quality.md).
