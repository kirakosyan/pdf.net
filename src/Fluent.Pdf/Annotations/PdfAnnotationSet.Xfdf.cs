using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace Fluent.Pdf;
public sealed partial class PdfAnnotationSet
{
    internal const string XfdfNamespace = "http://ns.adobe.com/xfdf/";
    internal const string GraphNamespace = "urn:fluent-pdf:annotation-graph:1";
    /// <summary>Exports common XFDF annotation metadata with a mandatory owned graph extension retaining exact appearances, references and payloads. This explicit profile is not a general XFDF 3.0 converter.</summary>
    public PdfOperationReport WriteXfdf(Stream output, IEnumerable<int>? annotationIndices = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable caller-owned stream is required.", nameof(output));
        return PdfFont.Run("WriteAnnotationXfdf", "managed.annotations", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(Source, new(), context);
            if (Source.Pages.Count > 10000)
                throw graph.Error("Annotation.XfdfPageLimit", "The owned interchange profile supports at most ten thousand pages.");
            if (graph.Catalog.Entries.ContainsKey("Extensions"))
                throw graph.Error("Annotation.XfdfExtensions", "Developer-extension declarations require an explicit interchange manifest profile.");
            var chosen = Selection(annotationIndices ?? Enumerable.Range(0, Annotations.Count), context);
            var records = new Dictionary<PdfObjectId, PdfAnnotationRecord>();
            var all = new HashSet<PdfObjectId>();
            context.Consume(PdfResourceKind.ExpandedBytes, Annotations.Count * 192L + Source.Pages.Count * 128L + 8192);
            foreach (var record in Annotations)
            {
                if (record.ObjectId.HasValue)
                    all.Add(record.ObjectId.Value);
                if (!chosen.Contains(record.Index))
                    continue;
                if (!record.ObjectId.HasValue || record.Subtype == "Widget" || !(Enum.TryParse<PdfAnnotationKind>(record.Subtype, out var parsed) && parsed.ToString() == record.Subtype || record.Subtype == "Popup" || record.Subtype == "Link"))
                    throw graph.Error("Annotation.XfdfOwnership", "This interchange profile requires indirect non-widget annotation identities.");
                records.Add(record.ObjectId.Value, record);
            }

            var pages = new Dictionary<PdfObjectId, int>();
            for (int i = 0; i < Source.Pages.Count; i++)
                pages.Add(Source.Pages[i].Source!.ObjectId, i);
            var queue = new Queue<PdfObjectId>();
            var queued = new HashSet<PdfObjectId>();
            void Enqueue(PdfObjectId id)
            {
                if (all.Contains(id) && !records.ContainsKey(id))
                    throw graph.Error("Annotation.XfdfRelationships", "The interchange selection omits a referenced annotation relationship.");
                if (queued.Add(id))
                {
                    context.Consume(PdfResourceKind.Objects, 1);
                    context.Consume(PdfResourceKind.ExpandedBytes, 128);
                    queue.Enqueue(id);
                }
            }

            using var target = new BorrowedStream(output, false, context);
            using var finite = new PdfXfdfOutput(target, context);
            using var writer = XmlWriter.Create(finite, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false, Indent = false, CheckCharacters = true, NewLineHandling = NewLineHandling.Entitize });
            writer.WriteStartDocument();
            writer.WriteStartElement("xfdf", XfdfNamespace);
            writer.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
            writer.WriteAttributeString("xmlns", "fp", null, GraphNamespace);
            writer.WriteStartElement("annots", XfdfNamespace);
            foreach (var record in Annotations)
            {
                context.Checkpoint();
                if (!chosen.Contains(record.Index))
                    continue;
                writer.WriteStartElement(record.Subtype.ToLowerInvariant(), XfdfNamespace);
                writer.WriteAttributeString("page", record.PageIndex.ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("rect", RectText(record.Bounds));
                writer.WriteAttributeString("fp", "ref", GraphNamespace, IdText(record.ObjectId!.Value));
                foreach (var pair in XfdfTextKeys)
                    if (record.TextProperties.TryGetValue(pair.Value, out var text))
                    {
                        VerifyXmlText(text, graph);
                        if (pair.Key == "contents")
                            writer.WriteElementString("contents", XfdfNamespace, text);
                        else
                            writer.WriteAttributeString(pair.Key, text);
                    }

                writer.WriteEndElement();
                Enqueue(record.ObjectId.Value);
            }

            writer.WriteFullEndElement();
            writer.WriteStartElement("fp", "graph", GraphNamespace);
            writer.WriteAttributeString("version", "1");
            var declaredVersion = graph.Get(graph.Catalog, "Version");
            writer.WriteAttributeString("pdf-version", PdfVersionNumber.Text(Math.Max(PdfVersionNumber.Parse(graph.Graph.Version), declaredVersion.Kind == PdfValueKind.Name ? PdfVersionNumber.Parse(declaredVersion.NameValue) : 12)));
            writer.WriteAttributeString("page-count", Source.Pages.Count.ToString(CultureInfo.InvariantCulture));
            writer.WriteStartElement("fp", "pages", GraphNamespace);
            for (int i = 0; i < Source.Pages.Count; i++)
            {
                context.Checkpoint();
                var geometry = Source.Pages[i].Geometry;
                writer.WriteStartElement("fp", "page", GraphNamespace);
                writer.WriteAttributeString("index", i.ToString(CultureInfo.InvariantCulture));
                foreach (var box in new[]
                {
                    ("media", geometry.Boxes.Media),
                    ("crop", geometry.Boxes.Crop),
                    ("bleed", geometry.Boxes.Bleed),
                    ("trim", geometry.Boxes.Trim),
                    ("art", geometry.Boxes.Art)
                }

                )
                    writer.WriteAttributeString(box.Item1, RectText(box.Item2));
                writer.WriteAttributeString("rotate", ((int)geometry.Rotation).ToString(CultureInfo.InvariantCulture));
                writer.WriteAttributeString("unit", geometry.UserUnit.ToString("R", CultureInfo.InvariantCulture));
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.WriteStartElement("fp", "objects", GraphNamespace);
            while (queue.Count > 0)
            {
                context.Checkpoint();
                var id = queue.Dequeue();
                writer.WriteStartElement("fp", "object", GraphNamespace);
                writer.WriteAttributeString("id", IdText(id));
                Value(graph.Edit.Resolve(id));
                writer.WriteEndElement();
            }

            writer.WriteFullEndElement();
            writer.WriteEndElement();
            writer.WriteEndElement();
            writer.WriteEndDocument();
            writer.Flush();
            void Value(PdfValue value)
            {
                context.Checkpoint();
                using var depth = context.EnterScope(PdfResourceKind.Depth);
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 128);
                string kind = value.Kind.ToString().ToLowerInvariant();
                if (value.Kind == PdfValueKind.Reference)
                {
                    if (pages.TryGetValue(value.ReferenceId, out int page))
                    {
                        writer.WriteStartElement("fp", "page-ref", GraphNamespace);
                        writer.WriteAttributeString("index", page.ToString(CultureInfo.InvariantCulture));
                        writer.WriteEndElement();
                    }
                    else
                    {
                        Enqueue(value.ReferenceId);
                        writer.WriteStartElement("fp", "ref", GraphNamespace);
                        writer.WriteAttributeString("id", IdText(value.ReferenceId));
                        writer.WriteEndElement();
                    }

                    return;
                }

                writer.WriteStartElement("fp", kind, GraphNamespace);
                if (value.Kind == PdfValueKind.Boolean)
                    writer.WriteAttributeString("v", value.BooleanValue ? "true" : "false");
                else if (value.Kind == PdfValueKind.Number)
                    writer.WriteAttributeString("v", value.NumberText);
                else if (value.Kind == PdfValueKind.Name)
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, value.NameValue.Length * 4L + 32);
                    byte[] data = Encoding.UTF8.GetBytes(value.NameValue);
                    writer.WriteBase64(data, 0, data.Length);
                }
                else if (value.Kind == PdfValueKind.String)
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, value.StringBytes.Count + 32);
                    byte[] data = value.StringBytes.ToArray();
                    writer.WriteBase64(data, 0, data.Length);
                }
                else if (value.Kind == PdfValueKind.Array)
                    foreach (var child in value.Items)
                        Value(child);
                else if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
                {
                    string? type = PdfPageImportSource.Name(value, "Type");
                    if (type == "Catalog" || type == "Pages" || type == "Page" || value.Entries.ContainsKey("OC") || value.Entries.ContainsKey("StructParent") || value.Entries.ContainsKey("StructParents"))
                        throw graph.Error("Annotation.XfdfOwnership", "The interchange graph cannot import document/catalog, optional-content or structure ownership implicitly.");
                    if (PdfPageImportSource.Name(value, "S") == "GoTo" && value.Entries.TryGetValue("D", out var destination) && (graph.Resolve(destination).Kind == PdfValueKind.Name || graph.Resolve(destination).Kind == PdfValueKind.String))
                        throw graph.Error("Annotation.XfdfNavigationProfile", "Named local actions require an explicit destination import profile.");
                    foreach (var pair in value.Entries.OrderBy(p => p.Key, StringComparer.Ordinal))
                    {
                        context.Checkpoint();
                        context.Consume(PdfResourceKind.ExpandedBytes, pair.Key.Length * 8L + 64);
                        writer.WriteStartElement("fp", "entry", GraphNamespace);
                        writer.WriteAttributeString("key", Convert.ToBase64String(Encoding.UTF8.GetBytes(pair.Key)));
                        Value(pair.Value);
                        writer.WriteEndElement();
                    }

                    if (value.Kind == PdfValueKind.Stream)
                    {
                        writer.WriteStartElement("fp", "data", GraphNamespace);
                        context.Consume(PdfResourceKind.ExpandedBytes, 4096);
                        var buffer = new byte[4096];
                        using var raw = value.OpenRawStream();
                        using var lease = new BorrowedStream(raw, true, context);
                        int read;
                        while ((read = lease.Read(buffer, 0, buffer.Length)) > 0)
                        {
                            context.Checkpoint();
                            writer.WriteBase64(buffer, 0, read);
                        }

                        writer.WriteEndElement();
                    }
                }

                writer.WriteEndElement();
            }
        }, PdfCapability.WriteDocument);
    }

    internal static readonly KeyValuePair<string, string>[] XfdfTextKeys =
    {
        new("name", "NM"),
        new("title", "T"),
        new("subject", "Subj"),
        new("creationdate", "CreationDate"),
        new("date", "M"),
        new("state", "State"),
        new("state-model", "StateModel"),
        new("contents", "Contents")
    };
    internal static string IdText(PdfObjectId id) => id.Number.ToString(CultureInfo.InvariantCulture) + ":" + id.Generation.ToString(CultureInfo.InvariantCulture);
    internal static string RectText(PdfRect rect) => string.Join(",", new[] { rect.X, rect.Y, rect.Right, rect.Top }.Select(n => n.ToString("R", CultureInfo.InvariantCulture)));
    private static void VerifyXmlText(string text, PdfGraphEdit graph)
    {
        try
        {
            XmlConvert.VerifyXmlChars(text);
        }
        catch (XmlException)
        {
            throw graph.Error("Annotation.XfdfText", "The explicit XFDF metadata profile requires XML-compatible Unicode text.");
        }
    }
}
