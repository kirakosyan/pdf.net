using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
internal static class PdfTableExport
{
    internal static PdfOperationReport Write(PdfTableExtractionResult result, Stream output, bool json, PdfMergedCellCsvPolicy merged, int sequence, bool omitHeaders, bool escape, PdfOperationPolicy? policy, CancellationToken token)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        if (!Enum.IsDefined(typeof(PdfMergedCellCsvPolicy), merged))
            throw new ArgumentOutOfRangeException(nameof(merged));
        if (!json && (sequence < 0 || sequence >= result.Sequences.Count))
            throw new ArgumentOutOfRangeException(nameof(sequence));
        return PdfFont.Run(json ? "ExportTableJson" : "ExportTableCsv", "managed.tables.extraction", policy, token, c =>
        {
            using var lease = new BorrowedStream(output, false, c);
            Serialize(result, lease, json, merged, sequence, omitHeaders, escape, c);
        }, PdfCapability.ReadDocument);
    }

    internal static PdfOperationReport File(PdfTableExtractionResult result, string path, PdfOperationPolicy? policy, CancellationToken token)
    {
        Guard.Text(path, nameof(path), 32767);
        return PdfFont.Run("ExportTableJsonFile", "managed.tables.extraction", policy, token, c => PdfAtomicFile.Write(path, c, output =>
        {
            using var lease = new BorrowedStream(output, false, c);
            Serialize(result, lease, true, PdfMergedCellCsvPolicy.AnchorOnly, 0, false, false, c);
        }), PdfCapability.ReadDocument);
    }

    private static void Serialize(PdfTableExtractionResult result, Stream output, bool json, PdfMergedCellCsvPolicy merged, int sequence, bool omitHeaders, bool escape, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        using var writer = new StreamWriter(output, new UTF8Encoding(false, true), 1024, true);
        if (json)
            Json();
        else
            Csv();
        writer.Flush();
        void String(string value)
        {
            writer.Write('"');
            foreach (char character in value)
            {
                c.Checkpoint();
                if (character == '"')
                    writer.Write("\\\"");
                else if (character == '\\')
                    writer.Write("\\\\");
                else if (character < 32)
                {
                    writer.Write("\\u");
                    writer.Write(((int)character).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                    writer.Write(character);
            }

            writer.Write('"');
        }

        void Integer(int value) => writer.Write(value.ToString(CultureInfo.InvariantCulture));
        void Number(double value) => writer.Write(value.ToString("R", CultureInfo.InvariantCulture));
        void Box(PdfRect bounds)
        {
            writer.Write('[');
            Number(bounds.X);
            writer.Write(',');
            Number(bounds.Y);
            writer.Write(',');
            Number(bounds.Width);
            writer.Write(',');
            Number(bounds.Height);
            writer.Write(']');
        }

        void Json()
        {
            writer.Write("{\"version\":1,\"tables\":[");
            bool firstTable = true;
            foreach (var table in result.Tables)
            {
                c.Checkpoint();
                c.Consume(PdfResourceKind.Objects, 1);
                if (!firstTable)
                    writer.Write(',');
                firstTable = false;
                writer.Write("{\"page\":");
                Integer(table.PageIndex);
                writer.Write(",\"rows\":");
                Integer(table.RowCount);
                writer.Write(",\"columns\":");
                Integer(table.ColumnCount);
                writer.Write(",\"mode\":");
                String(table.Mode.ToString());
                writer.Write(",\"confidence\":");
                Number(table.Confidence);
                writer.Write(",\"bounds\":");
                Box(table.Bounds);
                writer.Write(",\"cells\":[");
                bool firstCell = true;
                foreach (var cell in table.Cells)
                {
                    c.Checkpoint();
                    c.Consume(PdfResourceKind.Objects, 1);
                    if (!firstCell)
                        writer.Write(',');
                    firstCell = false;
                    writer.Write("{\"row\":");
                    Integer(cell.Row);
                    writer.Write(",\"column\":");
                    Integer(cell.Column);
                    writer.Write(",\"rowSpan\":");
                    Integer(cell.RowSpan);
                    writer.Write(",\"columnSpan\":");
                    Integer(cell.ColumnSpan);
                    writer.Write(",\"bounds\":");
                    Box(cell.Bounds);
                    writer.Write(",\"confidence\":");
                    Number(cell.Confidence);
                    writer.Write(",\"text\":");
                    String(cell.Text);
                    writer.Write(",\"kind\":");
                    String(cell.Number.HasValue ? "decimal" : "text");
                    writer.Write(",\"value\":");
                    if (cell.Number.HasValue)
                        writer.Write(cell.Number.Value.ToString(CultureInfo.InvariantCulture));
                    else
                        String(cell.Text);
                    writer.Write(",\"sourceCharacters\":[");
                    bool first = true;
                    foreach (var character in cell.Characters)
                    {
                        c.Checkpoint();
                        c.Consume(PdfResourceKind.Objects, 1);
                        if (!first)
                            writer.Write(',');
                        first = false;
                        writer.Write("{\"index\":");
                        Integer(character.Index);
                        writer.Write(",\"operation\":");
                        Integer(character.OperationIndex);
                        writer.Write(",\"mapping\":");
                        String(character.Confidence.ToString());
                        writer.Write(",\"estimated\":");
                        writer.Write(character.GeometryEstimated ? "true" : "false");
                        writer.Write('}');
                    }

                    writer.Write("],\"ruleSources\":[");
                    first = true;
                    foreach (var source in cell.RuleSources)
                    {
                        c.Checkpoint();
                        c.Consume(PdfResourceKind.Objects, 1);
                        if (!first)
                            writer.Write(',');
                        first = false;
                        writer.Write("{\"page\":");
                        Integer(source.PageIndex);
                        writer.Write(",\"object\":");
                        Integer(source.ContainerObjectId.Number);
                        writer.Write(",\"generation\":");
                        Integer(source.ContainerObjectId.Generation);
                        writer.Write(",\"operation\":");
                        Integer(source.OperationIndex);
                        writer.Write(",\"formCalls\":[");
                        for (int i = 0; i < source.FormInvocationIndices.Count; i++)
                        {
                            c.Checkpoint();
                            if (i != 0)
                                writer.Write(',');
                            Integer(source.FormInvocationIndices[i]);
                        }

                        writer.Write("]}");
                    }

                    writer.Write("]}");
                }

                writer.Write("]}");
            }

            writer.Write("],\"sequences\":[");
            bool initial = true;
            foreach (var group in result.Sequences)
            {
                c.Checkpoint();
                if (!initial)
                    writer.Write(',');
                initial = false;
                writer.Write("{\"tables\":[");
                for (int i = 0; i < group.TableIndices.Count; i++)
                {
                    c.Checkpoint();
                    if (i != 0)
                        writer.Write(',');
                    Integer(group.TableIndices[i]);
                }

                writer.Write("],\"repeatedHeaderRows\":");
                Integer(group.RepeatedHeaderRows);
                writer.Write('}');
            }

            writer.Write("]}");
        }

        void Csv()
        {
            var group = result.Sequences[sequence];
            for (int part = 0; part < group.TableIndices.Count; part++)
            {
                c.Checkpoint();
                var table = result.Tables[group.TableIndices[part]];
                int count = checked(table.RowCount * table.ColumnCount);
                c.Consume(PdfResourceKind.ExpandedBytes, count * 16L + 64);
                c.Consume(PdfResourceKind.Objects, count);
                var grid = new PdfExtractedTableCell? [count];
                foreach (var cell in table.Cells)
                    for (int row = cell.Row; row < cell.Row + cell.RowSpan; row++)
                        for (int col = cell.Column; col < cell.Column + cell.ColumnSpan; col++)
                        {
                            c.Checkpoint();
                            grid[row * table.ColumnCount + col] = cell;
                        }

                for (int row = omitHeaders && part != 0 ? group.RepeatedHeaderRows : 0; row < table.RowCount; row++)
                {
                    for (int col = 0; col < table.ColumnCount; col++)
                    {
                        c.Checkpoint();
                        if (col != 0)
                            writer.Write(',');
                        var cell = grid[row * table.ColumnCount + col];
                        string value = cell == null || merged == PdfMergedCellCsvPolicy.AnchorOnly && (cell.Row != row || cell.Column != col) ? "" : cell.Text;
                        writer.Write('"');
                        if (escape && cell != null && !cell.Number.HasValue && value.Length != 0 && "=+-@".IndexOf(value[0]) >= 0)
                            writer.Write('\'');
                        foreach (char character in value)
                        {
                            c.Checkpoint();
                            if (character == '"')
                                writer.Write('"');
                            writer.Write(character);
                        }

                        writer.Write('"');
                    }

                    writer.Write("\r\n");
                }
            }
        }
    }
}
