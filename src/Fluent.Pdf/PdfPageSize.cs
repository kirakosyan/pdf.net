namespace Fluent.Pdf;

/// <summary>Immutable physical page dimensions in points (72 points per inch).</summary>
public readonly struct PdfPageSize : IEquatable<PdfPageSize>
{
    /// <summary>Creates a size with finite positive dimensions in points.</summary>
    public PdfPageSize(double width, double height)
    {
        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0) throw new ArgumentOutOfRangeException(nameof(height));
        Width = width;
        Height = height;
    }

    /// <summary>Gets the unrotated width in points.</summary>
    public double Width { get; }
    /// <summary>Gets the unrotated height in points.</summary>
    public double Height { get; }
    /// <summary>Gets whether this value has usable dimensions; the default struct value is invalid.</summary>
    public bool IsValid => Width > 0 && Height > 0 && !double.IsInfinity(Width) && !double.IsInfinity(Height);
    /// <summary>Gets ISO A4 (210 by 297 mm), expressed in points.</summary>
    public static PdfPageSize A4 => new(595.2755905511812, 841.8897637795276);
    /// <summary>Gets US Letter (8.5 by 11 inches), expressed in points.</summary>
    public static PdfPageSize Letter => new(612, 792);
    /// <inheritdoc />
    public bool Equals(PdfPageSize other) => Width.Equals(other.Width) && Height.Equals(other.Height);
    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is PdfPageSize other && Equals(other);
    /// <inheritdoc />
    public override int GetHashCode() => unchecked((Width.GetHashCode() * 397) ^ Height.GetHashCode());
    /// <summary>Compares dimensions for exact equality.</summary>
    public static bool operator ==(PdfPageSize left, PdfPageSize right) => left.Equals(right);
    /// <summary>Compares dimensions for inequality.</summary>
    public static bool operator !=(PdfPageSize left, PdfPageSize right) => !left.Equals(right);
}

/// <summary>Clockwise page display rotation, independent of the physical page size.</summary>
public enum PdfPageRotation
{
    /// <summary>No rotation.</summary>
    None = 0,
    /// <summary>Rotate clockwise by 90 degrees.</summary>
    Clockwise90 = 90,
    /// <summary>Rotate clockwise by 180 degrees.</summary>
    Clockwise180 = 180,
    /// <summary>Rotate clockwise by 270 degrees.</summary>
    Clockwise270 = 270
}
