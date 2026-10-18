using System.Globalization;
using System.Text;

namespace Fluent.Pdf;
internal sealed partial class PdfInputParser
{
    private void Markdown(string text)
    {
        var lines = Lines(text);
        for (int i = 0; i < lines.Length; i++)
        {
            Node();
            string line = lines[i];
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (line != "```")
                    Loss("CodeInfo", "The source profile omits fenced-code language metadata.");
                var code = new StringBuilder();
                bool ended = false, firstCodeLine = true;
                while (++i < lines.Length)
                {
                    Node();
                    if (lines[i] == "```")
                    {
                        ended = true;
                        break;
                    }

                    if (!firstCodeLine)
                        code.Append('\n');
                    firstCodeLine = false;
                    if (lines[i].Length > 1000000 - code.Length)
                        throw Error("TextLimit", "A literal code block exceeds one million UTF-16 units.");
                    code.Append(lines[i]);
                }

                if (!ended)
                    throw Error("MarkdownFence", "A fenced code block is unterminated.");
                _generation.Add(new PdfFlowParagraph(code.ToString(), Style("code").Source(null, "Code")));
                continue;
            }

            if (line[0] == '#')
            {
                int level = 0;
                while (level < line.Length && line[level] == '#')
                    level++;
                if (level > 6 || level == line.Length || line[level] != ' ')
                {
                    Loss("MarkdownHeading", "An unsupported heading construct was omitted.");
                    continue;
                }

                string title = line.Substring(level + 1);
                if (title.IndexOfAny(new[] { '*', '`', '[', '<', '_' }) >= 0)
                {
                    Loss("MarkdownHeadingInline", "Rich heading syntax is outside this explicit source profile.");
                    continue;
                }

                _generation.Add(new PdfFlowHeading("input-heading-" + (++_headings).ToString(CultureInfo.InvariantCulture), title, Style("heading"), level));
                continue;
            }

            if (line.StartsWith("|", StringComparison.Ordinal) && i + 1 < lines.Length && Separator(lines[i + 1], out int count))
            {
                var rows = new List<IReadOnlyList<string>>
                {
                    Cells(line)
                };
                if (rows[0].Count != count)
                    throw Error("MarkdownTable", "A pipe table header does not match its separator.");
                i++;
                while (i + 1 < lines.Length && lines[i + 1].StartsWith("|", StringComparison.Ordinal))
                {
                    Node();
                    rows.Add(Cells(lines[++i]));
                }

                _generation.Add(Table(rows, 1, true));
                continue;
            }

            if (ListMarker(line, out bool ordered, out int start, out string first))
            {
                var items = new List<string>
                {
                    first
                };
                while (i + 1 < lines.Length && ListMarker(lines[i + 1], out bool next, out int counter, out string body) && next == ordered)
                {
                    if (ordered && counter != start + items.Count)
                        throw Error("MarkdownCounter", "Ordered list counters must be consecutive in this profile.");
                    Node();
                    items.Add(body);
                    i++;
                }

                _generation.Add(List(items, ordered, start, true));
                continue;
            }

            if (UnsupportedBlock(line))
            {
                Loss("MarkdownBlock", "Unsupported indentation, HTML, image, quote, rule or extension syntax was omitted.");
                continue;
            }

            bool hardBreak = line.EndsWith("  ", StringComparison.Ordinal);
            var paragraph = new StringBuilder(hardBreak ? line.TrimEnd(' ') : line);
            while (i + 1 < lines.Length && !string.IsNullOrWhiteSpace(lines[i + 1]) && !StartsBlock(lines[i + 1]))
            {
                Node();
                paragraph.Append(hardBreak ? '\n' : ' ');
                string next = lines[++i];
                hardBreak = next.EndsWith("  ", StringComparison.Ordinal);
                paragraph.Append(hardBreak ? next.TrimEnd(' ') : next);
            }

            _generation.Add(Paragraph(paragraph.ToString(), true));
        }
    }

    private static bool ListMarker(string line, out bool ordered, out int start, out string body)
    {
        ordered = false;
        start = 1;
        body = "";
        if (line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("* ", StringComparison.Ordinal) || line.StartsWith("+ ", StringComparison.Ordinal))
        {
            body = line.Substring(2);
            return true;
        }

        int at = 0;
        while (at < line.Length && at < 9 && line[at] >= '0' && line[at] <= '9')
            at++;
        if (at == 0 || at + 1 >= line.Length || line[at] != '.' || line[at + 1] != ' ')
            return false;
        if (!int.TryParse(line.Substring(0, at), NumberStyles.None, CultureInfo.InvariantCulture, out start))
            return false;
        ordered = true;
        body = line.Substring(at + 2);
        return true;
    }

    private static bool UnsupportedBlock(string line) => line.StartsWith(" ", StringComparison.Ordinal) || line.StartsWith("\t", StringComparison.Ordinal) || line.StartsWith(">", StringComparison.Ordinal) || line.StartsWith("<", StringComparison.Ordinal) || line.StartsWith("![", StringComparison.Ordinal) || line.StartsWith("|", StringComparison.Ordinal) || line == "---" || line == "***" || line.StartsWith("~~~", StringComparison.Ordinal) || line.StartsWith("[", StringComparison.Ordinal) && line.IndexOf("]:", StringComparison.Ordinal) >= 0;
    private static bool StartsBlock(string line) => line.StartsWith("#", StringComparison.Ordinal) || line.StartsWith("```", StringComparison.Ordinal) || line.StartsWith("|", StringComparison.Ordinal) || UnsupportedBlock(line) || ListMarker(line, out _, out _, out _);
    private bool Separator(string line, out int count)
    {
        count = 0;
        if (!line.StartsWith("|", StringComparison.Ordinal))
            return false;
        var cells = Cells(line);
        foreach (var cell in cells)
        {
            if (cell.Length < 3 || cell.Any(c => c != '-'))
                return false;
            count++;
        }

        return count > 0;
    }

    private IReadOnlyList<string> Cells(string line)
    {
        Context.Checkpoint();
        if (line.Length < 2 || line[line.Length - 1] != '|')
            throw Error("MarkdownTable", "Pipe table rows require explicit leading and trailing delimiters.");
        var cells = new List<string>();
        var cell = new StringBuilder();
        for (int i = 1; i < line.Length; i++)
        {
            Context.Checkpoint();
            char ch = line[i];
            if (ch == '\\' && i + 1 < line.Length && line[i + 1] == '|')
            {
                cell.Append('|');
                i++;
            }
            else if (ch == '|')
            {
                Node();
                cells.Add(cell.ToString().Trim());
                cell.Clear();
            }
            else
                cell.Append(ch);
        }

        return cells;
    }

    private IReadOnlyList<PdfFlowRun> Inline(string text)
    {
        var runs = new List<PdfFlowRun>();
        var plain = new StringBuilder();
        void Add(string value, PdfFlowTextStyle style)
        {
            Node();
            if (runs.Count == 4096)
                throw Error("RunLimit", "A source paragraph exceeds 4096 styled runs.");
            runs.Add(new(value, style));
        }

        void Flush()
        {
            if (plain.Length > 0)
            {
                Add(plain.ToString(), Style("body"));
                plain.Clear();
            }
        }

        for (int i = 0; i < text.Length;)
        {
            Context.Checkpoint();
            char ch = text[i];
            if (ch == '\\')
            {
                if (i + 1 < text.Length && "\\`*_{}[]()#+-.!|".IndexOf(text[i + 1]) >= 0)
                {
                    plain.Append(text[i + 1]);
                    i += 2;
                    continue;
                }

                Loss("MarkdownEscape", "An unsupported escape marker was omitted.");
                i++;
                continue;
            }

            if (ch == '`' || ch == '*' || ch == '_')
            {
                string delimiter = ch == '*' && i + 1 < text.Length && text[i + 1] == '*' ? "**" : ch.ToString();
                int end = text.IndexOf(delimiter, i + delimiter.Length, StringComparison.Ordinal);
                if (end < 0)
                {
                    Loss("MarkdownDelimiter", "An unmatched inline delimiter was omitted.");
                    i += delimiter.Length;
                    continue;
                }

                var body = text.Substring(i + delimiter.Length, end - i - delimiter.Length);
                if (body.Length == 0 || ch != '`' && body.IndexOfAny(new[] { '*', '_', '`', '[', '<' }) >= 0)
                {
                    Loss("MarkdownNesting", "An unsupported nested inline construct was omitted.");
                    i = end + delimiter.Length;
                    continue;
                }

                Flush();
                string role = ch == '`' ? "Code" : delimiter == "**" ? "Strong" : "Em";
                string style = ch == '`' ? "code" : delimiter == "**" ? "strong" : "emphasis";
                Add(body, Style(style).Source(null, role));
                i = end + delimiter.Length;
                continue;
            }

            if (ch == '[')
            {
                int close = text.IndexOf("](", i + 1, StringComparison.Ordinal);
                int end = close < 0 ? -1 : text.IndexOf(')', close + 2);
                if (close < 0 || end < 0)
                {
                    Loss("MarkdownLink", "An unsupported link or reference construct was omitted.");
                    i++;
                    continue;
                }

                string label = text.Substring(i + 1, close - i - 1), uri = text.Substring(close + 2, end - close - 2);
                if (label.Length == 0 || label.IndexOfAny(new[] { '[', '*', '_', '`' }) >= 0)
                    throw Error("MarkdownLink", "A link requires an unambiguous nonempty literal label.");
                PdfInputLinks.ValidateUri(uri, Context);
                Flush();
                Add(label, Style("body").Source(uri, null));
                _links = true;
                i = end + 1;
                continue;
            }

            if (ch == '<' || ch == '!' && i + 1 < text.Length && text[i + 1] == '[' || ch == '~' || ch == '&')
            {
                Loss("MarkdownInline", "Unsupported raw HTML, images, entities or extension syntax was omitted.");
                i++;
                continue;
            }

            plain.Append(ch);
            i++;
        }

        Flush();
        if (runs.Count == 0)
            Add("", Style("body"));
        return runs;
    }
}
