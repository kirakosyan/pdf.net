using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Bounded optional-provider page and region rasterization.</summary>
public static class PdfRendering
{
    /// <summary>Renders an immutable snapshot using an explicitly registered provider; no desktop or runtime download is implied.</summary>
    public static PdfRaster RenderPage(PdfDocumentSnapshot source, int pageIndex, PdfProviderRegistry providers, PdfRenderOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        var provider = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
        options ??= new();
        PdfByteStore? samples = null;
        PdfRasterRequest? request = null;
        var report = PdfFont.Run("RenderPage", providers.GetIdentifier(PdfCapability.RenderPages), policy, cancellationToken, context =>
        {
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 256);
            // Normalization is a private preview only; an unmodified source retains its original byte history.
            using var graph = new PdfGraphEdit(source, new(allowSignatureInvalidation: true), context);
            request = new(pageIndex, graph.Snapshot.Pages[pageIndex].Source!, options, context);
            context.ConsumePixels(request.Width, request.Height);
            using var output = new PdfCodecBufferStream(context, request.ByteLength);
            using (var lease = new BorrowedStream(output, false, context))
                provider.Render(request, lease, context);
            context.Checkpoint();
            if (output.BufferedLength != request.ByteLength)
                throw PdfWriterCore.Error(context, "Render.OutputLength", "The renderer did not return the exact planned sample extent.");
            samples = output.Finish();
            for (long at = 0; at < samples.Length; at += 4)
            {
                if ((at & 65535) == 0)
                    context.Checkpoint();
                int b = samples.At(at), g = samples.At(at + 1), r = samples.At(at + 2), a = samples.At(at + 3);
                if (options.BackgroundRgb.HasValue && a != 255 || a == 0 && (b != 0 || g != 0 || r != 0))
                    throw PdfWriterCore.Error(context, "Render.OutputAlpha", "The renderer returned samples outside the declared alpha contract.");
                if (options.Color == PdfRasterColor.Gray && (r != g || g != b))
                    throw PdfWriterCore.Error(context, "Render.OutputColor", "A device-gray renderer returned unequal color channels.");
            }
        }, PdfCapability.RenderPages);
        return new(samples!, request!, report);
    }
}
