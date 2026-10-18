using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Original bounded geometric scan-layout analysis, with table/form semantics kept distinct from prose-column inference.</summary>
public static class PdfScanLayout
{
    /// <summary>Analyzes immutable words, explicit table/form hints and optional exact Gray8 pixels. Raw order is preserved separately; confidence is declared/heuristic rather than correctness.</summary>
    public static PdfScanLayoutResult Analyze(PdfOcrResult recognition, int rasterWidth, int rasterHeight, PdfScanLayoutOptions? options = null, PdfOcrRaster? raster = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (recognition == null)
            throw new ArgumentNullException(nameof(recognition));
        options ??= new();
        PdfScanLayoutResult? result = null;
        var report = PdfFont.Run("AnalyzeScanLayout", "managed.scan-layout", policy, cancellationToken, c => result = AnalyzeCore(recognition, rasterWidth, rasterHeight, options, raster, c), PdfCapability.RecognizeText);
        result!.OperationReport = report;
        return result;
    }

    internal static PdfScanLayoutResult AnalyzeCore(PdfOcrResult recognition, int width, int height, PdfScanLayoutOptions options, PdfOcrRaster? raster, PdfOperationContext c)
    {
        PdfOcrOutput.Validate(recognition, width, height, c);
        if (recognition.Words.Count > options.MaximumWords)
            throw Error("WordLimit", "The explicit layout word ceiling was exceeded.");
        if (raster != null && (raster.Width != width || raster.Height != height || raster.PixelFormat != PdfOcrPixelFormat.Gray8))
            throw Error("RasterGeometry", "Optional ruling analysis requires exact matching Gray8 raster geometry.");
        c.Consume(PdfResourceKind.ExpandedBytes, recognition.Words.Count * 512L + 16384);
        var assigned = new bool[recognition.Words.Count];
        var regions = new List<PdfScanLayoutRegion>();
        var proseOrder = new List<int>();
        long work = 0;
        foreach (var hint in options.Regions)
        {
            Use(1);
            if (hint.Bounds.Right > width || hint.Bounds.Top > height)
                throw Error("Region", "An explicit semantic region lies outside the processed raster.");
            Assign(hint.Kind, hint.Bounds, hint.Confidence, "caller-declared", true);
        }

        if (raster != null)
            foreach (var box in Grids(raster))
                Assign(PdfScanRegionKind.RuledTableOrForm, box, 0.5, "literal-rulings", false);
        var prose = Enumerable.Range(0, recognition.Words.Count).Where(i => !assigned[i]).ToArray();
        if (prose.Length == 0)
            return Finish();
        var events = new SortedDictionary<double, double>();
        double top = double.MaxValue, bottom = 0;
        foreach (int i in prose)
        {
            Use(1);
            var b = recognition.Words[i].Bounds;
            top = Math.Min(top, b.Y);
            bottom = Math.Max(bottom, b.Top);
            Change(b.X, b.Height);
            Change(b.Right, -b.Height);
        }

        var cuts = new List<double>();
        if (options.InferColumns)
        {
            double coverage = 0, previous = 0;
            double? start = null;
            bool first = true;
            foreach (var item in events)
            {
                Use(1);
                if (!first)
                {
                    if (coverage <= (bottom - top) * options.SpanningHeightFraction)
                        start ??= previous;
                    else if (start.HasValue)
                    {
                        if (previous - start.Value >= options.MinimumColumnGapPixels)
                            cuts.Add((start.Value + previous) / 2);
                        start = null;
                    }
                }

                coverage += item.Value;
                previous = item.Key;
                first = false;
            }

            if (start.HasValue && previous - start.Value >= options.MinimumColumnGapPixels)
                cuts.Add((start.Value + previous) / 2);
            if (cuts.Count + 1 > options.MaximumColumns)
                throw Error("ColumnLimit", "The explicit inferred column ceiling was exceeded.");
        }

        c.Consume(PdfResourceKind.ExpandedBytes, (cuts.Count + 1L) * 128 + prose.Length * 32L);
        var columns = Enumerable.Range(0, cuts.Count + 1).Select(_ => new List<int>()).ToArray();
        var spanning = new List<int>();
        foreach (int i in prose)
        {
            Use(cuts.Count + 1L);
            var b = recognition.Words[i].Bounds;
            int left = cuts.Count(x => b.X >= x), right = cuts.Count(x => b.Right > x);
            if (left != right)
                spanning.Add(i);
            else
                columns[left].Add(i);
        }

        if (cuts.Count != 0 && columns.Any(col => col.Count < options.MinimumWordsPerColumn))
        {
            cuts.Clear();
            columns = new[]
            {
                prose.ToList()
            };
            spanning.Clear();
            c.Report(new("Ocr.LayoutColumnsAmbiguous", "Proposed gutters did not meet the explicit minimum column population; prose remains in one geometric column.", severity: PdfDiagnosticSeverity.Information));
        }

        double confidence = cuts.Count == 0 ? 0 : Math.Min(1, options.MinimumColumnGapPixels / Math.Max(1, recognition.Words.Where((_, i) => !assigned[i]).Average(w => w.Bounds.Height)));
        for (int n = 0; n < columns.Length; n++)
            if (columns[n].Count != 0)
                regions.Add(new(PdfScanRegionKind.Prose, Bounds(columns[n]), columns[n].ToArray(), n, confidence, "whitespace"));
        if (spanning.Count != 0)
            regions.Add(new(PdfScanRegionKind.Prose, Bounds(spanning), spanning.ToArray(), null, confidence, "whitespace"));
        var columnSequence = Enumerable.Range(0, columns.Length).ToArray();
        if (options.RightToLeftColumns)
            Array.Reverse(columnSequence);
        double begin = double.NegativeInfinity;
        foreach (var line in Lines(spanning))
        {
            double center = line.Average(i => Center(i));
            Emit(begin, center);
            proseOrder.AddRange(line);
            begin = center;
        }

        Emit(begin, double.PositiveInfinity);
        return Finish();
        void Emit(double first, double last)
        {
            foreach (int column in columnSequence)
            {
                var indices = new List<int>();
                foreach (int i in columns[column])
                {
                    Use(1);
                    double y = Center(i);
                    if (y >= first && y < last)
                        indices.Add(i);
                }

                foreach (var line in Lines(indices))
                    proseOrder.AddRange(line);
            }
        }

        List<List<int>> Lines(IReadOnlyList<int> indices)
        {
            c.Consume(PdfResourceKind.ExpandedBytes, indices.Count * 64L + 256);
            var sorted = indices.OrderBy(i => Center(i)).ThenBy(i => recognition.Words[i].Bounds.X).ToArray();
            var lines = new List<List<int>>();
            foreach (int i in sorted)
            {
                Use(1);
                if (lines.Count == 0 || Math.Abs(Center(lines[lines.Count - 1][0]) - Center(i)) > options.LineTolerancePixels)
                    lines.Add(new());
                lines[lines.Count - 1].Add(i);
            }

            foreach (var line in lines)
            {
                Use(line.Count);
                line.Sort((a, b) => (options.RightToLeftWords ? -1 : 1) * recognition.Words[a].Bounds.X.CompareTo(recognition.Words[b].Bounds.X));
            }

            return lines;
        }

        double Center(int index)
        {
            var b = recognition.Words[index].Bounds;
            return b.Y + b.Height / 2;
        }

        void Change(double x, double amount)
        {
            events.TryGetValue(x, out double value);
            events[x] = value + amount;
        }

        PdfRect Bounds(IReadOnlyList<int> indices)
        {
            double x = double.MaxValue, y = double.MaxValue, r = 0, b = 0;
            foreach (int i in indices)
            {
                Use(1);
                var box = recognition.Words[i].Bounds;
                x = Math.Min(x, box.X);
                y = Math.Min(y, box.Y);
                r = Math.Max(r, box.Right);
                b = Math.Max(b, box.Top);
            }

            return new(x, y, r - x, b - y);
        }

        void Assign(PdfScanRegionKind kind, PdfRect box, double confidenceValue, string provenance, bool strict)
        {
            var indices = new List<int>();
            for (int i = 0; i < recognition.Words.Count; i++)
            {
                Use(1);
                if (assigned[i])
                    continue;
                var b = recognition.Words[i].Bounds;
                bool intersects = Math.Min(box.Right, b.Right) > Math.Max(box.X, b.X) && Math.Min(box.Top, b.Top) > Math.Max(box.Y, b.Y);
                if (!intersects)
                    continue;
                if (!PdfPageBoxes.Contains(box, b))
                {
                    if (strict)
                        throw Error("RegionCrossing", "A recognized word crosses an explicit semantic region boundary.");
                    continue;
                }

                assigned[i] = true;
                indices.Add(i);
            }

            if (indices.Count != 0)
                regions.Add(new(kind, box, indices.ToArray(), null, confidenceValue, provenance));
        }

        PdfScanLayoutResult Finish()
        {
            c.Consume(PdfResourceKind.Objects, regions.Count + 1L);
            c.Consume(PdfResourceKind.ExpandedBytes, recognition.Words.Count * 16L + regions.Count * 128L + 512);
            var nonprose = Enumerable.Range(0, assigned.Length).Where(i => assigned[i]).ToArray();
            return new(recognition, regions.ToArray(), proseOrder.ToArray(), nonprose, work);
        }

        void Use(long count)
        {
            c.Checkpoint();
            if (count > options.MaximumAnalysisWork - work)
                throw Error("WorkLimit", "The explicit scan-layout analysis work ceiling was exceeded.");
            work += count;
        }

        PdfException Error(string code, string message) => PdfWriterCore.Error(c, "Ocr.Layout" + code, message);
        IEnumerable<PdfRect> Grids(PdfOcrRaster image)
        {
            Use((long)width * height * 2);
            c.Consume(PdfResourceKind.ExpandedBytes, width * 8L + image.Stride + 1048576);
            var vertical = new int[width];
            var row = new byte[image.Stride];
            var horizontal = new Dictionary<(int Start, int End), List<int>>();
            var upright = new Dictionary<(int Start, int End), List<int>>();
            int rules = 0;
            using (var input = image.OpenPixels(c))
            {
                for (int y = 0; y < height; y++)
                {
                    c.Checkpoint();
                    int at = 0;
                    while (at < row.Length)
                    {
                        int read = input.Read(row, at, row.Length - at);
                        if (read == 0)
                            throw Error("RasterLength", "Ruling raster ended before its exact extent.");
                        at += read;
                    }

                    int start = -1;
                    for (int x = 0; x <= width; x++)
                    {
                        if ((x & 1023) == 0)
                            c.Checkpoint();
                        bool dark = x < width && row[x] < 128;
                        if (dark && start < 0)
                            start = x;
                        else if (!dark && start >= 0)
                        {
                            Rule(horizontal, start, x, y);
                            start = -1;
                        }

                        if (x == width)
                            continue;
                        if (dark)
                            vertical[x]++;
                        else if (vertical[x] != 0)
                        {
                            Rule(upright, y - vertical[x], y, x);
                            vertical[x] = 0;
                        }
                    }
                }

                if (input.ReadByte() != -1)
                    throw Error("RasterLength", "Ruling raster exceeds its exact extent.");
            }

            for (int x = 0; x < width; x++)
                if (vertical[x] != 0)
                    Rule(upright, height - vertical[x], height, x);
            c.Consume(PdfResourceKind.ExpandedBytes, rules * 128L + 1024);
            var horizontalBands = horizontal.Select(p => (p.Key, Lines: Bands(p.Value))).Where(p => p.Lines.Count >= 3).ToArray();
            var verticalBands = upright.Select(p => (p.Key, Lines: Bands(p.Value))).Where(p => p.Lines.Count >= 3).ToArray();
            int found = 0;
            foreach (var h in horizontalBands)
                foreach (var v in verticalBands)
                {
                    Use(1);
                    var ys = h.Lines;
                    var xs = v.Lines;
                    if (ys.Count < 3 || xs.Count < 3 || ys[0] != v.Key.Start || ys[ys.Count - 1] != v.Key.End - 1 || xs[0] != h.Key.Start || xs[xs.Count - 1] != h.Key.End - 1)
                        continue;
                    if (++found > 64)
                        throw Error("RegionLimit", "The finite detected-grid region ceiling was exceeded.");
                    yield return new(h.Key.Start, v.Key.Start, h.Key.End - h.Key.Start, v.Key.End - v.Key.Start);
                }

            void Rule(Dictionary<(int Start, int End), List<int>> collection, int start, int end, int position)
            {
                if (end - start < options.MinimumRuleLengthPixels)
                    return;
                if (++rules > 4096)
                    throw Error("RuleLimit", "The finite literal-ruling inventory was exceeded.");
                if (!collection.TryGetValue((start, end), out var list))
                    collection.Add((start, end), list = new());
                list.Add(position);
            }

            List<int> Bands(List<int> values)
            {
                Use(values.Count);
                c.Consume(PdfResourceKind.ExpandedBytes, values.Count * 24L + 256);
                var sorted = values.OrderBy(x => x).ToArray();
                var result = new List<int>();
                int previous = -2;
                foreach (int value in sorted)
                {
                    if (result.Count == 0 || value > previous + 1)
                        result.Add(value);
                    previous = value;
                }

                return result;
            }
        }
    }
}
