namespace Fluent.Pdf;
internal sealed class PdfTableMeasure
{
    internal const double CellViewport = 14400;
    private readonly PdfFlowOptions _options;
    private readonly IPdfTextShaper _shaper;
    private readonly PdfOperationContext _context;
    private readonly List<Action<PdfOperationReport>> _finish;
    private readonly Dictionary<PdfFlowBlock, (double Minimum, double Preferred)> _natural = new();
    private readonly HashSet<PdfTable> _path = new();
    internal PdfTableMeasure(PdfFlowOptions options, IPdfTextShaper shaper, PdfOperationContext context, List<Action<PdfOperationReport>> finish)
    {
        _options = options;
        _shaper = shaper;
        _context = context;
        _finish = finish;
    }

    internal PdfTablePlan Build(PdfTable table, double available)
    {
        using var depth = _context.EnterScope(PdfResourceKind.Depth);
        if (!_path.Add(table))
            throw Error("Table.ContentCycle", "Nested table content contains a cycle.");
        try
        {
            var limits = Constraints(table);
            var widths = limits.Minimum;
            double minimum = widths.Sum();
            if (minimum > available + 1e-8)
                throw Error("Table.WidthOverflow", "The table's minimum widths exceed the available page frame.");
            Grow(widths, limits.Preferred, table.Columns, available - minimum);
            Grow(widths, table.Columns.Select(c => c.Maximum).ToArray(), table.Columns, available - widths.Sum());
            _context.Consume(PdfResourceKind.ExpandedBytes, table.Columns.Count * 32L + table.Rows.Count * 32L + table.Grid.Length * 96L + 512);
            var x = Prefix(widths);
            var heights = table.Rows.Select(r => r.MinimumHeight).ToArray();
            var contents = new PdfTableCellContent[table.Grid.Length];
            for (int i = 0; i < table.Grid.Length; i++)
            {
                _context.Checkpoint();
                var grid = table.Grid[i];
                var style = grid.Cell.Style;
                double width = x[grid.Column + grid.Cell.ColumnSpan] - x[grid.Column] - InsetX(style);
                if (width < 0 || width == 0 && grid.Cell.Content.Count > 0)
                    throw Error("Table.CellWidth", "Cell padding and borders leave no usable content width.");
                contents[i] = Content(grid.Cell.Content, width);
                if (grid.Cell.RowSpan == 1)
                    heights[grid.Row] = Math.Max(heights[grid.Row], contents[i].Height + InsetY(style));
            }

            // Increasing a row can never invalidate an earlier minimum-span inequality.
            for (int i = 0; i < table.Grid.Length; i++)
            {
                var g = table.Grid[i];
                if (g.Cell.RowSpan == 1)
                    continue;
                double actual = 0;
                for (int row = g.Row; row < g.Row + g.Cell.RowSpan; row++)
                    actual += heights[row];
                double deficit = contents[i].Height + InsetY(g.Cell.Style) - actual;
                if (deficit > 0)
                    heights[g.Row + g.Cell.RowSpan - 1] += deficit;
            }

            var y = Prefix(heights);
            var prepared = new PdfTablePreparedCell[table.Grid.Length];
            var unsafeIntervals = new List<PdfTableInterval>();
            for (int i = 0; i < prepared.Length; i++)
            {
                _context.Checkpoint();
                var g = table.Grid[i];
                var style = g.Cell.Style;
                var box = new PdfRect(x[g.Column], y[g.Row], x[g.Column + g.Cell.ColumnSpan] - x[g.Column], y[g.Row + g.Cell.RowSpan] - y[g.Row]);
                if (style.Left.Width + style.Right.Width > box.Width || style.Top.Width + style.Bottom.Width > box.Height)
                    throw Error("Table.BorderWidth", "Cell borders exceed the completed merged rectangle.");
                double spare = Math.Max(0, box.Height - InsetY(style) - contents[i].Height);
                double shift = style.VerticalAlignment == PdfTableVerticalAlignment.Top ? 0 : style.VerticalAlignment == PdfTableVerticalAlignment.Center ? spare / 2 : spare;
                double contentY = box.Y + style.Top.Width + style.Padding.Top + shift;
                prepared[i] = new(g, contents[i], box, contentY);
                _context.Consume(PdfResourceKind.ExpandedBytes, contents[i].UnsafeIntervals.Length * 24L + 32);
                foreach (var interval in contents[i].UnsafeIntervals)
                    unsafeIntervals.Add(new(contentY + interval.Start, contentY + interval.End));
                if (contents[i].UnsafeIntervals.Length > 0)
                {
                    var first = contents[i].UnsafeIntervals.OrderBy(v => v.Start).ThenByDescending(v => v.End).First();
                    var last = contents[i].UnsafeIntervals.OrderByDescending(v => v.End).ThenBy(v => v.Start).First();
                    if (contentY - box.Y <= style.Top.Width + style.Padding.Top + 1e-8)
                        unsafeIntervals.Add(new(box.Y, contentY + first.End));
                    if (box.Top - contentY - contents[i].Height <= style.Bottom.Width + style.Padding.Bottom + 1e-8)
                        unsafeIntervals.Add(new(contentY + last.Start, box.Top));
                }
            }

            for (int row = table.HeaderRows; row < heights.Length; row++)
                if (!table.AllowRowSplit || !table.Rows[row].AllowSplit)
                    unsafeIntervals.Add(new(y[row], y[row + 1]));
            return new(table, x, y, prepared, unsafeIntervals.OrderBy(i => i.Start).ToArray());
        }
        finally
        {
            _path.Remove(table);
        }
    }

