using System.IO;
using System.Threading;

namespace Fluent.Pdf.Formats;
/// <summary>Exact caller-selected metafile family; no format guessing or desktop-session fallback.</summary>
public enum PdfMetafileFormat
{
    /// <summary>Computer Graphics Metafile, with encoding/version explicit in the configured converter profile.</summary>
    Cgm,
    /// <summary>Windows Metafile.</summary>
    Wmf,
    /// <summary>Enhanced Metafile.</summary>
    Emf,
    /// <summary>Enhanced Metafile Plus; not silently treated as its fallback records.</summary>
    EmfPlus
}

/// <summary>Explicit trusted optional offline isolated converter. Implementations declare exact version/OS/font/record support through their pinned configuration; platform restrictions fail before capture.</summary>
public interface IPdfIsolatedMetafileConverter
{
    /// <summary>Gets a nonsecret ASCII pinned converter/configuration identity.</summary>
    string EngineId { get; }

    /// <summary>Reports exact current-host/profile availability, including record encodings and local resources.</summary>
    bool Supports(PdfMetafileFormat format);
    /// <summary>Converts in a bounded separately qualified OS-isolated job with no ambient files/network/GUI dependencies. Preserve vector/text/raster geometry where supported and report every unsupported record through the shared context. Leases expire at return.</summary>
    void Convert(Stream input, PdfMetafileFormat format, Stream pdfOutput, PdfOperationContext context);
}

