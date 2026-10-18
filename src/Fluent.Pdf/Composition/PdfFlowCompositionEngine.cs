namespace Fluent.Pdf;
internal sealed partial class PdfFlowEngine
{
    private readonly List<(int Page, PdfFlowColumnPlacement Column)> _columns = new();
    private readonly List<(int Page, int Block, double Y, PdfFlowPage Source)> _components = new();
    private IReadOnlyList<PdfFlowBlock> ExpandComponents(IReadOnlyList<PdfFlowBlock> source)
    {
        var result = new List<PdfFlowBlock>();
        void Add(IReadOnlyList<PdfFlowBlock> blocks)
        {
            using var depth = _context.EnterScope(PdfResourceKind.Depth);
            foreach (var block in blocks)
            {
                _context.Checkpoint();
                _context.Consume(PdfResourceKind.Objects, 1);
                _context.Consume(PdfResourceKind.ExpandedBytes, 32);
                if (block is PdfFlowComponent component && !component.KeepTogether)
                    Add(component.Content);
                else
                    result.Add(block);
            }
        }

        Add(source);
        return result;
    }

    private void PlaceColumns(int block, PdfFlowColumns columns)
    {
        Clear(PdfFlowClear.Both);
        double height = columns.Height ?? _options.Frame.Height;
        double width = (_options.Frame.Width - columns.Gap * (columns.Count - 1)) / columns.Count;
        if (height > _options.Frame.Height || width <= 0)
            throw Error("Composition.ColumnFrame", "The explicit column region does not fit a positive page frame.");
        if (_y + height > BodyHeight(_page))
            NewPage();
        if (height > BodyHeight(_page))
        {
            NewPage();
        }

        var options = new PdfFlowOptions(new(new(new(0, 0, width, height))), PdfMargins.Uniform(0), _options.Overflow, Math.Min(1000000, checked(_options.MaximumPages * columns.Count)), _options.TabInterval, _options.TabStops.ToArray());
        var engine = new PdfFlowEngine(options, _shaper, _context);
        var flow = engine.Layout(columns.Content);
        _finish.Add(report =>
        {
            engine.Complete(report);
            flow.OperationReport = report;
        });
        double top = _y, used = 0;
        for (int i = 0; i < flow.Pages.Count; i++)
        {
            _context.Checkpoint();
            _context.Consume(PdfResourceKind.ExpandedBytes, 160);
            int physical = i % columns.Count;
            if (i > 0 && physical == 0)
            {
                NewPage();
                top = 0;
                used = 0;
            }

            int slot = columns.Order == PdfFlowColumnOrder.LeftToRight ? physical : columns.Count - 1 - physical;
            var bounds = new PdfRect(slot * (width + columns.Gap), top, width, height);
            _columns.Add((_page, new(block, i, slot, bounds, flow.Pages[i])));
            used = Math.Max(used, flow.Pages[i].ContentHeight);
            _y = top + height;
        }
    }

    private void PlaceKeptComponent(int block, PdfFlowComponent component)
    {
        var none = PdfTableBorder.None;
        var style = new PdfTableCellStyle(PdfMargins.Uniform(0), left: none, top: none, right: none, bottom: none);
        var table = PdfTable.CreateCore(new[] { PdfTableColumn.Fixed(_options.Frame.Width) }, new[] { new PdfTableRow(new[] { new PdfTableCell(component.Content, style: style) }, allowSplit: false) }, 0, false, 1, _context);
        PlaceTable(block, table);
    }

    private void Section(PdfFlowSectionStart start)
    {
        if (start == PdfFlowSectionStart.Continuous)
            return;
        if (HasContent(_page))
            NewPage();
        if (start == PdfFlowSectionStart.NextOddPage && (_page & 1) != 0 || start == PdfFlowSectionStart.NextEvenPage && (_page & 1) == 0)
            NewPage();
    }

    private double BodyHeight(int page) => _options.Frame.Height - ReservedNotes(page);
}
