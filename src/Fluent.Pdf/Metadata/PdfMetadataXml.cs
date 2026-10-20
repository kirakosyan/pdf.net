using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Fluent.Pdf;
// Original bounded XMP packet handling. No DTD, external resolver, schema download or action evaluation.
internal sealed class PdfMetadataXml
{
    internal const int MaximumPacketBytes = 16777216;
    internal static readonly XNamespace Rdf = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";
    internal static readonly XNamespace Dc = "http://purl.org/dc/elements/1.1/";
    internal static readonly XNamespace Xmp = "http://ns.adobe.com/xap/1.0/";
    internal static readonly XNamespace Pdf = "http://ns.adobe.com/pdf/1.3/";
    private readonly XDocument _document;
    private PdfMetadataXml(XDocument document)
    {
        _document = document;
    }

    internal static PdfMetadataXml Empty(PdfOperationContext context)
    {
        if (context.Policy.Limits.MaximumDepth < 3)
            context.RejectLimit(PdfResourceKind.Depth);
        context.Consume(PdfResourceKind.Objects, 3);
        context.Consume(PdfResourceKind.ExpandedBytes, 2048);
        return new(new XDocument(new XElement(XName.Get("xmpmeta", "adobe:ns:meta/"), new XElement(Rdf + "RDF", new XElement(Rdf + "Description", new XAttribute(Rdf + "about", ""))))));
    }

    internal static PdfMetadataXml? Read(PdfGraphEdit graph, PdfValue stream)
    {
        if (stream.Kind == PdfValueKind.Null)
            return null;
        if (stream.Kind != PdfValueKind.Stream || graph.Get(stream, "Type").Kind != PdfValueKind.Name || graph.Get(stream, "Type").NameValue != "Metadata" || graph.Get(stream, "Subtype").Kind != PdfValueKind.Name || graph.Get(stream, "Subtype").NameValue != "XML")
            throw graph.Error("Metadata.PacketDictionary", "A metadata packet must be a Metadata/XML stream.");
        using var decoded = PdfFilters.Decode(PdfFilterDeclarations.Resolve(stream, id => graph.Resolve(PdfValue.Reference(id)), graph.Context), graph.Context);
        using var buffer = new PdfCodecBufferStream(graph.Context, MaximumPacketBytes);
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var chunk = new byte[8192];
        int count;
        long total = 0;
        while ((count = decoded.Read(chunk, 0, chunk.Length)) > 0)
        {
            graph.Context.Checkpoint();
            if (count > MaximumPacketBytes - total)
                throw graph.Error("Metadata.PacketLimit", "The XMP packet exceeds its bounded XML envelope.");
            buffer.Write(chunk, 0, count);
            total += count;
        }

        var bytes = buffer.Finish();
        return Parse(() => bytes.Open(0, bytes.Length), bytes.Length, graph.Context);
    }

    internal static PdfMetadataXml Parse(string xml, PdfOperationContext context)
    {
        if (xml == null)
            throw new ArgumentNullException(nameof(xml));
        if (xml.Length > MaximumPacketBytes / 2)
            throw PdfWriterCore.Error(context, "Metadata.PacketLimit", "The XMP packet exceeds its bounded XML envelope.");
        context.Consume(PdfResourceKind.ExpandedBytes, xml.Length * 6L + 128);
        int byteCount;
        try
        {
            byteCount = new UTF8Encoding(false, true).GetByteCount(xml);
        }
        catch (EncoderFallbackException)
        {
            throw PdfWriterCore.Error(context, "Metadata.XmlEncoding", "The XMP packet contains malformed Unicode.");
        }

        if (byteCount > MaximumPacketBytes)
            throw PdfWriterCore.Error(context, "Metadata.PacketLimit", "The XMP packet exceeds its bounded XML envelope.");
        return Parse(() => Stream.Null, byteCount, context, xml);
    }

