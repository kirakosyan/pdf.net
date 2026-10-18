using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>A detached immutable image, exact encoded samples and explicit display metadata.</summary>
public sealed class PdfImageAsset
{
    internal PdfImageAsset(PdfGraphicsResource resource, PdfImageInfo info, PdfImageMetadata metadata, PdfImageAsset? softMask = null, PdfByteStore? originalFile = null, string? mediaType = null)
    {
        Resource = resource;
        Info = info;
        Metadata = metadata;
        SoftMask = softMask;
        OriginalFile = originalFile;
        MediaType = mediaType;
    }

    /// <summary>Gets the exact reusable PDF image XObject, including its color/mask dependencies.</summary>
    public PdfGraphicsResource Resource { get; }
    /// <summary>Gets the exact detached PDF image stream, including color, Decode, masks and metadata.</summary>
    public PdfValue Definition => Resource.Definition;
    /// <summary>Gets encoded sample geometry before display orientation.</summary>
    public PdfImageInfo Info { get; }
    /// <summary>Gets immutable display and resolution metadata.</summary>
    public PdfImageMetadata Metadata { get; }
    /// <summary>Gets the optional separate unassociated alpha image.</summary>
    public PdfImageAsset? SoftMask { get; }
    /// <summary>Gets the original file media type, or null for authored/PDF-stream images.</summary>
    public string? MediaType { get; }
    /// <summary>Gets whether the source file is retained exactly for explicit export.</summary>
    public bool HasOriginalFile => OriginalFile != null;
    /// <summary>Gets whether the image is a one-bit paint stencil.</summary>
    public bool IsStencil => Resource.Definition.Entries.TryGetValue("ImageMask", out var mask) && mask.Kind == PdfValueKind.Boolean && mask.BooleanValue;
    /// <summary>Gets physical display aspect after orientation and nonsquare-pixel correction.</summary>
    public double DisplayAspect => SwapsAxes ? (double)Info.Height / Info.Width / Metadata.PixelAspectRatio : (double)Info.Width / Info.Height * Metadata.PixelAspectRatio;

    /// <summary>Gets the natural oriented size in points when physical resolution is known and representable; otherwise null.</summary>
    public PdfPageSize? NaturalSize
    {
        get
        {
            if (Metadata.HorizontalDpi == 0)
                return null;
            double w = Info.Width / Metadata.HorizontalDpi * 72, h = Info.Height / Metadata.VerticalDpi * 72;
            if (w <= 0 || h <= 0 || double.IsInfinity(w) || double.IsInfinity(h))
                return null;
            return SwapsAxes ? new PdfPageSize(h, w) : new PdfPageSize(w, h);
        }
    }

    /// <summary>Gets the successful bounded construction or extraction report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;
    internal PdfByteStore? OriginalFile { get; }
    internal bool SwapsAxes => (int)Metadata.Orientation >= 5;
    internal PdfMatrix OrientationMatrix => Metadata.Orientation switch
    {
        PdfImageOrientation.MirrorHorizontal => new(-1, 0, 0, 1, 1, 0),
        PdfImageOrientation.Rotate180 => new(-1, 0, 0, -1, 1, 1),
        PdfImageOrientation.MirrorVertical => new(1, 0, 0, -1, 0, 1),
        PdfImageOrientation.Transpose => new(0, -1, -1, 0, 1, 1),
        PdfImageOrientation.Rotate90 => new(0, -1, 1, 0, 0, 1),
        PdfImageOrientation.Transverse => new(0, 1, 1, 0, 0, 0),
        PdfImageOrientation.Rotate270 => new(0, 1, -1, 0, 1, 0),
        _ => PdfMatrix.Identity
    };

    /// <summary>Exports exact PDF filter bytes without decoding, color conversion or recompression, retaining caller ownership.</summary>
    public PdfOperationReport WriteEncoded(Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Export(output, false, policy, cancellationToken);
    /// <summary>Exports the exact retained image file; rejects assets without an original container.</summary>
    public PdfOperationReport WriteOriginalFile(Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Export(output, true, policy, cancellationToken);
    private PdfOperationReport Export(Stream output, bool original, PdfOperationPolicy? policy, CancellationToken token)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable caller stream is required.", nameof(output));
        if (original && OriginalFile == null)
            throw new InvalidOperationException("This image has no retained original file.");
        return PdfFont.Run("ExportImageBytes", "managed.images", policy, token, context =>
        {
            using var input = original ? OriginalFile!.Open(0, OriginalFile.Length) : Resource.Definition.OpenRawStream();
            using var source = new BorrowedStream(input, true, context);
            using var target = new BorrowedStream(output, false, context);
            context.Consume(PdfResourceKind.ExpandedBytes, 4096);
            var buffer = new byte[4096];
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                context.Checkpoint();
                target.Write(buffer, 0, read);
            }
        }, PdfCapability.ImageCodecs);
    }

    /// <summary>Decodes filter samples before Decode/color/alpha interpretation into an owned stream under one bounded operation. Optional image codecs must be explicit.</summary>
    public Stream OpenSamples(PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfByteStore? samples = null;
        PdfFont.Run("DecodeImageSamples", "managed.images", policy, cancellationToken, context =>
        {
            var definition = Resource.Definition;
            bool codec = PdfFilters.Inspect(definition).Any(f => PdfStreamFilters.Canonical(f.Name) == "CCITTFaxDecode" || PdfImageCodecPipeline.TryKind(PdfStreamFilters.Canonical(f.Name), out _));
            if (!codec)
                context.ConsumePixels(Info.Width, Info.Height);
            context.Consume(PdfResourceKind.InputBytes, definition.RawStreamLength);
            using var decoded = PdfFilters.Decode(definition, context, providers);
            samples = PdfImages.ReadExact(decoded, Info.ByteLength, context, false);
        }, PdfCapability.ImageCodecs);
        return samples!.Open(0, samples.Length);
    }
}
