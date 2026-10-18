using System.IO;
using System.Text;

namespace Fluent.Pdf.Fonts;
internal sealed class RawCffFont : IPdfFontProgram, IPdfCidFontProgram
{
    private readonly CffFont _font;
    private readonly Dictionary<int, int> _unicode = new();
    private readonly Dictionary<int, int> _cids = new();
    public PdfFontInfo Info { get; }

    internal RawCffFont(byte[] source, PdfOperationContext context)
    {
        _font = new CffFont(source, 0, context);
        Info = new PdfFontInfo(_font.Name, PdfFontFormat.Cff, _font.GlyphCount, _font.Metrics(context), FontData.Hash(source, context));
        if (!_font.CidKeyed)
        {
            var names = FontResources.GlyphList(context);
            context.Consume(PdfResourceKind.ExpandedBytes, _font.GlyphCount * 64L);
            for (int glyph = 0; glyph < _font.GlyphCount; glyph++)
            {
                context.Checkpoint();
                string text = FontResources.Unicode(_font.GlyphName(glyph), names);
                if (text.Length == 1 && !char.IsSurrogate(text[0]))
                    _unicode[(int)text[0]] = glyph;
                else if (text.Length == 2 && char.IsSurrogatePair(text, 0))
                    _unicode[char.ConvertToUtf32(text, 0)] = glyph;
            }
        }
        else
        {
            context.Consume(PdfResourceKind.ExpandedBytes, _font.GlyphCount * 64L);
            _cids.Add(0, 0);
            for (int glyph = 1; glyph < _font.GlyphCount; glyph++)
            {
                context.Checkpoint();
                string name = _font.GlyphName(glyph);
                if (!name.StartsWith("cid", StringComparison.Ordinal) || !int.TryParse(name.Substring(3), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int cid) || _cids.ContainsKey(cid))
                    throw PdfWriterCore.Error(context, "Font.CffCharset", "CID identities must be unique bounded integers.");
                _cids.Add(cid, glyph);
            }
        }
    }

    public int MapCid(int cid, PdfOperationContext context)
    {
        if (cid < 0 || cid > 65535)
            throw new ArgumentOutOfRangeException(nameof(cid));
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        return _cids.TryGetValue(cid, out int glyph) ? glyph : 0;
    }

    public int MapGlyph(int unicodeScalar, PdfOperationContext context)
    {
        FontData.Scalar(unicodeScalar);
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        return _unicode.TryGetValue(unicodeScalar, out int glyph) ? glyph : 0;
    }

    private void Check(int glyph, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        if (glyph < 0 || glyph >= Info.GlyphCount)
            throw new ArgumentOutOfRangeException(nameof(glyph));
    }

    public double GetAdvance(int glyph, PdfOperationContext context)
    {
        Check(glyph, context);
        return _font.Width(glyph, context);
    }

    public string GetGlyphName(int glyph, PdfOperationContext context)
    {
        Check(glyph, context);
        return _font.GlyphName(glyph);
    }

    public PdfFontProgramWriteResult Write(Stream output, IReadOnlyList<int> glyphs, bool subset, PdfOperationContext context)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (glyphs == null)
            throw new ArgumentNullException(nameof(glyphs));
        context.Consume(PdfResourceKind.ExpandedBytes, glyphs.Count * 64L + 512);
        var retained = new HashSet<int>
        {
            0
        };
        foreach (int glyph in glyphs)
        {
            Check(glyph, context);
            retained.Add(glyph);
        }

        string identity = Info.Sha256 + ":" + string.Join(",", retained.OrderBy(x => x).Select(x => x.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        string digest = FontData.Hash(Encoding.ASCII.GetBytes(identity), context);
        string name = subset ? new string (Enumerable.Range(0, 6).Select(i => (char)('A' + Convert.ToInt32(digest.Substring(i * 2, 2), 16) % 26)).ToArray()) + "+FluentCff" : "FluentEmbedded-" + Info.Sha256.Substring(0, 16);
        byte[] program = _font.Write(name, retained, subset, context);
        output.Write(program, 0, program.Length);
        context.Checkpoint();
        return new PdfFontProgramWriteResult(name, "CIDFontType0C", cidKeyed: true);
    }
}
