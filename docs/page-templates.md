# Final page templates and linked contents

`PdfTemplateLayout.Layout` reserves explicit header/footer heights before body pagination. Its callbacks run once for each final page and receive its physical index, total count, formatted label and first/odd/even flags. A callback selects an immutable flow, vector/image drawing or explicit HTML decoration. HTML fragments require `IPdfTemplateHtmlProvider`; core has no browser engine and performs no resource download. Provider calls receive the same operation context.

`PdfFlowHeading` supplies a stable unique destination ID. `PdfFlowContents` selects heading levels; `PdfFlowIndex` sorts explicit terms using caller-supplied culture and emits a linked locator for each destination. There is no implicit text search or inferred index. Titles, locators, headings and body use the existing explicit font/shaping pipeline. Components, lists, columns and nested table cells retain final heading and locator geometry.

The planner repeatedly lays out contents/index rows against previous destinations until page count and heading positions stabilize. All passes share cumulative budgets and cancellation. Repeated nonadjacent states and the finite pass limit fail with stable diagnostics. Callbacks receive only the final state, so changing totals cannot feed a callback-driven body pagination loop. Fixed decoration regions reject overflow. Kept locator rows do not divide page numbers from their titles.

```csharp
var template = new PdfPageTemplate(footerHeight: 18,
    footer: c => PdfPageDecoration.Flow(new[] {
        new PdfFlowParagraph($"{c.PageLabel} of {c.TotalPages}", style)
    }));
var result = PdfTemplateLayout.Layout(new PdfFlowBlock[] {
    new PdfFlowContents(style),
    new PdfFlowHeading("intro", "Introduction", style),
    new PdfFlowParagraph("Body text", style)
}, options, providers, template);
using var document = result.CreateDocument();
document.SaveFile("report.pdf");
```

`PdfPageNumbering` supports ordered zero-based physical-page restarts, literal prefixes and the standard decimal, Roman, repeated-letter or prefix-only styles. Roman labels support 1–3999; repeated-letter labels have a finite 128-character profile. Labels are written as a real `/PageLabels` number tree. Each locator becomes a `/Link` annotation with a final page-object `/XYZ` destination. Target page identities survive subsequent document reordering; removing a target requires explicit navigation repair and fails before save.

Creating the document appends decorations inside `/Artifact` marked content without replacing body structure associations. This retains the basic column structure profile; full semantic tagging and PDF/UA qualification remain mandatory separate work. Header/footer source content is reusable artwork rather than a body heading/index source. `CreateDocument` and save are separate bounded operations, with caller stream ownership retained and atomic file saves.

C# and VB executable examples generate final totals and linked contents. Tests exercise changing document length, convergence limits, restart formatting, real destination objects, artifact decoration, nested headings/locators, missing/duplicate targets, cancellation, provider absence and decoration overflow. Full book-length convergence, independent fidelity/conformance, minimum-runtime and exact-package qualification remain required. NuGet creation and publishing stay blocked.

Convergence reuses measurements of the same immutable body paragraph within one template operation, under the exact same frame/options, shaper and context. Cache entries are charged before retention; cache hits checkpoint cancellation/deadlines. Nested frames and final callbacks use separate measurements. No cache crosses operations or persists globally. This removes repeated shaping during pagination without extending a deadline or changing output geometry.

Trial passes construct body geometry and destination metadata without painting the top-level body. After convergence, the retained final plan is painted once, before final decoration callbacks. Nested/table measurement keeps its required local content; failed convergence never embeds the top-level body fonts. Planning and final painting share the same context and unchanged deadline, and output remains subject to the same bounds and diagnostics.
