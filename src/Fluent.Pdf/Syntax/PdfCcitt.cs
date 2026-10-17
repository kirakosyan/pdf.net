using System.IO;

namespace Fluent.Pdf;
// Original T.4/T.6 implementation using independently observed standard code facts.
internal static class PdfCcitt
{
    private static readonly Dictionary<int, int> White = Codes(PdfCcittCodes.WhiteTerminating, PdfCcittCodes.WhiteMakeup);
    private static readonly Dictionary<int, int> Black = Codes(PdfCcittCodes.BlackTerminating, PdfCcittCodes.BlackMakeup);
    private static Dictionary<int, int> Codes(string[] terminating, string[] makeup)
    {
        var result = new Dictionary<int, int>();
        void Add(string text, int value)
        {
            int key = 1;
            foreach (char bit in text)
                key = (key << 1) | (bit - '0');
            result.Add(key, value);
        }

        for (int i = 0; i < terminating.Length; i++)
            Add(terminating[i], i);
        for (int i = 1; i < makeup.Length; i++)
            Add(makeup[i], i * 64);
        return result;
    }

    internal static PdfByteStore Decode(Stream source, PdfValue parameters, PdfValue image, PdfOperationContext context)
    {
        var options = new Options(parameters, image, context, false);
        context.ConsumePixels(options.Columns, options.Rows == 0 ? 1 : options.Rows);
        var data = PdfByteStore.Read(source, context);
        var input = new Reader(data, context);
        int columns = options.Columns, rowSize = (int)(((long)columns + 7) / 8);
        context.Consume(PdfResourceKind.ExpandedBytes, checked(((long)columns + 4) * 8 + rowSize));
        var previous = new int[checked(columns + 4)];
        var changes = new int[checked(columns + 4)];
        var row = new byte[rowSize];
        previous[0] = columns;
        previous[1] = columns;
        int previousCount = 2, rows = 0;
        var output = new PdfByteBuilder(context);
        bool ended = false;
        while (options.Rows == 0 || rows < options.Rows)
        {
            context.Checkpoint();
            if (input.EndMarker(options.K < 0, options.K > 0, options.ByteAlign))
            {
                ended = true;
                if (options.Rows != 0 && rows != options.Rows)
                    throw Error(context, "The fax end marker precedes the declared image height.");
                break;
            }

            if (rows == int.MaxValue)
                throw Error(context, "A fax stream has too many rows.");
            if (options.Rows == 0 && rows > 0)
                context.ConsumePixels(columns, 1);
            context.Consume(PdfResourceKind.Objects, 1);
            for (int i = 0; i < row.Length; i++)
                row[i] = options.BlackIsOne ? (byte)0 : (byte)255;
            bool oneDimensional = options.K == 0;
            if (options.K >= 0)
            {
                bool eol = input.Eol();
                if (!eol && options.ByteAlign)
                {
                    input.Align();
                    eol = input.Eol();
                }

                if ((options.EndOfLine || options.K > 0) && !eol)
                    throw Error(context, "A required fax end-of-line marker is missing.");
                if (options.K > 0)
                    oneDimensional = input.Read(1) != 0;
            }
            else if (options.ByteAlign)
                input.Align();
            int count = 0;
            void Change(int x)
            {
                if (count > 0 && changes[count - 1] == x)
                {
                    count--;
                    return;
                }

                if (count >= columns + 1)
                    throw Error(context, "A fax row has too many changing elements.");
                changes[count++] = x;
            }

            void Fill(int start, int end, bool black)
            {
                if (start < 0 || end < start || end > columns)
                    throw Error(context, "A fax run extends beyond its row.");
                if (!black || start == end)
                    return;
                for (int index = start / 8; index <= (end - 1) / 8; index++)
                {
                    if ((index & 1023) == 0)
                        context.Checkpoint();
                    long origin = index * 8L;
                    int lo = (int)(Math.Max(start, origin) - origin), hi = (int)(Math.Min(end, origin + 8) - origin);
                    byte mask = (byte)((255 >> lo) & (255 << (8 - hi)));
                    if (options.BlackIsOne)
                        row[index] |= mask;
                    else
                        row[index] &= (byte)~mask;
                }
            }

            if (oneDimensional)
            {
                int x = 0;
                bool black = false;
                while (x < columns)
                {
                    context.Checkpoint();
                    int length = input.Run(black, columns - x);
                    Fill(x, x + length, black);
                    x += length;
                    Change(x);
                    black = !black;
                }
            }
            else
            {
                int a0 = -1, referenceIndex = 0;
                bool black = false;
                while (a0 < columns)
                {
                    context.Checkpoint();
                    while (referenceIndex < previousCount && previous[referenceIndex] <= a0)
                        referenceIndex++;
                    int index = referenceIndex;
                    if ((index & 1) != (black ? 1 : 0))
                        index++;
                    int b1 = index < previousCount ? previous[index] : columns, b2 = index + 1 < previousCount ? previous[index + 1] : columns;
                    int mode = input.Mode(), start = Math.Max(a0, 0);
                    if (mode == 100)
                    {
                        if (b2 < start)
                            throw Error(context, "A fax pass mode moves backwards.");
                        Fill(start, b2, black);
                        a0 = b2;
                    }
                    else if (mode == 101)
                    {
                        int first = input.Run(black, columns - start), second = input.Run(!black, columns - start - first);
                        if (first + second == 0)
                            throw Error(context, "A horizontal fax mode makes no progress.");
                        int a1 = start + first, a2 = a1 + second;
                        Fill(start, a1, black);
                        Fill(a1, a2, !black);
                        Change(a1);
                        Change(a2);
                        a0 = a2;
                    }
                    else
                    {
                        int a1 = checked(b1 + mode);
                        Fill(start, a1, black);
                        Change(a1);
                        a0 = a1;
                        black = !black;
                    }
                }
            }

            changes[count++] = columns;
            changes[count++] = columns;
            previousCount = count;
            var swap = previous;
            previous = changes;
            changes = swap;
            for (int i = 0; i < row.Length; i++)
                output.Add(row[i]);
            rows++;
        }

        if (!ended)
            ended = input.EndMarker(options.K < 0, options.K > 0, options.ByteAlign);
        if (options.EndOfBlock && !ended)
            throw Error(context, "A required fax end-of-block marker is missing.");
        if (rows == 0)
            throw Error(context, "A fax stream contains no image rows.");
        input.Padding();
        return output.Build();
    }

