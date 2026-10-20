using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Fluent.Pdf.Formats;
using Xunit;
namespace Fluent.Pdf.Contract.Tests;
public sealed class SpreadsheetTests
{
    internal static PdfSheetData Fixture() => new("Data", 4, 3, new PdfSheetCell[] { new(0, 0, "=SUM(A1:A2)", columnSpan: 2, sourcePage: 2, sourceBounds: new(1, 2, 3, 4)), new(1, 0, "12.50", PdfSheetValueKind.Number), new(1, 1, "true", PdfSheetValueKind.Boolean), new(1, 2, "2026-10-20", PdfSheetValueKind.Date), new(2, 0, "quoted,\"line\"\nnext") });
    [Theory]
    [InlineData(PdfSpreadsheetFormat.Xlsx)]
    [InlineData(PdfSpreadsheetFormat.Ods)]
    [InlineData(PdfSpreadsheetFormat.SpreadsheetMl)]
    public void TypedDataAndMergesRoundTripWithoutExecutableFormulas(PdfSpreadsheetFormat format)
    {
        var sheet = Fixture(); using var output = new MemoryStream(); PdfSpreadsheets.WriteData(sheet, format, output); Assert.True(output.CanWrite); output.Position = 0; var read = PdfSpreadsheets.ReadData(output, format, "Data"); Assert.True(output.CanRead); Assert.Equal(4, read.RowCount); Assert.Equal(3, read.ColumnCount);
        foreach (var expected in sheet.Cells) { var cell = Assert.Single(read.Cells, c => c.Row == expected.Row && c.Column == expected.Column); Assert.Equal(expected.Value, cell.Value); Assert.Equal(expected.Kind, cell.Kind); Assert.Equal(expected.ColumnSpan, cell.ColumnSpan); Assert.Null(cell.SourceFormula); }
        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures"); Directory.CreateDirectory(directory); File.WriteAllBytes(Path.Combine(directory, "table." + (format == PdfSpreadsheetFormat.Xlsx ? "xlsx" : format == PdfSpreadsheetFormat.Ods ? "ods" : "xml")), output.ToArray());
        using var provenance = new MemoryStream(); PdfSpreadsheets.WriteProvenance(sheet, provenance); var xml = XDocument.Parse(Encoding.UTF8.GetString(provenance.ToArray())); Assert.Equal("2", (string?)xml.Descendants("cell").First().Attribute("sourcePage"));
    }
    [Fact]
    public void CsvQuotesAllFieldsAndNeverGuessesTypesOrChangesFormulaLikeText()
    {
        var sheet = new PdfSheetData("Data", 2, 2, new PdfSheetCell[] { new(0, 0, "a,\"b\"\nc"), new(0, 1, "12.50"), new(1, 0, "-2.5", PdfSheetValueKind.Number) }); using var output = new MemoryStream(); PdfSpreadsheets.WriteData(sheet, PdfSpreadsheetFormat.Csv, output); output.Position = 0; var read = PdfSpreadsheets.ReadData(output, PdfSpreadsheetFormat.Csv, "Data"); output.Position = 0; Assert.Equal(2, ContractExamples.CSharp.ContractExample.ReadSpreadsheet(output, PdfSpreadsheetFormat.Csv, "Data").RowCount); output.Position = 0; Assert.Equal(2, ContractExamples.VisualBasic.ContractExample.ReadSpreadsheet(output, PdfSpreadsheetFormat.Csv, "Data").RowCount); Assert.Equal(sheet.Cells[0].Value, read.Cells[0].Value); Assert.All(read.Cells, cell => Assert.Equal(PdfSheetValueKind.Text, cell.Kind));
        using var denied = new MemoryStream(); denied.WriteByte(42); var unsafeSheet = new PdfSheetData("Data", 1, 1, new[] { new PdfSheetCell(0, 0, " =HYPERLINK(\"x\")") }); Assert.Equal("Spreadsheet.CsvFormulaText", Assert.Throws<PdfException>(() => PdfSpreadsheets.WriteData(unsafeSheet, PdfSpreadsheetFormat.Csv, denied)).Diagnostic.Code); Assert.Equal(new byte[] { 42 }, denied.ToArray());
        using var merge = new MemoryStream(); Assert.Equal("Spreadsheet.CsvMerge", Assert.Throws<PdfException>(() => PdfSpreadsheets.WriteData(Fixture(), PdfSpreadsheetFormat.Csv, merge)).Diagnostic.Code);
    }
    [Fact]
    public void SelectedRangePreservesLiteralDataAndRejectsCutMerges()
    {
        var options = new PdfFormatTextOptions(new(new(TextTests.Read("business.ttf")), 9)); var providers = new PdfProviderRegistry(new PdfSimpleTextShaper()); using var document = PdfSpreadsheets.ImportData(Fixture(), 0, 0, 3, 3, options, providers); Assert.Contains("=SUM(A1:A2)", PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0).Text); Assert.Equal("Spreadsheet.RangeMerge", Assert.Throws<PdfException>(() => PdfSpreadsheets.ImportData(Fixture(), 0, 1, 1, 1, options, providers)).Diagnostic.Code);
    }
    [Fact]
    public void FormulaCachesArePassiveAndMissingCachesReject()
    {
        const string before = "<Workbook xmlns='urn:schemas-microsoft-com:office:spreadsheet' xmlns:s='urn:schemas-microsoft-com:office:spreadsheet'><Worksheet s:Name='Data'><Table><Row><Cell s:Formula='=1+1'>", after = "</Cell></Row></Table></Worksheet></Workbook>"; using var input = new MemoryStream(Encoding.UTF8.GetBytes(before + "<Data s:Type='Number'>2</Data>" + after)); var read = PdfSpreadsheets.ReadData(input, PdfSpreadsheetFormat.SpreadsheetMl, "Data"); Assert.Equal("=1+1", read.Cells[0].SourceFormula); using var exported = new MemoryStream(); PdfSpreadsheets.WriteData(read, PdfSpreadsheetFormat.Xlsx, exported); exported.Position = 0; var again = PdfSpreadsheets.ReadData(exported, PdfSpreadsheetFormat.Xlsx, "Data"); Assert.Null(again.Cells[0].SourceFormula); Assert.Equal("2", again.Cells[0].Value);
        using var missing = new MemoryStream(Encoding.UTF8.GetBytes(before + after)); Assert.Equal("Spreadsheet.FormulaCache", Assert.Throws<PdfException>(() => PdfSpreadsheets.ReadData(missing, PdfSpreadsheetFormat.SpreadsheetMl, "Data")).Diagnostic.Code);
    }
    [Fact]
    public void PackagesTraversalGridLimitsCancellationAndUnavailableAdaptersReject()
    {
        using var zipBytes = new MemoryStream(); using (var zip = new ZipArchive(zipBytes, ZipArchiveMode.Create, true)) { using var entry = zip.CreateEntry("../escape").Open(); entry.WriteByte(1); } zipBytes.Position = 0; Assert.Equal("Format.PackageTraversal", Assert.Throws<PdfException>(() => PdfSpreadsheets.ReadData(zipBytes, PdfSpreadsheetFormat.Xlsx, "Data")).Diagnostic.Code);
        using var input = new MemoryStream(new byte[] { 1 }); Assert.Equal("Spreadsheet.AdapterUnavailable", Assert.Throws<PdfException>(() => PdfSpreadsheets.ReadData(input, PdfSpreadsheetFormat.Xls, "Data")).Diagnostic.Code); Assert.Equal(0, input.Position);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfSheetData("Data", 100000, 1024, Array.Empty<PdfSheetCell>())); Assert.Throws<ArgumentException>(() => new PdfSheetData("Data", 1, 2, new[] { new PdfSheetCell(0, 0, "a", columnSpan: 2), new PdfSheetCell(0, 1, "b") }));
        using var output = new MemoryStream(); Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfSpreadsheets.WriteData(Fixture(), PdfSpreadsheetFormat.Xlsx, output, new(limits: new(maximumObjects: 2)))).Diagnostic.Code); Assert.Equal(0, output.Length);
        using var cancelled = new CancellationTokenSource(); cancelled.Cancel(); Assert.Throws<OperationCanceledException>(() => PdfSpreadsheets.WriteData(Fixture(), PdfSpreadsheetFormat.Xlsx, output, cancellationToken: cancelled.Token)); Assert.Equal(0, output.Length);
    }
}
