using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml.Linq;
using Fluent.Pdf.Formats;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class OfdTests
{
    private static MemoryStream Fixture()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(72, 36)).AddPage(new PdfPageSize(36, 72));
        var output = new MemoryStream();
        PdfOfd.WriteRasterPages(source.CreateSnapshot(), new[] { 1, 0, 1 }, output, new("0123456789ABCDEF0123456789ABCDEF", "Literal <title>", "2026-10-20"), new(72, 72), new(new RasterViewTests.PatternRenderer()));
        output.Position = 0;
        return output;
    }

    [Fact]
    public void OwnedResourcesPageOrderGeometryAndLiteralMetadataRoundTrip()
    {
        using var package = Fixture();
        using var doc = PdfOfd.ImportRasterPages(package, out var identity);
        Assert.Equal("Literal <title>", identity.Title);
        Assert.Equal("2026-10-20", identity.CreationDate);
        Assert.Equal("0123456789ABCDEF0123456789ABCDEF", identity.DocumentId);
        Assert.Equal(new[] { new PdfPageSize(36, 72), new(72, 36), new(36, 72) }, doc.Pages.Select(p => p.Size));
        Assert.True(package.CanRead);
        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "raster-pages.ofd"), package.ToArray());
        doc.SaveFile(Path.Combine(directory, "raster-pages.ofd.pdf"));
    }

    [Theory]
    [InlineData("signature", "Ofd.SignatureUnsupported")]
    [InlineData("extension", "Ofd.ExtensionUnsupported")]
    [InlineData("owner", "Ofd.Ownership")]
    [InlineData("transform", "Ofd.Geometry")]
    public void SignaturesExtensionsOwnershipAndPartialImageTransformsReject(string change, string code)
    {
        using var original = Fixture();
        var parts = new Dictionary<string, byte[]>();
        using (var zip = new ZipArchive(original, ZipArchiveMode.Read, true))
            foreach (var entry in zip.Entries)
            {
                using var source = entry.Open();
                using var bytes = new MemoryStream();
                source.CopyTo(bytes);
                parts.Add(entry.FullName, bytes.ToArray());
            }

        XNamespace ns = "http://www.ofdspec.org/2016";
        string name = change == "signature" ? "OFD.xml" : change == "owner" ? "Doc_0/Res.xml" : "Doc_0/Pages/1/Content.xml";
        var xml = XDocument.Parse(Encoding.UTF8.GetString(parts[name]));
        if (change == "signature")
            xml.Descendants(ns + "DocBody").Single().Add(new XElement(ns + "Signatures", "Signs/Signatures.xml"));
        else if (change == "extension")
            xml.Root!.Add(new XElement(ns + "Actions"));
        else if (change == "owner")
            xml.Descendants(ns + "MediaFile").First().Value = "../../outside.png";
        else
            xml.Descendants(ns + "ImageObject").Single().SetAttributeValue("CTM", "1 0 0 1 0 0");
        parts[name] = Encoding.UTF8.GetBytes(xml.ToString());
        using var input = new MemoryStream();
        using (var zip = new ZipArchive(input, ZipArchiveMode.Create, true))
            foreach (var part in parts)
            {
                using var stream = zip.CreateEntry(part.Key).Open();
                stream.Write(part.Value, 0, part.Value.Length);
            }

        input.Position = 0;
        Assert.Equal(code, Assert.Throws<PdfException>(() => PdfOfd.ImportRasterPages(input, out _)).Diagnostic.Code);
    }

    [Fact]
    public void MissingProviderAndBudgetsLeaveOutputUntouched()
    {
        using var doc = Pdf.Create().AddPage(new PdfPageSize(72, 36));
        using var output = new MemoryStream();
        output.WriteByte(42);
        var identity = new PdfOfdIdentity("0123456789ABCDEF0123456789ABCDEF", "Title", "2026-10-20");
        Assert.Throws<PdfProviderUnavailableException>(() => PdfOfd.WriteRasterPages(doc.CreateSnapshot(), new[] { 0 }, output, identity, new(), PdfProviderRegistry.Empty));
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        using var input = Fixture();
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfOfd.ImportRasterPages(input, out _, new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
    }
}
