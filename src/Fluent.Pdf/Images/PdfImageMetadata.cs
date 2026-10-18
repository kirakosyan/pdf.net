namespace Fluent.Pdf;
/// <summary>The eight standard TIFF/Exif display orientations.</summary>
public enum PdfImageOrientation
{
    /// <summary>Rows top to bottom, columns left to right.</summary>
    Normal = 1,
    /// <summary>Reflect horizontally.</summary>
    MirrorHorizontal = 2,
    /// <summary>Rotate 180 degrees.</summary>
    Rotate180 = 3,
    /// <summary>Reflect vertically.</summary>
    MirrorVertical = 4,
    /// <summary>Transpose the row and column axes.</summary>
    Transpose = 5,
    /// <summary>Rotate 90 degrees clockwise.</summary>
    Rotate90 = 6,
    /// <summary>Transpose across the opposite diagonal.</summary>
    Transverse = 7,
    /// <summary>Rotate 270 degrees clockwise.</summary>
    Rotate270 = 8
}

/// <summary>Immutable explicit image orientation, pixel aspect and optional physical resolution.</summary>
public sealed class PdfImageMetadata
{
    /// <summary>Captures display metadata. Both DPI values must be positive or both zero; density also determines pixel aspect.</summary>
    public PdfImageMetadata(PdfImageOrientation orientation = PdfImageOrientation.Normal, double horizontalDpi = 0, double verticalDpi = 0, double pixelAspectRatio = 1)
    {
        if (!Enum.IsDefined(typeof(PdfImageOrientation), orientation))
            throw new ArgumentOutOfRangeException(nameof(orientation));
        foreach (double value in new[]
        {
            horizontalDpi,
            verticalDpi,
            pixelAspectRatio
        }

        )
            PdfGraphicsNumbers.Finite(value);
        if (horizontalDpi < 0 || verticalDpi < 0 || (horizontalDpi == 0) != (verticalDpi == 0) || pixelAspectRatio <= 0)
            throw new ArgumentOutOfRangeException(nameof(horizontalDpi));
        Orientation = orientation;
        HorizontalDpi = horizontalDpi;
        VerticalDpi = verticalDpi;
        PixelAspectRatio = horizontalDpi > 0 ? verticalDpi / horizontalDpi : pixelAspectRatio;
        if (PixelAspectRatio <= 0 || double.IsInfinity(PixelAspectRatio))
            throw new ArgumentOutOfRangeException(nameof(pixelAspectRatio));
    }

    /// <summary>Gets the explicit display orientation.</summary>
    public PdfImageOrientation Orientation { get; }
    /// <summary>Gets horizontal samples per inch, or zero when unknown.</summary>
    public double HorizontalDpi { get; }
    /// <summary>Gets vertical samples per inch, or zero when unknown.</summary>
    public double VerticalDpi { get; }
    /// <summary>Gets physical pixel width divided by physical pixel height.</summary>
    public double PixelAspectRatio { get; }
}

/// <summary>Immutable image-file import choices; preserving encoded data never silently transcodes it.</summary>
public sealed class PdfImageReadOptions
{
    /// <summary>Captures explicit permission for lossless layout/channel conversion and selection of Exif display orientation.</summary>
    public PdfImageReadOptions(bool allowLosslessTranscode = false, bool applyExifOrientation = true, PdfColorSpace? colorSpaceOverride = null)
    {
        if (colorSpaceOverride?.IsPattern == true)
            throw new ArgumentException("An image requires a non-pattern color space.", nameof(colorSpaceOverride));
        AllowLosslessTranscode = allowLosslessTranscode;
        ApplyExifOrientation = applyExifOrientation;
        ColorSpaceOverride = colorSpaceOverride;
    }

    /// <summary>Gets permission to separate alpha or deinterlace using an explicit lossless transcode when byte-preserving PDF packaging is impossible.</summary>
    public bool AllowLosslessTranscode { get; }
    /// <summary>Gets an explicit component interpretation for a file without an embedded ICC profile; sample values remain unchanged.</summary>
    public PdfColorSpace? ColorSpaceOverride { get; }
    /// <summary>Gets whether file-declared Exif orientation controls placement.</summary>
    public bool ApplyExifOrientation { get; }
}
