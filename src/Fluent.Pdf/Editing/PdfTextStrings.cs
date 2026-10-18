using System.Text;

namespace Fluent.Pdf;
// Original PDF text-string conversion. New text uses UTF-16BE; no platform code page is involved.
internal static class PdfTextStrings
{
    private static readonly string Low = "\u02d8\u02c7\u02c6\u02d9\u02dd\u02db\u02da\u02dc";
    private static readonly string High = "\u2022\u2020\u2021\u2026\u2014\u2013\u0192\u2044\u2039\u203a\u2212\u2030\u201e\u201c\u201d\u2018\u2019\u201a\u2122\ufb01\ufb02\u0141\u0152\u0160\u0178\u017d\u0131\u0142\u0153\u0161\u017e";
    internal static char? LegacyCharacter(int b) => b < 0 || b > 255 || b == 127 || b == 159 || b == 173 ? null : b >= 24 && b <= 31 ? Low[b - 24] : b >= 128 && b <= 158 ? High[b - 128] : b == 160 ? '\u20ac' : (char)b;
    internal static PdfValue Encode(string text, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 6L + 128);
        try
        {
            var bytes = new UnicodeEncoding(true, true, true).GetBytes(text);
            var result = new byte[bytes.Length + 2];
            result[0] = 254;
            result[1] = 255;
            System.Array.Copy(bytes, 0, result, 2, bytes.Length);
            return PdfValue.String(result);
        }
        catch (EncoderFallbackException)
        {
            throw PdfWriterCore.Error(context, "Text.StringEncoding", "Text strings require well-formed Unicode.");
        }
    }

    internal static string Decode(PdfValue value, PdfOperationContext context)
    {
        if (value.Kind != PdfValueKind.String)
            throw PdfWriterCore.Error(context, "Text.StringRequired", "A PDF text string is required.");
        var data = value.StringBytes;
        context.Consume(PdfResourceKind.ExpandedBytes, data.Count * 6L + 128);
        context.Checkpoint();
        try
        {
            if (data.Count >= 2 && data[0] == 254 && data[1] == 255)
                return new UnicodeEncoding(true, false, true).GetString(data.Skip(2).ToArray());
            if (data.Count >= 3 && data[0] == 239 && data[1] == 187 && data[2] == 191)
                return new UTF8Encoding(false, true).GetString(data.Skip(3).ToArray());
            var chars = new char[data.Count];
            for (int i = 0; i < chars.Length; i++)
            {
                if ((i & 1023) == 0)
                    context.Checkpoint();
                int b = data[i];
                if (b == 127 || b == 159 || b == 173)
                    throw PdfWriterCore.Error(context, "Text.StringEncoding", "A PDF text string contains an undefined legacy character.");
                chars[i] = b >= 24 && b <= 31 ? Low[b - 24] : b >= 128 && b <= 158 ? High[b - 128] : b == 160 ? '\u20ac' : (char)b;
            }

            return new string (chars);
        }
        catch (DecoderFallbackException)
        {
            throw PdfWriterCore.Error(context, "Text.StringEncoding", "The PDF text string contains malformed Unicode.");
        }
    }
}
