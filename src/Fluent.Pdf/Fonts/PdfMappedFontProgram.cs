using System.IO;

namespace Fluent.Pdf;
internal sealed class PdfMappedFontProgram : IPdfFontProgram, IPdfVerticalFontProgram
{
    private readonly IPdfFontProgram _inner;
    private readonly Dictionary<int, int> _unicode;
    internal PdfMappedFontProgram(IPdfFontProgram inner, PdfUnicodeMap map, IPdfCidFontProgram cids, PdfOperationContext context)
    {
        _inner = inner;
        _unicode = map.ScalarMappings((code, length) => length == 2 && code <= 65535 ? cids.MapCid((int)code, context) : 0, context);
    }

    public PdfFontInfo Info => _inner.Info;

    public int MapGlyph(int unicodeScalar, PdfOperationContext context)
    {
        PdfFont.Scalar(unicodeScalar);
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        return _unicode.TryGetValue(unicodeScalar, out int glyph) ? glyph : _inner.MapGlyph(unicodeScalar, context);
    }

    public double GetAdvance(int glyph, PdfOperationContext context) => _inner.GetAdvance(glyph, context);
    public string GetGlyphName(int glyph, PdfOperationContext context) => _inner.GetGlyphName(glyph, context);
    public PdfFontProgramWriteResult Write(Stream output, IReadOnlyList<int> glyphs, bool subset, PdfOperationContext context) => _inner.Write(output, glyphs, subset, context);
    public bool SupportsVerticalMetrics => _inner is IPdfVerticalFontProgram vertical && vertical.SupportsVerticalMetrics;

    public PdfFontVerticalMetrics GetVerticalMetrics(int glyph, PdfOperationContext context)
    {
        if (_inner is IPdfVerticalFontProgram vertical && vertical.SupportsVerticalMetrics)
            return vertical.GetVerticalMetrics(glyph, context);
        throw PdfWriterCore.Error(context, "Font.VerticalMetricsUnavailable", "The selected embedded font has no complete vertical metrics.");
    }
}
