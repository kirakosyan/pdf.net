using System.IO;
using System.Text;
using System.Xml.Linq;
using Fluent.Pdf.Formats;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class SvgTests
{
    internal const string Fixture = "<svg xmlns='http://www.w3.org/2000/svg' width='200' height='120' viewBox='0 0 200 120'><defs><clipPath id='c'><rect x='10' y='5' width='180' height='110'/></clipPath></defs><g transform='translate(2 3)' clip-path='url(#c)'><path d='M0 0 L200 0 L200 120 L0 120 Z' fill='#aa2200'/><path d='M20 30 C40 5 100 70 150 20 L170 90' fill='none' stroke='blue' stroke-width='3' stroke-dasharray='8 4' stroke-dashoffset='0'/></g></svg>";
    private static MemoryStream Input(string source) => new(Encoding.UTF8.GetBytes(source));
    [Fact]
    public void SvgVectorsClipsAndTransformsRemainEditableAcrossPdfAndSvg()
    {
        using var input = Input(Fixture); using var document = PdfSvg.Import(input);
        Assert.Equal(new PdfPageSize(150, 90), document.Pages[0].Size); Assert.True(input.CanRead);
        using var output = new MemoryStream(); PdfSvg.WritePage(document.CreateSnapshot(), 0, output); Assert.True(output.CanWrite);
        var xml = XDocument.Parse(Encoding.UTF8.GetString(output.ToArray())); XNamespace svg = "http://www.w3.org/2000/svg";
        Assert.NotEmpty(xml.Descendants(svg + "path")); Assert.Empty(xml.Descendants(svg + "image")); Assert.NotEmpty(xml.Descendants(svg + "clipPath"));
        output.Position = 0; using var roundTrip = PdfSvg.Import(output); Assert.Equal(document.Pages[0].Size, roundTrip.Pages[0].Size);
        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures"); Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "vectors-source.svg"), Fixture, new UTF8Encoding(false)); File.WriteAllBytes(Path.Combine(directory, "vectors-export.svg"), output.ToArray()); document.SaveFile(Path.Combine(directory, "vectors.pdf")); roundTrip.SaveFile(Path.Combine(directory, "vectors-roundtrip.pdf"));
    }
    [Fact]
    public void SvgDefaultViewportUsesCssPixelsAndExplicitTextHasNoAmbientFontFallback()
    {
        using var input = Input("<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><rect width='100' height='80' fill='red'/></svg>"); using var document = PdfSvg.Import(input);
        Assert.Equal(new PdfPageSize(75, 60), document.Pages[0].Size);
        using var csharpInput = Input("<svg width='10' height='10'><rect width='10' height='10'/></svg>"); using var csharp = ContractExamples.CSharp.ContractExample.ImportSvg(csharpInput); Assert.Single(csharp.Pages);
        using var vbInput = Input("<svg width='10' height='10'><rect width='10' height='10'/></svg>"); using var vb = ContractExamples.VisualBasic.ContractExample.ImportSvg(vbInput); Assert.Single(vb.Pages);
        const string source = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><text x='4' y='20' font-size='12'>office</text></svg>";
        using var noFonts = Input(source); Assert.Equal("Svg.Fonts", Assert.Throws<PdfException>(() => PdfSvg.Import(noFonts)).Diagnostic.Code);
        using var text = Input(source); using var rendered = PdfSvg.Import(text, new(fonts: new(TextTests.Read("business.ttf"))), new PdfProviderRegistry(new PdfSimpleTextShaper()));
        Assert.Contains("office", PdfTextExtraction.ReadPage(rendered.CreateSnapshot(), 0).RawText);
        using var unchanged = new MemoryStream(); unchanged.WriteByte(42); Assert.Throws<PdfException>(() => PdfSvg.WritePage(rendered.CreateSnapshot(), 0, unchanged)); Assert.Equal(new byte[] { 42 }, unchanged.ToArray());
    }
    [Fact]
    public void SvgImagesRequireLocalAuthorityAndPreserveAlpha()
    {
        string png = Convert.ToBase64String(ImageAssetTests.Fixture("rgba.png")); string source = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='80'><image width='30' height='20' href='data:image/png;base64," + png + "'/></svg>";
        using var denied = Input(source); Assert.Equal("Svg.ImageAuthority", Assert.Throws<PdfException>(() => PdfSvg.Import(denied)).Diagnostic.Code);
        using var allowed = Input(source); using var document = PdfSvg.Import(allowed, new(allowDataImages: true, images: new(allowLosslessTranscode: true)));
        Assert.Contains(PdfPageGraphics.Inspect(document.CreateSnapshot(), 0).Occurrences, item => item.Kind == PdfGraphicsOccurrenceKind.Image);
        using var remote = Input("<svg width='100' height='80'><image width='30' height='20' href='https://not-fetched.invalid/a.png'/></svg>"); Assert.Equal("Svg.ImageResource", Assert.Throws<PdfException>(() => PdfSvg.Import(remote, new(allowDataImages: true))).Diagnostic.Code);
    }
    [Theory]
    [InlineData("<svg width='1' height='1'><script>while(true){}</script></svg>", "Svg.ActiveContent")]
    [InlineData("<svg width='1' height='1' onclick='run()'/>", "Svg.ActiveContent")]
    [InlineData("<svg width='1' height='1'><circle r='1'/></svg>", "Svg.Element")]
    [InlineData("<svg width='1' height='1'><path d='M0 0 A1 1 0 0 0 2 2'/></svg>", "Svg.PathCommand")]
    [InlineData("<svg width='10' height='10'><g transform='scale(1e308) scale(1e308)'><rect width='1' height='1'/></g></svg>", "Svg.Geometry")]
    [InlineData("<!DOCTYPE svg [<!ENTITY ext SYSTEM 'file:///not-read'>]><svg width='1' height='1'>&ext;</svg>", "Format.Xml")]
    public void UnsupportedOrActiveSvgCannotBecomeSuccessfulPartialOutput(string source, string code)
    {
        using var input = Input(source); Assert.Equal(code, Assert.Throws<PdfException>(() => PdfSvg.Import(input)).Diagnostic.Code);
    }
    [Fact]
    public void SvgResourceDepthAndExportUnsupportedEffectsAreBounded()
    {
        using var input = Input(Fixture); Assert.Throws<PdfException>(() => PdfSvg.Import(input, policy: new(limits: new(maximumObjects: 2))));
        using var nested = Input("<svg width='1' height='1'><g><g><g><rect width='1' height='1'/></g></g></g></svg>"); Assert.Equal("Limit.Depth", Assert.Throws<PdfException>(() => PdfSvg.Import(nested, policy: new(limits: new(maximumDepth: 3)))).Diagnostic.Code);
        using var marked = ExtractionTests.Fixture("/Span BMC 0 0 10 10 re f EMC"); using var output = new MemoryStream(); Assert.Equal("Svg.ExportUnsupported", Assert.Throws<PdfException>(() => PdfSvg.WritePage(marked.CreateSnapshot(), 0, output)).Diagnostic.Code); Assert.Equal(0, output.Length);
    }
}