    private (double[] Minimum, double[] Preferred) Constraints(PdfTable table)
    {
        _context.Consume(PdfResourceKind.ExpandedBytes, table.Columns.Count * 64L + 128);
        var minimum = table.Columns.Select(c => c.Minimum).ToArray();
        var preferred = (double[])minimum.Clone();
        foreach (var grid in table.Grid)
        {
            _context.Checkpoint();
            double min = 0, pref = 0;
            foreach (var block in grid.Cell.Content)
            {
                var value = Natural(block);
                min = Math.Max(min, value.Minimum);
                pref = Math.Max(pref, value.Preferred);
            }

            min += InsetX(grid.Cell.Style);
            pref += InsetX(grid.Cell.Style);
            Satisfy(minimum, grid.Column, grid.Cell.ColumnSpan, min, table.Columns, true);
            Satisfy(preferred, grid.Column, grid.Cell.ColumnSpan, pref, table.Columns, false);
        }

        for (int i = 0; i < preferred.Length; i++)
            preferred[i] = Math.Max(preferred[i], minimum[i]);
        return (minimum, preferred);
    }

    private (double Minimum, double Preferred) Natural(PdfFlowBlock block)
    {
        if (_natural.TryGetValue(block, out var cached))
            return cached;
        using var depth = _context.EnterScope(PdfResourceKind.Depth);
        _context.Consume(PdfResourceKind.Objects, 1);
        _context.Consume(PdfResourceKind.ExpandedBytes, 128);
        (double Minimum, double Preferred) result;
        switch (block)
        {
            case PdfFlowParagraph paragraph:
                var data = new PdfFlowParagraphData(paragraph, _options, _shaper, _context);
                double preferred = 0, minimum = 0;
                int begin = 0;
                foreach (int end in data.HardBreaks.OrderBy(i => i).Concat(new[] { data.Text.Length }))
                {
                    preferred = Math.Max(preferred, data.Get(begin, end).Width);
                    begin = end;
                }

                if (_options.Overflow == PdfFlowOverflow.Error)
                {
                    begin = 0;
                    foreach (int end in data.Breaks.OrderBy(i => i))
                    {
                        minimum = Math.Max(minimum, data.Get(begin, end).Width);
                        begin = end;
                    }
                }
                else if (_options.Overflow == PdfFlowOverflow.BreakGrapheme)
                {
                    begin = 0;
                    foreach (int end in data.Ends)
                    {
                        minimum = Math.Max(minimum, data.Get(begin, end).Width);
                        begin = end;
                    }
                }

                double indents = paragraph.Style.LeftIndent + paragraph.Style.RightIndent + paragraph.Style.FirstLineIndent;
                result = (minimum + indents, preferred + indents);
                break;
            case PdfFlowFloat value:
                result = (value.Width + value.Gap, value.Width + value.Gap);
                break;
            case PdfTable nested:
                if (!_path.Add(nested))
                    throw Error("Table.ContentCycle", "Nested table content contains a cycle.");
                try
                {
                    var constraints = Constraints(nested);
                    result = (constraints.Minimum.Sum(), constraints.Preferred.Sum());
                }
                finally
                {
                    _path.Remove(nested);
                }

                break;
            case PdfFlowHeading heading: result = Natural(heading.Paragraph); break;
            case PdfFlowList list: result = Natural(list.ToTable(_options, _shaper, _context)); break;
            case PdfFlowComponent component:
                double componentMinimum = 0, componentPreferred = 0;
                foreach (var child in component.Content) { var natural = Natural(child); componentMinimum = Math.Max(componentMinimum, natural.Minimum); componentPreferred = Math.Max(componentPreferred, natural.Preferred); }
                result = (componentMinimum, componentPreferred); break;
            case PdfFlowColumns columns:
                double columnMinimum = 0, columnPreferred = 0;
                foreach (var child in columns.Content) { var natural = Natural(child); columnMinimum = Math.Max(columnMinimum, natural.Minimum); columnPreferred = Math.Max(columnPreferred, natural.Preferred); }
                result = (columnMinimum * columns.Count + columns.Gap * (columns.Count - 1), columnPreferred * columns.Count + columns.Gap * (columns.Count - 1)); break;
            case PdfFlowPageBreak:
                throw Error("Table.CellPageBreak", "An explicit page break inside a cell requires a separate cell-fragment policy.");
            default:
                throw Error("Table.ContentType", "This cell block type has no table measurement profile.");
        }

        PdfGraphicsNumbers.Finite(result.Minimum);
        PdfGraphicsNumbers.Finite(result.Preferred);
        _natural.Add(block, result);
        return result;
    }

