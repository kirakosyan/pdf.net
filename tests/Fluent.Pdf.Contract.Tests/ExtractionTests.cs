using System.IO;
using System.Text;
using System.Threading;
using Fluent.Pdf.Fonts;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ExtractionTests
{
    private static PdfValue D(params (string, PdfValue)[] pairs) => NavigationTests.D(pairs);
    private static PdfValue N(string value) => PdfValue.Name(value);
    private static PdfValue T(string value) => NavigationTests.T(value);
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    internal static PdfDocument Fixture(string content, Action<PdfTransaction, PdfObjectId, PdfObjectId, PdfObjectId>? extra = null, bool unicode = true) => NavigationTests.Fixture((edit, root, pages) =>
    {
        var descriptor = edit.Add(D(("Type", N("FontDescriptor")), ("FontName", N("Courier")), ("Flags", PdfValue.Integer(33)), ("Ascent", PdfValue.Integer(800)), ("Descent", PdfValue.Integer(-200)), ("FontBBox", PdfGraphicsNumbers.Array(0, -200, 600, 800))));
        var entries = new Dictionary<string, PdfValue>
        {
            {
                "Type",
                N("Font")
            },
            {
                "Subtype",
                N("Type1")
            },
            {
                "BaseFont",
                N("Courier")
            },
            {
                "Encoding",
                N("WinAnsiEncoding")
            },
            {
                "FirstChar",
                PdfValue.Integer(0)
            },
            {
                "LastChar",
                PdfValue.Integer(255)
            },
            {
                "Widths",
                PdfValue.Array(Enumerable.Repeat(PdfValue.Integer(600), 256).ToArray())
            },
            {
                "FontDescriptor",
                PdfValue.Reference(descriptor)
            }
        };
        if (unicode)
        {
            using var cmap = new MemoryStream(Encoding.ASCII.GetBytes("begincmap 1 begincodespacerange <00> <FF> endcodespacerange 1 beginbfrange <00> <7F> <0000> endbfrange 3 beginbfchar <80> <0531> <81> <D83DDE00> <82> <00660069> endbfchar endcmap"));
            entries["ToUnicode"] = PdfValue.Reference(edit.Add(PdfValues.Stream(D(), cmap)));
        }

        var font = edit.Add(PdfValue.Dictionary(entries));
        NavigationTests.Put(edit, pages[0], "Resources", D(("Font", D(("F1", PdfValue.Reference(font))))));
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(content));
        NavigationTests.Put(edit, pages[0], "Contents", PdfValue.Reference(edit.Add(PdfValues.Stream(D(), input))));
        extra?.Invoke(edit, root, pages[0], font);
    }, 1);
    [Fact]
    public void ToUnicodeClustersRawOrderAndAdvanceGeometryStayDetached()
    {
        using var document = Fixture("BT /F1 10 Tf 1 0 0 1 20 100 Tm <41808182> Tj ET");
        var result = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0);
        document.Dispose();
        Assert.Equal("AԱ😀fi", result.RawText);
        Assert.Equal(result.RawText, result.Text);
        Assert.Equal(4, result.Characters.Count);
        Assert.All(result.Characters, c => Assert.Equal(PdfTextMappingConfidence.ToUnicode, c.Confidence));
        var first = result.Characters[0];
        Assert.Equal(20, first.BaselineStart.X, 8);
        Assert.Equal(300, first.BaselineStart.Y, 8);
        Assert.Equal(26, first.BaselineEnd.X, 8);
        Assert.Equal(6, first.Quad.Bounds.Width, 8);
        Assert.Equal(10, first.Quad.Bounds.Height, 8);
        Assert.Equal(20, first.UserQuad.Bounds.X, 8);
        Assert.Equal(98, first.UserQuad.Bounds.Y, 8);
        Assert.Equal("fi", result.Characters[3].Text);
        Assert.True(result.OperationReport.Succeeded);
    }

    [Fact]
    public void TextSpacingScalingTjAdjustmentsAndQuoteOperatorsFollowPdfPositioning()
    {
        using var document = Fixture("BT /F1 10 Tf 1 Tc 7 Tw 50 Tz 12 TL 1 0 0 1 20 100 Tm (A B) Tj [(C) 200 (D)] TJ (E) ' 2 3 (F) \" ET");
        var result = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0);
        Assert.Equal("A BCDEF", result.RawText);
        Assert.Equal(23.5, result.Characters[1].BaselineStart.X, 8);
        Assert.Equal(30.5, result.Characters[2].BaselineStart.X, 8);
        Assert.Equal(34, result.Characters[3].BaselineStart.X, 8);
        Assert.Equal(36.5, result.Characters[4].BaselineStart.X, 8);
        Assert.Equal(20, result.Characters[5].BaselineStart.X, 8);
        Assert.Equal(312, result.Characters[5].BaselineStart.Y, 8);
        Assert.Equal(324, result.Characters[6].BaselineStart.Y, 8);
        Assert.Contains('\n', result.Text);
        Assert.True(result.Words.Count >= 3);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(90)]
    [InlineData(180)]
    [InlineData(270)]
    public void DisplayCoordinatesHonorCropRotationAndUserUnit(int rotation)
    {
        using var source = Fixture("BT /F1 10 Tf 1 0 0 1 20 100 Tm (A) Tj ET", (edit, _, page, _) =>
        {
            NavigationTests.Put(edit, page, "CropBox", PdfGraphicsNumbers.Array(10, 20, 210, 320));
            NavigationTests.Put(edit, page, "Rotate", PdfValue.Integer(rotation));
            NavigationTests.Put(edit, page, "UserUnit", PdfValue.Integer(2));
        });
        var result = PdfTextExtraction.ReadPage(source.CreateSnapshot(), 0);
        var expected = result.Geometry.UserToDisplay().Transform(new(20, 100));
        var character = Assert.Single(result.Characters);
        Assert.Equal(expected.X, character.BaselineStart.X, 8);
        Assert.Equal(expected.Y, character.BaselineStart.Y, 8);
        Assert.Equal(20, character.FontSize, 8);
        Assert.Equal(20, character.UserQuad.Bounds.X, 8);
    }

    [Fact]
    public void RawWordOrderIsSeparateFromInferredLinesAndParagraphs()
    {
        using var document = Fixture("BT /F1 10 Tf 1 0 0 1 20 200 Tm (Bottom) Tj 1 0 0 1 20 300 Tm (Top) Tj ET");
        var result = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0);
        Assert.Equal("BottomTop", result.RawText);
        Assert.Equal("Bottom\nTop", result.Text);
        Assert.Equal("Bottom", result.Words[0].Text);
        Assert.Equal("Top", result.Lines[0].Words[0].Text);
        Assert.Equal("Top\nBottom", result.LayoutText);
        Assert.Equal(2, result.Paragraphs.Count);
    }

    [Fact]
    public void SourceActualTextOverridesLogicalTextWithoutInventingClusterGeometry()
    {
        string logical = "Հայերեն العربية 😀";
        string hex = BitConverter.ToString(new byte[] { 254, 255 }.Concat(Encoding.BigEndianUnicode.GetBytes(logical)).ToArray()).Replace("-", "");
        using var document = Fixture("/Span << /ActualText <" + hex + "> >> BDC BT /F1 10 Tf 1 0 0 1 20 100 Tm (visual) Tj ET EMC");
        var result = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0);
        Assert.Equal("visual", result.RawText);
        Assert.Equal(logical, result.Text);
        var span = Assert.Single(result.ActualTextSpans);
        Assert.Equal(6, span.CharacterCount);
        Assert.NotNull(span.Bounds);
    }

    [Fact]
    public void UnmappedUnicodeIsRejectedOrExplicitlySubstitutedWithoutClaimingCertainty()
    {
        using var document = Fixture("BT /F1 10 Tf <83> Tj ET");
        var snapshot = document.CreateSnapshot();
        Assert.Equal("Font.UnmappedCode", Error(() => PdfTextExtraction.ReadPage(snapshot, 0)));
        var result = PdfTextExtraction.ReadPage(snapshot, 0, policy: new(PdfProcessingMode.Permissive));
        Assert.Equal("\ufffd", result.Text);
        Assert.Equal(PdfTextMappingConfidence.Unmapped, Assert.Single(result.Characters).Confidence);
        Assert.Contains(result.OperationReport.Diagnostics, d => d.Code == "Font.UnmappedCode");
    }

    [Fact]
    public void ExplicitSimpleEncodingProviderIsReportedAsInferred()
    {
        using var document = Fixture("BT /F1 10 Tf (ABC) Tj ET", unicode: false);
        Assert.Equal("Extraction.EncodingProviderRequired", Error(() => PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0)));
        var result = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0, providers: new(ManagedFontProvider.Instance));
        Assert.Equal("ABC", result.Text);
        Assert.All(result.Characters, c => Assert.Equal(PdfTextMappingConfidence.Inferred, c.Confidence));
        Assert.Contains(result.OperationReport.Diagnostics, d => d.Code == "Font.UnicodeInferred");
    }

    [Fact]
    public void StandardWidthsUseEncodedGlyphIdentityRatherThanToUnicodeReplacement()
    {
        using var document = Fixture("BT /F1 10 Tf (A) Tj ET", (edit, _, _, font) =>
        {
            var entries = edit.Resolve(font).Entries.ToDictionary(p => p.Key, p => p.Value);
            entries.Remove("Widths");
            entries.Remove("FirstChar");
            entries.Remove("LastChar");
            entries["BaseFont"] = N("Helvetica");
            using var cmap = new MemoryStream(Encoding.ASCII.GetBytes("begincmap 1 begincodespacerange <00> <FF> endcodespacerange 1 beginbfchar <41> <0057> endbfchar endcmap"));
            entries["ToUnicode"] = PdfValue.Reference(edit.Add(PdfValues.Stream(D(), cmap)));
            edit.Set(font, PdfValue.Dictionary(entries));
        });
        var result = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0, providers: new(ManagedFontProvider.Instance));
        var character = Assert.Single(result.Characters);
        Assert.Equal("W", character.Text);
        Assert.Equal(6.67, character.BaselineEnd.X - character.BaselineStart.X, 8);
    }

    [Fact]
    public void FormTransformsAndGraphicStateRestorationRemainLocal()
    {
        using var document = Fixture("q 2 0 0 2 10 20 cm /Fm Do Q BT /F1 10 Tf (B) Tj ET", (edit, _, page, font) =>
        {
            using var bytes = new MemoryStream(Encoding.ASCII.GetBytes("BT /F1 10 Tf 1 0 0 1 5 6 Tm (A) Tj ET"));
            var form = edit.Add(PdfValues.Stream(D(("Type", N("XObject")), ("Subtype", N("Form")), ("BBox", PdfGraphicsNumbers.Array(0, 0, 100, 100)), ("Matrix", PdfGraphicsNumbers.Array(1, 0, 0, 1, 3, 4)), ("Resources", D(("Font", D(("F1", PdfValue.Reference(font))))))), bytes));
            var resources = edit.Resolve(page).Entries["Resources"].Entries.ToDictionary(p => p.Key, p => p.Value);
            resources["XObject"] = D(("Fm", PdfValue.Reference(form)));
            NavigationTests.Put(edit, page, "Resources", PdfValue.Dictionary(resources));
        });
        var result = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0);
        Assert.Equal("AB", result.RawText);
        Assert.Equal(26, result.Characters[0].BaselineStart.X, 8);
        Assert.Equal(360, result.Characters[0].BaselineStart.Y, 8);
        Assert.Equal(0, result.Characters[1].BaselineStart.X, 8);
        Assert.Equal(10, result.Characters[1].FontSize, 8);
    }

    [Fact]
    public void OptionalVisibilityAndNonpaintingTextAreExplicitSelections()
    {
        using var original = Fixture("BT /F1 10 Tf /OC /Hide BDC (hidden) Tj EMC 1 0 0 1 50 100 Tm 3 Tr (ocr) Tj ET", (edit, root, page, _) =>
        {
            var group = edit.Add(D(("Type", N("OCG")), ("Name", T("Hidden"))));
            NavigationTests.Put(edit, root, "OCProperties", D(("OCGs", PdfValue.Array(PdfValue.Reference(group))), ("D", D(("OFF", PdfValue.Array(PdfValue.Reference(group)))))));
            var resources = edit.Resolve(page).Entries["Resources"].Entries.ToDictionary(p => p.Key, p => p.Value);
            resources["Properties"] = D(("Hide", PdfValue.Reference(group)));
            NavigationTests.Put(edit, page, "Resources", PdfValue.Dictionary(resources));
        });
        var normal = PdfTextExtraction.ReadPage(original.CreateSnapshot(), 0);
        Assert.Equal("ocr", normal.RawText);
        Assert.All(normal.Characters, c => Assert.Equal(3, c.RenderingMode));
        var all = PdfTextExtraction.ReadPage(original.CreateSnapshot(), 0, new(includeHiddenLayers: true));
        Assert.Equal("hiddenocr", all.RawText);
        Assert.Contains(all.Characters, c => !c.LayerVisible);
        Assert.Empty(PdfTextExtraction.ReadPage(original.CreateSnapshot(), 0, new(includeInvisibleText: false)).Characters);
    }

    [Fact]
    public void GeneratedEmbeddedMultilingualFontsExtractWithoutOptionalMachineFontLookup()
    {
        using var font = File.OpenRead(FontTests.Fixture("original.ttf"));
        using var output = new MemoryStream();
        ContractExamples.CSharp.ContractExample.WriteFontPdf(font, output, "A一😀");
        output.Position = 0;
        using var document = Pdf.Open(output);
        var result = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0);
        Assert.Equal("A一😀", result.Text);
        Assert.Equal(3, result.Characters.Count);
        Assert.All(result.Characters, c => Assert.Equal(PdfTextMappingConfidence.ToUnicode, c.Confidence));
    }

    [Fact]
    public void IdentityVerticalCidMetricsUseW2AndNeverApplySingleByteWordSpacing()
    {
        using var document = Fixture("BT /F1 10 Tf 20 Tw 1 0 0 1 20 300 Tm <00200021> Tj ET", (edit, _, _, font) =>
        {
            var descriptor = edit.Resolve(font).Entries["FontDescriptor"];
            var widths = PdfValue.Array(PdfValue.Integer(32), PdfValue.Array(PdfValue.Integer(600), PdfValue.Integer(700)));
            var vertical = PdfValue.Array(PdfValue.Integer(32), PdfValue.Array(PdfValue.Integer(-1200), PdfValue.Integer(300), PdfValue.Integer(900)));
            var descendant = edit.Add(D(("Type", N("Font")), ("Subtype", N("CIDFontType2")), ("BaseFont", N("OriginalVertical")), ("FontDescriptor", descriptor), ("W", widths), ("W2", vertical)));
            using var bytes = new MemoryStream(Encoding.ASCII.GetBytes("begincmap 1 begincodespacerange <0000> <FFFF> endcodespacerange 2 beginbfchar <0020> <0041> <0021> <0042> endbfchar endcmap"));
            var cmap = edit.Add(PdfValues.Stream(D(), bytes));
            edit.Set(font, D(("Type", N("Font")), ("Subtype", N("Type0")), ("BaseFont", N("OriginalVertical")), ("Encoding", N("Identity-V")), ("DescendantFonts", PdfValue.Array(PdfValue.Reference(descendant))), ("ToUnicode", PdfValue.Reference(cmap))));
        });
        var result = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0);
        Assert.Equal("AB", result.RawText);
        Assert.Equal(100, result.Characters[0].BaselineStart.Y, 8);
        Assert.Equal(112, result.Characters[1].BaselineStart.Y, 8);
        Assert.Equal(17, result.Characters[0].UserQuad.Bounds.X, 8);
    }

    [Fact]
    public void PartialActualTextVisibilityRejectsOrReportsTheLostReplacement()
    {
        using var document = Fixture("/Span << /ActualText (all logical text) >> BDC BT /F1 10 Tf (visible) Tj /OC /Hide BDC (hidden) Tj EMC ET EMC", (edit, root, page, _) =>
        {
            var group = edit.Add(D(("Type", N("OCG")), ("Name", T("Hidden"))));
            NavigationTests.Put(edit, root, "OCProperties", D(("OCGs", PdfValue.Array(PdfValue.Reference(group))), ("D", D(("OFF", PdfValue.Array(PdfValue.Reference(group)))))));
            var resources = edit.Resolve(page).Entries["Resources"].Entries.ToDictionary(p => p.Key, p => p.Value);
            resources["Properties"] = D(("Hide", PdfValue.Reference(group)));
            NavigationTests.Put(edit, page, "Resources", PdfValue.Dictionary(resources));
        });
        Assert.Equal("Extraction.ActualTextPartialVisibility", Error(() => PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0)));
        var permissive = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0, policy: new(PdfProcessingMode.Permissive));
        Assert.Equal("visible", permissive.Text);
        Assert.Empty(permissive.ActualTextSpans);
        Assert.Contains(permissive.OperationReport.Diagnostics, d => d.Code == "Extraction.ActualTextPartialVisibility");
        Assert.Equal("all logical text", PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0, new(includeHiddenLayers: true)).Text);
    }

    [Fact]
    public void HiddenUnmappedCodesStillAdvanceVisibleTextWithoutInventingUnicode()
    {
        using var document = Fixture("BT /F1 10 Tf /OC /Hide BDC <83> Tj EMC (A) Tj ET", (edit, root, page, _) =>
        {
            var group = edit.Add(D(("Type", N("OCG")), ("Name", T("Hidden"))));
            NavigationTests.Put(edit, root, "OCProperties", D(("OCGs", PdfValue.Array(PdfValue.Reference(group))), ("D", D(("OFF", PdfValue.Array(PdfValue.Reference(group)))))));
            var resources = edit.Resolve(page).Entries["Resources"].Entries.ToDictionary(p => p.Key, p => p.Value);
            resources["Properties"] = D(("Hide", PdfValue.Reference(group)));
            NavigationTests.Put(edit, page, "Resources", PdfValue.Dictionary(resources));
        });
        var result = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0);
        Assert.Equal("A", result.Text);
        Assert.Equal(6, Assert.Single(result.Characters).BaselineStart.X, 8);
    }

    [Fact]
    public void CSharpAndVisualBasicConsumersExtractTextFromBorrowedInputs()
    {
        using var source = Fixture("BT /F1 10 Tf (Example text) Tj ET");
        using var output = new MemoryStream();
        source.Save(output);
        foreach (var read in new Func<Stream, string>[]
        {
            ContractExamples.CSharp.ContractExample.ExtractPageText,
            ContractExamples.VisualBasic.ContractExample.ExtractPageText
        }

        )
        {
            output.Position = 0;
            Assert.Equal("Example text", read(output));
            Assert.True(output.CanRead);
        }
    }

    [Fact]
    public void FiniteSourceNumbersThatOverflowGeometryFailWithAStableDiagnostic()
    {
        string enormous = "1" + new string ('0', 308);
        using var source = Fixture(enormous + " 0 0 " + enormous + " 0 0 cm BT /F1 10 Tf (A) Tj ET");
        Assert.Equal("Extraction.GeometryRange", Error(() => PdfTextExtraction.ReadPage(source.CreateSnapshot(), 0)));
    }

    [Fact]
    public void CancellationAndResourceLimitsCannotReturnPartialResults()
    {
        using var document = Fixture("BT /F1 10 Tf (ABC) Tj ET");
        var snapshot = document.CreateSnapshot();
        Assert.Throws<OperationCanceledException>(() => PdfTextExtraction.ReadPage(snapshot, 0, cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.Objects", Error(() => PdfTextExtraction.ReadPage(snapshot, 0, policy: new(limits: new(maximumObjects: 2)))));
        Assert.Equal("Limit.ExpandedBytes", Error(() => PdfTextExtraction.ReadPage(snapshot, 0, policy: new(limits: new(maximumExpandedBytes: 4096)))));
        Assert.Equal("ABC", PdfTextExtraction.ReadPage(snapshot, 0).Text);
    }
}
