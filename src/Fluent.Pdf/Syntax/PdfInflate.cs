using System.IO;

namespace Fluent.Pdf;
// Original RFC 1950/1951 decoder. It checks final blocks, canonical trees, distances,
// stored-block complements and Adler-32 instead of accepting truncated raw output.
internal static class PdfInflate
{
    private static readonly int[] LengthBase =
    {
        3,
        4,
        5,
        6,
        7,
        8,
        9,
        10,
        11,
        13,
        15,
        17,
        19,
        23,
        27,
        31,
        35,
        43,
        51,
        59,
        67,
        83,
        99,
        115,
        131,
        163,
        195,
        227,
        258
    };
    private static readonly int[] LengthExtra =
    {
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        0,
        1,
        1,
        1,
        1,
        2,
        2,
        2,
        2,
        3,
        3,
        3,
        3,
        4,
        4,
        4,
        4,
        5,
        5,
        5,
        5,
        0
    };
    private static readonly int[] DistanceBase =
    {
        1,
        2,
        3,
        4,
        5,
        7,
        9,
        13,
        17,
        25,
        33,
        49,
        65,
        97,
        129,
        193,
        257,
        385,
        513,
        769,
        1025,
        1537,
        2049,
        3073,
        4097,
        6145,
        8193,
        12289,
        16385,
        24577
    };
    private static readonly int[] DistanceExtra =
    {
        0,
        0,
        0,
        0,
        1,
        1,
        2,
        2,
        3,
        3,
        4,
        4,
        5,
        5,
        6,
        6,
        7,
        7,
        8,
        8,
        9,
        9,
        10,
        10,
        11,
        11,
        12,
        12,
        13,
        13
    };
    internal static PdfByteStore Decode(Stream source, PdfOperationContext context)
    {
        var bits = new Bits(source, context);
        int cmf = bits.Byte(), flg = bits.Byte();
        if ((cmf & 15) != 8 || (cmf >> 4) > 7 || ((cmf << 8) + flg) % 31 != 0 || (flg & 32) != 0)
            throw Bad(context, "The zlib header or preset-dictionary flag is unsupported or malformed.");
        int windowSize = 1 << ((cmf >> 4) + 8);
        context.Consume(PdfResourceKind.ExpandedBytes, 32768);
        var window = new byte[32768];
        int position = 0;
        uint a = 1, b = 0;
        var output = new PdfByteBuilder(context);
        void Emit(byte value)
        {
            output.Add(value);
            window[position] = value;
            position = (position + 1) & 32767;
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }

        bool final;
        do
        {
            context.Checkpoint();
            final = bits.Read(1) != 0;
            int type = bits.Read(2);
            if (type == 0)
            {
                bits.Align();
                int length = bits.Read(16), inverse = bits.Read(16);
                if ((length ^ inverse) != 65535)
                    throw Bad(context, "A stored block has an invalid length complement.");
                for (int i = 0; i < length; i++)
                {
                    if ((i & 1023) == 0)
                        context.Checkpoint();
                    Emit((byte)bits.Read(8));
                }

                continue;
            }

            if (type == 3)
                throw Bad(context, "A deflate block uses a reserved type.");
            Huffman literal, distance;
            if (type == 1)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, (288 + 32) * 4);
                var lengths = new int[288];
                for (int i = 0; i < 288; i++)
                    lengths[i] = i < 144 ? 8 : i < 256 ? 9 : i < 280 ? 7 : 8;
                var distances = new int[32];
                for (int i = 0; i < 32; i++)
                    distances[i] = 5;
                literal = new(lengths, context, false);
                distance = new(distances, context, false);
            }
            else
            {
                int count = bits.Read(5) + 257, dc = bits.Read(5) + 1, cc = bits.Read(4) + 4;
                if (count > 286)
                    throw Bad(context, "A dynamic block declares reserved literal symbols.");
                int[] order =
                {
                    16,
                    17,
                    18,
                    0,
                    8,
                    7,
                    9,
                    6,
                    10,
                    5,
                    11,
                    4,
                    12,
                    3,
                    13,
                    2,
                    14,
                    1,
                    15
                };
                context.Consume(PdfResourceKind.ExpandedBytes, (19 + count + dc + count + dc) * 4L);
                var codeLengths = new int[19];
                for (int i = 0; i < cc; i++)
                    codeLengths[order[i]] = bits.Read(3);
                var codes = new Huffman(codeLengths, context, true);
                var lengths = new int[count + dc];
                int index = 0;
                while (index < lengths.Length)
                {
                    context.Checkpoint();
                    int symbol = codes.Decode(bits), repeat, value;
                    if (symbol <= 15)
                    {
                        lengths[index++] = symbol;
                        continue;
                    }

                    if (symbol == 16)
                    {
                        if (index == 0)
                            throw Bad(context, "A code-length repeat has no predecessor.");
                        repeat = bits.Read(2) + 3;
                        value = lengths[index - 1];
                    }
                    else if (symbol == 17)
                    {
                        repeat = bits.Read(3) + 3;
                        value = 0;
                    }
                    else if (symbol == 18)
                    {
                        repeat = bits.Read(7) + 11;
                        value = 0;
                    }
                    else
                        throw Bad(context, "An invalid code-length symbol was encountered.");
                    if (repeat > lengths.Length - index)
                        throw Bad(context, "A code-length repeat exceeds its declared alphabet.");
                    while (repeat-- > 0)
                        lengths[index++] = value;
                }

                var literals = new int[count];
                var distances = new int[dc];
                System.Array.Copy(lengths, 0, literals, 0, count);
                System.Array.Copy(lengths, count, distances, 0, dc);
                if (literals[256] == 0)
                    throw Bad(context, "A literal tree has no end-of-block symbol.");
                literal = new(literals, context, false);
                distance = new(distances, context, false);
            }

