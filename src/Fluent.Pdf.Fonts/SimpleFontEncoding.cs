using System.Globalization;
using System.IO;
using System.Text;

namespace Fluent.Pdf.Fonts;
internal static class SimpleFontEncoding
{
    internal static string Decode(PdfValue encoding, PdfValue encodedText, string baseFont, PdfOperationContext context)
    {
        if (encoding == null)
            throw new ArgumentNullException(nameof(encoding));
        if (encodedText == null)
            throw new ArgumentNullException(nameof(encodedText));
        if (baseFont == null)
            throw new ArgumentNullException(nameof(baseFont));
        if (encodedText.Kind != PdfValueKind.String)
            throw new ArgumentException("Encoded font text must be a byte string.", nameof(encodedText));
        var glyphNames = Names(encoding, baseFont, context, out var normalized);
        var names = FontResources.GlyphList(context);
        if (normalized == "ZapfDingbats")
        {
            string metrics = FontResources.Text("ZapfDingbats.metrics", context);
            context.Consume(PdfResourceKind.ExpandedBytes, metrics.Length * 8L);
            using var reader = new StringReader(metrics);
            reader.ReadLine();
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                context.Checkpoint();
                var fields = line.Split('\t');
                string[] scalars = fields[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                var unicode = new StringBuilder();
                foreach (string scalar in scalars)
                    unicode.Append(char.ConvertFromUtf32(int.Parse(scalar, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
                names[fields[0]] = unicode.ToString();
            }
        }

        var result = new StringBuilder();
        foreach (byte code in encodedText.StringBytes)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            string unicode = FontResources.Unicode(glyphNames[code], names);
            if (unicode.Length == 0)
            {
                context.Report(new PdfDiagnostic("Font.UnmappedCode", "A simple-font glyph name has no published Unicode mapping.", severity: PdfDiagnosticSeverity.Warning, lossCategory: PdfLossCategory.SubstitutedContent));
                unicode = "\ufffd";
            }

            context.Consume(PdfResourceKind.ExpandedBytes, unicode.Length * 4L + 16);
            result.Append(unicode);
        }

        return result.ToString();
    }

    internal static string[] Names(PdfValue encoding, string baseFont, PdfOperationContext context, out string normalized)
    {
        if (encoding == null)
            throw new ArgumentNullException(nameof(encoding));
        if (baseFont == null)
            throw new ArgumentNullException(nameof(baseFont));
        context.Checkpoint();
        normalized = baseFont.Length > 7 && baseFont[6] == '+' && baseFont.Take(6).All(c => c >= 'A' && c <= 'Z') ? baseFont.Substring(7) : baseFont;
        string? name = encoding.Kind == PdfValueKind.Name ? encoding.NameValue : null;
        PdfValue differences = PdfValue.Null;
        if (encoding.Kind == PdfValueKind.Dictionary)
        {
            if (encoding.Entries.TryGetValue("BaseEncoding", out var declared))
            {
                if (declared.Kind != PdfValueKind.Name)
                    throw Error(context, "Font.Encoding", "BaseEncoding must be a direct standard encoding name.");
                name = declared.NameValue;
            }

            if (encoding.Entries.TryGetValue("Differences", out var diff))
                differences = diff;
        }
        else if (encoding.Kind != PdfValueKind.Name && encoding.Kind != PdfValueKind.Null)
            throw Error(context, "Font.Encoding", "A simple-font encoding must be a name, dictionary or null.");
        if (name == null && StandardFont.Names.Contains(normalized))
            name = normalized == "Symbol" ? "SymbolEncoding" : normalized == "ZapfDingbats" ? "ZapfDingbatsEncoding" : "StandardEncoding";
        string[] glyphNames;
        if (name == null)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, 256 * 8);
            glyphNames = Enumerable.Repeat(".notdef", 256).ToArray();
        }
        else
        {
            if (name != "StandardEncoding" && name != "WinAnsiEncoding" && name != "MacRomanEncoding" && name != "MacExpertEncoding" && name != "SymbolEncoding" && name != "ZapfDingbatsEncoding")
                throw Error(context, "Font.EncodingUnsupported", "The named simple-font encoding requires an explicit provider.");
            string data = FontResources.Text(name + ".txt", context);
            context.Consume(PdfResourceKind.ExpandedBytes, data.Length * 4L + 256 * 16);
            glyphNames = data.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (glyphNames.Length != 256)
                throw new InvalidDataException("Pinned font encoding data has an invalid length.");
        }

        if (differences.Kind != PdfValueKind.Null)
        {
            if (differences.Kind != PdfValueKind.Array)
                throw Error(context, "Font.Encoding", "Differences must be a direct array of byte positions and glyph names.");
            int code = -1;
            foreach (var value in differences.Items)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                if (value.Kind == PdfValueKind.Number)
                {
                    if (!value.TryGetInteger(out long position) || position < 0 || position > 255)
                        throw Error(context, "Font.Encoding", "A Differences position is outside the byte range.");
                    code = (int)position;
                }
                else if (value.Kind == PdfValueKind.Name && code >= 0 && code < 256)
                    glyphNames[code++] = value.NameValue;
                else
                    throw Error(context, "Font.Encoding", "A Differences glyph is missing an in-range position or name.");
            }
        }

        return glyphNames;
    }

    private static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, code, message);
}
