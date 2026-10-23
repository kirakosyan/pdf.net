using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit raster-file families. Optional codec availability is checked before source conversion.</summary>
public enum PdfRasterFileFormat
{
    /// <summary>Static PNG; APNG requires another explicitly qualified profile.</summary>
    Png,
    /// <summary>JPEG.</summary>
    Jpeg,
    /// <summary>Optional BMP file decoder.</summary>
    Bmp,
    /// <summary>Optional GIF frame decoder.</summary>
    Gif,
    /// <summary>Optional TIFF/multipage TIFF decoder.</summary>
    Tiff,
    /// <summary>Optional WebP frame decoder.</summary>
    WebP,
    /// <summary>Optional HEIF/HEIC frame decoder.</summary>
    Heif
}

/// <summary>One borrowed file and an explicit ordered frame selection. The caller retains stream ownership until import completes.</summary>
public sealed class PdfRasterInput
{
    internal Stream Input { get; }

    /// <summary>Captures one readable file, one to4096 frame indices and explicit animation-frame authority. No file is read here.</summary>
    public PdfRasterInput(Stream input, PdfRasterFileFormat format, IEnumerable<int> frames, bool allowAnimationFrames = false)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable borrowed raster file is required.", nameof(input));
        if (!Enum.IsDefined(typeof(PdfRasterFileFormat), format))
            throw new ArgumentOutOfRangeException(nameof(format));
        if (frames == null)
            throw new ArgumentNullException(nameof(frames));
        var selected = new List<int>();
        foreach (int frame in frames)
        {
            if (frame < 0 || frame > 65534 || selected.Count == 4096)
                throw new ArgumentOutOfRangeException(nameof(frames));
            selected.Add(frame);
        }

        if (selected.Count == 0)
            throw new ArgumentException("An explicit nonempty ordered frame selection is required.", nameof(frames));
        Input = input;
        Format = format;
        Frames = selected.AsReadOnly();
        AllowAnimationFrames = allowAnimationFrames;
    }

    /// <summary>Gets declared content format, never inferred from a filename.</summary>
    public PdfRasterFileFormat Format { get; }
    /// <summary>Gets ordered zero-based frames, including explicit repetitions.</summary>
    public IReadOnlyList<int> Frames { get; }
    /// <summary>Gets whether explicitly selected animation frames may be used.</summary>
    public bool AllowAnimationFrames { get; }
}

/// <summary>One complete unassociated RGBA8 frame with explicit orientation, density and RGB/ICC interpretation.</summary>
public sealed class PdfRasterFrameInfo
{
    /// <summary>Captures positive geometry and RGB/CalRGB/three-channel ICC color; samples must be complete decoded canvas frames, not animation deltas.</summary>
    public PdfRasterFrameInfo(int width, int height, PdfImageMetadata? metadata = null, PdfColorSpace? colorSpace = null)
    {
        Samples = new(width, height, 4, 8);
        Metadata = metadata ?? new();
        ColorSpace = colorSpace ?? PdfColorSpace.Rgb;
        var definition = ColorSpace.Definition;
        bool rgb = definition.Kind == PdfValueKind.Name && definition.NameValue == "DeviceRGB" || definition.Kind == PdfValueKind.Array && definition.Items.Count > 0 && definition.Items[0].Kind == PdfValueKind.Name && (definition.Items[0].NameValue == "ICCBased" || definition.Items[0].NameValue == "CalRGB");
        if (!rgb || ColorSpace.Components != 3 || ColorSpace.IsPattern)
            throw new ArgumentException("An explicit RGB/CalRGB/three-channel ICC interpretation is required.", nameof(colorSpace));
    }

    /// <summary>Gets complete packed RGBA8 geometry.</summary>
    public PdfImageInfo Samples { get; }
    /// <summary>Gets exact declared orientation/density.</summary>
    public PdfImageMetadata Metadata { get; }
    /// <summary>Gets sample color interpretation; no implicit conversion occurs.</summary>
    public PdfColorSpace ColorSpace { get; }
}

/// <summary>Finite inspected file frames, before sample allocation.</summary>
public sealed class PdfRasterFileInfo
{
    /// <summary>Captures one to4096 complete frame descriptors and explicit animation classification.</summary>
    public PdfRasterFileInfo(IEnumerable<PdfRasterFrameInfo> frames, bool isAnimation = false)
    {
        if (frames == null)
            throw new ArgumentNullException(nameof(frames));
        var captured = new List<PdfRasterFrameInfo>();
        foreach (var frame in frames)
        {
            if (frame == null || captured.Count == 4096)
                throw new ArgumentException("One to4096 bounded frames are required.", nameof(frames));
            captured.Add(frame);
        }

        if (captured.Count == 0)
            throw new ArgumentException("One to4096 bounded frames are required.", nameof(frames));
        Frames = captured.AsReadOnly();
        IsAnimation = isAnimation;
    }

