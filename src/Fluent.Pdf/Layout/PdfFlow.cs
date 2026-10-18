using System.Threading;

namespace Fluent.Pdf;
/// <summary>Bounded offline paragraph layout and deterministic pagination using explicit shaping providers.</summary>
public static class PdfFlow
{
    /// <summary>Lays out immutable blocks using explicitly supplied offline shaping, shared budgets and cooperative cancellation.</summary>
    public static PdfFlowResult Layout(IEnumerable<PdfFlowBlock> blocks, PdfFlowOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (blocks == null)
            throw new ArgumentNullException(nameof(blocks));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        var shaper = providers.GetRequired<IPdfTextShaper>(PdfCapability.ShapeText);
        PdfFlowResult? result = null;
        PdfFlowEngine? engine = null;
        var report = PdfFont.Run("LayoutFlow", "managed.layout", policy, cancellationToken, context =>
        {
            context.Consume(PdfResourceKind.ExpandedBytes, 512);
            var copy = new List<PdfFlowBlock>();
            foreach (var block in blocks)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 64);
                if (block == null)
                    throw new ArgumentException("Flow blocks cannot contain null.", nameof(blocks));
                copy.Add(block);
            }

            engine = new PdfFlowEngine(options, shaper, context);
            result = engine.Layout(copy);
        }, PdfCapability.WriteDocument);
        engine!.Complete(report);
        result!.OperationReport = report;
        return result;
    }
}

internal sealed partial class PdfFlowEngine
{
    private readonly PdfFlowOptions _options;
    private readonly IPdfTextShaper _shaper;
    private readonly PdfOperationContext _context;
    private readonly List<(int Page, PdfFlowFloatPlacement Value)> _floats = new();
    private readonly List<PlannedLine> _lines = new();
    private readonly List<Action<PdfOperationReport>> _finish = new();
    private readonly Dictionary<int, PdfFlowParagraphData> _prepared = new();
    private int _page;
    private double _y;
    internal PdfFlowEngine(PdfFlowOptions options, IPdfTextShaper shaper, PdfOperationContext context)
    {
        _options = options;
        _shaper = shaper;
        _context = context;
    }

    private sealed class PlannedLine
    {
        internal PlannedLine(int page, PdfFlowLine line, PdfFlowParagraphData.Measure measured, PdfFlowParagraphStyle style, bool last)
        {
            Page = page;
            Line = line;
            Measured = measured;
            Style = style;
            Last = last;
        }

        internal int Page { get; }
        internal PdfFlowLine Line { get; }
        internal PdfFlowParagraphData.Measure Measured { get; }
        internal PdfFlowParagraphStyle Style { get; }
        internal bool Last { get; }
    }

