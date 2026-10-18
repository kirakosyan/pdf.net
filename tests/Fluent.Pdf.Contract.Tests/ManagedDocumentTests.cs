using System.IO;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ManagedDocumentTests
{
    [Fact]
    public void CSharpAndVisualBasicConsumersEmitRealManagedPdf()
    {
        foreach (var write in new Func<Stream, PdfOperationReport>[]
        {
            ContractExamples.CSharp.ContractExample.WriteManagedPdf,
            ContractExamples.VisualBasic.ContractExample.WriteManagedPdf
        }

        )
        {
            using var output = new MemoryStream();
            Assert.True(write(output).Succeeded);
            output.Position = 0;
            using var document = Pdf.Open(output);
            Assert.Single(document.Pages);
            Assert.Equal(PdfPageSize.Letter, document.Pages[0].Size);
        }
    }

    [Fact]
    public void DefaultManagedProviderWritesAndReopensPagesWithoutOptionalDependencies()
    {
        using var original = Pdf.Create().AddPage(PdfPageSize.Letter).AddPage(PdfPageSize.A4);
        original.Pages[1].Rotation = PdfPageRotation.Clockwise90;
        using var output = new MemoryStream();
        var report = original.Save(output);
        Assert.Equal("managed.pdf", report.ProviderId);
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        Assert.Equal(2, reopened.Pages.Count);
        Assert.Equal(PdfPageSize.Letter, reopened.Pages[0].Size);
        Assert.Equal(PdfPageRotation.Clockwise90, reopened.Pages[1].Rotation);
        Assert.True(output.CanRead);
        Assert.NotNull(reopened.SourceDocument);
        Assert.NotNull(reopened.Pages[0].Source);
        var snapshot = reopened.CreateSnapshot();
        reopened.Dispose();
        Assert.Equal(PdfPageSize.Letter, snapshot.Pages[0].Size);
        Assert.NotNull(snapshot.SourceDocument);
    }

    [Fact]
    public void ExistingIndependentFixturePreservesContentWhileChangingGeometry()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "Fixtures", "syntax", "one-page-classic.pdf");
        using var document = Pdf.OpenFile(path);
        Assert.Single(document.Pages);
        Assert.Equal(PdfPageSize.Letter, document.Pages[0].Size);
        var source = document.Pages[0].Source!;
        var contents = source.Dictionary.Entries["Contents"].ReferenceId;
        using var before = source.Document.Resolve(contents).OpenRawStream();
        byte[] expected = Copy(before);
        document.Pages[0].Rotation = PdfPageRotation.Clockwise180;
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        Assert.Equal(PdfPageRotation.Clockwise180, reopened.Pages[0].Rotation);
        using var after = reopened.SourceDocument!.Resolve(contents).OpenRawStream();
        Assert.Equal(expected, Copy(after));
    }

    [Fact]
    public void ReorderingMaterializesInheritanceAndPreservesUnitsOriginsAndUnknownEntries()
    {
        using var fixture = Nested();
        fixture.Bytes.Position = 0;
        using var document = Pdf.Open(fixture.Bytes);
        Assert.Equal(new PdfPageSize(400, 200), document.Pages[0].Size);
        Assert.Equal(PdfPageRotation.Clockwise270, document.Pages[0].Rotation);
        document.Pages[0].Size = new PdfPageSize(600, 400);
        document.Pages.Move(1, 0);
        using var output = new MemoryStream();
        document.Save(output, writeOptions: new(PdfWriteMode.Incremental));
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        Assert.Equal(new PdfPageSize(100, 200), reopened.Pages[0].Size);
        Assert.Equal(new PdfPageSize(600, 400), reopened.Pages[1].Size);
        Assert.Equal(PdfPageRotation.Clockwise270, reopened.Pages[1].Rotation);
        Assert.Equal(2, reopened.SourceDocument!.Revisions.Count);
        var page = reopened.Pages[1].Source!.Dictionary;
        Assert.Equal(new[] { 10d, 20d, 310d, 220d }, page.Entries["MediaBox"].Items.Select(x => x.NumberValue));
        Assert.Equal("retained", page.Entries["Owned"].NameValue);
        Assert.Equal(2, page.Entries["UserUnit"].NumberValue);
        Assert.Equal(PdfValueKind.Dictionary, page.Entries["Resources"].Kind);
        Assert.True(page.Entries.ContainsKey("CropBox"));
    }

    [Fact]
    public void CatalogAliasesAndIndirectVersionNamesRetainTheirIdentityAndVersionFloor()
    {
        using var fixture = new SyntaxFixtures("1.2");
        long one = fixture.Object(1, "10 0 R"), two = fixture.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"), three = fixture.Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 200] /Resources << >> >>"), ten = fixture.Object(10, "<< /Type 11 0 R /Pages 2 0 R /Version 12 0 R >>"), eleven = fixture.Object(11, "/Catalog"), twelve = fixture.Object(12, "/1.7");
        fixture.Classic(new[] { (0, 65535, 0L, true), (1, 0, one, false), (2, 0, two, false), (3, 0, three, false), (10, 0, ten, false), (11, 0, eleven, false), (12, 0, twelve, false) }, "/Root 1 0 R");
        fixture.Bytes.Position = 0;
        using var document = Pdf.Open(fixture.Bytes);
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        var syntax = PdfSyntax.Read(output);
        Assert.Equal("1.7", syntax.Version);
        Assert.Equal(new PdfObjectId(10), syntax.Resolve(new(1)).ReferenceId);
        using var untouched = new MemoryStream();
        Assert.Equal("Writer.VersionDowngrade", Assert.Throws<PdfException>(() => document.Save(untouched, writeOptions: new(version: "1.4"))).Diagnostic.Code);
        Assert.Equal(0, untouched.Length);
    }

    [Fact]
    public void UnchangedManagedIncrementalSaveIsExactAndUnsupportedWriterChoicesFailEarly()
    {
        using var source = WriterTests.Create();
        byte[] bytes = WriterTests.Save(source);
        using var input = new MemoryStream(bytes);
        using var document = Pdf.Open(input);
        using var output = new MemoryStream();
        document.Save(output, writeOptions: new(PdfWriteMode.Incremental));
        Assert.Equal(bytes, output.ToArray());
        using var custom = Pdf.Create(providers: new PdfProviderRegistry(new ContractProvider())).AddPage();
        using var untouched = new MemoryStream();
        Assert.Equal("Writer.OptionsUnsupported", Assert.Throws<PdfException>(() => custom.Save(untouched, writeOptions: new(PdfWriteMode.Incremental))).Diagnostic.Code);
        Assert.Equal(0, untouched.Length);
    }

    [Theory]
    [InlineData("/Type /Pages /Kids [2 0 R] /Count 1", "Reader.PageTreeCycle")]
    [InlineData("/Type /Pages /Kids [] /Count 1", "Reader.PageCountMismatch")]
    [InlineData("/Type /Page /MediaBox [0 0 100 100]", "Reader.InvalidPageTree")]
    public void MalformedPageTreesFailPrecisely(string pages, string code)
    {
        using var fixture = new SyntaxFixtures();
        long root = fixture.Object(1, "<< /Type /Catalog /Pages 2 0 R >>"), tree = fixture.Object(2, "<< " + pages + " >>");
        fixture.Classic(new[] { (0, 65535, 0L, true), (1, 0, root, false), (2, 0, tree, false) }, "/Root 1 0 R");
        fixture.Bytes.Position = 0;
        Assert.Equal(code, Assert.Throws<PdfException>(() => Pdf.Open(fixture.Bytes)).Diagnostic.Code);
    }

    [Fact]
    public void EmptyDocumentsAndEncryptedPageReadingRequireExplicitSupportedBehavior()
    {
        using var empty = Pdf.Create();
        using var output = new MemoryStream();
        Assert.Equal("Writer.EmptyDocument", Assert.Throws<PdfException>(() => empty.Save(output)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        using var fixture = new SyntaxFixtures();
        long catalog = fixture.Object(1, "<< /Type /Catalog >>"), encryption = fixture.Object(2, "<< /Filter /Standard >>");
        fixture.Classic(new[] { (0, 65535, 0L, true), (1, 0, catalog, false), (2, 0, encryption, false) }, "/Root 1 0 R /Encrypt 2 0 R");
        fixture.Bytes.Position = 0;
        Assert.Equal("Reader.EncryptionProviderRequired", Assert.Throws<PdfException>(() => Pdf.Open(fixture.Bytes)).Diagnostic.Code);
    }

    private static SyntaxFixtures Nested()
    {
        var fixture = new SyntaxFixtures();
        long one = fixture.Object(1, "<< /Type /Catalog /Pages 2 0 R >>"), two = fixture.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 2 /MediaBox [10 20 210 120] /CropBox [20 30 190 110] /Resources << >> /Rotate -90 >>"), three = fixture.Object(3, "<< /Type /Pages /Parent 2 0 R /Kids [4 0 R 5 0 R] /Count 2 >>"), four = fixture.Object(4, "<< /Type /Page /Parent 3 0 R /UserUnit 2 /Owned /retained >>"), five = fixture.Object(5, "<< /Type /Page /Parent 3 0 R /MediaBox [0 0 100 200] /Rotate 90 >>");
        fixture.Classic(new[] { (0, 65535, 0L, true), (1, 0, one, false), (2, 0, two, false), (3, 0, three, false), (4, 0, four, false), (5, 0, five, false) }, "/Root 1 0 R");
        return fixture;
    }

    private static byte[] Copy(Stream input)
    {
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }
}