    /// <summary>Gets immutable frame descriptors.</summary>
    public IReadOnlyList<PdfRasterFrameInfo> Frames { get; }
    /// <summary>Gets whether the container is an animation; selection then requires explicit authority.</summary>
    public bool IsAnimation { get; }
}

/// <summary>Explicit optional file codec. Native decoders require an appropriately isolated provisioned host; no codec may discover/download dependencies.</summary>
public interface IPdfRasterDocumentDecoder : IPdfProvider
{
    /// <summary>Reports exact installed format support before conversion.</summary>
    bool Supports(PdfRasterFileFormat format);
    /// <summary>Checks content/format and finite frame metadata under the context before allocating decoded samples.</summary>
    PdfRasterFileInfo Inspect(Stream file, PdfRasterFileFormat format, PdfOperationContext context);
    /// <summary>Decodes exactly one inspected complete RGBA8 frame, preserving declared color/alpha and polling the same context. Leases expire on return.</summary>
    void DecodeFrame(Stream file, PdfRasterFileFormat format, int frame, PdfRasterFrameInfo info, Stream rgbaOutput, PdfOperationContext context);
}

/// <summary>Explicit physical page, fallback density, margins and managed PNG/JPEG packaging policy.</summary>
public sealed class PdfRasterImportOptions
{
    /// <summary>Captures natural-density pages or an exact fixed page with contain fitting. Unknown density uses the explicit fallback DPI. Alpha is preserved.</summary>
    public PdfRasterImportOptions(PdfPageSize? pageSize = null, double fallbackDpi = 72, double margin = 0, int maximumPages = 1000, PdfImageReadOptions? imageOptions = null)
    {
        PdfGraphicsNumbers.Finite(fallbackDpi);
        PdfGraphicsNumbers.Finite(margin);
        if (fallbackDpi < 1 || fallbackDpi > 9600 || margin < 0 || maximumPages < 1 || maximumPages > 10000)
            throw new ArgumentOutOfRangeException(nameof(fallbackDpi));
        if (pageSize.HasValue && (!pageSize.Value.IsValid || pageSize.Value.Width > 14400 || pageSize.Value.Height > 14400))
            throw new ArgumentOutOfRangeException(nameof(pageSize), "This profile uses unscaled page boxes no larger than14400 points.");
        if (pageSize.HasValue && (pageSize.Value.Width <= margin * 2 || pageSize.Value.Height <= margin * 2))
            throw new ArgumentException("Page margins require positive content area.", nameof(margin));
        PageSize = pageSize;
        FallbackDpi = fallbackDpi;
        Margin = margin;
        MaximumPages = maximumPages;
        ImageOptions = imageOptions ?? new();
    }

    /// <summary>Gets fixed page geometry or null for natural physical size.</summary>
    public PdfPageSize? PageSize { get; }
    /// <summary>Gets explicit fallback samples per inch.</summary>
    public double FallbackDpi { get; }
    /// <summary>Gets physical margin in points.</summary>
    public double Margin { get; }
    /// <summary>Gets selected-page ceiling.</summary>
    public int MaximumPages { get; }
    /// <summary>Gets PNG/JPEG encoded-color/orientation/lossless-transcode choices.</summary>
    public PdfImageReadOptions ImageOptions { get; }
}

/// <summary>Original bounded raster-document composition with native PNG/JPEG packaging and explicit optional frame codecs.</summary>
public static class PdfRasterDocuments
{
    private sealed class Plan
    {
        internal PdfRasterInput Source = null!;
        internal PdfByteStore Bytes = null!;
        internal PdfImageAsset? Image;
        internal PdfRasterFileInfo? Info;
        internal readonly Dictionary<int, PdfImageAsset> Frames = new();
    }

