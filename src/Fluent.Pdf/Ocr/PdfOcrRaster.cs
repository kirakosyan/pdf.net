using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit packed OCR raster byte order; no image format or alpha interpretation is guessed.</summary>
public enum PdfOcrPixelFormat
{
    /// <summary>One unsigned gray byte per pixel.</summary>
    Gray8,
    /// <summary>Three unsigned red, green, blue bytes per pixel.</summary>
    Rgb24,
    /// <summary>Four blue, green, red, alpha bytes; only opaque samples are accepted by the local source profile.</summary>
    Bgra32
}

/// <summary>An immutable explicitly captured top-left-origin raster, independent of a rendering engine or implicit image decoder.</summary>
public sealed class PdfOcrRaster
{
    internal readonly PdfByteStore Bytes;
    private PdfOcrRaster(PdfByteStore bytes, int width, int height, int stride, PdfOcrPixelFormat format, int dpi)
    {
        Bytes = bytes;
        Width = width;
        Height = height;
        Stride = stride;
        PixelFormat = format;
        Dpi = dpi;
    }

    /// <summary>Captures exactly height*stride bytes and one EOF probe at the borrowed current position; the caller stream stays open. No image decoder, native library or model is discovered.</summary>
    public static PdfOcrRaster Read(Stream input, int width, int height, int stride, PdfOcrPixelFormat pixelFormat, int dpi = 200, long maximumBytes = 67108864, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("Readable borrowed input is required.", nameof(input));
        Validate(width, height, stride, pixelFormat, dpi, maximumBytes);
        PdfOcrRaster? result = null;
        var report = PdfFont.Run("CaptureOcrRaster", "managed.ocr", policy, cancellationToken, c => result = ReadCore(input, width, height, stride, pixelFormat, dpi, maximumBytes, c), PdfCapability.RecognizeText);
        result!.OperationReport = report;
        return result;
    }

    internal static PdfOcrRaster ReadCore(Stream input, int width, int height, int stride, PdfOcrPixelFormat pixelFormat, int dpi, long maximumBytes, PdfOperationContext c)
    {
        long length = Validate(width, height, stride, pixelFormat, dpi, maximumBytes);
        c.Checkpoint();
        c.ConsumePixels(width, height);
        c.Consume(PdfResourceKind.ExpandedBytes, 65536);
        var buffer = new byte[65536];
        using var output = new PdfCodecBufferStream(c, length);
        using var lease = new BorrowedStream(input, true, c);
        long left = length;
        while (left > 0)
        {
            c.Checkpoint();
            int count = lease.Read(buffer, 0, (int)Math.Min(left, buffer.Length));
            if (count == 0)
                throw PdfWriterCore.Error(c, "Ocr.RasterLength", "The explicit raster ended before its declared geometry.");
            output.Write(buffer, 0, count);
            left -= count;
        }

        if (lease.ReadByte() != -1)
            throw PdfWriterCore.Error(c, "Ocr.RasterLength", "The explicit raster contains trailing bytes beyond its declared geometry.");
        return new(output.Finish(), width, height, stride, pixelFormat, dpi);
    }

    private static long Validate(int width, int height, int stride, PdfOcrPixelFormat format, int dpi, long maximum)
    {
        if (!Enum.IsDefined(typeof(PdfOcrPixelFormat), format))
            throw new ArgumentOutOfRangeException(nameof(format));
        int channels = format == PdfOcrPixelFormat.Gray8 ? 1 : format == PdfOcrPixelFormat.Rgb24 ? 3 : 4;
        if (width < 1 || height < 1 || width > 100000 || height > 100000 || stride < (long)width * channels || stride > (long)width * channels + 65536 || dpi < 72 || dpi > 600)
            throw new ArgumentOutOfRangeException(nameof(width));
        long length = (long)height * stride;
        if (maximum < 1 || maximum > int.MaxValue || length > maximum)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        return length;
    }

    /// <summary>Opens a read-only borrowed pixel lease under the mandatory provider context. The lease expires with that operation and must be disposed before the provider returns.</summary>
    public Stream OpenPixels(PdfOperationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        return context.BorrowExpandedInput(Bytes.Open(0, Bytes.Length));
    }

    /// <summary>Gets pixel width.</summary>
    public int Width { get; }
    /// <summary>Gets pixel height.</summary>
    public int Height { get; }
    /// <summary>Gets declared bytes per scanline including explicit padding.</summary>
    public int Stride { get; }
    /// <summary>Gets explicit sample order.</summary>
    public PdfOcrPixelFormat PixelFormat { get; }
    /// <summary>Gets explicit source DPI.</summary>
    public int Dpi { get; }
    /// <summary>Gets exact captured byte count.</summary>
    public long ByteLength => Bytes.Length;
    /// <summary>Gets the capture report, or null for an internal same-context raster.</summary>
    public PdfOperationReport? OperationReport { get; internal set; }
}
