using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Actual sample replacement and comprehensive fresh-graph raster sanitization. A painted overlay is never used as a removal mechanism.</summary>
public static class PdfRedaction
{
    /// <summary>Creates a fresh serialized PDF using only explicitly rendered/masked RGB samples. All original object programs, hidden data, metadata, attachments, actions and revisions are discarded. Every page is rasterized; original editable text/vector/structure are not retained.</summary>
    public static PdfRedactionResult RasterRewrite(PdfDocumentSnapshot source, IEnumerable<PdfRedactionRegion> regions, PdfProviderRegistry providers, PdfRedactionRasterOptions options, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Validate(source, regions, providers, options);
        PdfByteStore? bytes = null;
        PdfRedactionAudit? audit = null;
        var selected = policy ?? source.Options.OperationPolicy;
        var report = PdfFont.Run("RasterRedactAndSanitize", "managed.redaction", selected, cancellationToken, c =>
        {
            var result = Build(source, regions, providers, options, c);
            bytes = result.Bytes;
            audit = result.Audit;
        }, PdfCapability.WriteDocument);
        return new(bytes!, audit!, report, selected);
    }

    /// <summary>Sanitizes all original object/history data using a fresh raster-only graph with no removal rectangles. Only configured visible crop pixels are adopted; this is an explicit flattening profile.</summary>
    public static PdfRedactionResult SanitizeRasterRewrite(PdfDocumentSnapshot source, PdfProviderRegistry providers, PdfRedactionRasterOptions options, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => RasterRewrite(source, Array.Empty<PdfRedactionRegion>(), providers, options, policy, cancellationToken);
    /// <summary>Builds, validates and writes a fresh complete PDF under one context to an empty seekable borrowed destination. Renderer failures write no output; copy failures can leave only a sanitized prefix.</summary>
    public static PdfRedactionResult WriteRasterRewrite(PdfDocumentSnapshot source, IEnumerable<PdfRedactionRegion> regions, Stream output, PdfProviderRegistry providers, PdfRedactionRasterOptions options, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Validate(source, regions, providers, options);
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        PdfRedactionAudit? audit = null;
        PdfByteStore? bytes = null;
        var selected = policy ?? source.Options.OperationPolicy;
        var report = PdfFont.Run("WriteRasterRedactedPdf", "managed.redaction", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            RequireEmpty(output, c);
            var result = Build(source, regions, providers, options, c);
            Copy(result.Bytes, output, c);
            audit = result.Audit;
            bytes = result.Bytes;
        }, PdfCapability.WriteDocument);
        return new(bytes!, audit!, report, selected);
    }

