using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace Fluent.Pdf.Formats;

/// <summary>Versioned typed XML interchange for the active PDF object graph, including opaque stream bytes. It does not execute actions or preserve revision history, encryption or signatures.</summary>
public static class PdfObjectXml
{
    private static readonly XNamespace Ns = "urn:fluent-pdf:objects:1";

    /// <summary>Exports an unencrypted, unsigned PDF as a bounded typed object graph. Byte-valued names and strings use base64; indirect identities and exact decimal tokens are retained.</summary>
    public static PdfOperationReport Export(Stream pdf, Stream xml, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
        => PdfDocumentCollection.Run("ExportObjectXml", policy, cancellationToken, c => ExportCore(pdf, xml, c));

    /// <summary>Imports the strict version-one object vocabulary and writes a full PDF revision after validation. Unknown XML, encrypted trailers and signature byte ranges reject.</summary>
    public static PdfOperationReport Import(Stream xml, Stream pdf, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
        => PdfDocumentCollection.Run("ImportObjectXml", policy, cancellationToken, c => ImportCore(xml, pdf, c));

    internal static void ExportCore(Stream pdf, Stream xml, PdfOperationContext c)
    {
        using var borrowed = new BorrowedStream(pdf, true, c);
        var graph = PdfSyntax.ReadCore(borrowed, c);
        if (graph.HasSignatureByteRanges || graph.Revisions.Any(r => r.Trailer.Entries.ContainsKey("Encrypt")))
            throw Error(c, "Protection", "Encrypted or signed documents are outside the object XML rewrite profile.");
        Charge(c);
        var root = new XElement(Ns + "pdf", new XAttribute("version", "1"), new XAttribute("pdfVersion", graph.Version));
        var trailer = graph.Revisions[0].Trailer;
        var trailerEntries = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        foreach (var pair in trailer.Entries)
        {
            Charge(c);
            if (pair.Key == "Size" || pair.Key == "Prev" || pair.Key == "XRefStm" || trailer.Kind == PdfValueKind.Stream && new[] { "Type", "W", "Index", "Length", "Filter", "DecodeParms" }.Contains(pair.Key)) continue;
            trailerEntries.Add(pair.Key, pair.Value);
        }
        root.Add(new XElement(Ns + "trailer", Encode(PdfValue.OwnedDictionary(trailerEntries), c)));
        var seen = new HashSet<int>();
        foreach (var revision in graph.Revisions)
            foreach (var entry in revision.Entries)
            {
                Charge(c);
                if (!seen.Add(entry.Id.Number) || entry.Id.Number == 0 || entry.Kind == PdfCrossReferenceKind.Free) continue;
                if (entry.Id.Number > 1000000) throw Error(c, "Identity", "This interchange profile bounds object numbers to one million.");
                var value = graph.Resolve(entry.Id);
                if (value.Kind == PdfValueKind.Stream && value.Entries.TryGetValue("Type", out var type) && type.Kind == PdfValueKind.Name && (type.NameValue == "XRef" || type.NameValue == "ObjStm")) continue;
                root.Add(new XElement(Ns + "object", new XAttribute("number", entry.Id.Number), new XAttribute("generation", entry.Id.Generation), Encode(value, c)));
            }
        Report(c);
        FormatIo.Copy(FormatIo.XmlBytes(root, c), xml, c);
    }

    internal static void ImportCore(Stream xml, Stream pdf, PdfOperationContext c)
    {
        var document = FormatIo.Xml(xml, c, 16777216);
        if (document.Nodes().Any(n => !(n is XElement) && !(n is XText t && string.IsNullOrWhiteSpace(t.Value)))) throw Error(c, "Schema", "Only the root element is permitted.");
        var root = document.Root ?? throw Error(c, "Schema", "An object XML root is required.");
        Shape(root, "pdf", c, "version", "pdfVersion");
        if (root.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value))) throw Error(c, "Schema", "Root text is not permitted.");
        if ((string?)root.Attribute("version") != "1") throw Error(c, "Version", "Only version-one object XML is supported.");
        string version = (string?)root.Attribute("pdfVersion") ?? "";
        if (!new[] { "1.2", "1.3", "1.4", "1.5", "1.6", "1.7", "2.0" }.Contains(version)) throw Error(c, "Version", "A supported PDF version is required.");
        var elements = root.Elements().GetEnumerator();
        using (elements)
        {
            if (!elements.MoveNext()) throw Error(c, "Schema", "A trailer and object inventory are required.");
            var trailerElement = elements.Current;
            Shape(trailerElement, "trailer", c);
            var trailer = Decode(Only(trailerElement, c), c);
            if (trailer.Kind != PdfValueKind.Dictionary || trailer.Entries.ContainsKey("Encrypt")) throw Error(c, "Protection", "An unencrypted trailer dictionary is required.");
            var objects = new Dictionary<PdfObjectId, PdfValue>();
            var numbers = new HashSet<int>();
            int maximum = 0;
            while (elements.MoveNext())
            {
                Charge(c);
                var element = elements.Current;
                Shape(element, "object", c, "number", "generation");
                var id = Identity(element, c);
                if (id.Number == 0 || !numbers.Add(id.Number)) throw Error(c, "Identity", "Distinct active nonzero object numbers are required.");
                maximum = Math.Max(maximum, id.Number);
                var value = Decode(Only(element, c), c);
                if (value.Kind == PdfValueKind.Stream && value.Entries.TryGetValue("Type", out var type) && type.Kind == PdfValueKind.Name && (type.NameValue == "XRef" || type.NameValue == "ObjStm"))
                    throw Error(c, "Schema", "Object and cross-reference containers must be expanded into the active inventory.");
                objects.Add(id, value);
            }
            using var staged = new PdfCodecBufferStream(c, 67108864);
            PdfWriterCore.Write(null, trailer, objects, new HashSet<PdfObjectId>(), maximum, true, staged, new(version: version, crossReferenceFormat: PdfCrossReferenceFormat.Table), c);
            Report(c);
            FormatIo.Copy(staged.Finish(), pdf, c);
        }
    }

