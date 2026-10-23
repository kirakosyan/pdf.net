using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Fluent.Pdf.Formats;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class XpsTests
{
    private static MemoryStream Package(PdfDocumentSnapshot source, PdfXpsFormat format, RasterViewTests.PatternRenderer? renderer = null)
    {
        var output = new MemoryStream();
        PdfXps.WriteRasterPages(source, new[] { 1, 0, 1 }, output, format, "en", new(72, 72), new(renderer ?? new()));
        output.Position = 0;
        return output;
    }

    private static Dictionary<string, byte[]> Parts(Stream source)
    {
        var parts = new Dictionary<string, byte[]>(StringComparer.Ordinal);
        using var zip = new ZipArchive(source, ZipArchiveMode.Read, true);
        foreach (var entry in zip.Entries)
        {
            using var input = entry.Open();
            using var output = new MemoryStream();
            input.CopyTo(output);
            parts.Add(entry.FullName, output.ToArray());
        }

        return parts;
    }

    private static MemoryStream Repack(Dictionary<string, byte[]> parts)
    {
        var result = new MemoryStream();
        using (var zip = new ZipArchive(result, ZipArchiveMode.Create, true))
            foreach (var part in parts)
            {
                using var output = zip.CreateEntry(part.Key).Open();
                output.Write(part.Value, 0, part.Value.Length);
            }

        result.Position = 0;
        return result;
    }

    [Theory]
    [InlineData(PdfXpsFormat.Xps)]
    [InlineData(PdfXpsFormat.OpenXps)]
    public void RasterPackagesPreserveOrderGeometryAndDecodedSamplesAcrossNamespaces(PdfXpsFormat format)
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(21, 10.5)).AddPage(new PdfPageSize(10.5, 21));
        var renderer = new RasterViewTests.PatternRenderer();
        using var package = Package(source.CreateSnapshot(), format, renderer);
        Assert.Equal(3, renderer.Calls);
        Assert.Throws<ObjectDisposedException>(() => renderer.Context!.Checkpoint());
        using var imported = PdfXps.ImportRasterPages(package, format);
        Assert.True(package.CanRead);
        package.Position = 0;
        using var csharp = ContractExamples.CSharp.ContractExample.ImportRasterXps(package, format);
        package.Position = 0;
        using var vb = ContractExamples.VisualBasic.ContractExample.ImportRasterXps(package, format);
        Assert.Equal(3, csharp.Pages.Count);
        Assert.Equal(3, vb.Pages.Count);
        Assert.Equal(3, imported.Pages.Count);
        Assert.Equal(new[] { new PdfPageSize(10.5, 21), new(21, 10.5), new(10.5, 21) }, imported.Pages.Select(p => p.Size));
        for (int i = 0; i < 3; i++)
        {
            var graphics = PdfPageGraphics.Inspect(imported.CreateSnapshot(), i);
            int index = Enumerable.Range(0, graphics.Occurrences.Count).Single(n => graphics.Occurrences[n].Kind == PdfGraphicsOccurrenceKind.Image);
            var image = graphics.ExtractImage(index);
            using var pixels = image.OpenSamples();
            Assert.Equal(i == 1 ? 0 : 40, pixels.ReadByte());
            Assert.Equal(0, pixels.ReadByte());
            Assert.Equal(0, pixels.ReadByte());
        }

        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures");
        Directory.CreateDirectory(directory);
        string stem = format == PdfXpsFormat.Xps ? "raster-pages.xps" : "raster-pages.oxps";
        File.WriteAllBytes(Path.Combine(directory, stem), package.ToArray());
        imported.SaveFile(Path.Combine(directory, stem + ".pdf"));
    }

    [Theory]
    [InlineData("glyph", "Xps.UnsupportedConstruct")]
    [InlineData("dictionary", "Xps.UnsupportedConstruct")]
    [InlineData("crop", "Xps.Geometry")]
    [InlineData("uri", "Format.PackageUri")]
    [InlineData("ticket", "Xps.PrintTicketUnsupported")]
    public void UnsupportedFixedPageContentAndResourcesCannotDisappear(string change, string code)
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(21, 10.5)).AddPage(new PdfPageSize(10.5, 21));
        using var original = Package(source.CreateSnapshot(), PdfXpsFormat.Xps);
        var parts = Parts(original);
        XNamespace ns = "http://schemas.microsoft.com/xps/2005/06";
        string path = change == "ticket" ? "[Content_Types].xml" : "Documents/1/Pages/1.fpage";
        var xml = XDocument.Parse(Encoding.UTF8.GetString(parts[path]));
        if (change == "glyph")
            xml.Root!.Add(new XElement(ns + "Glyphs"));
        else if (change == "dictionary")
            xml.Root!.Add(new XElement(ns + "FixedPage.Resources", new XElement(ns + "ResourceDictionary")));
        else if (change == "crop")
            xml.Descendants(ns + "ImageBrush").Single().SetAttributeValue("Viewbox", "1,0,9,20");
        else if (change == "uri")
            xml.Descendants(ns + "ImageBrush").Single().SetAttributeValue("ImageSource", "https://not-fetched.invalid/image.png");
        else
        {
            XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types";
            xml.Root!.Add(new XElement(ct + "Override", new XAttribute("PartName", "/Metadata/PrintTicket.xml"), new XAttribute("ContentType", "application/vnd.ms-printing.printticket+xml")));
            parts.Add("Metadata/PrintTicket.xml", Encoding.UTF8.GetBytes("<PrintTicket/>"));
        }

        parts[path] = Encoding.UTF8.GetBytes(xml.ToString());
        using var modified = Repack(parts);
        Assert.Equal(code, Assert.Throws<PdfException>(() => PdfXps.ImportRasterPages(modified, PdfXpsFormat.Xps)).Diagnostic.Code);
        Assert.True(modified.CanRead);
    }

    [Fact]
    public void VersionAvailabilityAndResourcePoliciesFailBeforePartialPackageWrites()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(21, 10.5)).AddPage(new PdfPageSize(10.5, 21));
        using var package = Package(source.CreateSnapshot(), PdfXpsFormat.Xps);
        Assert.Equal("Format.PackageProfile", Assert.Throws<PdfException>(() => PdfXps.ImportRasterPages(package, PdfXpsFormat.OpenXps)).Diagnostic.Code);
        using var output = new MemoryStream();
        output.WriteByte(42);
        Assert.Throws<PdfProviderUnavailableException>(() => PdfXps.WriteRasterPages(source.CreateSnapshot(), new[] { 0 }, output, PdfXpsFormat.Xps, "en", new(), PdfProviderRegistry.Empty));
        var renderer = new RasterViewTests.PatternRenderer();
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfXps.WriteRasterPages(source.CreateSnapshot(), new[] { 0 }, output, PdfXpsFormat.Xps, "en", new(), new(renderer), new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        Assert.Equal(0, renderer.Calls);
        using var cancelled = new CancellationTokenSource();
        renderer.BeforeRender = c =>
        {
            cancelled.Cancel();
            c.Checkpoint();
        };
        Assert.Throws<OperationCanceledException>(() => PdfXps.WriteRasterPages(source.CreateSnapshot(), new[] { 0 }, output, PdfXpsFormat.Xps, "en", new(), new(renderer), cancellationToken: cancelled.Token));
        Assert.Equal(new byte[] { 42 }, output.ToArray());
    }

    [Fact]
    public void FractionalPageUnitsNeedDeclaredLossAuthorityInsteadOfSilentGeometryChanges()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(20, 10));
        var renderer = new RasterViewTests.PatternRenderer();
        using var denied = new MemoryStream();
        denied.WriteByte(42);
        Assert.Equal("Xps.PageGeometryQuantized", Assert.Throws<PdfException>(() => PdfXps.WriteRasterPages(source.CreateSnapshot(), new[] { 0 }, denied, PdfXpsFormat.Xps, "en", new(72, 72), new(renderer))).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, denied.ToArray());
        Assert.Equal(0, renderer.Calls);
        using var accepted = new MemoryStream();
        var report = PdfXps.WriteRasterPages(source.CreateSnapshot(), new[] { 0 }, accepted, PdfXpsFormat.Xps, "en", new(72, 72), new(renderer), new(PdfProcessingMode.Permissive));
        Assert.Contains(report.Diagnostics, d => d.Code == "Xps.PageGeometryQuantized" && d.LossCategory == PdfLossCategory.FidelityReduction);
        accepted.Position = 0;
        using var imported = PdfXps.ImportRasterPages(accepted, PdfXpsFormat.Xps);
        Assert.Equal(new PdfPageSize(20.25, 10.5), imported.Pages[0].Size);
    }
}
