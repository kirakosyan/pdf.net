namespace Fluent.Pdf;
/// <summary>A finite point in PDF Cartesian user coordinates.</summary>
public readonly struct PdfPoint
{
    /// <summary>Creates finite coordinates.</summary>
    public PdfPoint(double x, double y)
    {
        PdfGraphicsNumbers.Finite(x);
        PdfGraphicsNumbers.Finite(y);
        X = x;
        Y = y;
    }

    /// <summary>Gets horizontal position.</summary>
    public double X { get; }
    /// <summary>Gets vertical position.</summary>
    public double Y { get; }
}

/// <summary>A finite nonnegative Cartesian rectangle with ordered bounds.</summary>
public readonly struct PdfRect
{
    /// <summary>Creates a rectangle from lower-left origin and nonnegative extent.</summary>
    public PdfRect(double x, double y, double width, double height)
    {
        PdfGraphicsNumbers.Finite(x);
        PdfGraphicsNumbers.Finite(y);
        PdfGraphicsNumbers.Finite(width);
        PdfGraphicsNumbers.Finite(height);
        if (width < 0 || height < 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        PdfGraphicsNumbers.Finite(x + width);
        PdfGraphicsNumbers.Finite(y + height);
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }

    /// <summary>Gets its lower-left horizontal position.</summary>
    public double X { get; }
    /// <summary>Gets its lower-left vertical position.</summary>
    public double Y { get; }
    /// <summary>Gets nonnegative width.</summary>
    public double Width { get; }
    /// <summary>Gets nonnegative height.</summary>
    public double Height { get; }
    /// <summary>Gets the right bound.</summary>
    public double Right => X + Width;
    /// <summary>Gets the top bound.</summary>
    public double Top => Y + Height;
    internal PdfValue Value => PdfGraphicsNumbers.Array(X, Y, Right, Top);
}

/// <summary>A finite affine PDF matrix: x'=A*x+C*y+E, y'=B*x+D*y+F.</summary>
public readonly struct PdfMatrix
{
    /// <summary>Captures six finite affine coefficients. Singular matrices are representable but cannot be inverted.</summary>
    public PdfMatrix(double a, double b, double c, double d, double e, double f)
    {
        PdfGraphicsNumbers.Finite(a);
        PdfGraphicsNumbers.Finite(b);
        PdfGraphicsNumbers.Finite(c);
        PdfGraphicsNumbers.Finite(d);
        PdfGraphicsNumbers.Finite(e);
        PdfGraphicsNumbers.Finite(f);
        A = a;
        B = b;
        C = c;
        D = d;
        E = e;
        F = f;
    }

    /// <summary>Gets x scale.</summary>
    public double A { get; }
    /// <summary>Gets x-to-y skew.</summary>
    public double B { get; }
    /// <summary>Gets y-to-x skew.</summary>
    public double C { get; }
    /// <summary>Gets y scale.</summary>
    public double D { get; }
    /// <summary>Gets horizontal translation.</summary>
    public double E { get; }
    /// <summary>Gets vertical translation.</summary>
    public double F { get; }
    /// <summary>Gets the identity transform.</summary>
    public static PdfMatrix Identity => new(1, 0, 0, 1, 0, 0);

    /// <summary>Transforms a point and rejects nonfinite results.</summary>
    public PdfPoint Transform(PdfPoint point) => new(A * point.X + C * point.Y + E, B * point.X + D * point.Y + F);
    /// <summary>Returns this matrix times right; right is applied to points first.</summary>
    public PdfMatrix Multiply(PdfMatrix right) => new(A * right.A + C * right.B, B * right.A + D * right.B, A * right.C + C * right.D, B * right.C + D * right.D, A * right.E + C * right.F + E, B * right.E + D * right.F + F);
    /// <summary>Returns the exact algebraic inverse, rejecting singular/nonfinite arithmetic.</summary>
    public PdfMatrix Invert()
    {
        double determinant = A * D - B * C;
        if (determinant == 0 || double.IsNaN(determinant) || double.IsInfinity(determinant))
            throw new InvalidOperationException("The matrix has no finite inverse.");
        return new(D / determinant, -B / determinant, -C / determinant, A / determinant, (C * F - D * E) / determinant, (B * E - A * F) / determinant);
    }

    internal PdfValue Value => PdfGraphicsNumbers.Array(A, B, C, D, E, F);
}

internal static class PdfGraphicsNumbers
{
    internal static void Finite(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw new ArgumentOutOfRangeException(nameof(value), "Finite geometry is required.");
    }

    internal static void Unit(double value)
    {
        Finite(value);
        if (value < 0 || value > 1)
            throw new ArgumentOutOfRangeException(nameof(value));
    }

    internal static PdfValue Array(params double[] values) => PdfValue.Array(values.Select(PdfValues.Real).ToArray());
    internal static PdfValue Dict(params (string Key, PdfValue Value)[] values) => PdfValue.Dictionary(values.Select(x => new KeyValuePair<string, PdfValue>(x.Key, x.Value)));
}