    /// <summary>Builds a full sanitized PDF privately and atomically replaces the explicit destination only after complete rendering, masking and validation.</summary>
    public static PdfRedactionResult WriteRasterRewriteFile(PdfDocumentSnapshot source, IEnumerable<PdfRedactionRegion> regions, string path, PdfProviderRegistry providers, PdfRedactionRasterOptions options, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Validate(source, regions, providers, options);
        Guard.Text(path, nameof(path), 32767);
        PdfRedactionAudit? audit = null;
        PdfByteStore? bytes = null;
        var selected = policy ?? source.Options.OperationPolicy;
        var report = PdfFont.Run("WriteRasterRedactedPdfFile", "managed.redaction", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            var result = Build(source, regions, providers, options, c);
            PdfAtomicFile.Write(path, c, output =>
            {
                RequireEmpty(output, c);
                Copy(result.Bytes, output, c);
            });
            audit = result.Audit;
            bytes = result.Bytes;
        }, PdfCapability.WriteDocument);
        return new(bytes!, audit!, report, selected);
    }

    private static void Validate(PdfDocumentSnapshot source, IEnumerable<PdfRedactionRegion> regions, PdfProviderRegistry providers, PdfRedactionRasterOptions options)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (regions == null)
            throw new ArgumentNullException(nameof(regions));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
    }

    private static (PdfByteStore Bytes, PdfRedactionAudit Audit) Build(PdfDocumentSnapshot source, IEnumerable<PdfRedactionRegion> selection, PdfProviderRegistry providers, PdfRedactionRasterOptions options, PdfOperationContext c)
    {
        if (source.Pages.Count == 0)
            throw Error("EmptyDocument", "The raster sanitization profile requires at least one page.");
        c.Consume(PdfResourceKind.ExpandedBytes, source.Pages.Count * 128L + 8192);
        var originalGraphs = new HashSet<PdfSyntaxDocument>();
        if (source.SourceDocument != null)
            originalGraphs.Add(source.SourceDocument);
        foreach (var page in source.Pages)
        {
            c.Checkpoint();
            if (page.Source != null)
                originalGraphs.Add(page.Source.Document);
        }

        bool signed = originalGraphs.Any(g => g.HasSignatureByteRanges);
        if (signed && !options.AllowSignatureInvalidation)
            throw Error("SignaturePolicy", "Discarding existing signature byte ranges requires explicit invalidation authority.");
        long revisions = 0;
        foreach (var graph in originalGraphs)
        {
            c.Checkpoint();
            revisions += graph.Revisions.Count;
        }

        var regions = new List<PdfRedactionRegion>();
        foreach (var region in selection)
        {
            c.Checkpoint();
            if (regions.Count == options.MaximumRegions)
                throw Error("RegionLimit", "The explicit removal-rectangle ceiling was exceeded.");
            if (region == null || region.PageIndex >= source.Pages.Count || !PdfPageBoxes.Contains(source.Pages[region.PageIndex].Geometry.DisplayBounds(), region.Bounds))
                throw Error("RegionBounds", "Removal regions must lie within exact displayed source crops.");
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 96);
            regions.Add(region);
        }

        using var prepared = new PdfGraphEdit(source, new(allowSignatureInvalidation: options.AllowSignatureInvalidation), c);
        // A new transaction is essential: no original graph, trailer, object identity, program or stream is adopted.
        using var edit = new PdfTransaction(policy: c.Policy);
        var writer = new PdfResourceWriter(edit, c, new());
        c.Consume(PdfResourceKind.Objects, 2);
        var pagesId = edit.Add(D(("Type", N("Pages")), ("Kids", PdfValue.OwnedArray(new List<PdfValue>())), ("Count", PdfValue.Integer(0))));
        var kids = new List<PdfValue>();
        var coverage = new List<PdfRedactionCoverage>();
        long maskWork = 0, maskedPixels = 0;
        for (int pageIndex = 0; pageIndex < source.Pages.Count; pageIndex++)
        {
            c.Checkpoint();
            var page = prepared.Snapshot.Pages[pageIndex].Source!;
            var displayed = page.Geometry.DisplayBounds();
            var render = new PdfRenderOptions(options.Dpi, options.Dpi, backgroundRgb: options.BackgroundRgb, annotations: options.IncludeAnnotationAppearances);
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 256);
            var plan = new PdfRasterRequest(pageIndex, page, render, c);
            if (plan.ByteLength > options.MaximumRasterBytes)
                throw Error("RasterLimit", "A planned page exceeds the explicit raster extent ceiling.");
            var raster = PdfRendering.RenderCore(prepared, pageIndex, providers, render, c);
            c.Consume(PdfResourceKind.ExpandedBytes, regions.Count * 128L + 512);
            var rectangles = new List<IntervalRect>();
            double sx = raster.Request.Width / displayed.Width, sy = raster.Request.Height / displayed.Height;
            foreach (var region in regions)
            {
                Use(1);
                if (region.PageIndex != pageIndex)
                    continue;
                var b = region.Bounds;
                int x0 = (int)Math.Max(0, Math.Floor(b.X * sx) - options.PaddingPixels), y0 = (int)Math.Max(0, Math.Floor(b.Y * sy) - options.PaddingPixels);
                int x1 = (int)Math.Min(raster.Request.Width, Math.Ceiling(b.Right * sx) + options.PaddingPixels), y1 = (int)Math.Min(raster.Request.Height, Math.Ceiling(b.Top * sy) + options.PaddingPixels);
                rectangles.Add(new(x0, y0, x1, y1));
                coverage.Add(new(pageIndex, b, new(x0, y0, x1 - x0, y1 - y0)));
            }

            c.Consume(PdfResourceKind.ExpandedBytes, rectangles.Count * 16L + 1024);
            var intervals = new Interval[rectangles.Count];
            var comparer = Comparer<Interval>.Create((a, b) =>
            {
                Use(1);
                return a.Start.CompareTo(b.Start);
            });
            var encoded = new PdfRedactionRle(c);
            byte red = (byte)(options.FillRgb >> 16), green = (byte)(options.FillRgb >> 8), blue = (byte)options.FillRgb;
            for (int y = 0; y < raster.Request.Height; y++)
            {
                c.Checkpoint();
                int count = 0;
                foreach (var r in rectangles)
                {
                    Use(1);
                    if (y >= r.Y0 && y < r.Y1)
                        intervals[count++] = new(r.X0, r.X1);
                }

                Array.Sort(intervals, 0, count, comparer);
                int merged = 0;
                for (int n = 0; n < count; n++)
                {
                    Use(1);
                    var interval = intervals[n];
                    if (merged != 0 && interval.Start <= intervals[merged - 1].End)
                        intervals[merged - 1] = new(intervals[merged - 1].Start, Math.Max(intervals[merged - 1].End, interval.End));
                    else
                        intervals[merged++] = interval;
                }

                for (int n = 0; n < merged; n++)
                    maskedPixels += intervals[n].End - intervals[n].Start;
                Use(raster.Request.Width);
                int mask = 0;
                for (int x = 0; x < raster.Request.Width; x++)
                {
                    if ((x & 1023) == 0)
                        c.Checkpoint();
                    while (mask < merged && x >= intervals[mask].End)
                        mask++;
                    bool removed = mask < merged && x >= intervals[mask].Start;
                    long at = (long)y * raster.Request.Stride + x * 4L;
                    encoded.Add(removed ? red : (byte)raster.Samples.At(at + 2));
                    encoded.Add(removed ? green : (byte)raster.Samples.At(at + 1));
                    encoded.Add(removed ? blue : (byte)raster.Samples.At(at));
                }
            }

            var image = PdfImages.Build(encoded.Finish(), new(raster.Request.Width, raster.Request.Height, 3, 8), PdfColorSpace.Rgb.Definition, null, null, false, new(), false, c, new[] { new PdfFilter("RunLengthDecode") });
            var resource = writer.Embed(image.Resource);
            var program = PdfContentProgram.CreateCore(new[] { new PdfContentOperation(PdfOperator.SaveState), new PdfContentOperation(PdfOperator.Transform, PdfValues.Real(displayed.Width), PdfValue.Integer(0), PdfValue.Integer(0), PdfValues.Real(displayed.Height), PdfValue.Integer(0), PdfValue.Integer(0)), new PdfContentOperation(PdfOperator.DrawObject, N("R")), new PdfContentOperation(PdfOperator.RestoreState) }, PdfContentKind.Page, c);
            c.Consume(PdfResourceKind.Objects, 2);
            c.Consume(PdfResourceKind.ExpandedBytes, 2048);
            var contentId = edit.Add(program.ToStream(c));
            var pageId = edit.Add(D(("Type", N("Page")), ("Parent", PdfValue.Reference(pagesId)), ("MediaBox", PdfGraphicsNumbers.Array(0, 0, displayed.Width, displayed.Height)), ("Resources", D(("XObject", D(("R", resource))))), ("Contents", PdfValue.Reference(contentId))));
            kids.Add(PdfValue.Reference(pageId));
        }

        edit.Set(pagesId, D(("Type", N("Pages")), ("Kids", PdfValue.OwnedArray(kids)), ("Count", PdfValue.Integer(kids.Count))));
        var catalogId = edit.Add(D(("Type", N("Catalog")), ("Pages", PdfValue.Reference(pagesId))));
        edit.Trailer = D(("Root", PdfValue.Reference(catalogId)));
        using var buffer = new PdfCodecBufferStream(c, c.Available(PdfResourceKind.ExpandedBytes));
        using (var output = new BorrowedStream(buffer, false, c))
            edit.WriteCore(output, new(crossReferenceFormat: PdfCrossReferenceFormat.Table, minimumVersion: "1.4"), c);
        var bytes = buffer.Finish();
        ValidateFresh(bytes, c);
        c.Consume(PdfResourceKind.ExpandedBytes, coverage.Count * 16L + 512);
        var audit = new PdfRedactionAudit(source.Pages.Count, revisions, signed, maskedPixels, coverage.ToArray(), Hash(bytes, c));
        c.Report(new("Redaction.OriginalGraphDiscarded", "A fresh full revision adopts only masked RGB samples and fixed structural names; original objects and history are discarded.", severity: PdfDiagnosticSeverity.Information));
        return (bytes, audit);
        void Use(long count)
        {
            c.Checkpoint();
            if (count > options.MaximumMaskWork - maskWork)
                throw Error("WorkLimit", "The explicit cumulative redaction masking work ceiling was exceeded.");
            maskWork += count;
        }

        PdfValue D(params (string Key, PdfValue Value)[] pairs)
        {
            c.Consume(PdfResourceKind.Objects, pairs.Length + 1L);
            c.Consume(PdfResourceKind.ExpandedBytes, pairs.Length * 128L + 256);
            var values = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
            foreach (var p in pairs)
                values.Add(p.Key, p.Value);
            return PdfValue.OwnedDictionary(values);
        }

        PdfException Error(string code, string message) => PdfWriterCore.Error(c, "Redaction." + code, message);
    }

    private static PdfValue N(string name) => PdfValue.Name(name);
    private readonly struct Interval
    {
        internal readonly int Start, End;
        internal Interval(int start, int end)
        {
            Start = start;
            End = end;
        }
    }

    private readonly struct IntervalRect
    {
        internal readonly int X0, Y0, X1, Y1;
        internal IntervalRect(int x0, int y0, int x1, int y1)
        {
            X0 = x0;
            Y0 = y0;
            X1 = x1;
            Y1 = y1;
        }
    }

    private static void ValidateFresh(PdfByteStore bytes, PdfOperationContext c)
    {
        var parsed = new PdfSyntaxReader(bytes, c).Read();
        if (parsed.Revisions.Count != 1 || parsed.HasSignatureByteRanges)
            throw Error();
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var keys = new HashSet<string>(new[] { "Type", "Pages", "Kids", "Count", "Parent", "MediaBox", "Resources", "XObject", "Contents", "Subtype", "Width", "Height", "BitsPerComponent", "ColorSpace", "Filter", "Length", "Root", "Size" }, StringComparer.Ordinal);
        var names = new HashSet<string>(new[] { "Catalog", "Pages", "Page", "XObject", "Image", "DeviceRGB", "RunLengthDecode" }, StringComparer.Ordinal);
        Check(parsed.Revisions[0].Trailer);
        foreach (var value in parsed.Revisions[0].Values.Values)
            Check(value);
        void Check(PdfValue value)
        {
            c.Checkpoint();
            using var depth = c.EnterScope(PdfResourceKind.Depth);
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 64);
            if (value.Kind == PdfValueKind.String)
                throw Error();
            if (value.Kind == PdfValueKind.Name && !names.Contains(value.NameValue))
                throw Error();
            if (value.Kind == PdfValueKind.Reference && parsed.Resolve(value.ReferenceId).Kind == PdfValueKind.Null)
                throw Error();
            if (value.Kind == PdfValueKind.Array)
                foreach (var item in value.Items)
                    Check(item);
            if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
                foreach (var pair in value.Entries)
                {
                    if (!keys.Contains(pair.Key) && pair.Key != "R")
                        throw Error();
                    Check(pair.Value);
                }
        }

        PdfException Error() => PdfWriterCore.Error(c, "Redaction.FreshGraph", "The new redaction graph contains a prohibited structural value or history.");
    }

    internal static void RequireEmpty(Stream output, PdfOperationContext c)
    {
        c.Checkpoint();
        if (!output.CanSeek)
            throw PdfWriterCore.Error(c, "Stream.SeekRequired", "This complete-document redaction sink must expose an empty extent.");
        if (output.Position != 0 || output.Length != 0)
            throw PdfWriterCore.Error(c, "Redaction.OutputExtent", "A complete redacted PDF requires an empty destination; prefixes and old tails are prohibited.");
    }

    internal static void Copy(PdfByteStore bytes, Stream output, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 65536);
        var buffer = new byte[65536];
        using var input = c.BorrowExpandedInput(bytes.Open(0, bytes.Length));
        using var destination = new BorrowedStream(output, false, c);
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
        {
            c.Checkpoint();
            destination.Write(buffer, 0, read);
        }
    }

    private static string Hash(PdfByteStore bytes, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 65536);
        var buffer = new byte[65536];
        using var input = bytes.Open(0, bytes.Length);
        using var hash = SHA256.Create();
        int read;
        while ((read = input.Read(buffer, 0, buffer.Length)) != 0)
        {
            c.Checkpoint();
            hash.TransformBlock(buffer, 0, read, buffer, 0);
        }

        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return BitConverter.ToString(hash.Hash!).Replace("-", "").ToLowerInvariant();
    }
}
