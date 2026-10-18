using System.IO;
using System.Text;
using System.Xml;

namespace Fluent.Pdf;
internal static class PdfFormXml
{
    internal const string Namespace = "urn:fluent-pdf:form-data:1";
    internal static List<PdfFormDataField> Read(PdfByteStore bytes, bool xfdf, PdfOperationContext context)
    {
        if (bytes.Length == 0)
            throw PdfFormData.Error(context, "Xml", "Field-data XML is required.");
        var fields = new List<PdfFormDataField>();
        string ns = xfdf ? PdfAnnotationSet.XfdfNamespace : Namespace;
        using var input = bytes.Open(0, bytes.Length);
        try
        {
            using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = false, MaxCharactersInDocument = bytes.Length, MaxCharactersFromEntities = 0, IgnoreWhitespace = false });
            reader.MoveToContent();
            Require(xfdf ? "xfdf" : "form-data");
            Attrs(xfdf ? Array.Empty<string>() : new[] { "version" });
            if (!xfdf && reader.GetAttribute("version") != "1")
                throw Profile();
            bool empty = reader.IsEmptyElement;
            reader.ReadStartElement();
            if (xfdf)
            {
                if (empty)
                    throw Profile();
                reader.MoveToContent();
                Require("fields");
                Attrs(Array.Empty<string>());
                bool noFields = reader.IsEmptyElement;
                reader.ReadStartElement();
                if (!noFields)
                {
                    Items("");
                    reader.ReadEndElement();
                }

                reader.MoveToContent();
                reader.ReadEndElement();
            }
            else if (!empty)
            {
                Items("");
                reader.ReadEndElement();
            }

