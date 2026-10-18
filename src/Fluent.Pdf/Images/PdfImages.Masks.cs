using System.Threading;

namespace Fluent.Pdf;
public static partial class PdfImages
{
    /// <summary>Attaches an exact matching one-bit explicit mask without changing either image stream.</summary>
    public static PdfImageAsset WithHardMask(PdfImageAsset image, PdfImageAsset stencil, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (image == null)
            throw new ArgumentNullException(nameof(image));
        if (stencil == null)
            throw new ArgumentNullException(nameof(stencil));
        PdfImageAsset? result = null;
        var report = PdfFont.Run("AttachImageMask", "managed.images", policy, cancellationToken, context =>
        {
            if (image.IsStencil || image.SoftMask != null || image.Definition.Entries.ContainsKey("Mask") || !stencil.IsStencil || stencil.Info.Width != image.Info.Width || stencil.Info.Height != image.Info.Height || stencil.Metadata.Orientation != PdfImageOrientation.Normal)
                throw Error(context, "Image.HardMask", "An explicit mask requires a matching normally oriented stencil and an image without another mask.");
            context.ConsumePixels(image.Info.Width, image.Info.Height);
            context.ConsumePixels(stencil.Info.Width, stencil.Info.Height);
            result = WithMaskCore(image, stencil.Definition, context);
        }, PdfCapability.ImageCodecs);
        Complete(result!, report);
        return result!;
    }

    /// <summary>Attaches a color key expressed as inclusive integer ranges in the encoded component domain, before Decode/color conversion.</summary>
    public static PdfImageAsset WithColorKey(PdfImageAsset image, int[] componentRanges, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (image == null)
            throw new ArgumentNullException(nameof(image));
        if (componentRanges == null)
            throw new ArgumentNullException(nameof(componentRanges));
        PdfImageAsset? result = null;
        var report = PdfFont.Run("AttachImageColorKey", "managed.images", policy, cancellationToken, context =>
        {
            if (image.IsStencil || image.SoftMask != null || image.Definition.Entries.ContainsKey("Mask") || componentRanges.Length != image.Info.Components * 2)
                throw Error(context, "Image.ColorKey", "A color key requires an unmasked color image and one complete range per component.");
            context.ConsumePixels(image.Info.Width, image.Info.Height);
            context.Consume(PdfResourceKind.ExpandedBytes, componentRanges.Length * 64L + 128);
            var values = new double[componentRanges.Length];
            long maximum = (1L << image.Info.BitsPerComponent) - 1;
            for (int c = 0; c < componentRanges.Length; c += 2)
            {
                int first = componentRanges[c], last = componentRanges[c + 1];
                if (first < 0 || last < first || last > maximum)
                    throw Error(context, "Image.ColorKey", "Color-key ranges must fit the declared component precision.");
                values[c] = first;
                values[c + 1] = last;
            }

            result = WithMaskCore(image, PdfGraphicsNumbers.Array(values), context);
        }, PdfCapability.ImageCodecs);
        Complete(result!, report);
        return result!;
    }

    private static PdfImageAsset WithMaskCore(PdfImageAsset image, PdfValue mask, PdfOperationContext context)
    {
        var entries = PdfWriterCore.CopyDictionary(image.Definition.Entries, context);
        entries["Mask"] = mask;
        var resource = PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.XObject, image.Definition.WithStreamDictionary(PdfValue.OwnedDictionary(entries)), image.Resource.MinimumVersion, PdfResourceSet.Empty, context);
        return new(resource, image.Info, image.Metadata, null, image.OriginalFile, image.MediaType);
    }
}
