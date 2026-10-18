using System.Globalization;

namespace Fluent.Pdf;
internal sealed partial class PdfInputParser
{
    private void Json(string text)
    {
        var root = Object(new PdfInputJson(text, this).Read());
        Fields(root, "version", "data", "blocks");
        if (!root.TryGetValue("version", out var version) || !(version is decimal number) || number != 1)
            throw Error("Version", "Version one is required.");
        if (root.TryGetValue("data", out var data))
            foreach (var item in Object(data))
            {
                Node();
                if (item.Value is Dictionary<string, object?> || item.Value is List<object?>)
                    throw Error("BindingType", "Only closed scalar data values are supported.");
                _data.Add(Guard.Text(item.Key, "data", 128), item.Value);
            }

        foreach (var block in Array(Required(root, "blocks")))
            Block(Object(block));
    }

    private Dictionary<string, object?> Object(object? value) => value as Dictionary<string, object?> ?? throw Error("Schema", "An object was required by the source schema.");
    private List<object?> Array(object? value) => value as List<object?> ?? throw Error("Schema", "An array was required by the source schema.");
    private object? Required(Dictionary<string, object?> value, string name) => value.TryGetValue(name, out var result) ? result : throw Error("Schema", "A required source schema member is absent.");
    private string Text(object? value) => value as string ?? throw Error("Schema", "A string was required by the source schema.");
    private int Integer(Dictionary<string, object?> value, string key, int fallback, int maximum)
    {
        if (!value.TryGetValue(key, out var raw))
            return fallback;
        if (!(raw is decimal number) || number < 0 || number > maximum || decimal.Truncate(number) != number)
            throw Error("Schema", "A bounded nonnegative integer was required.");
        return (int)number;
    }

    private void Fields(Dictionary<string, object?> value, params string[] names)
    {
        foreach (var name in value.Keys)
        {
            Context.Checkpoint();
            if (!names.Contains(name, StringComparer.Ordinal))
                Loss("UnknownField", "An unknown source schema member was omitted.");
        }
    }

    private void Block(Dictionary<string, object?> value)
    {
        Node();
        string type = Text(Required(value, "type"));
        switch (type)
        {
            case "paragraph":
            case "code":
                Fields(value, "type", "text", "style", "runs");
                string style = value.TryGetValue("style", out var name) ? Text(name) : type == "code" ? "code" : "body";
                if (value.TryGetValue("runs", out var sourceRuns))
                {
                    if (type == "code" || value.ContainsKey("text"))
                        throw Error("Schema", "Runs and text are mutually exclusive; code uses literal text.");
                    var runs = new List<PdfFlowRun>();
                    foreach (var source in Array(sourceRuns))
                    {
                        Node();
                        var run = Object(source);
                        Fields(run, "text", "style", "uri", "role");
                        string runStyle = run.TryGetValue("style", out var rs) ? Text(rs) : style;
                        string? uri = run.TryGetValue("uri", out var u) ? Text(u) : null;
                        string? role = run.TryGetValue("role", out var r) ? Text(r) : null;
                        if (role != null && role != "Code" && role != "Strong" && role != "Em")
                            throw Error("Role", "Only Code, Strong and Em inline source roles are supported.");
                        if (uri != null)
                        {
                            PdfInputLinks.ValidateUri(uri, Context);
                            _links = true;
                        }

                        runs.Add(new(Bind(Text(Required(run, "text"))), Style(runStyle).Source(uri, role)));
                    }

                    if (runs.Count == 0)
                        throw Error("Schema", "An explicit run array must be nonempty.");
                    _generation.Add(new PdfFlowParagraph(runs));
                }
                else
                    _generation.Add(new PdfFlowParagraph(Bind(Text(Required(value, "text"))), type == "code" ? Style(style).Source(null, "Code") : Style(style)));
                break;
            case "heading":
                Fields(value, "type", "text", "style", "level");
                int level = Integer(value, "level", 1, 6);
                if (level < 1)
                    throw Error("Schema", "A heading level must be between one and six.");
                _generation.Add(new PdfFlowHeading("input-heading-" + (++_headings).ToString(CultureInfo.InvariantCulture), Bind(Text(Required(value, "text"))), Style(value.TryGetValue("style", out var headingStyle) ? Text(headingStyle) : "heading"), level));
                break;
            case "list":
                Fields(value, "type", "items", "ordered", "start");
                bool ordered = false;
                if (value.TryGetValue("ordered", out var order))
                    ordered = order is bool b ? b : throw Error("Schema", "The ordered list member must be boolean.");
                var items = new List<string>();
                foreach (var item in Array(Required(value, "items")))
                {
                    Node();
                    items.Add(Bind(Text(item)));
                }

                _generation.Add(List(items, ordered, Integer(value, "start", 1, 1000000), false));
                break;
            case "table":
                Fields(value, "type", "rows", "headers");
                var rows = new List<IReadOnlyList<string>>();
                foreach (var sourceRow in Array(Required(value, "rows")))
                {
                    Node();
                    var row = new List<string>();
                    foreach (var cell in Array(sourceRow))
                    {
                        Node();
                        row.Add(Bind(Text(cell)));
                    }

                    rows.Add(row);
                }

                int headers = Integer(value, "headers", 0, 1000000);
                if (headers > rows.Count)
                    throw Error("Schema", "The table header count exceeds its rows.");
                _generation.Add(Table(rows, headers, false));
                break;
            case "drawing":
                Fields(value, "type", "resource");
                AddDrawing(Text(Required(value, "resource")));
                break;
            case "pageBreak":
                Fields(value, "type");
                _generation.Add(new PdfFlowPageBreak());
                break;
            default:
                Loss("UnknownBlock", "An unknown source block type and its content were omitted.");
                break;
        }
    }
}