    internal PdfFlowResult Layout(IReadOnlyList<PdfFlowBlock> blocks)
    {
        using var layoutDepth = _context.EnterScope(PdfResourceKind.Depth);
        blocks = ExpandComponents(blocks);
        for (int i = 0; i < blocks.Count; i++)
        {
            _context.Checkpoint();
            switch (blocks[i])
            {
                case PdfFlowPageBreak:
                    NewPage();
                    break;
                case PdfFlowFloat value:
                    PlaceFloat(i, value);
                    break;
                case PdfFlowList list:
                    PlaceTable(i, list.ToTable(_options, _shaper, _context));
                    break;
                case PdfFlowColumns columns:
                    PlaceColumns(i, columns);
                    break;
                case PdfFlowComponent component:
                    PlaceKeptComponent(i, component);
                    break;
                case PdfFlowSectionBreak section:
                    Section(section.Start);
                    break;
                case PdfFlowNoteReference note:
                    PlaceNote(i, note);
                    break;
                case PdfTable table:
                    PlaceTable(i, table);
                    break;
                case PdfFlowParagraph value:
                    var data = Prepare(i, value);
                    var style = value.Style;
                    if (style.PageBreakBefore && HasContent(_page))
                        NewPage();
                    Clear(style.Clear);
                    List<PlannedLine> plan;
                    if (style.KeepWithNext)
                    {
                        plan = PlanKeepGroup(i, blocks, out int consumed);
                        i += consumed - 1;
                        style = ((PdfFlowParagraph)blocks[i]).Style;
                    }
                    else
                        plan = PlanParagraph(i, data, style, _page, _y);
                    _lines.AddRange(plan);
                    var end = plan[plan.Count - 1];
                    _page = end.Page;
                    _y = end.Line.Bounds.Top + style.SpaceAfter;
                    CheckPage(_page);
                    break;
                default:
                    throw Error("Layout.BlockType", "The requested block type has no managed layout implementation.");
            }
        }

        AppendEndnotes();
        _context.Consume(PdfResourceKind.ExpandedBytes, (_page + 1L) * 256 + _lines.Count * 32L + _floats.Count * 32L);
        var pages = new PdfFlowPage[_page + 1];
        for (int p = 0; p < pages.Length; p++)
        {
            CheckPage(p);
            _context.Consume(PdfResourceKind.Objects, 1);
            var lines = _lines.Where(l => l.Page == p).ToArray();
            var floats = _floats.Where(f => f.Page == p).Select(f => f.Value).ToArray();
            var tables = _tableFragments.Where(t => t.Page == p).Select(t => t.Fragment).ToArray();
            var columns = _columns.Where(v => v.Page == p).Select(v => v.Column).ToArray();
            var components = _components.Where(v => v.Page == p).ToArray();
            var notes = NotesForPage(p);
            var canvas = PdfCanvas.CreateCore(_options.Geometry, c =>
            {
                var paint = new List<(int Block, Action Draw)>();
                foreach (var table in tables)
                    paint.Add((table.BlockIndex, () => PdfTablePainter.Paint(c, table, _context)));
                foreach (var f in floats)
                    paint.Add((f.BlockIndex, () => c.DrawDrawing(f.Float.Drawing, f.Float.SourceBounds, f.Bounds, PdfCanvasFit.Stretch)));
                foreach (var line in lines)
                    paint.Add((line.Line.BlockIndex, () => PdfFlowLinePainter.Paint(c, line.Line, line.Measured, line.Style, line.Last, _options, _context, _finish)));
                foreach (var column in columns)
                    paint.Add((column.BlockIndex, () => column.PlacementIndex = c.DrawDrawing(column.Source.Canvas.Drawing, column.Source.Canvas.Geometry.Boxes.Media, column.Bounds, PdfCanvasFit.Stretch).Index));
                foreach (var component in components)
                    paint.Add((component.Block, () => c.DrawDrawing(component.Source.Canvas.Drawing, component.Source.Canvas.Geometry.Boxes.Media, new(0, component.Y, _options.Frame.Width, component.Source.Canvas.Geometry.Size.Height), PdfCanvasFit.Stretch)));
                foreach (var item in paint.OrderBy(v => v.Block))
                    item.Draw();
                PaintNotes(c, p);
            }, new(PdfLengthUnit.Point, margins: _options.Margins), _context, out var finish);
            _finish.Add(finish);
            double used = Math.Max(lines.Select(l => l.Line.Bounds.Top).DefaultIfEmpty(0).Max(), Math.Max(floats.Select(f => f.Bounds.Top).DefaultIfEmpty(0).Max(), tables.Select(t => t.Bounds.Top).DefaultIfEmpty(0).Max()));
            used = Math.Max(used, columns.Select(v => v.Bounds.Y + v.Source.ContentHeight).DefaultIfEmpty(0).Max());
            used = Math.Max(used, notes.Select(v => v.Bounds.Top).DefaultIfEmpty(0).Max());
            if (p == _page)
                used = Math.Max(used, Math.Min(_options.Frame.Height, _y));
            pages[p] = new(p, canvas, lines.Select(l => l.Line).ToArray(), floats, tables, used, columns, notes);
        }

        return new(_options, pages);
    }

