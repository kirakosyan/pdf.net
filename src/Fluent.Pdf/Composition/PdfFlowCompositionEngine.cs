namespace Fluent.Pdf;
internal sealed partial class PdfFlowEngine
{
    private readonly Dictionary<int, PdfFlowHeading> _headingDefinitions = new();
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
                else if (block is PdfFlowHeading heading)
                {
                    _headingDefinitions.Add(result.Count, heading);
                    result.Add(heading.Paragraph);
                }
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

    private PdfFlowHeadingPlacement[] HeadingsForPage(int page)
    {
        var result = new List<PdfFlowHeadingPlacement>();
        foreach (var pair in _headingDefinitions)
        {
            var line = _lines.FirstOrDefault(l => l.Line.BlockIndex == pair.Key);
            if (line != null && line.Page == page)
                result.Add(new(pair.Value, page, line.Line.Bounds));
        }

        foreach (var column in _columns.Where(c => c.Page == page))
            foreach (var heading in column.Column.Source.Headings)
                result.Add(new(heading.Heading, page, new(heading.Bounds.X + column.Column.Bounds.X, heading.Bounds.Y + column.Column.Bounds.Y, heading.Bounds.Width, heading.Bounds.Height)));
        foreach (var fragment in _tableFragments.Where(t => t.Page == page).Select(t => t.Fragment))
        {
            foreach (var cell in fragment.Plan.Cells)
            {
                if (cell.Content.Flow == null)
                    continue;
                double stacked = 0;
                for (int i = 0; i < cell.Content.Flow.Pages.Count; i++)
                {
                    var source = cell.Content.Flow.Pages[i];
                    foreach (var heading in source.Headings)
                    {
                        double sheetY = cell.ContentY + stacked + heading.Bounds.Y;
                        if (sheetY < fragment.BodyStart || sheetY >= fragment.BodyEnd)
                            continue;
                        var style = cell.Grid.Cell.Style;
                        result.Add(new(heading.Heading, page, new(fragment.Bounds.X + cell.Bounds.X + style.Left.Width + style.Padding.Left + heading.Bounds.X, fragment.Bounds.Y + fragment.Plan.HeaderHeight + fragment.NestedHeaderBandHeight + sheetY - fragment.BodyStart, heading.Bounds.Width, heading.Bounds.Height)));
                    }

                    stacked += i + 1 < cell.Content.Flow.Pages.Count ? PdfTableMeasure.CellViewport : source.ContentHeight;
                }
            }
        }

        return result.ToArray();
    }

    private PdfTemplateLocator[] LocatorsForPage(int page)
    {
        var result = new List<PdfTemplateLocator>();
        void Add(string id, PdfRect bounds)
        {
            _context.Consume(PdfResourceKind.ExpandedBytes, 96);
            result.Add(new(id, bounds));
        }

        foreach (var column in _columns.Where(c => c.Page == page))
            foreach (var locator in column.Column.Source.TemplateLocators)
                Add(locator.Id, new(locator.Bounds.X + column.Column.Bounds.X, locator.Bounds.Y + column.Column.Bounds.Y, locator.Bounds.Width, locator.Bounds.Height));
        foreach (var fragment in _tableFragments.Where(t => t.Page == page).Select(t => t.Fragment))
        {
            if (fragment.Table.TemplateTargets != null)
                foreach (var cell in fragment.Cells.Where(c => c.Column == 0))
                    Add(fragment.Table.TemplateTargets[cell.Row], new(cell.Bounds.X, cell.Bounds.Y, fragment.Bounds.Width, cell.Bounds.Height));
            foreach (var cell in fragment.Plan.Cells)
            {
                if (cell.Content.Flow == null)
                    continue;
                double stacked = 0;
                for (int i = 0; i < cell.Content.Flow.Pages.Count; i++)
                {
                    var source = cell.Content.Flow.Pages[i];
                    foreach (var locator in source.TemplateLocators)
                    {
                        double sheetY = cell.ContentY + stacked + locator.Bounds.Y;
                        double top = Math.Max(sheetY, fragment.BodyStart), bottom = Math.Min(sheetY + locator.Bounds.Height, fragment.BodyEnd);
                        if (bottom <= top)
                            continue;
                        var style = cell.Grid.Cell.Style;
                        Add(locator.Id, new(fragment.Bounds.X + cell.Bounds.X + style.Left.Width + style.Padding.Left + locator.Bounds.X, fragment.Bounds.Y + fragment.Plan.HeaderHeight + fragment.NestedHeaderBandHeight + top - fragment.BodyStart, locator.Bounds.Width, bottom - top));
                    }

                    stacked += i + 1 < cell.Content.Flow.Pages.Count ? PdfTableMeasure.CellViewport : source.ContentHeight;
                }
            }
        }

        return result.ToArray();
    }

    private double BodyHeight(int page) => _options.Frame.Height - ReservedNotes(page);
}
