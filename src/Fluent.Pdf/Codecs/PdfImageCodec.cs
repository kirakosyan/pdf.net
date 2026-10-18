using System.IO;

namespace Fluent.Pdf;
/// <summary>An explicitly selected optional image stream codec.</summary>
public enum PdfImageCodec
{
    /// <summary>DCT JPEG image samples.</summary>
    Dct,
    /// <summary>JPEG 2000 image samples.</summary>
    Jpx,
    /// <summary>JBIG2 bitonal image samples.</summary>
    Jbig2
}

/// <summary>Immutable interleaved image geometry. Samples are packed most-significant bit first, with each row byte-aligned.</summary>
public sealed class PdfImageInfo
{
    /// <summary>Creates positive geometry with 1–4 components and 1–16 bits per component; arithmetic overflow is rejected.</summary>
    public PdfImageInfo(int width, int height, int components, int bitsPerComponent)
    {
        if (width <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        if (height <= 0)
            throw new ArgumentOutOfRangeException(nameof(height));
        if (components < 1 || components > 4)
            throw new ArgumentOutOfRangeException(nameof(components));
        if (bitsPerComponent < 1 || bitsPerComponent > 16)
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent));
        Width = width;
        Height = height;
        Components = components;
        BitsPerComponent = bitsPerComponent;
        RowBytes = ((long)width * components * bitsPerComponent + 7) / 8;
        try
        {
            ByteLength = checked(RowBytes * height);
        }
        catch (OverflowException)
        {
            throw new ArgumentOutOfRangeException(nameof(height), "Image sample storage exceeds the supported 64-bit range.");
        }
    }

    /// <summary>Gets the positive sample width.</summary>
    public int Width { get; }
    /// <summary>Gets the positive sample height.</summary>
    public int Height { get; }
    /// <summary>Gets the interleaved component count.</summary>
    public int Components { get; }
    /// <summary>Gets the precision of each unsigned component.</summary>
    public int BitsPerComponent { get; }
    /// <summary>Gets the byte-aligned row length.</summary>
    public long RowBytes { get; }
    /// <summary>Gets the complete sample byte count, including row padding.</summary>
    public long ByteLength { get; }
}

/// <summary>An explicitly registered image codec boundary. Stream leases expire on return and must never be retained.</summary>
public interface IPdfImageCodecProvider : IPdfProvider
{
    /// <summary>Reports declared algorithm/direction support; this is not production qualification.</summary>
    bool Supports(PdfImageCodec codec, bool encoding);
    /// <summary>Inspects bounded encoded headers without allocating an image; the SDK charges returned pixels before decoding.</summary>
    PdfImageInfo Inspect(PdfImageCodec codec, Stream encodedInput, PdfValue imageDictionary, PdfOperationContext context);
    /// <summary>Decodes into the SDK's bounded sample sink, polling the mandatory context and reporting every failure/loss.</summary>
    void Decode(PdfImageCodec codec, PdfImageInfo info, PdfValue imageDictionary, PdfValue decodeParameters, Stream encodedInput, Stream sampleOutput, PdfOperationContext context);
    /// <summary>Encodes exactly the supplied sample geometry into the SDK's bounded sink; input remains caller-owned.</summary>
    void Encode(PdfImageCodec codec, PdfImageInfo info, PdfValue parameters, Stream sampleInput, Stream encodedOutput, PdfOperationContext context);
}
