using System.Threading;

namespace Fluent.Pdf;
/// <summary>A bounded mapped word boundary in original-raster pixels, with an explicit curve approximation error.</summary>
public sealed class PdfScanMappedBoundary
{
    internal PdfScanMappedBoundary(PdfPoint[] points, PdfRect bounds, double error)
    {
        Points = Array.AsReadOnly(points);
        Bounds = bounds;
        MaximumErrorPixels = error;
    }

    /// <summary>Gets ordered boundary vertices, including the repeated closing point.</summary>
    public IReadOnlyList<PdfPoint> Points { get; }
    /// <summary>Gets a conservative original-raster bounding rectangle including the error envelope.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets the certified maximum original-pixel distance from each exact quadratic edge piece to its chord.</summary>
    public double MaximumErrorPixels { get; }
    /// <summary>Gets root diagnostics for public mapping, or null for same-context helpers.</summary>
    public PdfOperationReport? OperationReport { get; internal set; }
}

/// <summary>Complete inverse mapping from processed pixel edges through deskew/orientation and optional explicit dewarping.</summary>
public sealed class PdfScanGeometry
{
    internal PdfScanGeometry(int originalWidth, int originalHeight, int width, int height, PdfMatrix affine, PdfScanDewarpGrid? dewarp)
    {
        OriginalWidth = originalWidth;
        OriginalHeight = originalHeight;
        ProcessedWidth = width;
        ProcessedHeight = height;
        ProcessedToDewarped = affine;
        Dewarp = dewarp;
    }

    /// <summary>Gets original raster width.</summary>
    public int OriginalWidth { get; }
    /// <summary>Gets original raster height.</summary>
    public int OriginalHeight { get; }
    /// <summary>Gets corrected raster width.</summary>
    public int ProcessedWidth { get; }
    /// <summary>Gets corrected raster height.</summary>
    public int ProcessedHeight { get; }
    /// <summary>Gets the affine inverse of rotation/deskew, before optional nonlinear dewarping.</summary>
    public PdfMatrix ProcessedToDewarped { get; }
    /// <summary>Gets explicit nonlinear dewarp data, or null for a fully affine mapping.</summary>
    public PdfScanDewarpGrid? Dewarp { get; }

    /// <summary>Maps one pixel-edge point to the original raster. White deskew padding can map outside the original; nonlinear maps reject points outside their explicit domain.</summary>
    public PdfPoint MapPoint(PdfPoint point)
    {
        var mapped = ProcessedToDewarped.Transform(point);
        return Dewarp == null ? mapped : Dewarp.MapPoint(mapped);
    }

    /// <summary>Maps a recognized processed word to original geometry. Nonlinear boundaries split at grid crossings and bound quadratic chord error. Words outside original coverage reject rather than clamp silently.</summary>
    public PdfScanMappedBoundary MapWord(PdfOcrWord word, double maximumErrorPixels = 0.25, int maximumPoints = 4096, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (word == null)
            throw new ArgumentNullException(nameof(word));
        Check(maximumErrorPixels, maximumPoints);
        PdfScanMappedBoundary? result = null;
        var report = PdfFont.Run("MapScanWord", "managed.scan-geometry", policy, cancellationToken, c => result = MapCore(word.Bounds, maximumErrorPixels, maximumPoints, c), PdfCapability.RecognizeText);
        result!.OperationReport = report;
        return result;
    }

    private static void Check(double error, int points)
    {
        if (double.IsNaN(error) || double.IsInfinity(error) || error <= 0 || error > 100 || points < 5 || points > 65536)
            throw new ArgumentOutOfRangeException(nameof(error));
    }

