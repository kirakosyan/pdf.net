using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class EncryptedSyntaxTests
{
    [Fact]
    public void EncryptedCompressedObjectsRejectBeforePlaintextFilterDecoding()
    {
        using var fixture = new SyntaxFixtures("1.5");
        long catalog = fixture.Object(1, "<< /Type /Catalog >>"), container = fixture.StreamObject(2, "/Type /ObjStm /N 1 /First 4 /Filter /FlateDecode", new byte[] { 1, 2, 3, 4 }), encryption = fixture.Object(4, "<< /Filter /Standard >>"), xref = fixture.Bytes.Position;
        byte[] rows = SyntaxFixtures.Entry(0, 0, 65535).Concat(SyntaxFixtures.Entry(1, catalog, 0)).Concat(SyntaxFixtures.Entry(1, container, 0)).Concat(SyntaxFixtures.Entry(1, xref, 0)).Concat(SyntaxFixtures.Entry(1, encryption, 0)).Concat(SyntaxFixtures.Entry(2, 2, 0)).ToArray();
        fixture.StreamObject(3, "/Type /XRef /Size 6 /Root 1 0 R /Encrypt 4 0 R /W [1 4 2]", rows);
        fixture.Text("startxref\n" + xref + "\n%%EOF\n");
        fixture.Bytes.Position = 0;
        Assert.Equal("Syntax.EncryptionProviderRequired", Assert.Throws<PdfException>(() => PdfSyntax.Read(fixture.Bytes)).Diagnostic.Code);
    }
}
