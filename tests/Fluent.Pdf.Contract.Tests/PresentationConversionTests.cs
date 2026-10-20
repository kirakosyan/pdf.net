using System.IO;
using System.IO.Compression;
using System.Xml.Linq;
using Fluent.Pdf.Formats;
using Xunit;
namespace Fluent.Pdf.Contract.Tests;
public sealed class PresentationConversionTests
{
    internal static MemoryStream Deck(PdfDocumentSnapshot source, int[] pages, RasterViewTests.PatternRenderer? renderer = null)
    { var output = new MemoryStream(); PdfPresentations.WriteRasterSlides(source, pages, new(72, 72), new(72, 72), new(renderer ?? new()), output); output.Position = 0; return output; }
    [Fact]
    public void RasterSlidesKeepOrderGeometryPixelsAndOriginalSnapshot()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(72, 36)).AddPage(new PdfPageSize(36, 36)); var snapshot = source.CreateSnapshot(); var renderer = new RasterViewTests.PatternRenderer(); using var deck = Deck(snapshot, new[] { 1, 0, 1 }, renderer); Assert.Equal(3, renderer.Calls); Assert.True(deck.CanRead); using var imported = PdfPresentations.ImportRasterSlides(deck); deck.Position = 0; using var csharp = ContractExamples.CSharp.ContractExample.ImportRasterSlides(deck); deck.Position = 0; using var vb = ContractExamples.VisualBasic.ContractExample.ImportRasterSlides(deck); Assert.Equal(3, csharp.Pages.Count); Assert.Equal(3, vb.Pages.Count); Assert.Equal(3, imported.Pages.Count); Assert.All(imported.Pages, p => Assert.Equal(new PdfPageSize(72, 72), p.Size)); Assert.Equal(2, snapshot.Pages.Count);
        for (int i = 0; i < 3; i++) { var graphics = PdfPageGraphics.Inspect(imported.CreateSnapshot(), i); int occurrence = Enumerable.Range(0, graphics.Occurrences.Count).Single(index => graphics.Occurrences[index].Kind == PdfGraphicsOccurrenceKind.Image); var image = graphics.ExtractImage(occurrence); using var pixels = image.OpenSamples(); var pixel = new byte[3]; int at = 0; while (at < pixel.Length) { int n = pixels.Read(pixel, at, pixel.Length - at); Assert.True(n > 0); at += n; } Assert.Equal(i == 1 ? 0 : 40, pixel[0]); }
        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures"); Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, "raster-slides.pptx"), deck.ToArray()); imported.SaveFile(Path.Combine(directory, "raster-slides.pdf"));
    }
    [Theory]
    [InlineData("effect")]
    [InlineData("crop")]
    [InlineData("external")]
    [InlineData("text")]
    public void UnsupportedPresentationContentRejectsInsteadOfDisappearing(string change)
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(72, 36)); using var original = Deck(source.CreateSnapshot(), new[] { 0 }); using var modified = new MemoryStream(); original.CopyTo(modified); modified.Position = 0;
        using (var zip = new ZipArchive(modified, ZipArchiveMode.Update, true)) { string name = change == "external" ? "ppt/slides/_rels/slide1.xml.rels" : "ppt/slides/slide1.xml"; var entry = zip.GetEntry(name)!; XDocument xml; using (var input = entry.Open()) xml = XDocument.Load(input); XNamespace p = "http://schemas.openxmlformats.org/presentationml/2006/main", a = "http://schemas.openxmlformats.org/drawingml/2006/main"; if (change == "external") xml.Root!.Elements().First().SetAttributeValue("TargetMode", "External"); else if (change == "text") xml.Descendants(p + "spTree").Single().Add(new XElement(p + "sp")); else if (change == "crop") xml.Descendants(p + "blipFill").Single().Add(new XElement(a + "srcRect")); else xml.Descendants(p + "spPr").Single().Add(new XElement(a + "effectLst")); entry.Delete(); using var output = zip.CreateEntry(name).Open(); xml.Save(output); }
        modified.Position = 0; Assert.Throws<PdfException>(() => PdfPresentations.ImportRasterSlides(modified)); Assert.True(modified.CanRead);
    }
    [Fact]
    public void RenderingAndPackageLimitsPreventPartialPublication()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(72, 36)); using var output = new MemoryStream(); output.WriteByte(42); var renderer = new RasterViewTests.PatternRenderer { BeforeRender = c => c.RejectLimit(PdfResourceKind.Pixels) }; Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfPresentations.WriteRasterSlides(source.CreateSnapshot(), new[] { 0 }, new(72, 72), new(72, 72), new(renderer), output)).Diagnostic.Code); Assert.Equal(new byte[] { 42 }, output.ToArray()); Assert.Throws<ArgumentException>(() => PdfPresentations.WriteRasterSlides(source.CreateSnapshot(), new[] { 0 }, new(72, 72), new(region: new(0, 0, 1, 1)), new(new RasterViewTests.PatternRenderer()), output));
    }
}
