# Fonts, embedding and Unicode maps

FP-008 has an original managed source implementation behind the optional, non-packable `Fluent.Pdf.Fonts` provider. The managed core contains provider-neutral font/resource contracts and authored Type 3 glyphs. The optional assembly introduces no runtime native or GUI dependency. Its API targets `net472`, `netstandard2.0`, `net8.0` and `net10.0`; production qualification remains pending.

```csharp
using Fluent.Pdf;
using Fluent.Pdf.Fonts;
using PdfFacade = Fluent.Pdf.Pdf;

var providers = new PdfProviderRegistry(ManagedPdfProvider.Instance, ManagedFontProvider.Instance);
using var fontInput = File.OpenRead("explicitly-provisioned-font.ttf");
var font = PdfFont.Read(fontInput, providers);
var resource = PdfFontResource.CreateText(font, "Example text");
using var document = PdfFacade.Create(providers: providers).AddPage();
document.Pages[0].SetFont("F1", resource);
document.Pages[0].Content = PdfContentProgram.Create(new[]
{
    new PdfContentOperation(PdfOperator.BeginText),
    new PdfContentOperation(PdfOperator.Font, PdfValue.Name("F1"), PdfValue.Integer(24)),
    new PdfContentOperation(PdfOperator.MoveText, PdfValue.Integer(20), PdfValue.Integer(60)),
    new PdfContentOperation(PdfOperator.ShowText, resource.EncodedText),
    new PdfContentOperation(PdfOperator.EndText)
});
document.SaveFile("font-example.pdf");
```

This example maps unshaped scalars. Bidirectional ordering, script shaping, positioning and line layout belong to the text/layout services. A missing glyph fails explicitly; it is never silently replaced by a machine font. `PdfFontFallback` captures caller-loaded fonts in preference order and resolves the first actual glyph. All loading is offline and explicit. No font directory, operating-system search order, browser or download is consulted.

`PdfFont.Read` snapshots borrowed bytes eagerly; `faceIndex` selects an exact SFNT collection face. `PdfFont.Standard` selects one exact base-14 PostScript name through the provider. `GetGlyph` returns the actual glyph identity, advance and optional exact vertical advance; glyph zero explicitly means unavailable. Font objects and fallback lists are immutable and shareable by independent jobs. Documents retain their normal serialized-mutation contract. Each public parse/query/subset/map/resource operation creates a fresh budget; providers receive the mandatory shared context.

The managed provider reads static TrueType/SFNT collections, OpenType CFF1, raw CFF1 and Type 1 PFA/PFB. It validates required table extents, glyph locations/composite references, Unicode cmap representations, CFF INDEX/DICT/FDSelect structures and explicit Type 1 binary boundaries. Original Type 1 encryption and bounded Type 1/Type 2 operand processing never execute PostScript. Variable/CFF2 input requires an explicitly configured instancing provider; unsupported affine metrics, nondeterministic random glyph programs and unsupported width constructs fail with stable diagnostics. These explicit profile limits remain part of full font qualification work and do not remove requirements from the release scope.

TrueType subsetting retains stable glyph identities, includes composite dependencies, removes unused outline data, rebuilds Unicode cmap entries and SFNT checksums, and gives modified primary names a deterministic `FluentSubset` identity. CFF output is CID-keyed, retains glyph identities, rewrites offsets/charsets/private dictionaries and conservatively retains subroutines. Legacy named CFF composite accents are expanded before CID conversion; coordinates needing precision reduction report `Font.CffCoordinatePrecision` and fail under strict processing. Type 1 subsets retain required `seac` components. `OtherSubrs` conservatively retain all glyph charstrings with `Font.ConservativeSubset`; no external PostScript runs. Subsets need not be minimal. Full conversions that change CFF identity also receive new primary names. Copyright/license records remain retained where the source format supplies them.

Declared restricted and bitmap-only embedding fail before font output. A no-subsetting flag requires a full embedding choice. `PdfFontInfo.EmbeddingRights` exposes declared preview/print/edit flags; metadata does not grant a license beyond the caller's rights. Metrics absent from a standalone Type 1/CFF program use its declared design-space bounding extents for conservative vertical/capital bounds. TrueType/OpenType faces use their explicit metric tables. Vertical CID writing requires actual advances and origins: TrueType uses `vmtx` and glyph geometry; OpenType CFF uses `vmtx` and `VORG`. No approximate vertical mode is silently selected.

`PdfFontResource` eagerly captures a font program, exact glyph/Unicode mappings, widths, encoding and ToUnicode data. `Create` accepts actual glyph mappings, including multi-scalar ligatures; `CreateText` maps unshaped scalars. An empty Unicode mapping explicitly carries no logical text. `EncodedText` is the immutable byte-string operand for content operators. Simple font resources have at most 255 distinct mappings; callers split larger simple-font runs into explicit resources. CID resources support 65,535 mappings. TrueType uses Identity encoding plus a CID-to-GID stream. CFF uses Identity encoding when the mapping permits it, otherwise an explicit CMap preserves distinct logical text for a shared glyph. Font bindings merge with inherited page resources and remain detached in snapshots. OpenType embedding negotiates PDF 1.6; Type 3 procedures contribute their operator version floors.

`PdfFont.Type3` accepts caller-authored `PdfType3Glyph` procedures validated in Type 3 context. Width declarations must begin each procedure. Uncolored `d1` glyphs cannot set colors or paint non-mask inline images, and their declared bounds must fit the font. Named resources remain explicit. The writer emits CharProcs, encoding, widths, matrix, resources and exact ToUnicode data. A Type 3 font is a PDF resource, not an external binary font file.

`PdfPage.ReadFonts` returns detached original/replacement bindings without running document actions. `PdfPageFont.ExtractProgram` extracts decoded binary font bytes; standard and Type 3 fonts correctly report that they have no external program. `Load` uses an explicitly selected font provider or returns an authored face. Raw CID programs using Identity encodings acquire their scalar mappings from the source ToUnicode stream. Source Type 3 procedures remain available in `SourceDictionary` rather than pretending to be an external font file.

`PdfUnicodeMap` reads `bfchar`, sequential/array `bfrange`, one-to-four-byte code spaces and explicitly supplied inherited CMaps. It supports supplementary Unicode and multi-scalar mappings, rejects ambiguous prefixes, malformed targets and conflicting local definitions, and charges large ranges before expansion. It inspects a restricted standard CMap envelope without executing it. Missing decoded codes fail in strict mode; permissive mode records every replacement. `PdfPageFont.DecodeText` uses explicit ToUnicode first. Simple-font text without ToUnicode requires the encoding provider and records `Font.UnicodeInferred`; standard, WinAnsi, MacRoman, MacExpert, Symbol, Dingbats and explicit glyph-name differences are supported. Unknown collection CMaps and custom built-in encodings require explicit caller/provider data. There is no implicit external CMap fetch.

Data under `Fluent.Pdf.Fonts/Data` consists of factual standard-font metrics and normative encoding/glyph identifiers. Notices and pinned data hashes are in `third-party/fonts`. No font outlines are bundled by default. Tests include original geometric fonts encoded independently and a small renamed SIL OFL CJK fixture with provenance and license. Development checks cover font families, mappings, dependency-complete subsets, Unicode, collection selection, Type 3, vertical CJK metrics, ownership, restrictions, output recovery, concurrency and deterministic mutations. Independent tools inspect PDF text/raster output and used glyph outlines. Complete actual minimum-runtime, final-package, licensed-corpus, standards/fidelity, security, long fuzz/endurance and font/profile qualification remain required. NuGet candidate creation and publishing stay blocked.
