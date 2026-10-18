using System.Text;
using Fluent.Pdf.Fonts;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ResourceExtractionTests
{
    internal static readonly byte[] ImageBytes = Encoding.ASCII.GetBytes("FF0000>");
    internal static byte[] Fixture(bool cycle = false, bool unknown = false, bool corruptFont = false, bool restricted = false, bool inline = false, bool graphicsFont = false)
    {
        using var f = new SyntaxFixtures();
        var offsets = new Dictionary<int, long>();
        offsets[1] = f.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        offsets[2] = f.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        offsets[3] = f.Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Resources << /XObject << /Shared 5 0 R /DirectImage 6 0 R /Unused 11 0 R >> " + (graphicsFont ? "/ExtGState << /G << /Font [7 0 R 12] >> >>" : "/Font << /F1 7 0 R >>") + " /Custom << /Original 12 0 R >> >> /Contents 4 0 R >>");
        string content = "q 2 0 0 3 10 20 cm /Shared Do Q\nq 1 0 0 1 100 0 cm /Shared Do Q\n/Artifact << /Type /Layout >> BDC 0 0 m 10 0 l 2 0 0 2 0 0 cm 10 10 l h S /DirectImage Do EMC\nBT /F1 12 Tf 20 50 Td (A) Tj ET\n";
        if (graphicsFont)
            content = content.Replace("BT /F1 12 Tf", "/G gs BT");
        if (inline)
            content += "q BI /W 1 /H 1 /BPC 8 /CS /RGB /F /AHx ID 00FF00> EI Q\n";
        if (unknown)
            content += "42 OriginalExtension\n";
        offsets[4] = f.StreamObject(4, "", Encoding.ASCII.GetBytes(content));
        offsets[5] = f.StreamObject(5, "/Type /XObject /Subtype /Form /BBox [0 0 20 20] /Matrix [1 0 0 1 5 7] /Resources << /XObject << /Im 6 0 R" + (cycle ? " /Self 5 0 R" : "") + " >> " + (graphicsFont ? "" : "/Font << /F1 7 0 R >>") + " >>", Encoding.ASCII.GetBytes("q 4 0 0 5 1 2 cm /Im Do Q 1 2 3 4 re W n\n" + (cycle ? "/Self Do\n" : "")));
        offsets[6] = f.StreamObject(6, "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode", ImageBytes);
        offsets[7] = f.Object(7, "<< /Type /Font /Subtype /TrueType /BaseFont /FluentOriginalTrueType /FirstChar 65 /LastChar 66 /Widths [600 600] /Encoding /WinAnsiEncoding /FontDescriptor 8 0 R >>");
        offsets[8] = f.Object(8, "<< /Type /FontDescriptor /FontName /FluentOriginalTrueType /Flags 32 /FontBBox [0 -200 600 800] /ItalicAngle 0 /Ascent 800 /Descent -200 /CapHeight 700 /StemV 80 /FontFile2 9 0 R >>");
        byte[] font = File.ReadAllBytes(FontTests.Fixture("original.ttf"));
        if (restricted)
            for (int i = 0; i < font[4] * 256 + font[5]; i++)
            {
                int record = 12 + i * 16;
                if (Encoding.ASCII.GetString(font, record, 4) != "OS/2")
                    continue;
                int at = (font[record + 8] << 24 | font[record + 9] << 16 | font[record + 10] << 8 | font[record + 11]) + 8;
                font[at] = 0;
                font[at + 1] = 2;
            }

        offsets[9] = f.StreamObject(9, "/Length1 " + font.Length + " /Filter /FlateDecode", corruptFont ? new byte[] { 0x78, 1, 0xff } : SyntaxFixtures.ZlibStored(font));
        offsets[11] = f.StreamObject(11, "/Type /XObject /Subtype /PS", Encoding.ASCII.GetBytes("Original passive unsupported stream"));
        offsets[12] = f.Object(12, "<< /Original true >>");
        var rows = new List<(int, int, long, bool)>
        {
            (0, 65535, 0, true)
        };
        rows.AddRange(offsets.Select(p => (p.Key, 0, p.Value, false)));
        f.Classic(rows.ToArray(), "/Root 1 0 R", 13);
        return f.Bytes.ToArray();
    }

    private static PdfDocument Open(byte[]? bytes = null)
    {
        using var input = new MemoryStream(bytes ?? Fixture());
        return Pdf.Open(input);
    }

    [Fact]
    public void BothLanguageConsumersEnumerateActualPageUses()
    {
        using var document = Open();
        var snapshot = document.CreateSnapshot();
        var cs = ContractExamples.CSharp.ContractExample.ExtractResources(snapshot, 0);
        var vb = ContractExamples.VisualBasic.ContractExample.ExtractResources(snapshot, 0);
        Assert.Equal(3, cs.Occurrences.Count(x => cs.Resources[x.ResourceIndex].Kind == PdfExtractedResourceKind.Image));
        Assert.Equal(cs.Paths.Count, vb.Paths.Count);
    }

    [Fact]
    public void SharedDefinitionsAreSeparateFromDeclarationsAndTransformedNestedUses()
    {
        using var document = Open();
        var result = PdfResourceExtraction.ReadPage(document.CreateSnapshot(), 0);
        var image = Assert.Single(result.Resources, r => r.Kind == PdfExtractedResourceKind.Image);
        Assert.Equal(new PdfObjectId(6), image.ObjectId);
        Assert.Equal(2, result.Declarations.Count(d => d.ResourceIndex == image.Index));
        var uses = result.Occurrences.Where(x => x.ResourceIndex == image.Index).ToArray();
        Assert.Equal(3, uses.Length);
        Assert.Equal(8, uses[0].Transform.A);
        Assert.Equal(15, uses[0].Transform.D);
        Assert.Equal(22, uses[0].Transform.E);
        Assert.Equal(47, uses[0].Transform.F);
        Assert.Equal(4, uses[1].Transform.A);
        Assert.Equal(5, uses[1].Transform.D);
        Assert.Equal(106, uses[1].Transform.E);
        Assert.Equal(9, uses[1].Transform.F);
        Assert.Equal(2, uses[2].Transform.A);
        Assert.True(uses[2].IsArtifact);
        Assert.False(uses[0].IsArtifact);
        Assert.Single(uses[0].Source.FormPath);
        Assert.NotEqual(uses[0].Source.FormInvocationIndices[0], uses[1].Source.FormInvocationIndices[0]);
        Assert.Equal(2, result.Content.Count);
        Assert.Contains(result.Resources, r => r.Kind == PdfExtractedResourceKind.Unknown && r.ObjectId == new PdfObjectId(11));
        Assert.Contains(result.OperationReport.Diagnostics, d => d.Code == "Resources.UnknownType");
    }

    [Fact]
    public void LiteralMixedTransformPathsAndExplicitArtifactAssociationsRemainDistinct()
    {
        using var document = Open();
        var result = PdfResourceExtraction.ReadPage(document.CreateSnapshot(), 0);
        Assert.Equal(3, result.Paths.Count);
        Assert.Equal(2, result.Paths.Count(p => p.SetsClip));
        var path = Assert.Single(result.Paths, p => p.EndOperation.Operation == PdfOperator.Stroke);
        Assert.True(path.IsArtifact);
        Assert.Equal(1, path.Elements[0].Transform.A);
        Assert.Equal(2, path.Elements[2].Transform.A);
        var artifact = Assert.Single(result.Artifacts);
        Assert.Equal("Layout", artifact.Properties.Entries["Type"].NameValue);
        Assert.Equal(2, Assert.Single(artifact.PathIndices));
        Assert.Single(artifact.OccurrenceIndices);
        Assert.True(artifact.EndOperationIndex > artifact.Source.OperationIndex);
    }

    [Fact]
    public void EncodedImageAndFontBytesSurviveDocumentDisposalAndDecodedExportsAreExplicit()
    {
        PdfPageResourceManifest result;
        using (var document = Open())
            result = PdfResourceExtraction.ReadPage(document.CreateSnapshot(), 0);
        var image = Assert.Single(result.Resources, r => r.Kind == PdfExtractedResourceKind.Image);
        using var encoded = new MemoryStream();
        image.WriteStream(encoded);
        Assert.True(encoded.CanWrite);
        Assert.Equal(ImageBytes, encoded.ToArray());
        using var decoded = new MemoryStream();
        image.WriteStream(decoded, true);
        Assert.Equal(new byte[] { 255, 0, 0 }, decoded.ToArray());
        var font = Assert.Single(result.Resources, r => r.Kind == PdfExtractedResourceKind.Font);
        var program = result.Resources[Assert.Single(font.FontProgramIndices)];
        using var bytes = new MemoryStream();
        program.WriteStream(bytes, true);
        Assert.Equal(File.ReadAllBytes(FontTests.Fixture("original.ttf")), bytes.ToArray());
        Assert.Equal("Provider.Unavailable", Assert.Throws<PdfProviderUnavailableException>(() => program.ReadFontProgram(new())).Diagnostic.Code);
        var face = program.ReadFontProgram(new(ManagedFontProvider.Instance));
        Assert.Equal(PdfFontEmbeddingRights.PreviewAndPrint, face.Info.EmbeddingRights);
        Assert.True(face.OperationReport.Succeeded);
    }

    [Fact]
    public void ExplicitFontInspectionReportsActualEmbeddingRestrictionsWithoutSubstitution()
    {
        using var document = Open(Fixture(restricted: true));
        var result = PdfResourceExtraction.ReadPage(document.CreateSnapshot(), 0);
        var program = Assert.Single(result.Resources, r => r.Kind == PdfExtractedResourceKind.FontProgram);
        var face = program.ReadFontProgram(new(ManagedFontProvider.Instance));
        Assert.True((face.Info.EmbeddingRights & PdfFontEmbeddingRights.Restricted) != 0);
    }

    [Fact]
    public void FontsSelectedThroughGraphicsStateAreInventoriedWithoutInventingNamedFontBindings()
    {
        using var document = Open(Fixture(graphicsFont: true));
        var result = PdfResourceExtraction.ReadPage(document.CreateSnapshot(), 0);
        var font = Assert.Single(result.Resources, r => r.Kind == PdfExtractedResourceKind.Font);
        Assert.Single(font.FontProgramIndices);
        Assert.DoesNotContain(result.Declarations, d => d.Category == "Font");
        Assert.Equal("gs.Font", Assert.Single(result.Occurrences, o => o.ResourceIndex == font.Index).Usage);
    }

    [Fact]
    public void InlineImageBytesAndUnknownOperatorsHaveExplicitIndependentInventoryRoles()
    {
        using var document = Open(Fixture(inline: true, unknown: true));
        Assert.Throws<PdfException>(() => PdfResourceExtraction.ReadPage(document.CreateSnapshot(), 0));
        var result = PdfResourceExtraction.ReadPage(document.CreateSnapshot(), 0, PdfUnknownOperatorPolicy.Preserve);
        var inline = Assert.Single(result.Resources, r => r.DeclaredSubtype == "InlineImage");
        Assert.Null(inline.ObjectId);
        Assert.Equal(PdfExtractedResourceKind.Image, inline.Kind);
        using var output = new MemoryStream();
        inline.WriteStream(output);
        Assert.Equal("00FF00>", Encoding.ASCII.GetString(output.ToArray()));
        Assert.Contains(result.OperationReport.Diagnostics, d => d.Code == "Resources.UnknownOperator");
        Assert.Contains(result.Content[0].Program.Operations, op => op.Operation == PdfOperator.Unknown && op.Token == "OriginalExtension");
    }

    [Fact]
    public void CorruptFontDecodingDoesNotPublishCallerBytesOrReplaceExistingFiles()
    {
        using var document = Open(Fixture(corruptFont: true));
        var result = PdfResourceExtraction.ReadPage(document.CreateSnapshot(), 0);
        var program = Assert.Single(result.Resources, r => r.Kind == PdfExtractedResourceKind.FontProgram);
        using var output = new MemoryStream();
        Assert.Throws<PdfException>(() => program.WriteStream(output, true));
        Assert.Equal(0, output.Length);
        Assert.True(output.CanWrite);
        string path = Path.Combine(Path.GetTempPath(), "original-extracted-font-" + Guid.NewGuid().ToString("N"));
        File.WriteAllText(path, "retained");
        try
        {
            Assert.Throws<PdfException>(() => program.WriteFile(path, true));
            Assert.Equal("retained", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InvokedFormCyclesAndResourceBudgetsFailBeforeSuccessfulInventory()
    {
        using var recursive = Open(Fixture(cycle: true));
        Assert.Equal("Resources.FormCycle", Assert.Throws<PdfException>(() => PdfResourceExtraction.ReadPage(recursive.CreateSnapshot(), 0)).Diagnostic.Code);
        using var document = Open();
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfResourceExtraction.ReadPage(document.CreateSnapshot(), 0, policy: new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        var bounded = PdfResourceExtraction.ReadPage(document.CreateSnapshot(), 0, policy: new(limits: new(maximumPixels: 1)));
        int shared = Assert.Single(bounded.Resources, r => r.Kind == PdfExtractedResourceKind.Image).Index;
        Assert.Equal(3, bounded.Occurrences.Count(x => x.ResourceIndex == shared));
        using var additionalImage = Open(Fixture(inline: true));
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfResourceExtraction.ReadPage(additionalImage.CreateSnapshot(), 0, policy: new(limits: new(maximumPixels: 1)))).Diagnostic.Code);
        Assert.Throws<OperationCanceledException>(() => PdfResourceExtraction.ReadPage(document.CreateSnapshot(), 0, cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public void OwnedStampsAndTheirExplicitArtifactsAreInventoriedUnderTheSameBoundary()
    {
        using var original = Open();
        using var stamped = PdfStamps.Apply(original.CreateSnapshot(), "owned", PdfStampContent.FromDrawing(GraphicsTests.Rectangle(), new(0, 0, 100, 100)), new[] { 0 }, new(new(20, 20, 40, 40)));
        var result = PdfResourceExtraction.ReadPage(stamped.CreateSnapshot(), 0);
        Assert.Equal("owned", Assert.Single(result.Stamps).Identifier);
        Assert.True(result.Artifacts.Count >= 2);
        Assert.Contains(result.Paths, p => p.IsArtifact);
    }
}