    private PdfTableCellContent Content(IReadOnlyList<PdfFlowBlock> blocks, double width)
    {
        if (blocks.Count == 0)
            return new(null, width, 0, Array.Empty<PdfTableContentSegment>(), Array.Empty<PdfTableInterval>(), Array.Empty<PdfTableRepeatSource>());
        if (blocks.OfType<PdfFlowParagraph>().Any(p => p.Style.PageBreakBefore))
            throw Error("Table.CellPageBreak", "Page-break-before inside a cell requires a separate cell-fragment policy.");
        var geometry = new PdfPageGeometry(new(new(0, 0, width, CellViewport)));
        var options = new PdfFlowOptions(geometry, PdfMargins.Uniform(0), _options.Overflow, _options.MaximumPages, _options.TabInterval, _options.TabStops.ToArray());
        var engine = new PdfFlowEngine(options, _shaper, _context);
        var flow = engine.Layout(blocks);
        _finish.Add(report =>
        {
            engine.Complete(report);
            flow.OperationReport = report;
        });
        _context.Consume(PdfResourceKind.ExpandedBytes, flow.Pages.Count * 96L + 128);
        var segments = new List<PdfTableContentSegment>();
        var intervals = new List<PdfTableInterval>();
        var repeats = new List<PdfTableRepeatSource>();
        var logicalLines = new List<(PdfFlowLine Line, double Y)>();
        double y = 0;
        for (int i = 0; i < flow.Pages.Count; i++)
        {
            var page = flow.Pages[i];
            double height = i + 1 < flow.Pages.Count ? CellViewport : page.ContentHeight;
            _context.Consume(PdfResourceKind.ExpandedBytes, page.Canvas.Placements.Count * 32L + 32);
            foreach (var placement in page.Canvas.Placements)
                segments.Add(new(placement, y));
            _context.Consume(PdfResourceKind.ExpandedBytes, (page.Lines.Count + page.Floats.Count) * 64L + page.Tables.Count * 32L + 64);
            foreach (var line in page.Lines)
            {
                intervals.Add(new(y + line.Bounds.Y, y + line.Bounds.Top));
                logicalLines.Add((line, y));
            }

            foreach (var value in page.Floats)
                intervals.Add(new(y + value.Bounds.Y, y + value.Bounds.Top));
            foreach (var table in page.Tables)
                foreach (var interval in table.UnsafeIntervals)
                    intervals.Add(new(y + interval.Start, y + interval.End));
            CaptureRepeats(page, y, repeats);
            y += height;
        }

        foreach (var group in logicalLines.GroupBy(l => l.Line.BlockIndex))
        {
            if (blocks[group.Key] is not PdfFlowParagraph paragraph)
                continue;
            var lines = group.ToArray();
            var style = paragraph.Style;
            double Start(int index) => lines[index].Y + lines[index].Line.Bounds.Y;
            double End(int index) => lines[index].Y + lines[index].Line.Bounds.Top;
            if (style.KeepTogether)
                intervals.Add(new(Start(0), End(lines.Length - 1)));
            intervals.Add(new(Start(0), End(Math.Min(lines.Length, style.OrphanLines) - 1)));
            intervals.Add(new(Start(Math.Max(0, lines.Length - style.WidowLines)), End(lines.Length - 1)));
            if (style.KeepWithNext)
            {
                var following = logicalLines.Where(l => l.Line.BlockIndex == group.Key + 1).ToArray();
                if (following.Length > 0 && blocks[group.Key + 1] is PdfFlowParagraph next)
                {
                    int last = Math.Min(following.Length, next.Style.OrphanLines) - 1;
                    intervals.Add(new(Start(Math.Max(0, lines.Length - style.WidowLines)), following[last].Y + following[last].Line.Bounds.Top));
                }
            }
        }

        return new(flow, width, y, segments.ToArray(), intervals.ToArray(), repeats.ToArray());
    }

