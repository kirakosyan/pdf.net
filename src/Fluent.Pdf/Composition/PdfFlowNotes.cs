namespace Fluent.Pdf;
internal sealed partial class PdfFlowEngine
{
    private const double NoteGap = 6;
    private sealed class PendingNote
    {
        internal PendingNote(PdfFlowNote note, PdfFlowResult flow, bool endnote)
        {
            Note = note;
            Flow = flow;
            IsEndnote = endnote;
        }

        internal PdfFlowNote Note { get; }
        internal PdfFlowResult Flow { get; }
        internal bool IsEndnote { get; }
        internal List<int> References { get; } = new();
        internal double Height => Flow.Pages[0].ContentHeight;
    }

    private readonly Dictionary<string, (PdfFlowNote Note, bool Endnote)> _noteDefinitions = new(StringComparer.Ordinal);
    private readonly Dictionary<int, List<PendingNote>> _footnotes = new();
    private readonly List<PendingNote> _endnotes = new();
    private readonly List<(int Page, PendingNote Note, double Y, double Height)> _endnotePlacements = new();
    private double ReservedNotes(int page) => _footnotes.TryGetValue(page, out var notes) ? notes.Sum(n => n.Height + NoteGap) : 0;
    private void PlaceNote(int block, PdfFlowNoteReference reference)
    {
        var definition = reference.Note;
        if (_noteDefinitions.TryGetValue(definition.Id, out var prior) && (!ReferenceEquals(prior.Note, definition) || prior.Endnote != reference.IsEndnote))
            throw Error("Composition.NoteIdentity", "A note ID must identify one immutable definition and placement policy.");
        _noteDefinitions[definition.Id] = (definition, reference.IsEndnote);
        if (reference.Paragraph.Style.KeepWithNext)
            throw Error("Composition.NoteKeep", "A note-reference paragraph requires an explicit following-block keep profile.");
        var style = definition.MarkerStyle;
        var direction = reference.Paragraph.Runs[0].Style.TextOptions.ParagraphDirection;
        var markerOptions = new PdfTextOptions(direction, false, style.TextOptions.Language, style.TextOptions.Features.ToArray());
        var raised = new PdfFlowTextStyle(style.Fonts, style.FontSize * .75, markerOptions, style.Color, style.BaselineShift + style.FontSize * .35);
        _context.Consume(PdfResourceKind.ExpandedBytes, reference.Paragraph.Runs.Count * 32L + 256);
        var paragraph = new PdfFlowParagraph(reference.Paragraph.Runs.Concat(new[] { new PdfFlowRun(definition.Label, raised) }), reference.Paragraph.Style);
        if (reference.Paragraph.Hyphenation != null) paragraph = paragraph.WithHyphenation(reference.Paragraph.Hyphenation);
        var data = Prepare(block, paragraph);
        if (paragraph.Style.PageBreakBefore && HasContent(_page))
            NewPage();
        Clear(paragraph.Style.Clear);
        var pending = FindNote(definition, reference.IsEndnote) ?? BuildNote(definition, reference.IsEndnote);
        if (reference.IsEndnote)
        {
            if (!_endnotes.Contains(pending))
                _endnotes.Add(pending);
            var plan = PlanParagraph(block, data, paragraph.Style, _page, _y);
            _lines.AddRange(plan);
            var last = plan[plan.Count - 1];
            _page = last.Page;
            _y = last.Line.Bounds.Top + paragraph.Style.SpaceAfter;
            pending.References.Add(block);
            return;
        }

        int startPage = _page;
        double startY = _y;
        var forced = new HashSet<int>();
        int? reservedPage = null;
        for (int attempt = 0; attempt < 32; attempt++)
        {
            _context.Checkpoint();
            var plan = PlanParagraph(block, data, paragraph.Style, startPage, startY, forced);
            var last = plan[plan.Count - 1];
            int target = last.Page;
            if (_footnotes.TryGetValue(target, out var existing) && existing.Any(n => ReferenceEquals(n.Note, definition)))
            {
                if (reservedPage.HasValue && reservedPage != target)
                    _footnotes[reservedPage.Value].Remove(pending);
                var found = existing.First(n => ReferenceEquals(n.Note, definition));
                found.References.Add(block);
                _lines.AddRange(plan);
                _page = target;
                _y = last.Line.Bounds.Top + paragraph.Style.SpaceAfter;
                return;
            }

            if (reservedPage == target)
            {
                pending.References.Add(block);
                _lines.AddRange(plan);
                _page = target;
                _y = last.Line.Bounds.Top + paragraph.Style.SpaceAfter;
                return;
            }

            if (reservedPage.HasValue)
            {
                _footnotes[reservedPage.Value].Remove(pending);
                var tail = plan.Where(l => l.Page == target).ToArray();
                int index = Math.Max(0, tail.Length - paragraph.Style.WidowLines);
                forced.Add(tail[index].Line.Utf16Offset);
            }

            if (!_footnotes.TryGetValue(target, out existing))
            {
                existing = new();
                _footnotes.Add(target, existing);
            }

            if (pending.Height + NoteGap + data.Height > _options.Frame.Height)
                throw Error("Composition.NoteOverflow", "A complete note and reference line cannot fit one page.");
            double fixedBottom = Math.Max(_lines.Where(l => l.Page == target).Select(l => l.Line.Bounds.Top).DefaultIfEmpty(0).Max(), Math.Max(_tableFragments.Where(t => t.Page == target).Select(t => t.Fragment.Bounds.Top).DefaultIfEmpty(0).Max(), _floats.Where(f => f.Page == target).Select(f => f.Value.Bounds.Top).DefaultIfEmpty(0).Max()));
            if (fixedBottom > _options.Frame.Height - ReservedNotes(target) - pending.Height - NoteGap)
            {
                startPage = target + 1;
                startY = 0;
                reservedPage = null;
                forced.Clear();
                continue;
            }

            existing.Add(pending);
            reservedPage = target;
        }

        throw Error("Composition.NoteIterations", "The bounded reference/note pagination solver did not converge.");
    }

