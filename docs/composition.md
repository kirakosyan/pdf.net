# Lists, columns, notes and reusable content

The managed composition layer adds immutable `PdfFlowList`, `PdfFlowColumns`, `PdfFlowComponent`, section boundaries and note definitions/references to the existing flow pipeline. It uses one operation context for expansion, shaping, pagination, nested content and canvas output. Public contracts are identical on all four library targets; font discovery, rendering and native downloads remain explicit optional services.

Lists capture a finite source and fix counters before pagination. Decimal, alphabetic and Roman markers are invariant; item values restart the local counter, and nested lists own independent restarts. Roman notation explicitly supports 1–3999. Counter/item limits fail with stable diagnostics. Bullets use explicit text and the supplied marker font; missing glyphs fail. Marker width can be fixed or measured. Borderless grid composition keeps marker and body order stable across page/row continuations without repeating a marker on every body fragment.

```csharp
var list = PdfFlowList.Create(new[] {
    new PdfFlowListItem(new[] { new PdfFlowParagraph("First item", style) }),
    new PdfFlowListItem(new[] { new PdfFlowParagraph("Restarted item", style) },
        counterValue: 10)
}, style);
var columns = new PdfFlowColumns(new PdfFlowBlock[] { list },
    count: 2, gap: 18, height: 300);
var result = PdfFlow.Layout(new[] { columns }, options, providers);
using var document = result.CreateDocument();
document.SaveFile("composition.pdf");
```

A column region has an explicit fill height, count, gap and physical left-to-right/right-to-left ordering. The default height is a full page frame, moving to a fresh page when needed. The region reserves its height, and logical content fills successive columns before continuing on another physical page. Column order in the PDF content stream remains logical regardless of physical direction. Geometry exposes logical/physical indices and the immutable source column page. Automatic column balancing is not an advertised profile.

Creating a document containing top-level columns emits real basic structure: ordered `Div` elements, page MCIDs, `/StructParents` and a matching parent number tree. Generic content segments retain their order around column segments. This is a coarse structure contract; full headings/list/table semantics, nested structure editing, accessibility review and PDF/UA conformance belong to the mandatory accessibility requirements and remain unqualified. Opening/saving unchanged source structure preserves it; edits that need source structure remapping fail explicitly. Ordinary inspection executes no actions.

Reusable components capture immutable blocks. Ordinary components expand in logical order; kept components use a bounded unsplittable borderless cell. Expanded block indices describe the resulting logical sequence. Section breaks support continuous, next-page, next-odd and next-even starts without changing page geometry. Intentional blank pages satisfy parity. Independent jobs can share immutable components, fonts and definitions.

A `PdfFlowNote` defines a bounded stable ID, visible label, body and marker style. `PdfFlowNoteReference` appends a raised label to its reference paragraph; `PdfFlowTextStyle.BaselineShift` is an explicit physical baseline raise. Footnotes reserve space on the reference page and replan the reference when that reservation would overlap earlier body content. Repeated references to the same definition share a note on the same page. A definition can repeat on another reference page. Distinct definitions sharing an ID fail. Endnotes append once in encounter order with their source associations.

The explicit footnote profile keeps a complete note with its reference on one page; an overlarge note fails. Endnote bodies can span pages. Notes cannot recursively contain note references. A reference paragraph with keep-with-next requires a further following-block profile and fails explicitly. Notes within a column are local to that column and inspectable through the column's source page. Full mixed-script/legal/book-length note and structure corpora remain required before qualification.

`PdfFlowResult.CreateDocument` is now an explicit bounded boundary with cancellation; document page limits apply, and failures dispose the partial editing session. Save remains a separate operation preserving caller streams and atomic paths. Source tests exercise counters, restarts, nested lists, parity, column/tag associations, note reservations, repeated/endnote references, budgets, cancellation, independent jobs and C#/VB consumers. Independent direct PDF models check column order/geometry, parent-tree relationships and note placement with an explicit renderer profile. Source checks do not qualify full accessibility, minimum runtimes, final packages or the complete production gates. NuGet candidate creation and publication remain blocked.