    private void CaptureRepeats(PdfFlowPage page, double pageY, List<PdfTableRepeatSource> output)
    {
        foreach (var fragment in page.Tables)
        {
            _context.Checkpoint();
            var plan = fragment.Plan;
            double header = plan.HeaderHeight;
            if (header > 0 && fragment.BodyEnd > fragment.BodyStart)
            {
                var bounds = new PdfRect(fragment.Bounds.X, fragment.Bounds.Y, plan.Width, header);
                _context.Consume(PdfResourceKind.ExpandedBytes, page.Canvas.Placements.Count * 16L + 128);
                var sources = page.Canvas.Placements.Where(p => Overlap(p.VisibleBounds, bounds)).ToArray();
                output.Add(new(plan.Table, sources, bounds, fragment.Bounds.X, pageY + bounds.Top, pageY + fragment.Bounds.Top, 0));
            }

            foreach (var cell in plan.Cells)
            {
                if (cell.Grid.Row < plan.Table.HeaderRows)
                    continue;
                double x = fragment.Bounds.X + cell.Bounds.X + cell.Grid.Cell.Style.Left.Width + cell.Grid.Cell.Style.Padding.Left;
                foreach (var repeat in cell.Content.Repeats)
                {
                    double start = Math.Max(fragment.BodyStart, cell.ContentY + repeat.Start), end = Math.Min(fragment.BodyEnd, cell.ContentY + repeat.End);
                    if (end <= start)
                        continue;
                    _context.Consume(PdfResourceKind.ExpandedBytes, 128);
                    double target = pageY + fragment.Bounds.Y + header + fragment.NestedHeaderBandHeight;
                    output.Add(new(repeat.Table, repeat.Sources, repeat.SourceBounds, x + repeat.X, target + start - fragment.BodyStart, target + end - fragment.BodyStart, repeat.Depth + 1));
                }
            }
        }

        bool Overlap(PdfRect a, PdfRect b) => a.Right > b.X && b.Right > a.X && a.Top > b.Y && b.Top > a.Y;
    }

