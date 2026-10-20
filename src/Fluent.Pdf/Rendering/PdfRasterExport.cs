using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Original lossless raster container profiles.</summary>
public enum PdfRasterFormat
{
    /// <summary>Eight-bit RGBA PNG with physical resolution.</summary>
    Png,
    /// <summary>Opaque24-bit bottom-up BMP with physical resolution.</summary>
    Bmp,
    /// <summary>Multipage uncompressed eight-bit RGBA classic TIFF with resolution and optional ICC.</summary>
    Tiff
}
/// <summary>Explicit page/region raster export. Render choices include DPI, alpha/matte, annotations, print/layer use and color.</summary>
public sealed class PdfRasterExportOptions
{
    /// <summary>Captures an exact supported container and rendering policy. Optional ICC output requires a color-managed renderer and is embedded in PNG/TIFF; BMP ICC output rejects.</summary>
    public PdfRasterExportOptions(PdfRasterFormat format, PdfRenderOptions? rendering = null, PdfColorRenderOptions? colorManagement = null)
    {
        if (!Enum.IsDefined(typeof(PdfRasterFormat), format))
            throw new ArgumentOutOfRangeException(nameof(format));
        Rendering = rendering ?? new();
        if (format == PdfRasterFormat.Bmp && (!Rendering.BackgroundRgb.HasValue || colorManagement != null))
            throw new ArgumentException("The BMP24 profile requires an opaque matte and device color.", nameof(rendering));
        if (colorManagement != null && Rendering.Color != PdfRasterColor.Rgb)
            throw new ArgumentException("RGB ICC output requires RGB samples.", nameof(rendering));
        Format = format;
        ColorManagement = colorManagement;
    }
    /// <summary>Gets selected container.</summary>
    public PdfRasterFormat Format
    {
        get;
    }
    /// <summary>Gets explicit rasterization choices.</summary>
    public PdfRenderOptions Rendering
    {
        get;
    }
    /// <summary>Gets explicit color transform/profile, or null for device-color output.</summary>
    public PdfColorRenderOptions? ColorManagement
    {
        get;
    }
    /// <summary>Gets eight bits per color channel; no quantization or lossy conversion is performed.</summary>
    public int BitsPerComponent => 8;
}
/// <summary>Bounded original PNG/BMP/multipage TIFF export, rendering one frame at a time into private staged output.</summary>
public static class PdfRasterExport
{
    /// <summary>Stages all selected pages in exact caller order and writes only after every frame succeeds. Caller streams stay open. PNG/BMP require exactly one page; TIFF supports up to65535 pages, including repeated pages.</summary>
    public static PdfOperationReport Write(PdfDocumentSnapshot source, IEnumerable<int> pages, Stream output, PdfRasterExportOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Check(source, pages, options, providers);
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        return PdfFont.Run("ExportRaster", "managed.raster-export", policy ?? source.Options.OperationPolicy, cancellationToken, c => { var bytes = Build(source, pages, options, providers, c); Copy(bytes, output, c); }, PdfCapability.RenderPages);
    }
    /// <summary>Stages complete raster output before atomic file replacement.</summary>
    public static PdfOperationReport WriteFile(PdfDocumentSnapshot source, IEnumerable<int> pages, string path, PdfRasterExportOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Check(source, pages, options, providers);
        Guard.Text(path, nameof(path), 32767);
        return PdfFont.Run("ExportRasterFile", "managed.raster-export", policy ?? source.Options.OperationPolicy, cancellationToken, c => { var bytes = Build(source, pages, options, providers, c); PdfAtomicFile.Write(path, c, s => Copy(bytes, s, c)); }, PdfCapability.RenderPages);
    }
    private static void Check(PdfDocumentSnapshot source, IEnumerable<int> pages, PdfRasterExportOptions options, PdfProviderRegistry providers)
    {
        if (source == null || pages == null || options == null || providers == null)
            throw new ArgumentNullException(source == null ? nameof(source) : pages == null ? nameof(pages) : options == null ? nameof(options) : nameof(providers));
        if (options.ColorManagement == null)
            _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
        else
            _ = providers.GetRequired<IPdfColorManagedPageRenderer>(PdfCapability.RenderColorManaged);
    }
    internal static PdfByteStore Build(PdfDocumentSnapshot source, IEnumerable<int> pages, PdfRasterExportOptions options, PdfProviderRegistry providers, PdfOperationContext c)
    {
        using var graph = new PdfGraphEdit(source, new(allowSignatureInvalidation: true), c);
        var selected = new List<int>();
        foreach (int page in pages)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 16);
            if (page < 0 || page >= graph.Snapshot.Pages.Count)
                throw new ArgumentOutOfRangeException(nameof(pages));
            if (selected.Count == 65535)
                throw PdfWriterCore.Error(c, "Raster.PageLimit", "Classic TIFF page count exceeds its finite profile.");
            selected.Add(page);
        }
        if (selected.Count == 0 || options.Format != PdfRasterFormat.Tiff && selected.Count != 1)
            throw PdfWriterCore.Error(c, "Raster.PageSelection", "A nonempty selection is required; PNG/BMP require exactly one page.");
        using var output = new PdfCodecBufferStream(c, Math.Min(c.Available(PdfResourceKind.ExpandedBytes), c.Available(PdfResourceKind.OutputBytes)));
        byte[]? profile = null;
        if (options.ColorManagement != null)
        {
            var value = options.ColorManagement.OutputProfile.Definition.Items[1];
            if (value.Kind != PdfValueKind.Stream)
                throw PdfWriterCore.Error(c, "Raster.ColorProfile", "A complete captured ICC stream is required.");
            using var raw = value.OpenRawStream();
            var bytes = PdfByteStore.Read(raw, c);
            if (bytes.Length > 16777216)
                throw PdfWriterCore.Error(c, "Raster.ColorProfile", "The ICC profile exceeds its finite export extent.");
            c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length);
            profile = new byte[(int)bytes.Length];
            for (int i = 0; i < profile.Length; i++)
                profile[i] = (byte)bytes.At(i);
        }
        if (options.Format == PdfRasterFormat.Tiff)
        {
            output.WriteByte(73);
            output.WriteByte(73);
            U16(output, 42);
            U32(output, 8);
        }
        for (int frame = 0; frame < selected.Count; frame++)
        {
            c.Checkpoint();
            var rendered = PdfRendering.RenderCore(graph, selected[frame], providers, options.Rendering, c, options.ColorManagement);
            if (options.Format == PdfRasterFormat.Png)
                Png(output, rendered.Request, rendered.Samples, profile, c);
            else if (options.Format == PdfRasterFormat.Bmp)
                Bmp(output, rendered.Request, rendered.Samples, c);
            else
                Tiff(output, rendered.Request, rendered.Samples, profile, frame, selected.Count, c);
        }
        c.Checkpoint();
        return output.Finish();
    }
    private static void Png(Stream output, PdfRasterRequest request, PdfByteStore samples, byte[]? profile, PdfOperationContext c)
    {
        var signature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
        output.Write(signature, 0, signature.Length);
        var header = new byte[13];
        Big(header, 0, (uint)request.Width);
        Big(header, 4, (uint)request.Height);
        header[8] = 8;
        header[9] = 6;
        Chunk(output, "IHDR", header, c);
        var density = new byte[9];
        Big(density, 0, Density(request.EffectiveDpiX));
        Big(density, 4, Density(request.EffectiveDpiY));
        density[8] = 1;
        Chunk(output, "pHYs", density, c);
        if (profile != null)
        {
            using var raw = new MemoryStream(profile, false);
            var compressed = PdfFilterEncoders.Flate(raw, c);
            c.Consume(PdfResourceKind.ExpandedBytes, compressed.Length + 5);
            var payload = new byte[(int)compressed.Length + 5];
            payload[0] = 73;
            payload[1] = 67;
            payload[2] = 67;
            for (int i = 0; i < compressed.Length; i++)
                payload[i + 5] = (byte)compressed.At(i);
            Chunk(output, "iCCP", payload, c);
        }
        // A bounded row stream feeds zlib; no second full uncompressed frame is allocated.
        using var rows = new RgbaRows(samples, request, true, c);
        var encoded = PdfFilterEncoders.Flate(rows, c);
        for (long at = 0; at < encoded.Length;)
        {
            int size = (int)Math.Min(65536, encoded.Length - at);
            c.Consume(PdfResourceKind.ExpandedBytes, size);
            var part = new byte[size];
            for (int i = 0; i < size; i++)
                part[i] = (byte)encoded.At(at + i);
            Chunk(output, "IDAT", part, c);
            at += size;
        }
        Chunk(output, "IEND", Array.Empty<byte>(), c);
    }
    private static void Chunk(Stream output, string name, byte[] bytes, PdfOperationContext c)
    {
        c.Checkpoint();
        c.Consume(PdfResourceKind.Objects, 1);
        var header = new byte[8];
        c.Consume(PdfResourceKind.ExpandedBytes, 8);
        Big(header, 0, (uint)bytes.Length);
        for (int i = 0; i < 4; i++)
            header[i + 4] = (byte)name[i];
        output.Write(header, 0, 8);
        output.Write(bytes, 0, bytes.Length);
        uint crc = 0xffffffff;
        for (int i = 4; i < 8; i++)
            crc = Crc(crc, header[i]);
        for (int i = 0; i < bytes.Length; i++)
        {
            if ((i & 4095) == 0)
                c.Checkpoint();
            crc = Crc(crc, bytes[i]);
        }
        var tail = new byte[4];
        Big(tail, 0, crc ^ 0xffffffff);
        output.Write(tail, 0, 4);
    }
    private static readonly uint[] CrcTable = BuildCrcTable();
    private static uint[] BuildCrcTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            uint value = i;
            for (int bit = 0; bit < 8; bit++) value = (value & 1) == 0 ? value >> 1 : (value >> 1) ^ 0xedb88320;
            table[i] = value;
        }
        return table;
    }
    private static uint Crc(uint crc, byte value) => (crc >> 8) ^ CrcTable[(crc ^ value) & 255];
    private static void Bmp(Stream output, PdfRasterRequest r, PdfByteStore samples, PdfOperationContext c)
    {
        long row = ((long)r.Width * 3 + 3) & ~3L, total = 54 + row * r.Height;
        if (total > uint.MaxValue)
            throw PdfWriterCore.Error(c, "Raster.ContainerExtent", "BMP exceeds its32-bit extent.");
        output.WriteByte(66);
        output.WriteByte(77);
        U32(output, (uint)total);
        U32(output, 0);
        U32(output, 54);
        U32(output, 40);
        U32(output, (uint)r.Width);
        U32(output, (uint)r.Height);
        U16(output, 1);
        U16(output, 24);
        U32(output, 0);
        U32(output, (uint)(row * r.Height));
        U32(output, Density(r.EffectiveDpiX));
        U32(output, Density(r.EffectiveDpiY));
        U32(output, 0);
        U32(output, 0);
        c.Consume(PdfResourceKind.ExpandedBytes, r.Stride + row);
        var inputRow = new byte[r.Stride];
        var outputRow = new byte[(int)row];
        for (int y = r.Height - 1; y >= 0; y--)
        {
            c.Checkpoint();
            using (var input = samples.Open((long)y * r.Stride, r.Stride)) ReadRow(input, inputRow);
            for (int x = 0; x < r.Width; x++)
            {
                if ((x & 1023) == 0) c.Checkpoint();
                outputRow[x * 3] = inputRow[x * 4];
                outputRow[x * 3 + 1] = inputRow[x * 4 + 1];
                outputRow[x * 3 + 2] = inputRow[x * 4 + 2];
            }
            output.Write(outputRow, 0, outputRow.Length);
        }
    }
    private static void ReadRow(Stream input, byte[] row)
    {
        int position = 0;
        while (position < row.Length)
        {
            int n = input.Read(row, position, row.Length - position);
            if (n == 0) throw new EndOfStreamException();
            position += n;
        }
    }
    private static void Tiff(Stream output, PdfRasterRequest r, PdfByteStore samples, byte[]? profile, int frame, int count, PdfOperationContext c)
    {
        int entries = profile == null ? 16 : 17;
        long start = output.Position, extra = start + 2 + entries * 12 + 4, data = extra + 24 + (profile == null ? 0 : profile.Length + (profile.Length & 1));
        long end = data + r.ByteLength;
        if (end > uint.MaxValue)
            throw PdfWriterCore.Error(c, "Raster.ContainerExtent", "Classic TIFF exceeds its32-bit offset profile.");
        U16(output, (ushort)entries);
        void Entry(ushort tag, ushort type, uint n, uint value)
        {
            U16(output, tag);
            U16(output, type);
            U32(output, n);
            U32(output, value);
        }
        Entry(256, 4, 1, (uint)r.Width);
        Entry(257, 4, 1, (uint)r.Height);
        Entry(258, 3, 4, (uint)extra);
        Entry(259, 3, 1, 1);
        Entry(262, 3, 1, 2);
        Entry(273, 4, 1, (uint)data);
        Entry(274, 3, 1, 1);
        Entry(277, 3, 1, 4);
        Entry(278, 4, 1, (uint)r.Height);
        Entry(279, 4, 1, (uint)r.ByteLength);
        Entry(282, 5, 1, (uint)(extra + 8));
        Entry(283, 5, 1, (uint)(extra + 16));
        Entry(284, 3, 1, 1);
        Entry(296, 3, 1, 2);
        Entry(297, 3, 2, (uint)frame | ((uint)count << 16));
        Entry(338, 3, 1, 2);
        if (profile != null)
            Entry(34675, 7, (uint)profile.Length, (uint)(extra + 24));
        U32(output, frame + 1 == count ? 0 : (uint)end);
        for (int i = 0; i < 4; i++)
            U16(output, 8);
        U32(output, (uint)Math.Round(r.EffectiveDpiX * 10000));
        U32(output, 10000);
        U32(output, (uint)Math.Round(r.EffectiveDpiY * 10000));
        U32(output, 10000);
        if (profile != null)
        {
            output.Write(profile, 0, profile.Length);
            if ((profile.Length & 1) != 0)
                output.WriteByte(0);
        }
        using var rows = new RgbaRows(samples, r, false, c);
        var part = new byte[8192];
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        int n;
        while ((n = rows.Read(part, 0, part.Length)) > 0)
        {
            c.Checkpoint();
            output.Write(part, 0, n);
        }
    }
    private sealed class RgbaRows : Stream
    {
        private readonly PdfByteStore _samples;
        private readonly PdfRasterRequest _r;
        private readonly bool _filter;
        private readonly PdfOperationContext _c;
        private readonly byte[] _row;
        private int _rowPosition, _nextRow;
        private long _position;
        internal RgbaRows(PdfByteStore samples, PdfRasterRequest request, bool filter, PdfOperationContext c)
        {
            _samples = samples;
            _r = request;
            _filter = filter;
            _c = c;
            c.Consume(PdfResourceKind.ExpandedBytes, request.Stride + (filter ? 1L : 0));
            _row = new byte[request.Stride + (filter ? 1 : 0)];
            _rowPosition = _row.Length;
        }
        public override int Read(byte[] buffer, int offset, int count)
        {
            if (buffer == null) throw new ArgumentNullException(nameof(buffer));
            if (offset < 0 || count < 0 || offset > buffer.Length - count) throw new ArgumentOutOfRangeException(nameof(count));
            int copied = 0;
            while (copied < count && _position < Length)
            {
                _c.Checkpoint();
                if (_rowPosition == _row.Length)
                {
                    int prefix = _filter ? 1 : 0;
                    using (var input = _samples.Open((long)_nextRow++ * _r.Stride, _r.Stride))
                    {
                        int at = prefix;
                        while (at < _row.Length)
                        {
                            int n = input.Read(_row, at, _row.Length - at);
                            if (n == 0) throw new EndOfStreamException();
                            at += n;
                        }
                    }
                    for (int at = prefix; at < _row.Length; at += 4)
                    {
                        if ((at & 4095) < 4) _c.Checkpoint();
                        byte blue = _row[at];
                        _row[at] = _row[at + 2];
                        _row[at + 2] = blue;
                    }
                    _rowPosition = 0;
                }
                int size = Math.Min(count - copied, _row.Length - _rowPosition);
                Buffer.BlockCopy(_row, _rowPosition, buffer, offset + copied, size);
                _rowPosition += size;
                _position += size;
                copied += size;
            }
            return copied;
        }
        public override long Length => _r.ByteLength + (_filter ? _r.Height : 0); public override long Position
        {
            get => _position; set => throw new NotSupportedException();
        }
        public override bool CanRead => true; public override bool CanSeek => false; public override bool CanWrite => false; public override void Flush()
        {
        }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException(); public override void SetLength(long value) => throw new NotSupportedException(); public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
    private static uint Density(double dpi) => (uint)Math.Round(dpi / 0.0254);
    private static void Big(byte[] bytes, int at, uint value)
    {
        for (int i = 3; i >= 0; i--)
        {
            bytes[at + i] = (byte)value;
            value >>= 8;
        }
    }
    private static void U16(Stream output, ushort value)
    {
        output.WriteByte((byte)value);
        output.WriteByte((byte)(value >> 8));
    }
    private static void U32(Stream output, uint value)
    {
        for (int i = 0; i < 4; i++)
        {
            output.WriteByte((byte)value);
            value >>= 8;
        }
    }
    private static void Copy(PdfByteStore bytes, Stream output, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 65536);
        var part = new byte[65536];
        using var input = bytes.Open(0, bytes.Length);
        using var sink = new BorrowedStream(output, false, c);
        int n;
        while ((n = input.Read(part, 0, part.Length)) > 0)
        {
            c.Checkpoint();
            sink.Write(part, 0, n);
        }
    }
}
