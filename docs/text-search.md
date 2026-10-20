# Search, regex and text replacement

FP-026 has an original source profile. Complete profiles and production qualification remain mandatory; NuGet packing/publication remain blocked.

`PdfTextSearch.FindPage` searches source-order semantic Unicode across Tj/TJ runs, concatenated page streams and invoked Forms. ActualText replaces its complete covered source range. The search text has no inferred spaces or paragraph separators; it remains separate from geometric reading-order inference. Literal matching is ordinal; explicit regex matching uses invariant culture and a caller-selected finite 1–250ms timeout per match call, plus mandatory operation checkpoints. Invalid patterns, regex timeouts and match-count overflow have stable diagnostics. Results never silently truncate.

Immutable queries support case selection, a displayed top-left region and exact declared font-name/font-size filters. All covered advance cells must satisfy a filter. Matches expose UTF-16 offsets, exact source characters, advance-cell bounds, ActualText provenance and partial-cluster flags. A substring of a multi-scalar ligature has the whole source cell; no invented per-scalar geometry is returned. Explicit zero-length regex matches have no source characters/geometry. Search retains mapping/geometry diagnostics and never runs document actions or discovers host fonts.

```csharp
var matches = PdfTextSearch.FindPage(snapshot, 0,
    new PdfTextQuery("invoice-[0-9]+", regex: true, maximumMatches: 100));
using var edited = PdfTextSearch.ReplacePage(snapshot, 0,
    new PdfTextQuery("old value"), "new value", explicitFonts, explicitProviders,
    new PdfTextReplacementOptions(12, PdfTextReplacementOverflow.ShrinkToFit));
```

Replacement re-runs selection against the exact captured graph under one shared context. It requires explicit font fallback, shaping and maximum font size. Selected encoded source codes are removed from active text-showing strings; exact TJ displacements retain their source advances, spacing and surrounding characters' positions. Quote operators retain their leading/spacing effects. Replacement text uses embedded explicitly shaped fonts and logical ActualText in isolated black foreground drawing. It fits the union of source advance cells, with explicit rejection or uniform shrink-to-fit. This measures source/target advances and font ascent/descent, not glyph ink or a general typographic reflow assertion. Source colors and text styles are not inferred. Nonoverlapping matches each receive their own captured frame; the source snapshot remains unchanged.

The current replacement profile accepts complete source-code clusters on untagged native-horizontal forward single-line fill text with exact source geometry. Empty replacement can delete across lines while preserving each remaining source advance. Partial ligatures, selected ActualText, tagged documents, invoked Forms, vertical/rotated/reversed text, estimated geometry, unsupported glyph progression and multiline/tab replacement require explicit additional profiles and reject transactionally. Unrelated ActualText properties and source operators remain intact. Complete per-invocation Form cloning, accessibility/MCID association preservation, source color/z-order integration, paragraph/run reflow, all shaping/overflow/font profiles and complete corpus coverage remain mandatory.

Replacement is content editing, not secure redaction: historical/unreachable original streams and definitions remain retained. Callers use ordinary atomic save/borrowed-stream ownership and signature-invalidating edit guards. Source tests exercise cross-stream and cross-run selection/replacement, ligatures/ActualText, preserved unrelated geometry/accessibility, regex denial, match/resource limits, cancellation, overflow/missing glyphs, Form/tagged restrictions and executable C#/VB consumers. Independent readers and raster comparisons cover small original replacement/deletion/shrink fixtures. All actual mandatory RID/minimum/current/fallback runtime lanes, exact final packages and full standards/fidelity/security/fuzz/concurrency/endurance/performance gates remain required.

See [source evidence](evidence/FP-026-source.json), [standards observations](evidence/FP-026-standards.json), [independent observations](evidence/FP-026-independent.json) and [production quality](production-quality.md).

Replacement prunes unreachable old content streams in its full rewrite. Shared streams that other pages still reference remain present; text replacement does not substitute for the explicit redaction/sanitization workflow.
