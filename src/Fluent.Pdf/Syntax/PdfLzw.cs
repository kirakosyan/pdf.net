using System.IO;

namespace Fluent.Pdf;
internal static class PdfLzw
{
    internal static PdfByteStore Decode(Stream source, int early, PdfOperationContext context)
    {
        CheckEarly(early, context);
        context.Consume(PdfResourceKind.ExpandedBytes, 4096 * 4);
        var prefix = new short[4096];
        var suffix = new byte[4096];
        var stack = new byte[4096];
        var output = new PdfByteBuilder(context);
        int width = 9, next = 258, previous = -1;
        var bits = new Reader(source, context);
        bool started = false;
        while (true)
        {
            context.Checkpoint();
            int code = bits.Read(width);
            if (!started && code != 256)
                throw Bad(context, "An LZW stream must begin with a clear code.");
            started = true;
            if (code == 256)
            {
                width = 9;
                next = 258;
                previous = -1;
                continue;
            }

            if (code == 257)
                break;
            int count = 0, current = code;
            bool special = code == next && previous >= 0;
            if (code > next || code == next && !special || code >= 256 && code < 258)
                throw Bad(context, "An LZW code is not in the current dictionary.");
            if (special)
                current = previous;
            while (current >= 256)
            {
                if (current >= next || count >= 4095)
                    throw Bad(context, "An LZW prefix chain is invalid.");
                stack[count++] = suffix[current];
                current = prefix[current];
            }

            byte first = (byte)current;
            stack[count++] = first;
            for (int i = count - 1; i >= 0; i--)
                output.Add(stack[i]);
            if (special)
                output.Add(first);
            if (previous >= 0 && next < 4096)
            {
                prefix[next] = (short)previous;
                suffix[next] = first;
                next++;
                if (width < 12 && next + early == (1 << width))
                    width++;
            }

            previous = code;
        }

        PdfStreamFilters.Trailing(source, context);
        return output.Build();
    }

    internal static PdfByteStore Encode(Stream source, int early, PdfOperationContext context)
    {
        CheckEarly(early, context);
        context.Consume(PdfResourceKind.ExpandedBytes, 4096 * 40);
        var dictionary = new Dictionary<(int, byte), int>(4096);
        var output = new PdfByteBuilder(context);
        var bits = new Writer(output);
        int width = 9, next = 258, current = source.ReadByte();
        bits.Write(256, width);
        if (current < 0)
        {
            bits.Write(257, width);
            bits.Finish();
            return output.Build();
        }

        while (true)
        {
            context.Checkpoint();
            int value = source.ReadByte();
            if (value < 0)
                break;
            if (dictionary.TryGetValue((current, (byte)value), out int code))
            {
                current = code;
                continue;
            }

            bits.Write(current, width);
            if (next < 4096)
            {
                dictionary.Add((current, (byte)value), next++);
                if (width < 12 && next + early > (1 << width))
                    width++;
            }
            else
            {
                bits.Write(256, width);
                dictionary.Clear();
                next = 258;
                width = 9;
            }

            current = value;
        }

        bits.Write(current, width);
        if (width < 12 && next + early == (1 << width))
            width++;
        bits.Write(257, width);
        bits.Finish();
        return output.Build();
    }

    private static void CheckEarly(int early, PdfOperationContext context)
    {
        if (early != 0 && early != 1)
            throw PdfStreamFilters.Error(context, "Filter.Parameters", "LZW EarlyChange must be zero or one.");
    }

    private static PdfException Bad(PdfOperationContext context, string message) => PdfStreamFilters.Error(context, "Filter.LzwMalformed", message);
    private sealed class Reader
    {
        private readonly Stream _source;
        private readonly PdfOperationContext _context;
        private uint _buffer;
        private int _count;
        internal Reader(Stream source, PdfOperationContext context)
        {
            _source = source;
            _context = context;
        }

        internal int Read(int width)
        {
            while (_count < width)
            {
                int b = _source.ReadByte();
                if (b < 0)
                    throw PdfStreamFilters.Error(_context, "Filter.Truncated", "An LZW stream is truncated.");
                _buffer = (_buffer << 8) | (uint)b;
                _count += 8;
            }

            _count -= width;
            return (int)((_buffer >> _count) & ((1u << width) - 1));
        }
    }

    private sealed class Writer
    {
        private readonly PdfByteBuilder _output;
        private uint _buffer;
        private int _count;
        internal Writer(PdfByteBuilder output) => _output = output;
        internal void Write(int code, int width)
        {
            _buffer = (_buffer << width) | (uint)code;
            _count += width;
            while (_count >= 8)
            {
                _count -= 8;
                _output.Add((byte)(_buffer >> _count));
            }
        }

        internal void Finish()
        {
            if (_count > 0)
                _output.Add((byte)(_buffer << (8 - _count)));
            _count = 0;
        }
    }
}
