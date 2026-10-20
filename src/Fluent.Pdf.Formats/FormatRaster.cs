using System.IO;

namespace Fluent.Pdf.Formats;
internal sealed class FormatRasterFrame
{
    internal PdfByteStore Png = null!;
    internal PdfPageSize Size;
    internal int Width, Height;
}

internal static class FormatRaster
{
    internal static FormatRasterFrame Capture(PdfDocumentSnapshot source, int page, PdfRenderOptions rendering, PdfProviderRegistry providers, PdfOperationContext c)
    {
        if (rendering.Region.HasValue || rendering.DpiX < 1 || rendering.DpiY < 1)
            throw FormatIo.Error(c, "RasterPageOptions", "This fixed-page profile requires whole pages and at least1 DPI.");
        c.Checkpoint();
        var bounds = source.Pages[page].Geometry.DisplayBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0 || bounds.Width > 14400 || bounds.Height > 14400)
            throw FormatIo.Error(c, "RasterPageGeometry", "Fixed pages require positive dimensions at most14400 points.");
        var captured = PdfRasterExport.Build(source, new[] { page }, new(PdfRasterFormat.Png, rendering), providers, c);
        if (captured.Length > 16777216)
            throw FormatIo.Error(c, "RasterPageExtent", "A fixed-page image exceeds16 MiB.");
        var info = PngInfo(captured, c);
        using var output = new PdfCodecBufferStream(c, 16777216);
        // Fixed-page geometry is authoritative. Remove the generated PNG density chunk so XPS image-space units are exactly pixels at its normative96-DPI default.
        Copy(0, 8);
        for (long at = 8; at < captured.Length;)
        {
            c.Checkpoint();
            long count = U32(captured, at) + 12L;
            if (count > captured.Length - at)
                throw FormatIo.Error(c, "RasterPageExtent", "Generated PNG chunk extent is invalid.");
            if (!Type(captured, at + 4, "pHYs"))
                Copy(at, count);
            at += count;
        }

        return new()
        {
            Png = output.Finish(),
            Width = info.Width,
            Height = info.Height,
            Size = new(bounds.Width, bounds.Height)
        };
        void Copy(long at, long length)
        {
            using var input = captured.Open(at, length);
            c.Consume(PdfResourceKind.ExpandedBytes, 8192);
            var buffer = new byte[8192];
            int n;
            while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                c.Checkpoint();
                output.Write(buffer, 0, n);
            }
        }
    }

    internal static (int Width, int Height, double DpiX, double DpiY) PngInfo(PdfByteStore bytes, PdfOperationContext c)
    {
        if (bytes.Length < 33 || bytes.At(0) != 137 || bytes.At(1) != 80 || !Type(bytes, 12, "IHDR"))
            throw FormatIo.Error(c, "RasterPageImage", "This profile requires a complete PNG image.");
        uint width = U32(bytes, 16), height = U32(bytes, 20);
        if (width == 0 || height == 0 || width > int.MaxValue || height > int.MaxValue)
            throw FormatIo.Error(c, "RasterPageImage", "Bounded PNG dimensions are required.");
        double dx = 96, dy = 96;
        int count = 0;
        bool density = false, ended = false;
        for (long at = 8; at < bytes.Length;)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            if (++count > 16384 || at > bytes.Length - 12)
                throw FormatIo.Error(c, "RasterPageImage", "The finite PNG chunk inventory is incomplete.");
            long length = U32(bytes, at);
            if (length > bytes.Length - at - 12)
                throw FormatIo.Error(c, "RasterPageImage", "A PNG chunk exceeds its extent.");
            if (Type(bytes, at + 4, "pHYs"))
            {
                if (density || length != 9 || bytes.At(at + 16) != 1)
                    throw FormatIo.Error(c, "RasterPageImage", "This profile requires unique explicit physical PNG density or no density chunk.");
                density = true;
                dx = U32(bytes, at + 8) * .0254;
                dy = U32(bytes, at + 12) * .0254;
                if (dx <= 0 || dy <= 0)
                    throw FormatIo.Error(c, "RasterPageImage", "Positive PNG density is required.");
            }

            if (Type(bytes, at + 4, "IEND"))
            {
                if (length != 0 || at + 12 != bytes.Length)
                    throw FormatIo.Error(c, "RasterPageImage", "A complete final PNG marker is required.");
                ended = true;
            }

            at += length + 12;
        }

        if (!ended)
            throw FormatIo.Error(c, "RasterPageImage", "A final PNG marker is required.");
        return ((int)width, (int)height, dx, dy);
    }

    private static uint U32(PdfByteStore b, long at) => (uint)b.At(at) << 24 | (uint)b.At(at + 1) << 16 | (uint)b.At(at + 2) << 8 | (uint)b.At(at + 3);
    private static bool Type(PdfByteStore b, long at, string value)
    {
        for (int i = 0; i < 4; i++)
            if (b.At(at + i) != value[i])
                return false;
        return true;
    }
}
