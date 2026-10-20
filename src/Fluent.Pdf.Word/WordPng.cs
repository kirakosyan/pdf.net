using System.IO.Compression;

namespace Fluent.Pdf.Word;
// Original bounded RGB PNG serialization with compressed zlib blocks; no external image/runtime discovery.
internal static class WordPng
{
    internal static PdfByteStore Write(PdfByteStore bgra, int width, int height, PdfOperationContext c)
    {
        if (width <= 0 || height <= 0)
            throw WordErrors.Error(c, "RasterLength", "Positive raster geometry is required.");
        long length = checked((width * 3L + 1) * height);
        if (length > int.MaxValue || bgra.Length != width * (long)height * 4)
            throw WordErrors.Error(c, "RasterLength", "The raster does not match its positive bounded PNG extent.");
        c.Consume(PdfResourceKind.ExpandedBytes, length + 128);
        var raw = new byte[(int)length];
        int at = 0;
        uint a = 1, b = 0;
        for (int y = 0; y < height; y++)
        {
            c.Checkpoint();
            raw[at++] = 0;
            for (int x = 0; x < width; x++)
            {
                if ((x & 4095) == 0)
                    c.Checkpoint();
                long p = (y * (long)width + x) * 4;
                if (bgra.At(p + 3) != 255)
                    throw WordErrors.Error(c, "RasterAlpha", "Fixed Word pages require the explicitly requested opaque raster.");
                raw[at++] = (byte)bgra.At(p + 2);
                raw[at++] = (byte)bgra.At(p + 1);
                raw[at++] = (byte)bgra.At(p);
            }
        }

        var zlib = new PdfByteBuilder(c);
        zlib.Add(120);
        zlib.Add(1);
        using (var compressed = new PdfCodecBufferStream(c, c.Available(PdfResourceKind.ExpandedBytes)))
        {
            using (var deflate = new DeflateStream(compressed, CompressionLevel.Optimal, leaveOpen: true))
                for (int start = 0; start < raw.Length;)
                {
                    c.Checkpoint();
                    int count = Math.Min(65536, raw.Length - start);
                    deflate.Write(raw, start, count);
                    for (int i = start; i < start + count; i++)
                    {
                        a = (a + raw[i]) % 65521;
                        b = (b + a) % 65521;
                    }
                    start += count;
                }
            var encoded = compressed.Finish();
            for (long i = 0; i < encoded.Length; i++)
            {
                if ((i & 4095) == 0) c.Checkpoint();
                zlib.Add((byte)encoded.At(i));
            }
        }

        Big(zlib, b << 16 | a);
        var output = new PdfByteBuilder(c);
        foreach (byte value in new byte[]
        {
            137,
            80,
            78,
            71,
            13,
            10,
            26,
            10
        }

        )
            output.Add(value);
        var header = new PdfByteBuilder(c);
        Big(header, (uint)width);
        Big(header, (uint)height);
        foreach (byte value in new byte[]
        {
            8,
            2,
            0,
            0,
            0
        }

        )
            header.Add(value);
        Chunk(output, "IHDR", header.Build(), c);
        Chunk(output, "IDAT", zlib.Build(), c);
        Chunk(output, "IEND", new PdfByteBuilder(c).Build(), c);
        return output.Build();
    }

    private static void Big(PdfByteBuilder output, uint value)
    {
        output.Add((byte)(value >> 24));
        output.Add((byte)(value >> 16));
        output.Add((byte)(value >> 8));
        output.Add((byte)value);
    }

    private static void Chunk(PdfByteBuilder output, string kind, PdfByteStore bytes, PdfOperationContext c)
    {
        WordErrors.Node(c);
        Big(output, checked((uint)bytes.Length));
        uint crc = uint.MaxValue;
        void Write(byte value)
        {
            output.Add(value);
            crc ^= value;
            for (int i = 0; i < 8; i++)
                crc = (crc & 1) == 0 ? crc >> 1 : (crc >> 1) ^ 0xedb88320;
        }

        foreach (char ch in kind)
            Write((byte)ch);
        for (long i = 0; i < bytes.Length; i++)
        {
            if ((i & 4095) == 0)
                c.Checkpoint();
            Write((byte)bytes.At(i));
        }

        Big(output, ~crc);
    }
}
