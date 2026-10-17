using System.Globalization;

namespace Fluent.Pdf;
internal sealed class PdfSyntaxCursor
{
    internal PdfByteStore Source { get; }
    internal PdfOperationContext Context { get; }
    internal long Position { get; set; }
    internal long End { get; }

    internal PdfSyntaxCursor(PdfByteStore source, PdfOperationContext context, long position = 0, long? end = null)
    {
        Source = source;
        Context = context;
        Position = position;
        End = end ?? source.Length;
    }

    internal int Peek() => Position >= End ? -1 : Source.At(Position);
    internal static bool White(int c) => c == 0 || c == 9 || c == 10 || c == 12 || c == 13 || c == 32;
    internal static bool Delimiter(int c) => c < 0 || White(c) || c == '(' || c == ')' || c == '<' || c == '>' || c == '[' || c == ']' || c == '{' || c == '}' || c == '/' || c == '%';
    internal void Skip()
    {
        while (Position < End)
        {
            Context.Checkpoint();
            int c = Peek();
            if (White(c))
            {
                Position++;
                continue;
            }

            if (c != '%')
                break;
            do
            {
                Position++;
                c = Peek();
            }
            while (c >= 0 && c != 10 && c != 13);
        }
    }

    internal string Token()
    {
        Skip();
        long start = Position;
        while (!Delimiter(Peek()))
        {
            if ((Position & 1023) == 0)
                Context.Checkpoint();
            Position++;
        }

        if (Position == start)
            throw Error("Syntax.Token", "A lexical token is missing or truncated.");
        return Text(start, Position - start);
    }

    internal bool TryKeyword(string expected)
    {
        Skip();
        long start = Position;
        for (int i = 0; i < expected.Length; i++)
            if (start + i >= End || Source.At(start + i) != expected[i])
                return false;
        if (!Delimiter(start + expected.Length >= End ? -1 : Source.At(start + expected.Length)))
            return false;
        Position += expected.Length;
        return true;
    }

    internal void Require(string keyword)
    {
        if (!TryKeyword(keyword))
            throw Error("Syntax.Token", "A required PDF keyword is missing.");
    }