    private PendingNote? FindNote(PdfFlowNote note, bool endnote)
    {
        if (endnote)
            return _endnotes.FirstOrDefault(n => ReferenceEquals(n.Note, note));
        foreach (var page in _footnotes.Values)
        {
            var value = page.FirstOrDefault(n => ReferenceEquals(n.Note, note));
            if (value != null)
                return new PendingNote(note, value.Flow, false);
        }

        return null;
    }

    private PendingNote BuildNote(PdfFlowNote note, bool endnote)
    {
        void Check(IReadOnlyList<PdfFlowBlock> blocks)
        {
            using var depth = _context.EnterScope(PdfResourceKind.Depth);
            foreach (var block in blocks)
            {
                _context.Checkpoint();
                if (block is PdfFlowNoteReference)
                    throw Error("Composition.NestedNotes", "A note body cannot recursively contain note references.");
                if (block is PdfFlowComponent component)
                    Check(component.Content);
                if (block is PdfFlowColumns columns)
                    Check(columns.Content);
                if (block is PdfFlowList list)
                    foreach (var item in list.Items)
                        Check(item.Content);
                if (block is PdfTable table)
                    foreach (var cell in table.Grid)
                        Check(cell.Cell.Content);
            }
        }

        Check(note.Content);
        _context.Consume(PdfResourceKind.ExpandedBytes, note.Content.Count * 32L + 256);
        var content = new List<PdfFlowBlock>();
        if (note.Content.Count > 0 && note.Content[0] is PdfFlowParagraph first)
        {
            var o = note.MarkerStyle.TextOptions;
            var marker = new PdfFlowTextStyle(note.MarkerStyle.Fonts, note.MarkerStyle.FontSize, new(first.Runs[0].Style.TextOptions.ParagraphDirection, false, o.Language, o.Features.ToArray()), note.MarkerStyle.Color);
            var firstWithMarker = new PdfFlowParagraph(new[] { new PdfFlowRun(note.Label + " ", marker) }.Concat(first.Runs), first.Style);
            content.Add(first.Hyphenation == null ? firstWithMarker : firstWithMarker.WithHyphenation(first.Hyphenation));
            content.AddRange(note.Content.Skip(1));
        }
        else
        {
            content.Add(new PdfFlowParagraph(note.Label, note.MarkerStyle));
            content.AddRange(note.Content);
        }

        var options = new PdfFlowOptions(new(new(new(0, 0, _options.Frame.Width, _options.Frame.Height))), PdfMargins.Uniform(0), _options.Overflow, _options.MaximumPages, _options.TabInterval, _options.TabStops.ToArray());
        var engine = new PdfFlowEngine(options, _shaper, _context);
        var flow = engine.Layout(content);
        if (!endnote && flow.Pages.Count > 1)
            throw Error("Composition.NoteOverflow", "The explicit footnote profile keeps the complete note on its reference page.");
        _finish.Add(report =>
        {
            engine.Complete(report);
            flow.OperationReport = report;
        });
        return new(note, flow, endnote);
    }

    private void AppendEndnotes()
    {
        if (_endnotes.Count == 0)
            return;
        if (HasContent(_page))
            NewPage();
        foreach (var note in _endnotes)
        {
            for (int i = 0; i < note.Flow.Pages.Count; i++)
            {
                var source = note.Flow.Pages[i];
                if (_y + source.ContentHeight > BodyHeight(_page))
                    NewPage();
                _components.Add((_page, int.MaxValue, _y, source));
                _endnotePlacements.Add((_page, note, _y, source.ContentHeight));
                _y += source.ContentHeight;
                if (i + 1 < note.Flow.Pages.Count)
                    NewPage();
            }
        }
    }

    private PdfFlowNotePlacement[] NotesForPage(int page)
    {
        var output = new List<PdfFlowNotePlacement>();
        double y = _options.Frame.Height - ReservedNotes(page);
        if (_footnotes.TryGetValue(page, out var notes))
            foreach (var note in notes)
            {
                y += NoteGap;
                output.Add(new(note.Note, new(0, y, _options.Frame.Width, note.Height), false, note.References.ToArray()));
                y += note.Height;
            }

        foreach (var note in _endnotePlacements.Where(n => n.Page == page))
            output.Add(new(note.Note.Note, new(0, note.Y, _options.Frame.Width, note.Height), true, note.Note.References.ToArray()));
        return output.ToArray();
    }

    private void PaintNotes(PdfCanvas canvas, int page)
    {
        if (!_footnotes.TryGetValue(page, out var notes))
            return;
        double y = _options.Frame.Height - ReservedNotes(page);
        if (notes.Count > 0)
            canvas.DrawPath(PdfPath.RectangleCore(new(0, y + NoteGap / 2, Math.Min(72, _options.Frame.Width), .5), _context));
        foreach (var note in notes)
        {
            y += NoteGap;
            foreach (var placement in note.Flow.Pages[0].Canvas.Placements)
                canvas.CopyPlacement(placement, new(1, 0, 0, 1, 0, y));
            y += note.Height;
        }
    }
}