    internal static void PreflightEncoding(PdfValue parameters, PdfOperationContext context)
    {
        var options = new Options(parameters, PdfValue.Null, context, true);
        context.ConsumePixels(options.Columns, options.Rows);
    }

    internal static PdfByteStore Encode(PdfByteStore source, PdfValue parameters, PdfOperationContext context, bool pixelsCharged = false)
    {
        var options = new Options(parameters, PdfValue.Null, context, true);
        int width = options.Columns;
        long rowSize = ((long)width + 7) / 8;
        if (!pixelsCharged)
            context.ConsumePixels(width, options.Rows);
        if (source.Length != checked(rowSize * options.Rows))
            throw Error(context, "Fax encoding input must contain complete rows matching Columns and Rows.");
        var output = new PdfByteBuilder(context);
        var bits = new Writer(output);
        void Run(int length, bool black)
        {
            string[] makeup = black ? PdfCcittCodes.BlackMakeup : PdfCcittCodes.WhiteMakeup, terminating = black ? PdfCcittCodes.BlackTerminating : PdfCcittCodes.WhiteTerminating;
            while (length >= 64)
            {
                int units = Math.Min(40, length / 64);
                bits.Code(makeup[units]);
                length -= units * 64;
            }

            bits.Code(terminating[length]);
        }

        bool BlackAt(int x, int y)
        {
            int sample = (source.At(y * rowSize + x / 8) >> (7 - (x & 7))) & 1;
            return options.BlackIsOne ? sample == 1 : sample == 0;
        }

        for (int y = 0; y < options.Rows; y++)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            if (options.ByteAlign)
                bits.Align();
            bool twoDimensional = options.K < 0 || options.K > 0 && y % options.K != 0;
            if (options.K >= 0 && (options.EndOfLine || options.K > 0))
                bits.Code("000000000001");
            if (options.K > 0)
                bits.Code(twoDimensional ? "0" : "1");
            int x = 0;
            bool black = false;
            while (x < width)
            {
                context.Checkpoint();
                if (twoDimensional)
                    bits.Code("001");
                int start = x;
                while (x < width && BlackAt(x, y) == black)
                {
                    if ((x & 1023) == 0)
                        context.Checkpoint();
                    x++;
                }

                Run(x - start, black);
                if (!twoDimensional)
                {
                    black = !black;
                    continue;
                }

                start = x;
                while (x < width && BlackAt(x, y) != black)
                {
                    if ((x & 1023) == 0)
                        context.Checkpoint();
                    x++;
                }

                Run(x - start, !black);
            }
        }

