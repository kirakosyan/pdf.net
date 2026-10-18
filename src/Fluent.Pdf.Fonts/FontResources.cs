using System.Globalization;
using System.IO;
using System.Text;

namespace Fluent.Pdf.Fonts;
internal static class FontResources
{
    internal static byte[] Read(string name, PdfOperationContext context)
    {
        using var stream = typeof(FontResources).Assembly.GetManifestResourceStream("Fluent.Pdf.Fonts.Data." + name) ?? throw PdfWriterCore.Error(context, "Font.DataMissing", "A pinned font data resource is absent.");
        if (stream.Length > int.MaxValue)
            throw PdfWriterCore.Error(context, "Font.DataRange", "A font data resource exceeds the managed range.");
        context.Consume(PdfResourceKind.ExpandedBytes, stream.Length);
        var bytes = new byte[(int)stream.Length];
        int offset = 0;
        while (offset < bytes.Length)
        {
            context.Checkpoint();
            int count = stream.Read(bytes, offset, Math.Min(4096, bytes.Length - offset));
            if (count == 0)
                throw new EndOfStreamException();
            offset += count;
        }

        return bytes;
    }

    internal static string Text(string name, PdfOperationContext context)
    {
        byte[] bytes = Read(name, context);
        context.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 2L);
        return Encoding.UTF8.GetString(bytes, 0, bytes.Length);
    }

    internal static Dictionary<string, string> GlyphList(PdfOperationContext context)
    {
        string text = Text("AdobeGlyphList.txt", context);
        context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 12L);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        using var reader = new StringReader(text);
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            int split = line.IndexOf(';');
            if (split <= 0)
                throw new InvalidDataException("Pinned glyph data is malformed.");
            var unicode = new StringBuilder();
            foreach (string scalar in line.Substring(split + 1).Split(' '))
                unicode.Append(char.ConvertFromUtf32(int.Parse(scalar, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
            result.Add(line.Substring(0, split), unicode.ToString());
        }

        return result;
    }

    internal static string Unicode(string name, IReadOnlyDictionary<string, string> glyphs)
    {
        int dot = name.IndexOf('.');
        if (dot >= 0)
            name = name.Substring(0, dot);
        if (glyphs.TryGetValue(name, out string? unicode))
            return unicode;
        if (name.StartsWith("uni", StringComparison.Ordinal) && name.Length > 3 && (name.Length - 3) % 4 == 0)
        {
            var value = new StringBuilder();
            for (int offset = 3; offset < name.Length; offset += 4)
            {
                if (!int.TryParse(name.Substring(offset, 4), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int scalar) || scalar >= 0xd800 && scalar <= 0xdfff)
                    return "";
                value.Append((char)scalar);
            }

            return value.ToString();
        }

        if (name.StartsWith("u", StringComparison.Ordinal) && name.Length >= 5 && name.Length <= 7 && int.TryParse(name.Substring(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int number) && number <= 0x10ffff && (number < 0xd800 || number > 0xdfff))
            return char.ConvertFromUtf32(number);
        return "";
    }
}
