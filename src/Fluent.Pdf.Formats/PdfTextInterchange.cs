using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace Fluent.Pdf.Formats;

/// <summary>Exact-source, explicitly ordered text/Markdown projections with caller-assigned roles and declared reconstruction losses.</summary>
public static class PdfTextInterchange
{
    /// <summary>Stages complete UTF8 or a deterministic bundle before copying to a distinct borrowed output. The entire projection requires explicit permissive loss authority.</summary>
    public static PdfOperationReport Write(Stream input, Stream output, PdfTextInterchangeFormat format, IEnumerable<PdfTextInterchangeBlock> blocks, PdfTextInterchangeOptions options, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite || ReferenceEquals(input, output)) throw new ArgumentException("A distinct writable borrowed output is required.", nameof(output));
        return Run(input, output, null, format, blocks, options, providers, policy, cancellationToken);
    }
    /// <summary>Stages all content/assets before atomic file replacement. No asset path is resolved or written separately.</summary>
    public static PdfOperationReport WriteFile(Stream input, string path, PdfTextInterchangeFormat format, IEnumerable<PdfTextInterchangeBlock> blocks, PdfTextInterchangeOptions options, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        return Run(input, null, path, format, blocks, options, providers, policy, cancellationToken);
    }
    private static PdfOperationReport Run(Stream input, Stream? output, string? path, PdfTextInterchangeFormat format, IEnumerable<PdfTextInterchangeBlock> blocks, PdfTextInterchangeOptions options, PdfProviderRegistry? providers, PdfOperationPolicy? policy, CancellationToken token)
    {
        if (blocks == null || options == null) throw new ArgumentNullException(blocks == null ? nameof(blocks) : nameof(options));
        if (!Enum.IsDefined(typeof(PdfTextInterchangeFormat), format)) throw new ArgumentOutOfRangeException(nameof(format));
        providers ??= PdfProviderRegistry.Default;
        return PdfFont.Run("ExportTextInterchange", "managed.text-interchange", policy, token, c =>
        {
            var bytes = FormatIo.Capture(input, c, 67108864);
            if (PdfArchival.Digest(bytes, c) != options.SourceSha256) throw Error(c, "SourceMismatch");
            using var document = PdfPageOrganization.Read(bytes, 10000, c);
            using var graph = new PdfGraphEdit(document.CreateSnapshot(), new(), c);
            using var staged = new PdfCodecBufferStream(c, options.MaximumOutputBytes);
            var assets = new List<(string Name, byte[] Bytes, bool Stored)>();
            c.Consume(PdfResourceKind.ExpandedBytes, 8192);
            bool markdown = format != PdfTextInterchangeFormat.PlainText;
            using (var writer = new StreamWriter(staged, new UTF8Encoding(false, true), 1024, true))
            {
                writer.NewLine = "\n";
                writer.WriteLine(markdown ? "<!-- source-sha256=" + options.SourceSha256 + "; roles=caller-assigned; grouping=inferred -->" : "Source-SHA256: " + options.SourceSha256 + "\nRoles: caller-assigned; grouping: inferred");
                int index = 0;
                foreach (var block in blocks)
                {
                    c.Checkpoint(); c.Consume(PdfResourceKind.Objects, 1); c.Consume(PdfResourceKind.ExpandedBytes, 512);
                    if (block == null || index == 4095 || block.PageIndex >= document.Pages.Count) throw Error(c, "BlockSelection");
                    c.Report(new("TextInterchange.Reconstruction", "Caller-assigned text/Markdown projection does not retain original layout, complete graphics or verified source semantics.", lossCategory: PdfLossCategory.FidelityReduction, pageIndex: block.PageIndex));
                    writer.WriteLine();
                    string provenance = "block=" + N(index) + "; page-index=" + N(block.PageIndex) + "; order=" + block.Order + "; role=" + block.Role + (block.FirstCharacter.HasValue ? "; raw-range=" + N(block.FirstCharacter.Value) + ":" + N(block.CharacterCount!.Value) : "; selection=whole-page");
                    writer.WriteLine(markdown ? "<!-- " + provenance + " -->" : "[" + provenance + "]");
                    writer.WriteLine();
                    if (block.Role == PdfTextInterchangeRole.PageImage)
                    {
                        if (format != PdfTextInterchangeFormat.MarkdownBundle || options.Rendering == null) throw Error(c, "ImageBundleRequired");
                        string name = "page-" + N(index) + ".png";
                        var png = PdfRasterExport.Build(graph.Snapshot, new[] { block.PageIndex }, new(PdfRasterFormat.Png, options.Rendering), providers, c);
                        if (png.Length > 16777216) throw Error(c, "ImageExtent");
                        assets.Add((name, FormatIo.Raw(png, c), true));
                        writer.WriteLine("![Rendered PDF page " + N(block.PageIndex + 1) + "](" + name + ")");
                    }
                    else
                    {
                        var data = new PdfTextExtractionEngine(graph, options.Text, providers).Read(block.PageIndex);
                        if (block.Role == PdfTextInterchangeRole.Table) Table(writer, graph, data, block.Table!, providers, markdown, c);
                        else
                        {
                            string text;
                            if (block.FirstCharacter.HasValue)
                            {
                                int first = block.FirstCharacter.Value, count = block.CharacterCount!.Value;
                                if (first > data.Characters.Count || count > data.Characters.Count - first) throw Error(c, "CharacterRange");
                                c.Consume(PdfResourceKind.ExpandedBytes, data.Characters.Skip(first).Take(count).Sum(ch => ch.Text.Length * 4L) + 128);
                                text = string.Concat(data.Characters.Skip(first).Take(count).Select(ch => ch.Text));
                            }
                            else text = block.Order == PdfTextInterchangeOrder.Raw ? data.Raw : block.Order == PdfTextInterchangeOrder.Layout ? data.Layout : data.Text;
                            Verify(text, c);
                            if (markdown) Markdown(writer, text, block, c); else writer.WriteLine(text);
                        }
                    }
                    index++;
                }
                if (index == 0) throw Error(c, "BlockSelection");
                writer.Flush();
            }
            var result = staged.Finish();
            if (format == PdfTextInterchangeFormat.MarkdownBundle)
            {
                assets.Insert(0, ("document.md", FormatIo.Raw(result, c), true));
                result = FormatPackage.Write(assets, c);
            }
            if (path == null) FormatIo.Copy(result, output!, c); else PdfAtomicFile.Write(path, c, stream => FormatIo.Copy(result, stream, c));
        }, PdfCapability.ReadDocument);
    }
    private static void Markdown(TextWriter writer, string text, PdfTextInterchangeBlock block, PdfOperationContext c)
    {
        if (block.Role == PdfTextInterchangeRole.Code)
        {
            int run = 0, maximum = 2;
            foreach (char ch in text) { c.Checkpoint(); run = ch == '`' ? run + 1 : 0; maximum = Math.Max(maximum, run); }
            if (maximum > 1024) throw Error(c, "CodeFenceExtent");
            c.Consume(PdfResourceKind.ExpandedBytes, maximum * 2L + 32);
            string fence = new('`', maximum + 1);
            writer.WriteLine(fence); writer.Write(text); if (!text.EndsWith("\n", StringComparison.Ordinal)) writer.WriteLine(); writer.WriteLine(fence); return;
        }
        if ((block.Role == PdfTextInterchangeRole.Heading || block.Role == PdfTextInterchangeRole.Link) && text.IndexOfAny(new[] { '\n', '\r' }) >= 0) throw Error(c, "SingleLineRequired");
        string prefix = block.Role == PdfTextInterchangeRole.Heading ? new string('#', block.HeadingLevel) + " " : block.Role == PdfTextInterchangeRole.Bullet ? "- " : block.Role == PdfTextInterchangeRole.Numbered ? N(block.Number) + ". " : "";
        writer.Write(prefix);
        if (block.Role == PdfTextInterchangeRole.Link) writer.Write('[');
        Escaped(writer, text, c, false, block.Role == PdfTextInterchangeRole.Bullet || block.Role == PdfTextInterchangeRole.Numbered ? new string(' ', prefix.Length) : "");
        if (block.Role == PdfTextInterchangeRole.Link) { c.Consume(PdfResourceKind.ExpandedBytes, block.Link!.Length * 16L + 32); writer.Write("](<" + block.Link.Replace("&", "&amp;") + ">)"); }
        writer.WriteLine();
    }
    private static void Table(TextWriter writer, PdfGraphEdit graph, PdfTextPageData data, PdfTableExtractionOptions options, PdfProviderRegistry providers, bool markdown, PdfOperationContext c)
    {
        var table = PdfTableExtraction.Detect(graph, data, options, providers);
        if (table == null) throw Error(c, "TableUnavailable");
        if (table.Cells.Any(cell => cell.RowSpan != 1 || cell.ColumnSpan != 1)) throw Error(c, "TableSpansUnsupported");
        c.Consume(PdfResourceKind.ExpandedBytes, table.RowCount * (long)table.ColumnCount * 96 + 128);
        var cells = table.Cells.ToDictionary(cell => (cell.Row, cell.Column));
        for (int row = options.HeaderRows == 0 && markdown ? -1 : 0; row < table.RowCount; row++)
        {
            c.Checkpoint();
            for (int column = 0; column < table.ColumnCount; column++)
            {
                string text = row < 0 ? "Column " + N(column + 1) : cells.TryGetValue((row, column), out var cell) ? cell.Text : "";
                Verify(text, c);
                writer.Write(markdown ? "| " : column == 0 ? "" : "\t");
                if (markdown) Escaped(writer, text, c, true, ""); else { writer.Write('"'); writer.Write(text.Replace("\"", "\"\"")); writer.Write('"'); }
                if (markdown) writer.Write(' ');
            }
            writer.WriteLine(markdown ? "|" : "");
            if (markdown && row == (options.HeaderRows == 0 ? -1 : 0)) { for (int column = 0; column < table.ColumnCount; column++) writer.Write("| --- "); writer.WriteLine('|'); }
        }
    }
    private static void Verify(string value, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, value.Length * 4L + 128);
        try { XmlConvert.VerifyXmlChars(value); } catch (XmlException) { throw Error(c, "Unicode"); }
        if (value.IndexOf('\r') >= 0) throw Error(c, "LineEndingUnsupported");
    }
    private static void Escaped(TextWriter writer, string value, PdfOperationContext c, bool table, string continuation)
    {
        foreach (char ch in value)
        {
            c.Checkpoint();
            if (ch == '\n') { writer.Write(table ? "&#10;" : "\\\n" + continuation); continue; }
            if (ch == '\t' || ch == ' ' || ch >= '!' && ch <= '/' || ch >= ':' && ch <= '@' || ch >= '[' && ch <= '`' || ch >= '{' && ch <= '~')
            { c.Consume(PdfResourceKind.ExpandedBytes, 64); writer.Write("&#" + ((int)ch).ToString(CultureInfo.InvariantCulture) + ";"); }
            else writer.Write(ch);
        }
    }
    private static string N(int value) => value.ToString(CultureInfo.InvariantCulture);
    private static PdfException Error(PdfOperationContext c, string code) => PdfWriterCore.Error(c, "TextInterchange." + code, "The explicit source projection cannot be represented by this interchange profile.");
}
