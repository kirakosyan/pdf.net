using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class OptimizationTests
{
    private static readonly byte[] Pixels =
    {
        240,
        20,
        10,
        10,
        230,
        30,
        10,
        20,
        240,
        80,
        100,
        120
    };
    private static readonly byte[] Alpha =
    {
        0,
        64,
        128,
        255
    };
    private static readonly byte[] Attachment = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("<original>Literal &amp; owned attachment.</original>\n", 100)));
    private static byte[] Fixture(string? extraContent = null, bool identitySensitive = false, bool nestedResources = false)
    {
        using var f = new SyntaxFixtures();
        var entries = new List<(int, int, long, bool)>
        {
            (0, 65535, 0, true)
        };
        void Object(int id, string body) => entries.Add((id, 0, f.Object(id, body), false));
        void Stream(int id, string dict, byte[] data) => entries.Add((id, 0, f.StreamObject(id, dict, data), false));
        Object(1, "<< /Type /Catalog /Pages 2 0 R /Names << /EmbeddedFiles << /Names [(note.xml) 9 0 R] >> >> /AF [9 0 R] >>");
        Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << /XObject << /ImA 5 0 R /ImB 6 0 R /Unused 7 0 R " + (nestedResources ? "/Fm 30 0 R " : "") + ">> /Font << /F1 20 0 R /F2 21 0 R /UnusedFont 8 0 R " + (nestedResources ? "/F3 32 0 R " : "") + ">> /Properties << /UnusedProperty 16 0 R >> /ColorSpace << /UnusedCS /DeviceRGB /DefaultRGB /DeviceRGB >> >> /Contents 4 0 R >>");
        string program = string.Concat(Enumerable.Repeat("% Original bounded content padding.\n", 100)) + "q 50 0 0 100 0 0 cm /ImA Do Q q 50 0 0 100 50 0 cm /ImB Do Q BT /F1 10 Tf 1 0 0 1 5 5 Tm (A) Tj /F2 10 Tf (A) Tj ET\n" + (extraContent ?? "");
        if (nestedResources)
            program += "q /Fm Do Q BT /F3 10 Tf 1 0 0 1 20 20 Tm (A) Tj ET";
        Stream(4, "", Encoding.ASCII.GetBytes(program));
        Stream(5, "/Type /XObject /Subtype /Image /Width 2 /Height 2 /BitsPerComponent 8 /ColorSpace /DeviceRGB /SMask 11 0 R" + (identitySensitive ? " /StructParent 100" : ""), Pixels);
        Stream(6, "/Type /XObject /Subtype /Image /Width 2 /Height 2 /BitsPerComponent 8 /ColorSpace /DeviceRGB /SMask 12 0 R" + (identitySensitive ? " /StructParent 101" : ""), Pixels);
        Stream(7, "/Type /XObject /Subtype /Image /Width 2 /Height 2 /BitsPerComponent 8 /ColorSpace /DeviceRGB", Pixels);
        Object(8, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        Object(9, "<< /Type /Filespec /F (note.xml) /UF (note.xml) /AFRelationship /Data /EF << /F 10 0 R /UF 10 0 R >> >>");
        Stream(10, "/Type /EmbeddedFile /Subtype /application#2fxml", Attachment);
        Stream(11, "/Type /XObject /Subtype /Image /Width 2 /Height 2 /BitsPerComponent 8 /ColorSpace /DeviceGray", Alpha);
        Stream(12, "/Type /XObject /Subtype /Image /Width 2 /Height 2 /BitsPerComponent 8 /ColorSpace /DeviceGray", Alpha);
        Stream(15, "/Type /OriginalOrphan", new byte[5000]);
        Object(16, "<< /Name /OriginalUnusedProperty >>");
        Object(20, "<< /Type /Font /Subtype /TrueType /BaseFont /Original /FirstChar 65 /LastChar 65 /Widths [600] /Encoding /WinAnsiEncoding /FontDescriptor 22 0 R >>");
        Object(21, "<< /Type /Font /Subtype /TrueType /BaseFont /Original /FirstChar 65 /LastChar 65 /Widths [600] /Encoding /WinAnsiEncoding /FontDescriptor 23 0 R >>");
        const string descriptor = "/Type /FontDescriptor /FontName /Original /Flags 32 /FontBBox [-50 -200 1000 800] /Ascent 800 /Descent -200 /CapHeight 700 /ItalicAngle 0 /StemV 80 /FontFile2 ";
        Object(22, "<< " + descriptor + "24 0 R >>");
        Object(23, "<< " + descriptor + "25 0 R >>");
        var font = File.ReadAllBytes(FontTests.Fixture("original.ttf"));
        Stream(24, "/Length1 " + font.Length, font);
        Stream(25, "/Length1 " + font.Length, font);
        if (nestedResources)
        {
            Stream(30, "/Type /XObject /Subtype /Form /BBox [0 0 100 100] /Resources << /XObject << /ImOnly 5 0 R /Unused 7 0 R >> >>", Encoding.ASCII.GetBytes("q 10 0 0 10 0 0 cm /ImOnly Do Q"));
            Object(32, "<< /Type /Font /Subtype /Type3 /Name /OriginalType3 /FontBBox [0 0 100 100] /FontMatrix [0.001 0 0 0.001 0 0] /FirstChar 65 /LastChar 65 /Widths [500] /Encoding << /Type /Encoding /Differences [65 /A] >> /CharProcs << /A 33 0 R >> /Resources << /XObject << /ImGlyph 5 0 R /Unused 7 0 R >> >> >>");
            Stream(33, "", Encoding.ASCII.GetBytes("500 0 d0 q 50 0 0 50 0 0 cm /ImGlyph Do Q"));
        }

        f.Classic(entries.OrderBy(x => x.Item1).ToArray(), "/Root 1 0 R", nestedResources ? 34 : 26);
        return f.Bytes.ToArray();
    }

    private static PdfDocument Open(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        return Pdf.Open(input);
    }

    private static byte[] Decode(PdfSyntaxDocument syntax, int id) => SignatureTests.Bytes(syntax.DecodeStream(new(id)).OpenRead());
    private static void Save(string name, byte[] bytes)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "optimization-fixtures");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), bytes);
    }

    [Fact]
    public void LosslessOptimizationSharesExactImagesMasksAndFontProgramsAndRetainsAssociatedXml()
    {
        var original = Fixture();
        using var source = Open(original);
        var result = PdfOptimization.Optimize(source.CreateSnapshot());
        Assert.True(result.OperationReport.Succeeded);
        Assert.True(result.SavedBytes > 0);
        Assert.True(result.SharedStreams >= 3);
        Assert.True(result.CompressedStreams > 0);
        Assert.True(result.RemovedObjects > 0);
        Assert.Equal(0, result.DownsampledImages);
        Assert.Equal(0, result.RemovedResourceBindings);
        Assert.Equal(result.OutputSha256, ContractExamples.CSharp.ContractExample.OptimizePdf(source.CreateSnapshot()).OutputSha256);
        Assert.Equal(result.OutputSha256, ContractExamples.VisualBasic.ContractExample.OptimizePdf(source.CreateSnapshot()).OutputSha256);
        using var bytes = result.OpenPdfStream();
        var graph = PdfSyntax.Read(bytes);
        Assert.Equal(Pixels, Decode(graph, 5));
        Assert.Equal(Alpha, Decode(graph, 11));
        Assert.Equal(Attachment, Decode(graph, 10));
        Assert.Equal(PdfValueKind.Null, graph.Resolve(new(6)).Kind);
        Assert.Equal(PdfValueKind.Null, graph.Resolve(new(12)).Kind);
        Assert.Equal(PdfValueKind.Null, graph.Resolve(new(25)).Kind);
        Assert.Equal(PdfValueKind.Null, graph.Resolve(new(15)).Kind);
        Assert.Equal(graph.Resolve(new(22)).Entries["FontFile2"].ReferenceId, graph.Resolve(new(23)).Entries["FontFile2"].ReferenceId);
        Assert.Equal("Data", graph.Resolve(new(9)).Entries["AFRelationship"].NameValue);
        Assert.Equal(original, SignatureTests.Bytes(source.CreateSnapshot().SourceDocument!.OpenOriginalStream()));
        Save("source.pdf", original);
        Save("lossless.pdf", SignatureTests.Bytes(result.OpenPdfStream()));
    }

    [Fact]
    public void ResourcePruningKeepsUsedBindingsAndImplicitColorSpacesAndAssociatedFiles()
    {
        using var source = Open(Fixture());
        var result = PdfOptimization.Optimize(source.CreateSnapshot(), new(removeUnusedResources: true));
        Assert.True(result.RemovedResourceBindings >= 3);
        using var bytes = result.OpenPdfStream();
        var graph = PdfSyntax.Read(bytes);
        var resources = graph.Resolve(new(3)).Entries["Resources"];
        Assert.False(resources.Entries["XObject"].Entries.ContainsKey("Unused"));
        Assert.False(resources.Entries["Font"].Entries.ContainsKey("UnusedFont"));
        Assert.True(resources.Entries["Font"].Entries.ContainsKey("F1"));
        Assert.True(resources.Entries["Font"].Entries.ContainsKey("F2"));
        Assert.True(resources.Entries["ColorSpace"].Entries.ContainsKey("UnusedCS"));
        Assert.True(resources.Entries["ColorSpace"].Entries.ContainsKey("DefaultRGB"));
        Assert.Equal(Attachment, Decode(graph, 10));
        Save("pruned.pdf", SignatureTests.Bytes(result.OpenPdfStream()));
    }

    [Fact]
    public void ResourceUsageIncludesNestedFormsAndType3GlyphProcedures()
    {
        using var source = Open(Fixture(nestedResources: true));
        var result = PdfOptimization.Optimize(source.CreateSnapshot(), new(removeUnusedResources: true));
        using var bytes = result.OpenPdfStream();
        var graph = PdfSyntax.Read(bytes);
        var form = graph.Resolve(new(30)).Entries["Resources"].Entries["XObject"];
        var font = graph.Resolve(new(32)).Entries["Resources"].Entries["XObject"];
        Assert.True(form.Entries.ContainsKey("ImOnly"));
        Assert.False(form.Entries.ContainsKey("Unused"));
        Assert.True(font.Entries.ContainsKey("ImGlyph"));
        Assert.False(font.Entries.ContainsKey("Unused"));
        Assert.Equal(Pixels, Decode(graph, 5));
        Assert.Equal(Alpha, Decode(graph, 11));
    }

    [Fact]
    public void UnsupportedImagePrecisionCannotBeSilentlyReduced()
    {
        using var samples = new MemoryStream(new byte[8]);
        var image = PdfImages.FromSamples(samples, new(2, 2, 1, 16), PdfColorSpace.Gray);
        using var source = Pdf.Create().AddPage(new PdfPageSize(20, 20));
        var canvas = PdfCanvas.Create(source.Pages[0].Geometry, draw => draw.DrawImageAsset(image, new(0, 0, 20, 20)));
        canvas.ApplyTo(source.Pages[0], PdfCanvasApplyMode.Replace);
        using var output = new MemoryStream();
        output.WriteByte(42);
        Assert.Equal("Optimization.ImageProfile", Assert.Throws<PdfException>(() => PdfOptimization.Write(source.CreateSnapshot(), output, new(PdfOptimizationPreset.CompactImages, maximumImageDimension: 1), policy: new(PdfProcessingMode.Permissive))).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
    }

    [Fact]
    public void IdentitySensitiveImageResourcesAreNotAliasedEvenWhenTheirPixelsMatch()
    {
        using var source = Open(Fixture(identitySensitive: true));
        var result = PdfOptimization.Optimize(source.CreateSnapshot());
        using var bytes = result.OpenPdfStream();
        var graph = PdfSyntax.Read(bytes);
        Assert.Equal(100, graph.Resolve(new(5)).Entries["StructParent"].IntegerValue);
        Assert.Equal(101, graph.Resolve(new(6)).Entries["StructParent"].IntegerValue);
        Assert.Equal(Pixels, Decode(graph, 5));
        Assert.Equal(Pixels, Decode(graph, 6));
    }

    [Fact]
    public void ImageResamplingIsExplicitUsesSampleCentersAndPreservesAlphaAndGeometry()
    {
        using var source = Open(Fixture());
        using var output = new MemoryStream();
        output.WriteByte(42);
        var options = new PdfOptimizationOptions(PdfOptimizationPreset.CompactImages, maximumImageDimension: 1);
        Assert.Equal("Optimization.ImageDownsampled", Assert.Throws<PdfException>(() => PdfOptimization.Write(source.CreateSnapshot(), output, options)).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        var result = PdfOptimization.Optimize(source.CreateSnapshot(), options, policy: new(PdfProcessingMode.Permissive));
        Assert.True(result.DownsampledImages >= 2);
        Assert.Contains(result.OperationReport.Diagnostics, d => d.Code == "Optimization.ImageDownsampled" && d.LossCategory == PdfLossCategory.FidelityReduction);
        using var bytes = result.OpenPdfStream();
        var graph = PdfSyntax.Read(bytes);
        Assert.Equal(1, graph.Resolve(new(5)).Entries["Width"].IntegerValue);
        Assert.Equal(new byte[] { 80, 100, 120 }, Decode(graph, 5));
        Assert.Equal(new byte[] { 255 }, Decode(graph, 11));
        using var read = Pdf.Open(result.OpenPdfStream());
        Assert.Equal(source.Pages[0].Geometry, read.Pages[0].Geometry);
        Save("downsampled.pdf", SignatureTests.Bytes(result.OpenPdfStream()));
    }

    [Fact]
    public void UnusedResourcePruningRejectsUnknownContentRatherThanDroppingPossibleBindings()
    {
        using var source = Open(Fixture("/Unused originalExtension\n"));
        using var output = new MemoryStream();
        output.WriteByte(42);
        Assert.Equal("Content.UnknownOperator", Assert.Throws<PdfException>(() => PdfOptimization.Write(source.CreateSnapshot(), output, new(removeUnusedResources: true))).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        var preserved = PdfOptimization.Optimize(source.CreateSnapshot());
        using var bytes = preserved.OpenPdfStream();
        var graph = PdfSyntax.Read(bytes);
        Assert.Contains("originalExtension", Encoding.ASCII.GetString(Decode(graph, 4)));
    }

    [Fact]
    public void PixelAndExpandedLimitsCancellationAndAtomicFailureCannotCommitOutput()
    {
        using var source = Open(Fixture());
        using var output = new MemoryStream();
        output.WriteByte(42);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfOptimization.Write(source.CreateSnapshot(), output, policy: new(limits: new(maximumExpandedBytes: 100)))).Diagnostic.Code);
        Assert.ThrowsAny<OperationCanceledException>(() => PdfOptimization.Write(source.CreateSnapshot(), output, cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => PdfOptimization.Write(source.CreateSnapshot(), output, policy: new(limits: new(maximumOutputBytes: 100)))).Diagnostic.Code);
        var options = new PdfOptimizationOptions(PdfOptimizationPreset.CompactImages, maximumImageDimension: 1);
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfOptimization.Write(source.CreateSnapshot(), output, options, policy: new(PdfProcessingMode.Permissive, limits: new(maximumPixels: 4)))).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        string path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".pdf");
        File.WriteAllText(path, "retained");
        try
        {
            Assert.Throws<PdfException>(() => PdfOptimization.WriteFile(source.CreateSnapshot(), path, options));
            Assert.Equal("retained", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SignedSourcesRequireExplicitRewriteAuthorityAndKeepOriginalSignedBytesUntouched()
    {
        var signed = SignatureVerificationTests.Signed();
        var before = SignatureTests.Bytes(signed.OpenOriginalStream());
        using var input = signed.OpenOriginalStream();
        using var source = Pdf.Open(input);
        Assert.Equal("Optimization.SignatureRewrite", Assert.Throws<PdfException>(() => PdfOptimization.Optimize(source.CreateSnapshot())).Diagnostic.Code);
        var result = PdfOptimization.Optimize(source.CreateSnapshot(), new(allowSignatureInvalidation: true));
        Assert.Contains(result.OperationReport.Diagnostics, d => d.Code == "Writer.SignatureRewriteAllowed");
        Assert.Equal(before, SignatureTests.Bytes(signed.OpenOriginalStream()));
    }
}
