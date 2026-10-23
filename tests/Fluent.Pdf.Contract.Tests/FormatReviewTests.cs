using System.IO;
using System.IO.Compression;
using System.Text;
using Fluent.Pdf.Formats;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class FormatReviewTests
{
    internal static MemoryStream Repack(Stream original, Func<string, byte[], byte[]> transform)
    {
        original.Position = 0;
        var output = new MemoryStream();
        using (var read = new ZipArchive(original, ZipArchiveMode.Read, true))
        using (var write = new ZipArchive(output, ZipArchiveMode.Create, true))
            foreach (var entry in read.Entries)
            {
                using var input = entry.Open();
                using var captured = new MemoryStream();
                input.CopyTo(captured);
                byte[] bytes = transform(entry.FullName, captured.ToArray());
                using var target = write.CreateEntry(entry.FullName).Open();
                target.Write(bytes, 0, bytes.Length);
            }

        output.Position = 0;
        return output;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void XlsxMergesIgnoreOnlyCoveredCellsWithoutData(bool data)
    {
        using var original = new MemoryStream();
        PdfSpreadsheets.WriteData(new("Data", 1, 2, new[] { new PdfSheetCell(0, 0, "3", PdfSheetValueKind.Number, columnSpan: 2) }), PdfSpreadsheetFormat.Xlsx, original);
        using var modified = Repack(original, (name, bytes) => name == "xl/worksheets/sheet1.xml" ? Encoding.UTF8.GetBytes(Encoding.UTF8.GetString(bytes).Replace("</row>", "<c r=\"B1\" s=\"1\">" + (data ? "<v>4</v>" : "") + "</c></row>")) : bytes);
        if (data)
            Assert.Equal("Spreadsheet.Data", Assert.Throws<PdfException>(() => PdfSpreadsheets.ReadData(modified, PdfSpreadsheetFormat.Xlsx, "Data")).Diagnostic.Code);
        else
        {
            var cell = Assert.Single(PdfSpreadsheets.ReadData(modified, PdfSpreadsheetFormat.Xlsx, "Data").Cells);
            Assert.Equal("3", cell.Value);
            Assert.Equal(2, cell.ColumnSpan);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void XlsxPhoneticRunsAreExcludedFromSharedAndInlineText(bool shared)
    {
        using var original = new MemoryStream();
        PdfSpreadsheets.WriteData(new("Data", 1, 1, new[] { new PdfSheetCell(0, 0, "placeholder") }), PdfSpreadsheetFormat.Xlsx, original);
        const string runs = "<t>東京</t><rPh sb='0' eb='2'><t>トウキョウ</t></rPh><r><t>市</t></r>";
        using var modified = new MemoryStream();
        original.Position = 0;
        using (var read = new ZipArchive(original, ZipArchiveMode.Read, true))
        using (var write = new ZipArchive(modified, ZipArchiveMode.Create, true))
        {
            foreach (var entry in read.Entries)
            {
                using var input = entry.Open();
                using var captured = new MemoryStream();
                input.CopyTo(captured);
                byte[] bytes = captured.ToArray();
                if (entry.FullName == "xl/worksheets/sheet1.xml")
                {
                    var xml = System.Xml.Linq.XDocument.Parse(Encoding.UTF8.GetString(bytes));
                    System.Xml.Linq.XNamespace x = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
                    var cell = xml.Descendants(x + "c").Single();
                    cell.RemoveNodes();
                    cell.SetAttributeValue("t", shared ? "s" : "inlineStr");
                    cell.Add(shared ? new System.Xml.Linq.XElement(x + "v", "0") : System.Xml.Linq.XElement.Parse("<is xmlns='" + x + "'>" + runs + "</is>"));
                    bytes = Encoding.UTF8.GetBytes(xml.ToString());
                }

                using var target = write.CreateEntry(entry.FullName).Open();
                target.Write(bytes, 0, bytes.Length);
            }

            if (shared)
            {
                using var target = write.CreateEntry("xl/sharedStrings.xml").Open();
                byte[] bytes = Encoding.UTF8.GetBytes("<sst xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main'><si>" + runs + "</si></sst>");
                target.Write(bytes, 0, bytes.Length);
            }
        }

        modified.Position = 0;
        Assert.Equal("東京市", Assert.Single(PdfSpreadsheets.ReadData(modified, PdfSpreadsheetFormat.Xlsx, "Data").Cells).Value);
    }

    [Theory]
    [InlineData(PdfSpreadsheetFormat.Xlsx)]
    [InlineData(PdfSpreadsheetFormat.Ods)]
    public void SpreadsheetNumbersPreserveFiniteDoubleRange(PdfSpreadsheetFormat format)
    {
        var sheet = new PdfSheetData("Data", 1, 2, new[] { new PdfSheetCell(0, 0, "1.5E-30", PdfSheetValueKind.Number), new PdfSheetCell(0, 1, "1E+30", PdfSheetValueKind.Number) });
        using var output = new MemoryStream();
        PdfSpreadsheets.WriteData(sheet, format, output);
        output.Position = 0;
        var read = PdfSpreadsheets.ReadData(output, format, "Data");
        Assert.Equal(sheet.Cells.Select(c => c.Value), read.Cells.Select(c => c.Value));
        Assert.NotEqual("0", read.Cells[0].Value);
        Assert.Throws<ArgumentException>(() => new PdfSheetCell(0, 0, "1E+400", PdfSheetValueKind.Number));
    }

    [Theory]
    [InlineData("<html><body>Hi<br></body></html>", PdfDetectedFormat.Unknown)]
    [InlineData("<not closed", PdfDetectedFormat.Unknown)]
    [InlineData("<html xmlns='http://www.w3.org/1999/xhtml'><body>Hi</body></html>", PdfDetectedFormat.Html)]
    [InlineData("<!DOCTYPE html ><html><br>", PdfDetectedFormat.Html)]
    public void DetectionReturnsMarkupFamiliesWithoutLatchingParseErrors(string markup, PdfDetectedFormat expected)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(markup));
        var detection = PdfConversions.Detect(input);
        Assert.Equal(expected, detection.Format);
        Assert.True(detection.OperationReport.Succeeded);
    }

    [Fact]
    public void StoredMimetypeHasNoCompressionOrExtraFieldsOnEveryTargetRuntime()
    {
        using var source = Pdf.Create().AddPage();
        using var epub = new MemoryStream();
        PdfEbooks.WriteEpub(source.CreateSnapshot(), new[] { 0 }, epub, EbookTests.Metadata(), new(PdfEbookLayout.ReflowText), policy: new(PdfProcessingMode.Permissive));
        using var ods = new MemoryStream();
        PdfSpreadsheets.WriteData(SpreadsheetTests.Fixture(), PdfSpreadsheetFormat.Ods, ods);
        foreach (var bytes in new[]
        {
            epub.ToArray(),
            ods.ToArray()
        }

        )
        {
            Assert.Equal(new byte[] { 80, 75, 3, 4 }, bytes.Take(4));
            Assert.Equal(0, bytes[8] | bytes[9] << 8);
            Assert.Equal(0, bytes[28] | bytes[29] << 8);
            Assert.Equal("mimetype", Encoding.ASCII.GetString(bytes, 30, 8));
            Assert.Equal(8, bytes[26] | bytes[27] << 8);
        }
    }

    [Fact]
    public void SvgNormalizesDefaultTextWhitespaceBeforeShaping()
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes("<svg width='200' height='80'><text x='4' y='20'>\n    Total\t  due\n  </text></svg>"));
        using var pdf = PdfSvg.Import(input, new(fonts: new(TextTests.Read("business.ttf"))), new(new PdfSimpleTextShaper()));
        Assert.Equal("Total due", PdfTextExtraction.ReadPage(pdf.CreateSnapshot(), 0).RawText);
        using var preserve = new MemoryStream(Encoding.UTF8.GetBytes("<svg width='200' height='80' xml:space='preserve'><text>Total</text></svg>"));
        Assert.Equal("Svg.Attribute", Assert.Throws<PdfException>(() => PdfSvg.Import(preserve)).Diagnostic.Code);
    }

    [Fact]
    public void EpubCollapsesNormalWhitespaceAndRetainsStructuralParagraphsAndPreText()
    {
        using var source = Pdf.Create().AddPage();
        using var original = new MemoryStream();
        PdfEbooks.WriteEpub(source.CreateSnapshot(), new[] { 0 }, original, EbookTests.Metadata(), new(PdfEbookLayout.ReflowText), policy: new(PdfProcessingMode.Permissive));
        using var modified = Repack(original, (name, bytes) => name == "EPUB/page1.xhtml" ? Encoding.UTF8.GetBytes("<html xmlns='http://www.w3.org/1999/xhtml'><head><title>Test</title></head><body><p>\n    Hello\n\n    <span>world</span>\n  </p><p>Second<br/>line</p><pre>Pre  text\n\nLast</pre></body></html>") : bytes);
        using var imported = PdfEbooks.ImportReflowText(modified, new(new(new(TextTests.Read("business.ttf")), 10)), new(new PdfSimpleTextShaper()), new(PdfProcessingMode.Permissive));
        string text = PdfTextExtraction.ReadPage(imported.CreateSnapshot(), 0).RawText;
        Assert.Contains("Hello world", text);
        Assert.Contains("Second", text);
        Assert.Contains("line", text);
        Assert.Contains("Pre  text", text);
        Assert.Contains("Last", text);
        Assert.DoesNotContain("    Hello", text);
    }
}
