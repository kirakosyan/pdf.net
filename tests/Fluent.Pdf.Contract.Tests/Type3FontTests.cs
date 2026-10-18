using System.IO;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class Type3FontTests
{
    private static PdfContentProgram Glyph(params PdfContentOperation[] rest) => PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.GlyphBounds, PdfValue.Integer(600), PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(600), PdfValue.Integer(800)) }.Concat(rest), PdfContentKind.Type3Glyph);
    private static PdfFont Create(PdfContentProgram program) => PdfFont.Type3("OriginalType3", new(1000, 800, 0, 800, 0, 0, 600, 800), new[] { new PdfType3Glyph("A", "A", program) });
    [Fact]
    public void AuthoredType3GlyphProgramsAndMultipleUnicodeMappingsSurviveSaving()
    {
        var glyph = Glyph(new PdfContentOperation(PdfOperator.Rectangle, PdfValue.Integer(100), PdfValue.Integer(100), PdfValue.Integer(400), PdfValue.Integer(600)), new PdfContentOperation(PdfOperator.Fill));
        var font = Create(glyph);
        Assert.Equal(PdfFontFormat.Type3, font.Info.Format);
        Assert.Equal(600, font.GetGlyph('A').Advance);
        var resource = PdfFontResource.Create(font, new[] { new PdfGlyphMapping(1, "A"), new PdfGlyphMapping(1, "Ａ") });
        using var document = Pdf.Create().AddPage();
        document.Pages[0].SetFont("F1", resource);
        document.Pages[0].Content = FontTests.Show(resource);
        Assert.Equal("AＡ", Assert.Single(document.Pages[0].ReadFonts()).DecodeText(resource.EncodedText).Text);
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        var binding = Assert.Single(reopened.Pages[0].ReadFonts());
        Assert.Equal("Type3", binding.Subtype);
        Assert.Equal("AＡ", binding.DecodeText(resource.EncodedText).Text);
        Assert.Equal("Font.NoEmbeddedProgram", Assert.Throws<PdfException>(() => binding.ExtractProgram()).Diagnostic.Code);
    }

    [Fact]
    public void UncoloredGlyphsRejectColorOperatorsAndDeclaredBoundsMustFitTheFont()
    {
        Assert.Equal("Content.Type3Color", Assert.Throws<PdfException>(() => Glyph(new PdfContentOperation(PdfOperator.FillRgb, PdfValue.Integer(1), PdfValue.Integer(0), PdfValue.Integer(0)))).Diagnostic.Code);
        var glyph = Glyph();
        Assert.Equal("Font.Type3Bounds", Assert.Throws<PdfException>(() => PdfFont.Type3("TooSmall", new(1000, 800, 0, 800, 0, 0, 500, 700), new[] { new PdfType3Glyph("A", "A", glyph) })).Diagnostic.Code);
        Assert.Throws<ArgumentException>(() => new PdfType3Glyph("A", "A", PdfContentProgram.Create(Array.Empty<PdfContentOperation>())));
    }

    [Fact]
    public void Type3ExtensionVersionFloorsAreIncludedBeforeAnyFileOutput()
    {
        var program = PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.GlyphWidth, PdfValue.Integer(600), PdfValue.Integer(0)), PdfContentOperation.Extension("original_glyph_extension", "2.0") }, PdfContentKind.Type3Glyph);
        var font = Create(program);
        var resource = PdfFontResource.CreateText(font, "A");
        Assert.Equal("2.0", resource.MinimumVersion);
        using var document = Pdf.Create().AddPage();
        document.Pages[0].SetFont("F1", resource);
        document.Pages[0].Content = FontTests.Show(resource);
        using var output = new MemoryStream();
        Assert.Equal("Writer.VersionTooLow", Assert.Throws<PdfException>(() => document.Save(output, writeOptions: new(version: "1.7"))).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }
}
