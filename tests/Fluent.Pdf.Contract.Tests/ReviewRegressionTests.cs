using System.IO;
using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ReviewRegressionTests
{
    [Theory]
    [InlineData(PdfArchivalProfile.A1B)]
    [InlineData(PdfArchivalProfile.A2B)]
    [InlineData(PdfArchivalProfile.A3B)]
    public void LosslessOptimizationRetainsPlainXmpAndIndependentArchivalConformance(PdfArchivalProfile profile)
    {
        using var original = ArchivalTests.DecorativeDrawing(profile);
        using var archival = new MemoryStream();
        var providers = ArchivalFixture.Providers();
        Assert.True(PdfArchival.Convert(original.CreateSnapshot(), archival, new(profile, ArchivalFixture.Icc()), providers).Validation.IsCompliant);
        archival.Position = 0;
        using var source = Pdf.Open(archival);
        var result = PdfOptimization.Optimize(source.CreateSnapshot());
        using var output = result.OpenPdfStream();
        var graph = PdfSyntax.Read(output);
        var metadata = graph.Resolve(graph.Resolve(graph.Revisions[0].Trailer.Entries["Root"].ReferenceId).Entries["Metadata"].ReferenceId);
        Assert.False(metadata.Entries.ContainsKey("Filter"));
        output.Position = 0;
        Assert.True(PdfArchival.Validate(output, profile, providers).IsCompliant);
    }

    [Fact]
    public void PrunedIndirectResourceAndCategoryDictionariesStaySharedByAllPages()
    {
        using var fixture = new SyntaxFixtures();
        var entries = new List<(int, int, long, bool)>
        {
            (0, 65535, 0, true)
        };
        void Object(int id, string value) => entries.Add((id, 0, fixture.Object(id, value), false));
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, "<< /Type /Pages /Count 128 /Kids [" + string.Join(" ", Enumerable.Range(10, 128).Select(id => id + " 0 R")) + "] >>");
        entries.Add((4, 0, fixture.StreamObject(4, "", Encoding.ASCII.GetBytes("BT /F1 10 Tf (Original shared resource) Tj ET")), false));
        Object(5, "<< /Font 6 0 R >>");
        Object(6, "<< /F1 7 0 R /Unused 8 0 R >>");
        Object(7, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        Object(8, "<< /Type /Font /Subtype /Type1 /BaseFont /Courier >>");
        foreach (int id in Enumerable.Range(10, 128))
            Object(id, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources 5 0 R /Contents 4 0 R >>");
        fixture.Classic(entries.OrderBy(e => e.Item1).ToArray(), "/Root 1 0 R", 138);
        using var source = Pdf.Open(new MemoryStream(fixture.Bytes.ToArray()));
        var result = PdfOptimization.Optimize(source.CreateSnapshot(), new(removeUnusedResources: true));
        Assert.Equal(1, result.RemovedResourceBindings);
        var graph = PdfSyntax.Read(result.OpenPdfStream());
        foreach (int id in Enumerable.Range(10, 128))
            Assert.Equal(new PdfObjectId(5), graph.Resolve(new(id)).Entries["Resources"].ReferenceId);
        Assert.Equal(new PdfObjectId(6), graph.Resolve(new(5)).Entries["Font"].ReferenceId);
        Assert.False(graph.Resolve(new(6)).Entries.ContainsKey("Unused"));
        Assert.True(graph.Resolve(new(6)).Entries.ContainsKey("F1"));
    }

    private sealed class ExplicitSamples : IPdfImageCodecProvider
    {
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.resampling", "1", PdfCapability.ImageCodecs);

        public bool Supports(PdfImageCodec codec, bool encoding) => codec == PdfImageCodec.Dct && !encoding;
        public PdfImageInfo Inspect(PdfImageCodec codec, Stream input, PdfValue dictionary, PdfOperationContext context) => new(50, 50, 1, 8);
        public void Decode(PdfImageCodec codec, PdfImageInfo info, PdfValue dictionary, PdfValue parameters, Stream input, Stream output, PdfOperationContext context)
        {
            for (int i = 0; i < 2500; i++)
                output.WriteByte((byte)((i * 73 + i / 17) % 256));
        }

        public void Encode(PdfImageCodec codec, PdfImageInfo info, PdfValue parameters, Stream input, Stream output, PdfOperationContext context) => throw new NotSupportedException();
    }

    [Fact]
    public void LargerResampledEncodingRetainsTheOriginalImageBytesAndResolution()
    {
        using var fixture = new SyntaxFixtures();
        var entries = new List<(int, int, long, bool)>
        {
            (0, 65535, 0, true)
        };
        void Object(int id, string value) => entries.Add((id, 0, fixture.Object(id, value), false));
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << /XObject << /Im 5 0 R >> >> /Contents 4 0 R >>");
        entries.Add((4, 0, fixture.StreamObject(4, "", Encoding.ASCII.GetBytes("q 100 0 0 100 0 0 cm /Im Do Q")), false));
        // A synthetic encoding owned by the explicit test codec, never a JPEG fidelity fixture.
        entries.Add((5, 0, fixture.StreamObject(5, "/Type /XObject /Subtype /Image /Width 50 /Height 50 /BitsPerComponent 8 /ColorSpace /DeviceGray /Filter /DCTDecode", new byte[] { 1, 2, 3, 4 }), false));
        fixture.Classic(entries.ToArray(), "/Root 1 0 R", 6);
        using var source = Pdf.Open(new MemoryStream(fixture.Bytes.ToArray()));
        var result = PdfOptimization.Optimize(source.CreateSnapshot(), new(PdfOptimizationPreset.CompactImages, maximumImageDimension: 25), new(new ExplicitSamples()));
        Assert.Equal(0, result.DownsampledImages);
        Assert.Contains(result.OperationReport.Diagnostics, d => d.Code == "Optimization.ImageEncodingRetained");
        var image = PdfSyntax.Read(result.OpenPdfStream()).Resolve(new(5));
        Assert.Equal(50, image.Entries["Width"].IntegerValue);
        Assert.Equal("DCTDecode", image.Entries["Filter"].NameValue);
        using var raw = image.OpenRawStream();
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, SignatureTests.Bytes(raw));
    }

    private static byte[] Structured(int count, string role, string mapping = "")
    {
        using var fixture = new SyntaxFixtures();
        var entries = new List<(int, int, long, bool)>
        {
            (0, 65535, 0, true)
        };
        void Object(int id, string value) => entries.Add((id, 0, fixture.Object(id, value), false));
        Object(1, "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 5 0 R /MarkInfo << /Marked true >> /Lang (en-US) >>");
        Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << >> /Contents 4 0 R >>");
        entries.Add((4, 0, fixture.StreamObject(4, "", Array.Empty<byte>()), false));
        Object(5, "<< /Type /StructTreeRoot /ParentTree 6 0 R /K [" + string.Join(" ", Enumerable.Range(7, count).Select(id => id + " 0 R")) + "] " + mapping + " >>");
        Object(6, "<< /Nums [] >>");
        foreach (int id in Enumerable.Range(7, count))
            Object(id, "<< /Type /StructElem /S /" + role + " /P 5 0 R /K [] >>");
        fixture.Classic(entries.ToArray(), "/Root 1 0 R", count + 7);
        return fixture.Bytes.ToArray();
    }

    [Fact]
    public void StandardRoleRemappingIsAFindingAndDoesNotBlockInspectionOrRemediation()
    {
        byte[] bytes = Structured(1, "Link", "/RoleMap << /Link /Span >>");
        using var source = Pdf.Open(new MemoryStream(bytes));
        var inspection = PdfAccessibility.Inspect(source.CreateSnapshot());
        Assert.Equal("Link", Assert.Single(inspection.Elements).Role);
        Assert.Single(inspection.Findings, f => f.Rule == "Accessibility.RoleMap");
        using var remediated = PdfAccessibility.Remediate(inspection, inspection.Elements[0].ObjectId, language: "fr");
        Assert.Equal("Link", Assert.Single(PdfAccessibility.Inspect(remediated.CreateSnapshot()).Elements).Role);
        var plan = PdfAccessibility.PrepareReview(new MemoryStream(bytes), new[] { PdfSemanticSuggestion.Element("language", inspection.Elements[0].ObjectId, "Explicit language", language: "fr") });
        using var output = new MemoryStream();
        var audit = PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, new[] { new PdfSemanticReviewDecision("language", true, "fixture", "Synthetic regression decision; no human qualification") });
        Assert.True(Assert.Single(audit.Entries).Applied);
    }

    [Fact]
    public void TwelveThousandCustomElementsProduceOneUnmappedRoleFinding()
    {
        using var source = Pdf.Open(new MemoryStream(Structured(12000, "Normal")));
        var inspection = PdfAccessibility.Inspect(source.CreateSnapshot());
        Assert.Equal(12000, inspection.Elements.Count);
        Assert.Single(inspection.Findings, f => f.Rule == "Accessibility.RoleMapping");
    }

    [Fact]
    public void LazySignedGraphsRejectCompleteSignatureAndRevisionValueInventories()
    {
        var signed = SignatureVerificationTests.Signed();
        PolicyTests.Run(c =>
        {
            var lazy = new PdfSyntaxReader(signed.Source, c).Read(lazy: true);
            Assert.Throws<InvalidOperationException>(() => lazy.HasSignatureByteRanges);
            Assert.Throws<InvalidOperationException>(() => lazy.Revisions[0].Values);
            Assert.NotEmpty(lazy.Revisions[0].Entries);
        });
    }
}
