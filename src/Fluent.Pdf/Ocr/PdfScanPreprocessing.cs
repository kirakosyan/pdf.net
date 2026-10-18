using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Original bounded opt-in gray scan correction, with immutable original preservation and complete inverse geometry.</summary>
public static class PdfScanPreprocessing
{
    /// <summary>Processes explicit raster samples. Gray conversion uses integer77R+150G+29B luma; resampling is deterministic nearest neighbor with white padding. Automatic orientation requires explicit OCR configuration, without implicit models.</summary>
    public static PdfScanPreprocessingResult Process(PdfOcrRaster original, PdfScanPreprocessingOptions options, PdfProviderRegistry? providers = null, PdfOcrOptions? orientationRecognition = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (original == null)
            throw new ArgumentNullException(nameof(original));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (options.Orientation == PdfScanOrientation.Automatic)
        {
            if (providers == null || orientationRecognition == null)
                throw new ArgumentException("Automatic orientation requires explicit OCR providers, language/models and finite recognition ceilings.");
            _ = providers.GetRequired<IPdfOcrProvider>(PdfCapability.RecognizeText);
        }

        PdfScanPreprocessingResult? result = null;
        var report = PdfFont.Run("PreprocessScan", "managed.scan", policy, cancellationToken, c => result = ProcessCore(original, options, providers, orientationRecognition, c), PdfCapability.RecognizeText);
        result!.OperationReport = report;
        result.Processed.OperationReport = report;
        return result;
    }

    /// <summary>Preprocesses, recognizes and analyzes layout under one root context. Explicit OCR configuration also supplies any requested orientation observations.</summary>
    public static PdfScanRecognitionResult Recognize(PdfOcrRaster original, PdfScanPreprocessingOptions preprocessing, PdfOcrOptions recognition, PdfProviderRegistry providers, PdfScanLayoutOptions? layout = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (original == null)
            throw new ArgumentNullException(nameof(original));
        if (preprocessing == null)
            throw new ArgumentNullException(nameof(preprocessing));
        if (recognition == null)
            throw new ArgumentNullException(nameof(recognition));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        var provider = providers.GetRequired<IPdfOcrProvider>(PdfCapability.RecognizeText);
        layout ??= new();
        PdfScanPreprocessingResult? scan = null;
        PdfOcrResult? observed = null;
        PdfScanLayoutResult? analyzed = null;
        var report = PdfFont.Run("RecognizeScan", "managed.scan", policy, cancellationToken, c =>
        {
            scan = ProcessCore(original, preprocessing, providers, recognition, c);
            observed = PdfOcr.RecognizeCore(scan.Processed, recognition, provider, c);
            analyzed = PdfScanLayout.AnalyzeCore(observed, scan.Processed.Width, scan.Processed.Height, layout, scan.Processed, c);
        }, PdfCapability.RecognizeText);
        scan!.OperationReport = report;
        scan.Processed.OperationReport = report;
        observed!.OperationReport = report;
        analyzed!.OperationReport = report;
        return new(scan, observed, analyzed, report);
    }

