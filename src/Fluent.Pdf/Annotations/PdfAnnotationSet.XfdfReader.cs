using System.Collections.ObjectModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace Fluent.Pdf;
public sealed partial class PdfAnnotationSet
{
    /// <summary>Reads the explicit owned-graph XFDF profile under a 16 MiB XML ceiling. DTDs, external entities, incomplete graphs and implicit appearance regeneration are rejected.</summary>
    public static PdfAnnotationSet ReadXfdf(Stream input, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable caller-owned stream is required.", nameof(input));
        PdfDocumentSnapshot? snapshot = null;
        PdfAnnotationRecord[]? records = null;
        var report = PdfFont.Run("ReadAnnotationXfdf", "managed.annotations", policy, cancellationToken, context =>
        {
            context.Consume(PdfResourceKind.ExpandedBytes, 8192);
            var buffer = new byte[4096];
            var builder = new PdfByteBuilder(context);
            const int ceiling = 16 * 1024 * 1024;
            long length = 0;
            using (var lease = new BorrowedStream(input, true, context))
                while (true)
                {
                    context.Checkpoint();
                    int count = lease.Read(buffer, 0, (int)Math.Min(buffer.Length, ceiling - length + 1));
                    if (count == 0)
                        break;
                    length += count;
                    if (length > ceiling)
                        throw Error("Annotation.XfdfLength", "The XFDF XML exceeds the explicit sixteen MiB profile ceiling.");
                    for (int i = 0; i < count; i++)
                        builder.Add(buffer[i]);
                }

            if (length == 0)
                throw Error("Annotation.XfdfXml", "An XFDF document is required.");
            context.Consume(PdfResourceKind.ExpandedBytes, length * 8L + 8192);
            var bytes = builder.Build();
            using var source = bytes.Open(0, bytes.Length);
            try
            {
                using var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = length, MaxCharactersFromEntities = 1024, IgnoreComments = true, IgnoreWhitespace = false, CloseInput = false });
                Start("xfdf", XfdfNamespace);
                bool emptyAnnots = reader.IsEmptyElement;
                Start("annots", XfdfNamespace);
                var roots = new List<(PdfObjectId Id, int Page, string Kind, PdfRect Rect, Dictionary<string, string> Text)>();
                while (!emptyAnnots && reader.MoveToContent() == XmlNodeType.Element)
                {
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.Objects, 1);
                    context.Consume(PdfResourceKind.ExpandedBytes, 1024);
                    if (reader.NamespaceURI != XfdfNamespace)
                        throw Error("Annotation.XfdfProfile", "Annotation metadata must use the declared XFDF namespace.");
                    string kind = reader.LocalName;
                    if (reader.HasAttributes)
                    {
                        while (reader.MoveToNextAttribute())
                        {
                            context.Checkpoint();
                            bool allowed = reader.NamespaceURI == "http://www.w3.org/2000/xmlns/" || reader.NamespaceURI == GraphNamespace && reader.LocalName == "ref" || reader.NamespaceURI.Length == 0 && (reader.LocalName == "page" || reader.LocalName == "rect" || XfdfTextKeys.Any(k => k.Key != "contents" && k.Key == reader.LocalName));
                            if (!allowed)
                                throw Error("Annotation.XfdfProfile", "Additional standard XML attributes require an explicit appearance/metadata import profile.");
                        }

                        reader.MoveToElement();
                    }

                    string root = Attr("ref", GraphNamespace);
                    int page = Integer(Attr("page"));
                    var rect = Rectangle(Attr("rect"));
                    var text = new Dictionary<string, string>(StringComparer.Ordinal);
                    foreach (var pair in XfdfTextKeys)
                        if (pair.Key != "contents" && reader.GetAttribute(pair.Key)is string value)
                            text.Add(pair.Value, value);
                    bool empty = reader.IsEmptyElement;
                    reader.ReadStartElement();
                    if (!empty)
                    {
                        reader.MoveToContent();
                        if (reader.IsStartElement("contents", XfdfNamespace))
                        {
                            text["Contents"] = reader.ReadElementContentAsString();
                            reader.MoveToContent();
                        }

                        if (reader.NodeType != XmlNodeType.EndElement)
                            throw Error("Annotation.XfdfProfile", "Additional annotation XML requires an explicit interchange profile.");
                        reader.ReadEndElement();
                    }

                    roots.Add((Identity(root), page, kind, rect, text));
                }

                if (!emptyAnnots)
                    reader.ReadEndElement();
                reader.MoveToContent();
                if (!reader.IsStartElement("graph", GraphNamespace))
                    throw Error("Annotation.XfdfAppearanceProviderRequired", "The owned-graph extension is required; appearances are never synthesized from external XFDF metadata implicitly.");
                if (Attr("version") != "1")
                    throw Error("Annotation.XfdfProfile", "The owned annotation graph version is unsupported.");
                string version = Attr("pdf-version");
                PdfVersionNumber.Parse(version);
                int countPages = Integer(Attr("page-count"));
                if (countPages < 1 || countPages > 10000)
                    throw Error("Annotation.XfdfPageLimit", "The annotation interchange page count must be one through ten thousand.");
                reader.ReadStartElement();
                Start("pages", GraphNamespace);
                using var edit = new PdfTransaction(policy: context.Policy);
                var catalogId = edit.Add(PdfValue.Null);
                var pagesId = edit.Add(PdfValue.Null);
                context.Consume(PdfResourceKind.ExpandedBytes, countPages * 128L + 128);
                var pages = new PdfObjectId[countPages];
                for (int i = 0; i < pages.Length; i++)
                {
                    context.Consume(PdfResourceKind.Objects, 1);
                    pages[i] = edit.Add(PdfValue.Null);
                }

                for (int i = 0; i < pages.Length; i++)
                {
                    context.Checkpoint();
                    using var depth = context.EnterScope(PdfResourceKind.Depth);
                    if (!reader.IsStartElement("page", GraphNamespace) || Integer(Attr("index")) != i)
                        throw Error("Annotation.XfdfPages", "Interchange pages must be contiguous and ordered.");
                    var media = Rectangle(Attr("media"));
                    var crop = Rectangle(Attr("crop"));
                    var bleed = Rectangle(Attr("bleed"));
                    var trim = Rectangle(Attr("trim"));
                    var art = Rectangle(Attr("art"));
                    int rotate = Integer(Attr("rotate"));
                    double unit = Number(Attr("unit"));
                    var geometry = new PdfPageGeometry(new(media, crop, bleed, trim, art), (PdfPageRotation)rotate, unit);
                    var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
                    {
                        ["Type"] = PdfValue.Name("Page"),
                        ["Parent"] = PdfValue.Reference(pagesId),
                        ["MediaBox"] = geometry.Boxes.Media.Value,
                        ["CropBox"] = geometry.Boxes.Crop.Value,
                        ["BleedBox"] = geometry.Boxes.Bleed.Value,
                        ["TrimBox"] = geometry.Boxes.Trim.Value,
                        ["ArtBox"] = geometry.Boxes.Art.Value,
                        ["Rotate"] = PdfValue.Integer(rotate),
                        ["UserUnit"] = PdfValues.Real(unit),
                        ["Resources"] = PdfGraphicsNumbers.Dict()
                    };
                    context.Consume(PdfResourceKind.ExpandedBytes, 2048);
                    edit.Set(pages[i], PdfValue.OwnedDictionary(entries));
                    Empty();
                }

                reader.MoveToContent();
                reader.ReadEndElement();
                bool emptyObjects = reader.IsEmptyElement;
                Start("objects", GraphNamespace);
                var map = new Dictionary<PdfObjectId, PdfObjectId>();
                var defined = new HashSet<PdfObjectId>();
                PdfObjectId Map(PdfObjectId id)
                {
                    if (!map.TryGetValue(id, out var result))
                    {
                        context.Consume(PdfResourceKind.Objects, 1);
                        context.Consume(PdfResourceKind.ExpandedBytes, 128);
                        result = edit.Add(PdfValue.Null);
                        map.Add(id, result);
                    }

                    return result;
                }

                foreach (var root in roots)
                {
                    if (root.Page < 0 || root.Page >= pages.Length)
                        throw Error("Annotation.XfdfPages", "An annotation references an absent interchange page.");
                    Map(root.Id);
                }

                while (!emptyObjects && reader.MoveToContent() == XmlNodeType.Element)
                {
                    if (!reader.IsStartElement("object", GraphNamespace))
                        throw Error("Annotation.XfdfGraph", "An object table entry is required.");
                    var id = Identity(Attr("id"));
                    if (!defined.Add(id))
                        throw Error("Annotation.XfdfDuplicateObject", "The interchange graph repeats an object identity.");
                    var mapped = Map(id);
                    reader.ReadStartElement();
                    reader.MoveToContent();
                    var value = Value();
                    reader.MoveToContent();
                    reader.ReadEndElement();
                    edit.Set(mapped, value);
                }

                if (!emptyObjects)
                    reader.ReadEndElement();
                reader.MoveToContent();
                reader.ReadEndElement();
                reader.MoveToContent();
                reader.ReadEndElement();
                if (reader.MoveToContent() != XmlNodeType.None)
                    throw Error("Annotation.XfdfTrailingXml", "Unexpected XML follows the interchange document.");
                if (map.Keys.Any(id => !defined.Contains(id)))
                    throw Error("Annotation.XfdfMissingObject", "The interchange graph contains an undefined object reference.");
                foreach (var group in roots.GroupBy(r => r.Page))
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, roots.Count * 32L + 256);
                    var page = edit.Resolve(pages[group.Key]).Entries.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
                    page["Annots"] = PdfValue.Array(group.Select(r => PdfValue.Reference(map[r.Id])).ToArray());
                    edit.Set(pages[group.Key], PdfValue.OwnedDictionary(page));
                }

                edit.Set(catalogId, PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Catalog")), ("Pages", PdfValue.Reference(pagesId))));
                edit.Set(pagesId, PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Pages")), ("Count", PdfValue.Integer(pages.Length)), ("Kids", PdfValue.Array(pages.Select(PdfValue.Reference).ToArray()))));
                edit.Trailer = PdfGraphicsNumbers.Dict(("Root", PdfValue.Reference(catalogId)));
                using var pdf = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
                using (var output = new BorrowedStream(pdf, false, context))
                    edit.WriteCore(output, new(minimumVersion: version), context);
                using var document = PdfPageOrganization.Read(pdf.Finish(), pages.Length, context);
                snapshot = document.CreateSnapshot();
                using var graph = new PdfGraphEdit(snapshot, new(), context);
                records = Read(graph);
                ValidateRelationships(records, graph);
                if (records.Length != roots.Count)
                    throw Error("Annotation.XfdfGraph", "The annotation root table disagrees with its object graph.");
                var expected = roots.GroupBy(r => r.Page).OrderBy(g => g.Key).SelectMany(g => g).ToArray();
                for (int i = 0; i < records.Length; i++)
                {
                    var record = records[i];
                    var root = expected[i];
                    if (record.Subtype.ToLowerInvariant() != root.Kind || RectText(record.Bounds) != RectText(root.Rect))
                        throw Error("Annotation.XfdfMetadataMismatch", "XFDF metadata disagrees with its explicit appearance/object graph.");
                    foreach (var pair in root.Text)
                        if (!record.TextProperties.TryGetValue(pair.Key, out var text) || text != pair.Value)
                            throw Error("Annotation.XfdfMetadataMismatch", "XFDF text disagrees with the explicit object graph; external edits require an appearance regeneration profile.");
                }

                PdfValue Value()
                {
                    context.Checkpoint();
                    using var depth = context.EnterScope(PdfResourceKind.Depth);
                    context.Consume(PdfResourceKind.Objects, 1);
                    context.Consume(PdfResourceKind.ExpandedBytes, 128);
                    if (reader.NamespaceURI != GraphNamespace || reader.NodeType != XmlNodeType.Element)
                        throw Error("Annotation.XfdfGraph", "A typed owned graph value is required.");
                    string kind = reader.LocalName;
                    if (kind == "ref")
                    {
                        var reference = PdfValue.Reference(Map(Identity(Attr("id"))));
                        Empty();
                        return reference;
                    }

                    if (kind == "page-ref")
                    {
                        int page = Integer(Attr("index"));
                        if (page < 0 || page >= pages.Length)
                            throw Error("Annotation.XfdfPages", "An object refers to an absent page.");
                        Empty();
                        return PdfValue.Reference(pages[page]);
                    }

                    if (kind == "null")
                    {
                        Empty();
                        return PdfValue.Null;
                    }

                    if (kind == "boolean")
                    {
                        string text = Attr("v");
                        if (text != "true" && text != "false")
                            throw Error("Annotation.XfdfGraph", "A boolean must be true or false.");
                        Empty();
                        return PdfValue.Boolean(text == "true");
                    }

                    if (kind == "number")
                    {
                        var result = PdfValue.Number(Attr("v"));
                        Empty();
                        return result;
                    }

                    if (kind == "name" || kind == "string")
                    {
                        var data = Base64();
                        context.Consume(PdfResourceKind.ExpandedBytes, data.Length * 8L + 64);
                        using var stream = data.Open(0, data.Length);
                        using var copy = new MemoryStream();
                        stream.CopyTo(copy);
                        var raw = copy.ToArray();
                        return kind == "name" ? PdfValue.Name(new UTF8Encoding(false, true).GetString(raw)) : PdfValue.String(raw);
                    }

                    if (kind == "array")
                    {
                        var items = new List<PdfValue>();
                        bool empty = reader.IsEmptyElement;
                        reader.ReadStartElement();
                        if (!empty)
                        {
                            while (reader.MoveToContent() == XmlNodeType.Element)
                            {
                                context.Consume(PdfResourceKind.ExpandedBytes, 16);
                                items.Add(Value());
                            }

                            reader.ReadEndElement();
                        }

                        return PdfValue.OwnedArray(items);
                    }

                    if (kind != "dictionary" && kind != "stream")
                        throw Error("Annotation.XfdfGraph", "The graph value kind is unsupported.");
                    var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
                    bool isEmpty = reader.IsEmptyElement;
                    reader.ReadStartElement();
                    PdfByteStore? encoded = null;
                    if (!isEmpty)
                    {
                        while (reader.MoveToContent() == XmlNodeType.Element && reader.IsStartElement("entry", GraphNamespace))
                        {
                            context.Checkpoint();
                            context.Consume(PdfResourceKind.ExpandedBytes, 128);
                            string key = new UTF8Encoding(false, true).GetString(Convert.FromBase64String(Attr("key")));
                            if (entries.ContainsKey(key))
                                throw Error("Annotation.XfdfGraph", "The graph dictionary repeats a key.");
                            reader.ReadStartElement();
                            reader.MoveToContent();
                            entries.Add(key, Value());
                            reader.MoveToContent();
                            reader.ReadEndElement();
                        }

                        if (reader.IsStartElement("data", GraphNamespace))
                            encoded = Base64();
                        reader.MoveToContent();
                        reader.ReadEndElement();
                    }

                    var dictionary = PdfValue.OwnedDictionary(entries);
                    string? ownerType = PdfPageImportSource.Name(dictionary, "Type");
                    if (ownerType == "Catalog" || ownerType == "Pages" || ownerType == "Page" || entries.ContainsKey("OC") || entries.ContainsKey("StructParent") || entries.ContainsKey("StructParents"))
                        throw Error("Annotation.XfdfOwnership", "Document, optional-content and structure ownership require an explicit interchange profile.");
                    if (kind == "dictionary")
                    {
                        if (encoded != null)
                            throw Error("Annotation.XfdfGraph", "Dictionary values cannot contain stream data.");
                        return dictionary;
                    }

                    if (encoded == null)
                        throw Error("Annotation.XfdfGraph", "A stream requires its exact encoded data element.");
                    return PdfValue.StreamValue(dictionary, encoded, 0, encoded.Length);
                }

                PdfByteStore Base64()
                {
                    var data = new PdfByteBuilder(context);
                    bool empty = reader.IsEmptyElement;
                    reader.ReadStartElement();
                    if (!empty)
                    {
                        int read;
                        while ((read = reader.ReadContentAsBase64(buffer, 0, buffer.Length)) > 0)
                        {
                            context.Checkpoint();
                            for (int i = 0; i < read; i++)
                                data.Add(buffer[i]);
                        }

                        reader.ReadEndElement();
                    }

                    return data.Build();
                }

                string Attr(string name, string? ns = null) => reader.GetAttribute(name, ns ?? "") ?? throw Error("Annotation.XfdfGraph", "A required interchange attribute is missing.");
                void Start(string name, string ns)
                {
                    reader.MoveToContent();
                    if (!reader.IsStartElement(name, ns))
                        throw Error("Annotation.XfdfProfile", "A required interchange element is missing.");
                    reader.ReadStartElement();
                    reader.MoveToContent();
                }

                void Empty()
                {
                    bool empty = reader.IsEmptyElement;
                    reader.ReadStartElement();
                    if (!empty)
                    {
                        reader.MoveToContent();
                        if (reader.NodeType != XmlNodeType.EndElement)
                            throw Error("Annotation.XfdfGraph", "A scalar graph value cannot contain child content.");
                        reader.ReadEndElement();
                    }

                    reader.MoveToContent();
                }
            }
            catch (XmlException)
            {
                context.Checkpoint();
                throw Error("Annotation.XfdfXml", "Interchange XML must be bounded and cannot contain document types or external entities.");
            }
            catch (ArgumentException)
            {
                context.Checkpoint();
                throw Error("Annotation.XfdfGraph", "The interchange graph contains invalid encoded text, numbers or geometry.");
            }
            catch (FormatException)
            {
                context.Checkpoint();
                throw Error("Annotation.XfdfGraph", "The interchange graph contains malformed scalar data.");
            }

            PdfException Error(string code, string message) => PdfWriterCore.Error(context, code, message);
            int Integer(string text) => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out int value) ? value : throw Error("Annotation.XfdfGraph", "A nonnegative integer is required.");
            double Number(string text) => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out double value) && !double.IsInfinity(value) && !double.IsNaN(value) ? value : throw Error("Annotation.XfdfGraph", "A finite coordinate is required.");
            PdfObjectId Identity(string text)
            {
                var parts = text.Split(':');
                if (parts.Length != 2)
                    throw Error("Annotation.XfdfGraph", "A numbered object identity is required.");
                return new(Integer(parts[0]), Integer(parts[1]));
            }

            PdfRect Rectangle(string text)
            {
                var parts = text.Split(',');
                if (parts.Length != 4)
                    throw Error("Annotation.XfdfGraph", "A rectangle requires four native bounds.");
                double x = Number(parts[0]), y = Number(parts[1]), right = Number(parts[2]), top = Number(parts[3]);
                return new(x, y, right - x, top - y);
            }
        }, PdfCapability.ReadDocument);
        return new(snapshot!, records!, report);
    }
}