            if (reader.MoveToContent() != XmlNodeType.None)
                throw Profile();
            return fields;
            void Items(string prefix)
            {
                using var depth = context.EnterScope(PdfResourceKind.Depth);
                while (reader.MoveToContent() == XmlNodeType.Element)
                {
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.Objects, 1);
                    context.Consume(PdfResourceKind.ExpandedBytes, 256);
                    Require("field");
                    Attrs(xfdf ? new[] { "name" } : new[] { "name", "name-encoding", "type" });
                    string partial = reader.GetAttribute("name") ?? throw Profile();
                    if (!xfdf)
                        partial = Decode(partial, reader.GetAttribute("name-encoding"));
                    context.Consume(PdfResourceKind.ExpandedBytes, (partial.Length + (long)prefix.Length + 1) * 4 + 64);
                    string name = prefix.Length == 0 ? partial : prefix + "." + partial;
                    string? type = xfdf ? null : reader.GetAttribute("type");
                    bool noChildren = reader.IsEmptyElement;
                    reader.ReadStartElement();
                    var values = new List<string>();
                    bool children = false;
                    if (!noChildren)
                    {
                        while (reader.MoveToContent() == XmlNodeType.Element)
                        {
                            context.Checkpoint();
                            if (xfdf && reader.LocalName == "field")
                            {
                                if (values.Count != 0)
                                    throw Profile();
                                children = true;
                                Items(name);
                                break;
                            }

                            if (children)
                                throw Profile();
                            Require("value");
                            Attrs(xfdf ? Array.Empty<string>() : new[] { "encoding" });
                            string? encoding = reader.GetAttribute("encoding");
                            context.Consume(PdfResourceKind.Objects, 1);
                            context.Consume(PdfResourceKind.ExpandedBytes, 64);
                            values.Add(Decode(reader.ReadElementContentAsString(), encoding));
                        }

                        if (reader.NodeType != XmlNodeType.EndElement || reader.LocalName != "field" || reader.NamespaceURI != ns)
                            throw Profile();
                        reader.ReadEndElement();
                    }

                    if (children)
                        continue;
                    if (fields.Count >= 100000)
                        throw Profile();
                    if (xfdf)
                        type = values.Count == 0 ? "null" : values.Count == 1 ? "text" : "texts";
                    if (type == null)
                        throw Profile();
                    fields.Add(new(name, PdfFormData.Typed(type, values, context)));
                }
            }

            void Require(string name)
            {
                if (reader.NodeType != XmlNodeType.Element || reader.LocalName != name || reader.NamespaceURI != ns)
                    throw Profile();
            }

            void Attrs(string[] allowed)
            {
                if (!reader.HasAttributes)
                    return;
                while (reader.MoveToNextAttribute())
                {
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.Objects, 1);
                    context.Consume(PdfResourceKind.ExpandedBytes, 64);
                    if (reader.NamespaceURI == "http://www.w3.org/2000/xmlns/" || reader.NamespaceURI == "http://www.w3.org/XML/1998/namespace" && reader.LocalName == "space")
                        continue;
                    if (reader.NamespaceURI.Length != 0 || !allowed.Contains(reader.LocalName, StringComparer.Ordinal))
                        throw Profile();
                }

                reader.MoveToElement();
            }

            string Decode(string value, string? encoding)
            {
                if (encoding == null)
                    return value;
                if (xfdf || encoding != "utf8-base64")
                    throw Profile();
                context.Consume(PdfResourceKind.ExpandedBytes, value.Length * 4L + 64);
                try
                {
                    return new UTF8Encoding(false, true).GetString(Convert.FromBase64String(value));
                }
                catch (Exception ex)when (ex is FormatException || ex is DecoderFallbackException)
                {
                    throw Profile();
                }
            }

            PdfException Profile() => PdfFormData.Error(context, "XmlProfile", "The XML input is outside the explicit field-value schema.");
        }
        catch (XmlException)
        {
            throw PdfFormData.Error(context, "Xml", "Malformed XML, DTDs or external entities are not permitted in field data.");
        }
    }

    internal static void Write(IReadOnlyList<PdfFormDataField> fields, Stream output, bool xfdf, PdfOperationContext context)
    {
        string ns = xfdf ? PdfAnnotationSet.XfdfNamespace : Namespace;
        // Preflight standard XML character/name limitations before output.
        if (xfdf)
            foreach (var field in fields)
            {
                Verify(field.Name);
                if (field.Value.Kind == PdfValueKind.Name && field.Value.NameValue.Any(c => c >= 128))
                    throw PdfFormData.Error(context, "XfdfNameProfile", "Non-ASCII button names require a qualified XFDF byte-name encoding profile.");
                foreach (var value in Texts(field.Value))
                    Verify(value);
            }

        context.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var tree = xfdf ? PdfFormDataTree.Create(fields, context) : null;
        using var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false, true), CloseOutput = false, Indent = false, CheckCharacters = true, NewLineHandling = NewLineHandling.Entitize });
        writer.WriteStartDocument();
        writer.WriteStartElement(xfdf ? "xfdf" : "form-data", ns);
        writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
        if (xfdf)
        {
            writer.WriteStartElement("fields", ns);
            foreach (var pair in tree!.Children)
                Node(pair.Key, pair.Value);
            writer.WriteEndElement();
        }
        else
        {
            writer.WriteAttributeString("version", "1");
            foreach (var field in fields)
            {
                context.Checkpoint();
                writer.WriteStartElement("field", ns);
                Name(field.Name);
                writer.WriteAttributeString("type", PdfFormData.Type(field.Value));
                Values(field.Value);
                writer.WriteEndElement();
            }
        }

        writer.WriteEndElement();
        writer.WriteEndDocument();
        writer.Flush();
        void Node(string name, PdfFormDataTree node)
        {
            context.Checkpoint();
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            writer.WriteStartElement("field", ns);
            Name(name);
            if (node.Value != null)
                Values(node.Value);
            foreach (var pair in node.Children)
                Node(pair.Key, pair.Value);
            writer.WriteEndElement();
        }

        void Name(string name)
        {
            bool encoded = !xfdf && !Valid(name);
            if (encoded)
                writer.WriteAttributeString("name-encoding", "utf8-base64");
            writer.WriteAttributeString("name", encoded ? Base64(name) : name);
        }

        void Values(PdfValue value)
        {
            foreach (string text in Texts(value))
            {
                context.Checkpoint();
                writer.WriteStartElement("value", ns);
                bool encoded = !xfdf && !Valid(text);
                if (encoded)
                    writer.WriteAttributeString("encoding", "utf8-base64");
                writer.WriteString(encoded ? Base64(text) : text);
                writer.WriteEndElement();
            }
        }

        IEnumerable<string> Texts(PdfValue value)
        {
            if (value.Kind == PdfValueKind.Null)
                yield break;
            if (value.Kind == PdfValueKind.Name)
                yield return value.NameValue;
            else if (value.Kind == PdfValueKind.Array)
                foreach (var item in value.Items)
                    yield return PdfTextStrings.Decode(item, context);
            else
                yield return PdfTextStrings.Decode(value, context);
        }

        bool Valid(string value)
        {
            try
            {
                XmlConvert.VerifyXmlChars(value);
                return true;
            }
            catch (XmlException)
            {
                return false;
            }
        }

        void Verify(string value)
        {
            context.Checkpoint();
            if (!Valid(value))
                throw PdfFormData.Error(context, "XmlCharacters", "The value cannot be represented by the standard XFDF XML character set.");
        }

        string Base64(string value)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, value.Length * 12L + 64);
            return Convert.ToBase64String(new UTF8Encoding(false, true).GetBytes(value));
        }
    }
}

internal sealed class PdfFormDataTree
{
    internal readonly SortedDictionary<string, PdfFormDataTree> Children = new(StringComparer.Ordinal);
    internal PdfValue? Value;
    internal static PdfFormDataTree Create(IReadOnlyList<PdfFormDataField> fields, PdfOperationContext context)
    {
        var root = new PdfFormDataTree();
        foreach (var field in fields)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, field.Name.Length * 8L + 64);
            var segments = field.Name.Split('.');
            if (segments.Length > context.Policy.Limits.MaximumDepth)
                context.RejectLimit(PdfResourceKind.Depth);
            var node = root;
            foreach (var segment in segments)
            {
                context.Checkpoint();
                if (node.Value != null)
                    throw PdfFormData.Error(context, "NamePrefix", "A data field cannot also be a hierarchical container.");
                if (!node.Children.TryGetValue(segment, out var child))
                {
                    context.Consume(PdfResourceKind.Objects, 1);
                    context.Consume(PdfResourceKind.ExpandedBytes, 256);
                    node.Children.Add(segment, child = new());
                }

                node = child;
            }

            if (node.Children.Count != 0)
                throw PdfFormData.Error(context, "NamePrefix", "A data field cannot also be a hierarchical container.");
            node.Value = field.Value;
        }

        return root;
    }
}
