using System.Text;

namespace Fluent.Pdf;
internal sealed class PdfFlowParagraphData
{
    internal sealed class Measure
    {
        internal Measure(PdfShapedText shaped, List<Entry> entries, double width, double advance, double padding, double top, double bottom, double height)
        {
            Shaped = shaped;
            Entries = entries;
            Width = width;
            Advance = advance;
            LeftPadding = padding;
            Top = top;
            Bottom = bottom;
            Height = height;
        }

        internal PdfShapedText Shaped { get; }
        internal List<Entry> Entries { get; }
        internal double Width { get; }
        internal double Advance { get; }
        internal double LeftPadding { get; }
        internal double Top { get; }
        internal double Bottom { get; }
        internal double Height { get; }
    }

    internal readonly struct Entry
    {
        internal Entry(PdfShapedRun? run, PdfFlowTextStyle? style, int tab, int visual)
        {
            Run = run;
            Style = style;
            Tab = tab;
            Visual = visual;
        }

        internal PdfShapedRun? Run { get; }
        internal PdfFlowTextStyle? Style { get; }
        internal int Tab { get; }
        internal int Visual { get; }
    }

    private readonly PdfFlowParagraph _paragraph;
    private readonly PdfFlowOptions _options;
    private readonly IPdfTextShaper _shaper;
    private readonly PdfOperationContext _context;
    private readonly int[] _ends;
    private readonly Dictionary<(int, int), Measure> _cache = new();
    internal PdfFlowParagraphData(PdfFlowParagraph paragraph, PdfFlowOptions options, IPdfTextShaper shaper, PdfOperationContext context)
    {
        _paragraph = paragraph;
        _options = options;
        _shaper = shaper;
        _context = context;
        long length = 0;
        foreach (var run in paragraph.Runs)
        {
            context.Checkpoint();
            length = checked(length + run.Text.Length);
        }

        if (length > int.MaxValue)
            throw Error("Layout.TextLength", "The paragraph exceeds the supported logical text extent.");
        context.Consume(PdfResourceKind.ExpandedBytes, length * 8 + paragraph.Runs.Count * 32L + 128);
        var builder = new StringBuilder((int)length);
        _ends = new int[paragraph.Runs.Count];
        for (int i = 0; i < _ends.Length; i++)
        {
            builder.Append(paragraph.Runs[i].Text);
            _ends[i] = builder.Length;
        }

        Text = builder.ToString();
        var direction = paragraph.Runs[0].Style.TextOptions.ParagraphDirection;
        if (paragraph.Runs.Any(r => r.Style.TextOptions.ParagraphDirection != direction))
            throw Error("Layout.ParagraphDirection", "All run styles must agree on the paragraph base-direction policy.");
        Unicode = PdfUnicode.AnalyzeCore(Text, direction, context);
        context.Consume(PdfResourceKind.ExpandedBytes, Unicode.Graphemes.Count * 64L + 256);
        var boundaries = new HashSet<int>(Unicode.Graphemes.Select(g => g.Utf16Offset))
        {
            Text.Length
        };
        for (int i = 0; i + 1 < _ends.Length; i++)
            if (!boundaries.Contains(_ends[i]))
                throw Error("Layout.StyleGraphemeBoundary", "A style boundary may not split an extended grapheme cluster.");
        Ends = Unicode.Graphemes.Select(g => g.Utf16Offset + g.Utf16Length).ToArray();
        Breaks = new HashSet<int>();
        HardBreaks = new HashSet<int>();
        foreach (var g in Unicode.Graphemes)
        {
            int end = g.Utf16Offset + g.Utf16Length, cp = Unicode.Scalars[g.FirstScalar].Value;
            bool hard = cp == 10 || cp == 13 || cp == 0x85 || cp == 0x2028 || cp == 0x2029;
            if (hard)
            {
                HardBreaks.Add(end);
                Breaks.Add(end);
            }
            else if (cp == 9 || cp == 32 || cp == 0x200b || cp == 0x2d || cp == 0x2010)
                Breaks.Add(end);
            else if (Ideograph(cp) && end < Text.Length && !Closing(Text[end]) && !Opening(cp))
                Breaks.Add(end);
        }

        foreach (int boundary in Breaks.ToArray())
            if (!HardBreaks.Contains(boundary) && boundary < Text.Length && (Glue(Text[boundary]) || boundary > 0 && Glue(Text[boundary - 1])))
                Breaks.Remove(boundary);
        Breaks.Add(Text.Length);
        var full = Get(0, Text.Length);
        Height = full.Height;
        context.Consume(PdfResourceKind.ExpandedBytes, Ends.Length * 16L + 64);
        Advances = new Dictionary<int, double>();
        foreach (var entry in full.Entries)
        {
            if (entry.Run == null)
                continue;
            double scale = entry.Style!.FontSize / entry.Run.Request.Font.Info.Metrics.UnitsPerEm;
            foreach (var g in entry.Run.Glyphs)
            {
                Advances.TryGetValue(g.Cluster, out double v);
                Advances[g.Cluster] = v + g.AdvanceX * scale;
            }
        }
    }

    internal string Text { get; }
    internal PdfUnicodeAnalysis Unicode { get; }
    internal int[] Ends { get; }
    internal HashSet<int> Breaks { get; }
    internal HashSet<int> HardBreaks { get; }
    internal Dictionary<int, double> Advances { get; }
    internal double Height { get; }

