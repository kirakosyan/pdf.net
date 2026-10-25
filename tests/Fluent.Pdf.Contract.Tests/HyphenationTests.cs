using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class HyphenationTests
{
    private static string Hash(byte[] bytes) { using var h = SHA256.Create(); return BitConverter.ToString(h.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant(); }
    private static PdfHyphenationDictionary Dictionary(string data = "office\t2,4\n", string language = "en")
    { byte[] bytes = Encoding.UTF8.GetBytes(data); using var input = new MemoryStream(bytes); var result = PdfHyphenationDictionary.Read(input, language, "original-1", "repository-original-MIT-exceptions", Hash(bytes)); Assert.True(input.CanRead); return result; }
    private static PdfFlowParagraph Paragraph(string text = "officeoffice", string language = "en") => new(text, new(new(TextTests.Read("business.ttf")), 12, new(language: language)), new(orphanLines: 1, widowLines: 1));
    private static PdfFlowOptions Options(double width) => new(new(new(new(0, 0, width, 200))), PdfMargins.Uniform(0));
    private static PdfFlowResult Layout(PdfFlowParagraph p, double width = 30) => PdfFlow.Layout(new[] { p }, Options(width), new(new PdfSimpleTextShaper()));
    private static string Artifact(string name) { string p = Path.Combine(AppContext.BaseDirectory, "artifacts", "hyphenation"); Directory.CreateDirectory(p); return Path.Combine(p, name); }

    [Fact]
    public void DictionaryBreaksPaintHyphensAndRetainLogicalActualText()
    {
        var plain = Paragraph(); var dictionary = Dictionary("officeoffice\t2,4,6,8,10\n"); var p = plain.WithHyphenation(dictionary);
        Assert.Null(plain.Hyphenation); Assert.Same(dictionary, p.Hyphenation);
        Assert.Equal("Layout.WordOverflow", Assert.Throws<PdfException>(() => Layout(plain)).Diagnostic.Code);
        var result = Layout(p); var lines = result.Pages.SelectMany(x => x.Lines).ToArray();
        Assert.True(lines.Length > 1); Assert.Equal("officeoffice", string.Concat(lines.Select(x => x.Text)));
        Assert.Contains(lines, x => x.HasDiscretionaryHyphen); Assert.False(lines[lines.Length - 1].HasDiscretionaryHyphen);
        Assert.All(lines, x => { Assert.False(x.IsClipped); Assert.True(x.Advance <= x.Bounds.Width); });
        using var document = result.CreateDocument();
        var text = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0);
        Assert.Equal("officeoffice", string.Concat(text.ActualTextSpans.Select(x => x.Text)));
        Assert.Contains("-", text.RawText); Assert.DoesNotContain("-", text.Text);
        document.SaveFile(Artifact("hyphenated.pdf"));
        var wide = Layout(p, 160); Assert.Single(wide.Pages[0].Lines); Assert.False(wide.Pages[0].Lines[0].HasDiscretionaryHyphen);
        using var wideDocument = wide.CreateDocument(); wideDocument.SaveFile(Artifact("wide.pdf"));
        Assert.Equal(lines.Select(x => x.Text), Layout(p).Pages.SelectMany(x => x.Lines).Select(x => x.Text));
    }

    [Theory]
    [InlineData("en", "office\t2,4\n")]
    [InlineData("nb", "arbeid\t2,4\n")]
    [InlineData("hy", "բարեւ\t2\n")]
    [InlineData("fr", "école\t2\n")]
    public void ExactLanguagesAndCombiningGraphemesHavePinnedOfflineEntries(string language, string data)
    {
        var dictionary = Dictionary(data, language); Assert.Equal(language, dictionary.Language); Assert.Equal(1, dictionary.Count);
        Assert.Equal(Hash(Encoding.UTF8.GetBytes(data)), dictionary.Sha256); Assert.Equal("original-1", dictionary.Version);
    }

    [Theory]
    [InlineData("en", "officeoffice", "2,4,6,8,10", false)]
    [InlineData("nb", "arbeidarbeid", "2,4,6,8,10", true)]
    public void LanguageConsumersRetainExactWordAcrossDiscretionaryBreaks(string language, string word, string breaks, bool visualBasic)
    {
        var dictionary = Dictionary(word + "\t" + breaks + "\n", language); var paragraph = Paragraph(word, language); var providers = new PdfProviderRegistry(new PdfSimpleTextShaper());
        var layout = visualBasic ? ContractExamples.VisualBasic.ContractExample.HyphenateParagraph(paragraph, dictionary, Options(30), providers) : ContractExamples.CSharp.ContractExample.HyphenateParagraph(paragraph, dictionary, Options(30), providers);
        var lines = layout.Pages.SelectMany(p => p.Lines).ToArray();
        Assert.Equal(word, string.Concat(lines.Select(l => l.Text))); Assert.Contains(lines, l => l.HasDiscretionaryHyphen);
        using var document = layout.CreateDocument(); document.SaveFile(Artifact(language + ".pdf"));
    }

    [Theory]
    [InlineData("office\t0\n", "Hyphenation.Boundary")]
    [InlineData("office\t2,2\n", "Hyphenation.Boundary")]
    [InlineData("office\t6\n", "Hyphenation.Boundary")]
    [InlineData("école\t1\n", "Hyphenation.Boundary")]
    [InlineData("office\t2\noffice\t4\n", "Hyphenation.Word")]
    [InlineData("office\t2\r\n", "Hyphenation.Syntax")]
    [InlineData("office\t2", "Hyphenation.Syntax")]
    [InlineData("off-ice\t2\n", "Hyphenation.Word")]
    public void InvalidCustomRulesReject(string data, string code) => Assert.Equal(code, Assert.Throws<PdfException>(() => Dictionary(data)).Diagnostic.Code);

    [Fact]
    public void SourcePinsBudgetsCancellationAndLanguageMismatchFail()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("office\t2\n"); using var input = new MemoryStream(bytes);
        Assert.Equal("Hyphenation.SourceHash", Assert.Throws<PdfException>(() => PdfHyphenationDictionary.Read(input, "en", "1", "original-MIT", new('0', 64))).Diagnostic.Code);
        input.Position = 0;
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => PdfHyphenationDictionary.Read(input, "en", "1", "original-MIT", Hash(bytes), new(limits: new(maximumInputBytes: 1)))).Diagnostic.Code);
        input.Position = 0;
        Assert.ThrowsAny<OperationCanceledException>(() => PdfHyphenationDictionary.Read(input, "en", "1", "original-MIT", Hash(bytes), cancellationToken: new(true)));
        Assert.Equal("Hyphenation.Language", Assert.Throws<PdfException>(() => Layout(Paragraph("office", "fr").WithHyphenation(Dictionary()))).Diagnostic.Code);
        Assert.Equal("Hyphenation.DirectionUnsupported", Assert.Throws<PdfException>(() => Layout(Paragraph("אoffice").WithHyphenation(Dictionary()))).Diagnostic.Code);
        using var invalid = new MemoryStream(new byte[] { 0xc0, 0x80, 10 });
        Assert.Equal("Hyphenation.Encoding", Assert.Throws<PdfException>(() => PdfHyphenationDictionary.Read(invalid, "en", "1", "original-MIT", Hash(invalid.ToArray()))).Diagnostic.Code);
        Assert.Equal("Layout.WordOverflow", Assert.Throws<PdfException>(() => Layout(Paragraph("office").WithHyphenation(Dictionary()), 1)).Diagnostic.Code);
    }

    [Fact]
    public void PaperFitEnforcesFloorCeilingMarginsAndSourceOwnership()
    {
        using var document = Layout(Paragraph("office"), 160).CreateDocument(); var snapshot = document.CreateSnapshot();
        using var fit = PdfPageImposition.FitToPaper(snapshot, new(80, 100), .5);
        Assert.Equal(80, fit.Pages[0].Size.Width); Assert.Equal(160, document.Pages[0].Size.Width);
        fit.SaveFile(Artifact("fit.pdf")); document.SaveFile(Artifact("fit-source.pdf"));
        Assert.Equal("Layout.MinimumScale", Assert.Throws<PdfException>(() => PdfPageImposition.FitToPaper(snapshot, new(80, 100), .51)).Diagnostic.Code);
        Assert.Equal("Layout.MinimumScale", Assert.Throws<PdfException>(() => PdfPageImposition.FitToPaper(snapshot, new(80, 100), .5, margins: PdfMargins.Uniform(1))).Diagnostic.Code);
        using var ceiling = PdfPageImposition.FitToPaper(snapshot, new(320, 400), .5, 1);
        ceiling.SaveFile(Artifact("ceiling.pdf"));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfPageImposition.FitToPaper(snapshot, new(80, 100), double.NaN));
        Assert.ThrowsAny<OperationCanceledException>(() => PdfPageImposition.FitToPaper(snapshot, new(80, 100), .5, cancellationToken: new(true)));
    }
}
