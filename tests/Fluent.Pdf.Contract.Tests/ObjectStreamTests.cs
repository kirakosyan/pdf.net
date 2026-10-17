using System.IO;
using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ObjectStreamTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResolvesIndexedMembersWithExactIdentityAndPreservesUnknownKeys(bool flate)
    {
        byte[] source = Fixture(flate);
        using var input = new MemoryStream(source);
        var syntax = PdfSyntax.Read(input);
        var catalog = syntax.Resolve(new(3));
        Assert.Equal("Catalog", catalog.Entries["Type"].NameValue);
        Assert.Equal(new PdfObjectId(4), catalog.Entries["Unknown"].ReferenceId);
        Assert.Equal(Encoding.ASCII.GetBytes("embedded"), syntax.Resolve(new(4)).StringBytes);
        Assert.Same(PdfValue.Null, syntax.Resolve(new(4, 1)));
        Assert.Equal(PdfCrossReferenceKind.Compressed, syntax.Revisions[0].Entries.Single(x => x.Id.Number == 3).Kind);
    }

    [Theory]
    [InlineData("wrong-index")]
    [InlineData("duplicate-id")]
    [InlineData("wrong-first")]
    [InlineData("stream-member")]
    [InlineData("bad-count")]
    public void InvalidObjectStreamMetadataFailsWithoutRecursionOrRepair(string fault)
    {
        using var input = new MemoryStream(Fixture(true, fault));
        var failure = Assert.Throws<PdfException>(() => PdfSyntax.Read(input));
        Assert.StartsWith("Syntax.", failure.Diagnostic.Code);
    }

    private static byte[] Fixture(bool flate, string fault = "")
    {
        using var fixture = new SyntaxFixtures("1.5");
        string first = fault == "stream-member" ? "<< /Length 0 >>\nstream\n\nendstream" : "<< /Type /Catalog /Unknown 4 0 R >>";
        string header = "3 0 " + (fault == "duplicate-id" ? "3" : "4") + " " + (first.Length + 1) + " ";
        byte[] raw = Encoding.ASCII.GetBytes(header + first + " (embedded)");
        int firstOffset = fault == "wrong-first" ? 1 : header.Length;
        string count = fault == "bad-count" ? "9223372036854775807" : "2";
        long container = fixture.StreamObject(1, $"/Type /ObjStm /N {count} /First {firstOffset}" + (flate ? " /Filter /FlateDecode" : ""), flate ? SyntaxFixtures.ZlibStored(raw) : raw);
        long xref = fixture.Bytes.Position;
        byte[] entries = SyntaxFixtures.Entry(0, 0, 65535).Concat(SyntaxFixtures.Entry(1, container, 0)).Concat(SyntaxFixtures.Entry(1, xref, 0)).Concat(SyntaxFixtures.Entry(2, 1, fault == "wrong-index" ? 1 : 0)).Concat(SyntaxFixtures.Entry(2, 1, 1)).ToArray();
        fixture.StreamObject(2, "/Type /XRef /Size 5 /Root 3 0 R /W [1 4 2]", entries);
        fixture.Text("startxref\n" + xref + "\n%%EOF\n");
        return fixture.Bytes.ToArray();
    }
}
