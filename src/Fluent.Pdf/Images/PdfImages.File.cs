using System.IO;
using System.Threading;

namespace Fluent.Pdf;
public static partial class PdfImages
{
    /// <summary>Reads an original PNG or JPEG file offline with explicit orientation and lossless-transcode policy, retaining caller ownership and exact original bytes.</summary>
    public static PdfImageAsset Read(Stream input, PdfImageReadOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable caller image stream is required.", nameof(input));
        options ??= new();
        PdfImageAsset? result = null;
        var report = PdfFont.Run("ReadImageFile", "managed.images", policy, cancellationToken, context =>
        {
            using var lease = new BorrowedStream(input, true, context);
            var bytes = PdfByteStore.Read(lease, context);
            if (bytes.Length >= 8 && bytes.At(0) == 137 && bytes.At(1) == 80 && bytes.At(2) == 78 && bytes.At(3) == 71 && bytes.At(4) == 13 && bytes.At(5) == 10 && bytes.At(6) == 26 && bytes.At(7) == 10)
                result = PdfImagePng.Read(bytes, options, context);
            else if (bytes.Length >= 4 && bytes.At(0) == 255 && bytes.At(1) == 216)
                result = PdfImageJpeg.Read(bytes, options, context);
            else
                throw Error(context, "Image.FileFormat", "The managed image-file service requires PNG or JPEG; other formats need an explicit adapter.");
        }, PdfCapability.ImageCodecs);
        Complete(result!, report);
        if (result!.SoftMask != null)
            Complete(result.SoftMask, report);
        return result;
    }
}
