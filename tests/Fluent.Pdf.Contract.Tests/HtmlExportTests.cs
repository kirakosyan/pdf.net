using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Fluent.Pdf.Formats;
using Xunit;
namespace Fluent.Pdf.Contract.Tests;
public sealed class HtmlExportTests
{
    [Fact]
    public void FixedHtmlContainsOrderedOfflinePngPagesAndExplicitRasterizationReports()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(20, 10)).AddPage(new PdfPageSize(10, 20)); var renderer = new RasterViewTests.PatternRenderer(); using var output = new MemoryStream(); var report = PdfHtmlExport.Write(source.CreateSnapshot(), new[] { 1, 0, 1 }, output, new(PdfHtmlExportMode.FixedRaster, "Pages <1>", "en-US", new(72, 72)), new(renderer)); Assert.Equal(3, renderer.Calls); Assert.Equal(3, report.Diagnostics.Count(d => d.Code == "HtmlExport.RasterPage")); Assert.Throws<ObjectDisposedException>(() => renderer.Context!.Checkpoint()); Assert.True(output.CanWrite);
        string markup = Encoding.UTF8.GetString(output.ToArray()); var xml = XDocument.Parse(markup); Assert.Equal("html", xml.DocumentType!.Name); Assert.Empty(xml.Descendants("script")); Assert.Empty(xml.Descendants("a")); Assert.Equal("Pages <1>", xml.Descendants("title").Single().Value); Assert.Contains("default-src 'none'", (string?)xml.Descendants("meta").Single(e => e.Attribute("http-equiv") != null).Attribute("content")); var pages = xml.Descendants("section").ToArray(); Assert.Equal(new[] { "1", "0", "1" }, pages.Select(p => (string?)p.Attribute("data-source-page"))); Assert.Equal("width:10pt;height:20pt", (string?)pages[0].Attribute("style")); Assert.All(xml.Descendants("img"), image => Assert.StartsWith("data:image/png;base64,", (string?)image.Attribute("src")));
        using var csharp = new MemoryStream(); Assert.True(ContractExamples.CSharp.ContractExample.WriteRasterHtml(source.CreateSnapshot(), csharp, new(new RasterViewTests.PatternRenderer())).Succeeded); using var vb = new MemoryStream(); Assert.True(ContractExamples.VisualBasic.ContractExample.WriteRasterHtml(source.CreateSnapshot(), vb, new(new RasterViewTests.PatternRenderer())).Succeeded);
        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures"); Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, "fixed-pages.html"), output.ToArray());
    }
    [Fact]
    public void InferredTextRequiresDeclaredLossAuthorityAndNeverBecomesActiveMarkup()
    {
        const string text = "</pre><script>x</script>&"; using var svg = new MemoryStream(Encoding.UTF8.GetBytes("<svg width='400' height='80'><text x='4' y='20' font-size='10'>" + System.Security.SecurityElement.Escape(text) + "</text></svg>")); using var source = PdfSvg.Import(svg, new(fonts: new(TextTests.Read("business.ttf"))), new(new PdfSimpleTextShaper())); var options = new PdfHtmlExportOptions(PdfHtmlExportMode.InferredText, "Literal text", "en"); using var denied = new MemoryStream(); denied.WriteByte(42); Assert.Equal("HtmlExport.InferredText", Assert.Throws<PdfException>(() => PdfHtmlExport.Write(source.CreateSnapshot(), new[] { 0 }, denied, options)).Diagnostic.Code); Assert.Equal(new byte[] { 42 }, denied.ToArray());
        using var output = new MemoryStream(); var report = PdfHtmlExport.Write(source.CreateSnapshot(), new[] { 0 }, output, options, policy: new(PdfProcessingMode.Permissive)); Assert.Contains(report.Diagnostics, d => d.LossCategory == PdfLossCategory.FidelityReduction && d.Severity == PdfDiagnosticSeverity.Warning); var xml = XDocument.Parse(Encoding.UTF8.GetString(output.ToArray())); Assert.Empty(xml.Descendants("script")); Assert.Equal(text, xml.Descendants("pre").Single().Value); Assert.Empty(xml.Descendants("img")); File.WriteAllBytes(Path.Combine(AppContext.BaseDirectory, "format-fixtures", "inferred-text.html"), output.ToArray());
    }
    [Fact]
    public void MissingRenderersBudgetsAndCancellationCannotPublishPartialHtml()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(20, 10)); var options = new PdfHtmlExportOptions(PdfHtmlExportMode.FixedRaster, "Pages", "en"); using var output = new MemoryStream(); Assert.Throws<PdfProviderUnavailableException>(() => PdfHtmlExport.Write(source.CreateSnapshot(), new[] { 0 }, output, options)); Assert.Equal(0, output.Length);
        var renderer = new RasterViewTests.PatternRenderer(); Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfHtmlExport.Write(source.CreateSnapshot(), new[] { 0 }, output, options, new(renderer), new(limits: new(maximumObjects: 1)))).Diagnostic.Code); Assert.Equal(0, output.Length);
        using var cancelled = new CancellationTokenSource(); renderer.BeforeRender = c => { cancelled.Cancel(); c.Checkpoint(); }; Assert.Throws<OperationCanceledException>(() => PdfHtmlExport.Write(source.CreateSnapshot(), new[] { 0 }, output, options, new(renderer), cancellationToken: cancelled.Token)); Assert.Equal(0, output.Length);
        Assert.Throws<ArgumentException>(() => new PdfHtmlExportOptions(PdfHtmlExportMode.FixedRaster, "x", "en' onclick='x")); Assert.Throws<ArgumentException>(() => new PdfHtmlExportOptions(PdfHtmlExportMode.FixedRaster, "x", "en-US-US")); Assert.Equal("zh-Hant-TW", new PdfHtmlExportOptions(PdfHtmlExportMode.FixedRaster, "x", "zh-Hant-TW").Language); Assert.Throws<ArgumentException>(() => new PdfHtmlExportOptions(PdfHtmlExportMode.FixedRaster, "x", "en", new(region: new(0, 0, 1, 1))));
    }
}
