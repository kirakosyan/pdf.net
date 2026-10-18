namespace Fluent.Pdf;
/// <summary>Four ordered corners of an affine rectangle, retaining its orientation under transforms.</summary>
public readonly struct PdfQuad
{
    /// <summary>Captures finite corners in perimeter order and rejects a nonconvex ordering.</summary>
    public PdfQuad(PdfPoint p0, PdfPoint p1, PdfPoint p2, PdfPoint p3)
    {
        P0 = p0;
        P1 = p1;
        P2 = p2;
        P3 = p3;
        var bounds = Bounds;
        double scale = Math.Max(bounds.Width, bounds.Height);
        if (scale == 0)
            return;
        bool positive = false, negative = false;
        var points = new[]
        {
            p0,
            p1,
            p2,
            p3
        };
        for (int i = 0; i < 4; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % 4];
            var c = points[(i + 2) % 4];
            double cross = ((b.X - a.X) / scale) * ((c.Y - b.Y) / scale) - ((b.Y - a.Y) / scale) * ((c.X - b.X) / scale);
            positive |= cross > 0;
            negative |= cross < 0;
        }

        if (positive && negative)
            throw new ArgumentException("Quad corners must form a convex perimeter.");
    }

    /// <summary>Gets the first transformed corner.</summary>
    public PdfPoint P0 { get; }
    /// <summary>Gets the second transformed corner.</summary>
    public PdfPoint P1 { get; }
    /// <summary>Gets the third transformed corner.</summary>
    public PdfPoint P2 { get; }
    /// <summary>Gets the fourth transformed corner.</summary>
    public PdfPoint P3 { get; }

    /// <summary>Gets its finite axis-aligned bounding rectangle.</summary>
    public PdfRect Bounds
    {
        get
        {
            double x = Math.Min(Math.Min(P0.X, P1.X), Math.Min(P2.X, P3.X)), y = Math.Min(Math.Min(P0.Y, P1.Y), Math.Min(P2.Y, P3.Y)), right = Math.Max(Math.Max(P0.X, P1.X), Math.Max(P2.X, P3.X)), top = Math.Max(Math.Max(P0.Y, P1.Y), Math.Max(P2.Y, P3.Y));
            return new(x, y, right - x, top - y);
        }
    }

    /// <summary>Tests the actual convex frame including its boundary; a degenerate frame has no area.</summary>
    public bool Contains(PdfPoint point)
    {
        var bounds = Bounds;
        if (bounds.Width == 0 || bounds.Height == 0 || point.X < bounds.X || point.X > bounds.Right || point.Y < bounds.Y || point.Y > bounds.Top)
            return false;
        double scale = Math.Max(bounds.Width, bounds.Height);
        var points = new[]
        {
            P0,
            P1,
            P2,
            P3
        };
        bool positive = false, negative = false;
        double area = 0;
        for (int i = 0; i < 4; i++)
        {
            var a = points[i];
            var b = points[(i + 1) % 4];
            double ax = (a.X - bounds.X) / scale, ay = (a.Y - bounds.Y) / scale, bx = (b.X - bounds.X) / scale, by = (b.Y - bounds.Y) / scale;
            area += ax * by - ay * bx;
            double cross = (bx - ax) * ((point.Y - a.Y) / scale) - (by - ay) * ((point.X - a.X) / scale);
            positive |= cross > 0;
            negative |= cross < 0;
        }

        return area != 0 && !(positive && negative);
    }
}

internal static class PdfGeometryMath
{
    internal static PdfQuad Transform(PdfRect rectangle, PdfMatrix matrix) => new(matrix.Transform(new(rectangle.X, rectangle.Y)), matrix.Transform(new(rectangle.Right, rectangle.Y)), matrix.Transform(new(rectangle.Right, rectangle.Top)), matrix.Transform(new(rectangle.X, rectangle.Top)));
    internal static bool Contains(PdfRect rectangle, PdfPoint point) => rectangle.Width > 0 && rectangle.Height > 0 && point.X >= rectangle.X && point.X <= rectangle.Right && point.Y >= rectangle.Y && point.Y <= rectangle.Top;
    internal static PdfRect Intersect(PdfRect a, PdfRect b)
    {
        double x = Math.Max(a.X, b.X), y = Math.Max(a.Y, b.Y), right = Math.Min(a.Right, b.Right), top = Math.Min(a.Top, b.Top);
        return new(x, y, Math.Max(0, right - x), Math.Max(0, top - y));
    }

    internal static PdfRect Expand(PdfRect rectangle, double amount) => new(rectangle.X - amount, rectangle.Y - amount, rectangle.Width + amount * 2, rectangle.Height + amount * 2);
}
