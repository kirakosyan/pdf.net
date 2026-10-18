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

    [Fact]
    public void CompressedMembersAndInflatedContainersAreSharedAcrossRevisions()
    {
        byte[] source = Fixture(true);
        using var input = new MemoryStream(source);
        var original = PdfSyntax.Read(input);
        using var fixture = new SyntaxFixtures();
        fixture.Bytes.SetLength(0);
        fixture.Bytes.Write(source, 0, source.Length);
        long previous = original.Revisions[0].StartXref;
        for (int i = 0; i < 5; i++)
            previous = fixture.Classic(Array.Empty<(int, int, long, bool)>(), "/Root 3 0 R /Prev " + previous, size: 5);
        fixture.Bytes.Position = 0;
        var syntax = PdfSyntax.Read(fixture.Bytes);
        for (int revision = 1; revision < 6; revision++)
        {
            Assert.Same(syntax.Resolve(new(3)), syntax.Resolve(new(3), revision));
            Assert.Same(syntax.Resolve(new(4)), syntax.Resolve(new(4), revision));
        }
    }

    [Fact]
    public void FullRewriteOmitsOldObjectAndCrossReferenceStreams()
    {
        using var input = new MemoryStream(Fixture(true));
        using var edit = new PdfTransaction(PdfSyntax.Read(input));
        using var output = new MemoryStream();
        edit.Save(output, new(crossReferenceFormat: PdfCrossReferenceFormat.Table));
        output.Position = 0;
        var syntax = PdfSyntax.Read(output);
        Assert.Equal("Catalog", syntax.Resolve(new(3)).Entries["Type"].NameValue);
        Assert.Equal(PdfValueKind.Null, syntax.Resolve(new(1)).Kind);
        Assert.Equal(PdfValueKind.Null, syntax.Resolve(new(2)).Kind);
        Assert.Equal(Encoding.ASCII.GetBytes("embedded"), syntax.Resolve(new(4)).StringBytes);
    }

    [Fact]
    public void ObjectStreamCacheRechecksRevisionSpecificIndirectFilterDeclarations()
    {
        using var fixture = new SyntaxFixtures("1.5");
        long filter = fixture.Object(5, "null");
        long container = fixture.StreamObject(1, "/Type /ObjStm /N 1 /First 4 /Filter 5 0 R", Encoding.ASCII.GetBytes("3 0 (shared)"));
        long xref = fixture.Bytes.Position;
        fixture.StreamObject(2, "/Type /XRef /Size 6 /W [1 4 2] /Index [1 3 5 1]", SyntaxFixtures.Entry(1, container, 0).Concat(SyntaxFixtures.Entry(1, xref, 0)).Concat(SyntaxFixtures.Entry(2, 1, 0)).Concat(SyntaxFixtures.Entry(1, filter, 0)).ToArray());
        fixture.Text("startxref\n" + xref + "\n%%EOF\n");
        long updatedFilter = fixture.Object(5, "[]");
        fixture.Classic(new[] { (5, 0, updatedFilter, false) }, "/Prev " + xref, size: 6);
        fixture.Bytes.Position = 0;
        var syntax = PdfSyntax.Read(fixture.Bytes);
        Assert.NotSame(syntax.Resolve(new(3)), syntax.Resolve(new(3), 1));
        Assert.Equal(syntax.Resolve(new(3)).StringBytes, syntax.Resolve(new(3), 1).StringBytes);
    }

    internal static byte[] Fixture(bool flate, string fault = "")
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