    internal static PdfScanPreprocessingResult ProcessCore(PdfOcrRaster original, PdfScanPreprocessingOptions options, PdfProviderRegistry? providers, PdfOcrOptions? recognition, PdfOperationContext c)
    {
        var work = new Work(c, options.MaximumPixelVisits);
        if (options.Dewarp != null && (options.Dewarp.SourceWidth != original.Width || options.Dewarp.SourceHeight != original.Height))
            throw Error("DewarpGeometry", "The explicit dewarp grid does not match exact original raster dimensions.");
        int width = original.Width, height = original.Height;
        work.Use((long)width * height);
        var pixels = Allocate(width, height, c);
        c.Consume(PdfResourceKind.ExpandedBytes, original.Stride + 128L);
        var row = new byte[original.Stride];
        using (var input = original.OpenPixels(c))
        {
            int channels = original.PixelFormat == PdfOcrPixelFormat.Gray8 ? 1 : original.PixelFormat == PdfOcrPixelFormat.Rgb24 ? 3 : 4;
            for (int y = 0; y < height; y++)
            {
                c.Checkpoint();
                int at = 0;
                while (at < row.Length)
                {
                    int count = input.Read(row, at, row.Length - at);
                    if (count == 0)
                        throw Error("RasterLength", "Immutable source raster ended before its declared extent.");
                    at += count;
                }

                for (int x = 0; x < width; x++)
                {
                    if ((x & 1023) == 0)
                        c.Checkpoint();
                    int p = x * channels;
                    if (channels == 4 && row[p + 3] != 255)
                        throw Error("Alpha", "Scan preprocessing requires opaque BGRA samples; no matte is inferred.");
                    pixels[y * width + x] = channels == 1 ? row[p] : (byte)((77 * row[p + (channels == 4 ? 2 : 0)] + 150 * row[p + 1] + 29 * row[p + (channels == 4 ? 0 : 2)] + 128) >> 8);
                }
            }

            if (input.ReadByte() != -1)
                throw Error("RasterLength", "Immutable source raster exceeds its declared extent.");
        }

        if (options.MedianDenoise)
        {
            work.Use((long)width * height * 9);
            var denoised = Allocate(width, height, c);
            c.Consume(PdfResourceKind.ExpandedBytes, 64);
            var nine = new byte[9];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    if ((x & 1023) == 0)
                        c.Checkpoint();
                    int n = 0;
                    for (int dy = -1; dy <= 1; dy++)
                        for (int dx = -1; dx <= 1; dx++)
                            nine[n++] = pixels[Math.Max(0, Math.Min(height - 1, y + dy)) * width + Math.Max(0, Math.Min(width - 1, x + dx))];
                    for (int i = 1; i < 9; i++)
                    {
                        byte value = nine[i];
                        int j = i;
                        while (j > 0 && nine[j - 1] > value)
                        {
                            nine[j] = nine[j - 1];
                            j--;
                        }

                        nine[j] = value;
                    }

                    denoised[y * width + x] = nine[4];
                }

            pixels = denoised;
        }

        if (options.ContrastLow != 0 || options.ContrastHigh != 255 || options.BinaryThreshold.HasValue)
        {
            work.Use(pixels.Length);
            int range = options.ContrastHigh - options.ContrastLow;
            for (int i = 0; i < pixels.Length; i++)
            {
                if ((i & 1023) == 0)
                    c.Checkpoint();
                int v = Math.Max(0, Math.Min(255, ((pixels[i] - options.ContrastLow) * 255 + range / 2) / range));
                pixels[i] = options.BinaryThreshold.HasValue ? (byte)(v < options.BinaryThreshold.Value ? 0 : 255) : (byte)v;
            }
        }

        if (options.Dewarp != null)
        {
            var grid = options.Dewarp;
            work.Use((long)grid.OutputWidth * grid.OutputHeight);
            var corrected = Allocate(grid.OutputWidth, grid.OutputHeight, c);
            for (int y = 0; y < grid.OutputHeight; y++)
                for (int x = 0; x < grid.OutputWidth; x++)
                {
                    if ((x & 1023) == 0)
                        c.Checkpoint();
                    var p = grid.MapPoint(new(x + 0.5, y + 0.5));
                    int sx = (int)Math.Floor(p.X), sy = (int)Math.Floor(p.Y);
                    corrected[y * grid.OutputWidth + x] = sx >= 0 && sy >= 0 && sx < width && sy < height ? pixels[sy * width + sx] : (byte)255;
                }

            pixels = corrected;
            width = grid.OutputWidth;
            height = grid.OutputHeight;
        }

        var orientation = options.Orientation;
        var scores = Array.Empty<PdfScanOrientationScore>();
        double? orientationMargin = null, skewMargin = null;
        if (orientation == PdfScanOrientation.Automatic)
        {
            c.Consume(PdfResourceKind.ExpandedBytes, 4096);
            c.Consume(PdfResourceKind.Objects, 5);
            scores = new PdfScanOrientationScore[4];
            for (int n = 0; n < 4; n++)
            {
                c.Checkpoint();
                var choice = (PdfScanOrientation)n;
                var candidate = Rotate(pixels, width, height, choice, work, c);
                var image = Capture(candidate.Pixels, candidate.Width, candidate.Height, original.Dpi, c);
                var observed = PdfOcr.RecognizeCore(image, recognition!, providers!.GetRequired<IPdfOcrProvider>(PdfCapability.RecognizeText), c);
                scores[n] = new(choice, observed.MeanConfidence);
                if (!observed.MeanConfidence.HasValue)
                    throw Error("OrientationScore", "Automatic orientation requires an engine mean confidence for every explicit candidate.");
            }

            var ranked = scores.OrderByDescending(s => s.EngineConfidence!.Value).ThenBy(s => (int)s.Orientation).ToArray();
            orientationMargin = (ranked[0].EngineConfidence!.Value - ranked[1].EngineConfidence!.Value) / 100;
            if (orientationMargin < options.MinimumOrientationMargin)
                throw Error("OrientationAmbiguous", "The requested orientation confidence margin was not met; no orientation fallback is selected.");
            orientation = ranked[0].Orientation;
        }

