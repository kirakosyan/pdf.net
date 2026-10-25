using ZXing;
using ZXing.Common;

namespace Fluent.Pdf.Ocr;
/// <summary>Explicit optional managed ZXing.Net0.16.11 decoder, independent of the original core encoders. One symbol per image/region; no native, desktop, image-format or network discovery.</summary>
public sealed class ZxingBarcodeDecoder : IPdfBarcodeDecoder
{
    /// <summary>Captures a finite decoded raster ceiling up to1048576 pixels. The adapter precharges a conservative128-byte-per-pixel managed workspace plus1MiB before invoking the engine. Cancellation is checked around its bounded synchronous call.</summary>
    public ZxingBarcodeDecoder(int maximumPixels = 1048576)
    {
        if (maximumPixels < 1 || maximumPixels > 1048576)
            throw new ArgumentOutOfRangeException(nameof(maximumPixels));
        if (typeof(BarcodeReaderGeneric).Assembly.GetName().Version?.ToString() != "0.16.11.0")
            throw new InvalidOperationException("The exact pinned ZXing.Net0.16.11 assembly is required.");
        MaximumPixels = maximumPixels;
        Descriptor = new("managed.zxing-barcodes", "0.16.11", PdfCapability.Barcodes);
    }

    /// <summary>Gets the exact pinned decoder service identity.</summary>
    public PdfProviderDescriptor Descriptor { get; }
    /// <summary>Gets the finite provider raster ceiling.</summary>
    public int MaximumPixels { get; }

    /// <summary>Reports QR Model2 and Code128 decoding.</summary>
    public bool Supports(PdfBarcodeSymbology symbology) => symbology == PdfBarcodeSymbology.QrCode || symbology == PdfBarcodeSymbology.Code128;
    /// <summary>Decodes one explicit opaque Gray8/Rgb24/Bgra32 raster. QR finder/alignment landmarks and Code128 scan-line endpoints remain top-left image-pixel coordinates. Returned Unicode is a decoder observation, not an original byte-encoding claim.</summary>
    public PdfBarcodeObservation? Decode(PdfOcrRaster raster, PdfOperationContext context)
    {
        if (raster == null)
            throw new ArgumentNullException(nameof(raster));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        long pixels = (long)raster.Width * raster.Height;
        if (pixels > MaximumPixels || raster.Width > 32768 || raster.Height > 32768)
            throw Error(context, "DecoderExtent", "The explicit decoder raster/pixel ceiling is exceeded; request a smaller explicit page region or resolution.");
        context.Consume(PdfResourceKind.ExpandedBytes, pixels * 128 + raster.Stride + 1048576);
        var luminance = new byte[(int)pixels];
        var row = new byte[raster.Stride];
        using (var input = raster.OpenPixels(context))
            for (int y = 0; y < raster.Height; y++)
            {
                context.Checkpoint();
                int read = 0;
                while (read < row.Length)
                {
                    int count = input.Read(row, read, row.Length - read);
                    if (count == 0)
                        throw Error(context, "RasterLength", "The explicit raster ended before its declared sample extent.");
                    read += count;
                }

                for (int x = 0; x < raster.Width; x++)
                {
                    int value;
                    if (raster.PixelFormat == PdfOcrPixelFormat.Gray8)
                        value = row[x];
                    else
                    {
                        bool bgra = raster.PixelFormat == PdfOcrPixelFormat.Bgra32;
                        int at = x * (bgra ? 4 : 3), r = row[at + (bgra ? 2 : 0)], g = row[at + 1], b = row[at + (bgra ? 0 : 2)];
                        if (bgra && row[at + 3] != 255)
                            throw Error(context, "RasterAlpha", "Barcode decoding requires explicitly composited opaque pixels.");
                        value = (306 * r + 601 * g + 117 * b + 512) >> 10;
                    }

                    luminance[y * raster.Width + x] = (byte)value;
                }
            }

        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions
            {
                TryHarder = true,
                TryInverted = true,
                PossibleFormats = new List<BarcodeFormat>
                {
                    BarcodeFormat.QR_CODE,
                    BarcodeFormat.CODE_128
                }
            }
        };
        var decoded = reader.Decode(luminance, raster.Width, raster.Height, RGBLuminanceSource.BitmapFormat.Gray8);
        context.Checkpoint();
        if (decoded == null)
            return null;
        var symbology = decoded.BarcodeFormat == BarcodeFormat.QR_CODE ? PdfBarcodeSymbology.QrCode : decoded.BarcodeFormat == BarcodeFormat.CODE_128 ? PdfBarcodeSymbology.Code128 : throw Error(context, "DecoderBinding", "The decoder returned an unrequested symbol family.");
        var correction = PdfBarcodeErrorCorrection.None;
        if (symbology == PdfBarcodeSymbology.QrCode)
        {
            string? value = decoded.ResultMetadata != null && decoded.ResultMetadata.TryGetValue(ResultMetadataType.ERROR_CORRECTION_LEVEL, out var level) ? level?.ToString() : null;
            correction = value switch
            {
                "L" => PdfBarcodeErrorCorrection.Low,
                "M" => PdfBarcodeErrorCorrection.Medium,
                "Q" => PdfBarcodeErrorCorrection.Quartile,
                "H" => PdfBarcodeErrorCorrection.High,
                _ => throw Error(context, "DecoderBinding", "The QR decoder returned no supported error-correction identity.")};
        }

        if (decoded.ResultPoints == null || decoded.ResultPoints.Length == 0 || decoded.ResultPoints.Length > 16)
            throw Error(context, "DetectorGeometry", "The decoder returned no supported detector landmark inventory.");
        context.Consume(PdfResourceKind.ExpandedBytes, decoded.Text.Length * 4L + decoded.ResultPoints.Length * 32L + 512);
        return new(symbology, decoded.Text, correction, decoded.ResultPoints.Select(point => new PdfPoint(point.X, point.Y)));
    }

    private static PdfException Error(PdfOperationContext context, string code, string message)
    {
        var diagnostic = new PdfDiagnostic("Barcode." + code, message, PdfCapability.Barcodes, "managed.zxing-barcodes");
        context.Report(diagnostic);
        return new(diagnostic);
    }
}
