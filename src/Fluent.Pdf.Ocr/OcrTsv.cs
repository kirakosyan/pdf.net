using System.Globalization;

namespace Fluent.Pdf.Ocr;
// Original bounded TSV interpretation. Raw engine order stays separate from inferred page reading order.
internal static class OcrTsv
{
    private const string Header = "level\tpage_num\tblock_num\tpar_num\tline_num\tword_num\tleft\ttop\twidth\theight\tconf\ttext";
    internal static List<PdfOcrWord> Read(string tsv, PdfOcrRaster raster, PdfOcrOptions options, int rawCharacters, PdfOperationContext c)
    {
        if (rawCharacters > options.MaximumCharacters)
            throw Error("CharacterLimit", "Recognized Unicode exceeds the explicit character ceiling.");
        var words = new List<PdfOcrWord>();
        int at = 0, rows = 0;
        long characters = rawCharacters;
        while (at < tsv.Length)
        {
            c.Checkpoint();
            int end = tsv.IndexOf('\n', at);
            if (end < 0)
                end = tsv.Length;
            int length = end - at;
            if (length > 0 && tsv[end - 1] == '\r')
                length--;
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, length * 8L + 256);
            string row = tsv.Substring(at, length);
            at = end == tsv.Length ? end : end + 1;
            if (rows++ == 0)
            {
                if (row != Header)
                    throw Error("TsvHeader", "The engine TSV header does not match the explicit word-geometry profile.");
                continue;
            }

            if (row.Length == 0)
                continue;
            if (rows > 1000001)
                throw Error("TsvRows", "The engine TSV exceeds the finite row ceiling.");
            var fields = new string[12];
            int position = 0;
            for (int i = 0; i < 11; i++)
            {
                int tab = row.IndexOf('\t', position);
                if (tab < 0)
                    throw Error("TsvFields", "The engine TSV record is incomplete.");
                fields[i] = row.Substring(position, tab - position);
                position = tab + 1;
            }

            fields[11] = row.Substring(position);
            int level = Number(0);
            if (level < 1 || level > 5 || Number(1) != 1)
                throw Error("TsvIdentity", "The engine TSV page/level identity is invalid.");
            int block = Number(2), paragraph = Number(3), line = Number(4), word = Number(5), x = Number(6), y = Number(7), width = Number(8), height = Number(9);
            if ((long)x + width > raster.Width || (long)y + height > raster.Height || level == 5 && (width == 0 || height == 0))
                throw Error("WordGeometry", "Engine word geometry exceeds its original source raster.");
            if (!double.TryParse(fields[10], NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint | NumberStyles.AllowExponent, CultureInfo.InvariantCulture, out double confidence) || double.IsNaN(confidence) || double.IsInfinity(confidence) || confidence < -1 || confidence > 100 || confidence < 0 && confidence != -1)
                throw Error("Confidence", "The engine confidence must be absent(-1) or within0–100.");
            if (level != 5)
            {
                if (fields[11].Length != 0)
                    throw Error("TsvFields", "A nonword TSV record unexpectedly contains text.");
                continue;
            }

            if (fields[11].Length == 0 || fields[11].IndexOf('\t') >= 0)
                throw Error("TsvFields", "A TSV word requires nonempty text without extra fields.");
            if (words.Count == options.MaximumWords)
                throw Error("WordLimit", "The recognized word inventory exceeds its explicit ceiling.");
            characters += fields[11].Length;
            if (characters > options.MaximumCharacters)
                throw Error("CharacterLimit", "Recognized Unicode and word text exceed the explicit total character ceiling.");
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, fields[11].Length * 2L + 256);
            try
            {
                words.Add(new(fields[11], new(x, y, width, height), confidence < 0 ? null : confidence, block, paragraph, line, word));
            }
            catch (ArgumentException)
            {
                throw Error("NativeText", "The engine word contains invalid Unicode or geometry.");
            }

            int Number(int index) => int.TryParse(fields[index], NumberStyles.None, CultureInfo.InvariantCulture, out int value) && value >= 0 ? value : throw Error("TsvNumber", "The engine TSV requires bounded nonnegative integer fields.");
        }

        if (rows == 0)
            throw Error("TsvHeader", "The engine returned no TSV header.");
        return words;
        PdfException Error(string code, string message) => PdfWriterCore.Error(c, "Ocr." + code, message);
    }
}