    internal long Integer()
    {
        string text = Token();
        if (!long.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long value))
            throw Error("Syntax.IntegerRange", "An integer is malformed or outside the supported 64-bit range.");
        return value;
    }

    internal PdfValue Value()
    {
        Context.Checkpoint();
        using var scope = Context.EnterScope(PdfResourceKind.Depth);
        Context.Consume(PdfResourceKind.Objects, 1);
        Skip();
        int c = Peek();
        if (c < 0)
            throw Error("Syntax.Truncated", "A value is truncated.");
        if (c == '/')
            return Name();
        if (c == '(')
            return Literal();
        if (c == '<')
        {
            Position++;
            if (Peek() != '<')
                return HexString();
            Position++;
            var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
            while (true)
            {
                Skip();
                if (Peek() == '>')
                {
                    Position++;
                    if (Peek() != '>')
                        throw Error("Syntax.Dictionary", "A dictionary terminator is malformed.");
                    Position++;
                    break;
                }

                if (Peek() != '/')
                    throw Error("Syntax.Dictionary", "Dictionary keys must be names.");
                Context.Consume(PdfResourceKind.Objects, 1);
                string key = Name().NameValue;
                if (entries.ContainsKey(key))
                    throw Error("Syntax.DuplicateKey", "A dictionary contains duplicate decoded keys.");
                entries.Add(key, Value());
            }

            return PdfValue.OwnedDictionary(entries);
        }

        if (c == '[')
        {
            Position++;
            var items = new List<PdfValue>();
            while (true)
            {
                Skip();
                if (Peek() == ']')
                {
                    Position++;
                    break;
                }

                items.Add(Value());
            }

            return PdfValue.OwnedArray(items);
        }

        string token = Token();
        if (token == "null")
            return PdfValue.Null;
        if (token == "true" || token == "false")
            return PdfValue.Boolean(token == "true");
        PdfValue number;
        try
        {
            number = PdfValue.Number(token);
        }
        catch (ArgumentException)
        {
            throw Error("Syntax.Value", "A value has an unsupported or malformed lexical form.");
        }

        long after = Position;
        Skip();
        int next = Peek();
        if (((next >= '0' && next <= '9') || next == '+' || next == '-') && token.IndexOf('.') < 0)
        {
            string generationText = Token();
            if (TryKeyword("R"))
            {
                if (!int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int id) || id < 0 || !int.TryParse(generationText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int generation) || generation > 65535 || generation < 0)
                    throw Error("Syntax.ReferenceRange", "An indirect reference is outside the supported object/generation range.");
                return PdfValue.Reference(new PdfObjectId(id, generation));
            }
        }

        Position = after;
        return number;
    }

    private PdfValue Name()
    {
        Position++;
        long start = Position;
        while (!Delimiter(Peek()))
        {
            if ((Position & 1023) == 0)
                Context.Checkpoint();
            Position++;
        }

        long length = Position - start;
        if (length > int.MaxValue)
            throw Error("Syntax.NameRange", "A name exceeds the managed value range.");
        Context.Consume(PdfResourceKind.ExpandedBytes, length * 2);
        var chars = new char[(int)length];
        int count = 0;
        for (long i = start; i < Position; i++)
        {
            int b = Source.At(i);
            if (b == '#')
            {
                if (i + 2 >= Position || Hex(Source.At(i + 1)) < 0 || Hex(Source.At(i + 2)) < 0)
                    throw Error("Syntax.NameEscape", "A name escape is malformed.");
                b = Hex(Source.At(i + 1)) * 16 + Hex(Source.At(i + 2));
                i += 2;
            }

            if (b == 0)
                throw Error("Syntax.NameEscape", "A name cannot contain a NUL byte.");
            chars[count++] = (char)b;
        }

        Context.Consume(PdfResourceKind.ExpandedBytes, count * 2L);
        return PdfValue.Name(new string (chars, 0, count));
    }

    private PdfValue HexString()
    {
        long start = Position;
        int digits = 0;
        while (Peek() != '>')
        {
            Context.Checkpoint();
            int c = Peek();
            if (c < 0)
                throw Error("Syntax.Truncated", "A hex string is truncated.");
            if (!White(c))
            {
                if (Hex(c) < 0)
                    throw Error("Syntax.HexString", "A hex string contains a non-hexadecimal byte.");
                digits = checked(digits + 1);
            }

            Position++;
        }

        Context.Consume(PdfResourceKind.ExpandedBytes, ((long)digits + 1) / 2);
        var bytes = new byte[(int)(((long)digits + 1) / 2)];
        int index = 0, high = -1;
        for (long i = start; i < Position; i++)
        {
            int c = Source.At(i);
            if (White(c))
                continue;
            int n = Hex(c);
            if (high < 0)
                high = n;
            else
            {
                bytes[index++] = (byte)(high * 16 + n);
                high = -1;
            }
        }

        if (high >= 0)
            bytes[index] = (byte)(high * 16);
        Position++;
        return PdfValue.OwnedString(bytes);
    }

    private PdfValue Literal()
    {
        long start = ++Position;
        int length = ReadLiteral(null);
        long end = Position;
        Context.Consume(PdfResourceKind.ExpandedBytes, length);
        var bytes = new byte[length];
        Position = start;
        ReadLiteral(bytes);
        Position = end;
        return PdfValue.OwnedString(bytes);
    }

    private int ReadLiteral(byte[]? output)
    {
        var nesting = new Stack<IDisposable>();
        int count = 0;
        try
        {
            nesting.Push(Context.EnterScope(PdfResourceKind.Depth));
            while (true)
            {
                Context.Checkpoint();
                int c = Peek();
                if (c < 0)
                    throw Error("Syntax.Truncated", "A literal string is truncated.");
                Position++;
                if (c == '(')
                {
                    nesting.Push(Context.EnterScope(PdfResourceKind.Depth));
                }
                else if (c == ')')
                {
                    nesting.Pop().Dispose();
                    if (nesting.Count == 0)
                        return count;
                }
                else if (c == '\\')
                {
                    c = Peek();
                    if (c < 0)
                        throw Error("Syntax.Truncated", "A literal string escape is truncated.");
                    Position++;
                    if (c == 10)
                        continue;
                    if (c == 13)
                    {
                        if (Peek() == 10)
                            Position++;
                        continue;
                    }

                    if (c >= '0' && c <= '7')
                    {
                        int value = c - '0';
                        for (int i = 0; i < 2 && Peek() >= '0' && Peek() <= '7'; i++)
                        {
                            value = value * 8 + Peek() - '0';
                            Position++;
                        }

                        c = value & 255;
                    }
                    else
                        c = c == 'n' ? 10 : c == 'r' ? 13 : c == 't' ? 9 : c == 'b' ? 8 : c == 'f' ? 12 : c;
                }
                else if (c == 13)
                {
                    if (Peek() == 10)
                        Position++;
                    c = 10;
                }

                if (output is not null)
                    output[count] = (byte)c;
                count = checked(count + 1);
            }
        }
        finally
        {
            while (nesting.Count > 0)
                nesting.Pop().Dispose();
        }
    }

    internal string Text(long start, long length)
    {
        if (length > int.MaxValue)
            throw Error("Syntax.TokenRange", "A lexical token exceeds the managed value range.");
        Context.Consume(PdfResourceKind.ExpandedBytes, length * 4); // temporary UTF-16 buffer and resulting string
        var text = new char[(int)length];
        for (int i = 0; i < text.Length; i++)
            text[i] = (char)Source.At(start + i);
        return new string (text);
    }

    internal PdfException Error(string code, string message)
    {
        var diagnostic = new PdfDiagnostic(code, message, PdfCapability.ReadDocument, "managed.syntax");
        Context.Report(diagnostic);
        return new PdfException(diagnostic);
    }

    internal static int Hex(int c) => c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
}
