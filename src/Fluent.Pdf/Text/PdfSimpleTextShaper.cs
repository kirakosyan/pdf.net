namespace Fluent.Pdf;
/// <summary>Explicit scalar-only Latin/Greek/Cyrillic shaping. Complex scripts, marks, feature overrides and vertical writing fail.</summary>
public sealed class PdfSimpleTextShaper : IPdfTextShaper
{
    /// <summary>Gets the narrowly scoped managed capability declaration.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("managed.simple-text", "1.0.0", PdfCapability.ShapeText);

    /// <summary>Maps simple scalars to exact glyphs and advances without claiming OpenType shaping.</summary>
    public IReadOnlyList<PdfShapedGlyph> Shape(PdfShapingRequest request, PdfOperationContext context)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        if (request.Direction != PdfTextDirection.LeftToRight || request.Features.Count != 0 || request.ScriptTag != 0x4c61746e && request.ScriptTag != 0x4772656b && request.ScriptTag != 0x4379726c && request.ScriptTag != 0x5a797979)
            throw PdfWriterCore.Error(context, "Text.ComplexShaperRequired", "The requested script, direction or feature requires an explicitly configured OpenType shaper.");
        var data = PdfUnicodeData.For(context);
        context.Consume(PdfResourceKind.ExpandedBytes, request.Length * 48L + 32);
        var glyphs = new List<PdfShapedGlyph>();
        for (int i = request.Start; i < request.Start + request.Length; i++)
        {
            context.Checkpoint();
            int begin = i, scalar = request.Text[i];
            if (char.IsHighSurrogate(request.Text[i]))
                scalar = char.ConvertToUtf32(request.Text[i], request.Text[++i]);
            var property = data.Get(scalar);
            if (property.Ignorable)
                continue;
            if (property.Break == PdfUnicodeData.Grapheme.Extend || property.Break == PdfUnicodeData.Grapheme.SpacingMark || property.Break == PdfUnicodeData.Grapheme.ZWJ)
                throw PdfWriterCore.Error(context, "Text.ComplexShaperRequired", "Combining marks require an explicitly configured OpenType shaper.");
            int glyph = request.Font.MapCore(scalar, context);
            glyphs.Add(new PdfShapedGlyph(glyph, begin, request.Font.Program.GetAdvance(glyph, context), 0));
        }

        return glyphs;
    }
}
