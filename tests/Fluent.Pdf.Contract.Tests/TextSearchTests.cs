using Xunit;
using System.Threading;

namespace Fluent.Pdf.Contract.Tests;
public sealed class TextSearchTests
{
    private static PdfProviderRegistry Providers() => new(new PdfSimpleTextShaper());
    private static PdfFontFallback Fonts() => new(FontTests.Read("original.ttf"));
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    [Fact]
    public void LiteralAndRegexCrossRunsRetainClustersAndStyleRegionFilters()
    {
        using var doc = ExtractionTests.Fixture("BT /F1 10 Tf 20 100 Td (AB) Tj [(CD) 20 (EF)] TJ <82> Tj ET");
        var source = doc.CreateSnapshot();
        var literal = PdfTextSearch.FindPage(source, 0, new("BCDE"));
        var match = Assert.Single(literal.Matches);
        Assert.Equal(1, match.Start);
        Assert.Equal(4, match.Characters.Count);
        Assert.False(match.IsPartialCluster);
        Assert.Single(PdfTextSearch.FindPage(source, 0, new("b.*e", regex: true, ignoreCase: true)).Matches);
        Assert.Empty(PdfTextSearch.FindPage(source, 0, new("BC", fontName: "Other")).Matches);
        Assert.Single(PdfTextSearch.FindPage(source, 0, new("BC", fontName: "Courier", minimumFontSize: 10, maximumFontSize: 10, region: new(0, 290, 100, 20))).Matches);
        Assert.Empty(PdfTextSearch.FindPage(source, 0, new("BC", region: new(0, 0, 1, 1))).Matches);
        Assert.True(Assert.Single(PdfTextSearch.FindPage(source, 0, new("f")).Matches).IsPartialCluster);
        Assert.False(Assert.Single(PdfTextSearch.FindPage(source, 0, new("fi")).Matches).IsPartialCluster);
    }

    [Fact]
    public void ActualTextAndEmptyRegexMatchesKeepHonestGeometry()
    {
        using var doc = ExtractionTests.Fixture("/Span << /ActualText (logical) >> BDC BT /F1 10 Tf 20 100 Td (AB) Tj ET EMC");
        var result = PdfTextSearch.FindPage(doc.CreateSnapshot(), 0, new("logical"));
        Assert.Equal("logical", result.Text);
        var match = Assert.Single(result.Matches);
        Assert.True(match.UsesActualText);
        Assert.Equal(2, match.Characters.Count);
        Assert.True(Assert.Single(PdfTextSearch.FindPage(doc.CreateSnapshot(), 0, new("og")).Matches).IsPartialCluster);
        Assert.Equal(8, PdfTextSearch.FindPage(doc.CreateSnapshot(), 0, new("(?=)", regex: true)).Matches.Count);
    }