            while (true)
            {
                context.Checkpoint();
                int symbol = literal.Decode(bits);
                if (symbol == 256)
                    break;
                if (symbol < 256)
                {
                    Emit((byte)symbol);
                    continue;
                }

                if (symbol > 285)
                    throw Bad(context, "A reserved length symbol was encountered.");
                int length = LengthBase[symbol - 257] + bits.Read(LengthExtra[symbol - 257]), ds = distance.Decode(bits);
                if (ds >= 30)
                    throw Bad(context, "A reserved distance symbol was encountered.");
                int backwards = DistanceBase[ds] + bits.Read(DistanceExtra[ds]);
                if (backwards > windowSize || backwards > output.Length)
                    throw Bad(context, "A copy distance precedes the available history window.");
                for (int i = 0; i < length; i++)
                    Emit(window[(position - backwards) & 32767]);
            }
        }
        while (!final);
        bits.Align();
        uint checksum = 0;
        for (int i = 0; i < 4; i++)
            checksum = (checksum << 8) | (uint)bits.Byte();
        if (checksum != (b << 16 | a))
            throw Bad(context, "The zlib checksum disagrees with the decoded stream.");
        if (source.ReadByte() != -1)
            throw Bad(context, "Unexpected bytes follow the zlib stream.");
        return output.Build();
    }

    private static PdfException Bad(PdfOperationContext context, string message) => PdfStreamFilters.Error(context, "Filter.FlateMalformed", message);
    private sealed class Bits
    {
        private readonly Stream _source;
        private readonly PdfOperationContext _context;
        private uint _buffer;
        private int _available;
        internal Bits(Stream source, PdfOperationContext context)
        {
            _source = source;
            _context = context;
        }

        internal int Byte()
        {
            int b = _source.ReadByte();
            if (b < 0)
                throw PdfStreamFilters.Error(_context, "Filter.Truncated", "A compressed stream is truncated.");
            return b;
        }

        internal int Read(int count)
        {
            while (_available < count)
            {
                _buffer |= (uint)Byte() << _available;
                _available += 8;
            }

            int value = (int)(_buffer & ((1u << count) - 1));
            _buffer >>= count;
            _available -= count;
            return value;
        }

        internal void Align()
        {
            _buffer = 0;
            _available = 0;
        }
    }

    private sealed class Huffman
    {
        private readonly int[] _counts, _symbols;
        private readonly PdfOperationContext _context;
        internal Huffman(int[] lengths, PdfOperationContext context, bool complete)
        {
            _context = context;
            context.Consume(PdfResourceKind.ExpandedBytes, (16 * 2 + lengths.Length) * 4L);
            _counts = new int[16];
            _symbols = new int[lengths.Length];
            int[] offsets = new int[16];
            int left = 1, total = 0;
            foreach (int length in lengths)
            {
                if (length < 0 || length > 15)
                    throw Bad(context, "A Huffman code length is outside range.");
                if (length > 0)
                {
                    _counts[length]++;
                    total++;
                }
            }

            for (int i = 1; i <= 15; i++)
            {
                left = (left << 1) - _counts[i];
                if (left < 0)
                    throw Bad(context, "A Huffman tree is oversubscribed.");
                if (i < 15)
                    offsets[i + 1] = offsets[i] + _counts[i];
            }

            if (left > 0 && (complete || (total != 0 && !(total == 1 && _counts[1] == 1))))
                throw Bad(context, "A Huffman tree is incomplete.");
            for (int i = 0; i < lengths.Length; i++)
                if (lengths[i] > 0)
                    _symbols[offsets[lengths[i]]++] = i;
        }

        internal int Decode(Bits bits)
        {
            int code = 0, first = 0, index = 0;
            for (int length = 1; length <= 15; length++)
            {
                code = (code << 1) | bits.Read(1);
                int count = _counts[length];
                if (code - first < count && code >= first)
                    return _symbols[index + code - first];
                index += count;
                first = (first + count) << 1;
            }

            throw Bad(_context, "A bit sequence has no Huffman symbol.");
        }
    }
}
