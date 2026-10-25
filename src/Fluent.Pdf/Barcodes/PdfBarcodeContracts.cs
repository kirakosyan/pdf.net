using System.Collections.ObjectModel;
using System.IO;

namespace Fluent.Pdf;
/// <summary>Explicit supported symbol profiles.</summary>
public enum PdfBarcodeSymbology
{
    /// <summary>QR Model2, versions1–40, UTF8 byte mode with ECI26.</summary>
    QrCode,
    /// <summary>Code128 code setB, printable ASCII, with mandatory modulo103 checksum.</summary>
    Code128
}

/// <summary>Explicit QR error correction; Code128 uses None and its mandatory checksum.</summary>
public enum PdfBarcodeErrorCorrection
{
    /// <summary>No error correction. Code128 checksum still applies.</summary>
    None,
    /// <summary>QR levelL.</summary>
    Low,
    /// <summary>QR levelM.</summary>
    Medium,
    /// <summary>QR levelQ.</summary>
    Quartile,
    /// <summary>QR levelH.</summary>
    High
}

/// <summary>Immutable finite generation settings. Generated symbols require an explicitly configured independent decoder.</summary>
public sealed class PdfBarcodeOptions
{
    /// <summary>Selects QR/ECC or Code128B. Zero quiet zone selects4 QR or10 Code128 modules; explicit margins must meet those minima and be at most64. QR version zero chooses the smallest fitting version. Code128 supports1–256 printable ASCII characters.</summary>
    public PdfBarcodeOptions(PdfBarcodeSymbology symbology = PdfBarcodeSymbology.QrCode, PdfBarcodeErrorCorrection errorCorrection = PdfBarcodeErrorCorrection.Medium, int qrVersion = 0, int quietZone = 0, int barHeight = 40)
    {
        if (!Enum.IsDefined(typeof(PdfBarcodeSymbology), symbology))
            throw new ArgumentOutOfRangeException(nameof(symbology));
        if (!Enum.IsDefined(typeof(PdfBarcodeErrorCorrection), errorCorrection) || (symbology == PdfBarcodeSymbology.QrCode ? errorCorrection == PdfBarcodeErrorCorrection.None : errorCorrection != PdfBarcodeErrorCorrection.None))
            throw new ArgumentOutOfRangeException(nameof(errorCorrection));
        if (qrVersion < 0 || qrVersion > 40 || symbology == PdfBarcodeSymbology.Code128 && qrVersion != 0)
            throw new ArgumentOutOfRangeException(nameof(qrVersion));
        int minimum = symbology == PdfBarcodeSymbology.QrCode ? 4 : 10;
        if (quietZone != 0 && (quietZone < minimum || quietZone > 64))
            throw new ArgumentOutOfRangeException(nameof(quietZone));
        if (barHeight < 1 || barHeight > 128 || symbology == PdfBarcodeSymbology.QrCode && barHeight != 40)
            throw new ArgumentOutOfRangeException(nameof(barHeight));
        Symbology = symbology;
        ErrorCorrection = errorCorrection;
        QrVersion = qrVersion;
        QuietZone = quietZone == 0 ? minimum : quietZone;
        BarHeight = barHeight;
    }

    /// <summary>Gets the exact symbol profile.</summary>
    public PdfBarcodeSymbology Symbology { get; }
    /// <summary>Gets the requested error correction.</summary>
    public PdfBarcodeErrorCorrection ErrorCorrection { get; }
    /// <summary>Gets an explicit QR version or zero for smallest fitting.</summary>
    public int QrVersion { get; }
    /// <summary>Gets white quiet-zone modules on every side.</summary>
    public int QuietZone { get; }
    /// <summary>Gets Code128 bar height in modules.</summary>
    public int BarHeight { get; }
}

/// <summary>One independent decoder observation. Text is decoded Unicode; this does not assert the original QR byte encoding or expose arbitrary binary payloads.</summary>
public sealed class PdfBarcodeObservation
{
    /// <summary>Captures decoded text and detector landmarks. QR points are finder/alignment centers; Code128 points are scan-line endpoints. They are not symbol outline corners.</summary>
    public PdfBarcodeObservation(PdfBarcodeSymbology symbology, string text, PdfBarcodeErrorCorrection errorCorrection, IEnumerable<PdfPoint> points)
    {
        if (!Enum.IsDefined(typeof(PdfBarcodeSymbology), symbology))
            throw new ArgumentOutOfRangeException(nameof(symbology));
        if (!Enum.IsDefined(typeof(PdfBarcodeErrorCorrection), errorCorrection))
            throw new ArgumentOutOfRangeException(nameof(errorCorrection));
        if (text == null || text.Length > 32768)
            throw new ArgumentException("Bounded decoded Unicode is required.", nameof(text));
        if (points == null)
            throw new ArgumentNullException(nameof(points));
        var copy = new List<PdfPoint>();
        foreach (var point in points)
        {
            if (copy.Count == 16)
                throw new ArgumentException("At most16 detector landmarks are supported.", nameof(points));
            copy.Add(point);
        }

        if (copy.Count == 0)
            throw new ArgumentException("At least one detector landmark is required.", nameof(points));
        Symbology = symbology;
        Text = text;
        ErrorCorrection = errorCorrection;
        Points = new ReadOnlyCollection<PdfPoint>(copy);
    }

