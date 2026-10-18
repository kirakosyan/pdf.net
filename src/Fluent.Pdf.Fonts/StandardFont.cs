using System.Globalization;
using System.IO;
using System.Text;

namespace Fluent.Pdf.Fonts;
internal sealed class StandardFont : IPdfFontProgram
{
    private readonly Dictionary<int, int> _unicode = new();
    private readonly List<(string Name, int Width)> _glyphs = new()
    {
        (".notdef", 0)
    };
    public PdfFontInfo Info { get; }

    internal StandardFont(string name, PdfOperationContext context)
    {
        if (!Names.Contains(name))
            throw PdfWriterCore.Error(context, "Font.StandardName", "Select an exact standard PDF PostScript font name.");
        var bytes = FontResources.Read(name + ".metrics", context);
        context.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 24L);
        string text = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
        using var reader = new StringReader(text);
        string[] header = reader.ReadLine()!.Split('\t');
        string[] box = header[4].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        int Integer(string value) => int.Parse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture);
        var metrics = new PdfFontMetrics(1000, Integer(header[1]), Integer(header[2]), Integer(header[3]), Integer(box[0]), Integer(box[1]), Integer(box[2]), Integer(box[3]), header[5] == "true", double.Parse(header[6], CultureInfo.InvariantCulture));
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            var fields = line.Split('\t');
            int glyph = _glyphs.Count;
            _glyphs.Add((fields[0], Integer(fields[1])));
            string[] scalars = fields[2].Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (scalars.Length == 1)
            {
                int scalar = int.Parse(scalars[0], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                if (!_unicode.ContainsKey(scalar))
                    _unicode.Add(scalar, glyph);
            }
        }

        if (name == "Symbol")
            foreach (var alias in new[]
            {
                (From: 0x03a9, To: 0x2126),
                (From: 0x03bc, To: 0xb5),
                (From: 0x0394, To: 0x2206)
            }

            )
                if (_unicode.TryGetValue(alias.To, out int glyph))
                    _unicode[alias.From] = glyph;
        Info = new PdfFontInfo(name, PdfFontFormat.Standard, _glyphs.Count, metrics, FontData.Hash(bytes, context), symbolic: name == "Symbol" || name == "ZapfDingbats");
    }

    internal static readonly string[] Names =
    {
        "Courier",
        "Courier-Bold",
        "Courier-Oblique",
        "Courier-BoldOblique",
        "Helvetica",
        "Helvetica-Bold",
        "Helvetica-Oblique",
        "Helvetica-BoldOblique",
        "Times-Roman",
        "Times-Bold",
        "Times-Italic",
        "Times-BoldItalic",
        "Symbol",
        "ZapfDingbats"
    };
    public int MapGlyph(int unicodeScalar, PdfOperationContext context)
    {
        FontData.Scalar(unicodeScalar);
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        return _unicode.TryGetValue(unicodeScalar, out int glyph) ? glyph : 0;
    }

    public double GetAdvance(int glyph, PdfOperationContext context)
    {
        Check(glyph, context);
        return _glyphs[glyph].Width;
    }

    public double GetVerticalAdvance(int glyph, PdfOperationContext context)
    {
        Check(glyph, context);
        return 1000;
    }

    public string GetGlyphName(int glyph, PdfOperationContext context)
    {
        Check(glyph, context);
        return _glyphs[glyph].Name;
    }

    private void Check(int glyph, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        if (glyph < 0 || glyph >= _glyphs.Count)
            throw new ArgumentOutOfRangeException(nameof(glyph));
    }

    public PdfFontProgramWriteResult Write(Stream output, IReadOnlyList<int> glyphs, bool subset, PdfOperationContext context)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (glyphs == null)
            throw new ArgumentNullException(nameof(glyphs));
        foreach (int glyph in glyphs)
            Check(glyph, context);
        return new PdfFontProgramWriteResult(Info.PostScriptName);
    }
}
