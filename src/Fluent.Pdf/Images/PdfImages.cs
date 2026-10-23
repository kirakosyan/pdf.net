using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Bounded image construction and byte-preserving packaging, independent of platform graphics APIs.</summary>
public static partial class PdfImages
{
    /// <summary>Captures exact packed samples with explicit color, alpha, Decode and display metadata. No lossy compression is selected.</summary>
    public static PdfImageAsset FromSamples(Stream input, PdfImageInfo info, PdfColorSpace colorSpace, PdfImageAsset? softMask = null, double[]? decode = null, bool interpolate = false, PdfImageMetadata? metadata = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        ValidateInput(input, info);
        if (colorSpace == null)
            throw new ArgumentNullException(nameof(colorSpace));
        PdfImageAsset? result = null;
        var report = PdfFont.Run("CreateSampleImage", "managed.images", policy, cancellationToken, context =>
        {
            Validate(info, colorSpace, softMask, decode, context);
            context.ConsumePixels(info.Width, info.Height);
            context.Consume(PdfResourceKind.ExpandedBytes, (decode?.Length ?? 0) * 16L + 64);
            var capturedDecode = decode?.ToArray();
            using var lease = new BorrowedStream(input, true, context);
            var bytes = ReadExact(lease, info.ByteLength, context, false);
            result = Build(bytes, info, colorSpace.Definition, softMask, capturedDecode, interpolate, metadata ?? new(), false, context, minimumVersion: colorSpace.MinimumVersion);
        }, PdfCapability.ImageCodecs);
        Complete(result!, report);
        return result!;
    }

    /// <summary>Creates a one-bit stencil; zero samples paint by default and inversion reverses that interpretation.</summary>
    public static PdfImageAsset FromStencil(Stream input, int width, int height, bool invert = false, PdfImageMetadata? metadata = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        var info = new PdfImageInfo(width, height, 1, 1);
        ValidateInput(input, info);
        PdfImageAsset? result = null;
        var report = PdfFont.Run("CreateStencilImage", "managed.images", policy, cancellationToken, context =>
        {
            context.ConsumePixels(width, height);
            using var lease = new BorrowedStream(input, true, context);
            result = Build(ReadExact(lease, info.ByteLength, context, false), info, null, null, invert ? new double[] { 1, 0 } : null, false, metadata ?? new(), true, context);
        }, PdfCapability.ImageCodecs);
        Complete(result!, report);
        return result!;
    }

    /// <summary>Separates exact unassociated RGBA samples into RGB and grayscale alpha planes without changing component precision.</summary>
    public static PdfImageAsset FromRgba(Stream input, int width, int height, int bitsPerComponent = 8, PdfColorSpace? colorSpace = null, PdfImageMetadata? metadata = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (bitsPerComponent != 8 && bitsPerComponent != 16)
            throw new ArgumentOutOfRangeException(nameof(bitsPerComponent));
        var info = new PdfImageInfo(width, height, 4, bitsPerComponent);
        ValidateInput(input, info);
        colorSpace ??= PdfColorSpace.Rgb;
        PdfImageAsset? result = null;
        var report = PdfFont.Run("CreateAlphaImage", "managed.images", policy, cancellationToken, context =>
        {
            result = FromRgbaCore(input, width, height, bitsPerComponent, colorSpace, metadata ?? new(), context);
        }, PdfCapability.ImageCodecs);
        Complete(result!, report);
        Complete(result!.SoftMask!, report);
        return result!;
    }

    internal static PdfImageAsset FromRgbaCore(Stream input, int width, int height, int bitsPerComponent, PdfColorSpace colorSpace, PdfImageMetadata metadata, PdfOperationContext context, bool pixelsAlreadyCharged = false)
    {
        var info = new PdfImageInfo(width, height, 4, bitsPerComponent);
        if (colorSpace.Components != 3 || colorSpace.IsPattern)
            throw Error(context, "Image.ColorComponents", "RGBA input requires an explicit three-component color space.");
        if (!pixelsAlreadyCharged)
        {
            context.ConsumePixels(width, height);
            context.ConsumePixels(width, height);
        }
        using var lease = new BorrowedStream(input, true, context);
        var data = ReadExact(lease, info.ByteLength, context, false);
        var colors = new PdfByteBuilder(context);
        var alpha = new PdfByteBuilder(context);
        int componentBytes = bitsPerComponent / 8;
        for (long p = 0; p < data.Length; p += componentBytes * 4)
        {
            context.Checkpoint();
            for (int i = 0; i < componentBytes * 3; i++)
                colors.Add((byte)data.At(p + i));
            for (int i = 0; i < componentBytes; i++)
                alpha.Add((byte)data.At(p + componentBytes * 3 + i));
        }

        var mask = Build(alpha.Build(), new(width, height, 1, bitsPerComponent), PdfColorSpace.Gray.Definition, null, null, false, new(), false, context);
        return Build(colors.Build(), new(width, height, 3, bitsPerComponent), colorSpace.Definition, mask, null, false, metadata ?? new(), false, context, minimumVersion: colorSpace.MinimumVersion);
    }