    internal PdfScanMappedBoundary MapCore(PdfRect bounds, double error, int maximumPoints, PdfOperationContext c)
    {
        Check(error, maximumPoints);
        c.Checkpoint();
        if (bounds.X < 0 || bounds.Y < 0 || bounds.Right > ProcessedWidth || bounds.Top > ProcessedHeight)
            throw Failure("A recognized word lies outside the processed raster.");
        c.Consume(PdfResourceKind.ExpandedBytes, maximumPoints * 48L + 8192);
        var points = new List<PdfPoint>(maximumPoints);
        double observedError = 0;
        var corners = new[]
        {
            new PdfPoint(bounds.X, bounds.Y),
            new PdfPoint(bounds.Right, bounds.Y),
            new PdfPoint(bounds.Right, bounds.Top),
            new PdfPoint(bounds.X, bounds.Top)
        };
        for (int edge = 0; edge < 4; edge++)
        {
            c.Checkpoint();
            var a = ProcessedToDewarped.Transform(corners[edge]);
            var b = ProcessedToDewarped.Transform(corners[(edge + 1) % 4]);
            var splits = new List<double>(130)
            {
                0,
                1
            };
            if (Dewarp != null)
            {
                for (int x = 1; x < Dewarp.Columns - 1; x++)
                    Split(x * (double)Dewarp.OutputWidth / (Dewarp.Columns - 1), a.X, b.X);
                for (int y = 1; y < Dewarp.Rows - 1; y++)
                    Split(y * (double)Dewarp.OutputHeight / (Dewarp.Rows - 1), a.Y, b.Y);
            }

            splits.Sort();
            for (int k = 0; k < splits.Count - 1; k++)
            {
                double start = splits[k], end = splits[k + 1];
                if (end <= start)
                    continue;
                var p0 = Map(Lerp(a, b, start));
                var p1 = Map(Lerp(a, b, end));
                var midpoint = Map(Lerp(a, b, (start + end) / 2));
                double dx = midpoint.X - (p0.X + p1.X) / 2, dy = midpoint.Y - (p0.Y + p1.Y) / 2;
                double deviation = Math.Sqrt(dx * dx + dy * dy);
                double requested = Math.Max(1, Math.Ceiling(Math.Sqrt(deviation / error)));
                if (double.IsInfinity(requested) || requested > maximumPoints - points.Count - 1)
                    throw PdfWriterCore.Error(c, "Ocr.ScanGeometryLimit", "The explicit mapped boundary point ceiling was exceeded.");
                int segments = (int)requested;
                observedError = Math.Max(observedError, deviation / (segments * (double)segments));
                for (int n = 0; n < segments; n++)
                    Add(Map(Lerp(a, b, start + (end - start) * n / segments)));
            }

            void Split(double line, double begin, double finish)
            {
                if (begin == finish)
                    return;
                double t = (line - begin) / (finish - begin);
                if (t > 0 && t < 1)
                    splits.Add(t);
            }
        }

        Add(points[0]);
        double left = points.Min(p => p.X), top = points.Min(p => p.Y), right = points.Max(p => p.X), bottom = points.Max(p => p.Y);
        return new(points.ToArray(), new(Math.Max(0, left - observedError), Math.Max(0, top - observedError), Math.Min(OriginalWidth, right + observedError) - Math.Max(0, left - observedError), Math.Min(OriginalHeight, bottom + observedError) - Math.Max(0, top - observedError)), observedError);
        PdfPoint Map(PdfPoint p)
        {
            if (Dewarp == null)
                return p;
            if (p.X < -1e-9 || p.Y < -1e-9 || p.X > Dewarp.OutputWidth + 1e-9 || p.Y > Dewarp.OutputHeight + 1e-9)
                throw Failure("The word boundary falls outside explicit dewarp coverage.");
            return Dewarp.MapPoint(new(Math.Max(0, Math.Min(Dewarp.OutputWidth, p.X)), Math.Max(0, Math.Min(Dewarp.OutputHeight, p.Y))));
        }

        void Add(PdfPoint p)
        {
            c.Checkpoint();
            if (points.Count == maximumPoints)
                throw PdfWriterCore.Error(c, "Ocr.ScanGeometryLimit", "The explicit mapped boundary point ceiling was exceeded.");
            if (p.X < -1e-9 || p.Y < -1e-9 || p.X > OriginalWidth + 1e-9 || p.Y > OriginalHeight + 1e-9)
                throw Failure("The recognized word includes padding outside original-image coverage.");
            c.Consume(PdfResourceKind.Objects, 1);
            points.Add(new(Math.Max(0, Math.Min(OriginalWidth, p.X)), Math.Max(0, Math.Min(OriginalHeight, p.Y))));
        }

        PdfException Failure(string message) => PdfWriterCore.Error(c, "Ocr.ScanGeometry", message);
    }

    private static PdfPoint Lerp(PdfPoint a, PdfPoint b, double t) => new(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t);
}
