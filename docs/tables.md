# Tables and explicit data binding

`PdfTable` is an immutable validated grid and a `PdfFlowBlock`. It composes with paragraphs through `PdfFlow.Layout`; `table.Layout` is a convenience for a table-only document. It shares the existing shaping, resource, cancellation and canvas context. The managed implementation has the same public contract on all four library targets and adds no platform, GUI, data-access or native runtime dependency.

Columns are fixed or automatic with finite minimum/maximum widths and explicit growth weights. Automatic sizing accounts for conservative text minima, preferred widths and merged spans, then distributes remaining width. Fixed columns preserve their requested widths and use the configured flow overflow policy. Cell content can contain styled paragraphs, vector floats and nested immutable tables. Padding, separate inside edge borders, background colors and top/center/bottom alignment use physical points. Borders are separate cell edges; collapsed-border conflict resolution is not claimed.

```csharp
var text = new PdfFlowTextStyle(new PdfFontFallback(font), 12);
var table = PdfTable.Create(
    new[] { PdfTableColumn.Auto(weight: 2), PdfTableColumn.Auto() },
    new[] {
        new PdfTableRow(new[] {
            PdfTableCell.Text("Item", text),
            PdfTableCell.Text("Amount", text) }),
        new PdfTableRow(new[] {
            PdfTableCell.Text("Office supplies", text),
            PdfTableCell.Text("125.00", text) })
    }, headerRows: 1);
var result = table.Layout(options, providers);
using var document = result.CreateDocument();
document.SaveFile("table.pdf");
```

Grid-origin cells have positive row/column spans. Cells fill the next free slot; every slot must be occupied by a cell or an earlier span. Holes, overlaps, out-of-grid spans and merges crossing the repeated-header/body boundary fail before layout. Spanning height constraints preserve the complete content/padding envelope. Header rows repeat as a complete set. Unsplittable rows must fit an empty body frame; permitted row splits choose boundaries that avoid text lines, floats and keep constraints. Impossible frames fail with `Table.*` diagnostics and return no partial result.

Merged cells retain their original grid identity across continuation fragments. Nested tables reserve ordered common bands for active nested headers on each continuation page. This keeps header stacks and neighboring cells aligned while preserving body source coordinates. Repeated headers are intentional additional text instances; already painted body lines are copied into one fragment. Painting copies individual placements with their complete captured clipping/resource scopes, so an invisible whole-cell text program is not repeated on every page. `PdfTableFragment`, `PdfTableCellFragment` and `PdfTableHeaderRepeat` expose the resulting physical rectangles and provenance. `PdfFlowPage.ContentHeight` records the occupied extent below its content origin.

`PdfTableData.Bind<T>` accepts an explicit scalar selector and immutable style/width for every column, an explicit `CultureInfo`, null text and optional format/alignment. It never reflects over a record, enumerates a selected collection or calls arbitrary object `ToString`. Supported values are strings/chars, booleans, integer/floating/decimal numbers, dates, offsets, durations and GUIDs. Null records use each column's null text without invoking its selector. Standard numeric precision is bounded at 99 before framework formatting can allocate; culture symbols, patterns and runtime calendar profiles are bounded. Unsupported values and profiles fail explicitly.

The binder reads the source once, disposes its enumerator on success/failure/cancellation and snapshots at most the declared finite row count, with one lookahead to detect excess. Direct source/record cycles are rejected; collection traversal is not implicit. Immutable table graphs prevent ordinary construction of recursive table cycles; nested layout also uses depth/operation limits. A selector or enumerator is trusted synchronous caller code and must return to cooperative boundaries. Configuration and cumulative budgets bound row objects, text, shaping, font subsets, nested content, header bands and output. Binding, layout and save are separate operation boundaries; none automatically contacts a data source or network.

Cell content uses bounded internal viewports and carries safe intervals into parent pagination. Explicit page breaks and page-break-before inside a cell require a separate policy and fail. Language-specific breaking, exact glyph ink measurements and complex shaping retain the explicit text/flow provider profiles. Conservative font envelopes can reserve more room than advance-only typography. A native shaper's cumulative worker envelope remains charged for each request.

Source tests cover normal/merged/nested/repeated grids, row splitting, data culture/nulls, finite lazy sources, cancellation, budgets and C#/VB PDF consumers. Independent invoice and nested-table checks retain exact source commits and limitations. Complete styled/merged/nested goldens, labeled business data, actual minimum runtimes, final packages and all mandatory production gates remain pending. NuGet packing and publication stay blocked.