    internal void Complete(PdfOperationReport report)
    {
        foreach (var callback in _finish)
            callback(report);
        foreach (var line in _lines)
        {
            line.Line.ShapedText.OperationReport = report;
            line.Line.ShapedText.Unicode.OperationReport = report;
        }

        _finish.Clear();
    }

    private PdfFlowParagraphData Prepare(int index, PdfFlowParagraph paragraph)
    {
        if (!_prepared.TryGetValue(index, out var data))
        {
            data = new(paragraph, _options, _shaper, _context);
            _prepared.Add(index, data);
        }

        return data;
    }

    private List<PlannedLine> PlanKeepGroup(int firstBlock, IReadOnlyList<PdfFlowBlock> blocks, out int consumed)
    {
        int finalBlock = firstBlock;
        while (((PdfFlowParagraph)blocks[finalBlock]).Style.KeepWithNext)
        {
            if (finalBlock + 1 >= blocks.Count || blocks[finalBlock + 1] is not PdfFlowParagraph)
                throw Error("Layout.KeepWithNextTarget", "Keep-with-next requires a following paragraph.");
            finalBlock++;
            if (((PdfFlowParagraph)blocks[finalBlock]).Style.PageBreakBefore)
                throw Error("Layout.KeepBreakConflict", "A page-break-before conflicts with the preceding keep-with-next constraint.");
        }

        consumed = finalBlock - firstBlock + 1;
        _context.Consume(PdfResourceKind.ExpandedBytes, consumed * 128L + 128);
        var forced = new Dictionary<int, HashSet<int>>();
        for (int attempt = 0; attempt < 32; attempt++)
        {
            _context.Checkpoint();
            var plans = new List<List<PlannedLine>>();
            int page = _page;
            double y = _y;
            for (int block = firstBlock; block <= finalBlock; block++)
            {
                var paragraph = (PdfFlowParagraph)blocks[block];
                foreach (var f in _floats)
                    if (f.Page == page && Clears(paragraph.Style.Clear, f.Value.Float.Side))
                        y = Math.Max(y, f.Value.Bounds.Top);
                var data = Prepare(block, paragraph);
                forced.TryGetValue(block, out var required);
                var plan = PlanParagraph(block, data, paragraph.Style, page, y, required);
                plans.Add(plan);
                var last = plan[plan.Count - 1];
                page = last.Page;
                y = last.Line.Bounds.Top + paragraph.Style.SpaceAfter;
            }

            int conflict = -1;
            for (int i = 0; i + 1 < plans.Count; i++)
            {
                var nextStyle = ((PdfFlowParagraph)blocks[firstBlock + i + 1]).Style;
                int pageOfEnd = plans[i][plans[i].Count - 1].Page;
                if (plans[i + 1].Take(Math.Min(nextStyle.OrphanLines, plans[i + 1].Count)).Any(l => l.Page != pageOfEnd))
                {
                    conflict = i;
                    break;
                }
            }

            if (conflict < 0)
                return plans.SelectMany(p => p).ToList();
            while (true)
            {
                var current = plans[conflict];
                var tailStyle = ((PdfFlowParagraph)blocks[firstBlock + conflict]).Style;
                int pageOfEnd = current[current.Count - 1].Page;
                var lastGroup = current.Where(l => l.Page == pageOfEnd).ToArray();
                int tailCount = current.Select(l => l.Page).Distinct().Count() > 1 ? tailStyle.WidowLines : 1;
                int offset = lastGroup[Math.Max(0, lastGroup.Length - tailCount)].Line.Utf16Offset;
                if (offset == 0 && conflict > 0)
                {
                    conflict--;
                    continue;
                }

                int block = firstBlock + conflict;
                if (!forced.TryGetValue(block, out var breaks))
                {
                    breaks = new HashSet<int>();
                    forced.Add(block, breaks);
                }

                if (!breaks.Add(offset))
                    throw Error("Layout.KeepWithNextImpossible", "The keep chain cannot fit an empty page under its line constraints.");
                break;
            }
        }

        throw Error("Layout.ConstraintIterations", "The bounded keep-chain solver did not converge.");
    }