    private static XElement Encode(PdfValue value, PdfOperationContext c)
    {
        Charge(c);
        using var depth = c.EnterScope(PdfResourceKind.Depth);
        switch (value.Kind)
        {
            case PdfValueKind.Null: return new(Ns + "null");
            case PdfValueKind.Boolean: return new(Ns + "boolean", value.BooleanValue ? "true" : "false");
            case PdfValueKind.Number: c.Consume(PdfResourceKind.ExpandedBytes, value.NumberText.Length * 4L); return new(Ns + "number", value.NumberText);
            case PdfValueKind.Name: return new(Ns + "name", Name64(value.NameValue, c));
            case PdfValueKind.String:
                c.Consume(PdfResourceKind.ExpandedBytes, value.StringBytes.Count * 5L + 64);
                return new(Ns + "string", Convert.ToBase64String(value.StringBytes.ToArray()));
            case PdfValueKind.Reference:
                if (value.ReferenceId.Number > 1000000) throw Error(c, "Identity", "This interchange profile bounds reference numbers to one million.");
                return new(Ns + "reference", new XAttribute("number", value.ReferenceId.Number), new XAttribute("generation", value.ReferenceId.Generation));
            case PdfValueKind.Array:
                var array = new XElement(Ns + "array");
                foreach (var item in value.Items) array.Add(Encode(item, c));
                return array;
            case PdfValueKind.Dictionary:
            case PdfValueKind.Stream:
                if (value.Entries.ContainsKey("ByteRange") && value.Entries.ContainsKey("Contents")) throw Error(c, "Protection", "Signature byte ranges cannot survive object XML rewriting.");
                var dictionary = new XElement(Ns + (value.Kind == PdfValueKind.Stream ? "stream" : "dictionary"));
                c.Consume(PdfResourceKind.ExpandedBytes, value.Entries.Count * 32L);
                foreach (var pair in value.Entries.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    Charge(c);
                    if (value.Kind == PdfValueKind.Stream && pair.Key == "Length") continue;
                    dictionary.Add(new XElement(Ns + "entry", new XAttribute("key", Name64(pair.Key, c)), Encode(pair.Value, c)));
                }
                if (value.Kind == PdfValueKind.Stream)
                {
                    using var raw = value.OpenRawStream();
                    var bytes = FormatIo.Capture(raw, c);
                    c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 3L + 64);
                    dictionary.Add(new XElement(Ns + "data", Convert.ToBase64String(FormatIo.Raw(bytes, c))));
                }
                return dictionary;
            default: throw Error(c, "Schema", "Unknown PDF value kind.");
        }
    }

    private static PdfValue Decode(XElement element, PdfOperationContext c)
    {
        Charge(c);
        using var depth = c.EnterScope(PdfResourceKind.Depth);
        string kind = element.Name.LocalName;
        Shape(element, kind, c, kind == "reference" ? new[] { "number", "generation" } : Array.Empty<string>());
        bool scalar = kind == "boolean" || kind == "number" || kind == "name" || kind == "string";
        if (scalar && element.Elements().Any()) throw Error(c, "Schema", "Scalar values cannot contain elements.");
        if (!scalar && element.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value))) throw Error(c, "Schema", "Container text is not permitted.");
        switch (kind)
        {
            case "null": if (element.Nodes().Any()) throw Error(c, "Schema", "Null must be empty."); return PdfValue.Null;
            case "boolean": if (element.Value != "true" && element.Value != "false") throw Error(c, "Schema", "A canonical boolean is required."); return PdfValue.Boolean(element.Value == "true");
            case "number":
                try { return PdfValue.Number(element.Value); }
                catch (ArgumentException) { throw Error(c, "Schema", "An exact PDF decimal token is required."); }
            case "name": return PdfValue.Name(Unname(element.Value, c));
            case "string": return PdfValue.OwnedString(Unbase64(element.Value, c));
            case "reference": if (element.Nodes().Any()) throw Error(c, "Schema", "References must be empty."); return PdfValue.Reference(Identity(element, c));
            case "array":
                var values = new List<PdfValue>();
                foreach (var child in element.Elements()) values.Add(Decode(child, c));
                return PdfValue.OwnedArray(values);
            case "dictionary":
            case "stream":
                var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
                PdfByteStore? bytes = null;
                foreach (var child in element.Elements())
                {
                    Charge(c);
                    if (kind == "stream" && child.Name == Ns + "data")
                    {
                        Shape(child, "data", c);
                        if (bytes != null || child.Elements().Any() || child.ElementsAfterSelf().Any()) throw Error(c, "Schema", "Exactly one final stream data element is required.");
                        byte[] raw = Unbase64(child.Value, c);
                        using var input = new MemoryStream(raw, false);
                        bytes = FormatIo.Capture(input, c);
                    }
                    else
                    {
                        Shape(child, "entry", c, "key");
                        string key = Unname((string?)child.Attribute("key") ?? "", c);
                        if (entries.ContainsKey(key) || kind == "stream" && key == "Length") throw Error(c, "Schema", "Duplicate keys and caller stream lengths reject.");
                        entries.Add(key, Decode(Only(child, c), c));
                    }
                }
                if (entries.ContainsKey("ByteRange") && entries.ContainsKey("Contents")) throw Error(c, "Protection", "Signature byte ranges cannot survive object XML rewriting.");
                var result = PdfValue.OwnedDictionary(entries);
                if (kind == "dictionary") return result;
                if (bytes == null) throw Error(c, "Schema", "Stream data is required.");
                return PdfValue.StreamValue(result, bytes, 0, bytes.Length);
            default: throw Error(c, "Schema", "Unknown XML value vocabulary.");
        }
    }

    private static void Shape(XElement element, string name, PdfOperationContext c, params string[] attributes)
    {
        c.Checkpoint();
        if (element.Name != Ns + name || element.Attributes().Any(a => !a.IsNamespaceDeclaration && (a.Name.NamespaceName != "" || !attributes.Contains(a.Name.LocalName))) || attributes.Any(a => element.Attribute(a) == null) || element.Nodes().Any(n => !(n is XElement) && !(n is XText)))
            throw Error(c, "Schema", "Unknown or incomplete XML structure rejects.");
    }
    private static XElement Only(XElement parent, PdfOperationContext c)
    {
        var child = parent.Elements().FirstOrDefault();
        if (child == null || child.ElementsAfterSelf().Any() || parent.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value))) throw Error(c, "Schema", "Exactly one typed value is required.");
        return child;
    }
    private static PdfObjectId Identity(XElement element, PdfOperationContext c)
    {
        if (!int.TryParse((string?)element.Attribute("number"), NumberStyles.None, CultureInfo.InvariantCulture, out int number) || number < 0 || number > 1000000 || !int.TryParse((string?)element.Attribute("generation"), NumberStyles.None, CultureInfo.InvariantCulture, out int generation) || generation < 0 || generation > 65535)
            throw Error(c, "Identity", "Bounded object and generation numbers are required.");
        return new(number, generation);
    }
    private static string Name64(string value, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, value.Length * 6L + 64);
        var bytes = new byte[value.Length];
        for (int i = 0; i < bytes.Length; i++) { if ((i & 8191) == 0) c.Checkpoint(); bytes[i] = (byte)value[i]; }
        return Convert.ToBase64String(bytes);
    }
    private static string Unname(string text, PdfOperationContext c)
    {
        byte[] bytes = Unbase64(text, c);
        c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 4L + 64);
        if (bytes.Any(b => b == 0)) throw Error(c, "Schema", "NUL is not a PDF name byte.");
        return new string(bytes.Select(b => (char)b).ToArray());
    }
    private static byte[] Unbase64(string text, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, text.Length * 4L + 64);
        try
        {
            byte[] bytes = Convert.FromBase64String(text);
            if (Convert.ToBase64String(bytes) != text) throw Error(c, "Schema", "Canonical base64 is required.");
            return bytes;
        }
        catch (FormatException) { throw Error(c, "Schema", "Canonical base64 is required."); }
    }
    private static void Charge(PdfOperationContext c) { c.Checkpoint(); c.Consume(PdfResourceKind.Objects, 1); c.Consume(PdfResourceKind.ExpandedBytes, 512); }
    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Interchange." + code, message);
    private static void Report(PdfOperationContext c) => c.Report(new PdfDiagnostic("Interchange.ActiveGraph", "Interchange retains the active object graph; file offsets, free entries, compression containers and prior revisions are regenerated or omitted.", severity: PdfDiagnosticSeverity.Information));
}