    private static PdfMetadataXml Parse(Func<Stream> open, long length, PdfOperationContext context, string? xml = null)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, length * 4 + 4096);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            CloseInput = true,
            MaxCharactersInDocument = MaximumPacketBytes,
            MaxCharactersFromEntities = 0,
            IgnoreWhitespace = false
        };
        XmlReader Reader() => xml == null ? XmlReader.Create(open(), settings) : XmlReader.Create(new StringReader(xml), settings);
        try
        {
            // The immutable packet is preflighted and charged before constructing any XML tree nodes.
            using (var reader = Reader())
                while (reader.Read())
                {
                    context.Checkpoint();
                    if (reader.Depth >= context.Policy.Limits.MaximumDepth)
                        context.RejectLimit(PdfResourceKind.Depth);
                    context.Consume(PdfResourceKind.Objects, 1 + reader.AttributeCount);
                    context.Consume(PdfResourceKind.ExpandedBytes, 256L * (1 + reader.AttributeCount) + reader.Value.Length * 2L);
                    if (reader.MoveToFirstAttribute())
                    {
                        do
                        {
                            context.Consume(PdfResourceKind.ExpandedBytes, reader.Value.Length * 2L + reader.Name.Length * 2L);
                        }
                        while (reader.MoveToNextAttribute());
                        reader.MoveToElement();
                    }
                }

            using var parsed = new CheckingReader(Reader(), context);
            var document = XDocument.Load(parsed, LoadOptions.PreserveWhitespace);
            var root = document.Root;
            if (root == null || !(root.Name == Rdf + "RDF" || root.Name == XName.Get("xmpmeta", "adobe:ns:meta/") || root.Name == XName.Get("xapmeta", "adobe:ns:meta/")) || root.Name != Rdf + "RDF" && root.Elements(Rdf + "RDF").Count() != 1)
                throw PdfWriterCore.Error(context, "Metadata.XmpStructure", "An XMP packet requires an RDF root or an xmpmeta wrapper containing RDF.");
            context.Checkpoint();
            return new(document);
        }
        catch (XmlException)
        {
            throw PdfWriterCore.Error(context, "Metadata.MalformedXml", "The XMP packet is malformed or contains a prohibited XML construct.");
        }
        catch (InvalidOperationException)
        {
            throw PdfWriterCore.Error(context, "Metadata.XmpStructure", "The XMP packet contains an invalid document structure.");
        }
    }

    internal string? Scalar(XName name, PdfOperationContext context)
    {
        var values = Properties(name, context);
        if (values.Count == 0)
            return null;
        if (values.Count != 1)
            throw PdfWriterCore.Error(context, "Metadata.AmbiguousProperty", "Equivalent XMP properties occur more than once.");
        var property = values[0];
        if (property is XAttribute attribute)
            return attribute.Value;
        var element = (XElement)property;
        if (element.HasElements || element.Attributes().Any(a => !a.IsNamespaceDeclaration && a.Name != XNamespace.Xml + "lang"))
            throw PdfWriterCore.Error(context, "Metadata.ComplexProperty", "A scalar synchronization cannot flatten a structured XMP property.");
        return element.Value;
    }

    internal string? Text(XName name, bool alternate, PdfOperationContext context)
    {
        var values = Properties(name, context);
        if (values.Count == 0)
            return null;
        if (values.Count != 1)
            throw PdfWriterCore.Error(context, "Metadata.AmbiguousProperty", "Equivalent XMP properties occur more than once.");
        if (values[0] is XAttribute attribute)
            return attribute.Value;
        var element = (XElement)values[0];
        if (!element.HasElements)
            return element.Value;
        if (element.Elements().Count() != 1)
            throw PdfWriterCore.Error(context, "Metadata.ComplexProperty", "The XMP text property has an unsupported container shape.");
        var container = element.Elements().First();
        if (container.Name != (alternate ? Rdf + "Alt" : Rdf + "Seq"))
            throw PdfWriterCore.Error(context, "Metadata.ComplexProperty", "The XMP text property has an unsupported container shape.");
        var items = new List<XElement>();
        foreach (var item in container.Elements())
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.ExpandedBytes, 32);
            items.Add(item);
        }

        if (items.Any(x => x.Name != Rdf + "li" || x.HasElements))
            throw PdfWriterCore.Error(context, "Metadata.ComplexProperty", "The XMP text collection contains structured items.");
        if (alternate)
        {
            var defaults = items.Where(x => (string? )x.Attribute(XNamespace.Xml + "lang") == "x-default");
            if (defaults.Count() != 1)
                throw PdfWriterCore.Error(context, "Metadata.LanguageAlternative", "An XMP language alternative requires exactly one x-default item for synchronization.");
            return defaults.First().Value;
        }

        if (items.Count > 1)
            throw PdfWriterCore.Error(context, "Metadata.MultipleAuthors", "Multiple XMP creators cannot be collapsed into a single Info Author without an explicit caller edit.");
        return items.Count == 0 ? "" : items[0].Value;
    }

    private List<XObject> Properties(XName name, PdfOperationContext context)
    {
        var result = new List<XObject>();
        foreach (var description in Descriptions(context))
        {
            context.Checkpoint();
            if (description.Attribute(name)is XAttribute attribute)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 32);
                result.Add(attribute);
            }

            foreach (var element in description.Elements(name))
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 32);
                result.Add(element);
            }
        }

        return result;
    }

    internal void Set(XName name, string? value, int kind, PdfOperationContext context)
    {
        if (value != null && context.Policy.Limits.MaximumDepth < (RdfRoot == _document.Root ? (kind == 0 ? 3 : 5) : (kind == 0 ? 4 : 6)))
            context.RejectLimit(PdfResourceKind.Depth);
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 4);
        context.Consume(PdfResourceKind.ExpandedBytes, (value?.Length ?? 0) * 8L + name.ToString().Length * 8L + 2048);
        if (value != null)
        {
            try
            {
                XmlConvert.VerifyXmlChars(value);
            }
            catch (XmlException)
            {
                throw PdfWriterCore.Error(context, "Metadata.XmlCharacters", "The requested XMP value contains characters prohibited by XML 1.0.");
            }

            PdfTextStrings.Encode(value, context);
        }

        var existing = Properties(name, context);
        XElement? prior = existing.Count == 1 ? existing[0] as XElement : null;
        if (kind == 1 && value != null && prior?.Element(Rdf + "Alt")is XElement alt)
        {
            var defaults = alt.Elements(Rdf + "li").Where(x => (string? )x.Attribute(XNamespace.Xml + "lang") == "x-default");
            int count = defaults.Count();
            if (count > 1)
                throw PdfWriterCore.Error(context, "Metadata.LanguageAlternative", "Duplicate default-language XMP items cannot be synchronized safely.");
            if (count == 1)
                defaults.First().Value = value;
            else
                alt.AddFirst(new XElement(Rdf + "li", new XAttribute(XNamespace.Xml + "lang", "x-default"), value));
            return;
        }

        foreach (var property in existing)
        {
            if (property is XAttribute attribute)
                attribute.Remove();
            else
                ((XElement)property).Remove();
        }

        if (value == null)
            return;
        var description = Descriptions(context).FirstOrDefault();
        if (description == null)
        {
            description = new XElement(Rdf + "Description", new XAttribute(Rdf + "about", ""));
            RdfRoot.Add(description);
        }

        description.Add(kind == 0 ? new XElement(name, value) : new XElement(name, new XElement(Rdf + (kind == 1 ? "Alt" : "Seq"), kind == 1 ? new XElement(Rdf + "li", new XAttribute(XNamespace.Xml + "lang", "x-default"), value) : new XElement(Rdf + "li", value))));
    }

    private XElement RdfRoot => _document.Root!.Name == Rdf + "RDF" ? _document.Root : _document.Root.Element(Rdf + "RDF")!;

    private IEnumerable<XElement> Descriptions(PdfOperationContext context)
    {
        string? subject = null;
        bool first = true;
        foreach (var description in RdfRoot.Elements(Rdf + "Description"))
        {
            context.Checkpoint();
            string current = (string? )description.Attribute(Rdf + "about") ?? "";
            if (description.Attribute(Rdf + "nodeID") != null || !first && subject != current)
                throw PdfWriterCore.Error(context, "Metadata.MultipleSubjects", "Typed metadata edits require one RDF subject; raw XML replacement preserves other packet structures.");
            first = false;
            subject = current;
            yield return description;
        }
    }

    internal PdfByteStore Bytes(PdfOperationContext context)
    {
        using var buffer = new PdfCodecBufferStream(context, MaximumPacketBytes);
        using var bounded = new PacketWriter(buffer, context);
        using (var writer = XmlWriter.Create(bounded, new XmlWriterSettings { Encoding = new UTF8Encoding(false, true), OmitXmlDeclaration = _document.Declaration == null, CloseOutput = false, Indent = false, NewLineHandling = NewLineHandling.None }))
            _document.Save(writer);
        context.Checkpoint();
        return buffer.Finish();
    }

    internal string Serialize(PdfOperationContext context)
    {
        var bytes = Bytes(context);
        context.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 3 + 64);
        var data = new byte[(int)bytes.Length];
        using var input = bytes.Open(0, bytes.Length);
        int at = 0, count;
        while ((count = input.Read(data, at, data.Length - at)) > 0)
        {
            context.Checkpoint();
            at += count;
        }

        return new UTF8Encoding(false, true).GetString(data);
    }

    private sealed class CheckingReader : XmlReader
    {
        private readonly XmlReader _reader;
        private readonly PdfOperationContext _context;
        internal CheckingReader(XmlReader reader, PdfOperationContext context)
        {
            _reader = reader;
            _context = context;
        }

        public override bool Read()
        {
            _context.Checkpoint();
            bool result = _reader.Read();
            _context.Checkpoint();
            return result;
        }

        public override XmlNodeType NodeType => _reader.NodeType;
        public override string Name => _reader.Name;
        public override string LocalName => _reader.LocalName;
        public override string NamespaceURI => _reader.NamespaceURI;
        public override string Prefix => _reader.Prefix;
        public override string Value => _reader.Value;
        public override int Depth => _reader.Depth;
        public override string BaseURI => _reader.BaseURI;
        public override bool IsEmptyElement => _reader.IsEmptyElement;
        public override int AttributeCount => _reader.AttributeCount;
        public override bool EOF => _reader.EOF;
        public override ReadState ReadState => _reader.ReadState;
        public override XmlNameTable NameTable => _reader.NameTable;

        public override string? GetAttribute(string name) => _reader.GetAttribute(name);
        public override string? GetAttribute(string name, string? namespaceURI) => _reader.GetAttribute(name, namespaceURI);
        public override string GetAttribute(int i) => _reader.GetAttribute(i);
        public override bool MoveToAttribute(string name) => _reader.MoveToAttribute(name);
        public override bool MoveToAttribute(string name, string? ns) => _reader.MoveToAttribute(name, ns);
        public override void MoveToAttribute(int i) => _reader.MoveToAttribute(i);
        public override bool MoveToFirstAttribute() => _reader.MoveToFirstAttribute();
        public override bool MoveToNextAttribute() => _reader.MoveToNextAttribute();
        public override bool MoveToElement() => _reader.MoveToElement();
        public override bool ReadAttributeValue() => _reader.ReadAttributeValue();
        public override void ResolveEntity() => _reader.ResolveEntity();
        public override string? LookupNamespace(string prefix) => _reader.LookupNamespace(prefix);
        public override void Close() => _reader.Close();
    }

    private sealed class PacketWriter : Stream
    {
        private readonly PdfCodecBufferStream _buffer;
        private readonly PdfOperationContext _context;
        internal PacketWriter(PdfCodecBufferStream buffer, PdfOperationContext context)
        {
            _buffer = buffer;
            _context = context;
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _buffer.BufferedLength;
        public override long Position { get => Length; set => throw new NotSupportedException(); }

        public override void Write(byte[] bytes, int offset, int count)
        {
            if (count > MaximumPacketBytes - _buffer.BufferedLength)
                throw PdfWriterCore.Error(_context, "Metadata.PacketLimit", "The serialized XMP packet exceeds its bounded XML envelope.");
            _buffer.Write(bytes, offset, count);
        }

        public override void Flush() => _buffer.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
    }
}