        var rotated = Rotate(pixels, width, height, orientation, work, c);
        pixels = rotated.Pixels;
        var inverse = RotationInverse(width, height, orientation);
        width = rotated.Width;
        height = rotated.Height;
        double skew = options.SkewDegrees;
        if (options.DetectSkew)
        {
            var detected = DetectSkew(pixels, width, height, options, work, c);
            skew = detected.Angle;
            skewMargin = detected.Margin;
            if (skewMargin < options.MinimumSkewMargin)
                throw Error("SkewAmbiguous", "The requested skew projection margin was not met; no unreported correction is selected.");
        }

        if (skew != 0)
        {
            double radians = skew * Math.PI / 180, cos = Math.Cos(radians), sin = Math.Sin(radians);
            double w = Math.Ceiling(Math.Abs(cos) * width + Math.Abs(sin) * height), h = Math.Ceiling(Math.Abs(sin) * width + Math.Abs(cos) * height);
            if (w > 100000 || h > 100000)
                throw Error("RasterLimit", "Expanded deskew geometry exceeds the finite scan profile.");
            int outputWidth = (int)w, outputHeight = (int)h;
            work.Use((long)outputWidth * outputHeight);
            var corrected = Allocate(outputWidth, outputHeight, c);
            var map = new PdfMatrix(1, 0, 0, 1, width / 2d, height / 2d).Multiply(new(cos, sin, -sin, cos, 0, 0)).Multiply(new(1, 0, 0, 1, -outputWidth / 2d, -outputHeight / 2d));
            for (int y = 0; y < outputHeight; y++)
                for (int x = 0; x < outputWidth; x++)
                {
                    if ((x & 1023) == 0)
                        c.Checkpoint();
                    var p = map.Transform(new(x + 0.5, y + 0.5));
                    int sx = (int)Math.Floor(p.X), sy = (int)Math.Floor(p.Y);
                    corrected[y * outputWidth + x] = sx >= 0 && sy >= 0 && sx < width && sy < height ? pixels[sy * width + sx] : (byte)255;
                }

            pixels = corrected;
            inverse = inverse.Multiply(map);
            width = outputWidth;
            height = outputHeight;
        }

