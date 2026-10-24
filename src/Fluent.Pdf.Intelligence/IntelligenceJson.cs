using System.Globalization;
using System.IO;
using System.Text;

namespace Fluent.Pdf.Intelligence;
internal static class IntelligenceJson
{
    internal static PdfByteStore Utf8(string text, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        using var bytes = new PdfCodecBufferStream(c, 4194304);
        using (var writer = new StreamWriter(bytes, new UTF8Encoding(false, true), 1024, true))
        {
            writer.Write(text);
            writer.Flush();
        }

        c.Checkpoint();
        return bytes.Finish();
    }

    internal static object? Parse(string text, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, text.Length * 2L + 256);
        return new PdfInputJson(text, c).Read();
    }

    internal static PdfByteStore Bytes(object? value, PdfOperationContext c, int maximum = 4194304)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        using var bytes = new PdfCodecBufferStream(c, maximum);
        using (var writer = new StreamWriter(bytes, new UTF8Encoding(false, true), 1024, true))
        {
            Write(value, writer, c);
            writer.Flush();
        }

        return bytes.Finish();
    }

    internal static string Text(object? value, PdfOperationContext c)
    {
        var bytes = Bytes(value, c);
        c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 4L + 8192);
        using var raw = bytes.Open(0, bytes.Length);
        using var reader = new StreamReader(raw, new UTF8Encoding(false, true), false, 1024, true);
        return reader.ReadToEnd();
    }

    private static void Write(object? value, TextWriter output, PdfOperationContext c)
    {
        c.Checkpoint();
        using var scope = c.EnterScope(PdfResourceKind.Depth);
        if (value == null)
        {
            output.Write("null");
            return;
        }

        if (value is bool boolean)
        {
            output.Write(boolean ? "true" : "false");
            return;
        }

        if (value is decimal number)
        {
            output.Write(number.ToString(CultureInfo.InvariantCulture));
            return;
        }

        if (value is string text)
        {
            output.Write('"');
            foreach (char ch in text)
            {
                c.Checkpoint();
                if (ch == '"' || ch == '\\')
                {
                    output.Write('\\');
                    output.Write(ch);
                }
                else if (ch < 32)
                {
                    output.Write("\\u");
                    output.Write(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                    output.Write(ch);
            }

            output.Write('"');
            return;
        }

        if (value is Dictionary<string, object?> members)
        {
            output.Write('{');
            bool first = true;
            c.Consume(PdfResourceKind.ExpandedBytes, members.Count * 32L);
            foreach (var pair in members.OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                if (!first)
                    output.Write(',');
                first = false;
                Write(pair.Key, output, c);
                output.Write(':');
                Write(pair.Value, output, c);
            }

            output.Write('}');
            return;
        }

        if (value is List<object?> items)
        {
            output.Write('[');
            bool first = true;
            foreach (var item in items)
            {
                if (!first)
                    output.Write(',');
                first = false;
                Write(item, output, c);
            }

            output.Write(']');
            return;
        }

        throw PdfDocumentIntelligence.Error(c, "JsonType", "The closed intelligence JSON value profile is required.");
    }

    internal static Dictionary<string, object?> Object(object? value, PdfOperationContext c) => value as Dictionary<string, object?> ?? throw PdfDocumentIntelligence.Error(c, "ResponseShape", "A required response object is absent or malformed.");
    internal static List<object?> Array(object? value, PdfOperationContext c) => value as List<object?> ?? throw PdfDocumentIntelligence.Error(c, "ResponseShape", "A required response array is absent or malformed.");
    internal static object? Get(Dictionary<string, object?> value, string key, PdfOperationContext c) => value.TryGetValue(key, out var item) ? item : throw PdfDocumentIntelligence.Error(c, "ResponseShape", "A required response member is absent.");
    internal static string String(object? value, PdfOperationContext c) => value as string ?? throw PdfDocumentIntelligence.Error(c, "ResponseShape", "A required literal response string is absent.");
    internal static int Integer(object? value, PdfOperationContext c)
    {
        if (value is not decimal n || n < 0 || n > 2000000 || decimal.Truncate(n) != n)
            throw PdfDocumentIntelligence.Error(c, "Usage", "A finite nonnegative integer token count is required.");
        return (int)n;
    }

    internal static void Keys(Dictionary<string, object?> value, PdfOperationContext c, params string[] allowed)
    {
        if (value.Keys.Any(key => !allowed.Contains(key)))
            throw PdfDocumentIntelligence.Error(c, "ResponseShape", "The response contains an undeclared member or action.");
    }

    internal static object? Pointer(object? value, string pointer, PdfOperationContext c)
    {
        if (pointer.Length == 0)
            return value;
        foreach (string raw in pointer.Substring(1).Split('/'))
        {
            c.Checkpoint();
            var key = new StringBuilder();
            c.Consume(PdfResourceKind.ExpandedBytes, raw.Length * 4L + 128);
            for (int n = 0; n < raw.Length; n++)
                if (raw[n] != '~')
                    key.Append(raw[n]);
                else
                {
                    if (++n == raw.Length || raw[n] != '0' && raw[n] != '1')
                        throw PdfDocumentIntelligence.Error(c, "CitationPath", "A citation JSON pointer escape is malformed.");
                    key.Append(raw[n] == '0' ? '~' : '/');
                }

            string name = key.ToString();
            if (value is Dictionary<string, object?> members && members.TryGetValue(name, out var member))
                value = member;
            else if (value is List<object?> items && int.TryParse(name, NumberStyles.None, CultureInfo.InvariantCulture, out int index) && index >= 0 && index < items.Count && index.ToString(CultureInfo.InvariantCulture) == name)
                value = items[index];
            else
                throw PdfDocumentIntelligence.Error(c, "CitationPath", "A citation points outside the accepted structured result.");
        }

        return value;
    }

    internal static long Characters(object? value, PdfOperationContext c)
    {
        c.Checkpoint();
        using var scope = c.EnterScope(PdfResourceKind.Depth);
        if (value is string text)
            return text.Length;
        if (value is List<object?> items)
            return items.Sum(item => Characters(item, c));
        if (value is Dictionary<string, object?> members)
            return members.Sum(pair => Characters(pair.Value, c));
        return 0;
    }
}