    /// <summary>Preserves exact encoded PDF filter bytes with explicit inspected geometry and a bounded standard filter chain.</summary>
    public static PdfImageAsset FromEncoded(Stream input, PdfImageInfo info, PdfColorSpace colorSpace, PdfFilter[] filters, PdfImageAsset? softMask = null, double[]? decode = null, PdfImageMetadata? metadata = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        ValidateInput(input, info);
        if (colorSpace == null)
            throw new ArgumentNullException(nameof(colorSpace));
        if (filters == null || filters.Length < 1 || filters.Length > 16 || filters.Any(f => f == null))
            throw new ArgumentException("Supply one through sixteen explicit filters.", nameof(filters));
        PdfImageAsset? result = null;
        var report = PdfFont.Run("PackageEncodedImage", "managed.images", policy, cancellationToken, context =>
        {
            Validate(info, colorSpace, softMask, decode, context);
            context.ConsumePixels(info.Width, info.Height);
            context.Consume(PdfResourceKind.ExpandedBytes, filters.Length * 128L + (decode?.Length ?? 0) * 16L + 128);
            var capturedFilters = filters.Select(f => new PdfFilter(PdfStreamFilters.Canonical(f.Name), f.Parameters)).ToArray();
            var capturedDecode = decode?.ToArray();
            foreach (var filter in capturedFilters)
                if (!new[]
                {
                    "FlateDecode",
                    "LZWDecode",
                    "ASCIIHexDecode",
                    "ASCII85Decode",
                    "RunLengthDecode",
                    "CCITTFaxDecode",
                    "DCTDecode",
                    "JPXDecode",
                    "JBIG2Decode"
                }.Contains(PdfStreamFilters.Canonical(filter.Name)))
                    throw Error(context, "Image.Filter", "The image declares a filter outside the standard image packaging profile.");
            int imageFilters = 0;
            for (int i = 0; i < capturedFilters.Length; i++)
            {
                string name = capturedFilters[i].Name;
                if (name == "DCTDecode" || name == "JPXDecode" || name == "JBIG2Decode" || name == "CCITTFaxDecode")
                {
                    if (++imageFilters > 1 || i != capturedFilters.Length - 1)
                        throw Error(context, "Image.FilterOrder", "An image compression filter must be the single terminal image codec in the chain.");
                    if ((name == "CCITTFaxDecode" || name == "JBIG2Decode") && (info.Components != 1 || info.BitsPerComponent != 1) || name == "DCTDecode" && (info.BitsPerComponent != 8 || info.Components != 1 && info.Components != 3 && info.Components != 4))
                        throw Error(context, "Image.CodecGeometry", "Image geometry disagrees with the selected compression profile.");
                }
            }

            using var lease = new BorrowedStream(input, true, context);
            var bytes = PdfByteStore.Read(lease, context);
            if (bytes.Length == 0)
                throw Error(context, "Image.EncodedData", "Encoded image data must be nonempty.");
            result = Build(bytes, info, colorSpace.Definition, softMask, capturedDecode, false, metadata ?? new(), false, context, capturedFilters, minimumVersion: colorSpace.MinimumVersion);
        }, PdfCapability.ImageCodecs);
        Complete(result!, report);
        return result!;
    }

