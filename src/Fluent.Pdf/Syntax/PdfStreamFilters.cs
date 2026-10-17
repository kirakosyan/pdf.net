using System.IO;

namespace Fluent.Pdf;
internal static class PdfStreamFilters
{
    internal static Stream OpenBootstrap(PdfValue stream, PdfOperationContext context)
    {
        if (!stream.Entries.TryGetValue("Filter", out var filter))
            return stream.OpenRawStream();
        var filters = filter.Kind == PdfValueKind.Array ? filter.Items : new[]
        {
            filter
        };
        IReadOnlyList<PdfValue>? parameters = null;
        if (stream.Entries.TryGetValue("DecodeParms", out var parms) && parms.Kind != PdfValueKind.Null)
        {
            if (filter.Kind == PdfValueKind.Array)
            {
                if (parms.Kind != PdfValueKind.Array || parms.Items.Count != filters.Count)
                    throw Error(context, "Filter.Parameters", "Filter and DecodeParms arrays must have matching lengths.");
                parameters = parms.Items;
            }
            else
                parameters = new[]
                {
                    parms
                };
        }

        Stream current = stream.OpenRawStream();
        try
        {
            for (int i = 0; i < filters.Count; i++)
            {
                context.Checkpoint();
                if (filters[i].Kind != PdfValueKind.Name)
                    throw Error(context, "Filter.Name", "A stream filter must be a name.");
                string name = filters[i].NameValue;
                PdfValue parameter = parameters?[i] ?? PdfValue.Null;
                if (parameter.Kind != PdfValueKind.Null && parameter.Kind != PdfValueKind.Dictionary)
                    throw Error(context, "Filter.Parameters", "Decode parameters must be dictionaries or null.");
                PdfByteStore decoded;
                if (name == "FlateDecode" || name == "Fl")
                    decoded = Predict(PdfInflate.Decode(current, context), parameter, context);
                else if (name == "ASCIIHexDecode" || name == "AHx")
                    decoded = Hex(current, context);
                else
                    throw Error(context, "Filter.Unsupported", "The requested encoded stream filter is unsupported.");
                current.Dispose();
                current = decoded.Open(0, decoded.Length);
            }

            return current;
        }
        catch
        {
            current.Dispose();
            throw;
        }
    }

    private static PdfByteStore Hex(Stream input, PdfOperationContext context)
    {
        var output = new PdfByteBuilder(context);
        int high = -1;
        while (true)
        {
            context.Checkpoint();
            int c = input.ReadByte();
            if (c < 0)
                throw Error(context, "Filter.Truncated", "An ASCIIHex stream is missing its terminator.");
            if (c == '>')
                break;
            if (PdfSyntaxCursor.White(c))
                continue;
            int digit = PdfSyntaxCursor.Hex(c);
            if (digit < 0)
                throw Error(context, "Filter.Malformed", "An ASCIIHex stream contains an invalid digit.");
            if (high < 0)
                high = digit;
            else
            {
                output.Add((byte)(high * 16 + digit));
                high = -1;
            }
        }

        if (high >= 0)
            output.Add((byte)(high * 16));
        Trailing(input, context);
        return output.Build();
    }

