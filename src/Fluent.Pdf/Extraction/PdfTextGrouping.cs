using System.Text;

namespace Fluent.Pdf;
// Geometric grouping is explicitly an inference; source order and ActualText remain independently available.
internal sealed class PdfTextGrouping
{
    private readonly PdfTextPageData _page;
    private readonly PdfTextExtractionOptions _options;
    private readonly PdfOperationContext _context;
    internal PdfTextGrouping(PdfTextPageData page, PdfTextExtractionOptions options, PdfOperationContext context)
    {
        _page = page;
        _options = options;
        _context = context;
    }

    private sealed class Line
    {
        internal readonly List<PdfExtractedWord> Words = new();
        internal readonly int Angle;
        internal readonly double Position;
        internal PdfRect Bounds;
        internal Line(int angle, double position, PdfRect bounds)
        {
            Angle = angle;
            Position = position;
            Bounds = bounds;
        }
    }

    internal void Build()
    {
        var raw = new StringBuilder();
        var semantic = new StringBuilder();
        var word = new List<PdfExtractedCharacter>();
        PdfExtractedCharacter? previous = null;
        int spanAt = 0, replacedUntil = -1;
        for (int i = 0; i <= _page.Characters.Count; i++)
        {
            _context.Checkpoint();
            while (spanAt < _page.Spans.Count && _page.Spans[spanAt].FirstCharacter == i)
            {
                var span = _page.Spans[spanAt++];
                Append(semantic, span.Text);
                replacedUntil = i + span.CharacterCount;
            }

            if (i == _page.Characters.Count)
                break;
            var character = _page.Characters[i];
            Append(raw, character.Text);
            bool separator = previous != null && Break(previous, character);
            if (i >= replacedUntil)
            {
                if (separator && semantic.Length > 0 && !char.IsWhiteSpace(semantic[semantic.Length - 1]) && !character.Text.All(char.IsWhiteSpace))
                    Append(semantic, SameLine(previous!, character) ? " " : "\n");
                Append(semantic, character.Text);
            }

            if (character.Text.Length == 0 || character.Text.All(char.IsWhiteSpace))
                Flush();
            else
            {
                if (character.Text.Any(char.IsWhiteSpace))
                    _context.Report(new("Extraction.SharedClusterWordGeometry", "A source code maps across a word boundary; separate word geometry is unavailable.", lossCategory: PdfLossCategory.FidelityReduction));
                if (separator)
                    Flush();
                _context.Consume(PdfResourceKind.ExpandedBytes, 32);
                word.Add(character);
            }

            previous = character;
        }

        Flush();
        _context.Consume(PdfResourceKind.ExpandedBytes, (raw.Length + (long)semantic.Length) * 2 + 64);
        _page.Raw = raw.ToString();
        _page.Text = semantic.ToString();
        var buckets = new Dictionary<(int, long), Line>();
        var lines = new List<Line>();
        foreach (var entry in _page.Words)
        {
            _context.Checkpoint();
            var first = entry.Characters[0];
            var last = entry.Characters[entry.Characters.Count - 1];
            double dx = last.BaselineEnd.X - first.BaselineStart.X, dy = last.BaselineEnd.Y - first.BaselineStart.Y;
            double angle = Math.Atan2(dy, dx);
            if (angle < 0)
                angle += Math.PI;
            if (angle >= Math.PI)
                angle -= Math.PI;
            int axis = (int)Math.Round(angle * 180 / Math.PI) % 180;
            double radians = axis * Math.PI / 180;
            double position = -Math.Sin(radians) * first.BaselineStart.X + Math.Cos(radians) * first.BaselineStart.Y;
            double scaled = Math.Round(position / _options.LineTolerance);
            if (scaled < long.MinValue + 2 || scaled > long.MaxValue - 2)
                throw PdfWriterCore.Error(_context, "Extraction.GroupingRange", "The baseline exceeds the finite grouping range.");
            long bucket = (long)scaled;
            Line? target = null;
            for (int near = -1; near <= 1; near++)
                if (buckets.TryGetValue((axis, bucket + near), out var candidate) && Math.Abs(candidate.Position - position) <= _options.LineTolerance)
                {
                    target = candidate;
                    break;
                }

            if (target == null)
            {
                _context.Consume(PdfResourceKind.ExpandedBytes, 256);
                target = new(axis, position, entry.Bounds);
                buckets[(axis, bucket)] = target;
                lines.Add(target);
            }

            _context.Consume(PdfResourceKind.ExpandedBytes, 32);
            target.Words.Add(entry);
            target.Bounds = PdfTextExtractionEngine.Union(target.Bounds, entry.Bounds);
        }

        _context.Consume(PdfResourceKind.ExpandedBytes, lines.Count * 96L + 64);
        var ordered = lines.OrderBy(x => x.Bounds.Y).ThenBy(x => x.Bounds.X).ThenBy(x => x.Angle).ToArray();
        var layout = new StringBuilder();
        List<PdfExtractedLine>? paragraph = null;
        PdfRect paragraphBounds = default;
        PdfExtractedLine? prior = null;
        foreach (var line in ordered)
        {
            _context.Checkpoint();
            double angle = line.Angle * Math.PI / 180;
            _context.Consume(PdfResourceKind.ExpandedBytes, line.Words.Count * 64L + 128);
            var sorted = line.Words.OrderBy(w => Math.Cos(angle) * w.Characters[0].BaselineStart.X + Math.Sin(angle) * w.Characters[0].BaselineStart.Y).ThenBy(w => w.Index).ToList();
            var current = new PdfExtractedLine(sorted, line.Bounds);
            _page.Lines.Add(current);
            if (layout.Length > 0)
                Append(layout, "\n");
            PdfExtractedWord? last = null;
            foreach (var item in sorted)
            {
                if (last != null)
                {
                    double em = Math.Max(1, item.Characters[0].FontSize), gap = item.Bounds.X - last.Bounds.Right;
                    int spaces = (int)Math.Max(1, Math.Min(1000, Math.Round(gap / (em * .5))));
                    _context.Consume(PdfResourceKind.ExpandedBytes, spaces * 4L + 32);
                    layout.Append(' ', spaces);
                }

                Append(layout, item.Text);
                last = item;
            }

            double height = current.Words.Count == 0 ? 1 : Math.Max(1, current.Words[0].Characters[0].FontSize);
            bool continues = prior != null && Math.Abs(current.Bounds.Y - prior.Bounds.Top) <= height * 1.5 && Math.Abs(current.Bounds.X - prior.Bounds.X) <= height * 2;
            if (!continues && paragraph != null)
            {
                _page.Paragraphs.Add(new(paragraph, paragraphBounds));
                paragraph = null;
            }

            if (paragraph == null)
            {
                _context.Consume(PdfResourceKind.ExpandedBytes, 128);
                paragraph = new();
                paragraphBounds = current.Bounds;
            }

            _context.Consume(PdfResourceKind.ExpandedBytes, 32);
            paragraph.Add(current);
            paragraphBounds = PdfTextExtractionEngine.Union(paragraphBounds, current.Bounds);
            prior = current;
        }

        if (paragraph != null)
            _page.Paragraphs.Add(new(paragraph, paragraphBounds));
        _context.Consume(PdfResourceKind.ExpandedBytes, layout.Length * 2L + 32);
        _page.Layout = layout.ToString();
        void Flush()
        {
            if (word.Count == 0)
                return;
            _context.Consume(PdfResourceKind.Objects, 1);
            _context.Consume(PdfResourceKind.ExpandedBytes, word.Count * 32L + 128);
            var text = new StringBuilder();
            var bounds = word[0].Quad.Bounds;
            foreach (var character in word)
            {
                Append(text, character.Text);
                bounds = PdfTextExtractionEngine.Union(bounds, character.Quad.Bounds);
            }

            _context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 2L + 32);
            _page.Words.Add(new(_page.Words.Count, word, text.ToString(), bounds));
            word = new();
        }
    }

    private bool SameLine(PdfExtractedCharacter a, PdfExtractedCharacter b)
    {
        var dx = a.BaselineEnd.X - a.BaselineStart.X;
        var dy = a.BaselineEnd.Y - a.BaselineStart.Y;
        double length = PdfTextExtractionEngine.Distance(a.BaselineStart, a.BaselineEnd);
        if (length == 0)
            return PdfTextExtractionEngine.Distance(a.BaselineStart, b.BaselineStart) <= _options.LineTolerance;
        return Math.Abs((b.BaselineStart.X - a.BaselineStart.X) * (dy / length) - (b.BaselineStart.Y - a.BaselineStart.Y) * (dx / length)) <= _options.LineTolerance;
    }

    private bool Break(PdfExtractedCharacter a, PdfExtractedCharacter b)
    {
        if (!SameLine(a, b))
            return true;
        double dx = a.BaselineEnd.X - a.BaselineStart.X, dy = a.BaselineEnd.Y - a.BaselineStart.Y, length = PdfTextExtractionEngine.Distance(a.BaselineStart, a.BaselineEnd);
        if (length == 0)
            return false;
        double gap = (b.BaselineStart.X - a.BaselineEnd.X) * (dx / length) + (b.BaselineStart.Y - a.BaselineEnd.Y) * (dy / length);
        return gap > Math.Max(a.FontSize, b.FontSize) * _options.WordGapRatio;
    }

    private void Append(StringBuilder builder, string text)
    {
        _context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 4L + 32);
        builder.Append(text);
    }
}