    internal PdfFlowTextStyle StyleAt(int offset)
    {
        int low = 0, high = _ends.Length - 1;
        while (low < high)
        {
            int mid = low + (high - low) / 2;
            if (_ends[mid] <= offset)
                low = mid + 1;
            else
                high = mid;
        }

        return _paragraph.Runs[low].Style;
    }

    internal Measure Get(int start, int end)
    {
        if (_cache.TryGetValue((start, end), out var cached))
            return cached;
        var analysis = PdfFlowUnicode.Line(Unicode, start, end - start, _context);
        var firstStyle = StyleAt(start);
        var shaped = PdfText.ShapeCore(analysis.Text, firstStyle.Fonts, _shaper, firstStyle.TextOptions, _context, analysis, p => StyleAt(start + p).Fonts, p => StyleAt(start + p).TextOptions, true);
        _context.Consume(PdfResourceKind.ExpandedBytes, analysis.Scalars.Count * 24L + shaped.Runs.Count * 64L + 256);
        var visual = new Dictionary<int, int>();
        for (int i = 0; i < analysis.VisualScalarOrder.Count; i++)
            visual[analysis.Scalars[analysis.VisualScalarOrder[i]].Utf16Offset] = i;
        var entries = new List<Entry>();
        foreach (var run in shaped.Runs)
        {
            _context.Checkpoint();
            int rank = int.MaxValue;
            foreach (var scalar in analysis.Scalars)
                if (scalar.Utf16Offset >= run.Request.Start && scalar.Utf16Offset < run.Request.Start + run.Request.Length && visual.TryGetValue(scalar.Utf16Offset, out int v))
                    rank = Math.Min(rank, v);
            entries.Add(new(run, StyleAt(start + run.Request.Start), -1, rank));
        }

        foreach (var s in analysis.Scalars)
            if (s.Value == 9)
                entries.Add(new(null, null, s.Utf16Offset, visual[s.Utf16Offset]));
        entries.Sort((a, b) => a.Visual.CompareTo(b.Visual));
        double x = 0, top = 0, bottom = 0, requested = 0, paintedEnd = 0, inkLeft = 0, inkRight = 0;
        foreach (var e in entries)
        {
            if (e.Run == null)
            {
                x = _options.NextTab(x);
                continue;
            }

            var style = e.Style!;
            var m = e.Run.Request.Font.Info.Metrics;
            double scale = style.FontSize / m.UnitsPerEm;
            requested = Math.Max(requested, style.FontSize * _paragraph.Style.LineHeight);
            foreach (var g in e.Run.Glyphs)
            {
                _context.Checkpoint();
                if (g.AdvanceX < 0 || g.AdvanceY != 0)
                    throw Error("Layout.ShaperDirection", "Horizontal flow requires nonnegative horizontal advances and zero vertical advance.");
                top = Math.Max(top, (m.Top + g.OffsetY) * scale + style.BaselineShift);
                bottom = Math.Min(bottom, (m.Bottom + g.OffsetY) * scale + style.BaselineShift);
                char c = analysis.Text[g.Cluster];
                if (!char.IsWhiteSpace(c))
                {
                    inkLeft = Math.Min(inkLeft, x + (m.Left + g.OffsetX) * scale);
                    inkRight = Math.Max(inkRight, x + (m.Right + g.OffsetX) * scale);
                }

                x += g.AdvanceX * scale;
                if (!char.IsWhiteSpace(c))
                    paintedEnd = x;
            }
        }

        if (entries.Count == 0 || requested == 0)
        {
            var m = firstStyle.Fonts.Fonts[0].Info.Metrics;
            double scale = firstStyle.FontSize / m.UnitsPerEm;
            top = Math.Max(0, m.Top * scale + firstStyle.BaselineShift);
            bottom = Math.Min(0, m.Bottom * scale + firstStyle.BaselineShift);
            requested = firstStyle.FontSize * _paragraph.Style.LineHeight;
        }

        double height = Math.Max(requested, top - bottom);
        PdfGraphicsNumbers.Finite(height);
        PdfGraphicsNumbers.Finite(x);
        var result = new Measure(shaped, entries, Math.Max(paintedEnd, inkRight) - inkLeft, paintedEnd, -inkLeft, top, bottom, height);
        _cache.Add((start, end), result);
        return result;
    }

    private PdfException Error(string code, string message) => PdfWriterCore.Error(_context, code, message);
    private static bool Glue(int c) => c == 0x00a0 || c == 0x202f || c == 0x2011 || c == 0x2060 || c == 0xfeff || c == 0x200d;
    private static bool Ideograph(int c) => c >= 0x2e80 && c <= 0xa4cf || c >= 0xac00 && c <= 0xd7af || c >= 0xf900 && c <= 0xfaff || c >= 0x20000 && c <= 0x323af;
    private static bool Closing(int c) => ")]},.!?:;\u3001\u3002\u3009\u300b\u300d\u300f\u3011\uff0c\uff0e\uff01\uff1f".IndexOf((char)c) >= 0;
    private static bool Opening(int c) => "([{\u3008\u300a\u300c\u300e\u3010".IndexOf((char)c) >= 0;
}
