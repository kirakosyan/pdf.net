using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class EngineeringTests
{
    internal static byte[] Fixture()
    {
        using var f = new SyntaxFixtures();
        var entries = new List<(int, int, long, bool)>
        {
            (0, 65535, 0, true)
        };
        void Obj(int id, string body) => entries.Add((id, 0, f.Object(id, body), false));
        void Stream(int id, string dictionary, string body) => entries.Add((id, 0, f.StreamObject(id, dictionary, Encoding.ASCII.GetBytes(body)), false));
        Obj(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Obj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Obj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 72 36] /Resources <<>> /Annots [4 0 R] >>");
        Obj(4, "<< /Type /Annot /Subtype /3D /Rect [0 0 20 20] /3DD 5 0 R /3DA << /A /PO >> >>");
        Stream(5, "/Type /3D /Subtype /U3D", "Original opaque U3D fixture; not a real media corpus");
        Stream(6, "/Type /EmbeddedFile", "Original opaque engineering companion");
        Obj(7, "<< /Type /Measure /Subtype /GEO /GPTS [0 0 1 1] >>");
        Stream(8, "/Type /3D /Subtype /PRC", "Original opaque PRC fixture; not a real media corpus");
        f.Classic(entries.ToArray(), "/Root 1 0 R", 9);
        return f.Bytes.ToArray();
    }

    private static string Hash(byte[] bytes)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    [Fact]
    public void InventoryIncludesUnattachedOpaquePayloadsAndGeospatialDefinitionsWithoutExecution()
    {
        byte[] bytes = Fixture();
        using var input = new MemoryStream(bytes);
        using var doc = Pdf.Open(input);
        var inventory = PdfEngineering.Inspect(doc.CreateSnapshot());
        Assert.Contains(inventory.Records, r => r.Kind == PdfEngineeringContentKind.U3D && r.EncodedSha256 == Hash(Encoding.ASCII.GetBytes("Original opaque U3D fixture; not a real media corpus")));
        Assert.Contains(inventory.Records, r => r.Kind == PdfEngineeringContentKind.Prc);
        Assert.Contains(inventory.Records, r => r.Kind == PdfEngineeringContentKind.ThreeDAnnotation);
        Assert.Contains(inventory.Records, r => r.Kind == PdfEngineeringContentKind.Geospatial);
        Assert.Contains(inventory.Records, r => r.Kind == PdfEngineeringContentKind.EmbeddedFile);
        Assert.All(inventory.Records, r => Assert.Contains("obj", r.Location));
        using var original = doc.CreateSnapshot().SourceDocument!.OpenOriginalStream();
        using var retained = new MemoryStream();
        original.CopyTo(retained);
        Assert.Equal(bytes, retained.ToArray());
    }

    [Fact]
    public void PreserveCopiesExactHistoryAndAllAssociationsWithoutClaimingSignatureOrConformanceValidity()
    {
        byte[] bytes = Fixture();
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        var result = PdfEngineering.Preserve(input, output);
        Assert.Equal(bytes, output.ToArray());
        Assert.Equal(Hash(bytes), result.Sha256);
        Assert.Equal(bytes.Length, result.Length);
        Assert.True(input.CanRead);
        Assert.True(output.CanWrite);
        input.Position = 0;
        using var failed = new MemoryStream();
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfEngineering.Preserve(input, failed, new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        Assert.Equal(0, failed.Length);
    }

    private sealed class Producer : IPdfProfileProducer
    {
        internal byte[] Bytes = Fixture();
        public string EngineId => "fixture.engineering-producer";

        public bool Supports(PdfProfileOptions options) => options.Profile == PdfConformanceProfile.E12008;
        public void Convert(Stream source, PdfProfileOptions options, Stream candidate, PdfOperationContext context)
        {
            context.Checkpoint();
            candidate.Write(Bytes, 0, Bytes.Length);
        }
    }

    private sealed class Validator : IPdfProfileValidator
    {
        public string EngineId => "fixture.engineering-binding-only";

        public bool Supports(PdfConformanceProfile profile) => profile == PdfConformanceProfile.E12008;
        public PdfProfileValidation Validate(Stream input, PdfProfileOptions options, string request, PdfOperationContext context)
        {
            using var bytes = new MemoryStream();
            input.CopyTo(bytes);
            context.Checkpoint();
            return new(options.Profile, Hash(bytes.ToArray()), request, true, true, Array.Empty<string>());
        }
    }

    [Fact]
    public void EngineeringConversionProvesOnlyExactByteRetentionUnlessRewriteLossIsExplicitlyAuthorized()
    {
        using var input = new MemoryStream(Fixture());
        using var output = new MemoryStream();
        var producer = new Producer();
        var validator = new Validator();
        var result = PdfProfileConversions.Convert(input, output, new(PdfConformanceProfile.E12008), producer, validator);
        Assert.True(result.Completed);
        Assert.Equal(Fixture(), output.ToArray());
        input.Position = 0;
        output.SetLength(0);
        producer.Bytes = ProfileConversionTests.PdfBytes();
        Assert.Equal("Profile.AdvancedContentRetention", Assert.Throws<PdfException>(() => PdfProfileConversions.Convert(input, output, new(PdfConformanceProfile.E12008), producer, validator)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        input.Position = 0;
        var options = new PdfProfileOptions(PdfConformanceProfile.E12008, allowAdvancedContentLoss: true);
        Assert.Equal("Profile.AdvancedContentLoss", Assert.Throws<PdfException>(() => PdfProfileConversions.Convert(input, output, options, producer, validator)).Diagnostic.Code);
        input.Position = 0;
        var accepted = PdfProfileConversions.Convert(input, output, options, producer, validator, new(PdfProcessingMode.Permissive));
        Assert.Contains(accepted.OperationReport!.Diagnostics, d => d.Code == "Profile.AdvancedContentLoss" && d.Severity == PdfDiagnosticSeverity.Warning);
    }
}
