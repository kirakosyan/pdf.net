using System.IO;
using System.Text;

namespace Fluent.Pdf;
// Original bounded parser for the explicit version-one field-data schema; no dynamic object binding.
internal static class PdfFormJson
{
    internal static List<PdfFormDataField> Read(PdfByteStore bytes, PdfOperationContext context)
    {
        if (bytes.Length > int.MaxValue)
            throw PdfFormData.Error(context, "Length", "The JSON input exceeds its supported extent.");
        var raw = new byte[(int)bytes.Length];
        using (var stream = bytes.Open(0, bytes.Length))
        {
            int at = 0;
            while (at < raw.Length)
            {
                context.Checkpoint();
                int n = stream.Read(raw, at, raw.Length - at);
                if (n == 0)
                    break;
                at += n;
            }
        }

        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(raw);
        }
        catch (DecoderFallbackException)
        {
            throw PdfFormData.Error(context, "Utf8", "JSON field data requires well-formed UTF-8.");
        }

        return new Reader(text, context).Read();
    }

    internal static void Write(IReadOnlyList<PdfFormDataField> fields, Stream output, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 8192);
        using var writer = new StreamWriter(output, new UTF8Encoding(false, true), 1024, true);
        writer.Write("{\"version\":1,\"fields\":[");
        bool first = true;
        foreach (var field in fields)
        {
            context.Checkpoint();
            if (!first)
                writer.Write(',');
            first = false;
            writer.Write("{\"name\":");
            String(field.Name);
            writer.Write(",\"type\":");
            String(PdfFormData.Type(field.Value));
            writer.Write(",\"value\":");
            var value = field.Value;
            if (value.Kind == PdfValueKind.Null)
                writer.Write("null");
            else if (value.Kind == PdfValueKind.Array)
            {
                writer.Write('[');
                bool initial = true;
                foreach (var item in value.Items)
                {
                    if (!initial)
                        writer.Write(',');
                    initial = false;
                    String(PdfTextStrings.Decode(item, context));
                }

                writer.Write(']');
            }
            else
                String(value.Kind == PdfValueKind.Name ? value.NameValue : PdfTextStrings.Decode(value, context));
            writer.Write('}');
        }

        writer.Write("]}");
        writer.Flush();
        void String(string value)
        {
            writer.Write('"');
            foreach (char c in value)
            {
                context.Checkpoint();
                if (c == '"')
                    writer.Write("\\\"");
                else if (c == '\\')
                    writer.Write("\\\\");
                else if (c < 32)
                {
                    writer.Write("\\u");
                    writer.Write(((int)c).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
                }
                else
                    writer.Write(c);
            }

            writer.Write('"');
        }
    }

    private sealed class Reader
    {
        private readonly string _text;
        private readonly PdfOperationContext _context;
        private int _at;
        internal Reader(string text, PdfOperationContext context)
        {
            _text = text;
            _context = context;
            if (text.Length > 0 && text[0] == '\ufeff')
                _at++;
        }

        private PdfException Error() => PdfFormData.Error(_context, "Json", "The JSON field-data syntax or version-one schema is invalid.");
        private void Space()
        {
            while (_at < _text.Length && (_text[_at] == ' ' || _text[_at] == '\t' || _text[_at] == '\r' || _text[_at] == '\n'))
            {
                _context.Checkpoint();
                _at++;
            }
        }

        private bool Take(char c)
        {
            Space();
            if (_at < _text.Length && _text[_at] == c)
            {
                _at++;
                return true;
            }

            return false;
        }

        private void Need(char c)
        {
            if (!Take(c))
                throw Error();
        }

        private void Literal(string value)
        {
            Space();
            foreach (char c in value)
                if (_at >= _text.Length || _text[_at++] != c)
                    throw Error();
        }

        internal List<PdfFormDataField> Read()
        {
            using var depth = _context.EnterScope(PdfResourceKind.Depth);
            Need('{');
            bool version = false, fields = false;
            var result = new List<PdfFormDataField>();
            if (Take('}'))
                throw Error();
            do
            {
                string name = String();
                Need(':');
                if (name == "version" && !version)
                {
                    Literal("1");
                    version = true;
                }
                else if (name == "fields" && !fields)
                {
                    fields = true;
                    Need('[');
                    if (!Take(']'))
                    {
                        do
                        {
                            _context.Consume(PdfResourceKind.Objects, 1);
                            _context.Consume(PdfResourceKind.ExpandedBytes, 192);
                            if (result.Count >= 100000)
                                throw Error();
                            result.Add(Field());
                        }
                        while (Take(','));
                        Need(']');
                    }
                }
                else
                    throw Error();
            }
            while (Take(','));
            Need('}');
            Space();
            if (_at != _text.Length || !version || !fields)
                throw Error();
            return result;
        }

        private PdfFormDataField Field()
        {
            using var depth = _context.EnterScope(PdfResourceKind.Depth);
            Need('{');
            string? name = null, type = null;
            bool haveValue = false, isArray = false, isNull = false;
            var values = new List<string>();
            do
            {
                string key = String();
                Need(':');
                if (key == "name" && name == null)
                    name = String();
                else if (key == "type" && type == null)
                    type = String();
                else if (key == "value" && !haveValue)
                {
                    haveValue = true;
                    Space();
                    if (Take('['))
                    {
                        isArray = true;
                        if (!Take(']'))
                        {
                            do
                            {
                                _context.Consume(PdfResourceKind.Objects, 1);
                                _context.Consume(PdfResourceKind.ExpandedBytes, 32);
                                values.Add(String());
                            }
                            while (Take(','));
                            Need(']');
                        }
                    }
                    else if (_at < _text.Length && _text[_at] == 'n')
                    {
                        Literal("null");
                        isNull = true;
                    }
                    else
                        values.Add(String());
                }
                else
                    throw Error();
            }
            while (Take(','));
            Need('}');
            if (name == null || type == null || !haveValue || (type == "null") != isNull || (type == "texts") != isArray)
                throw Error();
            return new(name, PdfFormData.Typed(type, values, _context));
        }

        private string String()
        {
            Need('"');
            var value = new StringBuilder();
            while (_at < _text.Length)
            {
                _context.Checkpoint();
                char c = _text[_at++];
                if (c == '"')
                {
                    string result = value.ToString();
                    for (int i = 0; i < result.Length; i++)
                    {
                        _context.Checkpoint();
                        if (char.IsHighSurrogate(result[i]))
                        {
                            if (++i >= result.Length || !char.IsLowSurrogate(result[i]))
                                throw Error();
                        }
                        else if (char.IsLowSurrogate(result[i]))
                            throw Error();
                    }

                    return result;
                }

                if (c < 32)
                    throw Error();
                if (c == '\\')
                {
                    if (_at == _text.Length)
                        throw Error();
                    c = _text[_at++];
                    switch (c)
                    {
                        case '"':
                        case '\\':
                        case '/':
                            break;
                        case 'b':
                            c = '\b';
                            break;
                        case 'f':
                            c = '\f';
                            break;
                        case 'n':
                            c = '\n';
                            break;
                        case 'r':
                            c = '\r';
                            break;
                        case 't':
                            c = '\t';
                            break;
                        case 'u':
                            int code = 0;
                            for (int i = 0; i < 4; i++)
                            {
                                if (_at == _text.Length)
                                    throw Error();
                                char digit = _text[_at++];
                                int n = digit >= '0' && digit <= '9' ? digit - '0' : digit >= 'a' && digit <= 'f' ? digit - 'a' + 10 : digit >= 'A' && digit <= 'F' ? digit - 'A' + 10 : -1;
                                if (n < 0)
                                    throw Error();
                                code = code * 16 + n;
                            }

                            c = (char)code;
                            break;
                        default:
                            throw Error();
                    }
                }

                value.Append(c);
            }

            throw Error();
        }
    }
}