        var processed = Capture(pixels, width, height, original.Dpi, c);
        c.Consume(PdfResourceKind.ExpandedBytes, 1024);
        c.Consume(PdfResourceKind.Objects, 2);
        return new(original, processed, options, new(original.Width, original.Height, width, height, inverse, options.Dewarp), orientation, skew, orientationMargin, skewMargin, scores, work.Visits);
        PdfException Error(string code, string message) => PdfWriterCore.Error(c, "Ocr.Scan" + code, message);
    }

    private static (double Angle, double Margin) DetectSkew(byte[] pixels, int width, int height, PdfScanPreprocessingOptions options, Work work, PdfOperationContext c)
    {
        int radius = (int)Math.Ceiling(Math.Sqrt((double)width * width + (double)height * height));
        c.Consume(PdfResourceKind.ExpandedBytes, (radius * 2L + 3) * 4 + 8192);
        var bins = new int[radius * 2 + 3];
        int steps = (int)Math.Floor(options.MaximumSkewDegrees / options.SkewStepDegrees);
        double bestAngle = 0;
        long best = -1, second = -1;
        Score(0);
        for (int n = 1; n <= steps; n++)
        {
            Score(-n * options.SkewStepDegrees);
            Score(n * options.SkewStepDegrees);
        }

        return (bestAngle, best <= 0 ? 0 : (best - Math.Max(0, second)) / (double)best);
        void Score(double angle)
        {
            c.Checkpoint();
            work.Use(bins.Length * 2L + ((width + options.AnalysisStride - 1L) / options.AnalysisStride) * ((height + options.AnalysisStride - 1L) / options.AnalysisStride));
            Array.Clear(bins, 0, bins.Length);
            double radians = angle * Math.PI / 180, cos = Math.Cos(radians), sin = Math.Sin(radians);
            for (int y = 0; y < height; y += options.AnalysisStride)
                for (int x = 0; x < width; x += options.AnalysisStride)
                {
                    if ((x & 1023) == 0)
                        c.Checkpoint();
                    if (pixels[y * width + x] >= 128)
                        continue;
                    int bin = radius + (int)Math.Round((y - height / 2d) * cos - (x - width / 2d) * sin, MidpointRounding.AwayFromZero);
                    bins[bin]++;
                }

            long score = 0;
            for (int i = 0; i < bins.Length; i++)
            {
                if ((i & 1023) == 0)
                    c.Checkpoint();
                score += (long)bins[i] * bins[i];
            }

            if (score > best)
            {
                second = best;
                best = score;
                bestAngle = angle;
            }
            else if (score > second)
                second = score;
        }
    }

    private static (byte[] Pixels, int Width, int Height) Rotate(byte[] pixels, int width, int height, PdfScanOrientation orientation, Work work, PdfOperationContext c)
    {
        if (orientation == PdfScanOrientation.None)
            return (pixels, width, height);
        int w = orientation == PdfScanOrientation.Clockwise180 ? width : height, h = orientation == PdfScanOrientation.Clockwise180 ? height : width;
        work.Use((long)w * h);
        var result = Allocate(w, h, c);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                if ((x & 1023) == 0)
                    c.Checkpoint();
                int sx = orientation == PdfScanOrientation.Clockwise90 ? y : orientation == PdfScanOrientation.Clockwise180 ? width - 1 - x : width - 1 - y;
                int sy = orientation == PdfScanOrientation.Clockwise90 ? height - 1 - x : orientation == PdfScanOrientation.Clockwise180 ? height - 1 - y : x;
                result[y * w + x] = pixels[sy * width + sx];
            }

        return (result, w, h);
    }

    private static PdfMatrix RotationInverse(int width, int height, PdfScanOrientation orientation) => orientation switch
    {
        PdfScanOrientation.None => PdfMatrix.Identity,
        PdfScanOrientation.Clockwise90 => new(0, -1, 1, 0, 0, height),
        PdfScanOrientation.Clockwise180 => new(-1, 0, 0, -1, width, height),
        PdfScanOrientation.Clockwise270 => new(0, 1, -1, 0, width, 0),
        _ => throw new InvalidOperationException("Automatic orientation must resolve before geometry construction.")};
    private static byte[] Allocate(int width, int height, PdfOperationContext c)
    {
        long count = (long)width * height;
        if (count < 1 || count > 67108864)
            throw PdfWriterCore.Error(c, "Ocr.ScanRasterLimit", "The scan profile permits at most64MiBof Gray8 samples per stage.");
        c.ConsumePixels(width, height);
        c.Consume(PdfResourceKind.Objects, 1);
        c.Consume(PdfResourceKind.ExpandedBytes, count + 128);
        return new byte[(int)count];
    }

    private static PdfOcrRaster Capture(byte[] pixels, int width, int height, int dpi, PdfOperationContext c)
    {
        using var input = new MemoryStream(pixels, false);
        return PdfOcrRaster.ReadCore(input, width, height, width, PdfOcrPixelFormat.Gray8, dpi, 67108864, c);
    }

    private sealed class Work
    {
        private readonly PdfOperationContext _context;
        private readonly long _maximum;
        internal long Visits;
        internal Work(PdfOperationContext context, long maximum)
        {
            _context = context;
            _maximum = maximum;
        }

        internal void Use(long count)
        {
            _context.Checkpoint();
            if (count < 0 || count > _maximum - Visits)
                throw PdfWriterCore.Error(_context, "Ocr.ScanWorkLimit", "The explicit cumulative scan pixel-visit ceiling was exceeded.");
            Visits += count;
        }
    }
}
