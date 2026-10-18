using System.IO;
using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class FontUnicodeMapTests
{
    private static PdfUnicodeMap Parse(string body, PdfOperationPolicy? policy = null) => PdfUnicodeMap.Parse(new MemoryStream(Encoding.ASCII.GetBytes("begincmap\n" + body + "\nendcmap")), policy: policy);
    [Fact]
    public void VariableLengthCodesRangesLigaturesAndSupplementaryUnicodeDecodeExactly()
    {
        var map = Parse("3 begincodespacerange <01> <7F> <8000> <8FFF> <900000> <9FFFFF> endcodespacerange 2 beginbfchar <01> <006600660069> <8001> <D83DDE00> endbfchar 1 beginbfrange <02> <04> <0041> endbfrange 1 beginbfrange <900001> <900002> [<4E2D> <6587>] endbfrange");
        Assert.Equal("ffiABC😀中文", map.Decode(PdfValue.String(new byte[] { 1, 2, 3, 4, 0x80, 1, 0x90, 0, 1, 0x90, 0, 2 })).Text);
        Assert.Equal(7, map.Count);
    }

    [Fact]
    public void ExplicitInheritedCmapsCanBeOverriddenLocallyWithoutLoadingResources()
    {
        var parent = Parse("1 begincodespacerange <00> <FF> endcodespacerange 1 beginbfchar <01> <0041> endbfchar");
        using var input = new MemoryStream(Encoding.ASCII.GetBytes("begincmap /Parent usecmap 1 beginbfchar <01> <0042> endbfchar endcmap"));
        var map = PdfUnicodeMap.Parse(input, new Dictionary<string, PdfUnicodeMap> { { "Parent", parent } });
        Assert.Equal("B", map.Decode(PdfValue.String(new byte[] { 1 })).Text);
        Assert.Equal("Font.CMapProviderRequired", Assert.Throws<PdfException>(() => Parse("/Absent usecmap")).Diagnostic.Code);
    }

    [Theory]
    [InlineData("1 begincodespacerange <00> <FF> endcodespacerange 1 beginbfchar <01> <D800> endbfchar")]
    [InlineData("1 begincodespacerange <00> <FF> endcodespacerange 1 beginbfchar <0101> <0041> endbfchar")]
    [InlineData("2 begincodespacerange <00> <FF> <0000> <FFFF> endcodespacerange")]
    [InlineData("1 begincodespacerange <00> <FF> endcodespacerange 1 beginbfrange <01> <02> [<0041>] endbfrange")]
    [InlineData("1 begincodespacerange <FF> <00> endcodespacerange")]
    [InlineData("1 begincodespacerange <00> <FF> endcodespacerange 2 beginbfchar <01> <0041> <01> <0042> endbfchar")]
    public void MalformedOrAmbiguousUnicodeMapsRejectPrecisely(string body)
    {
        Assert.Equal("Font.CMapMalformed", Assert.Throws<PdfException>(() => Parse(body)).Diagnostic.Code);
    }

    [Fact]
    public void UnsupportedExecutableConstructsNeverRunAndHugeRangesChargeBeforeExpansion()
    {
        Assert.Equal("Font.CMapOperatorUnsupported", Assert.Throws<PdfException>(() => Parse("run")).Diagnostic.Code);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => Parse("1 begincodespacerange <00000000> <FFFFFFFF> endcodespacerange 1 beginbfrange <00000000> <FFFFFFFF> <0041> endbfrange")).Diagnostic.Code);
        var map = Parse("1 begincodespacerange <00> <FF> endcodespacerange");
        Assert.Equal("Font.UnmappedCode", Assert.Throws<PdfException>(() => map.Decode(PdfValue.String(new byte[] { 1 }))).Diagnostic.Code);
        var permissive = map.Decode(PdfValue.String(new byte[] { 1 }), new(PdfProcessingMode.Permissive));
        Assert.Equal("�", permissive.Text);
        Assert.Single(permissive.OperationReport.Diagnostics);
        Assert.Equal(PdfLossCategory.SubstitutedContent, permissive.OperationReport.Diagnostics[0].LossCategory);
    }

    [Theory]
    [InlineData("original.ttf", "AB一😀")]
    [InlineData("original.otf", "AB一😀")]
    [InlineData("original.pfb", "AÁ")]
    [InlineData("licensed-cjk.otf", "中文一AB")]
    public void SavedFontBindingsExtractTheirExactProgramsAndDecodeAfterDocumentDisposal(string filename, string text)
    {
        var font = FontTests.Read(filename);
        var resource = PdfFontResource.CreateText(font, text);
        using var original = Pdf.Create().AddPage();
        original.Pages[0].SetFont("F1", resource);
        original.Pages[0].Content = FontTests.Show(resource);
        using var output = new MemoryStream();
        original.Save(output);
        output.Position = 0;
        PdfPageFont binding;
        using (var reopened = Pdf.Open(output, providers: FontTests.Providers))
            binding = Assert.Single(reopened.Pages[0].ReadFonts());
        Assert.Equal(text, binding.DecodeText(resource.EncodedText).Text);
        using var bytes = binding.ExtractProgram().OpenRead();
        var loaded = PdfFont.Read(bytes, FontTests.Providers);
        Assert.Equal(font.Info.Format, loaded.Info.Format);
        Assert.NotEqual(0, binding.Load().GetGlyph(text[0]).GlyphIndex);
    }

    [Fact]
    public void SourceBaseFourteenFontsDecodeThroughTheExplicitEncodingProvider()
    {
        using var input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "syntax", "one-page-classic.pdf"));
        using var document = Pdf.Open(input, providers: FontTests.Providers);
        var font = Assert.Single(document.Pages[0].ReadFonts());
        var shown = document.Pages[0].ReadContent().Operations.Single(x => x.Operation == PdfOperator.ShowText).Operands[0];
        var decoded = font.DecodeText(shown);
        Assert.Equal("Original syntax fixture", decoded.Text);
        Assert.Contains(decoded.OperationReport.Diagnostics, x => x.Code == "Font.UnicodeInferred");
        Assert.Equal(PdfFontFormat.Standard, font.Load().Info.Format);
    }
}
