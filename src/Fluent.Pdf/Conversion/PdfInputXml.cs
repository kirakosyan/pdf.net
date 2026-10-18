using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;

namespace Fluent.Pdf;
internal sealed partial class PdfInputParser
{
    private sealed class XmlNode
    {
        internal string Name = "";
        internal readonly Dictionary<string, object?> Attributes = new(StringComparer.Ordinal);
        internal readonly List<XmlNode> Children = new();
        internal readonly StringBuilder Text = new();
    }

    private void Xml(string text)
    {
        // No resolver, DTD, XInclude, stylesheet processing, source URI resolution or schema fetching.
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false,
            MaxCharactersInDocument = _options.MaximumInputBytes,
            MaxCharactersFromEntities = 1,
            CloseInput = false
        };
        try
        {
            using var input = new StringReader(text);
            using var reader = XmlReader.Create(input, settings);
            while (reader.Read() && reader.NodeType == XmlNodeType.Whitespace)
                Context.Checkpoint();
            if (reader.NodeType == XmlNodeType.XmlDeclaration)
                while (reader.Read() && reader.NodeType == XmlNodeType.Whitespace)
                    Context.Checkpoint();
            if (reader.NodeType != XmlNodeType.Element)
                throw Error("Xml", "A document element is required.");
            var root = ReadElement(reader);
            while (reader.Read())
                if (reader.NodeType != XmlNodeType.Whitespace)
                    throw Error("Xml", "Trailing XML nodes are outside the source schema.");
            if (root.Name != "document")
                throw Error("Schema", "The version-one document element is required.");
            Fields(root.Attributes, "version");
            Container(root);
            if (!root.Attributes.TryGetValue("version", out var version) || Text(version) != "1")
                throw Error("Version", "Version one is required.");
            bool haveData = false, haveBlocks = false;
            foreach (var child in root.Children)
            {
                if (child.Name == "data")
                {
                    if (haveData || haveBlocks)
                        throw Error("Schema", "One data element must precede blocks.");
                    haveData = true;
                    Fields(child.Attributes);
                    Container(child);
                    foreach (var entry in child.Children)
                    {
                        if (entry.Name != "value")
                        {
                            Loss("UnknownElement", "An unknown data element was omitted.");
                            continue;
                        }

                        Fields(entry.Attributes, "name", "type");
                        Leaf(entry);
                        string name = Text(Required(entry.Attributes, "name")), type = Text(Required(entry.Attributes, "type"));
                        object? scalar;
                        if (type == "text")
                            scalar = entry.Text.ToString();
                        else if (type == "null" && entry.Text.Length == 0)
                            scalar = null;
                        else if (type == "boolean" && (entry.Text.ToString() == "true" || entry.Text.ToString() == "false"))
                            scalar = entry.Text.ToString() == "true";
                        else if (type == "decimal")
                        {
                            var parsed = new PdfInputJson(entry.Text.ToString(), this).Read();
                            if (!(parsed is decimal number))
                                throw Error("BindingType", "A decimal value requires an exactly representable closed scalar.");
                            scalar = number;
                        }
                        else
                            throw Error("BindingType", "A data value has an invalid closed scalar type or lexical value.");
                        if (name.Length == 0 || name.Length > 128 || _data.ContainsKey(name))
                            throw Error("Binding", "Data names must be bounded and unique.");
                        _data.Add(name, scalar);
                    }
                }
                else if (child.Name == "blocks")
                {
                    if (haveBlocks)
                        throw Error("Schema", "Only one blocks element is allowed.");
                    haveBlocks = true;
                    Fields(child.Attributes);
                    Container(child);
                    foreach (var block in child.Children)
                    {
                        var converted = XmlBlock(block);
                        if (Text(converted["type"]) != "pageBreak-omitted")
                            Block(converted);
                    }
                }
                else
                    Loss("UnknownElement", "An unknown document element was omitted.");
            }

            if (!haveBlocks)
                throw Error("Schema", "A blocks element is required.");
        }
        catch (XmlException)
        {
            throw Error("Xml", "XML syntax, prohibited DTD/entity content or character limits were rejected.");
        }
    }

    private XmlNode ReadElement(XmlReader reader)
    {
        Node();
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        if (reader.NamespaceURI.Length != 0 || reader.Prefix.Length != 0)
            throw Error("XmlNamespace", "Only the documented unqualified source schema is accepted.");
        var node = new XmlNode
        {
            Name = reader.LocalName
        };
        if (reader.HasAttributes)
        {
            while (reader.MoveToNextAttribute())
            {
                Node();
                if (reader.NamespaceURI.Length != 0 || reader.Prefix.Length != 0)
                    throw Error("XmlNamespace", "Namespace and external schema attributes are prohibited.");
                node.Attributes.Add(reader.LocalName, reader.Value);
            }

            reader.MoveToElement();
        }

        if (reader.IsEmptyElement)
            return node;
        while (reader.Read())
        {
            Context.Checkpoint();
            if (reader.NodeType == XmlNodeType.EndElement)
                return node;
            if (reader.NodeType == XmlNodeType.Element)
                node.Children.Add(ReadElement(reader));
            else if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.CDATA || reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.SignificantWhitespace)
            {
                Context.Consume(PdfResourceKind.ExpandedBytes, reader.Value.Length * 4L + 32);
                node.Text.Append(reader.Value);
            }
            else
                throw Error("XmlNode", "Comments, processing instructions and entity nodes are outside this explicit schema.");
        }

        throw Error("Xml", "An XML element ended unexpectedly.");
    }

    private void Container(XmlNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.Text.ToString()))
            throw Error("Schema", "Container elements cannot contain mixed text.");
    }

    private void Leaf(XmlNode node)
    {
        if (node.Children.Count != 0)
            throw Error("Schema", "A literal source leaf cannot contain child elements.");
    }

    private Dictionary<string, object?> XmlBlock(XmlNode node)
    {
        Node();
        switch (node.Name)
        {
            case "paragraph":
            case "code":
                Fields(node.Attributes, "style");
                break;
            case "heading":
                Fields(node.Attributes, "style", "level");
                break;
            case "list":
                Fields(node.Attributes, "ordered", "start");
                break;
            case "table":
                Fields(node.Attributes, "headers");
                break;
            case "drawing":
                Fields(node.Attributes, "resource");
                break;
            case "pageBreak":
                Fields(node.Attributes);
                break;
            default:
                Loss("UnknownBlock", "An unknown XML block and its content were omitted.");
                return new()
                {
                    ["type"] = "pageBreak-omitted"
                };
        }

        var value = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["type"] = node.Name
        };
        foreach (var attribute in node.Attributes)
            if (attribute.Key == "style" || attribute.Key == "level" || attribute.Key == "ordered" || attribute.Key == "start" || attribute.Key == "headers" || attribute.Key == "resource")
                value.Add(attribute.Key, attribute.Value);
        if (node.Name == "paragraph" || node.Name == "code" || node.Name == "heading")
        {
            if (node.Children.Count == 0)
                value["text"] = node.Text.ToString();
            else
            {
                Container(node);
                var runs = new List<object?>();
                foreach (var child in node.Children)
                {
                    if (child.Name != "run")
                    {
                        Loss("UnknownElement", "An unknown inline element was omitted.");
                        continue;
                    }

                    Leaf(child);
                    Fields(child.Attributes, "style", "uri", "role");
                    var run = new Dictionary<string, object?>(StringComparer.Ordinal);
                    foreach (var attribute in child.Attributes)
                        if (attribute.Key == "style" || attribute.Key == "uri" || attribute.Key == "role")
                            run.Add(attribute.Key, attribute.Value);
                    run["text"] = child.Text.ToString();
                    runs.Add(run);
                }

                value["runs"] = runs;
            }
        }
        else if (node.Name == "list")
        {
            Container(node);
            var items = new List<object?>();
            foreach (var item in node.Children)
            {
                if (item.Name != "item")
                {
                    Loss("UnknownElement", "An unknown list element was omitted.");
                    continue;
                }

                Fields(item.Attributes);
                Leaf(item);
                items.Add(item.Text.ToString());
            }

            value["items"] = items;
        }
        else if (node.Name == "table")
        {
            Container(node);
            var rows = new List<object?>();
            foreach (var row in node.Children)
            {
                if (row.Name != "row")
                {
                    Loss("UnknownElement", "An unknown table element was omitted.");
                    continue;
                }

                Fields(row.Attributes);
                Container(row);
                var cells = new List<object?>();
                foreach (var cell in row.Children)
                {
                    if (cell.Name != "cell")
                    {
                        Loss("UnknownElement", "An unknown row element was omitted.");
                        continue;
                    }

                    Fields(cell.Attributes);
                    Leaf(cell);
                    cells.Add(cell.Text.ToString());
                }

                rows.Add(cells);
            }

            value["rows"] = rows;
        }
        else
        {
            Leaf(node);
            if (!string.IsNullOrWhiteSpace(node.Text.ToString()))
                throw Error("Schema", "This block cannot contain literal text.");
        }

        foreach (var key in new[]
        {
            "level",
            "start",
            "headers"
        }

        )
            if (value.TryGetValue(key, out var number))
            {
                if (!decimal.TryParse(Text(number), NumberStyles.None, CultureInfo.InvariantCulture, out var parsed))
                    throw Error("Schema", "An integer attribute is malformed.");
                value[key] = parsed;
            }

        if (value.TryGetValue("ordered", out var ordered))
        {
            if (Text(ordered) != "true" && Text(ordered) != "false")
                throw Error("Schema", "An ordered attribute must be true or false.");
            value["ordered"] = Text(ordered) == "true";
        }

        return value;
    }
}