    [Fact]
    public void MatchLimitRegexDeadlineInvalidPatternCancellationAndBudgetsFailExplicitly()
    {
        using var doc = ExtractionTests.Fixture("BT /F1 10 Tf (AAAA) Tj ET");
        var source = doc.CreateSnapshot();
        Assert.Equal("Search.MatchLimit", Error(() => PdfTextSearch.FindPage(source, 0, new("A", maximumMatches: 3))));
        Assert.Equal("Search.Pattern", Error(() => PdfTextSearch.FindPage(source, 0, new("[", regex: true))));
        Assert.Throws<OperationCanceledException>(() => PdfTextSearch.FindPage(source, 0, new("A"), cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.Objects", Error(() => PdfTextSearch.FindPage(source, 0, new("A"), policy: new(limits: new(maximumObjects: 1)))));
        Assert.Equal("Limit.ExpandedBytes", Error(() => PdfTextSearch.FindPage(source, 0, new("A"), policy: new(limits: new(maximumExpandedBytes: 1)))));
        using var denial = ExtractionTests.Fixture("BT /F1 10 Tf (" + new string ('A', 10000) + "!) Tj ET");
        Assert.Equal("Search.RegexTimeout", Error(() => PdfTextSearch.FindPage(denial.CreateSnapshot(), 0, new("^(A+)+$", regex: true, regexTimeoutMilliseconds: 10))));
    }

    [Fact]
    public void ReplacementRemovesCrossRunBytesAndPreservesUnrelatedPositionsAndSource()
    {
        using var doc = ExtractionTests.Fixture("BT /F1 10 Tf 1 Tc 20 100 Td (ABC) Tj [(DEF) 20 (G)] TJ ET");
        var source = doc.CreateSnapshot();
        var original = PdfTextExtraction.ReadPage(source, 0);
        using var edited = PdfTextSearch.ReplacePage(source, 0, new("BCDE"), "AB", Fonts(), Providers(), new(10));
        var after = PdfTextExtraction.ReadPage(edited.CreateSnapshot(), 0);
        Assert.Equal("AFGAB", after.RawText);
        Assert.Equal("ABCDEFG", PdfTextExtraction.ReadPage(source, 0).RawText);
        foreach (var letter in new[]
        {
            "A",
            "F",
            "G"
        }

        )
        {
            var before = original.Characters.First(c => c.Text == letter);
            var kept = after.Characters.First(c => c.Text == letter);
            Assert.Equal(before.BaselineStart.X, kept.BaselineStart.X, 7);
            Assert.Equal(before.BaselineStart.Y, kept.BaselineStart.Y, 7);
        }

        using var bytes = new MemoryStream();
        edited.Save(bytes);
        bytes.Position = 0;
        using var reopened = Pdf.Open(bytes);
        Assert.Equal("AFGAB", PdfTextExtraction.ReadPage(reopened.CreateSnapshot(), 0).RawText);
    }

    [Fact]
    public void OverflowMissingGlyphAndExplicitShrinkAreTransactional()
    {
        using var doc = ExtractionTests.Fixture("BT /F1 10 Tf 20 100 Td (ABC) Tj ET");
        var source = doc.CreateSnapshot();
        Assert.Equal("Search.ReplacementOverflow", Error(() => PdfTextSearch.ReplacePage(source, 0, new("A"), "BBBB", Fonts(), Providers(), new(10))));
        Assert.Equal("Text.MissingClusterFont", Error(() => PdfTextSearch.ReplacePage(source, 0, new("A"), "Z", Fonts(), Providers(), new(10))));
        using var shrunk = PdfTextSearch.ReplacePage(source, 0, new("A"), "BBBB", Fonts(), Providers(), new(10, PdfTextReplacementOverflow.ShrinkToFit));
        Assert.Equal("BCBBBB", PdfTextExtraction.ReadPage(shrunk.CreateSnapshot(), 0).RawText);
        Assert.Contains(shrunk.LastOperationReport!.Diagnostics, d => d.Code == "Search.ReplacementScaled");
        Assert.Equal("ABC", PdfTextExtraction.ReadPage(source, 0).RawText);
    }

    [Fact]
    public void LigatureBoundariesAccessibilityAndParagraphReflowAreExplicit()
    {
        using var ligature = ExtractionTests.Fixture("BT /F1 10 Tf 20 100 Td <82> Tj ET");
        Assert.Equal("Search.ClusterReplacementProfile", Error(() => PdfTextSearch.ReplacePage(ligature.CreateSnapshot(), 0, new("f"), "A", Fonts(), Providers(), new(10))));
        using var complete = PdfTextSearch.ReplacePage(ligature.CreateSnapshot(), 0, new("fi"), "A", Fonts(), Providers(), new(10));
        Assert.Equal("A", PdfTextExtraction.ReadPage(complete.CreateSnapshot(), 0).Text);
        using var actual = ExtractionTests.Fixture("/Span << /ActualText (logical) >> BDC BT /F1 10 Tf (AB) Tj ET EMC");
        Assert.Equal("Search.ClusterReplacementProfile", Error(() => PdfTextSearch.ReplacePage(actual.CreateSnapshot(), 0, new("logical"), "A", Fonts(), Providers(), new(10))));
        Assert.Throws<ArgumentException>(() => PdfTextSearch.ReplacePage(ligature.CreateSnapshot(), 0, new("fi"), "A\nB", Fonts(), Providers(), new(10)));
    }

    [Fact]
    public void QuoteOperatorsAndDeletionPreserveTextState()
    {
        using var doc = ExtractionTests.Fixture("BT /F1 10 Tf 10 TL 20 100 Td (AB) Tj (CD) ' 2 3 (EF) \" (G) Tj ET");
        var original = PdfTextExtraction.ReadPage(doc.CreateSnapshot(), 0);
        using var edited = PdfTextSearch.ReplacePage(doc.CreateSnapshot(), 0, new("BCDE"), "", Fonts(), Providers(), new(10));
        var after = PdfTextExtraction.ReadPage(edited.CreateSnapshot(), 0);
        Assert.Equal("AFG", after.RawText);
        foreach (var c in after.Characters)
        {
            var previous = original.Characters.Single(x => x.Text == c.Text);
            Assert.Equal(previous.BaselineStart.X, c.BaselineStart.X, 7);
            Assert.Equal(previous.BaselineStart.Y, c.BaselineStart.Y, 7);
        }
    }

    [Fact]
    public void CrossStreamReplacementKeepsUnrelatedAccessibilityAndGeometry()
    {
        using var doc = ExtractionTests.Fixture("", (edit, root, page, font) =>
        {
            var streams = new List<PdfValue>();
            foreach (var text in new[]
            {
                "BT /F1 10 Tf 20 100 Td (AB) Tj",
                "(CD) Tj ET /Span << /ActualText (logical) >> BDC BT /F1 10 Tf 20 50 Td (EF) Tj ET EMC"
            }

            )
            {
                using var input = new MemoryStream(System.Text.Encoding.ASCII.GetBytes(text));
                streams.Add(PdfValue.Reference(edit.Add(PdfValues.Stream(NavigationTests.D(), input))));
            }

            NavigationTests.Put(edit, page, "Contents", PdfValue.Array(streams.ToArray()));
        });
        using var edited = PdfTextSearch.ReplacePage(doc.CreateSnapshot(), 0, new("BC"), "A", Fonts(), Providers(), new(10));
        Assert.Equal("ADlogicalA", PdfTextSearch.FindPage(edited.CreateSnapshot(), 0, new("logical")).Text);
        Assert.Single(PdfTextExtraction.ReadPage(edited.CreateSnapshot(), 0).ActualTextSpans, s => s.Text == "logical");
        Assert.Equal("ABCDlogical", PdfTextSearch.FindPage(doc.CreateSnapshot(), 0, new("logical")).Text);
    }

    [Fact]
    public void FormSearchWorksAndUnsupportedFormOrTaggedEditsReject()
    {
        using var doc = ExtractionTests.Fixture("/Fm Do BT /F1 10 Tf (B) Tj ET", (edit, root, page, font) =>
        {
            using var bytes = new MemoryStream(System.Text.Encoding.ASCII.GetBytes("BT /F1 10 Tf (A) Tj ET"));
            var form = edit.Add(PdfValues.Stream(NavigationTests.D(("Type", PdfValue.Name("XObject")), ("Subtype", PdfValue.Name("Form")), ("BBox", PdfGraphicsNumbers.Array(0, 0, 100, 100)), ("Resources", NavigationTests.D(("Font", NavigationTests.D(("F1", PdfValue.Reference(font))))))), bytes));
            var resources = edit.Resolve(page).Entries["Resources"].Entries.ToDictionary(p => p.Key, p => p.Value);
            resources["XObject"] = NavigationTests.D(("Fm", PdfValue.Reference(form)));
            NavigationTests.Put(edit, page, "Resources", PdfValue.Dictionary(resources));
        });
        Assert.Single(PdfTextSearch.FindPage(doc.CreateSnapshot(), 0, new("AB")).Matches);
        Assert.Equal("Search.FormReplacementProfile", Error(() => PdfTextSearch.ReplacePage(doc.CreateSnapshot(), 0, new("AB"), "A", Fonts(), Providers(), new(10))));
        using var tagged = ExtractionTests.Fixture("BT /F1 10 Tf (A) Tj ET", (edit, root, page, font) => NavigationTests.Put(edit, root, "StructTreeRoot", NavigationTests.D()));
        Assert.Equal("Structure.SourceEditPolicy", Error(() => PdfTextSearch.ReplacePage(tagged.CreateSnapshot(), 0, new("A"), "B", Fonts(), Providers(), new(10))));
    }

    [Fact]
    public void BothConsumersAndReplacementBoundariesUseThePublicContracts()
    {
        using var doc = ExtractionTests.Fixture("BT /F1 10 Tf 20 100 Td (AB) Tj ET");
        var source = doc.CreateSnapshot();
        Assert.Single(ContractExamples.CSharp.ContractExample.Search(source, "AB").Matches);
        Assert.Single(ContractExamples.VisualBasic.ContractExample.Search(source, "AB").Matches);
        Assert.Throws<OperationCanceledException>(() => PdfTextSearch.ReplacePage(source, 0, new("A"), "B", Fonts(), Providers(), new(10), cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.OutputBytes", Error(() => PdfTextSearch.ReplacePage(source, 0, new("A"), "B", Fonts(), Providers(), new(10), policy: new(limits: new(maximumOutputBytes: 1)))));
        Assert.Equal("AB", PdfTextExtraction.ReadPage(source, 0).Text);
    }
}
