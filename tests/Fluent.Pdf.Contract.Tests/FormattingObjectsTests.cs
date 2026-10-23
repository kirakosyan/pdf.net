using System.IO;
using System.Text;
using System.Threading;
using Fluent.Pdf.Formats;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class FormattingObjectsTests
{
    private const string Open = "<fo:root xmlns:fo='http://www.w3.org/1999/XSL/Format'><fo:layout-master-set><fo:simple-page-master master-name='body' page-width='200pt' page-height='100pt' margin='10pt'><fo:region-body/></fo:simple-page-master></fo:layout-master-set><fo:page-sequence master-reference='body'><fo:flow flow-name='xsl-region-body'>";
    private const string Close = "</fo:flow></fo:page-sequence></fo:root>";
    private static MemoryStream Input(string value) => new(Encoding.UTF8.GetBytes(value));
    private static PdfFlowTextStyle Style() => new(new(TextTests.Read("business.ttf")), 10);
    private static PdfProviderRegistry Providers() => new(ManagedPdfProvider.Instance, new PdfSimpleTextShaper());
    [Fact]
    public void ExplicitMasterBlocksAndTablesProduceRealTextAndSourceGeometry()
    {
        using var input = Input(Open + "<fo:block font-size='10pt'>AB</fo:block><fo:table table-layout='fixed'><fo:table-column column-width='90pt'/><fo:table-column column-width='90pt'/><fo:table-body><fo:table-row><fo:table-cell><fo:block>C</fo:block></fo:table-cell><fo:table-cell><fo:block>D</fo:block></fo:table-cell></fo:table-row></fo:table-body></fo:table>" + Close);
        using var doc = PdfFormattingObjects.Import(input, Style(), Providers());
        Assert.True(input.CanRead);
        Assert.Equal(new PdfPageSize(200, 100), doc.Pages[0].Size);
        string text = PdfTextExtraction.ReadPage(doc.CreateSnapshot(), 0).Text;
        Assert.Contains("AB", text);
        Assert.Contains("C", text);
        Assert.Contains("D", text);
        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures");
        Directory.CreateDirectory(directory);
        doc.SaveFile(Path.Combine(directory, "formatting-objects.pdf"));
    }

    [Fact]
    public void ClosedChildPathTransformEscapesDataWithoutEvaluatingFunctionsOrRecursion()
    {
        using var xml = Input("<records><row>AB &amp; CD</row><row>EF</row></records>");
        using var sheet = Input("<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0'><xsl:template match='/'>" + Open + "<xsl:for-each select='/records/row'><fo:block><xsl:value-of select='.'/></fo:block></xsl:for-each>" + Close + "</xsl:template></xsl:stylesheet>");
        using var doc = PdfFormattingObjects.Transform(xml, sheet, Style(), Providers());
        string text = PdfTextExtraction.ReadPage(doc.CreateSnapshot(), 0).Text;
        Assert.Contains("AB & CD", text);
        Assert.Contains("EF", text);
        Assert.True(sheet.CanRead);
        Assert.True(xml.CanRead);
    }

    [Theory]
    [InlineData("<fo:external-graphic src='file:///not-read'/>")]
    [InlineData("<fo:block font-family='ambient'>AB</fo:block>")]
    [InlineData("<fo:block><fo:inline>AB</fo:inline></fo:block>")]
    public void UnrequestedResourcesAndUnsupportedFormattingNeverDisappear(string block)
    {
        using var input = Input(Open + block + Close);
        Assert.Equal("Fo.FormattingUnsupported", Assert.Throws<PdfException>(() => PdfFormattingObjects.Import(input, Style(), Providers())).Diagnostic.Code);
    }

    [Theory]
    [InlineData("<xsl:value-of select=\"document('file:///not-read')\"/>", "Fo.StylesheetPath")]
    [InlineData("<xsl:apply-templates/>", "Fo.StylesheetUnsupported")]
    [InlineData("<fo:external-graphic/>", "Fo.FormattingUnsupported")]
    public void UnselectedTransformBranchesStillRejectActiveOrUnknownInstructions(string instruction, string code)
    {
        using var xml = Input("<empty/>");
        using var sheet = Input("<xsl:stylesheet xmlns:xsl='http://www.w3.org/1999/XSL/Transform' version='1.0'><xsl:template match='/'>" + Open + "<xsl:for-each select='/absent'>" + instruction + "</xsl:for-each>" + Close + "</xsl:template></xsl:stylesheet>");
        Assert.Equal(code, Assert.Throws<PdfException>(() => PdfFormattingObjects.Transform(xml, sheet, Style(), Providers())).Diagnostic.Code);
    }

    [Fact]
    public void XmlBudgetDtdAndCancellationBoundTheEntirePipeline()
    {
        using var input = Input(Open + "<fo:block>AB</fo:block>" + Close);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfFormattingObjects.Import(input, Style(), Providers(), policy: new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        input.Position = 0;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => PdfFormattingObjects.Import(input, Style(), Providers(), cancellationToken: cancelled.Token));
        using var dtd = Input("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///not-read'>]><x>&e;</x>");
        Assert.Equal("Format.Xml", Assert.Throws<PdfException>(() => PdfFormattingObjects.Import(dtd, Style(), Providers())).Diagnostic.Code);
    }
}
