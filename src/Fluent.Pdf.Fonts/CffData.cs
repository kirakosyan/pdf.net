using System.Globalization;
using System.Text;

namespace Fluent.Pdf.Fonts;
internal sealed class CffData
{
    internal readonly FontData Data;
    internal CffData(byte[] bytes, PdfOperationContext context) => Data = new FontData(bytes, context);
    internal sealed class Index
    {
        internal readonly (int Offset, int Length)[] Items;
        internal readonly int End;
        internal Index((int, int)[] items, int end)
        {
            Items = items;
            End = end;
        }
    }

    internal Index ReadIndex(int offset)
    {
        int count = Data.U16(offset);
        if (count == 0)
            return new Index(System.Array.Empty<(int, int)>(), offset + 2);
        int size = Data.U8(offset + 2);
        if (size < 1 || size > 4)
            throw Data.Error("Font.CffIndex", "A CFF INDEX offSize is invalid.");
        Data.Range(offset + 3, (count + 1L) * size);
        Data.Context.Consume(PdfResourceKind.Objects, count);
        Data.Context.Consume(PdfResourceKind.ExpandedBytes, count * 8L + 64);
        var items = new (int, int)[count];
        uint Read(int position)
        {
            uint value = 0;
            for (int i = 0; i < size; i++)
                value = (value << 8) | Data.U8(position + i);
            return value;
        }

        int start = checked(offset + 3 + (count + 1) * size);
        uint previous = Read(offset + 3);
        if (previous != 1)
            throw Data.Error("Font.CffIndex", "A CFF INDEX must begin at one.");
        for (int i = 0; i < count; i++)
        {
            uint next = Read(offset + 3 + (i + 1) * size);
            if (next < previous || next > int.MaxValue)
                throw Data.Error("Font.CffIndex", "CFF INDEX offsets are invalid.");
            Data.Range(start + previous - 1, next - previous);
            items[i] = (checked(start + (int)previous - 1), (int)(next - previous));
            previous = next;
        }

        return new Index(items, checked(start + (int)previous - 1));
    }

    internal Dictionary<int, double[]> Dictionary(int offset, int length)
    {
        Data.Range(offset, length);
        int end = checked(offset + length);
        var result = new Dictionary<int, double[]>();
        var operands = new List<double>();
        while (offset < end)
        {
            Data.Context.Checkpoint();
            int token = Data.U8(offset++);
            if (token >= 32 || token == 28 || token == 29 || token == 30)
            {
                if (operands.Count >= 48)
                    throw Data.Error("Font.CffDictionary", "A CFF DICT operand stack exceeds 48 entries.");
                Data.Context.Consume(PdfResourceKind.ExpandedBytes, 16);
                double value;
                if (token >= 32 && token <= 246)
                    value = token - 139;
                else if (token >= 247 && token <= 250)
                {
                    Require(1);
                    value = (token - 247) * 256 + Data.U8(offset++) + 108;
                }
                else if (token >= 251 && token <= 254)
                {
                    Require(1);
                    value = -(token - 251) * 256 - Data.U8(offset++) - 108;
                }
                else if (token == 28)
                {
                    Require(2);
                    value = Data.I16(offset);
                    offset += 2;
                }
                else if (token == 29)
                {
                    Require(4);
                    value = Data.I32(offset);
                    offset += 4;
                }
                else if (token == 30)
                {
                    var text = new StringBuilder();
                    bool done = false;
                    while (!done)
                    {
                        Require(1);
                        int pair = Data.U8(offset++);
                        for (int i = 0; i < 2; i++)
                        {
                            int n = i == 0 ? pair >> 4 : pair & 15;
                            if (n == 15)
                            {
                                done = true;
                                break;
                            }

                            if (text.Length >= 96 || n == 13)
                                throw Data.Error("Font.CffDictionary", "A CFF real number is malformed or excessive.");
                            text.Append(n < 10 ? (char)('0' + n) : n == 10 ? '.' : n == 11 ? 'E' : n == 12 ? 'e' : '-');
                            if (n == 12)
                                text.Append('-');
                        }
                    }

                    if (!double.TryParse(text.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture, out value) || double.IsNaN(value) || double.IsInfinity(value))
                        throw Data.Error("Font.CffDictionary", "A CFF real number is not finite.");
                }
                else
                    throw Data.Error("Font.CffDictionary", "A reserved CFF number token was encountered.");
                operands.Add(value);
                continue;
            }

            int operation = token;
            if (token == 12)
            {
                Require(1);
                operation = 1200 + Data.U8(offset++);
            }
            else if (token > 21)
                throw Data.Error("Font.CffDictionary", "A reserved CFF operator was encountered.");
            if (result.ContainsKey(operation))
                throw Data.Error("Font.CffDictionary", "A CFF dictionary repeats an operator.");
            Data.Context.Consume(PdfResourceKind.Objects, 1);
            Data.Context.Consume(PdfResourceKind.ExpandedBytes, operands.Count * 8L + 128);
            result.Add(operation, operands.ToArray());
            operands.Clear();
        }

        if (operands.Count != 0)
            throw Data.Error("Font.CffDictionary", "A CFF dictionary ends with unused operands.");
        return result;
        void Require(int count)
        {
            if (offset > end - count)
                throw Data.Error("Font.CffDictionary", "A CFF dictionary number is truncated.");
        }
    }