    internal static PdfImageAsset Build(PdfByteStore bytes, PdfImageInfo info, PdfValue? color, PdfImageAsset? softMask, double[]? decode, bool interpolate, PdfImageMetadata metadata, bool stencil, PdfOperationContext context, PdfFilter[]? filters = null, PdfByteStore? originalFile = null, string? mediaType = null, string minimumVersion = "1.2", string? renderingIntent = null, bool forceDisplayMetadata = false)
    {
        context.Consume(PdfResourceKind.Objects, 1);
        context.Consume(PdfResourceKind.ExpandedBytes, 1024 + (decode?.Length ?? 0) * 32L);
        var values = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
        {
            ["Type"] = PdfValue.Name("XObject"),
            ["Subtype"] = PdfValue.Name("Image"),
            ["Width"] = PdfValue.Integer(info.Width),
            ["Height"] = PdfValue.Integer(info.Height),
            ["BitsPerComponent"] = PdfValue.Integer(info.BitsPerComponent)
        };
        if (stencil)
            values["ImageMask"] = PdfValue.Boolean(true);
        else
            values["ColorSpace"] = color!;
        if (softMask != null)
            values["SMask"] = softMask.Resource.Definition;
        if (decode != null)
            values["Decode"] = PdfGraphicsNumbers.Array(decode);
        if (interpolate)
            values["Interpolate"] = PdfValue.Boolean(true);
        if (renderingIntent != null)
            values["Intent"] = PdfValue.Name(renderingIntent);
        var displayMetadata = PdfImageMetadataCodec.Write(metadata, context, forceDisplayMetadata);
        if (displayMetadata != null)
            values["Metadata"] = displayMetadata;
        if (filters != null)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, filters.Length * 128L + 128);
            values["Filter"] = filters.Length == 1 ? PdfValue.Name(filters[0].Name) : PdfValue.Array(filters.Select(f => PdfValue.Name(f.Name)).ToArray());
            if (filters.Any(f => f.Parameters.Kind != PdfValueKind.Null))
                values["DecodeParms"] = filters.Length == 1 ? filters[0].Parameters : PdfValue.Array(filters.Select(f => f.Parameters).ToArray());
        }

        var definition = PdfValue.StreamValue(PdfValue.OwnedDictionary(values), bytes, 0, bytes.Length);
        var resource = PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.XObject, definition, PdfVersionNumber.Text(Math.Max(PdfVersionNumber.Parse(minimumVersion), info.BitsPerComponent == 16 ? 15 : 12)), PdfResourceSet.Empty, context);
        var result = new PdfImageAsset(resource, info, metadata, softMask, originalFile, mediaType);
        if (result.DisplayAspect <= 0 || double.IsInfinity(result.DisplayAspect) || double.IsNaN(result.DisplayAspect))
            throw Error(context, "Image.DisplayAspect", "Image display metadata exceeds a finite positive aspect ratio.");
        return result;
    }

    internal static void Complete(PdfImageAsset image, PdfOperationReport report)
    {
        image.OperationReport = report;
        image.Resource.OperationReport = report;
    }

    internal static PdfByteStore ReadExact(Stream input, long expected, PdfOperationContext context, bool chargeInput)
    {
        if (expected < 0)
            throw new ArgumentOutOfRangeException(nameof(expected));
        context.Consume(PdfResourceKind.ExpandedBytes, 4096);
        var buffer = new byte[4096];
        var result = new PdfByteBuilder(context);
        long remaining = expected;
        while (remaining > 0)
        {
            context.Checkpoint();
            int requested = (int)Math.Min(buffer.Length, remaining);
            int read = chargeInput ? context.ReadBounded(input, buffer, 0, requested, PdfResourceKind.InputBytes) : input.Read(buffer, 0, requested);
            if (read == 0)
                throw Error(context, "Image.SampleLength", "Image samples are shorter than the declared packed geometry.");
            for (int i = 0; i < read; i++)
                result.Add(buffer[i]);
            remaining -= read;
        }

        context.Checkpoint();
        int extra = input.ReadByte();
        if (chargeInput && extra >= 0)
            context.Consume(PdfResourceKind.InputBytes, 1);
        if (extra >= 0)
            throw Error(context, "Image.SampleLength", "Image samples exceed the declared packed geometry.");
        return result.Build();
    }

    private static void ValidateInput(Stream input, PdfImageInfo info)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable caller stream is required.", nameof(input));
        if (info == null)
            throw new ArgumentNullException(nameof(info));
    }

    internal static void Validate(PdfImageInfo info, PdfColorSpace color, PdfImageAsset? mask, double[]? decode, PdfOperationContext context)
    {
        if (!new[]
        {
            1,
            2,
            4,
            8,
            16
        }.Contains(info.BitsPerComponent))
            throw Error(context, "Image.Precision", "PDF image samples require 1, 2, 4, 8 or 16 bits per component.");
        if (color.IsPattern || color.Components != info.Components)
            throw Error(context, "Image.ColorComponents", "Image sample components disagree with the explicit non-pattern color space.");
        if (mask != null && (mask.IsStencil || mask.Info.Components != 1 || mask.Info.Width != info.Width || mask.Info.Height != info.Height || mask.Info.BitsPerComponent != 8 && mask.Info.BitsPerComponent != 16 || mask.SoftMask != null || mask.Resource.Definition.Entries["ColorSpace"].Kind != PdfValueKind.Name || mask.Resource.Definition.Entries["ColorSpace"].NameValue != "DeviceGray"))
            throw Error(context, "Image.SoftMask", "A soft mask requires matching geometry, 8/16-bit DeviceGray and no nested mask.");
        if (decode != null)
        {
            if (decode.Length != info.Components * 2)
                throw Error(context, "Image.Decode", "Decode requires one finite range per sample component.");
            foreach (double value in decode)
                if (double.IsInfinity(value) || double.IsNaN(value))
                    throw Error(context, "Image.Decode", "Decode entries must be finite.");
        }
    }

    internal static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, code, message);
    internal static string IntentName(int value) => value switch
    {
        0 => "Perceptual",
        1 => "RelativeColorimetric",
        2 => "Saturation",
        3 => "AbsoluteColorimetric",
        _ => throw new ArgumentOutOfRangeException(nameof(value))};
}
