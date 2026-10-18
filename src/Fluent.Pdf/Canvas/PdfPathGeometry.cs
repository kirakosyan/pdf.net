using System.Threading;

namespace Fluent.Pdf;
public sealed partial class PdfPath
{
    internal static PdfPath RectangleCore(PdfRect bounds, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.Objects, 1);
        context.Consume(PdfResourceKind.ExpandedBytes, 1024);
        var segment = PdfPathSegment.Rectangle(bounds);
        return new PdfPath(new List<PdfPathSegment> { segment }, new List<PdfContentOperation> { new(PdfOperator.Rectangle, PdfValues.Real(bounds.X), PdfValues.Real(bounds.Y), PdfValues.Real(bounds.Width), PdfValues.Real(bounds.Height)) });
    }

    /// <summary>Computes curve extrema and closed-path bounds under a fresh bounded geometry operation.</summary>
    public PdfRect GetBounds(PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfRect result = default;
        PdfFont.Run("MeasurePath", "managed.canvas", policy, cancellationToken, c => result = PdfPathGeometry.Bounds(this, c), PdfCapability.WriteDocument);
        return result;
    }

    /// <summary>Tests the implicitly closed fill region using nonzero/even-odd winding and explicit absolute boundary tolerance.</summary>
    public bool Contains(PdfPoint point, bool evenOdd = false, double tolerance = 0.000000001, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfGraphicsNumbers.Finite(tolerance);
        if (tolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        bool result = false;
        PdfFont.Run("HitTestPath", "managed.canvas", policy, cancellationToken, c => result = PdfPathGeometry.Contains(this, point, evenOdd, tolerance, c), PdfCapability.WriteDocument);
        return result;
    }
}

