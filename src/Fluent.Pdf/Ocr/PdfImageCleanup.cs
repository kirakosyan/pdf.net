using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Original bounded image cleanup over explicitly captured samples, without implicit decoders, models or page deletion.</summary>
public static class PdfImageCleanup
{
    /// <summary>Preserves exact source samples. Applies explicit device gray, median and threshold before border cropping and nearest-neighbor resampling, under one mandatory context.</summary>
    public static PdfImageCleanupResult Process(PdfOcrRaster original, PdfImageCleanupOptions options, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (original == null || options == null) throw new ArgumentNullException(original == null ? nameof(original) : nameof(options));
        PdfImageCleanupResult? result = null;
        var report = PdfFont.Run("CleanupImage", "managed.image-cleanup", policy, cancellationToken, c => result = ProcessCore(original, options, c), PdfCapability.RecognizeText);
        result!.OperationReport = report;
        if (!ReferenceEquals(result.Processed, original)) result.Processed.OperationReport = report;
        return result;
    }

    private static PdfImageCleanupResult ProcessCore(PdfOcrRaster original, PdfImageCleanupOptions options, PdfOperationContext c)
    {
        int width = original.Width - options.CropLeft - options.CropRight, height = original.Height - options.CropTop - options.CropBottom;
        if (width < 1 || height < 1) throw Error("Crop", "Explicit crop must retain a nonempty source rectangle.");
        bool gray = options.Grayscale || options.MedianDenoise || options.BinaryThreshold.HasValue;
        if (gray && !options.Grayscale && original.PixelFormat != PdfOcrPixelFormat.Gray8) throw Error("ColorAuthority", "Median and threshold on color require explicit grayscale conversion.");
        int ow = options.OutputWidth ?? width, oh = options.OutputHeight ?? height;
        long visits = 0;
        PdfBlankPageObservation? before = options.BlankDetection == null ? null : Analyze(original, options.BlankDetection);
        var processed = original;
        if (gray)
        {
            if (visits == options.MaximumPixelVisits) throw Error("WorkLimit", "The explicit pixel-visit ceiling is exhausted.");
            var scan = PdfScanPreprocessing.ProcessCore(original, new(medianDenoise: options.MedianDenoise, binaryThreshold: options.BinaryThreshold, maximumPixelVisits: options.MaximumPixelVisits - visits), null, null, c);
            visits += scan.PixelVisits;
            processed = scan.Processed;
        }
        bool geometry = width != original.Width || height != original.Height || ow != width || oh != height;
        if (geometry)
        {
            Use((long)ow * oh);
            int channels = Channels(processed); long length = (long)ow * oh * channels;
            if (length > 67108864) throw Error("Extent", "A processed raster stage is limited to64MiB.");
            c.ConsumePixels(ow, oh); c.Consume(PdfResourceKind.Objects, 1); c.Consume(PdfResourceKind.ExpandedBytes, length + 128);
            var pixels = new byte[(int)length];
            for (int y = 0; y < oh; y++)
                for (int x = 0; x < ow; x++)
                {
                    if ((x & 1023) == 0) c.Checkpoint();
                    int sx = options.CropLeft + (int)(((2L * x + 1) * width) / (2L * ow));
                    int sy = options.CropTop + (int)(((2L * y + 1) * height) / (2L * oh));
                    long p = (long)sy * processed.Stride + sx * channels;
                    for (int k = 0; k < channels; k++) pixels[(y * ow + x) * channels + k] = (byte)processed.Bytes.At(p + k);
                }
            using var input = new MemoryStream(pixels, false);
            processed = PdfOcrRaster.ReadCore(input, ow, oh, ow * channels, processed.PixelFormat, original.Dpi, 67108864, c);
        }
        if (gray || geometry) c.Report(new("Cleanup.ExplicitLoss", "Explicit sample conversion, filtering, cropping or resampling may remove detail. Original samples remain available.", lossCategory: PdfLossCategory.FidelityReduction));
        var after = options.BlankDetection == null ? null : ReferenceEquals(original, processed) ? before : Analyze(processed, options.BlankDetection);
        return new(original, processed, options, new((double)width / ow, 0, 0, (double)height / oh, options.CropLeft, options.CropTop), before, after, visits);

        void Use(long count)
        {
            c.Checkpoint();
            if (count > options.MaximumPixelVisits - visits) throw Error("WorkLimit", "The explicit cumulative pixel-visit ceiling was exceeded.");
            visits += count;
        }
        PdfBlankPageObservation Analyze(PdfOcrRaster raster, PdfBlankPageOptions settings)
        {
            long count = (long)raster.Width * raster.Height; Use(count); c.ConsumePixels(raster.Width, raster.Height);
            int channels = Channels(raster), br = settings.BackgroundRgb >> 16, bg = (settings.BackgroundRgb >> 8) & 255, bb = settings.BackgroundRgb & 255;
            long foreground = 0;
            for (int y = 0; y < raster.Height; y++)
                for (int x = 0; x < raster.Width; x++)
                {
                    if ((x & 1023) == 0) c.Checkpoint();
                    long p = (long)y * raster.Stride + x * channels;
                    if (channels == 4 && raster.Bytes.At(p + 3) != 255) throw Error("Alpha", "Blank analysis requires opaque pixels; no matte is inferred.");
                    int r = raster.Bytes.At(p + (channels == 4 ? 2 : 0)), g = raster.Bytes.At(p + (channels == 1 ? 0 : 1)), b = raster.Bytes.At(p + (channels == 1 || channels == 4 ? 0 : 2));
                    if (Math.Max(Math.Abs(r - br), Math.Max(Math.Abs(g - bg), Math.Abs(b - bb))) > settings.ChannelTolerance) foreground++;
                }
            c.Consume(PdfResourceKind.Objects, 1); c.Consume(PdfResourceKind.ExpandedBytes, 128);
            return new(count, foreground, settings);
        }
        PdfException Error(string code, string message) => PdfWriterCore.Error(c, "Cleanup." + code, message);
    }
    private static int Channels(PdfOcrRaster raster) => raster.PixelFormat == PdfOcrPixelFormat.Gray8 ? 1 : raster.PixelFormat == PdfOcrPixelFormat.Rgb24 ? 3 : 4;
}
