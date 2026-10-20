using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace Fluent.Pdf.Formats;
/// <summary>Explicit spreadsheet data interchange families. Binary XLS requires a separate qualified adapter.</summary>
public enum PdfSpreadsheetFormat
{
    /// <summary>UTF8 RFC4180 comma-separated text.</summary>
    Csv,
    /// <summary>Office 2003 XML data.</summary>
    SpreadsheetMl,
    /// <summary>Open XML workbook data.</summary>
    Xlsx,
    /// <summary>OpenDocument spreadsheet data.</summary>
    Ods,
    /// <summary>Binary XLS; unavailable in this managed profile.</summary>
    Xls
}
/// <summary>Explicit scalar type; numeric/date inference from styles or locale never occurs.</summary>
public enum PdfSheetValueKind
{
    /// <summary>Literal Unicode text.</summary>
    Text,
    /// <summary>Invariant decimal.</summary>
    Number,
    /// <summary>Boolean true/false.</summary>
    Boolean,
    /// <summary>ISO calendar date.</summary>
    Date
}
/// <summary>Immutable cell data, rectangular span and optional passive source provenance.</summary>
public sealed class PdfSheetCell
{
    /// <summary>Captures a bounded explicit scalar. Formula records are observational cached-value provenance, never executable output.</summary>
    public PdfSheetCell(int row, int column, string value, PdfSheetValueKind kind = PdfSheetValueKind.Text, int rowSpan = 1, int columnSpan = 1, string? sourceFormula = null, int? sourcePage = null, PdfRect? sourceBounds = null)
    {
        if (row < 0 || column < 0 || row > 1048575 || column > 16383 || rowSpan < 1 || columnSpan < 1 || rowSpan > 100000 || columnSpan > 1024) throw new ArgumentOutOfRangeException(nameof(row));
        if (value == null || value.Length > 32767) throw new ArgumentException("A nonnull scalar of at most 32767 characters is required.", nameof(value)); if (!Enum.IsDefined(typeof(PdfSheetValueKind), kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (kind == PdfSheetValueKind.Number) { if (!decimal.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out decimal number)) throw new ArgumentException("A finite invariant decimal is required.", nameof(value)); value = number.ToString(CultureInfo.InvariantCulture); }
        if (kind == PdfSheetValueKind.Boolean && value != "true" && value != "false") throw new ArgumentException("A canonical boolean is required.", nameof(value));
        if (kind == PdfSheetValueKind.Date && !DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _)) throw new ArgumentException("An ISO calendar date is required.", nameof(value));
        if (sourceFormula?.Length > 32767) throw new ArgumentException("Source formula provenance exceeds its extent.", nameof(sourceFormula)); if (sourcePage < 0) throw new ArgumentOutOfRangeException(nameof(sourcePage));
        Row = row; Column = column; Value = value; Kind = kind; RowSpan = rowSpan; ColumnSpan = columnSpan; SourceFormula = sourceFormula; SourcePage = sourcePage; SourceBounds = sourceBounds;
    }
    /// <summary>Gets zero-based row.</summary>
    public int Row { get; }
    /// <summary>Gets zero-based column.</summary>
    public int Column { get; }
    /// <summary>Gets canonical scalar data.</summary>
    public string Value { get; }
    /// <summary>Gets explicit type.</summary>
    public PdfSheetValueKind Kind { get; }
    /// <summary>Gets vertical span.</summary>
    public int RowSpan { get; }
    /// <summary>Gets horizontal span.</summary>
    public int ColumnSpan { get; }
    /// <summary>Gets passive source formula, never executed or emitted as a formula.</summary>
    public string? SourceFormula { get; }
    /// <summary>Gets optional original PDF page.</summary>
    public int? SourcePage { get; }
    /// <summary>Gets optional original displayed PDF bounds.</summary>
    public PdfRect? SourceBounds { get; }
}
/// <summary>Immutable bounded data grid. This profile reconstructs data, without original print styling or charts.</summary>
public sealed class PdfSheetData
{
    /// <summary>Captures a valid explicit sheet name and at most 100000 grid positions/1024 columns, with non-overlapping in-bounds spans.</summary>
    public PdfSheetData(string name, int rows, int columns, IEnumerable<PdfSheetCell> cells)
    {
        Guard.Text(name, nameof(name), 31); if (name.IndexOfAny(new[] { '[', ']', ':', '*', '?', '/', '\\' }) >= 0 || name[0] == '\'' || name[name.Length - 1] == '\'') throw new ArgumentException("A valid explicit sheet name is required.", nameof(name));
        if (rows < 1 || columns < 1 || columns > 1024 || rows > 100000 || (long)rows * columns > 100000) throw new ArgumentOutOfRangeException(nameof(rows));
        var captured = new List<PdfSheetCell>(); var occupied = new HashSet<int>();
        foreach (var cell in cells ?? throw new ArgumentNullException(nameof(cells)))
        {
            if (cell == null || captured.Count == 100000 || cell.Row > rows - cell.RowSpan || cell.Column > columns - cell.ColumnSpan) throw new ArgumentException("Cell spans must fit the finite grid.", nameof(cells));
            for (int r = cell.Row; r < cell.Row + cell.RowSpan; r++) for (int col = cell.Column; col < cell.Column + cell.ColumnSpan; col++) if (!occupied.Add(r * columns + col)) throw new ArgumentException("Cell spans cannot overlap.", nameof(cells)); captured.Add(cell);
        }
        Name = name; RowCount = rows; ColumnCount = columns; Cells = captured.OrderBy(cell => cell.Row).ThenBy(cell => cell.Column).ToList().AsReadOnly();
    }
    /// <summary>Gets explicit name.</summary>
    public string Name { get; }
    /// <summary>Gets row count.</summary>
    public int RowCount { get; }
    /// <summary>Gets column count.</summary>
    public int ColumnCount { get; }
    /// <summary>Gets ordered grid-origin cells.</summary>
    public IReadOnlyList<PdfSheetCell> Cells { get; }
}
/// <summary>Original bounded spreadsheet data interchange. No formula execution, ambient locale or binary XLS fallback occurs.</summary>
public static class PdfSpreadsheets
{
    private static readonly XNamespace Ss = "urn:schemas-microsoft-com:office:spreadsheet", X = "http://schemas.openxmlformats.org/spreadsheetml/2006/main", R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships", Rel = "http://schemas.openxmlformats.org/package/2006/relationships", O = "urn:oasis:names:tc:opendocument:xmlns:office:1.0", T = "urn:oasis:names:tc:opendocument:xmlns:table:1.0", P = "urn:oasis:names:tc:opendocument:xmlns:text:1.0";
    /// <summary>Captures an inferred PDF table with typed values and original page/bounds. Numeric typing comes only from prior explicit-culture extraction.</summary>
    public static PdfSheetData FromTable(PdfExtractedTable table, string sheetName, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (table == null) throw new ArgumentNullException(nameof(table)); PdfSheetData? result = null;
        PdfFont.Run("CaptureSpreadsheetTable", "managed.spreadsheet-data", policy, cancellationToken, c => { Charge(table.RowCount, table.ColumnCount, c); result = new(sheetName, table.RowCount, table.ColumnCount, table.Cells.Select(cell => { c.Checkpoint(); return new PdfSheetCell(cell.Row, cell.Column, cell.Number?.ToString(CultureInfo.InvariantCulture) ?? cell.Text, cell.Number.HasValue ? PdfSheetValueKind.Number : PdfSheetValueKind.Text, cell.RowSpan, cell.ColumnSpan, sourcePage: cell.PageIndex, sourceBounds: cell.Bounds); })); }, PdfCapability.ReadDocument); return result!;
    }
    /// <summary>Reads one explicitly named sheet as data. Formula cells require cached values; original formulas are passive provenance. Binary XLS rejects before reading input.</summary>
    public static PdfSheetData ReadData(Stream input, PdfSpreadsheetFormat format, string sheetName, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfSheetData? result = null; PdfFont.Run("ReadSpreadsheetData", "managed.spreadsheet-data", policy, cancellationToken, c => { Available(format, c); try { result = ReadCore(input, format, sheetName, c); } catch (Exception ex) when (ex is ArgumentException || ex is InvalidDataException || ex is OverflowException || ex is FormatException) { c.Checkpoint(); throw Error(c, "Data", "Malformed, ambiguous or out-of-profile spreadsheet data rejects."); } }, PdfCapability.ReadDocument); return result!;
    }
    /// <summary>Writes one data sheet with original rectangular merges. All formulas remain data; dangerous untyped CSV text rejects. Output is staged before caller writes.</summary>
    public static PdfOperationReport WriteData(PdfSheetData sheet, PdfSpreadsheetFormat format, Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (sheet == null) throw new ArgumentNullException(nameof(sheet)); if (output == null || !output.CanWrite) throw new ArgumentException("A writable borrowed stream is required.", nameof(output));
        return PdfFont.Run("WriteSpreadsheetData", "managed.spreadsheet-data", policy, cancellationToken, c => { Available(format, c); Charge(sheet.RowCount, sheet.ColumnCount, c); ChargeWriting(sheet, c); FormatIo.Copy(WriteCore(sheet, format, c), output, c); }, PdfCapability.WriteDocument);
    }
    /// <summary>Writes a separate passive XML provenance ledger, including cached source formulas and PDF coordinates. It is never an executable spreadsheet part.</summary>
    public static PdfOperationReport WriteProvenance(PdfSheetData sheet, Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (sheet == null) throw new ArgumentNullException(nameof(sheet)); return PdfFont.Run("WriteSpreadsheetProvenance", "managed.spreadsheet-data", policy, cancellationToken, c => { Charge(sheet.RowCount, sheet.ColumnCount, c); ChargeWriting(sheet, c); var root = new XElement("sheet", new XAttribute("name", sheet.Name)); foreach (var cell in sheet.Cells) { c.Checkpoint(); var item = new XElement("cell", new XAttribute("row", cell.Row), new XAttribute("column", cell.Column), new XAttribute("rowSpan", cell.RowSpan), new XAttribute("columnSpan", cell.ColumnSpan)); if (cell.SourceFormula != null) item.Add(new XElement("sourceFormula", cell.SourceFormula)); if (cell.SourcePage.HasValue) item.Add(new XAttribute("sourcePage", cell.SourcePage.Value)); if (cell.SourceBounds is PdfRect b) item.Add(new XElement("bounds", new XAttribute("x", FormatIo.N(b.X)), new XAttribute("y", FormatIo.N(b.Y)), new XAttribute("width", FormatIo.N(b.Width)), new XAttribute("height", FormatIo.N(b.Height)))); root.Add(item); } FormatIo.Copy(XmlBytes(root, c), output, c); }, PdfCapability.WriteDocument);
    }
    /// <summary>Reconstructs an explicit zero-based rectangular range as a flow table with caller typography. Ranges crossing merged cells reject; original worksheet print layout is not asserted.</summary>
    public static PdfDocument ImportData(PdfSheetData sheet, int firstRow, int firstColumn, int rows, int columns, PdfFormatTextOptions text, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (sheet == null || text == null || providers == null) throw new ArgumentNullException(nameof(sheet)); if (firstRow < 0 || firstColumn < 0 || rows < 1 || columns < 1 || firstRow > sheet.RowCount - rows || firstColumn > sheet.ColumnCount - columns) throw new ArgumentOutOfRangeException(nameof(firstRow));
        PdfDocument? result = null; var finish = new List<Action<PdfOperationReport>>(); try { var report = PdfFont.Run("ImportSpreadsheetData", "managed.spreadsheet-data", policy, cancellationToken, c => {
            Charge(rows, columns, c); var origins = new Dictionary<int, PdfSheetCell>(); var covered = new HashSet<int>();
            foreach (var cell in sheet.Cells) { c.Checkpoint(); if (cell.Row >= firstRow + rows || cell.Column >= firstColumn + columns || cell.Row + cell.RowSpan <= firstRow || cell.Column + cell.ColumnSpan <= firstColumn) continue; if (cell.Row < firstRow || cell.Column < firstColumn || cell.Row + cell.RowSpan > firstRow + rows || cell.Column + cell.ColumnSpan > firstColumn + columns) throw Error(c, "RangeMerge", "The selected range cuts through a merged cell."); int r = cell.Row - firstRow, col = cell.Column - firstColumn; origins.Add(r * columns + col, cell); for (int dr = 0; dr < cell.RowSpan; dr++) for (int dc = 0; dc < cell.ColumnSpan; dc++) if (dr != 0 || dc != 0) covered.Add((r + dr) * columns + col + dc); }
            var tableRows = new List<PdfTableRow>(); for (int r = 0; r < rows; r++) { var cells = new List<PdfTableCell>(); for (int col = 0; col < columns; col++) { c.Checkpoint(); int key = r * columns + col; if (covered.Contains(key)) continue; origins.TryGetValue(key, out var cell); cells.Add(PdfTableCell.Text(cell?.Value ?? "", text.Body, cell?.ColumnSpan ?? 1, cell?.RowSpan ?? 1)); } tableRows.Add(new(cells)); }
            var table = PdfTable.CreateCore(Enumerable.Range(0, columns).Select(_ => PdfTableColumn.Auto()).ToArray(), tableRows, 0, true, 100000, c); result = FormatText.Build(new[] { table }, text, providers, c, finish);
        }, PdfCapability.WriteDocument); foreach (var complete in finish) complete(report); result!.SetCompositionReport(report); return result; } catch { result?.Dispose(); throw; }
    }
    private static void ChargeWriting(PdfSheetData sheet, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.Objects, 512); c.Consume(PdfResourceKind.ExpandedBytes, 65536);
        foreach (var cell in sheet.Cells) { c.Checkpoint(); c.Consume(PdfResourceKind.Objects, 12); c.Consume(PdfResourceKind.ExpandedBytes, 1024 + cell.Value.Length * 6L + (cell.SourceFormula?.Length ?? 0) * 4L); }
    }
    private static void Available(PdfSpreadsheetFormat format, PdfOperationContext c) { if (!Enum.IsDefined(typeof(PdfSpreadsheetFormat), format)) throw new ArgumentOutOfRangeException(nameof(format)); if (format == PdfSpreadsheetFormat.Xls) throw Error(c, "AdapterUnavailable", "Binary XLS needs an explicitly installed qualified adapter; no alternative format is substituted."); }
    private static void Charge(int rows, int columns, PdfOperationContext c) { c.Checkpoint(); if (rows < 1 || columns < 1 || rows > 100000 || columns > 1024 || (long)rows * columns > 100000) throw Error(c, "GridLimit", "This data profile accepts at most 100000 grid positions and 1024 columns."); c.Consume(PdfResourceKind.Objects, (long)rows * columns); c.Consume(PdfResourceKind.ExpandedBytes, (long)rows * columns * 256); }
    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Spreadsheet." + code, message);
    private static PdfByteStore XmlBytes(XElement root, PdfOperationContext c) { using var output = new PdfCodecBufferStream(c, 16777216); using (var writer = FormatIo.Writer(output)) { root.WriteTo(writer); writer.Flush(); } return output.Finish(); }
    private static byte[] Raw(PdfByteStore bytes, PdfOperationContext c) { c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length); var result = new byte[(int)bytes.Length]; using var input = bytes.Open(0, bytes.Length); FormatIo.Exact(input, result, c); return result; }
    private static string Address(int row, int column) { string letters = ""; for (int value = column + 1; value > 0; value = (value - 1) / 26) letters = (char)('A' + (value - 1) % 26) + letters; return letters + (row + 1).ToString(CultureInfo.InvariantCulture); }
    private static (int Row, int Column) Address(string value, PdfOperationContext c) { int at = 0, col = 0; while (at < value.Length && value[at] >= 'A' && value[at] <= 'Z') { col = checked(col * 26 + value[at++] - 'A' + 1); if (col > 1024) throw Error(c, "GridLimit", "Column exceeds the data profile."); } if (col == 0 || !int.TryParse(value.Substring(at), NumberStyles.None, CultureInfo.InvariantCulture, out int row) || row < 1 || row > 100000) throw Error(c, "Address", "A bounded canonical cell reference is required."); return (row - 1, col - 1); }
    private static int Positive(string? value, int fallback, PdfOperationContext c) { if (value == null) return fallback; if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n < 1 || n > 100000) throw Error(c, "GridLimit", "A bounded positive grid index/repetition is required."); return n; }
    private static PdfSheetData Grid(string name, List<PdfSheetCell> cells, PdfOperationContext c, int rows = 1, int columns = 1) { foreach (var cell in cells) { rows = Math.Max(rows, checked(cell.Row + cell.RowSpan)); columns = Math.Max(columns, checked(cell.Column + cell.ColumnSpan)); } Charge(rows, columns, c); return new(name, rows, columns, cells); }
    private static PdfSheetCell Cell(int row, int col, string value, string type, int rs, int cs, string? formula, PdfOperationContext c) { c.Checkpoint(); c.Consume(PdfResourceKind.Objects, 1); c.Consume(PdfResourceKind.ExpandedBytes, value.Length * 4L + 256); var kind = type switch { "String" or "string" or "inlineStr" or "s" or "str" or "" => PdfSheetValueKind.Text, "Number" or "float" or "currency" or "percentage" or "n" => PdfSheetValueKind.Number, "Boolean" or "boolean" or "b" => PdfSheetValueKind.Boolean, "DateTime" or "date" or "d" => PdfSheetValueKind.Date, _ => throw Error(c, "CellType", "Unsupported explicit spreadsheet cell type.") }; if (kind == PdfSheetValueKind.Boolean) value = value == "1" || value == "true" ? "true" : value == "0" || value == "false" ? "false" : throw Error(c, "Boolean", "Invalid boolean data."); if (kind == PdfSheetValueKind.Date && (value.EndsWith("T00:00:00", StringComparison.Ordinal) || value.EndsWith("T00:00:00.000", StringComparison.Ordinal))) value = value.Substring(0, 10); return new(row, col, value, kind, rs, cs, formula); }
    private static PdfSheetData ReadCore(Stream input, PdfSpreadsheetFormat format, string name, PdfOperationContext c)
    {
        if (format == PdfSpreadsheetFormat.Csv) return ReadCsv(FormatIo.Text(input, c), name, c);
        if (format == PdfSpreadsheetFormat.SpreadsheetMl)
        {
            var xml = FormatIo.Xml(input, c); if (xml.Root?.Name != Ss + "Workbook") throw Error(c, "Root", "A SpreadsheetML workbook is required."); var sheets = xml.Root.Elements(Ss + "Worksheet").Where(s => (string?)s.Attribute(Ss + "Name") == name).ToArray(); if (sheets.Length != 1) throw Error(c, "Sheet", "Exactly one explicitly named sheet is required."); var cells = new List<PdfSheetCell>(); int row = 0;
            foreach (var r in sheets[0].Element(Ss + "Table")?.Elements(Ss + "Row") ?? Enumerable.Empty<XElement>()) { row = Positive((string?)r.Attribute(Ss + "Index"), row + 1, c) - 1; int col = 0; foreach (var cell in r.Elements(Ss + "Cell")) { col = Positive((string?)cell.Attribute(Ss + "Index"), col + 1, c) - 1; var data = cell.Element(Ss + "Data"); string? formula = (string?)cell.Attribute(Ss + "Formula"); if (formula != null && data == null) throw Error(c, "FormulaCache", "Formula cells require an explicit cached value."); int rs = Nonnegative((string?)cell.Attribute(Ss + "MergeDown"), c) + 1, cs = Nonnegative((string?)cell.Attribute(Ss + "MergeAcross"), c) + 1; cells.Add(Cell(row, col, data?.Value ?? "", (string?)data?.Attribute(Ss + "Type") ?? "String", rs, cs, formula, c)); col += cs; } row++; } return Grid(name, cells, c, Positive((string?)sheets[0].Element(Ss + "Table")?.Attribute(Ss + "ExpandedRowCount"), Math.Max(1, row), c), Positive((string?)sheets[0].Element(Ss + "Table")?.Attribute(Ss + "ExpandedColumnCount"), 1, c));
        }
        using var package = new FormatPackage(input, c);
        if (format == PdfSpreadsheetFormat.Xlsx)
        {
            if (package.Names.Any(p => p.EndsWith("vbaProject.bin", StringComparison.OrdinalIgnoreCase) || p.StartsWith("xl/externalLinks/", StringComparison.Ordinal) || p == "xl/connections.xml")) throw Error(c, "ActivePackage", "Macros and external workbook connections reject in this data profile.");
            var book = package.Xml("xl/workbook.xml"); if (book.Root?.Name != X + "workbook") throw Error(c, "Root", "A spreadsheet workbook root is required."); var sheet = book.Descendants(X + "sheet").Where(s => (string?)s.Attribute("name") == name).ToArray(); if (sheet.Length != 1) throw Error(c, "Sheet", "Exactly one explicitly named sheet is required."); string id = (string?)sheet[0].Attribute(R + "id") ?? ""; var links = package.Xml("xl/_rels/workbook.xml.rels"); var relations = links.Root?.Elements(Rel + "Relationship").ToArray() ?? Array.Empty<XElement>(); if (relations.Any(r => r.Attribute("TargetMode") != null)) throw Error(c, "ExternalReference", "External relationship targets reject."); var target = relations.Where(r => (string?)r.Attribute("Id") == id && (string?)r.Attribute("Type") == R.NamespaceName + "/worksheet").ToArray(); if (target.Length != 1) throw Error(c, "Relationship", "One unambiguous worksheet relationship is required."); string path = FormatPackage.Resolve("xl/workbook.xml", (string?)target[0].Attribute("Target") ?? "", c); var xml = package.Xml(path); if (xml.Root?.Name != X + "worksheet") throw Error(c, "Root", "A worksheet root is required."); var strings = package.Has("xl/sharedStrings.xml") ? package.Xml("xl/sharedStrings.xml").Descendants(X + "si").Select(v => string.Concat(v.Descendants(X + "t").Select(t => t.Value))).ToArray() : Array.Empty<string>();
            var cells = new List<PdfSheetCell>(); foreach (var cell in xml.Descendants(X + "sheetData").Elements(X + "row").Elements(X + "c")) { var pos = Address((string?)cell.Attribute("r") ?? "", c); string type = (string?)cell.Attribute("t") ?? "n"; string? formula = cell.Element(X + "f")?.Value; var v = cell.Element(X + "v"); if (formula != null && v == null) throw Error(c, "FormulaCache", "Formula cells require cached values."); string value = type == "inlineStr" ? string.Concat(cell.Element(X + "is")?.Descendants(X + "t").Select(t => t.Value) ?? Enumerable.Empty<string>()) : v?.Value ?? ""; if (type == "s") { int index = Nonnegative(value, c); if (index >= strings.Length) throw Error(c, "SharedString", "A shared-string index is missing."); value = strings[index]; } if (value.Length == 0 && type == "n" && formula == null) type = "str"; cells.Add(Cell(pos.Row, pos.Column, value, type, 1, 1, formula, c)); }
            foreach (var merge in xml.Descendants(X + "mergeCell")) { string[] ends = ((string?)merge.Attribute("ref") ?? "").Split(':'); if (ends.Length != 2) throw Error(c, "Merge", "A rectangular merged range is required."); var first = Address(ends[0], c); var last = Address(ends[1], c); int index = cells.FindIndex(cell => cell.Row == first.Row && cell.Column == first.Column); if (last.Row < first.Row || last.Column < first.Column) throw Error(c, "Merge", "A positive merge is required."); var old = index >= 0 ? cells[index] : new PdfSheetCell(first.Row, first.Column, ""); var merged = new PdfSheetCell(old.Row, old.Column, old.Value, old.Kind, last.Row - first.Row + 1, last.Column - first.Column + 1, old.SourceFormula); if (index >= 0) cells[index] = merged; else cells.Add(merged); }
            var extent = ((string?)xml.Root.Element(X + "dimension")?.Attribute("ref"))?.Split(':'); var end = extent == null ? (Row: 0, Column: 0) : Address(extent[extent.Length - 1], c); return Grid(name, cells, c, end.Row + 1, end.Column + 1);
        }
        if (package.Text("mimetype") != "application/vnd.oasis.opendocument.spreadsheet") throw Error(c, "Mimetype", "An ODS package is required."); var content = package.Xml("content.xml"); var tables = content.Descendants(T + "table").Where(t => (string?)t.Attribute(T + "name") == name).ToArray(); if (tables.Length != 1) throw Error(c, "Sheet", "Exactly one explicitly named sheet is required."); var odsCells = new List<PdfSheetCell>(); int rr = 0;
        foreach (var row in tables[0].Elements(T + "table-row")) { int repeats = Positive((string?)row.Attribute(T + "number-rows-repeated"), 1, c); if (repeats > 100000 - rr) throw Error(c, "GridLimit", "Repeated rows exceed the finite grid."); for (int repeat = 0; repeat < repeats; repeat++) { c.Checkpoint(); int col = 0; foreach (var cell in row.Elements()) { c.Checkpoint(); if (cell.Name != T + "table-cell" && cell.Name != T + "covered-table-cell") throw Error(c, "OdsElement", "Only explicit data cells are supported."); int count = Positive((string?)cell.Attribute(T + "number-columns-repeated"), 1, c); if (rr >= 100000 || col > 1024 - count || (long)(rr + 1) * (col + count) > 100000) throw Error(c, "GridLimit", "Repeated ODS data exceeds the finite grid."); if (cell.Name == T + "covered-table-cell") { col += count; continue; } if (cell.Elements(P + "p").Any(p => p.HasElements)) throw Error(c, "OdsText", "Structured ODS text requires a richer data adapter."); string type = (string?)cell.Attribute(O + "value-type") ?? "string"; string value = type == "string" ? string.Join("\n", cell.Elements(P + "p").Select(p => p.Value)) : (string?)cell.Attribute(O + (type == "boolean" ? "boolean-value" : type == "date" ? "date-value" : "value")) ?? throw Error(c, "FormulaCache", "Typed/formula cells require cached values."); string? formula = (string?)cell.Attribute(T + "formula"); if (formula != null && cell.Attribute(O + "value-type") == null) throw Error(c, "FormulaCache", "Formula cells require an explicitly typed cached value."); for (int i = 0; i < count; i++) odsCells.Add(Cell(rr, col++, value, type, Positive((string?)cell.Attribute(T + "number-rows-spanned"), 1, c), Positive((string?)cell.Attribute(T + "number-columns-spanned"), 1, c), formula, c)); } rr++; } } return Grid(name, odsCells, c, Math.Max(1, rr));
    }
    private static int Nonnegative(string? value, PdfOperationContext c) { if (value == null) return 0; if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out int n) || n < 0 || n > 100000) throw Error(c, "GridLimit", "A bounded nonnegative index is required."); return n; }
    private static PdfSheetData ReadCsv(string value, string name, PdfOperationContext c)
    {
        var cells = new List<PdfSheetCell>(); var field = new StringBuilder(); int row = 0, col = 0; bool quoted = false, closed = false, start = true, lastEnd = false;
        void Field() { cells.Add(Cell(row, col++, field.ToString(), "String", 1, 1, null, c)); field.Clear(); start = true; closed = false; if (col > 1024 || (long)(row + 1) * col > 100000) throw Error(c, "GridLimit", "CSV exceeds this grid profile."); }
        for (int i = 0; i < value.Length; i++) { c.Checkpoint(); char ch = value[i]; lastEnd = false; if (quoted) { if (ch == '"') { if (i + 1 < value.Length && value[i + 1] == '"') { field.Append('"'); i++; } else { quoted = false; closed = true; } } else field.Append(ch); } else if (ch == '"' && start) { quoted = true; start = false; } else if (ch == ',' || ch == '\r' || ch == '\n') { Field(); if (ch != ',') { if (ch == '\r' && i + 1 < value.Length && value[i + 1] == '\n') i++; row++; col = 0; lastEnd = true; } } else { if (closed || ch == '"') throw Error(c, "CsvSyntax", "RFC4180 field quoting is required."); field.Append(ch); start = false; } if (field.Length > 32767) throw Error(c, "CellLimit", "A cell exceeds the text extent."); }
        if (quoted) throw Error(c, "CsvSyntax", "A quoted field is incomplete."); if (!lastEnd || cells.Count == 0) Field(); return Grid(name, cells, c);
    }
    private static PdfByteStore WriteCore(PdfSheetData sheet, PdfSpreadsheetFormat format, PdfOperationContext c)
    {
        if (format == PdfSpreadsheetFormat.Csv)
        {
            if (sheet.Cells.Any(cell => cell.RowSpan != 1 || cell.ColumnSpan != 1)) throw Error(c, "CsvMerge", "CSV cannot preserve merged cells; select a typed container."); using var output = new PdfCodecBufferStream(c, 16777216); using (var writer = new StreamWriter(output, new UTF8Encoding(false, true), 1024, leaveOpen: true)) { int at = 0; for (int row = 0; row < sheet.RowCount; row++) { for (int col = 0; col < sheet.ColumnCount; col++) { c.Checkpoint(); if (col != 0) writer.Write(','); PdfSheetCell? cell = at < sheet.Cells.Count && sheet.Cells[at].Row == row && sheet.Cells[at].Column == col ? sheet.Cells[at++] : null; string value = cell?.Value ?? ""; string trimmed = value.TrimStart(); if (cell?.Kind == PdfSheetValueKind.Text && trimmed.Length > 0 && "=+-@".IndexOf(trimmed[0]) >= 0) throw Error(c, "CsvFormulaText", "Untyped CSV cannot preserve potentially executable formula-like text; select XLSX/ODS/XML literal strings."); writer.Write('"'); writer.Write(value.Replace("\"", "\"\"")); writer.Write('"'); } writer.Write("\r\n"); } writer.Flush(); } return output.Finish();
        }
        if (format == PdfSpreadsheetFormat.SpreadsheetMl) { var table = new XElement(Ss + "Table", new XAttribute(Ss + "ExpandedRowCount", sheet.RowCount), new XAttribute(Ss + "ExpandedColumnCount", sheet.ColumnCount)); foreach (var group in sheet.Cells.GroupBy(cell => cell.Row)) { var row = new XElement(Ss + "Row", new XAttribute(Ss + "Index", group.Key + 1)); foreach (var cell in group) { c.Checkpoint(); var item = new XElement(Ss + "Cell", new XAttribute(Ss + "Index", cell.Column + 1), new XElement(Ss + "Data", new XAttribute(Ss + "Type", cell.Kind == PdfSheetValueKind.Text ? "String" : cell.Kind == PdfSheetValueKind.Number ? "Number" : cell.Kind == PdfSheetValueKind.Boolean ? "Boolean" : "DateTime"), cell.Kind == PdfSheetValueKind.Date ? cell.Value + "T00:00:00" : cell.Kind == PdfSheetValueKind.Boolean ? cell.Value == "true" ? "1" : "0" : cell.Value)); if (cell.RowSpan > 1) item.Add(new XAttribute(Ss + "MergeDown", cell.RowSpan - 1)); if (cell.ColumnSpan > 1) item.Add(new XAttribute(Ss + "MergeAcross", cell.ColumnSpan - 1)); row.Add(item); } table.Add(row); } return XmlBytes(new XElement(Ss + "Workbook", new XElement(Ss + "Worksheet", new XAttribute(Ss + "Name", sheet.Name), table)), c); }
        var parts = new List<(string Name, byte[] Bytes, bool Stored)>(); void Add(string name, XElement root) => parts.Add((name, Raw(XmlBytes(root, c), c), false));
        if (format == PdfSpreadsheetFormat.Xlsx)
        {
            XNamespace ct = "http://schemas.openxmlformats.org/package/2006/content-types"; Add("[Content_Types].xml", new XElement(ct + "Types", new XElement(ct + "Default", new XAttribute("Extension", "rels"), new XAttribute("ContentType", "application/vnd.openxmlformats-package.relationships+xml")), new XElement(ct + "Override", new XAttribute("PartName", "/xl/workbook.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")), new XElement(ct + "Override", new XAttribute("PartName", "/xl/worksheets/sheet1.xml"), new XAttribute("ContentType", "application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))); Add("_rels/.rels", new XElement(Rel + "Relationships", Relationship("r1", "officeDocument", "xl/workbook.xml"))); Add("xl/workbook.xml", new XElement(X + "workbook", new XElement(X + "sheets", new XElement(X + "sheet", new XAttribute("name", sheet.Name), new XAttribute("sheetId", "1"), new XAttribute(R + "id", "r1"))))); Add("xl/_rels/workbook.xml.rels", new XElement(Rel + "Relationships", Relationship("r1", "worksheet", "worksheets/sheet1.xml")));
            var data = new XElement(X + "sheetData"); foreach (var group in sheet.Cells.GroupBy(cell => cell.Row)) { var row = new XElement(X + "row", new XAttribute("r", group.Key + 1)); foreach (var cell in group) { c.Checkpoint(); var item = new XElement(X + "c", new XAttribute("r", Address(cell.Row, cell.Column)), new XAttribute("t", cell.Kind == PdfSheetValueKind.Text ? "inlineStr" : cell.Kind == PdfSheetValueKind.Number ? "n" : cell.Kind == PdfSheetValueKind.Boolean ? "b" : "d")); item.Add(cell.Kind == PdfSheetValueKind.Text ? new XElement(X + "is", new XElement(X + "t", new XAttribute(XNamespace.Xml + "space", "preserve"), cell.Value)) : new XElement(X + "v", cell.Kind == PdfSheetValueKind.Boolean ? cell.Value == "true" ? "1" : "0" : cell.Kind == PdfSheetValueKind.Date ? cell.Value + "T00:00:00.000" : cell.Value)); row.Add(item); } data.Add(row); } var ws = new XElement(X + "worksheet", new XElement(X + "dimension", new XAttribute("ref", "A1:" + Address(sheet.RowCount - 1, sheet.ColumnCount - 1))), data); var merges = sheet.Cells.Where(cell => cell.RowSpan > 1 || cell.ColumnSpan > 1).ToArray(); if (merges.Length > 0) ws.Add(new XElement(X + "mergeCells", new XAttribute("count", merges.Length), merges.Select(cell => new XElement(X + "mergeCell", new XAttribute("ref", Address(cell.Row, cell.Column) + ":" + Address(cell.Row + cell.RowSpan - 1, cell.Column + cell.ColumnSpan - 1)))))); Add("xl/worksheets/sheet1.xml", ws);
        }
        else
        {
            const string mime = "application/vnd.oasis.opendocument.spreadsheet"; parts.Add(("mimetype", Encoding.ASCII.GetBytes(mime), true)); XNamespace manifest = "urn:oasis:names:tc:opendocument:xmlns:manifest:1.0"; Add("META-INF/manifest.xml", new XElement(manifest + "manifest", new XAttribute(manifest + "version", "1.3"), new XElement(manifest + "file-entry", new XAttribute(manifest + "full-path", "/"), new XAttribute(manifest + "media-type", mime)), new XElement(manifest + "file-entry", new XAttribute(manifest + "full-path", "content.xml"), new XAttribute(manifest + "media-type", "text/xml")))); var table = new XElement(T + "table", new XAttribute(T + "name", sheet.Name)); var byPosition = sheet.Cells.ToDictionary(cell => cell.Row * sheet.ColumnCount + cell.Column); var covered = new HashSet<int>(); foreach (var cell in sheet.Cells) for (int r = 0; r < cell.RowSpan; r++) for (int col = 0; col < cell.ColumnSpan; col++) if (r != 0 || col != 0) covered.Add((cell.Row + r) * sheet.ColumnCount + cell.Column + col);
            for (int r = 0; r < sheet.RowCount; r++) { var row = new XElement(T + "table-row"); for (int col = 0; col < sheet.ColumnCount; col++) { c.Checkpoint(); int key = r * sheet.ColumnCount + col; if (covered.Contains(key)) { row.Add(new XElement(T + "covered-table-cell")); continue; } byPosition.TryGetValue(key, out var cell); string type = cell?.Kind == PdfSheetValueKind.Number ? "float" : cell?.Kind == PdfSheetValueKind.Boolean ? "boolean" : cell?.Kind == PdfSheetValueKind.Date ? "date" : "string"; var item = new XElement(T + "table-cell", new XAttribute(O + "value-type", type)); if (type != "string") item.Add(new XAttribute(O + (type == "boolean" ? "boolean-value" : type == "date" ? "date-value" : "value"), cell!.Value)); item.Add(new XElement(P + "p", cell?.Value ?? "")); if (cell?.RowSpan > 1) item.Add(new XAttribute(T + "number-rows-spanned", cell.RowSpan)); if (cell?.ColumnSpan > 1) item.Add(new XAttribute(T + "number-columns-spanned", cell.ColumnSpan)); row.Add(item); } table.Add(row); } Add("content.xml", new XElement(O + "document-content", new XAttribute(O + "version", "1.3"), new XElement(O + "body", new XElement(O + "spreadsheet", table))));
        }
        return FormatPackage.Write(parts, c);
    }
    private static XElement Relationship(string id, string type, string target) => new(Rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", R.NamespaceName + "/" + type), new XAttribute("Target", target));
}