    /// <summary>Gets the decoded symbol profile.</summary>
    public PdfBarcodeSymbology Symbology { get; }
    /// <summary>Gets decoded Unicode, never automatically interpreted as a URI, command or payment instruction.</summary>
    public string Text { get; }
    /// <summary>Gets observed QR error correction, or None for Code128.</summary>
    public PdfBarcodeErrorCorrection ErrorCorrection { get; }
    /// <summary>Gets detector landmarks in image pixels, or native PDF user coordinates for a page result.</summary>
    public IReadOnlyList<PdfPoint> Points { get; }
}

/// <summary>Explicit independently implemented caller-owned decoder. It must honor the mandatory context and must not delegate generation verification back to the encoder.</summary>
public interface IPdfBarcodeDecoder : IPdfProvider
{
    /// <summary>Reports exact symbology availability before generation.</summary>
    bool Supports(PdfBarcodeSymbology symbology);
    /// <summary>Decodes an explicitly captured raster. This finite source profile returns at most one symbol per requested image/region; all landmarks use its top-left pixel frame. Pixel leases expire at return.</summary>
    PdfBarcodeObservation? Decode(PdfOcrRaster raster, PdfOperationContext context);
}

/// <summary>A generated immutable symbol accepted only after independent decoding of its actual canonical pixels.</summary>
public sealed class PdfBarcodeSymbol
{
    internal PdfBarcodeSymbol(string text, PdfBarcodeOptions options, PdfBarcodeGrid grid, PdfDrawing drawing, PdfOcrRaster raster, string verifier)
    {
        Text = text;
        Options = options;
        Width = grid.Width;
        Height = grid.Height;
        QrVersion = grid.Version;
        Mask = grid.Mask;
        Drawing = drawing;
        VerificationRaster = raster;
        VerifierIdentity = verifier;
    }

    /// <summary>Gets exact independently decoded Unicode payload.</summary>
    public string Text { get; }
    /// <summary>Gets immutable requested settings.</summary>
    public PdfBarcodeOptions Options { get; }
    /// <summary>Gets module width including the white quiet zone.</summary>
    public int Width { get; }
    /// <summary>Gets module height including the white quiet zone.</summary>
    public int Height { get; }
    /// <summary>Gets actual QR version or zero for Code128.</summary>
    public int QrVersion { get; }
    /// <summary>Gets actual QR mask0–7 or minus one for Code128.</summary>
    public int Mask { get; }
    /// <summary>Gets a device-gray vector drawing with an opaque white quiet zone. Place with its module bounds and aspect-preserving canvas fit.</summary>
    public PdfDrawing Drawing { get; }
    /// <summary>Gets the exact canonical opaque grayscale pixels checked by the independent decoder.</summary>
    public PdfOcrRaster VerificationRaster { get; }

    /// <summary>Opens an independent read-only detached view of the exact canonical Gray8 verification pixels, with stride equal to raster width.</summary>
    public Stream OpenVerificationPixels() => VerificationRaster.Bytes.Open(0, VerificationRaster.ByteLength);
    /// <summary>Gets the explicit decoder identity/version, without a production qualification claim.</summary>
    public string VerifierIdentity { get; }
    /// <summary>Gets the completed generation/verification/drawing report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;
}

/// <summary>Detached bounded detection result, with native PDF coordinates only when PageIndex has a value.</summary>
public sealed class PdfBarcodeDetection
{
    internal PdfBarcodeDetection(PdfBarcodeObservation? observation, int? page, PdfOperationReport report)
    {
        Observation = observation;
        PageIndex = page;
        OperationReport = report;
    }

    /// <summary>Gets the single detected observation, or null when no supported symbol decoded.</summary>
    public PdfBarcodeObservation? Observation { get; }
    /// <summary>Gets a zero-based PDF page index or null for a standalone raster.</summary>
    public int? PageIndex { get; }
    /// <summary>Gets the complete operation report.</summary>
    public PdfOperationReport OperationReport { get; }
}

internal sealed class PdfBarcodeGrid
{
    internal readonly bool[] Bits;
    internal readonly int Width, Height, Version, Mask;
    internal PdfBarcodeGrid(bool[] bits, int width, int height, int version = 0, int mask = -1)
    {
        Bits = bits;
        Width = width;
        Height = height;
        Version = version;
        Mask = mask;
    }
}