    /// <summary>Creates an owned editable document after every selected frame succeeds. Codec availability is checked before reading any input.</summary>
    public static PdfDocument Create(IEnumerable<PdfRasterInput> inputs, PdfRasterImportOptions? options = null, PdfProviderRegistry? codecs = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Run(inputs, options, codecs, null, null, policy, cancellationToken, out _);
    /// <summary>Stages every selected frame and complete PDF before copying to a borrowed output.</summary>
    public static PdfOperationReport Write(IEnumerable<PdfRasterInput> inputs, Stream output, PdfRasterImportOptions? options = null, PdfProviderRegistry? codecs = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        using var document = Run(inputs, options, codecs, output, null, policy, cancellationToken, out var report);
        return report;
    }

    /// <summary>Stages conversion before atomic destination replacement.</summary>
    public static PdfOperationReport WriteFile(IEnumerable<PdfRasterInput> inputs, string path, PdfRasterImportOptions? options = null, PdfProviderRegistry? codecs = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        using var document = Run(inputs, options, codecs, null, path, policy, cancellationToken, out var report);
        return report;
    }

    private static PdfDocument Run(IEnumerable<PdfRasterInput> inputs, PdfRasterImportOptions? options, PdfProviderRegistry? codecs, Stream? output, string? path, PdfOperationPolicy? policy, CancellationToken token, out PdfOperationReport report)
    {
        if (inputs == null)
            throw new ArgumentNullException(nameof(inputs));
        options ??= new();
        codecs ??= PdfProviderRegistry.Empty;
        PdfDocument? document = null;
        var finish = new List<Action<PdfOperationReport>>();
        try
        {
            report = PdfFont.Run("ImportRasterDocuments", "managed.raster-import", policy, token, c =>
            {
                var sources = new List<PdfRasterInput>();
                IPdfRasterDocumentDecoder? decoder = null;
                int count = 0;
                foreach (var input in inputs)
                {
                    c.Checkpoint();
                    c.Consume(PdfResourceKind.Objects, 1);
                    c.Consume(PdfResourceKind.ExpandedBytes, 256);
                    if (input == null || sources.Count == 4096 || input.Frames.Count > options.MaximumPages - count)
                        throw PdfWriterCore.Error(c, "RasterImport.PageLimit", "A finite nonempty source/selected-page inventory is required.");
                    count += input.Frames.Count;
                    sources.Add(input);
                    if (input.Format != PdfRasterFileFormat.Png && input.Format != PdfRasterFileFormat.Jpeg)
                    {
                        if (!codecs.Supports(PdfCapability.DecodeRasterDocuments))
                            throw PdfWriterCore.Error(c, "RasterImport.CodecUnavailable", "This file family needs an explicitly installed file decoder.");
                        decoder = codecs.GetRequired<IPdfRasterDocumentDecoder>(PdfCapability.DecodeRasterDocuments);
                        if (!decoder.Supports(input.Format))
                            throw PdfWriterCore.Error(c, "RasterImport.CodecUnavailable", "The explicit decoder does not support the requested file family.");
                    }
                }

                if (count == 0)
                    throw PdfWriterCore.Error(c, "RasterImport.PageLimit", "At least one explicitly selected frame is required.");
                var plans = new List<Plan>();
                foreach (var source in sources)
                {
                    c.Checkpoint();
                    var bytes = Capture(source.Input, c);
                    var plan = new Plan
                    {
                        Source = source,
                        Bytes = bytes
                    };
                    plans.Add(plan);
                    if (source.Format == PdfRasterFileFormat.Png || source.Format == PdfRasterFileFormat.Jpeg)
                    {
                        bool png = bytes.Length >= 8 && bytes.At(0) == 137 && bytes.At(1) == 80 && bytes.At(2) == 78 && bytes.At(3) == 71;
                        bool jpeg = bytes.Length >= 4 && bytes.At(0) == 255 && bytes.At(1) == 216;
                        if (source.Format == PdfRasterFileFormat.Png ? !png : !jpeg)
                            throw PdfWriterCore.Error(c, "RasterImport.FormatMismatch", "Declared raster format does not match file content.");
                        if (source.Frames.Any(frame => frame != 0))
                            throw PdfWriterCore.Error(c, "RasterImport.FrameIndex", "Managed static PNG/JPEG contains exactly frame zero.");
                        plan.Image = PdfImages.ReadCore(bytes, options.ImageOptions, c);
                    }
                    else
                    {
                        using var file = bytes.Open(0, bytes.Length);
                        using var lease = c.BorrowExpandedInput(file);
                        using var worker = c.EnterScope(PdfResourceKind.Workers);
                        plan.Info = decoder!.Inspect(lease, source.Format, c) ?? throw PdfWriterCore.Error(c, "RasterImport.FrameInfo", "The explicit decoder returned no finite frame inventory.");
                        c.Checkpoint();
                        if (plan.Info.IsAnimation && !source.AllowAnimationFrames)
                            throw PdfWriterCore.Error(c, "RasterImport.AnimationSelection", "Animation frames require explicit selection authority.");
                        if (source.Frames.Any(frame => frame >= plan.Info.Frames.Count))
                            throw PdfWriterCore.Error(c, "RasterImport.FrameIndex", "Every requested frame must exist in the inspected file.");
                    }
                }

                document = Pdf.Create(new(maximumPages: options.MaximumPages, operationPolicy: c.Policy));
                using (document.UseModelContext(c))
                    foreach (var plan in plans)
                        foreach (int frame in plan.Source.Frames)
                        {
                            c.Checkpoint();
                            PdfImageAsset image;
                            if (plan.Image != null)
                                image = plan.Image;
                            else if (!plan.Frames.TryGetValue(frame, out image!))
                            {
                                var info = plan.Info!.Frames[frame];
                                using var decoded = new PdfCodecBufferStream(c, info.Samples.ByteLength);
                                using (var file = plan.Bytes.Open(0, plan.Bytes.Length))
                                using (var input = c.BorrowExpandedInput(file))
                                using (var target = new BorrowedStream(decoded, false, c))
                                using (var worker = c.EnterScope(PdfResourceKind.Workers))
                                    decoder!.DecodeFrame(input, plan.Source.Format, frame, info, target, c);
                                c.Checkpoint();
                                if (decoded.BufferedLength != info.Samples.ByteLength)
                                    throw PdfWriterCore.Error(c, "RasterImport.FrameLength", "Decoded frames must match exact inspected RGBA8 geometry.");
                                var raw = decoded.Finish();
                                using var rgba = raw.Open(0, raw.Length);
                                var meta = info.Metadata;
                                if (!options.ImageOptions.ApplyExifOrientation)
                                    meta = new(PdfImageOrientation.Normal, meta.HorizontalDpi, meta.VerticalDpi, meta.PixelAspectRatio);
                                image = PdfImages.FromRgbaCore(rgba, info.Samples.Width, info.Samples.Height, 8, info.ColorSpace, meta, c);
                                plan.Frames.Add(frame, image);
                            }

                            var captured = image;
                            finish.Add(r =>
                            {
                                PdfImages.Complete(captured, r);
                                if (captured.SoftMask != null)
                                    PdfImages.Complete(captured.SoftMask, r);
                            });
                            var size = Size(image, options, c);
                            var geometry = new PdfPageGeometry(new(new(0, 0, size.Width, size.Height)));
                            var canvas = PdfCanvas.CreateCore(geometry, draw => draw.DrawImageAsset(image, new(options.Margin, options.Margin, size.Width - options.Margin * 2, size.Height - options.Margin * 2)), new(), c, out var completed);
                            finish.Add(completed);
                            var page = document.Pages.Add(geometry);
                            foreach (var binding in canvas.Drawing.Resources.Bindings)
                                page.SetResource(binding.Name, binding.Resource);
                            page.Content = canvas.Drawing.Content;
                        }

                if (output != null || path != null)
                {
                    using var staged = new PdfCodecBufferStream(c, c.Available(PdfResourceKind.ExpandedBytes));
                    document.WriteWithinContext(ManagedPdfProvider.Instance, staged, c, new(minimumVersion: "1.7"));
                    var bytes = staged.Finish();
                    if (path != null)
                        PdfAtomicFile.Write(path, c, target => Copy(bytes, target, c));
                    else
                        Copy(bytes, output!, c);
                }
            }, PdfCapability.WriteDocument);
            foreach (var completed in finish)
                completed(report);
            document!.SetCompositionReport(report);
            return document;
        }
        catch
        {
            document?.Dispose();
            throw;
        }
    }

    private static PdfPageSize Size(PdfImageAsset image, PdfRasterImportOptions options, PdfOperationContext c)
    {
        if (options.PageSize.HasValue)
            return options.PageSize.Value;
        try
        {
            var natural = image.NaturalSize;
            if (!natural.HasValue)
            {
                double width = image.Info.Width * 72 / options.FallbackDpi * image.Metadata.PixelAspectRatio, height = image.Info.Height * 72 / options.FallbackDpi;
                natural = image.SwapsAxes ? new PdfPageSize(height, width) : new PdfPageSize(width, height);
            }

            var size = new PdfPageSize(natural.Value.Width + options.Margin * 2, natural.Value.Height + options.Margin * 2);
            if (size.Width > 14400 || size.Height > 14400)
                throw PdfWriterCore.Error(c, "RasterImport.PageGeometry", "This profile requires page boxes at most14400 points; choose an explicit fixed page.");
            return size;
        }
        catch (ArgumentException)
        {
            c.Checkpoint();
            throw PdfWriterCore.Error(c, "RasterImport.PageGeometry", "Natural density exceeds PDF page geometry; select an explicit fixed page.");
        }
    }

    private static PdfByteStore Capture(Stream input, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var chunk = new byte[8192];
        using var source = new BorrowedStream(input, true, c);
        using var buffer = new PdfCodecBufferStream(c, 67108864);
        int n;
        while ((n = source.Read(chunk, 0, chunk.Length)) > 0)
        {
            c.Checkpoint();
            buffer.Write(chunk, 0, n);
        }

        return buffer.Finish();
    }

    private static void Copy(PdfByteStore bytes, Stream output, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var chunk = new byte[8192];
        using var input = bytes.Open(0, bytes.Length);
        using var target = new BorrowedStream(output, false, c);
        int n;
        while ((n = input.Read(chunk, 0, chunk.Length)) > 0)
        {
            c.Checkpoint();
            target.Write(chunk, 0, n);
        }
    }
}
