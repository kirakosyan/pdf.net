using System.IO;
using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class VersionNegotiationTests
{
    [Theory]
    [InlineData("<< /Type /ExtGState /ca .5 >>", "1.4")]
    [InlineData("<< /Type /OCG /Name (layer) >>", "1.5")]
    [InlineData("<< /Type /Page /UserUnit 2 >>", "1.6")]
    [InlineData("<< /Type /ExtGState /UseBlackPtComp /ON >>", "2.0")]
    [InlineData("<< /Type /Namespace /NS (urn:original:test) >>", "2.0")]
    public void KnownStructuralFeaturesNegotiateOrRejectAnExactLowerVersion(string feature, string expected)
    {
        using var source = new SyntaxFixtures("1.2");
        long root = source.Object(1, "<< /Type /Catalog /Feature 2 0 R >>"), item = source.Object(2, feature);
        source.Classic(new[] { (0, 65535, 0L, true), (1, 0, root, false), (2, 0, item, false) }, "/Root 1 0 R", size: 3);
        source.Bytes.Position = 0;
        using var transaction = new PdfTransaction(PdfSyntax.Read(source.Bytes));
        using var output = new MemoryStream();
        transaction.Save(output);
        output.Position = 0;
        Assert.Equal(expected, PdfSyntax.Read(output).Version);
        using var rejected = new MemoryStream();
        Assert.Equal("Writer.VersionTooLow", Assert.Throws<PdfException>(() => transaction.Save(rejected, new(version: "1.2"))).Diagnostic.Code);
        Assert.Equal(0, rejected.Length);
    }

    [Fact]
    public void ContentOperatorAndExplicitCustomFeatureFloorsReachThePageWriter()
    {
        using var source = Pdf.Create().AddPage();
        using var bytes = new MemoryStream();
        source.Save(bytes, writeOptions: new(version: "1.2"));
        bytes.Position = 0;
        using var document = Pdf.Open(bytes);
        document.Pages[0].Content = PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.Shading, PdfValue.Name("Sh")) });
        using var lower = new MemoryStream();
        Assert.Equal("Writer.VersionTooLow", Assert.Throws<PdfException>(() => document.Save(lower, writeOptions: new(version: "1.2"))).Diagnostic.Code);
        Assert.Equal(0, lower.Length);
        using var auto = new MemoryStream();
        document.Save(auto);
        auto.Position = 0;
        Assert.Equal("1.3", PdfSyntax.Read(auto).Version);
        document.Pages[0].Content = PdfContentProgram.Create(new[] { PdfContentOperation.Extension("original_op", "2.0") });
        using var custom = new MemoryStream();
        document.Save(custom, writeOptions: new(PdfWriteMode.Incremental));
        custom.Position = 0;
        var syntax = PdfSyntax.Read(custom);
        Assert.Equal("1.2", syntax.Version);
        Assert.Equal("2.0", syntax.Resolve(syntax.Revisions[0].Trailer.Entries["Root"].ReferenceId).Entries["Version"].NameValue);
    }

    [Fact]
    public void DeveloperExtensionsMergePreserveOpaqueKeysAndCanBeRejectedExplicitly()
    {
        using var original = Pdf.Create().AddPage();
        using var bytes = new MemoryStream();
        original.Save(bytes, writeOptions: new(extensions: new[] { new PdfExtensionRequirement("OWN", "1.7", 2) }));
        bytes.Position = 0;
        var source = PdfSyntax.Read(bytes);
        using var edit = new PdfTransaction(source);
        var root = source.Revisions[0].Trailer.Entries["Root"].ReferenceId;
        var catalog = source.Resolve(root);
        var extension = catalog.Entries["Extensions"].Entries["OWN"];
        var extEntries = extension.Entries.ToDictionary(x => x.Key, x => x.Value);
        extEntries["Opaque"] = PdfValue.String(new byte[] { 0, 255 });
        var catalogEntries = catalog.Entries.ToDictionary(x => x.Key, x => x.Value);
        catalogEntries["Extensions"] = PdfValue.Dictionary(new Dictionary<string, PdfValue> { { "OWN", PdfValue.Dictionary(extEntries) } });
        edit.Set(root, PdfValue.Dictionary(catalogEntries));
        using var merged = new MemoryStream();
        edit.Save(merged, new(extensions: new[] { new PdfExtensionRequirement("OWN", "1.7", 3), new PdfExtensionRequirement("NEW", "2.0", 1) }));
        merged.Position = 0;
        var updated = PdfSyntax.Read(merged);
        Assert.Equal("2.0", updated.Version);
        var extensions = updated.Resolve(root).Entries["Extensions"];
        Assert.Equal("3", extensions.Entries["OWN"].Entries["ExtensionLevel"].NumberText);
        Assert.Equal(new byte[] { 0, 255 }, extensions.Entries["OWN"].Entries["Opaque"].StringBytes);
        using var rejected = new MemoryStream();
        Assert.Equal("Writer.UnknownExtension", Assert.Throws<PdfException>(() => edit.Save(rejected, new(unknownExtensions: PdfUnknownExtensionPolicy.Reject))).Diagnostic.Code);
        Assert.Equal(0, rejected.Length);
        Assert.Equal("Writer.ExtensionDowngrade", Assert.Throws<PdfException>(() => edit.Save(rejected, new(extensions: new[] { new PdfExtensionRequirement("OWN", "1.7", 1) }))).Diagnostic.Code);
        Assert.Equal("Writer.VersionDowngrade", Assert.Throws<PdfException>(() => edit.Save(rejected, new(version: "1.6"))).Diagnostic.Code);
    }

    [Fact]
    public void ExplicitFeatureFloorsAlsoApplyToUnchangedIncrementalSaves()
    {
        using var source = Pdf.Create().AddPage();
        using var bytes = new MemoryStream();
        source.Save(bytes, writeOptions: new(version: "1.2"));
        bytes.Position = 0;
        using var edit = new PdfTransaction(PdfSyntax.Read(bytes));
        using var output = new MemoryStream();
        edit.Save(output, new(PdfWriteMode.Incremental, minimumVersion: "2.0"));
        output.Position = 0;
        var syntax = PdfSyntax.Read(output);
        Assert.Equal(2, syntax.Revisions.Count);
        Assert.Equal("2.0", syntax.Resolve(syntax.Revisions[0].Trailer.Entries["Root"].ReferenceId).Entries["Version"].NameValue);
    }
}