    private void Satisfy(double[] widths, int start, int span, double required, IReadOnlyList<PdfTableColumn> columns, bool minimum)
    {
        double sum = 0;
        for (int c = start; c < start + span; c++)
            sum += widths[c];
        double deficit = required - sum;
        for (int iteration = 0; deficit > 1e-9 && iteration <= span; iteration++)
        {
            _context.Checkpoint();
            double weights = 0, maximumWeight = 0;
            for (int c = start; c < start + span; c++)
                if (!columns[c].IsFixed && widths[c] < columns[c].Maximum)
                    maximumWeight = Math.Max(maximumWeight, columns[c].Weight);
            for (int c = start; c < start + span; c++)
                if (!columns[c].IsFixed && widths[c] < columns[c].Maximum)
                    weights += columns[c].Weight / maximumWeight;
            if (weights == 0)
                break;
            double used = 0;
            for (int c = start; c < start + span; c++)
            {
                if (columns[c].IsFixed)
                    continue;
                double addition = Math.Min(columns[c].Maximum - widths[c], deficit / weights * (columns[c].Weight / maximumWeight));
                widths[c] += addition;
                used += addition;
            }

            if (used <= 0)
                break;
            deficit -= used;
        }

        if (minimum && deficit > 1e-8 && Enumerable.Range(start, span).Any(c => !columns[c].IsFixed))
            throw Error("Table.ColumnMaximum", "Automatic column limits cannot accommodate a cell's minimum content width.");
    }

    private void Grow(double[] widths, double[] limits, IReadOnlyList<PdfTableColumn> columns, double available)
    {
        for (int iteration = 0; available > 1e-9 && iteration <= widths.Length; iteration++)
        {
            _context.Checkpoint();
            double weights = 0, maximumWeight = 0;
            for (int i = 0; i < widths.Length; i++)
                if (!columns[i].IsFixed && widths[i] < limits[i])
                    maximumWeight = Math.Max(maximumWeight, columns[i].Weight);
            for (int i = 0; i < widths.Length; i++)
                if (!columns[i].IsFixed && widths[i] < limits[i])
                    weights += columns[i].Weight / maximumWeight;
            if (weights == 0)
                return;
            double used = 0;
            for (int i = 0; i < widths.Length; i++)
            {
                if (columns[i].IsFixed || widths[i] >= limits[i])
                    continue;
                double addition = Math.Min(limits[i] - widths[i], available / weights * (columns[i].Weight / maximumWeight));
                widths[i] += addition;
                used += addition;
            }

            if (used <= 0)
                return;
            available -= used;
        }
    }

    private double[] Prefix(double[] widths)
    {
        var result = new double[widths.Length + 1];
        for (int i = 0; i < widths.Length; i++)
        {
            result[i + 1] = result[i] + widths[i];
            PdfGraphicsNumbers.Finite(result[i + 1]);
        }

        return result;
    }

    internal static double InsetX(PdfTableCellStyle style) => style.Padding.Left + style.Padding.Right + style.Left.Width + style.Right.Width;
    internal static double InsetY(PdfTableCellStyle style) => style.Padding.Top + style.Padding.Bottom + style.Top.Width + style.Bottom.Width;
    private PdfException Error(string code, string message) => PdfWriterCore.Error(_context, code, message);
}