/// <summary>Managed EMF24 raster-page interchange and explicit optional full metafile conversion.</summary>
public static class PdfMetafiles
{
    /// <summary>Writes one explicit opaque rendered page as an original EMF1.0 header, SRCCOPY STRETCHDIBITS and EOF. Frame quantization requires declared loss authority; no vector reconstruction is claimed.</summary>
    public static PdfOperationReport WriteRasterEmf(PdfDocumentSnapshot source, int pageIndex, Stream output, PdfRenderOptions rendering, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || rendering == null || providers == null)
            throw new ArgumentNullException(nameof(source));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed EMF stream is required.", nameof(output));
        if (rendering.Region.HasValue || !rendering.BackgroundRgb.HasValue)
            throw new ArgumentException("The EMF24 page profile requires a whole page and explicit opaque matte.", nameof(rendering));
        return PdfFont.Run("ExportRasterEmf", "managed.emf24", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
            var bounds = source.Pages[pageIndex].Geometry.DisplayBounds();
            int fw = Frame(bounds.Width), fh = Frame(bounds.Height);
            c.Report(new("Metafile.RasterPage", "The explicit EMF profile contains only a rendered opaque page image, not editable vector/text records.", severity: PdfDiagnosticSeverity.Information, pageIndex: pageIndex));
            var bmp = PdfRasterExport.Build(source, new[] { pageIndex }, new(PdfRasterFormat.Bmp, rendering), providers, c);
            if (bmp.Length < 54 || bmp.Length > 67108000)
                throw Error(c, "ImageExtent", "A bounded complete BMP24 is required.");
            long bits = bmp.Length - 54, size = 120 + bits, total = 108 + size + 20;
            using var staged = new PdfCodecBufferStream(c, 67108864);
            c.Consume(PdfResourceKind.ExpandedBytes, 4096);
            using (var w = new BinaryWriter(staged, System.Text.Encoding.UTF8, true))
            {
                U(1, 108);
                Rect(0, 0, fw - 1, fh - 1);
                Rect(0, 0, fw - 1, fh - 1);
                U(0x464d4520, 0x10000, (uint)total, 3);
                w.Write((ushort)1);
                w.Write((ushort)0);
                U(0, 0, 0, 100000, 100000, 1000, 1000, 0, 0, 0, 1000000, 1000000);
                U(81, (uint)size);
                Rect(0, 0, fw - 1, fh - 1);
                U(0, 0, 0, 0, ReadU(bmp, 18), ReadU(bmp, 22), 80, 40, 120, (uint)bits, 0, 0x00cc0020, (uint)fw, (uint)fh);
                using (var dib = bmp.Open(14, bmp.Length - 14))
                {
                    c.Consume(PdfResourceKind.ExpandedBytes, 8192);
                    var b = new byte[8192];
                    int n;
                    while ((n = dib.Read(b, 0, b.Length)) > 0)
                    {
                        c.Checkpoint();
                        w.Write(b, 0, n);
                    }
                }

                U(14, 20, 0, 0, 20);
                w.Flush();
            }

            FormatIo.Copy(staged.Finish(), output, c);
            void U(params uint[] n)
            {
                foreach (uint v in n)
                {
                    c.Checkpoint();
                    staged.WriteByte((byte)v);
                    staged.WriteByte((byte)(v >> 8));
                    staged.WriteByte((byte)(v >> 16));
                    staged.WriteByte((byte)(v >> 24));
                }
            }

            void Rect(int x, int y, int r, int b) => U((uint)x, (uint)y, (uint)r, (uint)b);
            int Frame(double points)
            {
                if (points <= 0 || points > 14400)
                    throw Error(c, "Geometry", "EMF page dimensions must be at most14400 points.");
                double units = points * 2540 / 72;
                int value = (int)Math.Round(units, MidpointRounding.AwayFromZero);
                if (value < 1)
                    throw Error(c, "Geometry", "An EMF frame must span at least0.01 mm.");
                if (Math.Abs(value - units) > 1e-7)
                    c.Report(new("Metafile.GeometryQuantized", "The EMF frame rounds physical dimensions to0.01 mm.", lossCategory: PdfLossCategory.FidelityReduction, pageIndex: pageIndex));
                return value;
            }
        }, PdfCapability.RenderPages);
    }

    /// <summary>Reads the exact single-image EMF24 source profile without executing GDI, text/font discovery or comment/EMF+ records. Every unknown record rejects.</summary>
    public static PdfDocument ImportRasterEmf(Stream input, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("Readable borrowed EMF input is required.", nameof(input));
        PdfDocument? result = null;
        var finish = new List<Action<PdfOperationReport>>();
        try
        {
            var report = PdfFont.Run("ImportRasterEmf", "managed.emf24", policy, cancellationToken, c =>
            {
                var b = FormatIo.Capture(input, c, 67108864);
                c.Consume(PdfResourceKind.Objects, 32);
                c.Consume(PdfResourceKind.ExpandedBytes, 8192);
                if (b.Length < 248)
                    throw Error(c, "Header", "A complete EMF24 source profile is required.");
                uint U(long n) => ReadU(b, n);
                void Expect(long n, uint v)
                {
                    if (U(n) != v)
                        throw Error(c, "RecordUnsupported", "The EMF contains unsupported or inconsistent records/geometry.");
                }

                Expect(0, 1);
                Expect(4, 108);
                Expect(40, 0x464d4520);
                Expect(44, 0x10000);
                Expect(48, (uint)b.Length);
                Expect(52, 3);
                Expect(56, 1);
                foreach (int at in new[]
                {
                    60,
                    64,
                    68,
                    88,
                    92,
                    96
                }

                )
                    Expect(at, 0);
                Expect(72, 100000);
                Expect(76, 100000);
                Expect(80, 1000);
                Expect(84, 1000);
                Expect(100, 1000000);
                Expect(104, 1000000);
                Expect(24, 0);
                Expect(28, 0);
                uint fw = U(32) + 1, fh = U(36) + 1;
                if (fw == 0 || fh == 0 || fw > 508000 || fh > 508000)
                    throw Error(c, "Geometry", "Bounded positive EMF frames are required.");
                for (int n = 0; n < 2; n++)
                {
                    long at = n == 0 ? 8 : 116;
                    Expect(at, 0);
                    Expect(at + 4, 0);
                    Expect(at + 8, fw - 1);
                    Expect(at + 12, fh - 1);
                }

                Expect(108, 81);
                uint record = U(112);
                if (record < 120 || record != b.Length - 128)
                    throw Error(c, "RecordUnsupported", "An exact complete STRETCHDIBITS record is required.");
                foreach (int at in new[]
                {
                    132,
                    136,
                    140,
                    144
                }

                )
                    Expect(at, 0);
                uint width = U(148), height = U(152);
                if (width == 0 || height == 0 || width > 100000 || height > 100000)
                    throw Error(c, "ImageExtent", "Bounded bitmap dimensions are required.");
                Expect(156, 80);
                Expect(160, 40);
                Expect(164, 120);
                Expect(172, 0);
                Expect(176, 0x00cc0020);
                Expect(180, fw);
                Expect(184, fh);
                Expect(188, 40);
                Expect(192, width);
                Expect(196, height);
                Expect(200, 0x00180001);
                Expect(204, 0);
                foreach (int at in new[]
                {
                    220,
                    224
                }

                )
                    Expect(at, 0);
                long stride = ((long)width * 3 + 3) & ~3L, extent = stride * height;
                if (extent > 67108000 || U(168) != extent || record != extent + 120)
                    throw Error(c, "ImageExtent", "The exact uncompressed bitmap extent is required.");
                if (U(208) != 0 && U(208) != extent)
                    throw Error(c, "ImageExtent", "The DIB size conflicts with its samples.");
                long eof = 108 + record;
                Expect(eof, 14);
                Expect(eof + 4, 20);
                Expect(eof + 8, 0);
                Expect(eof + 12, 0);
                Expect(eof + 16, 20);
                c.Consume(PdfResourceKind.Pixels, (long)width * height);
                c.Consume(PdfResourceKind.ExpandedBytes, (long)width * height * 4);
                var rgba = new byte[(int)((long)width * height * 4)];
                for (int y = 0; y < (int)height; y++)
                {
                    c.Checkpoint();
                    for (int x = 0; x < (int)width; x++)
                    {
                        long from = 228 + ((long)height - 1 - y) * stride + x * 3L;
                        int to = (y * (int)width + x) * 4;
                        rgba[to] = (byte)b.At(from + 2);
                        rgba[to + 1] = (byte)b.At(from + 1);
                        rgba[to + 2] = (byte)b.At(from);
                        rgba[to + 3] = 255;
                    }
                }

                using var samples = new MemoryStream(rgba, false);
                var image = PdfImages.FromRgbaCore(samples, (int)width, (int)height, 8, PdfColorSpace.Rgb, new(), c);
                finish.Add(r => PdfImages.Complete(image, r));
                result = Pdf.Create(new(operationPolicy: c.Policy));
                var size = new PdfPageSize(fw * 72d / 2540, fh * 72d / 2540);
                FormatIo.Page(result, size, canvas => canvas.DrawImageAsset(image, new(0, 0, size.Width, size.Height), PdfCanvasFit.Stretch), c, finish);
            }, PdfCapability.WriteDocument);
            foreach (var complete in finish)
                complete(report);
            result!.SetCompositionReport(report);
            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }

    /// <summary>Uses an exact explicitly configured converter before input capture. All CGM/WMF/EMF+/broader EMF profiles need this separately qualified optional boundary.</summary>
    public static PdfDocument Import(Stream input, PdfMetafileFormat format, IPdfIsolatedMetafileConverter? converter = null, int maximumPages = 1000, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("Readable borrowed metafile input is required.", nameof(input));
        if (!Enum.IsDefined(typeof(PdfMetafileFormat), format))
            throw new ArgumentOutOfRangeException(nameof(format));
        if (maximumPages < 1 || maximumPages > 10000)
            throw new ArgumentOutOfRangeException(nameof(maximumPages));
        string identity = converter?.EngineId ?? "unavailable";
        Guard.Text(identity, nameof(converter), 80);
        if (identity.Any(ch => !(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || "._-".IndexOf(ch) >= 0)))
            throw new ArgumentException("A stable ASCII converter identity is required.", nameof(converter));
        PdfDocument? result = null;
        try
        {
            var report = PdfFont.Run("ImportMetafile", "metafile." + identity, policy, cancellationToken, c =>
            {
                if (converter == null)
                    throw Error(c, "ConverterUnavailable", "Configure a separately qualified exact metafile converter.");
                bool available;
                try
                {
                    available = converter.Supports(format);
                }
                catch
                {
                    c.Checkpoint();
                    throw Error(c, "ConverterFailed", "The configured converter capability check failed.");
                }

                if (!available)
                    throw Error(c, "ProfileUnavailable", "The exact format/profile/current platform is unavailable.");
                var b = FormatIo.Capture(input, c, 67108864);
                using var source = b.Open(0, b.Length);
                using var output = new PdfCodecBufferStream(c, 67108864);
                try
                {
                    using var il = new BorrowedStream(source, true, c);
                    using var ol = new BorrowedStream(output, false, c);
                    converter.Convert(il, format, ol, c);
                    c.Checkpoint();
                }
                catch
                {
                    c.Checkpoint();
                    throw Error(c, "ConverterFailed", "The configured converter failed without exposing partial output.");
                }

                result = PdfPageOrganization.Read(output.Finish(), maximumPages, c);
                if (result.Pages.Count == 0)
                {
                    result.Dispose();
                    result = null;
                    throw Error(c, "EmptyOutput", "The converter must return a nonempty valid PDF.");
                }
            }, PdfCapability.WriteDocument);
            result!.SetCompositionReport(report);
            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }

    private static uint ReadU(PdfByteStore b, long at) => (uint)b.At(at) | (uint)b.At(at + 1) << 8 | (uint)b.At(at + 2) << 16 | (uint)b.At(at + 3) << 24;
    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Metafile." + code, message);
}
