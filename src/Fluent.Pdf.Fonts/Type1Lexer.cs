using System.Globalization;
using System.Text;

namespace Fluent.Pdf.Fonts;
internal sealed class Type1Lexer
{
    internal readonly struct Token
    {
        internal Token(string text, int start, int end, bool literal = false, bool quoted = false)
        {
            Text = text;
            Start = start;
            End = end;
            Literal = literal;
            Quoted = quoted;
        }

        internal string Text { get; }
        internal int Start { get; }
        internal int End { get; }
        internal bool Literal { get; }
        internal bool Quoted { get; }

        internal bool Number(out double value) => double.TryParse(Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !double.IsNaN(value) && !double.IsInfinity(value);
    }

    private readonly FontData _data;
    internal int Position { get; set; }

    internal Type1Lexer(byte[] data, PdfOperationContext context, int position = 0)
    {
        _data = new FontData(data, context);
        Position = position;
    }

    internal Token? Next()
    {
        var bytes = _data.Bytes;
        var context = _data.Context;
        while (Position < bytes.Length)
        {
            context.Checkpoint();
            int c = bytes[Position];
            if (White(c))
            {
                Position++;
                continue;
            }

            if (c == '%')
            {
                while (Position < bytes.Length && bytes[Position] != 10 && bytes[Position] != 13)
                    Position++;
                continue;
            }

            break;
        }

        if (Position == bytes.Length)
            return null;
        int start = Position++;
        int first = bytes[start];
        context.Consume(PdfResourceKind.Objects, 1);
        if (first == '(')
        {
            var text = new StringBuilder();
            var scopes = new Stack<IDisposable>();
            scopes.Push(context.EnterScope(PdfResourceKind.Depth));
            try
            {
                while (Position < bytes.Length)
                {
                    context.Checkpoint();
                    int c = bytes[Position++];
                    if (c == '(')
                        scopes.Push(context.EnterScope(PdfResourceKind.Depth));
                    else if (c == ')')
                    {
                        scopes.Pop().Dispose();
                        if (scopes.Count == 0)
                            return new Token(text.ToString(), start, Position, quoted: true);
                    }
                    else if (c == '\\')
                    {
                        if (Position >= bytes.Length)
                            break;
                        c = bytes[Position++];
                        if (c == 10)
                            continue;
                        if (c == 13)
                        {
                            if (Position < bytes.Length && bytes[Position] == 10)
                                Position++;
                            continue;
                        }

                        if (c >= '0' && c <= '7')
                        {
                            int value = c - '0';
                            for (int i = 0; i < 2 && Position < bytes.Length && bytes[Position] >= '0' && bytes[Position] <= '7'; i++)
                                value = value * 8 + bytes[Position++] - '0';
                            c = value & 255;
                        }
                        else
                            c = c == 'n' ? 10 : c == 'r' ? 13 : c == 't' ? 9 : c == 'b' ? 8 : c == 'f' ? 12 : c;
                    }

                    context.Consume(PdfResourceKind.ExpandedBytes, 8);
                    text.Append((char)c);
                }

                throw _data.Error("Font.Type1String", "A Type 1 literal string is truncated.");
            }
            finally
            {
                while (scopes.Count > 0)
                    scopes.Pop().Dispose();
            }
        }

        bool literal = first == '/';
        if (literal)
            start = Position;
        if (first == '[' || first == ']' || first == '{' || first == '}' || first == '<' || first == '>')
            return new Token(((char)first).ToString(), Position - 1, Position);
        while (Position < bytes.Length && !Delimiter(bytes[Position]))
        {
            if ((Position & 1023) == 0)
                context.Checkpoint();
            Position++;
        }

        int length = Position - start;
        if (length == 0)
            throw _data.Error("Font.Type1Token", "A Type 1 token is malformed.");
        string valueText = FontData.ByteText(bytes, start, length, context);
        return new Token(valueText, literal ? start - 1 : start, Position, literal);
    }

    internal Token Required() => Next() ?? throw _data.Error("Font.Type1Truncated", "The Type 1 dictionary is truncated.");
    internal int Binary(int length)
    {
        if (Position >= _data.Bytes.Length || !White(_data.Bytes[Position]))
            throw _data.Error("Font.Type1Binary", "A Type 1 binary string requires a separator.");
        Position++;
        int start = Position;
        _data.Range(start, length);
        Position = checked(Position + length);
        return start;
    }

    internal static bool White(int c) => c == 0 || c == 9 || c == 10 || c == 12 || c == 13 || c == 32;
    private static bool Delimiter(int c) => White(c) || "()<>[]{}/%".IndexOf((char)c) >= 0;
}
