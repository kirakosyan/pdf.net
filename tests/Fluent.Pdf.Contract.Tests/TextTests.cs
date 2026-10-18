using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fluent.Pdf.Fonts;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class TextTests
{
    internal static PdfFont Read(string name)
    {
        using var input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "text", name));
        return PdfFont.Read(input, new PdfProviderRegistry(ManagedFontProvider.Instance));
    }

    private static PdfProviderRegistry Simple() => new(new PdfSimpleTextShaper());
    [Fact]
    public void SimpleShapingEmitsExactGlyphMatricesAndLogicalActualText()
    {
        var font = Read("latin.ttf");
        var shaped = PdfText.Shape("office", new(font), Simple());
        Assert.Equal("office", shaped.Unicode.Text);
        Assert.Equal(6, Assert.Single(shaped.Runs).Glyphs.Count);
        var drawing = PdfTextDrawing.Create(shaped, 20, 80, 24);
        Assert.Equal(6, drawing.Content.Operations.Count(x => x.Operation == PdfOperator.TextMatrix));
        Assert.Equal("office", Actual(drawing));
        using var document = Pdf.Create().AddPage(new PdfPageSize(400, 120));
        foreach (var entry in drawing.Fonts)
            document.Pages[0].SetFont(entry.Key, entry.Value);
        document.Pages[0].Content = drawing.Content;
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        Assert.Equal(6, reopened.Pages[0].ReadContent().Operations.Count(x => x.Operation == PdfOperator.ShowText));
        Assert.True(output.CanRead);
    }

    [Fact]
    public void CSharpAndVisualBasicConsumersComposeTheSameShapedText()
    {
        var fonts = new PdfFontFallback(Read("latin.ttf"));
        Assert.Equal("office", Actual(ContractExamples.CSharp.ContractExample.ShapeText("office", fonts, new PdfSimpleTextShaper())));
        Assert.Equal("office", Actual(ContractExamples.VisualBasic.ContractExample.ShapeText("office", fonts, new PdfSimpleTextShaper())));
    }

    [Fact]
    public void ExplicitFallbackSelectsACompleteGraphemeAndKeepsFontMetrics()
    {
        var latin = Read("latin.ttf");
        var original = FontTests.Read("original.ttf");
        var shaped = PdfText.Shape("AB", new(latin, original), Simple());
        Assert.Equal(2, shaped.Runs.Count);
        Assert.Same(latin, shaped.Runs[0].Request.Font);
        Assert.Same(original, shaped.Runs[1].Request.Font);
        Assert.Equal(original.GetGlyph('B').Advance, shaped.Runs[1].Glyphs[0].AdvanceX);
        Assert.Equal("Text.MissingClusterFont", Assert.Throws<PdfException>(() => PdfText.Shape("A\u0301", new(original), Simple())).Diagnostic.Code);
    }

    [Fact]
    public void ComplexScriptsMarksVerticalAndFeatureRequestsAreExplicitlyRejectedBySimpleShaper()
    {
        foreach (var item in new[]
        {
            ("arabic.ttf", "مرحبا"),
            ("devanagari.ttf", "नमस्ते"),
            ("latin.ttf", "A\u0301")
        }

        )
            Assert.Equal("Text.ComplexShaperRequired", Assert.Throws<PdfException>(() => PdfText.Shape(item.Item2, new(Read(item.Item1)), Simple())).Diagnostic.Code);
        var font = Read("latin.ttf");
        Assert.Equal("Text.ComplexShaperRequired", Assert.Throws<PdfException>(() => PdfText.Shape("fi", new(font), Simple(), new(features: new PdfTextFeature("liga", 0)))).Diagnostic.Code);
        Assert.Equal("Text.ComplexShaperRequired", Assert.Throws<PdfException>(() => PdfText.Shape("A", new(font), Simple(), new(vertical: true))).Diagnostic.Code);
    }

    [Fact]
    public void InvalidUnicodeCancellationAndBudgetsFailBeforeShaperExecution()
    {
        var font = Read("latin.ttf");
        var provider = new ProbeShaper();
        var registry = new PdfProviderRegistry(provider);
        Assert.Equal("Text.InvalidUnicode", Assert.Throws<PdfException>(() => PdfText.Shape("\ud800", new(font), registry)).Diagnostic.Code);
        Assert.Throws<OperationCanceledException>(() => PdfText.Shape("A", new(font), registry, cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfText.Shape("A", new(font), registry, policy: new(limits: new(maximumExpandedBytes: 1000)))).Diagnostic.Code);
        Assert.Equal(0, provider.Calls);
        Assert.Equal("Text.TabLayoutRequired", Assert.Throws<PdfException>(() => PdfText.Shape("A\tA", new(font), registry)).Diagnostic.Code);
    }

    [Fact]
    public void ProviderGlyphAndClusterResultsAreValidatedAndCopied()
    {
        var font = Read("latin.ttf");
        foreach (var glyph in new[]
        {
            new PdfShapedGlyph(0, 0, 1, 0),
            new PdfShapedGlyph(font.Info.GlyphCount, 0, 1, 0),
            new PdfShapedGlyph(1, 1, 1, 0)
        }

        )
        {
            var provider = new ProbeShaper(glyph);
            Assert.Equal("Text.ProviderResult", Assert.Throws<PdfException>(() => PdfText.Shape("A", new(font), new(provider))).Diagnostic.Code);
        }

        var valid = new ProbeShaper(new PdfShapedGlyph(font.GetGlyph('A').GlyphIndex, 0, 600, 0));
        var result = PdfText.Shape("A", new(font), new(valid));
        valid.Glyphs[0] = default;
        Assert.NotEqual(0, result.Runs[0].Glyphs[0].GlyphIndex);
    }

    [Fact]
    public void IndependentTextJobsShareImmutableFontsWithoutGlobalState()
    {
        var font = Read("latin.ttf");
        var registry = Simple();
        Parallel.For(0, 32, i =>
        {
            string text = i % 2 == 0 ? "office" : "fi";
            var result = PdfText.Shape(text, new(font), registry);
            var drawing = PdfTextDrawing.Create(result, 10, 40, 12);
            Assert.Equal(text, Actual(drawing));
        });
    }

    [Fact]
    public void ParagraphAndEmptyTextGeometryPreservesOriginalText()
    {
        var font = Read("latin.ttf");
        foreach (string text in new[]
        {
            "",
            "A\r\n\r\nA",
            "\u2066A\u2069"
        }

        )
        {
            var shaped = PdfText.Shape(text, new(font), Simple());
            var drawing = PdfTextDrawing.Create(shaped, 10, 50, 12);
            Assert.Equal(text, Actual(drawing));
        }

        Assert.Equal(3, PdfUnicode.Analyze("A\r\n\r\nA").Paragraphs.Count);
    }

    [Fact]
    public void OriginalShapingSourcePreservesFaceAndBorrowedStreamOwnership()
    {
        using var source = File.OpenRead(FontTests.Fixture("original.ttc"));
        var font = PdfFont.Read(source, new(ManagedFontProvider.Instance), 1);
        PolicyTests.Run(context =>
        {
            using var output = new MemoryStream();
            Assert.Equal(1, font.CopyShapingSource(output, context));
            Assert.Equal(source.Length, output.Length);
            Assert.True(output.CanWrite);
        });
    }

    internal static string Actual(PdfTextDrawing drawing)
    {
        var value = drawing.Content.Operations.Single(x => x.Operation == PdfOperator.BeginMarkedContentProperties).Operands[1].Entries["ActualText"];
        return Encoding.BigEndianUnicode.GetString(value.StringBytes.Skip(2).ToArray());
    }

    private sealed class ProbeShaper : IPdfTextShaper
    {
        internal ProbeShaper(params PdfShapedGlyph[] glyphs)
        {
            Glyphs = glyphs;
        }

        internal int Calls { get; private set; }
        internal PdfShapedGlyph[] Glyphs { get; }
        public PdfProviderDescriptor Descriptor { get; } = new("test.shaper", "1.0.0", PdfCapability.ShapeText);

        public IReadOnlyList<PdfShapedGlyph> Shape(PdfShapingRequest request, PdfOperationContext context)
        {
            context.Checkpoint();
            Calls++;
            return Glyphs;
        }
    }
}
