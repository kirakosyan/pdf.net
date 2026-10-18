using System.IO;
using System.Text;
using System.Threading;
using Fluent.Pdf.Fonts;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class FontTests
{
    [Fact]
    public void CSharpAndVisualBasicFontConsumersPreserveBorrowedStreamsAndUnicode()
    {
        foreach (var write in new Func<Stream, Stream, string, PdfOperationReport>[]
        {
            ContractExamples.CSharp.ContractExample.WriteFontPdf,
            ContractExamples.VisualBasic.ContractExample.WriteFontPdf
        }

        )
        {
            using var font = File.OpenRead(Fixture("original.ttf"));
            using var output = new MemoryStream();
            Assert.True(write(font, output, "A一😀").Succeeded);
            Assert.True(font.CanRead);
            Assert.True(output.CanWrite);
            output.Position = 0;
            using var document = Pdf.Open(output);
            var binding = Assert.Single(document.Pages[0].ReadFonts());
            var encoded = document.Pages[0].ReadContent().Operations.Single(x => x.Operation == PdfOperator.ShowText).Operands[0];
            Assert.Equal("A一😀", binding.DecodeText(encoded).Text);
        }
    }

    internal static readonly PdfProviderRegistry Providers = new(ManagedPdfProvider.Instance, ManagedFontProvider.Instance);
    internal static string Fixture(string name) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "fonts", name);
    internal static PdfFont Read(string name)
    {
        using var input = File.OpenRead(Fixture(name));
        return PdfFont.Read(input, Providers);
    }

    [Theory]
    [InlineData("original.ttf", PdfFontFormat.TrueType)]
    [InlineData("original.otf", PdfFontFormat.Cff)]
    [InlineData("original.pfb", PdfFontFormat.Type1)]
    [InlineData("original.pfa", PdfFontFormat.Type1)]
    public void OriginalIndependentFontProgramsMapMetricsAndRetainSubsetDependencies(string filename, PdfFontFormat format)
    {
        using var input = File.OpenRead(Fixture(filename));
        var font = PdfFont.Read(input, Providers);
        Assert.True(input.CanRead);
        Assert.True(font.OperationReport.Succeeded);
        Assert.Equal(format, font.Info.Format);
        Assert.Equal(600, font.GetGlyph('A').Advance);
        Assert.Equal(0, font.GetGlyph(0x10ffff).GlyphIndex);
        Assert.Null(font.GetGlyph('A').VerticalAdvance);
        var accented = font.GetGlyph(193);
        Assert.NotEqual(0, accented.GlyphIndex);
        using var output = new MemoryStream();
        var written = font.WriteProgram(output, new[] { accented.GlyphIndex });
        Assert.True(output.CanWrite);
        Assert.True(written.OperationReport.Succeeded);
        Assert.Contains("+", written.Program.PostScriptName);
        output.Position = 0;
        var subset = PdfFont.Read(output, Providers);
        Assert.NotEqual(0, subset.GetGlyph(193).GlyphIndex);
        Assert.Equal(0, subset.GetGlyph('B').GlyphIndex);
        if (format == PdfFontFormat.TrueType || format == PdfFontFormat.Type1)
            Assert.NotEqual(0, subset.GetGlyph('A').GlyphIndex);
        if (format == PdfFontFormat.Type1)
            Assert.Equal(4, subset.Info.GlyphCount);
    }

    [Fact]
    public void RawCffAndLegacyCffCompositeAccentsProduceSelfContainedCidPrograms()
    {
        foreach (string filename in new[]
        {
            "original.cff",
            "original-seac.otf"
        }

        )
        {
            var font = Read(filename);
            var glyph = font.GetGlyph(193);
            Assert.NotEqual(0, glyph.GlyphIndex);
            Assert.Equal(600, glyph.Advance);
            var resource = PdfFontResource.CreateText(font, "AÁ");
            using var document = Pdf.Create().AddPage();
            document.Pages[0].SetFont("F1", resource);
            document.Pages[0].Content = Show(resource);
            using var output = new MemoryStream();
            document.Save(output);
            output.Position = 0;
            using var reopened = Pdf.Open(output, providers: Providers);
            var binding = Assert.Single(reopened.Pages[0].ReadFonts());
            Assert.Equal("AÁ", binding.DecodeText(resource.EncodedText).Text);
            Assert.NotEqual(0, binding.Load().GetGlyph(193).GlyphIndex);
        }
    }

    [Fact]
    public void CollectionsSelectTheExactCallerFaceAndSubsetAsAStandaloneFont()
    {
        using var input = File.OpenRead(Fixture("original.ttc"));
        var first = PdfFont.Read(input, Providers);
        input.Position = 0;
        var second = PdfFont.Read(input, Providers, faceIndex: 1);
        Assert.Equal(600, first.GetGlyph('A').Advance);
        Assert.Equal(650, second.GetGlyph('A').Advance);
        Assert.NotEqual(first.Info.Sha256, second.Info.Sha256);
        Assert.Equal("FluentCollectionSecond", second.Info.PostScriptName);
        using var output = new MemoryStream();
        second.WriteProgram(output, new[] { second.GetGlyph('A').GlyphIndex });
        output.Position = 0;
        Assert.Equal(650, PdfFont.Read(output, Providers).GetGlyph('A').Advance);
        input.Position = 0;
        Assert.Equal("Font.CollectionFace", Assert.Throws<PdfException>(() => PdfFont.Read(input, Providers, faceIndex: 2)).Diagnostic.Code);
    }

    [Fact]
    public void AllFourteenStandardFontsUseDeterministicMetricsAndSymbolMappings()
    {
        foreach (string name in new[]
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
        }

        )
        {
            var font = PdfFont.Standard(name, Providers);
            Assert.Equal(PdfFontFormat.Standard, font.Info.Format);
            Assert.Equal(name, font.Info.PostScriptName);
            Assert.NotEqual(0, font.GetGlyph(name == "Symbol" ? 0x391 : name == "ZapfDingbats" ? 0x2701 : 'A').GlyphIndex);
        }

        Assert.Equal(667, PdfFont.Standard("Helvetica", Providers).GetGlyph('A').Advance);
        Assert.Equal(600, PdfFont.Standard("Courier", Providers).GetGlyph('A').Advance);
        Assert.Equal("Font.StandardName", Assert.Throws<PdfException>(() => PdfFont.Standard("MachineFontAlias", Providers)).Diagnostic.Code);
        Assert.Throws<PdfProviderUnavailableException>(() => PdfFont.Standard("Helvetica", PdfProviderRegistry.Empty));
    }

    [Fact]
    public void FallbackUsesOnlyTheCallerOrderAndReportsMissingGlyphs()
    {
        var latin = PdfFont.Standard("Helvetica", Providers);
        var original = Read("original.ttf");
        var first = new PdfFontFallback(latin, original);
        var second = new PdfFontFallback(original, latin);
        Assert.Same(latin, first.Resolve('A').Font);
        Assert.Same(original, second.Resolve('A').Font);
        Assert.Same(original, first.Resolve(0x4e00).Font);
        Assert.Equal("Font.MissingGlyph", Assert.Throws<PdfException>(() => first.Resolve(0x10ffff)).Diagnostic.Code);
        Assert.Throws<ArgumentOutOfRangeException>(() => first.Resolve(0xd800));
    }

    [Theory]
    [InlineData("original.ttf", "AB一😀", false)]
    [InlineData("original.otf", "A一😀", true)]
    [InlineData("original.pfb", "AÁ", false)]
    [InlineData("original.pfa", "AÁ", false)]
    [InlineData("licensed-cjk.otf", "中文一AB", true)]
    public void FontResourcesEmbedEagerlyAndPreserveToUnicodeWhenSavedAndReopened(string filename, string text, bool cff)
    {
        var font = Read(filename);
        var resource = PdfFontResource.CreateText(font, text);
        Assert.True(resource.OperationReport.Succeeded);
        Assert.Equal(cff ? "1.6" : "1.2", resource.MinimumVersion);
        using var document = Pdf.Create().AddPage();
        document.Pages[0].SetFont("F1", resource);
        document.Pages[0].Content = Show(resource);
        var snapshot = document.CreateSnapshot();
        document.Pages[0].RemoveFont("F1");
        Assert.Same(resource, snapshot.Pages[0].FontResources["F1"]);
        document.Pages[0].SetFont("F1", resource);
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        var source = PdfSyntax.Read(output);
        var catalog = source.Resolve(source.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        var tree = source.Resolve(catalog.Entries["Pages"].ReferenceId);
        var page = source.Resolve(tree.Entries["Kids"].Items[0].ReferenceId);
        var embedded = source.Resolve(page.Entries["Resources"].Entries["Font"].Entries["F1"].ReferenceId);
        Assert.True(embedded.Entries.ContainsKey("ToUnicode"));
        var unicode = source.DecodeStream(embedded.Entries["ToUnicode"].ReferenceId);
        using var mapping = unicode.OpenRead();
        using var reader = new StreamReader(mapping, Encoding.ASCII);
        Assert.Contains("beginbfchar", reader.ReadToEnd());
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        Assert.Equal(resource.EncodedText.StringBytes, reopened.Pages[0].ReadContent().Operations.Single(x => x.Operation == PdfOperator.ShowText).Operands[0].StringBytes);
    }

    [Fact]
    public void MissingMappingsAndUnsupportedVerticalMetricsFailBeforeFontResourcesAreReturned()
    {
        var font = Read("original.ttf");
        Assert.Equal("Font.MissingGlyph", Assert.Throws<PdfException>(() => PdfFontResource.CreateText(font, "unknown Ω")).Diagnostic.Code);
        Assert.Equal("Font.VerticalMetricsUnavailable", Assert.Throws<PdfException>(() => PdfFontResource.CreateText(font, "A", vertical: true)).Diagnostic.Code);
        Assert.Throws<ArgumentException>(() => PdfFontResource.CreateText(font, "\ud800"));
    }

    [Fact]
    public void FontReadAndSubsetBoundariesHonorBudgetsCancellationAndCallerOwnership()
    {
        byte[] bytes = File.ReadAllBytes(Fixture("original.ttf"));
        using var input = new MemoryStream(bytes);
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => PdfFont.Read(input, Providers, policy: new(limits: new(maximumInputBytes: bytes.Length - 1)))).Diagnostic.Code);
        Assert.True(input.CanRead);
        input.Position = 0;
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfFont.Read(input, Providers, policy: new(limits: new(maximumExpandedBytes: 4095)))).Diagnostic.Code);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        input.Position = 0;
        Assert.Throws<OperationCanceledException>(() => PdfFont.Read(input, Providers, cancellationToken: canceled.Token));
        var font = Read("original.ttf");
        using var output = new MemoryStream();
        Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => font.WriteProgram(output, new[] { font.GetGlyph('A').GlyphIndex }, policy: new(limits: new(maximumOutputBytes: 1)))).Diagnostic.Code);
        Assert.True(output.CanWrite);
        Assert.True(font.WriteProgram(output, new[] { font.GetGlyph('A').GlyphIndex }).OperationReport.Succeeded);
    }

    [Theory]
    [InlineData(2, "Font.EmbeddingRestricted", true)]
    [InlineData(512, "Font.EmbeddingRestricted", true)]
    [InlineData(256, "Font.SubsettingRestricted", true)]
    public void DeclaredEmbeddingRestrictionsAreEnforcedBeforeOutput(int flags, string code, bool subset)
    {
        byte[] bytes = File.ReadAllBytes(Fixture("original.ttf"));
        int tables = bytes[4] * 256 + bytes[5];
        for (int i = 0; i < tables; i++)
        {
            int record = 12 + i * 16;
            if (Encoding.ASCII.GetString(bytes, record, 4) != "OS/2")
                continue;
            int at = (bytes[record + 8] << 24 | bytes[record + 9] << 16 | bytes[record + 10] << 8 | bytes[record + 11]) + 8;
            bytes[at] = (byte)(flags >> 8);
            bytes[at + 1] = (byte)flags;
        }

        using var input = new MemoryStream(bytes);
        var font = PdfFont.Read(input, Providers);
        using var output = new MemoryStream();
        Assert.Equal(code, Assert.Throws<PdfException>(() => font.WriteProgram(output, new[] { font.GetGlyph('A').GlyphIndex }, subset)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }

    internal static PdfContentProgram Show(PdfFontResource resource) => PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.BeginText), new PdfContentOperation(PdfOperator.Font, PdfValue.Name("F1"), PdfValue.Integer(24)), new PdfContentOperation(PdfOperator.MoveText, PdfValue.Integer(20), PdfValue.Integer(60)), new PdfContentOperation(PdfOperator.ShowText, resource.EncodedText), new PdfContentOperation(PdfOperator.EndText) });
}