// Original analytic-extrema measurement and monotone-curve ray crossings; operators remain unchanged for PDF output.
internal static class PdfPathGeometry
{
    internal static PdfRect Bounds(PdfPath path, PdfOperationContext context)
    {
        bool any = false;
        double left = 0, bottom = 0, right = 0, top = 0;
        PdfPoint current = default, start = default;
        void Add(PdfPoint p)
        {
            if (!any)
            {
                left = right = p.X;
                bottom = top = p.Y;
                any = true;
            }
            else
            {
                left = Math.Min(left, p.X);
                right = Math.Max(right, p.X);
                bottom = Math.Min(bottom, p.Y);
                top = Math.Max(top, p.Y);
            }
        }

        foreach (var segment in path.Segments)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 1024);
            var a = segment.Coordinates;
            switch (segment.Operation)
            {
                case PdfOperator.MoveTo:
                    current = start = new(a[0], a[1]);
                    Add(current);
                    break;
                case PdfOperator.LineTo:
                    Add(current);
                    current = new(a[0], a[1]);
                    Add(current);
                    break;
                case PdfOperator.CurveTo:
                    var p1 = new PdfPoint(a[0], a[1]);
                    var p2 = new PdfPoint(a[2], a[3]);
                    var p3 = new PdfPoint(a[4], a[5]);
                    Add(current);
                    Add(p3);
                    foreach (double t in Roots(current.X, p1.X, p2.X, p3.X).Concat(Roots(current.Y, p1.Y, p2.Y, p3.Y)))
                        if (t > 0 && t < 1)
                            Add(Curve(current, p1, p2, p3, t));
                    current = p3;
                    break;
                case PdfOperator.ClosePath:
                    Add(current);
                    Add(start);
                    current = start;
                    break;
                case PdfOperator.Rectangle:
                    current = start = new(a[0], a[1]);
                    Add(current);
                    Add(new(a[0] + a[2], a[1] + a[3]));
                    break;
            }
        }

        return any ? new PdfRect(left, bottom, right - left, top - bottom) : default;
    }

    internal static bool Contains(PdfPath path, PdfPoint point, bool evenOdd, double tolerance, PdfOperationContext context)
    {
        int winding = 0;
        bool boundary = false, open = false;
        PdfPoint current = default, start = default;
        void Line(PdfPoint a, PdfPoint b)
        {
            double scale = Math.Max(Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y)), 1);
            double dx = (b.X - a.X) / scale, dy = (b.Y - a.Y) / scale, px = (point.X - a.X) / scale, py = (point.Y - a.Y) / scale;
            double cross = dx * py - dy * px;
            if (Math.Abs(cross) <= tolerance / scale && point.X >= Math.Min(a.X, b.X) - tolerance && point.X <= Math.Max(a.X, b.X) + tolerance && point.Y >= Math.Min(a.Y, b.Y) - tolerance && point.Y <= Math.Max(a.Y, b.Y) + tolerance)
                boundary = true;
            if ((a.Y <= point.Y && b.Y > point.Y) || (b.Y <= point.Y && a.Y > point.Y))
            {
                double x = a.X + (b.X - a.X) * ((point.Y - a.Y) / (b.Y - a.Y));
                if (x > point.X)
                    winding += b.Y > a.Y ? 1 : -1;
            }
        }

        void Close()
        {
            if (open)
                Line(current, start);
            open = false;
        }

        foreach (var segment in path.Segments)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 2048);
            var a = segment.Coordinates;
            switch (segment.Operation)
            {
                case PdfOperator.MoveTo:
                    Close();
                    current = start = new(a[0], a[1]);
                    break;
                case PdfOperator.LineTo:
                    var end = new PdfPoint(a[0], a[1]);
                    Line(current, end);
                    current = end;
                    open = true;
                    break;
                case PdfOperator.ClosePath:
                    Close();
                    current = start;
                    break;
                case PdfOperator.Rectangle:
                    Close();
                    var p0 = new PdfPoint(a[0], a[1]);
                    var p1 = new PdfPoint(a[0] + a[2], a[1]);
                    var p2 = new PdfPoint(a[0] + a[2], a[1] + a[3]);
                    var p3 = new PdfPoint(a[0], a[1] + a[3]);
                    Line(p0, p1);
                    Line(p1, p2);
                    Line(p2, p3);
                    Line(p3, p0);
                    current = start = p0;
                    break;
                case PdfOperator.CurveTo:
                    var c0 = current;
                    var c1 = new PdfPoint(a[0], a[1]);
                    var c2 = new PdfPoint(a[2], a[3]);
                    var c3 = new PdfPoint(a[4], a[5]);
                    var split = new[]
                    {
                        0d
                    }.Concat(Roots(c0.Y, c1.Y, c2.Y, c3.Y).Where(t => t > 0 && t < 1).OrderBy(t => t)).Concat(new[] { 1d }).ToArray();
                    for (int i = 0; i + 1 < split.Length; i++)
                    {
                        double lo = split[i], hi = split[i + 1];
                        var from = Curve(c0, c1, c2, c3, lo);
                        var to = Curve(c0, c1, c2, c3, hi);
                        if (Math.Abs(from.Y - point.Y) <= tolerance && Math.Abs(from.X - point.X) <= tolerance || Math.Abs(to.Y - point.Y) <= tolerance && Math.Abs(to.X - point.X) <= tolerance)
                            boundary = true;
                        if (from.Y == to.Y)
                        {
                            if (Math.Abs(from.Y - point.Y) <= tolerance)
                            {
                                double min = Math.Min(from.X, to.X), max = Math.Max(from.X, to.X);
                                foreach (double t in Roots(c0.X, c1.X, c2.X, c3.X))
                                    if (t > lo && t < hi)
                                    {
                                        double x = Curve(c0, c1, c2, c3, t).X;
                                        min = Math.Min(min, x);
                                        max = Math.Max(max, x);
                                    }

                                if (point.X >= min - tolerance && point.X <= max + tolerance)
                                    boundary = true;
                            }

                            continue;
                        }

                        if (!((from.Y <= point.Y && to.Y > point.Y) || (to.Y <= point.Y && from.Y > point.Y)))
                            continue;
                        bool up = to.Y > from.Y;
                        for (int iteration = 0; iteration < 64; iteration++)
                        {
                            double mid = (lo + hi) / 2, y = Curve(c0, c1, c2, c3, mid).Y;
                            if (up ? y < point.Y : y > point.Y)
                                lo = mid;
                            else
                                hi = mid;
                        }

                        double crossing = Curve(c0, c1, c2, c3, (lo + hi) / 2).X;
                        if (Math.Abs(crossing - point.X) <= tolerance)
                            boundary = true;
                        if (crossing > point.X)
                            winding += up ? 1 : -1;
                    }

                    current = c3;
                    open = true;
                    break;
            }
        }

        Close();
        return boundary || (evenOdd ? (Math.Abs(winding) & 1) != 0 : winding != 0);
    }

    internal static IEnumerable<double> Roots(double p0, double p1, double p2, double p3)
    {
        double scale = Math.Max(Math.Max(Math.Abs(p0), Math.Abs(p1)), Math.Max(Math.Abs(p2), Math.Abs(p3)));
        if (scale == 0)
            yield break;
        p0 /= scale;
        p1 /= scale;
        p2 /= scale;
        p3 /= scale;
        double a = -p0 + 3 * p1 - 3 * p2 + p3, b = 2 * (p0 - 2 * p1 + p2), c = p1 - p0;
        if (a == 0)
        {
            if (b != 0)
                yield return -c / b;
            yield break;
        }

        double determinant = b * b - 4 * a * c;
        if (determinant < 0)
            yield break;
        double root = Math.Sqrt(determinant), q = -.5 * (b + (b >= 0 ? root : -root));
        if (q == 0)
        {
            yield return -b / (2 * a);
            yield break;
        }

        yield return q / a;
        if (determinant != 0)
            yield return c / q;
    }

    internal static PdfPoint Curve(PdfPoint p0, PdfPoint p1, PdfPoint p2, PdfPoint p3, double t)
    {
        PdfPoint Mix(PdfPoint a, PdfPoint b) => new(a.X * (1 - t) + b.X * t, a.Y * (1 - t) + b.Y * t);
        var a = Mix(p0, p1);
        var b = Mix(p1, p2);
        var c = Mix(p2, p3);
        return Mix(Mix(a, b), Mix(b, c));
    }

    internal static IReadOnlyList<PdfContentOperation> Transform(PdfPath path, PdfMatrix matrix, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, path.Segments.Count * 4096L + 128);
        var operations = new List<PdfContentOperation>();
        PdfValue[] Values(params PdfPoint[] points) => points.SelectMany(p => new[] { PdfValues.Real(p.X), PdfValues.Real(p.Y) }).ToArray();
        foreach (var segment in path.Segments)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            var a = segment.Coordinates;
            switch (segment.Operation)
            {
                case PdfOperator.MoveTo:
                case PdfOperator.LineTo:
                    operations.Add(new(segment.Operation, Values(matrix.Transform(new(a[0], a[1])))));
                    break;
                case PdfOperator.CurveTo:
                    operations.Add(new(segment.Operation, Values(matrix.Transform(new(a[0], a[1])), matrix.Transform(new(a[2], a[3])), matrix.Transform(new(a[4], a[5])))));
                    break;
                case PdfOperator.ClosePath:
                    operations.Add(new(PdfOperator.ClosePath));
                    break;
                case PdfOperator.Rectangle:
                    var quad = PdfGeometryMath.Transform(new(a[0], a[1], a[2], a[3]), matrix);
                    operations.Add(new(PdfOperator.MoveTo, Values(quad.P0)));
                    operations.Add(new(PdfOperator.LineTo, Values(quad.P1)));
                    operations.Add(new(PdfOperator.LineTo, Values(quad.P2)));
                    operations.Add(new(PdfOperator.LineTo, Values(quad.P3)));
                    operations.Add(new(PdfOperator.ClosePath));
                    break;
            }
        }

        if (operations.Count == 0)
            operations.Add(new(PdfOperator.Rectangle, PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(0)));
        return operations.AsReadOnly();
    }
}