    private static bool Clears(PdfFlowClear clear, PdfFlowFloatSide side) => clear == PdfFlowClear.Both || clear == PdfFlowClear.Left && side == PdfFlowFloatSide.Left || clear == PdfFlowClear.Right && side == PdfFlowFloatSide.Right;
    private List<PlannedLine> PlanParagraph(int block, PdfFlowParagraphData data, PdfFlowParagraphStyle style, int initialPage, double initialY, HashSet<int>? requiredBreaks = null)
    {
        var forced = requiredBreaks == null ? new HashSet<int>() : new HashSet<int>(requiredBreaks);
        int startPage = initialPage;
        double startY = initialY;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            _context.Checkpoint();
            var plan = PlanRaw(block, data, style, startPage, startY, forced);
            var groups = plan.GroupBy(l => l.Page).ToArray();
            if (groups.Length == 1)
                return plan;
            if (style.KeepTogether)
            {
                if (startY == 0 && !_floats.Any(f => f.Page == startPage))
                    throw Error("Layout.KeepTogetherImpossible", "The paragraph cannot fit one empty page while kept together.");
                startPage++;
                startY = 0;
                forced.Clear();
                continue;
            }

            if (groups[0].Count() < style.OrphanLines)
            {
                if (startY == 0 && !_floats.Any(f => f.Page == startPage))
                    throw Error("Layout.OrphanConstraint", "An empty page cannot satisfy the paragraph orphan constraint.");
                startPage++;
                startY = 0;
                forced.Clear();
                continue;
            }

            var badMiddle = groups.Skip(1).Take(groups.Length - 2).FirstOrDefault(g => g.Count() < Math.Max(style.OrphanLines, style.WidowLines));
            if (badMiddle != null)
                throw Error("Layout.LineConstraint", "An intermediate page cannot satisfy the paragraph line constraints.");
            if (groups[groups.Length - 1].Count() >= style.WidowLines)
                return plan;
            int move = style.WidowLines - groups[groups.Length - 1].Count();
            var previous = groups[groups.Length - 2].ToArray();
            if (previous.Length - move < style.OrphanLines)
                throw Error("Layout.WidowConstraint", "The paragraph cannot satisfy both widow and orphan constraints.");
            int offset = previous[previous.Length - move].Line.Utf16Offset;
            if (!forced.Add(offset))
                throw Error("Layout.LineConstraint", "The paragraph constraints did not converge.");
        }

