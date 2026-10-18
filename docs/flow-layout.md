# Flow layout and pagination

`PdfFlow.Layout` supplies original managed paragraph layout on all four library targets. It takes immutable blocks, page geometry/options and an explicitly registered `IPdfTextShaper`. It performs analysis, shaping, constraint planning, font embedding and page canvas composition under one operation context. It returns a detached result only after the whole operation succeeds. No system font discovery, native download or rendering occurs.

```csharp
var style = new PdfFlowTextStyle(new PdfFontFallback(font), 12);
var blocks = new PdfFlowBlock[] {
    new PdfFlowParagraph("Invoice", style,
        new PdfFlowParagraphStyle(keepWithNext: true)),
    new PdfFlowParagraph("Payment is due within thirty days.", style)
};
var geometry = new PdfPageGeometry(
    new PdfPageBoxes(new PdfRect(0, 0, 595, 842)));
var result = PdfFlow.Layout(blocks, new PdfFlowOptions(geometry),
    new PdfProviderRegistry(shaper));
using var document = result.CreateDocument();
document.SaveFile("invoice.pdf");
```

Runs select explicit fonts, sizes, colors, language and shaping features. Style boundaries cannot split an extended grapheme. Styles in a paragraph share its base-direction policy. Flow is horizontal; vertical text remains available through the text/canvas APIs. Paragraph options set start/end/center/justified alignment, minimum line height, physical indents, before/after spacing, widow/orphan counts, keep-together, keep-with-next chains and page-break-before. Page and block limits fail without returning a partial layout. Impossible constraints produce stable `Layout.*` diagnostics.

The line breaker uses explicit hard separators, ordinary spaces/tabs, hyphens, zero-width spaces and a documented CJK boundary profile with closing/opening punctuation and glue protection. It preserves all logical UTF-16 units. It does not claim dictionary segmentation, automatic hyphenation or complete UAX #14 language tailoring. An overlong word either fails, is reshaped at an extended grapheme boundary, or is explicitly clipped. Clipping reports `Layout.ClippedText` as omitted content: strict policies reject it, permissive policies retain the warning. A single overlarge grapheme still fails under grapheme wrapping. Fixed tabs use explicit increasing physical stops and a repeating interval. Justification with tabs requires a further alignment profile and fails explicitly.

Paragraph bidi resolution remains intact across soft wraps, including explicit embeddings and isolates. Each selected line applies UAX #9 L1/L2 before contextual shaping. Font fallback covers complete graphemes, and line ends are reshaped rather than cutting an already shaped ligature. Each painted line has one logical UTF-16BE `ActualText` span and embedded glyph mappings. Copy/search behavior still requires the complete multilingual reader corpus; source logical-text checks do not qualify extraction behavior in every reader.

Line height and horizontal occupied intervals conservatively include the selected fonts' bounding boxes and positioned offsets. This can leave additional side/vertical space compared with advance-only typography; it protects ink at page boundaries and float exclusions without claiming exact glyph-ink measurement. `Advance` records the natural visible advance before alignment/justification. Geometry is in physical points relative to the page content origin, below the displayed top margin. `Bounds` retains the available line interval. Before/after paragraph spacing does not force a partial line across a page boundary; leading space is discarded when a paragraph moves to a new page.

Floating drawings have explicit source bounds, physical size, side, gap and clear policy. A rectangle excludes intersecting subsequent line intervals; a paragraph or float can clear either/both sides. Overlarge floats fail. Explicit page breaks can create intentional blank pages. Repeated inputs, font identities and provider results produce the same geometry; completed blocks/fonts/results can be shared across independent jobs.

`CreateDocument` returns a caller-owned managed document using already composed canvases. Document page limits apply independently, and save is a separate bounded operation preserving caller stream ownership and atomic path semantics. A native shaping provider charges its complete worker envelope for every itemization request; a caller must configure a cumulative layout budget sufficient for the chosen job. The pinned Unicode resident tables are charged once per operation, while per-line arrays, shaped results, font subsets and canvas resources are charged cumulatively.

C#/VB examples exercise real source PDF output. Source tests cover styled runs, wrapping, constraints, floats, tabs, bidi continuation, cancellation, budgets and independent jobs. Independent reference PDFs check pagination, metrics, alignment, logical text and raster output. Complete multilingual business/constraint corpora, language line-break profiles, actual minimum runtimes, final packages and all mandatory production gates remain pending. NuGet candidate creation and publishing remain blocked.