        if (options.ByteAlign)
            bits.Align();
        if (options.EndOfBlock)
            for (int i = 0; i < (options.K < 0 ? 2 : 6); i++)
            {
                bits.Code("000000000001");
                if (options.K > 0)
                    bits.Code("1");
            }

        bits.Align();
        return output.Build();
    }

    private static PdfException Error(PdfOperationContext context, string message) => PdfStreamFilters.Error(context, "Filter.CcittMalformed", message);
    private sealed class Options
    {
        internal readonly int K, Columns, Rows;
        internal readonly bool EndOfLine, ByteAlign, EndOfBlock, BlackIsOne;
        internal Options(PdfValue parameters, PdfValue image, PdfOperationContext context, bool encoding)
        {
            K = PdfStreamFilters.Parameter(parameters, "K", 0, context);
            Columns = PdfStreamFilters.Parameter(parameters, "Columns", 1728, context);
            Rows = PdfStreamFilters.Parameter(parameters, "Rows", 0, context);
            EndOfLine = Boolean(parameters, "EndOfLine", false, context);
            ByteAlign = Boolean(parameters, "EncodedByteAlign", false, context);
            EndOfBlock = Boolean(parameters, "EndOfBlock", true, context);
            BlackIsOne = Boolean(parameters, "BlackIs1", false, context);
            if (Columns <= 0 || Rows < 0)
                throw PdfStreamFilters.Error(context, "Filter.Parameters", "Fax Columns must be positive and Rows nonnegative.");
            if (image.Kind != PdfValueKind.Null)
            {
                int width = PdfStreamFilters.Parameter(image, "Width", Columns, context), height = PdfStreamFilters.Parameter(image, "Height", Rows, context);
                if (width != Columns || Rows != 0 && height != Rows || height < 0)
                    throw Error(context, "Fax image dimensions disagree with Columns or Rows.");
                if (Rows == 0)
                    Rows = height;
            }

            if (Rows == 0 && (encoding || !EndOfBlock))
                throw PdfStreamFilters.Error(context, "Filter.Parameters", "A row count is required when encoding or when no end-of-block marker is expected.");
            if (PdfStreamFilters.Parameter(parameters, "DamagedRowsBeforeError", 0, context) != 0)
                throw PdfStreamFilters.Error(context, "Filter.CcittRecoveryUnsupported", "Damaged fax-row recovery requires an explicit repair implementation.");
        }

        private static bool Boolean(PdfValue dictionary, string key, bool fallback, PdfOperationContext context)
        {
            if (dictionary.Kind == PdfValueKind.Null || !dictionary.Entries.TryGetValue(key, out var value) || value.Kind == PdfValueKind.Null)
                return fallback;
            if (value.Kind != PdfValueKind.Boolean)
                throw PdfStreamFilters.Error(context, "Filter.Parameters", "A fax Boolean parameter has the wrong kind.");
            return value.BooleanValue;
        }
    }

    private sealed class Reader
    {
        private readonly PdfByteStore _data;
        private readonly PdfOperationContext _context;
        private long _position;
        internal Reader(PdfByteStore data, PdfOperationContext context)
        {
            _data = data;
            _context = context;
        }

        internal int Read(int count)
        {
            int result = 0;
            for (int i = 0; i < count; i++)
            {
                if (_position >= _data.Length * 8)
                    throw PdfStreamFilters.Error(_context, "Filter.Truncated", "A fax code is truncated.");
                result = (result << 1) | ((_data.At(_position / 8) >> (7 - (int)(_position % 8))) & 1);
                _position++;
            }

            return result;
        }

        internal void Align()
        {
            while (_position % 8 != 0 && _position < _data.Length * 8)
                if (Read(1) != 0)
                    throw Error(_context, "Fax byte-alignment padding is nonzero.");
        }

        internal bool Eol()
        {
            long start = _position;
            int zero = 0;
            while (_position < _data.Length * 8 && zero <= 18)
            {
                int bit = Read(1);
                if (bit != 0)
                {
                    if (zero >= 11)
                        return true;
                    break;
                }

                zero++;
            }

            _position = start;
            return false;
        }

        internal bool EndMarker(bool group4, bool mixed, bool byteAlign)
        {
            long start = _position;
            bool Try()
            {
                for (int i = 0; i < (group4 ? 2 : 6); i++)
                {
                    if (!Eol())
                        return false;
                    if (mixed)
                    {
                        if (_position >= _data.Length * 8 || Read(1) != 1)
                            return false;
                    }
                }

                return true;
            }

            if (Try())
                return true;
            _position = start;
            if (byteAlign)
            {
                Align();
                if (Try())
                    return true;
            }

            _position = start;
            return false;
        }

        internal int Run(bool black, int remaining)
        {
            int total = 0;
            while (true)
            {
                _context.Checkpoint();
                int key = 1, run = -1;
                var table = black ? Black : White;
                for (int bits = 1; bits <= 13; bits++)
                {
                    key = (key << 1) | Read(1);
                    if (table.TryGetValue(key, out run))
                        break;
                    run = -1;
                }

                if (run < 0)
                    throw Error(_context, "A fax run has no valid terminating or makeup code.");
                if (run > remaining - total)
                    throw Error(_context, "A fax run exceeds the declared column count.");
                total += run;
                if (run < 64)
                    return total;
            }
        }

        internal int Mode()
        {
            int code = 0;
            for (int length = 1; length <= 7; length++)
            {
                code = (code << 1) | Read(1);
                if (length == 1 && code == 1)
                    return 0;
                if (length == 3)
                {
                    if (code == 1)
                        return 101;
                    if (code == 2)
                        return -1;
                    if (code == 3)
                        return 1;
                }

                if (length == 4 && code == 1)
                    return 100;
                if (length == 6)
                {
                    if (code == 2)
                        return -2;
                    if (code == 3)
                        return 2;
                }

                if (length == 7)
                {
                    if (code == 2)
                        return -3;
                    if (code == 3)
                        return 3;
                    if (code == 1)
                        throw PdfStreamFilters.Error(_context, "Filter.CcittExtensionUnsupported", "The requested fax extension mode is unsupported.");
                }
            }

            throw Error(_context, "A two-dimensional fax mode is malformed.");
        }

        internal void Padding()
        {
            if (_data.Length * 8 - _position > 7)
                throw Error(_context, "Unexpected data follows the fax image.");
            while (_position < _data.Length * 8)
                if (Read(1) != 0)
                    throw Error(_context, "Nonzero padding follows the fax image.");
        }
    }

    private sealed class Writer
    {
        private readonly PdfByteBuilder _output;
        private int _value, _count;
        internal Writer(PdfByteBuilder output) => _output = output;
        internal void Code(string code)
        {
            foreach (char bit in code)
            {
                _value = (_value << 1) | (bit - '0');
                if (++_count == 8)
                {
                    _output.Add((byte)_value);
                    _value = 0;
                    _count = 0;
                }
            }
        }

        internal void Align()
        {
            if (_count > 0)
                _output.Add((byte)(_value << (8 - _count)));
            _value = 0;
            _count = 0;
        }
    }
}