        throw Error("Layout.ConstraintIterations", "The bounded paragraph constraint solver did not converge.");
    }

    private List<PlannedLine> PlanRaw(int block, PdfFlowParagraphData data, PdfFlowParagraphStyle style, int page, double y, HashSet<int> forced)
    {
        var plan = new List<PlannedLine>();
        int start = 0;
        bool empty = data.Text.Length == 0, first = true;
        y += style.SpaceBefore;
        while (start < data.Text.Length || empty)
        {
            _context.Checkpoint();
            _context.Consume(PdfResourceKind.Objects, 1);
            _context.Consume(PdfResourceKind.ExpandedBytes, 384);
            if (forced.Contains(start) && y > 0)
            {
                page++;
                y = 0;
            }

            double height = data.Height;
            if (height > _options.Frame.Height)
                throw Error("Layout.LineTooTall", "A complete line cannot fit the empty page frame.");
            if (y + height > BodyHeight(page))
            {
                page++;
                y = 0;
            }

            CheckPage(page);
            if (height > BodyHeight(page))
                throw Error("Composition.NoteFrame", "Notes leave insufficient body height for a complete line.");
            var interval = Interval(page, y, height);
            double left = interval.Left + style.LeftIndent + (first ? style.FirstLineIndent : 0), right = interval.Right - style.RightIndent;
            if (right <= left)
            {
                double bottom = FloatBottom(page, y, height);
                if (bottom > y)
                {
                    y = bottom;
                    continue;
                }

                throw Error("Layout.ParagraphFrame", "Paragraph indentation leaves no usable line interval.");
            }

            double width = right - left;
            int end = empty ? 0 : FindEnd(data, start, width);
            var measure = data.Get(start, end);
            // A line is reshaped with the original paragraph levels; contextual advances may differ at the break.
            while (measure.Width > width + 1e-9 && end > start)
            {
                int previous = PreviousEnd(data, start, end);
                if (previous <= start)
                    break;
                if (_options.Overflow == PdfFlowOverflow.Error && !data.Breaks.Contains(previous))
                    break;
                end = previous;
                measure = data.Get(start, end);
            }

            bool clipped = measure.Width > width + 1e-9;
            if (clipped && _options.Overflow != PdfFlowOverflow.Clip)
            {
                double bottom = FloatBottom(page, y, height);
                if (bottom > y)
                {
                    y = bottom;
                    continue;
                }

                throw Error("Layout.WordOverflow", "An unbreakable word or grapheme exceeds the empty available line interval.");
            }

            if (clipped)
                _context.Report(new("Layout.ClippedText", "The explicitly requested overflow policy clips a line to its available interval.", lossCategory: PdfLossCategory.OmittedContent, pageIndex: page));
            height = measure.Height;
            if (height > data.Height + 1e-8)
                throw Error("Layout.ContextualHeight", "Contextual line shaping exceeds the paragraph metric envelope.");
            double extra = Math.Max(0, height - (measure.Top - measure.Bottom));
            var bounds = new PdfRect(left, y, width, height);
            var line = new PdfFlowLine(block, start, end - start, data.Text.Substring(start, end - start), bounds, y + extra / 2 + measure.Top, measure.Advance, measure.Shaped, data.HardBreaks.Contains(end), clipped);
            plan.Add(new(page, line, measure, style, end == data.Text.Length || line.EndsHardBreak));
            y += height;
            first = false;
            empty = false;
            if (end == start && start < data.Text.Length)
                throw Error("Layout.NoProgress", "The paragraph did not advance a grapheme boundary.");
            start = end;
        }

        return plan;
    }

    private int FindEnd(PdfFlowParagraphData data, int start, double width)
    {
        double advance = 0;
        int last = start, opportunity = start;
        foreach (int end in data.Ends)
        {
            if (end <= start)
                continue;
            _context.Checkpoint();
            _context.Consume(PdfResourceKind.Objects, 1);
            int begin = last;
            if (data.Text[begin] == '\t')
                advance = _options.NextTab(advance);
            else
            {
                data.Advances.TryGetValue(begin, out double addition);
                advance += addition;
            }

            bool hard = data.HardBreaks.Contains(end);
            if (advance > width + 1e-9)
            {
                if (opportunity > start)
                    return opportunity;
                if (_options.Overflow == PdfFlowOverflow.BreakGrapheme)
                    return last == start ? end : last;
                // Retain the complete overlong word for an explicit error or diagnosed clipping.
                if (data.Breaks.Contains(end) || hard)
                    return end;
                last = end;
                continue;
            }

            last = end;
            if (data.Breaks.Contains(end))
                opportunity = end;
            if (hard)
                return end;
        }

        return last;
    }

    private int PreviousEnd(PdfFlowParagraphData data, int start, int end)
    {
        int previous = start;
        foreach (int value in data.Ends)
        {
            if (value >= end)
                break;
            if (value <= start)
                continue;
            if (_options.Overflow == PdfFlowOverflow.BreakGrapheme || data.Breaks.Contains(value))
                previous = value;
        }

        return previous;
    }

    internal static double AlignmentShift(PdfFlowAlignment alignment, PdfFlowParagraphData.Measure measure, double width)
    {
        bool rtl = measure.Shaped.Unicode.Paragraphs.Count > 0 && measure.Shaped.Unicode.Paragraphs[0].EmbeddingLevel == 1;
        if (alignment == PdfFlowAlignment.Center)
            return Math.Max(0, width - measure.Width) / 2;
        if (alignment == PdfFlowAlignment.End ? !rtl : alignment == PdfFlowAlignment.Start && rtl)
            return Math.Max(0, width - measure.Width);
        return 0;
    }

    private void PlaceFloat(int block, PdfFlowFloat value)
    {
        Clear(value.Clear);
        if (value.Width > _options.Frame.Width || value.Height > _options.Frame.Height)
            throw Error("Layout.FloatOverflow", "A floating rectangle cannot fit an empty page frame.");
        double y = _y;
        while (true)
        {
            _context.Checkpoint();
            _context.Consume(PdfResourceKind.Objects, 1);
            if (y + value.Height > BodyHeight(_page))
            {
                NewPage();
                y = 0;
            }

            var interval = Interval(_page, y, value.Height);
            if (interval.Right - interval.Left >= value.Width)
            {
                double x = value.Side == PdfFlowFloatSide.Left ? interval.Left : interval.Right - value.Width;
                _context.Consume(PdfResourceKind.ExpandedBytes, 256);
                _floats.Add((_page, new(block, value, new(x, y, value.Width, value.Height))));
                _y = y;
                return;
            }

            double bottom = FloatBottom(_page, y, value.Height);
            if (bottom <= y)
                throw Error("Layout.FloatNoProgress", "The float exclusion geometry did not advance.");
            y = bottom;
        }
    }

    private (double Left, double Right) Interval(int page, double y, double height)
    {
        double left = 0, right = _options.Frame.Width;
        foreach (var f in _floats)
        {
            _context.Checkpoint();
            if (f.Page != page || f.Value.Bounds.Y >= y + height || f.Value.Bounds.Top <= y)
                continue;
            if (f.Value.Float.Side == PdfFlowFloatSide.Left)
                left = Math.Max(left, f.Value.Bounds.Right + f.Value.Float.Gap);
            else
                right = Math.Min(right, f.Value.Bounds.X - f.Value.Float.Gap);
        }

        return (left, right);
    }

    private double FloatBottom(int page, double y, double height)
    {
        double bottom = double.PositiveInfinity;
        foreach (var f in _floats)
            if (f.Page == page && f.Value.Bounds.Y < y + height && f.Value.Bounds.Top > y)
                bottom = Math.Min(bottom, f.Value.Bounds.Top);
        return double.IsPositiveInfinity(bottom) ? y : bottom;
    }

    private void Clear(PdfFlowClear clear)
    {
        if (clear == PdfFlowClear.None)
            return;
        foreach (var f in _floats)
            if (f.Page == _page && (clear == PdfFlowClear.Both || clear == PdfFlowClear.Left && f.Value.Float.Side == PdfFlowFloatSide.Left || clear == PdfFlowClear.Right && f.Value.Float.Side == PdfFlowFloatSide.Right))
                _y = Math.Max(_y, f.Value.Bounds.Top);
    }

    private bool HasContent(int page) => _y > 0 || _lines.Any(l => l.Page == page) || _floats.Any(f => f.Page == page);
    private void NewPage()
    {
        _page++;
        _y = 0;
        CheckPage(_page);
    }

    private void CheckPage(int page)
    {
        if (page >= _options.MaximumPages)
            throw Error("Limit.Pages", "Flow pagination exceeds the configured page limit.");
    }

    private PdfException Error(string code, string message) => PdfWriterCore.Error(_context, code, message);
}
