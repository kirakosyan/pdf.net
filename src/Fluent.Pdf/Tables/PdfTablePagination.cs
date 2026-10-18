namespace Fluent.Pdf;
internal sealed partial class PdfFlowEngine
{
    private readonly List<(int Page, PdfTableFragment Fragment)> _tableFragments = new();
    private void PlaceTable(int block, PdfTable table)
    {
        Clear(PdfFlowClear.Both);
        var measure = new PdfTableMeasure(_options, _shaper, _context, _finish);
        var plan = measure.Build(table, _options.Frame.Width);
        if (plan.Height == 0)
            return;
        double header = plan.HeaderHeight;
        if (header > _options.Frame.Height)
            throw Error("Table.HeaderOverflow", "Complete repeated headers cannot fit an empty page frame.");
        double start = header;
        bool repeated = false, first = true;
        while (start < plan.Height || first && start == plan.Height)
        {
            _context.Checkpoint();
            _context.Consume(PdfResourceKind.Objects, 1);
            CheckPage(_page);
            var repeats = ContinuationHeaders(plan, start, _y + header, out double nestedBand);
            double available = _options.Frame.Height - _y - header - nestedBand;
            if (available < 0 || start < plan.Height && available <= 0)
            {
                if (_y == 0)
                    throw Error("Table.HeaderOverflow", "Repeated headers leave no body interval on an empty page.");
                NewPage();
                continue;
            }

            double end = Math.Min(plan.Height, start + Math.Max(0, available));
            end = SafeCut(plan, start, end);
            if (end <= start && start < plan.Height)
            {
                if (_y > 0)
                {
                    NewPage();
                    continue;
                }

                throw Error("Table.RowFragmentOverflow", "A complete text/float/unsplittable row interval cannot fit the body frame below repeated headers.");
            }

            double height = header + nestedBand + end - start;
            _context.Consume(PdfResourceKind.ExpandedBytes, plan.Cells.Length * 96L + plan.UnsafeIntervals.Length * 32L + 256);
            var cells = new List<PdfTableCellFragment>();
            var intervals = new List<PdfTableInterval>();
            void AddRange(double from, double to, double target, bool isHeader)
            {
                if (to <= from)
                    return;
                foreach (var cell in plan.Cells)
                {
                    _context.Checkpoint();
                    double top = Math.Max(from, cell.Bounds.Y), bottom = Math.Min(to, cell.Bounds.Top);
                    if (bottom <= top)
                        continue;
                    double extra = !isHeader && cell.Bounds.Y <= from ? nestedBand : 0;
                    cells.Add(new(cell.Grid, new(cell.Bounds.X, target + top - from - extra, cell.Bounds.Width, bottom - top + extra), isHeader && repeated, !isHeader && from > cell.Bounds.Y));
                }

                foreach (var interval in plan.UnsafeIntervals)
                {
                    double top = Math.Max(from, interval.Start), bottom = Math.Min(to, interval.End);
                    if (bottom > top)
                        intervals.Add(new(target + top - from, target + bottom - from));
                }
            }

            AddRange(0, header, _y, true);
            AddRange(start, end, _y + header + nestedBand, false);
            if (header + nestedBand > 0)
            {
                double protectedEnd = _y + header + nestedBand;
                var firstBody = intervals.Where(i => i.End > protectedEnd).OrderBy(i => i.Start).FirstOrDefault();
                if (firstBody.End > protectedEnd)
                    protectedEnd = firstBody.End;
                intervals.Add(new(_y, protectedEnd));
            }

            var fragment = new PdfTableFragment(block, plan, new(0, _y, plan.Width, height), start, end, repeated, cells.ToArray(), intervals.ToArray(), repeats, nestedBand);
            _tableFragments.Add((_page, fragment));
            _y += height;
            first = false;
            start = end;
            if (start < plan.Height)
            {
                repeated = true;
                NewPage();
            }
        }
    }

    private PdfTableHeaderRepeat[] ContinuationHeaders(PdfTablePlan plan, double start, double targetY, out double height)
    {
        var active = new List<(PdfTableRepeatSource Source, double X)>();
        var heights = new SortedDictionary<int, double>();
        foreach (var cell in plan.Cells)
        {
            if (cell.Grid.Row < plan.Table.HeaderRows)
                continue;
            foreach (var repeat in cell.Content.Repeats)
            {
                _context.Checkpoint();
                _context.Consume(PdfResourceKind.Objects, 1);
                if (start < cell.ContentY + repeat.Start || start >= cell.ContentY + repeat.End)
                    continue;
                _context.Consume(PdfResourceKind.ExpandedBytes, 192);
                active.Add((repeat, cell.Bounds.X + cell.Grid.Cell.Style.Left.Width + cell.Grid.Cell.Style.Padding.Left + repeat.X));
                heights.TryGetValue(repeat.Depth, out double current);
                heights[repeat.Depth] = Math.Max(current, repeat.SourceBounds.Height);
            }
        }

        height = heights.Values.Sum();
        var result = new PdfTableHeaderRepeat[active.Count];
        for (int i = 0; i < active.Count; i++)
        {
            var value = active[i];
            double top = targetY;
            foreach (var level in heights)
                if (level.Key < value.Source.Depth)
                    top += level.Value;
            result[i] = new(value.Source, new(value.X, top, value.Source.SourceBounds.Width, value.Source.SourceBounds.Height));
        }

        return result;
    }

    private double SafeCut(PdfTablePlan plan, double start, double end)
    {
        if (end >= plan.Height)
            return plan.Height;
        // Prefer a complete row only when that boundary is also safe for every merged cell.
        double cap = ContentCut(end);
        int low = 0, high = plan.Rows.Length - 1;
        while (low < high)
        {
            int mid = low + (high - low + 1) / 2;
            if (plan.Rows[mid] <= cap)
                low = mid;
            else
                high = mid - 1;
        }

        for (int row = low; row >= 0 && plan.Rows[row] > start; row--)
        {
            _context.Checkpoint();
            _context.Consume(PdfResourceKind.Objects, 1);
            double boundary = plan.Rows[row];
            if (ContentCut(boundary) == boundary)
                return boundary;
        }

        return Math.Max(start, cap);
        double ContentCut(double candidate)
        {
            for (int attempt = 0; attempt <= plan.UnsafeIntervals.Length; attempt++)
            {
                _context.Checkpoint();
                double previous = candidate;
                foreach (var interval in plan.UnsafeIntervals)
                {
                    _context.Checkpoint();
                    _context.Consume(PdfResourceKind.Objects, 1);
                    if (interval.Start >= candidate)
                        break;
                    if (interval.Start < candidate && interval.End > candidate)
                        candidate = Math.Min(candidate, interval.Start);
                }

                if (candidate == previous || candidate <= start)
                    return candidate;
            }

            throw Error("Table.SplitIterations", "The bounded safe-split calculation did not converge.");
        }
    }
}
