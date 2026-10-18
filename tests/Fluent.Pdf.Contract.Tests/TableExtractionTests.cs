using System.Text;
using System.Threading;
using System.Globalization;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class TableExtractionTests
{
    internal static PdfDocument Fixture(bool merged = true, bool borderless = false, string left = "A", string right = "B", bool nonrectangular = false, bool actual = false)
    {
        string rules = borderless ? "" : "20 280 m 140 280 l 140 320 l 20 320 l h S 20 300 m 140 300 l S 80 280 m 80 " + (merged ? "300" : "320") + " l S ";
        if (nonrectangular)
            rules = rules.Replace("20 300 m", "80 300 m");
        string Cell(string value, int x, int y) => "BT /F1 10 Tf 1 0 0 1 " + x + " " + y + " Tm (" + Escape(value) + ") Tj ET ";
        string top = merged && !borderless ? Cell("Header", 25, 307) : Cell("H1", 25, 307) + Cell("H2", 85, 307);
        string bottom = (actual ? "/Span << /ActualText (quote\"comma,\\n=A1) >> BDC " : "") + Cell(left, 25, 287) + (actual ? "EMC " : "") + Cell(right, 85, 287);
        return ExtractionTests.Fixture(rules + top + bottom);
    }

    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("(", "\\(").Replace(")", "\\)");
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    [Fact]
    public void LiteralGridDetectsRectangularMergedCellsAndKeepsSourceGeometry()
    {
        using var doc = Fixture();
        var result = PdfTableExtraction.Read(doc.CreateSnapshot(), new[] { 0 }, new(PdfTableDetectionMode.Ruled));
        var table = Assert.Single(result.Tables);
        Assert.Equal(2, table.RowCount);
        Assert.Equal(2, table.ColumnCount);
        Assert.Equal(3, table.Cells.Count);
        Assert.Equal(new PdfRect(20, 80, 120, 40), table.Bounds);
        var header = Assert.Single(table.Cells, c => c.Row == 0);
        Assert.Equal(2, header.ColumnSpan);
        Assert.Equal("Header", header.Text);
        Assert.Equal(.95, header.Confidence);
        Assert.NotEmpty(header.RuleSources);
        Assert.All(header.Characters, c => Assert.True(c.OperationIndex >= 0));
        Assert.Equal("A", table.Cells.Single(c => c.Row == 1 && c.Column == 0).Text);
        Assert.Null(header.Number);
    }

    [Fact]
    public void BorderlessAlignmentIsExplicitAndConfidenceIsHeuristic()
    {
        using var doc = Fixture(merged: false, borderless: true);
        var result = PdfTableExtraction.Read(doc.CreateSnapshot(), new[] { 0 }, new(PdfTableDetectionMode.Borderless));
        var table = Assert.Single(result.Tables);
        Assert.Equal(4, table.Cells.Count);
        Assert.Equal(.65, table.Confidence);
        Assert.All(table.Cells, c => Assert.Empty(c.RuleSources));
        Assert.Equal(new[] { "H1", "H2", "A", "B" }, table.Cells.Select(c => c.Text));
        Assert.Empty(PdfTableExtraction.Read(doc.CreateSnapshot(), new[] { 0 }, new(PdfTableDetectionMode.Ruled)).Tables);
    }

    [Fact]
    public void CultureTypingRequiresExplicitSettingsAndIgnoresAmbientCulture()
    {
        using var doc = Fixture(left: "1.234,50", right: "-2,5");
        var snapshot = doc.CreateSnapshot();
        var previous = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-US");
            var plain = PdfTableExtraction.Read(snapshot, new[] { 0 }, new(PdfTableDetectionMode.Ruled));
            Assert.All(plain.Tables[0].Cells, c => Assert.Null(c.Number));
            var typed = PdfTableExtraction.Read(snapshot, new[] { 0 }, new(PdfTableDetectionMode.Ruled, numericCulture: "de-DE"));
            Assert.Equal(1234.50m, typed.Tables[0].Cells.Single(c => c.Row == 1 && c.Column == 0).Number);
            Assert.Equal(-2.5m, typed.Tables[0].Cells.Single(c => c.Row == 1 && c.Column == 1).Number);
            using var json = new MemoryStream();
            typed.WriteJson(json);
            Assert.Contains("\"value\":1234.50", Encoding.UTF8.GetString(json.ToArray()));
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void CsvMergedCellAndFormulaPoliciesAreExplicitAndJsonKeepsSpans()
    {
        using var doc = Fixture(left: "=A1");
        var result = PdfTableExtraction.Read(doc.CreateSnapshot(), new[] { 0 }, new(PdfTableDetectionMode.Ruled));
        using var anchor = new MemoryStream();
        result.WriteCsv(anchor, PdfMergedCellCsvPolicy.AnchorOnly);
        Assert.True(anchor.CanWrite);
        Assert.Equal("\"Header\",\"\"\r\n\"=A1\",\"B\"\r\n", Encoding.UTF8.GetString(anchor.ToArray()));
        using var repeat = new MemoryStream();
        result.WriteCsv(repeat, PdfMergedCellCsvPolicy.RepeatText, escapeSpreadsheetFormulas: true);
        Assert.Equal("\"Header\",\"Header\"\r\n\"'=A1\",\"B\"\r\n", Encoding.UTF8.GetString(repeat.ToArray()));
        using var json = new MemoryStream();
        result.WriteJson(json);
        string value = Encoding.UTF8.GetString(json.ToArray());
        Assert.Contains("\"columnSpan\":2", value);
        Assert.Contains("\"sourceCharacters\":[{", value);
        Assert.Contains("\"ruleSources\":[{", value);
    }

    [Fact]
    public void ActualTextSurvivesCellExtractionAndMultilineCsvEscaping()
    {
        using var doc = Fixture(actual: true);
        var result = PdfTableExtraction.Read(doc.CreateSnapshot(), new[] { 0 }, new(PdfTableDetectionMode.Ruled));
        var cell = result.Tables[0].Cells.Single(c => c.Row == 1 && c.Column == 0);
        Assert.Equal("quote\"comma,\n=A1", cell.Text);
        using var csv = new MemoryStream();
        result.WriteCsv(csv, PdfMergedCellCsvPolicy.AnchorOnly);
        Assert.Contains("\"quote\"\"comma,\n=A1\"", Encoding.UTF8.GetString(csv.ToArray()));
    }

    [Fact]
    public void NonrectangularMergesAndCellResourceLimitsRejectBeforeInventingCells()
    {
        using var malformed = Fixture(nonrectangular: true);
        Assert.Equal("Tables.MergeShapeProfile", Error(() => PdfTableExtraction.Read(malformed.CreateSnapshot(), new[] { 0 }, new(PdfTableDetectionMode.Ruled))));
        using var doc = Fixture();
        var source = doc.CreateSnapshot();
        Assert.Equal("Tables.CellLimit", Error(() => PdfTableExtraction.Read(source, new[] { 0 }, new(PdfTableDetectionMode.Ruled, maximumCells: 3))));
        Assert.Equal("Limit.Objects", Error(() => PdfTableExtraction.Read(source, new[] { 0 }, new(PdfTableDetectionMode.Ruled), policy: new(limits: new(maximumObjects: 1)))));
        Assert.Equal("Limit.ExpandedBytes", Error(() => PdfTableExtraction.Read(source, new[] { 0 }, new(PdfTableDetectionMode.Ruled), policy: new(limits: new(maximumExpandedBytes: 1)))));
        Assert.Throws<OperationCanceledException>(() => PdfTableExtraction.Read(source, new[] { 0 }, new(PdfTableDetectionMode.Ruled), cancellationToken: new CancellationToken(true)));
        Assert.Throws<ArgumentException>(() => PdfTableExtraction.Read(source, new[] { 0, 0 }, new(PdfTableDetectionMode.Ruled)));
    }

    [Fact]
    public void ExportBudgetFailurePreservesExistingAtomicFileAndBorrowedOwnership()
    {
        using var doc = Fixture();
        var result = PdfTableExtraction.Read(doc.CreateSnapshot(), new[] { 0 }, new(PdfTableDetectionMode.Ruled));
        using var output = new MemoryStream();
        Assert.Equal("Limit.OutputBytes", Error(() => result.WriteJson(output, new(limits: new(maximumOutputBytes: 1)))));
        Assert.True(output.CanWrite);
        string path = Path.Combine(Path.GetTempPath(), "fluent-table-" + Guid.NewGuid().ToString("N") + ".json");
        File.WriteAllText(path, "retained");
        try
        {
            Assert.Equal("Limit.OutputBytes", Error(() => result.WriteJsonFile(path, new(limits: new(maximumOutputBytes: 1)))));
            Assert.Equal("retained", File.ReadAllText(path));
            result.WriteJsonFile(path);
            Assert.StartsWith("{\"version\":1", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ConsecutivePagesRetainHeadersAndExplicitlyJoinOnlyMatchingGridAndHeaders()
    {
        using var first = Fixture(merged: false);
        using var second = Fixture(merged: false, left: "C", right: "D");
        using var multi = PdfPageOrganization.Merge(new[] { new PdfPageInput(first.CreateSnapshot()), new PdfPageInput(second.CreateSnapshot()) });
        var result = PdfTableExtraction.Read(multi.CreateSnapshot(), new[] { 0, 1 }, new(PdfTableDetectionMode.Ruled, headerRows: 1, joinConsecutivePages: true));
        Assert.Equal(2, result.Tables.Count);
        var sequence = Assert.Single(result.Sequences);
        Assert.Equal(new[] { 0, 1 }, sequence.TableIndices);
        Assert.Equal(1, sequence.RepeatedHeaderRows);
        Assert.Equal(4, result.Tables[1].Cells.Count);
        Assert.All(result.Tables[1].Cells, c => Assert.Equal(1, c.PageIndex));
        using var csv = new MemoryStream();
        result.WriteCsv(csv, PdfMergedCellCsvPolicy.AnchorOnly, omitRepeatedHeaders: true);
        Assert.Equal("\"H1\",\"H2\"\r\n\"A\",\"B\"\r\n\"C\",\"D\"\r\n", Encoding.UTF8.GetString(csv.ToArray()));
        Assert.Equal(2, PdfTableExtraction.Read(multi.CreateSnapshot(), new[] { 0, 1 }, new(PdfTableDetectionMode.Ruled)).Sequences.Count);
    }

    [Fact]
    public void DifferentHeaderSpansDoNotSilentlyMergePages()
    {
        using var first = Fixture(merged: false);
        using var second = Fixture();
        using var multi = PdfPageOrganization.Merge(new[] { new PdfPageInput(first.CreateSnapshot()), new PdfPageInput(second.CreateSnapshot()) });
        var result = PdfTableExtraction.Read(multi.CreateSnapshot(), new[] { 0, 1 }, new(PdfTableDetectionMode.Ruled, headerRows: 1, joinConsecutivePages: true));
        Assert.Equal(2, result.Sequences.Count);
    }

    [Fact]
    public void CrossCellActualTextIsExplicitAndMissingCandidatesAreDiagnosed()
    {
        string rules = "20 280 m 140 280 l 140 320 l 20 320 l h S 20 300 m 140 300 l S 80 280 m 80 320 l S ";
        using var split = ExtractionTests.Fixture(rules + "/Span << /ActualText (combined) >> BDC BT /F1 10 Tf 25 287 Td (A) Tj 60 0 Td (B) Tj ET EMC");
        Assert.Equal("Tables.ActualTextBoundary", Error(() => PdfTableExtraction.Read(split.CreateSnapshot(), new[] { 0 }, new(PdfTableDetectionMode.Ruled))));
        using var empty = Pdf.Create().AddPage();
        var result = PdfTableExtraction.Read(empty.CreateSnapshot(), new[] { 0 }, new(PdfTableDetectionMode.Borderless));
        Assert.Empty(result.Tables);
        Assert.Contains(result.OperationReport.Diagnostics, d => d.Code == "Tables.NoCandidate");
        using var json = new MemoryStream();
        result.WriteJson(json);
        Assert.Contains("\"tables\":[]", Encoding.UTF8.GetString(json.ToArray()));
    }

    [Fact]
    public void BothLanguageConsumersAndExplicitHeaderBoundsUsePublicContracts()
    {
        using var doc = Fixture();
        var source = doc.CreateSnapshot();
        Assert.Equal(3, ContractExamples.CSharp.ContractExample.ExtractTables(source, PdfTableDetectionMode.Ruled).Tables[0].Cells.Count);
        Assert.Equal(3, ContractExamples.VisualBasic.ContractExample.ExtractTables(source, PdfTableDetectionMode.Ruled).Tables[0].Cells.Count);
        Assert.Equal("Tables.HeaderRows", Error(() => PdfTableExtraction.Read(source, new[] { 0 }, new(PdfTableDetectionMode.Ruled, headerRows: 3))));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void UnsupportedCellTextDirectionsRejectExplicitly(bool rotated)
    {
        const string rules = "20 280 m 140 280 l 140 320 l 20 320 l h S 20 300 m 140 300 l S 80 280 m 80 320 l S ";
        string text = rotated ? "BT /F1 10 Tf 0 1 -1 0 40 285 Tm (A) Tj ET" : "/Span << /ActualText <FEFF05D0> >> BDC BT /F1 10 Tf 25 287 Td (A) Tj ET EMC";
        using var document = ExtractionTests.Fixture(rules + text);
        Assert.Equal("Tables.TextDirectionProfile", Error(() => PdfTableExtraction.Read(document.CreateSnapshot(), new[] { 0 }, new(PdfTableDetectionMode.Ruled))));
    }

    [Fact]
    public void UnrelatedRotatedTextOutsideTheLiteralGridDoesNotRejectItsCells()
    {
        const string rules = "20 280 m 140 280 l 140 320 l 20 320 l h S 20 300 m 140 300 l S 80 280 m 80 320 l S ";
        using var document = ExtractionTests.Fixture(rules + "BT /F1 10 Tf 25 287 Td (B) Tj ET BT /F1 10 Tf 0 1 -1 0 220 200 Tm (A) Tj ET");
        var table = Assert.Single(PdfTableExtraction.Read(document.CreateSnapshot(), new[] { 0 }, new(PdfTableDetectionMode.Ruled)).Tables);
        Assert.Equal(4, table.Cells.Count);
        Assert.Equal("B", Assert.Single(table.Cells, c => c.Characters.Count != 0).Text);
    }
}