    internal static PdfByteStore Predict(PdfByteStore input, PdfValue parameters, PdfOperationContext context)
    {
        int predictor = Parameter(parameters, "Predictor", 1, context);
        if (predictor == 1)
            return input;
        int colors = Parameter(parameters, "Colors", 1, context), bits = Parameter(parameters, "BitsPerComponent", 8, context), columns = Parameter(parameters, "Columns", 1, context);
        if (colors <= 0 || columns <= 0 || (bits != 1 && bits != 2 && bits != 4 && bits != 8 && bits != 16) || (predictor != 2 && (predictor < 10 || predictor > 15)))
            throw Error(context, "Filter.Predictor", "Predictor parameters are outside the supported PDF ranges.");
        long samples = checked((long)colors * columns), rowLength = checked((samples * bits + 7) / 8), pixel = checked(((long)colors * bits + 7) / 8);
        if (rowLength > int.MaxValue)
            throw Error(context, "Filter.Predictor", "A predictor row exceeds the managed buffer range.");
        int rowSize = (int)rowLength, stride = (int)pixel;
        context.Consume(PdfResourceKind.ExpandedBytes, rowLength * 2);
        var row = new byte[rowSize];
        var previous = new byte[rowSize];
        var output = new PdfByteBuilder(context);
        using var source = input.Open(0, input.Length);
        while (true)
        {
            context.Checkpoint();
            int first = source.ReadByte();
            if (first < 0)
                break;
            int filter = predictor == 2 ? 0 : first;
            if (predictor == 2)
                row[0] = (byte)first;
            else if (filter > 4)
                throw Error(context, "Filter.Predictor", "A PNG predictor row has an invalid filter selector.");
            int start = predictor == 2 ? 1 : 0;
            for (int i = start; i < rowSize; i++)
            {
                int value = source.ReadByte();
                if (value < 0)
                    throw Error(context, "Filter.Truncated", "A predictor row is truncated.");
                row[i] = (byte)value;
            }

            if (predictor == 2)
            {
                int mask = (1 << bits) - 1;
                for (long sample = colors; sample < samples; sample++)
                {
                    if ((sample & 1023) == 0)
                        context.Checkpoint();
                    int value = (Sample(row, sample, bits) + Sample(row, sample - colors, bits)) & mask;
                    SetSample(row, sample, bits, value);
                }
            }
            else
                for (int i = 0; i < rowSize; i++)
                {
                    if ((i & 1023) == 0)
                        context.Checkpoint();
                    int left = i >= stride ? row[i - stride] : 0, up = previous[i], diagonal = i >= stride ? previous[i - stride] : 0;
                    int add = filter == 0 ? 0 : filter == 1 ? left : filter == 2 ? up : filter == 3 ? (left + up) / 2 : Paeth(left, up, diagonal);
                    row[i] = unchecked((byte)(row[i] + add));
                }

            for (int i = 0; i < rowSize; i++)
                output.Add(row[i]);
            var swap = previous;
            previous = row;
            row = swap;
        }

        return output.Build();
    }

    private static int Sample(byte[] row, long sample, int bits)
    {
        long bit = sample * bits;
        int index = (int)(bit / 8), shift = 8 - bits - (int)(bit % 8);
        return bits == 16 ? row[index] * 256 + row[index + 1] : (row[index] >> shift) & ((1 << bits) - 1);
    }

    private static void SetSample(byte[] row, long sample, int bits, int value)
    {
        long bit = sample * bits;
        int index = (int)(bit / 8), shift = 8 - bits - (int)(bit % 8);
        if (bits == 16)
        {
            row[index] = (byte)(value >> 8);
            row[index + 1] = (byte)value;
        }
        else
        {
            int mask = ((1 << bits) - 1) << shift;
            row[index] = (byte)((row[index] & ~mask) | (value << shift));
        }
    }

    private static int Paeth(int a, int b, int c)
    {
        int p = a + b - c, pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
        return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
    }

    internal static int Parameter(PdfValue dictionary, string key, int fallback, PdfOperationContext context)
    {
        if (dictionary.Kind == PdfValueKind.Null || !dictionary.Entries.TryGetValue(key, out var value))
            return fallback;
        if (value.Kind != PdfValueKind.Number || value.IntegerValue < int.MinValue || value.IntegerValue > int.MaxValue)
            throw Error(context, "Filter.Parameters", "An integer decode parameter is outside range.");
        return (int)value.IntegerValue;
    }

    internal static void Trailing(Stream stream, PdfOperationContext context)
    {
        int c;
        while ((c = stream.ReadByte()) >= 0)
        {
            context.Checkpoint();
            if (!PdfSyntaxCursor.White(c))
                throw Error(context, "Filter.TrailingData", "Unexpected bytes follow a filter terminator.");
        }
    }

    internal static PdfException Error(PdfOperationContext context, string code, string message)
    {
        var diagnostic = new PdfDiagnostic(code, message, PdfCapability.ReadDocument, "managed.syntax");
        context.Report(diagnostic);
        return new PdfException(diagnostic);
    }
}
