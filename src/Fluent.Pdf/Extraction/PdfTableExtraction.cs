using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit bounded geometric table inference, separate from semantic structure and production reconstruction qualification.</summary>
public static class PdfTableExtraction
{
    /// <summary>Infers one candidate per selected page/region using an explicit ruled/borderless profile. Page indices must be unique and ascending; each original page/table/header remains observable.</summary>
    public static PdfTableExtractionResult Read(PdfDocumentSnapshot source, IReadOnlyList<int> pages, PdfTableExtractionOptions options, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (pages == null)
            throw new ArgumentNullException(nameof(pages));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        PdfDocumentSnapshot captured = null!;
        List<PdfExtractedTable>? tables = null;
        List<PdfExtractedTableSequence>? sequences = null;
        var report = PdfFont.Run("ExtractTables", "managed.tables.extraction", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            int selectionCount = pages.Count;
            c.Consume(PdfResourceKind.ExpandedBytes, selectionCount * 8L + 2048);
            if (selectionCount == 0 || selectionCount > source.Pages.Count)
                throw new ArgumentException("A finite nonempty page selection is required.", nameof(pages));
            var selected = new int[selectionCount];
            int previous = -1;
            for (int i = 0; i < selected.Length; i++)
            {
                c.Checkpoint();
                int page = pages[i];
                if (page <= previous || page >= source.Pages.Count)
                    throw new ArgumentException("Select unique ascending valid page indices.", nameof(pages));
                selected[i] = previous = page;
            }

            using var graph = new PdfGraphEdit(source, new(), c);
            captured = graph.Snapshot;
            tables = new();
            foreach (int page in selected)
            {
                c.Checkpoint();
                var data = new PdfTextExtractionEngine(graph, new(), providers ?? PdfProviderRegistry.Empty).Read(page);
                var table = options.Mode == PdfTableDetectionMode.Ruled ? Ruled(graph, data, options, providers ?? PdfProviderRegistry.Empty) : Borderless(graph, data, options);
                if (table == null)
                    c.Report(new("Tables.NoCandidate", "No table satisfies the explicit inference profile in this selected page/region.", severity: PdfDiagnosticSeverity.Information, pageIndex: page));
                else
                {
                    if (options.HeaderRows > table.RowCount)
                        throw graph.Error("Tables.HeaderRows", "The explicit header count exceeds inferred rows.");
                    Tick(c);
                    tables.Add(table);
                }
            }

            sequences = Sequences(tables, options, c);
            c.Consume(PdfResourceKind.ExpandedBytes, 256);
            c.Consume(PdfResourceKind.Objects, 4);
        }, PdfCapability.ReadDocument);
        return new(captured, tables!.AsReadOnly(), sequences!.AsReadOnly(), report);
    }

    internal static PdfExtractedTable? Detect(PdfGraphEdit graph, PdfTextPageData data, PdfTableExtractionOptions options, PdfProviderRegistry providers)
        => options.Mode == PdfTableDetectionMode.Ruled ? Ruled(graph, data, options, providers) : Borderless(graph, data, options);

    private sealed class Segment
    {
        internal bool Vertical;
        internal double Position, Start, End;
        internal PdfResourceSource Source = null!;
    }

    private static PdfExtractedTable? Ruled(PdfGraphEdit graph, PdfTextPageData data, PdfTableExtractionOptions options, PdfProviderRegistry providers)
    {
        var c = graph.Context;
        Tick(c);
        var segments = new List<Segment>();
        var display = data.Geometry.UserToDisplay();
        foreach (var path in PdfResourceExtraction.ReadPathsCore(graph, data.Page, providers))
        {
            c.Checkpoint();
            var paint = path.EndOperation.Operation;
            if (paint != PdfOperator.Stroke && paint != PdfOperator.CloseStroke && paint != PdfOperator.FillStroke && paint != PdfOperator.FillStrokeEvenOdd && paint != PdfOperator.CloseFillStroke && paint != PdfOperator.CloseFillStrokeEvenOdd)
                continue;
            PdfPoint? previous = null, first = null;
            foreach (var element in path.Elements)
            {
                Tick(c);
                var op = element.Operation;
                var values = op.Operands;
                var matrix = display.Multiply(element.Transform);
                PdfPoint Point(int at) => matrix.Transform(new(values[at].NumberValue, values[at + 1].NumberValue));
                if (op.Operation == PdfOperator.MoveTo)
                {
                    first = previous = Point(0);
                }
                else if (op.Operation == PdfOperator.LineTo)
                {
                    var end = Point(0);
                    if (previous.HasValue)
                        Add(previous.Value, end, element.Source);
                    previous = end;
                }
                else if (op.Operation == PdfOperator.ClosePath)
                {
                    if (previous.HasValue && first.HasValue)
                        Add(previous.Value, first.Value, element.Source);
                    previous = first;
                }
                else if (op.Operation == PdfOperator.Rectangle)
                {
                    double x = values[0].NumberValue, y = values[1].NumberValue, w = values[2].NumberValue, h = values[3].NumberValue;
                    var a = matrix.Transform(new(x, y));
                    var b = matrix.Transform(new(x + w, y));
                    var d = matrix.Transform(new(x, y + h));
                    var e = matrix.Transform(new(x + w, y + h));
                    Add(a, b, element.Source);
                    Add(b, e, element.Source);
                    Add(e, d, element.Source);
                    Add(d, a, element.Source);
                    first = previous = a;
                }
                else if (values.Count >= 2)
                    previous = Point(values.Count - 2);
            }

            if ((paint == PdfOperator.CloseStroke || paint == PdfOperator.CloseFillStroke || paint == PdfOperator.CloseFillStrokeEvenOdd) && previous.HasValue && first.HasValue)
                Add(previous.Value, first.Value, path.Source);
        }

        var xs = Unique(segments.Where(s => s.Vertical).Select(s => s.Position), options.Tolerance, c);
        var ys = Unique(segments.Where(s => !s.Vertical).Select(s => s.Position), options.Tolerance, c);
        if (xs.Count < options.MinimumColumns + 1 || ys.Count < options.MinimumRows + 1)
            return null;
        int rows = ys.Count - 1, columns = xs.Count - 1;
        long count = (long)rows * columns;
        CheckCount(count, options, graph);
        c.Consume(PdfResourceKind.ExpandedBytes, segments.Count * 32L + 64);
        var orderedSegments = segments.OrderBy(s => s.Start).ToArray();
        if (Cover(true, xs[0], ys[0], ys[rows]) != 1 || Cover(true, xs[columns], ys[0], ys[rows]) != 1 || Cover(false, ys[0], xs[0], xs[columns]) != 1 || Cover(false, ys[rows], xs[0], xs[columns]) != 1)
            throw graph.Error("Tables.RuleGridProfile", "The literal rules do not form one closed candidate; select an explicit region/profile for unrelated or incomplete rules.");
        c.Consume(PdfResourceKind.ExpandedBytes, count * 64 + 1024);
        var parent = new int[(int)count];
        for (int i = 0; i < parent.Length; i++)
            parent[i] = i;
        int Root(int value)
        {
            while (parent[value] != value)
            {
                c.Checkpoint();
                value = parent[value] = parent[parent[value]];
            }

            return value;
        }

        void Join(int a, int b) => parent[Root(a)] = Root(b);
        for (int row = 0; row < rows; row++)
            for (int col = 0; col < columns; col++)
            {
                Tick(c);
                if (col + 1 < columns)
                {
                    int coverage = Cover(true, xs[col + 1], ys[row], ys[row + 1]);
                    if (coverage == 2)
                        throw graph.Error("Tables.PartialRuleProfile", "An interior rule only partially covers a primitive cell edge.");
                    if (coverage == 0)
                        Join(row * columns + col, row * columns + col + 1);
                }

                if (row + 1 < rows)
                {
                    int coverage = Cover(false, ys[row + 1], xs[col], xs[col + 1]);
                    if (coverage == 2)
                        throw graph.Error("Tables.PartialRuleProfile", "An interior rule only partially covers a primitive cell edge.");
                    if (coverage == 0)
                        Join(row * columns + col, (row + 1) * columns + col);
                }
            }

        var groups = new Dictionary<int, List<int>>();
        for (int i = 0; i < parent.Length; i++)
        {
            Tick(c);
            int root = Root(i);
            if (!groups.TryGetValue(root, out var group))
            {
                Tick(c);
                groups.Add(root, group = new());
            }

            group.Add(i);
        }

        var buckets = BucketCharacters(data, xs, ys, c);
        var cells = new List<PdfExtractedTableCell>();
        c.Consume(PdfResourceKind.ExpandedBytes, segments.Count * 16L + 64);
        var rules = new ReadOnlyCollection<PdfResourceSource>(segments.Select(s => s.Source).Distinct().ToArray());
        foreach (var group in groups.Values)
        {
            Tick(c);
            int r0 = group.Min(i => i / columns), r1 = group.Max(i => i / columns), c0 = group.Min(i => i % columns), c1 = group.Max(i => i % columns);
            if (group.Count != (r1 - r0 + 1) * (c1 - c0 + 1))
                throw graph.Error("Tables.MergeShapeProfile", "Missing rules create a nonrectangular merged region; no rectangular cell is invented.");
            cells.Add(Cell(graph, data, options, r0, c0, r1 - r0 + 1, c1 - c0 + 1, new(xs[c0], ys[r0], xs[c1 + 1] - xs[c0], ys[r1 + 1] - ys[r0]), rules, .95, group.SelectMany(i => buckets.TryGetValue(i, out var items) ? items : Enumerable.Empty<PdfExtractedCharacter>())));
        }

        return Table(data.Page, xs, ys, cells, options.Mode, .95, c);
        void Add(PdfPoint a, PdfPoint b, PdfResourceSource source)
        {
            c.Checkpoint();
            if (options.Region.HasValue && (!Inside(options.Region.Value, a) || !Inside(options.Region.Value, b)))
                return;
            bool vertical = Math.Abs(a.X - b.X) <= options.Tolerance, horizontal = Math.Abs(a.Y - b.Y) <= options.Tolerance;
            if (vertical == horizontal)
                return;
            Tick(c);
            segments.Add(new() { Vertical = vertical, Position = vertical ? (a.X + b.X) / 2 : (a.Y + b.Y) / 2, Start = vertical ? Math.Min(a.Y, b.Y) : Math.Min(a.X, b.X), End = vertical ? Math.Max(a.Y, b.Y) : Math.Max(a.X, b.X), Source = source });
        }

        int Cover(bool vertical, double position, double start, double end)
        {
            c.Checkpoint();
            double covered = start;
            bool found = false;
            foreach (var segment in orderedSegments)
            {
                c.Checkpoint();
                if (segment.Vertical != vertical || Math.Abs(segment.Position - position) > options.Tolerance || segment.End <= start + options.Tolerance * .01 || segment.Start >= end - options.Tolerance * .01)
                    continue;
                found = true;
                if (segment.Start > covered + options.Tolerance)
                    return 2;
                covered = Math.Max(covered, segment.End);
                if (covered >= end - options.Tolerance)
                    return 1;
            }
            return found ? 2 : 0;
        }
    }

    private static PdfExtractedTable? Borderless(PdfGraphEdit graph, PdfTextPageData data, PdfTableExtractionOptions options)
    {
        var c = graph.Context;
        Tick(c);
        c.Consume(PdfResourceKind.ExpandedBytes, data.Lines.Count * 64L + data.Words.Count * 64L + 128);
        var rows = new List<List<List<PdfExtractedWord>>>();
        foreach (var line in data.Lines.OrderBy(l => l.Bounds.Y))
        {
            Tick(c);
            var cells = new List<List<PdfExtractedWord>>();
            foreach (var word in line.Words.OrderBy(w => w.Bounds.X))
            {
                Tick(c);
                var bounds = word.Bounds;
                if (options.Region.HasValue && !Inside(options.Region.Value, new(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2)))
                    continue;
                if (word.Characters.Any(ch => Math.Abs(ch.BaselineStart.Y - ch.BaselineEnd.Y) > options.Tolerance))
                    throw graph.Error("Tables.TextDirectionProfile", "This borderless profile requires horizontal displayed text.");
                if (cells.Count == 0 || bounds.X - (cells[cells.Count - 1][cells[cells.Count - 1].Count - 1].Bounds.X + cells[cells.Count - 1][cells[cells.Count - 1].Count - 1].Bounds.Width) >= options.MinimumColumnGap)
                {
                    Tick(c);
                    cells.Add(new());
                }

                cells[cells.Count - 1].Add(word);
            }

            if (cells.Count != 0)
                rows.Add(cells);
        }

        if (rows.Count < options.MinimumRows)
            return null;
        int columns = rows[0].Count;
        if (columns < options.MinimumColumns)
            return null;
        CheckCount((long)rows.Count * columns, options, graph);
        if (rows.Any(r => r.Count != columns))
            throw graph.Error("Tables.AlignmentProfile", "Borderless rows have different gutter-defined column counts; select an explicit region/profile.");
        c.Consume(PdfResourceKind.ExpandedBytes, (rows.Count + columns) * 64L + 1024);
        var xs = new List<double>();
        var ys = new List<double>();
        for (int col = 0; col < columns; col++)
        {
            Tick(c);
            double left = rows.Min(r => r[col][0].Bounds.X), right = rows.Max(r => r[col][r[col].Count - 1].Bounds.X + r[col][r[col].Count - 1].Bounds.Width);
            if (rows.Max(r => r[col][0].Bounds.X) - left > options.Tolerance)
                throw graph.Error("Tables.AlignmentProfile", "Borderless column starts exceed the explicit alignment tolerance.");
            if (col == 0)
                xs.Add(left);
            if (col + 1 < columns)
            {
                double next = rows.Min(r => r[col + 1][0].Bounds.X);
                if (next - right < options.MinimumColumnGap)
                    throw graph.Error("Tables.GutterProfile", "A consistent empty gutter does not separate the inferred columns.");
                xs.Add((right + next) / 2);
            }
            else
                xs.Add(right);
        }

        for (int row = 0; row < rows.Count; row++)
        {
            Tick(c);
            double top = rows[row].SelectMany(x => x).Min(w => w.Bounds.Y), bottom = rows[row].SelectMany(x => x).Max(w => w.Bounds.Y + w.Bounds.Height);
            if (row == 0)
                ys.Add(top);
            if (row + 1 < rows.Count)
            {
                double next = rows[row + 1].SelectMany(x => x).Min(w => w.Bounds.Y);
                if (next < bottom - options.Tolerance)
                    throw graph.Error("Tables.RowOverlapProfile", "Inferred borderless rows overlap.");
                ys.Add((bottom + next) / 2);
            }
            else
                ys.Add(bottom);
        }

        var buckets = BucketCharacters(data, xs, ys, c);
        var extracted = new List<PdfExtractedTableCell>();
        for (int row = 0; row < rows.Count; row++)
            for (int col = 0; col < columns; col++)
            {
                Tick(c);
                extracted.Add(Cell(graph, data, options, row, col, 1, 1, new(xs[col], ys[row], xs[col + 1] - xs[col], ys[row + 1] - ys[row]), Array.Empty<PdfResourceSource>(), .65, buckets.TryGetValue(row * columns + col, out var items) ? items : Enumerable.Empty<PdfExtractedCharacter>()));
            }

        return Table(data.Page, xs, ys, extracted, options.Mode, .65, c);
    }

    private static Dictionary<int, List<PdfExtractedCharacter>> BucketCharacters(PdfTextPageData data, IReadOnlyList<double> xs, IReadOnlyList<double> ys, PdfOperationContext c)
    {
        var buckets = new Dictionary<int, List<PdfExtractedCharacter>>();
        foreach (var character in data.Characters)
        {
            c.Checkpoint();
            var box = character.Quad.Bounds;
            int col = Interval(xs, box.X + box.Width / 2), row = Interval(ys, box.Y + box.Height / 2);
            if (col < 0 || row < 0) continue;
            int key = row * (xs.Count - 1) + col;
            if (!buckets.TryGetValue(key, out var items))
            {
                c.Consume(PdfResourceKind.ExpandedBytes, 128);
                buckets.Add(key, items = new());
            }
            c.Consume(PdfResourceKind.ExpandedBytes, 32);
            items.Add(character);
        }
        return buckets;
        static int Interval(IReadOnlyList<double> edges, double point)
        {
            if (point < edges[0] || point >= edges[edges.Count - 1]) return -1;
            int low = 0, high = edges.Count - 1;
            while (high - low > 1)
            {
                int mid = low + (high - low) / 2;
                if (point < edges[mid]) high = mid; else low = mid;
            }
            return low;
        }
    }

    private static PdfExtractedTableCell Cell(PdfGraphEdit graph, PdfTextPageData data, PdfTableExtractionOptions options, int row, int col, int rowSpan, int colSpan, PdfRect bounds, IReadOnlyList<PdfResourceSource> rules, double confidence, IEnumerable<PdfExtractedCharacter> candidates)
    {
        var c = graph.Context;
        Tick(c);
        var chars = new List<PdfExtractedCharacter>();
        foreach (var character in candidates)
        {
            c.Checkpoint();
            var box = character.Quad.Bounds;
            var center = new PdfPoint(box.X + box.Width / 2, box.Y + box.Height / 2);
            if (center.X < bounds.X || center.Y < bounds.Y || center.X >= bounds.X + bounds.Width || center.Y >= bounds.Y + bounds.Height)
                continue;
            if (Math.Abs(character.BaselineStart.Y - character.BaselineEnd.Y) > options.Tolerance || character.BaselineEnd.X < character.BaselineStart.X)
                throw graph.Error("Tables.TextDirectionProfile", "This geometric cell text profile requires horizontal forward displayed text.");
            if (box.X < bounds.X - options.Tolerance || box.Y < bounds.Y - options.Tolerance || box.X + box.Width > bounds.X + bounds.Width + options.Tolerance || box.Y + box.Height > bounds.Y + bounds.Height + options.Tolerance)
                throw graph.Error("Tables.ClusterBoundaryProfile", "A source advance cell crosses an inferred table boundary beyond tolerance.");
            c.Consume(PdfResourceKind.ExpandedBytes, 32);
            chars.Add(character);
        }

        c.Consume(PdfResourceKind.ExpandedBytes, chars.Count * 64L + 512);
        var indices = new HashSet<int>(chars.Select(ch => ch.Index));
        var covered = new Dictionary<int, PdfExtractedSpan>();
        var skip = new HashSet<int>();
        foreach (var span in data.Spans)
        {
            c.Checkpoint();
            int count = 0;
            for (int i = span.FirstCharacter; i < span.FirstCharacter + span.CharacterCount; i++)
            {
                c.Checkpoint();
                if (indices.Contains(i))
                    count++;
            }

            if (count == 0)
                continue;
            if (count != span.CharacterCount)
                throw graph.Error("Tables.ActualTextBoundary", "A source ActualText replacement spans multiple inferred cells; semantic cell splitting requires an explicit profile.");
            Tick(c);
            covered.Add(span.FirstCharacter, span);
            for (int i = span.FirstCharacter + 1; i < span.FirstCharacter + span.CharacterCount; i++)
            {
                Tick(c);
                skip.Add(i);
            }
        }

        c.Consume(PdfResourceKind.ExpandedBytes, chars.Count * 32L + 64);
        var ordered = chars.OrderBy(ch => Math.Round(ch.BaselineStart.Y / options.Tolerance)).ThenBy(ch => ch.Quad.Bounds.X).ToArray();
        var text = new StringBuilder();
        PdfExtractedCharacter? previous = null;
        foreach (var character in ordered)
        {
            c.Checkpoint();
            if (skip.Contains(character.Index))
            {
                previous = character;
                continue;
            }

            string value = covered.TryGetValue(character.Index, out var actual) ? actual.Text : character.Text;
            if (previous != null && !EndsSpace() && value.Length > 0 && !char.IsWhiteSpace(value[0]))
            {
                if (Math.Abs(character.BaselineStart.Y - previous.BaselineStart.Y) > options.Tolerance)
                    Append("\n");
                else if (character.Quad.Bounds.X - (previous.Quad.Bounds.X + previous.Quad.Bounds.Width) > Math.Min(character.FontSize, previous.FontSize) * .25)
                    Append(" ");
            }

            Append(value);
            previous = character;
        }

        c.Consume(PdfResourceKind.ExpandedBytes, text.Length * 2L + 128);
        string semantic = text.ToString();
        if (semantic.Length != 0 && PdfUnicode.AnalyzeCore(semantic, PdfParagraphDirection.LeftToRight, c).Scalars.Any(scalar => (scalar.EmbeddingLevel & 1) != 0))
            throw graph.Error("Tables.TextDirectionProfile", "Right-to-left cell reconstruction requires an explicit reading-order profile.");
        decimal? number = null;
        if (options.Culture != null && decimal.TryParse(semantic, NumberStyles.Number, options.Culture, out decimal numeric))
            number = numeric;
        return new(data.Page, row, col, rowSpan, colSpan, bounds, semantic, number, chars.AsReadOnly(), rules, confidence);
        bool EndsSpace() => text.Length == 0 || char.IsWhiteSpace(text[text.Length - 1]);
        void Append(string value)
        {
            c.Consume(PdfResourceKind.ExpandedBytes, value.Length * 4L + 32);
            text.Append(value);
        }
    }

    private static PdfExtractedTable Table(int page, List<double> xs, List<double> ys, List<PdfExtractedTableCell> cells, PdfTableDetectionMode mode, double confidence, PdfOperationContext c)
    {
        Tick(c);
        c.Consume(PdfResourceKind.ExpandedBytes, cells.Count * 16L + xs.Count * 16L + 192);
        return new(page, new(xs[0], ys[0], xs[xs.Count - 1] - xs[0], ys[ys.Count - 1] - ys[0]), ys.Count - 1, xs.Count - 1, new ReadOnlyCollection<PdfExtractedTableCell>(cells.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToArray()), mode, confidence, new ReadOnlyCollection<double>(xs));
    }

    private static List<PdfExtractedTableSequence> Sequences(List<PdfExtractedTable> tables, PdfTableExtractionOptions options, PdfOperationContext c)
    {
        Tick(c);
        var sequences = new List<PdfExtractedTableSequence>();
        var current = new List<int>();
        for (int i = 0; i < tables.Count; i++)
        {
            Tick(c);
            bool join = current.Count != 0 && options.JoinConsecutivePages && Compatible(tables[current[current.Count - 1]], tables[i]);
            if (!join && current.Count != 0)
            {
                sequences.Add(new(new ReadOnlyCollection<int>(current), current.Count > 1 ? options.HeaderRows : 0));
                current = new();
            }

            current.Add(i);
        }

        if (current.Count != 0)
        {
            Tick(c);
            sequences.Add(new(new ReadOnlyCollection<int>(current), current.Count > 1 ? options.HeaderRows : 0));
        }

        return sequences;
        bool Compatible(PdfExtractedTable a, PdfExtractedTable b)
        {
            if (b.PageIndex != a.PageIndex + 1 || a.ColumnCount != b.ColumnCount || a.RowCount <= options.HeaderRows || b.RowCount <= options.HeaderRows)
                return false;
            for (int col = 0; col < a.ColumnBoundaries.Count; col++)
            {
                c.Checkpoint();
                if (Math.Abs(a.ColumnBoundaries[col] - b.ColumnBoundaries[col]) > options.Tolerance)
                    return false;
            }

            for (int row = 0; row < options.HeaderRows; row++)
                for (int col = 0; col < a.ColumnCount; col++)
                {
                    Tick(c);
                    var ac = a.Cells.SingleOrDefault(x => x.Row == row && x.Column == col);
                    var bc = b.Cells.SingleOrDefault(x => x.Row == row && x.Column == col);
                    if (ac == null || bc == null || ac.RowSpan != bc.RowSpan || ac.ColumnSpan != bc.ColumnSpan || ac.Text != bc.Text)
                        return false;
                }

            return true;
        }
    }

    private static List<double> Unique(IEnumerable<double> values, double tolerance, PdfOperationContext c)
    {
        Tick(c);
        var result = new List<double>();
        foreach (double value in values.OrderBy(v => v))
        {
            Tick(c);
            if (result.Count == 0 || value - result[result.Count - 1] > tolerance)
                result.Add(value);
        }

        return result;
    }

    private static bool Inside(PdfRect bounds, PdfPoint point) => point.X >= bounds.X && point.Y >= bounds.Y && point.X <= bounds.X + bounds.Width && point.Y <= bounds.Y + bounds.Height;
    private static void CheckCount(long count, PdfTableExtractionOptions options, PdfGraphEdit graph)
    {
        if (count > options.MaximumCells)
            throw graph.Error("Tables.CellLimit", "The explicit maximum primitive-cell count was exceeded.");
        graph.Context.Consume(PdfResourceKind.Objects, count);
    }

    private static void Tick(PdfOperationContext c)
    {
        c.Checkpoint();
        c.Consume(PdfResourceKind.Objects, 1);
        c.Consume(PdfResourceKind.ExpandedBytes, 256);
    }
}