    internal int Integer(Dictionary<int, double[]> dictionary, int operation, int fallback = -1)
    {
        if (!dictionary.TryGetValue(operation, out var values))
            return fallback;
        if (values.Length != 1 || values[0] < 0 || values[0] > int.MaxValue || values[0] != Math.Floor(values[0]))
            throw Data.Error("Font.CffDictionary", "A CFF offset/count requires one nonnegative bounded integer.");
        return (int)values[0];
    }

    internal static byte[] IndexBytes(IReadOnlyList<byte[]> items, PdfOperationContext context)
    {
        if (items.Count > 65535)
            throw PdfWriterCore.Error(context, "Font.CffIndex", "The CFF INDEX count exceeds 16 bits.");
        long size = items.Count == 0 ? 2 : 3 + (items.Count + 1L) * 4;
        foreach (var item in items)
            size = checked(size + item.LongLength);
        if (size > int.MaxValue)
            throw PdfWriterCore.Error(context, "Font.ProgramRange", "A generated CFF INDEX exceeds the managed range.");
        context.Consume(PdfResourceKind.ExpandedBytes, size);
        var result = new byte[(int)size];
        FontData.Put16(result, 0, items.Count);
        if (items.Count == 0)
            return result;
        result[2] = 4;
        int position = 3 + (items.Count + 1) * 4;
        uint relative = 1;
        for (int i = 0; i < items.Count; i++)
        {
            context.Checkpoint();
            FontData.Put32(result, 3 + i * 4, relative);
            Buffer.BlockCopy(items[i], 0, result, position, items[i].Length);
            position += items[i].Length;
            relative = checked(relative + (uint)items[i].Length);
        }

        FontData.Put32(result, 3 + items.Count * 4, relative);
        return result;
    }

    internal static byte[] DictBytes(IEnumerable<(int Operation, double[] Values)> entries, PdfOperationContext context)
    {
        var builder = new PdfByteBuilder(context);
        foreach (var entry in entries)
        {
            foreach (double value in entry.Values)
            {
                context.Checkpoint();
                if (value >= int.MinValue && value <= int.MaxValue && value == Math.Floor(value))
                {
                    builder.Add(29);
                    uint bits = (uint)(int)value;
                    for (int i = 3; i >= 0; i--)
                        builder.Add((byte)(bits >> (8 * i)));
                }
                else
                {
                    if (double.IsInfinity(value) || double.IsNaN(value))
                        throw PdfWriterCore.Error(context, "Font.CffDictionary", "Generated CFF values must be finite.");
                    builder.Add(30);
                    string text = value.ToString("G17", CultureInfo.InvariantCulture);
                    var nibbles = new List<int>();
                    context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 16L + 16);
                    foreach (char c in text)
                    {
                        if (c >= '0' && c <= '9')
                            nibbles.Add(c - '0');
                        else if (c == '.')
                            nibbles.Add(10);
                        else if (c == '-')
                            nibbles.Add(14);
                        else if (c == 'E' || c == 'e')
                            nibbles.Add(11);
                        else if (c != '+')
                            throw new InvalidOperationException();
                    }

                    nibbles.Add(15);
                    if (nibbles.Count % 2 != 0)
                        nibbles.Add(15);
                    for (int i = 0; i < nibbles.Count; i += 2)
                        builder.Add((byte)(nibbles[i] * 16 + nibbles[i + 1]));
                }
            }

            if (entry.Operation >= 1200)
            {
                builder.Add(12);
                builder.Add((byte)(entry.Operation - 1200));
            }
            else
                builder.Add((byte)entry.Operation);
        }

        var store = builder.Build();
        context.Consume(PdfResourceKind.ExpandedBytes, store.Length);
        var bytes = new byte[(int)store.Length];
        using var input = store.Open(0, store.Length);
        int read = 0;
        while (read < bytes.Length)
        {
            context.Checkpoint();
            read += input.Read(bytes, read, bytes.Length - read);
        }

        return bytes;
    }
}
