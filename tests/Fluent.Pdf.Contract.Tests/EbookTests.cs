using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Fluent.Pdf.Formats;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class EbookTests
{
    internal static PdfEbookMetadata Metadata() => new("urn:uuid:11111111-2222-4333-8444-555555555555", "Original & literal", "en", "2026-10-20T00:00:00Z");
    private static PdfOperationPolicy Losses() => new(PdfProcessingMode.Permissive);
    private static PdfDocument TextSource()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("<svg width='400' height='80'><text x='4' y='20' font-size='10'>&lt;/pre&gt;&lt;script&gt;x&lt;/script&gt;&amp;</text></svg>"));
        return PdfSvg.Import(input, new(fonts: new(TextTests.Read("business.ttf"))), new(new PdfSimpleTextShaper()));
    }

    private static MemoryStream TextBook(PdfDocumentSnapshot source)
    {
        var output = new MemoryStream();
        PdfEbooks.WriteEpub(source, new[] { 0, 0 }, output, Metadata(), new(PdfEbookLayout.ReflowText), policy: Losses());
        output.Position = 0;
        return output;
    }

    private static void Save(string name, byte[] bytes)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, name), bytes);
    }

    [Fact]
    public void FixedEpubHasStoredFirstMimetypeExplicitMetadataNavigationAndOrderedPngPages()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(20, 10)).AddPage(new PdfPageSize(10, 20));
        var renderer = new RasterViewTests.PatternRenderer();
        using var output = new MemoryStream();
        var report = PdfEbooks.WriteEpub(source.CreateSnapshot(), new[] { 1, 0, 1 }, output, Metadata(), new(PdfEbookLayout.FixedRaster, rightToLeft: true, rendering: new(72, 72)), new(renderer));
        Assert.True(output.CanWrite);
        Assert.Equal(3, renderer.Calls);
        Assert.Equal(3, report.Diagnostics.Count(d => d.Code == "Ebook.RasterPage"));
        Assert.Throws<ObjectDisposedException>(() => renderer.Context!.Checkpoint());
        output.Position = 0;
        using (var package = new ZipArchive(output, ZipArchiveMode.Read, true))
        {
            Assert.Equal("mimetype", package.Entries[0].FullName);
            XNamespace opf = "http://www.idpf.org/2007/opf", dc = "http://purl.org/dc/elements/1.1/";
            using var input = package.GetEntry("EPUB/package.opf")!.Open();
            var xml = XDocument.Load(input);
            Assert.Equal(Metadata().Identifier, xml.Descendants(dc + "identifier").Single().Value);
            Assert.Equal("rtl", (string? )xml.Descendants(opf + "spine").Single().Attribute("page-progression-direction"));
            Assert.Equal(new[] { "page1", "page2", "page3" }, xml.Descendants(opf + "itemref").Select(e => (string? )e.Attribute("idref")));
            Assert.Single(xml.Descendants(opf + "item"), e => (string? )e.Attribute("properties") == "nav");
        }

        Save("fixed-book.epub", output.ToArray());
        output.Position = 0;
        Assert.Equal("Ebook.Layout", Assert.Throws<PdfException>(() => PdfEbooks.ImportReflowText(output, new(GenerationTests.Style()), new(new PdfSimpleTextShaper()), Losses())).Diagnostic.Code);
    }

    [Fact]
    public void ReflowEpubAndMobiXmlKeepLiteralTextWithoutActiveMarkupOrExecutableCode()
    {
        using var source = TextSource();
        using var book = TextBook(source.CreateSnapshot());
        Save("reflow-book.epub", book.ToArray());
        var typography = new PdfFormatTextOptions(new(new(TextTests.Read("business.ttf")), 9));
        var providers = new PdfProviderRegistry(new PdfSimpleTextShaper());
        Assert.Equal("Ebook.TextReconstruction", Assert.Throws<PdfException>(() => PdfEbooks.ImportReflowText(book, typography, providers)).Diagnostic.Code);
        book.Position = 0;
        using var csharp = ContractExamples.CSharp.ContractExample.ImportEpubText(book, typography, providers, Losses());
        book.Position = 0;
        using var vb = ContractExamples.VisualBasic.ContractExample.ImportEpubText(book, typography, providers, Losses());
        Assert.NotEmpty(csharp.Pages);
        Assert.NotEmpty(vb.Pages);
        book.Position = 0;
        using var imported = PdfEbooks.ImportReflowText(book, typography, providers, Losses());
        Assert.True(book.CanRead);
        string text = string.Concat(Enumerable.Range(0, imported.Pages.Count).Select(page => PdfTextExtraction.ReadPage(imported.CreateSnapshot(), page).Text));
        Assert.Equal(2, text.Split(new[] { "</pre><script>x</script>&" }, StringSplitOptions.None).Length - 1);
        Assert.Contains(imported.LastOperationReport!.Diagnostics, d => d.Code == "Ebook.TextReconstruction");
        using var output = new MemoryStream();
        PdfEbooks.WriteMobiSource(source.CreateSnapshot(), new[] { 0, 0 }, output, Metadata(), policy: Losses());
        var xml = XDocument.Parse(Encoding.UTF8.GetString(output.ToArray()));
        Assert.Empty(xml.Descendants("script"));
        Assert.Equal(2, xml.Descendants("pre").Count());
        Assert.All(xml.Descendants("pre"), p => Assert.Equal("</pre><script>x</script>&", p.Value));
        Assert.Single(xml.Descendants(XName.Get("pagebreak", "http://mobipocket.com/ns/mbp")));
        Save("book-source.xml", output.ToArray());
    }

    [Theory]
    [InlineData("drm", "Ebook.EncryptedResources")]
    [InlineData("script", "Ebook.UnsupportedMarkup")]
    [InlineData("external", "Format.PackageUri")]
    [InlineData("entity", "Format.Xml")]
    public void UnsupportedBookResourcesAreNeverExecutedOrSilentlyReconstructed(string change, string code)
    {
        using var source = TextSource();
        using var original = TextBook(source.CreateSnapshot());
        var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using (var zip = new ZipArchive(original, ZipArchiveMode.Read, true))
            foreach (var entry in zip.Entries)
            {
                using var input = entry.Open();
                using var captured = new MemoryStream();
                input.CopyTo(captured);
                parts.Add(entry.FullName, captured.ToArray());
            }

        string name = change == "drm" ? "META-INF/encryption.xml" : change == "external" ? "EPUB/package.opf" : "EPUB/page1.xhtml";
        string xml;
        if (change == "drm")
            xml = "<encryption/>";
        else if (change == "entity")
            xml = "<!DOCTYPE html [<!ENTITY ext SYSTEM 'file:///not-read'>]><html xmlns='http://www.w3.org/1999/xhtml'><body>&ext;</body></html>";
        else
        {
            var doc = XDocument.Parse(Encoding.UTF8.GetString(parts[name]));
            if (change == "external")
                doc.Descendants(XName.Get("item", "http://www.idpf.org/2007/opf")).First().SetAttributeValue("href", "https://not-fetched.invalid/a.xhtml");
            else
                doc.Descendants(XName.Get("body", "http://www.w3.org/1999/xhtml")).Single().Add(new XElement(XName.Get("script", "http://www.w3.org/1999/xhtml"), "while(true){}"));
            xml = doc.ToString();
        }

        parts[name] = Encoding.UTF8.GetBytes(xml);
        using var modified = new MemoryStream();
        using (var zip = new ZipArchive(modified, ZipArchiveMode.Create, true))
            foreach (var part in parts)
            {
                using var output = zip.CreateEntry(part.Key).Open();
                output.Write(part.Value, 0, part.Value.Length);
            }

        modified.Position = 0;
        Assert.Equal(code, Assert.Throws<PdfException>(() => PdfEbooks.ImportReflowText(modified, new(GenerationTests.Style()), new(new PdfSimpleTextShaper()), Losses())).Diagnostic.Code);
        Assert.True(modified.CanRead);
    }

    [Fact]
    public void StrictLossPolicyMissingRenderersCancellationAndPackageBombsPreventPublication()
    {
        using var source = TextSource();
        using var output = new MemoryStream();
        output.WriteByte(42);
        Assert.Equal("Ebook.TextReconstruction", Assert.Throws<PdfException>(() => PdfEbooks.WriteEpub(source.CreateSnapshot(), new[] { 0 }, output, Metadata(), new(PdfEbookLayout.ReflowText))).Diagnostic.Code);
        Assert.Throws<PdfProviderUnavailableException>(() => PdfEbooks.WriteEpub(source.CreateSnapshot(), new[] { 0 }, output, Metadata(), new(PdfEbookLayout.FixedRaster)));
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        var renderer = new RasterViewTests.PatternRenderer();
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfEbooks.WriteEpub(source.CreateSnapshot(), new[] { 0 }, output, Metadata(), new(PdfEbookLayout.FixedRaster), new(renderer), new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        Assert.Equal(0, renderer.Calls);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => PdfEbooks.WriteEpub(source.CreateSnapshot(), new[] { 0 }, output, Metadata(), new(PdfEbookLayout.ReflowText), policy: Losses(), cancellationToken: cancelled.Token));
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        using var original = TextBook(source.CreateSnapshot());
        byte[] bomb = original.ToArray();
        int entry = -1;
        for (int i = 0; i < bomb.Length - 46; i++)
            if (bomb[i] == 80 && bomb[i + 1] == 75 && bomb[i + 2] == 1 && bomb[i + 3] == 2)
            {
                entry = i;
                break;
            }

        Assert.True(entry >= 0);
        for (int i = 24; i < 28; i++)
            bomb[entry + i] = 255;
        using var malicious = new MemoryStream(bomb);
        Assert.Equal("Format.PackageLimits", Assert.Throws<PdfException>(() => PdfEbooks.ImportReflowText(malicious, new(GenerationTests.Style()), new(new PdfSimpleTextShaper()), Losses())).Diagnostic.Code);
        Assert.Throws<ArgumentException>(() => new PdfEbookMetadata("id", "title", "en", "2026-02-30T00:00:00Z"));
        Assert.Throws<ArgumentException>(() => new PdfEbookOptions(PdfEbookLayout.FixedRaster, rendering: new(region: new(0, 0, 1, 1))));
    }

    [Theory]
    [InlineData(false, "Format.PackageIntegrity")]
    [InlineData(true, "Format.PackageHeader")]
    public void CorruptedPayloadsAndConflictingZipHeaderCrcRejectBeforeBookParsing(bool header, string code)
    {
        using var source = TextSource();
        using var book = TextBook(source.CreateSnapshot());
        byte[] bytes = book.ToArray();
        if (header)
        {
            int central = -1;
            for (int i = 0; i < bytes.Length - 46; i++)
                if (bytes[i] == 80 && bytes[i + 1] == 75 && bytes[i + 2] == 1 && bytes[i + 3] == 2)
                {
                    central = i;
                    break;
                }

            Assert.True(central >= 0);
            bytes[central + 16] ^= 1;
        }
        else
        {
            int offset = 30 + bytes[26] + 256 * bytes[27] + bytes[28] + 256 * bytes[29];
            bytes[offset] ^= 1;
        }

        using var corrupted = new MemoryStream(bytes);
        Assert.Equal(code, Assert.Throws<PdfException>(() => PdfEbooks.ImportReflowText(corrupted, new(GenerationTests.Style()), new(new PdfSimpleTextShaper()), Losses())).Diagnostic.Code);
    }
}
