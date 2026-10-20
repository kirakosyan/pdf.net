using System.Globalization;
using System.Text;

namespace Fluent.Pdf;
internal sealed partial class PdfInputParser
{
    private readonly PdfGenerationContext _generation;
    private readonly PdfInputConversionOptions _options;
    private int _nodes, _headings;
    private bool _links;
    private readonly Dictionary<string, object?> _data = new(StringComparer.Ordinal);
    internal PdfInputParser(PdfGenerationContext generation, PdfInputConversionOptions options)
    {
        _generation = generation;
        _options = options;
    }

    internal PdfOperationContext Context => _generation.OperationContext;

    internal void Node()
    {
        Context.Checkpoint();
        if (_nodes++ == _options.MaximumNodes)
            throw Error("NodeLimit", "The explicit syntax/model node ceiling was exceeded.");
        Context.Consume(PdfResourceKind.Objects, 1);
        Context.Consume(PdfResourceKind.ExpandedBytes, 256);
    }

    private PdfException Error(string code, string message) => PdfInputConversion.Error(Context, code, message);
    private void Loss(string code, string message) => Context.Report(new("Input." + code, message, lossCategory: PdfLossCategory.OmittedContent));
    private PdfFlowTextStyle Style(string name)
    {
        if (!_options.Styles.TryGetValue(name, out var value))
            throw Error("Style", "A source style requires an explicitly supplied named style.");
        return value;
    }

    internal void Parse(string text, PdfInputFormat format)
    {
        Context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 8L + 1024);
        switch (format)
        {
            case PdfInputFormat.Text:
                foreach (var block in Paragraphs(text))
                    _generation.Add(block);
                break;
            case PdfInputFormat.Markdown:
                Markdown(text);
                break;
            case PdfInputFormat.Json:
                Json(text);
                break;
            case PdfInputFormat.Xml:
                Xml(text);
                break;
        }

        if (_links)
            _generation.RegisterModelAction((document, c) => PdfInputLinks.Prepare(document, c, _generation.Providers));
    }

    private IEnumerable<PdfFlowBlock> Paragraphs(string text)
    {
        var paragraph = new StringBuilder();
        foreach (string line in Lines(text))
        {
            Context.Checkpoint();
            if (line.Length == 0)
            {
                if (paragraph.Length > 0)
                {
                    Node();
                    yield return Paragraph(paragraph.ToString());
                    paragraph.Clear();
                }
            }
            else
            {
                if (paragraph.Length > 0)
                    paragraph.Append('\n');
                paragraph.Append(line);
            }
        }

        if (paragraph.Length > 0)
        {
            Node();
            yield return Paragraph(paragraph.ToString());
        }
    }

    private string[] Lines(string text)
    {
        // Charged before the normalized string and line array are allocated.
        Context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 8L + 128);
        return text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
    }

    private PdfFlowParagraph Paragraph(string text, bool markdown = false, string style = "body")
    {
        if (text.Length > 1000000)
            throw Error("TextLimit", "A literal paragraph exceeds one million UTF-16 units.");
        return markdown ? new(Inline(text), new PdfFlowParagraphStyle()) : new(text, Style(style));
    }

    private PdfFlowList List(IReadOnlyList<string> items, bool ordered, int start, bool markdown)
    {
        var list = new List<PdfFlowListItem>();
        foreach (var item in items)
        {
            Node();
            list.Add(new(new[] { Paragraph(item, markdown) }));
        }

        return PdfFlowList.CreateCore(list, Style("body"), ordered ? PdfFlowListKind.Decimal : PdfFlowListKind.Bullet, start, "", ".", "-", null, 6, _options.MaximumNodes, Context);
    }

    private PdfTable Table(IReadOnlyList<IReadOnlyList<string>> values, int headers, bool markdown)
    {
        if (values.Count == 0 || values[0].Count == 0)
            throw Error("Table", "A source table requires at least one cell.");
        int columns = values[0].Count;
        if (columns > 256)
            throw Error("Table", "The explicit source table supports at most 256 columns.");
        var rows = new List<PdfTableRow>();
        foreach (var row in values)
        {
            Node();
            if (row.Count != columns)
                throw Error("Table", "Source table rows must have the declared rectangular column count.");
            var cells = new List<PdfTableCell>();
            foreach (var cell in row)
            {
                Node();
                cells.Add(new(new[] { Paragraph(cell, markdown) }));
            }

            rows.Add(new(cells));
        }

        return PdfTable.CreateCore(Enumerable.Range(0, columns).Select(_ => PdfTableColumn.Auto()).ToArray(), rows, headers, true, _options.MaximumNodes, Context);
    }

    private string Bind(string text)
    {
        if (text.IndexOf("{{", StringComparison.Ordinal) < 0)
            return text;
        var output = new StringBuilder();
        int at = 0;
        while (at < text.Length)
        {
            Context.Checkpoint();
            int start = text.IndexOf("{{", at, StringComparison.Ordinal);
            if (start < 0)
            {
                output.Append(text, at, text.Length - at);
                break;
            }

            output.Append(text, at, start - at);
            int end = text.IndexOf("}}", start + 2, StringComparison.Ordinal);
            if (end < 0 || end - start > 512)
                throw Error("Binding", "A scalar placeholder is malformed.");
            string key = text.Substring(start + 2, end - start - 2);
            int colon = key.IndexOf(':');
            string name = colon < 0 ? key : key.Substring(0, colon);
            if (!_data.TryGetValue(name, out var value))
                throw Error("Binding", "A scalar placeholder names an absent explicitly supplied value.");
            var replacement = _generation.Format(value, colon < 0 ? null : key.Substring(colon + 1));
            Context.Consume(PdfResourceKind.ExpandedBytes, replacement.Length * 4L + 32);
            if (replacement.Length > 1000000 - output.Length)
                throw Error("BindingLimit", "A bound paragraph exceeds one million UTF-16 units.");
            output.Append(replacement);
            at = end + 2;
        }

        if (output.Length > 1000000)
            throw Error("BindingLimit", "A bound paragraph exceeds one million UTF-16 units.");
        return output.ToString();
    }

    private void AddDrawing(string name)
    {
        if (!_options.Resources.TryGetValue(name, out var asset))
            throw Error("Resource", "Only an explicitly captured resource name may be used.");
        _generation.Add(new PdfFlowFloat(asset.Drawing, asset.SourceBounds, asset.Width, asset.Height, gap: 0, clear: PdfFlowClear.Both));
        _generation.Add(new PdfFlowParagraph("", Style("body"), new(clear: PdfFlowClear.Both)));
    }
}
