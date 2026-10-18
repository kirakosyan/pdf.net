using System.IO;
using System.IO.Compression;

namespace Fluent.Pdf;
internal static class PdfFilterEncoders
{
    internal static PdfByteStore Hex(Stream input, PdfOperationContext context)
    {
        const string digits = "0123456789ABCDEF";
        var result = new PdfByteBuilder(context);
        int c;
        while ((c = input.ReadByte()) >= 0)
        {
            context.Checkpoint();
            result.Add((byte)digits[c >> 4]);
            result.Add((byte)digits[c & 15]);
        }

        result.Add((byte)'>');
        return result.Build();
    }

    internal static PdfByteStore Ascii85(Stream input, PdfOperationContext context)
    {
        var output = new PdfByteBuilder(context);
        context.Consume(PdfResourceKind.ExpandedBytes, 5);
        var digits = new byte[5];
        while (true)
        {
            context.Checkpoint();
            uint value = 0;
            int count = 0;
            for (int i = 0; i < 4; i++)
            {
                int b = input.ReadByte();
                value <<= 8;
                if (b >= 0)
                {
                    value |= (uint)b;
                    count++;
                }
            }

            if (count == 0)
                break;
            if (value == 0 && count == 4)
            {
                output.Add((byte)'z');
                continue;
            }

            for (int i = 4; i >= 0; i--)
            {
                digits[i] = (byte)(value % 85 + 33);
                value /= 85;
            }

            for (int i = 0; i < count + 1; i++)
                output.Add(digits[i]);
            if (count < 4)
                break;
        }

        output.Add((byte)'~');
        output.Add((byte)'>');
        return output.Build();
    }

    internal static PdfByteStore RunLength(PdfByteStore input, PdfOperationContext context)
    {
        var output = new PdfByteBuilder(context);
        long position = 0;
        int Repeat(long start)
        {
            int count = 1;
            while (count < 128 && start + count < input.Length && input.At(start + count) == input.At(start))
                count++;
            return count;
        }

        while (position < input.Length)
        {
            context.Checkpoint();
            int run = Repeat(position);
            if (run >= 3)
            {
                output.Add((byte)(257 - run));
                output.Add((byte)input.At(position));
                position += run;
                continue;
            }

            long start = position;
            position += run;
            while (position < input.Length && position - start < 128)
            {
                int next = Repeat(position);
                if (next >= 3)
                    break;
                position += Math.Min(next, 128 - (int)(position - start));
            }

            int literal = (int)(position - start);
            output.Add((byte)(literal - 1));
            for (int i = 0; i < literal; i++)
                output.Add((byte)input.At(start + i));
        }

        output.Add(128);
        return output.Build();
    }

    internal static PdfByteStore Flate(Stream source, PdfOperationContext context)
    {
        var output = new PdfByteBuilder(context);
        output.Add(0x78);
        output.Add(0x9C);
        uint a = 1, b = 0;
        int first = source.ReadByte();
        if (first < 0)
        {
            // DeflateStream can emit no block when it has never received input.
            // An empty zlib stream still needs a final block and Adler-32.
            output.Add(3);
            output.Add(0);
            output.Add(0);
            output.Add(0);
            output.Add(0);
            output.Add(1);
            return output.Build();
        }

        context.Consume(PdfResourceKind.ExpandedBytes, 4096);
        var buffer = new byte[4096];
        using (var sink = new BuilderStream(output))
        using (var deflate = new DeflateStream(sink, CompressionLevel.Optimal, true))
        {
            buffer[0] = (byte)first;
            deflate.Write(buffer, 0, 1);
            a = (a + (uint)first) % 65521;
            b = a;
            int read;
            while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
            {
                context.Checkpoint();
                for (int i = 0; i < read; i++)
                {
                    a = (a + buffer[i]) % 65521;
                    b = (b + a) % 65521;
                }

                deflate.Write(buffer, 0, read);
                context.Checkpoint();
            }
        }

        uint checksum = b << 16 | a;
        for (int shift = 24; shift >= 0; shift -= 8)
            output.Add((byte)(checksum >> shift));
        return output.Build();
    }

    private sealed class BuilderStream : Stream
    {
        private readonly PdfByteBuilder _output;
        internal BuilderStream(PdfByteBuilder output) => _output = output;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _output.Length;
        public override long Position { get => _output.Length; set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
                _output.Add(buffer[offset + i]);
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
