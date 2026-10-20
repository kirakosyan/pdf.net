using System.IO;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace Fluent.Pdf;
/// <summary>One immutable original XFA XML packet and exact source stream identity.</summary>
public sealed class PdfXfaPacket
{
    internal PdfXfaPacket(string name, PdfObjectId? id, string xml, XDocument document)
    {
        Name = name;
        ObjectId = id;
        Xml = xml;
        Document = document;
    }
    internal XDocument Document { get; }
    /// <summary>Gets exact packet name.</summary>
    public string Name
    {
        get;
    }
    /// <summary>Gets original indirect stream identity when present.</summary>
    public PdfObjectId? ObjectId
    {
        get;
    }
    /// <summary>Gets decoded XML without executing scripts, resolving resources or laying out dynamic forms.</summary>
    public string Xml
    {
        get;
    }
}
/// <summary>Passive packet and scalar datasets inventory. Repeated/ambiguous data paths reject rather than guessing SOM indices.</summary>
public sealed class PdfXfaSet
{
    internal PdfXfaSet(List<PdfXfaPacket> packets, Dictionary<string, string> data, PdfOperationReport report)
    {
        Packets = packets.AsReadOnly();
        Data = new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(data);
        OperationReport = report;
    }
    /// <summary>Gets preserved packet order.</summary>
    public IReadOnlyList<PdfXfaPacket> Packets
    {
        get;
    }
    /// <summary>Gets exact dot-separated scalar XML element paths below datasets/data.</summary>
    public IReadOnlyDictionary<string, string> Data
    {
        get;
    }
    /// <summary>Gets bounded inspection report.</summary>
    public PdfOperationReport OperationReport
    {
        get;
    }
}
/// <summary>Original passive XFA packet handling, bounded scalar filling and explicit static text-field conversion.</summary>
public static class PdfXfa
{
    /// <summary>Inspects packet-array XFA. Single XDP streams and dynamic layout require a separate profile; no XFA instruction ever runs.</summary>
    public static PdfXfaSet Inspect(PdfDocumentSnapshot source, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        List<PdfXfaPacket>? packets = null;
        Dictionary<string, string>? data = null;
        var report = PdfFont.Run("InspectXfa", "managed.xfa", policy ?? source.Options.OperationPolicy, cancellationToken, c => { using var g = new PdfGraphEdit(source, new(), c); packets = Read(g); data = Dataset(packets, c).ToDictionary(p => p.Key, p => p.Value.Value, StringComparer.Ordinal); }, PdfCapability.ReadDocument);
        return new(packets!, data!, report);
    }
    /// <summary>Atomically replaces existing unambiguous scalar datasets values only, preserving template/other packets, namespaces and stream identities. Scripts/layout are not evaluated.</summary>
    public static PdfDocument Fill(PdfDocumentSnapshot source, IReadOnlyDictionary<string, string> values, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));
        return PdfGraphEdit.Run("FillXfa", source, options, policy, cancellationToken, g =>
        {
            var packets = Read(g);
            var fields = Dataset(packets, g.Context);
            foreach (var pair in values)
            {
                g.Context.Checkpoint();
                g.Context.Consume(PdfResourceKind.Objects, 1);
                if (pair.Value == null || !fields.TryGetValue(pair.Key, out var element))
                    throw g.Error("Xfa.DataPath", "Every update must name an existing unique scalar data path.");
                g.Context.Consume(PdfResourceKind.ExpandedBytes, pair.Value.Length * 8L + 256);
                try { XmlConvert.VerifyXmlChars(pair.Value); }
                catch (XmlException) { g.Context.Checkpoint(); throw g.Error("Xfa.XmlValue", "Dataset values must contain valid XML characters."); }
                element.Value = pair.Value;
            }
            var packet = packets.Single(p => p.Name == "datasets");
            var root = packet.Document;
            // All XElement views belong to the same parsed immutable-input tree; unrelated packet bytes stay untouched.
            string xml = root!.ToString(SaveOptions.DisableFormatting);
            g.Context.Consume(PdfResourceKind.ExpandedBytes, xml.Length * 4L + 128);
            using var encoded = new MemoryStream(new UTF8Encoding(false, true).GetBytes(xml));
            var bytes = PdfByteStore.Read(encoded, g.Context);
            var stream = PdfValue.StreamValue(PdfGraphicsNumbers.Dict(), bytes, 0, bytes.Length);
            if (!packet.ObjectId.HasValue)
                throw g.Error("Xfa.PacketIdentity", "Dataset editing requires an indirect stream.");
            g.Edit.Set(packet.ObjectId.Value, stream);
        });
    }
    /// <summary>Converts exactly covered static text fields using caller-supplied page/widget appearances and unchanged qualified identities. Unknown template constructs/scripts/dynamic bindings reject. Source packet capture remains separately available through Inspect.</summary>
    public static PdfDocument ConvertToAcroForm(PdfDocumentSnapshot source, IEnumerable<PdfFormFieldDefinition> fields, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (fields == null)
            throw new ArgumentNullException(nameof(fields));
        return PdfGraphEdit.Run("ConvertStaticXfa", source, options, policy, cancellationToken, g =>
        {
            var packets = Read(g);
            var data = Dataset(packets, g.Context);
            var template = packets.SingleOrDefault(p => p.Name == "template") ?? throw g.Error("Xfa.Template", "One explicit template packet is required.");
            var xml = template.Document;
            string templateNamespace = xml.Root?.Name.NamespaceName ?? "";
            if (xml.Root?.Name.LocalName != "template" || templateNamespace != "" && templateNamespace != "http://www.xfa.org/schema/xfa-template/2.8/" && templateNamespace != "http://www.xfa.org/schema/xfa-template/3.3/")
                throw g.Error("Xfa.UnsupportedTemplate", "Only explicit unnamespaced, XFA2.8 or XFA3.3 static template roots are supported.");
            var names = new HashSet<string>(StringComparer.Ordinal);
            Walk(xml.Root!, "");
            void Walk(XElement e, string path)
            {
                g.Context.Checkpoint();
                using var depth = g.Context.EnterScope(PdfResourceKind.Depth);
                string name = e.Name.LocalName;
                if (e.Name.NamespaceName != templateNamespace)
                    throw g.Error("Xfa.UnsupportedTemplate", "Template namespace extensions require a separate conversion profile.");
                if (name != "template" && name != "subform" && name != "field" && name != "ui" && name != "textEdit" && name != "value" && name != "text")
                    throw g.Error("Xfa.UnsupportedTemplate", "Only explicit static subform/field/textEdit/value/text constructs are supported; dynamic behavior is unavailable.");
                if (e.Attributes().Any(a => !a.IsNamespaceDeclaration && (a.Name.NamespaceName != "" || a.Name.LocalName != "name" && a.Name.LocalName != "layout")))
                    throw g.Error("Xfa.UnsupportedTemplate", "Template attributes require an explicitly supported binding/layout profile.");
                if (e.Attribute("layout") is XAttribute layout && layout.Value != "position")
                    throw g.Error("Xfa.DynamicLayout", "Dynamic layout is not supported by explicit static conversion.");
                if (name == "subform" || name == "field")
                {
                    string? n = e.Attribute("name")?.Value;
                    if (string.IsNullOrEmpty(n) || n!.Contains("."))
                        throw g.Error("Xfa.FieldIdentity", "Static identities require nonempty undotted name segments.");
                    path = path.Length == 0 ? n : path + "." + n;
                }
                if (name == "field" && !names.Add(path))
                    throw g.Error("Xfa.FieldIdentity", "Duplicate template field identities reject.");
                foreach (var child in e.Elements())
                    Walk(child, path);
            }
            if (!names.SetEquals(data.Keys))
                throw g.Error("Xfa.FieldCoverage", "Every scalar data identity must match exactly one static template field.");
            var definitions = new List<PdfFormFieldDefinition>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in fields)
            {
                g.Context.Checkpoint();
                g.Context.Consume(PdfResourceKind.Objects, 1);
                g.Context.Consume(PdfResourceKind.ExpandedBytes, 512);
                if (field == null || field.Kind != PdfFormFieldKind.Text || !names.Contains(field.Name) || !seen.Add(field.Name))
                    throw g.Error("Xfa.FieldCoverage", "Every explicit text-field definition must preserve one original identity.");
                definitions.Add(field);
            }
            if (!seen.SetEquals(names))
                throw g.Error("Xfa.FieldCoverage", "Complete explicit page/widget mapping is required.");
            var form = g.Get(g.Catalog, "AcroForm");
            var oldFields = g.Get(form, "Fields");
            if (oldFields.Kind == PdfValueKind.Array && oldFields.Items.Count > 0)
                throw g.Error("Xfa.HybridProfile", "Hybrid existing AcroForm fields require an explicit reconciliation profile.");
            var dictionary = g.Copy(form);
            dictionary.Remove("XFA");
            PdfFormValues.Form(g, dictionary);
            foreach (var field in definitions)
                PdfForms.AddCore(g, new(field.Name, field.Kind, field.Widgets.ToArray(), PdfTextStrings.Encode(data[field.Name].Value, g.Context), flags: field.Flags, maximumLength: field.MaximumLength, alternateName: field.AlternateName, mappingName: field.Name, editingFont: field.EditingFont, fontSize: field.FontSize));
        }, "1.7");
    }
    private static List<PdfXfaPacket> Read(PdfGraphEdit g)
    {
        var packets = new List<PdfXfaPacket>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        var xfa = g.Get(g.Get(g.Catalog, "AcroForm"), "XFA");
        if (xfa.Kind != PdfValueKind.Array || xfa.Items.Count == 0 || xfa.Items.Count % 2 != 0)
            throw g.Error("Xfa.PacketProfile", "A nonempty name/indirect-stream packet array is required.");
        for (int i = 0; i < xfa.Items.Count; i += 2)
        {
            g.Context.Checkpoint();
            g.Context.Consume(PdfResourceKind.Objects, 1);
            var nameValue = g.Resolve(xfa.Items[i]);
            string name = PdfTextStrings.Decode(nameValue, g.Context);
            if (!names.Add(name))
                throw g.Error("Xfa.PacketIdentity", "Duplicate packet names reject.");
            var reference = xfa.Items[i + 1];
            var value = g.Resolve(reference);
            if (reference.Kind != PdfValueKind.Reference || value.Kind != PdfValueKind.Stream)
                throw g.Error("Xfa.PacketProfile", "XFA packets require indirect streams.");
            using var decoded = PdfFilters.Decode(PdfFilterDeclarations.Resolve(value, id => g.Resolve(PdfValue.Reference(id)), g.Context), g.Context);
            using var bounded = new PdfCodecBufferStream(g.Context, 16777216);
            var chunk = new byte[4096];
            g.Context.Consume(PdfResourceKind.ExpandedBytes, 4096);
            int n;
            while ((n = decoded.Read(chunk, 0, chunk.Length)) > 0)
            {
                g.Context.Checkpoint();
                bounded.Write(chunk, 0, n);
            }
            var bytes = bounded.Finish();
            g.Context.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 8L + 128);
            using var input = bytes.Open(0, bytes.Length);
            using var reader = new StreamReader(input, new UTF8Encoding(false, true), true, 4096);
            string xml;
            try
            {
                xml = reader.ReadToEnd();
            }
            catch (DecoderFallbackException) { g.Context.Checkpoint(); throw g.Error("Xfa.XmlEncoding", "XFA packet text encoding is invalid."); }
            var document = Parse(xml, g.Context);
            packets.Add(new(name, reference.ReferenceId, xml, document));
        }
        return packets;
    }
    private static Dictionary<string, XElement> Dataset(List<PdfXfaPacket> packets, PdfOperationContext c)
    {
        var packet = packets.SingleOrDefault(p => p.Name == "datasets") ?? throw PdfWriterCore.Error(c, "Xfa.Datasets", "One datasets packet is required.");
        var xml = packet.Document;
        var root = xml.Root;
        if (root == null || root.Name.LocalName != "datasets")
            throw PdfWriterCore.Error(c, "Xfa.Datasets", "The datasets root is required.");
        var dataNodes = root.Elements().Where(e => e.Name.LocalName == "data").ToArray();
        if (dataNodes.Length != 1)
            throw PdfWriterCore.Error(c, "Xfa.Datasets", "One unambiguous data element is required.");
        var data = dataNodes[0];
        var result = new Dictionary<string, XElement>(StringComparer.Ordinal);
        UniqueChildren(data);
        foreach (var e in data.Elements())
            Walk(e, "");
        return result;
        void Walk(XElement e, string parent)
        {
            c.Checkpoint();
            using var depth = c.EnterScope(PdfResourceKind.Depth);
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 256 + e.Name.LocalName.Length * 2L);
            if (e.Attributes().Any(a => !a.IsNamespaceDeclaration))
                throw PdfWriterCore.Error(c, "Xfa.DataPath", "Attributed scalar/container data requires an explicit binding profile.");
            if (e.Name.LocalName.Contains("."))
                throw PdfWriterCore.Error(c, "Xfa.DataPath", "Dot-containing names need an indexed SOM profile.");
            string path = parent.Length == 0 ? e.Name.LocalName : parent + "." + e.Name.LocalName;
            if (e.HasElements)
            {
                UniqueChildren(e);
                if (e.Nodes().OfType<XText>().Any(t => !string.IsNullOrWhiteSpace(t.Value)))
                    throw PdfWriterCore.Error(c, "Xfa.DataPath", "Mixed scalar/container data requires a separate data binding profile.");
                foreach (var child in e.Elements())
                    Walk(child, path);
            }
            else
            {
                if (result.ContainsKey(path))
                    throw PdfWriterCore.Error(c, "Xfa.DataPath", "Repeated or namespace-ambiguous scalar data paths reject.");
                result.Add(path, e);
            }
        }
        void UniqueChildren(XElement parent)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var child in parent.Elements())
            {
                c.Checkpoint();
                c.Consume(PdfResourceKind.ExpandedBytes, 64 + child.Name.LocalName.Length * 2L);
                if (!names.Add(child.Name.LocalName))
                    throw PdfWriterCore.Error(c, "Xfa.DataPath", "Repeated or namespace-ambiguous element identities require an indexed SOM profile.");
            }
        }
    }
    private static XDocument Parse(string xml, PdfOperationContext c)
    {
        if (xml.Length > 8388608)
            throw PdfWriterCore.Error(c, "Xfa.XmlExtent", "XFA XML exceeds its finite extent.");
        c.Consume(PdfResourceKind.ExpandedBytes, xml.Length * 8L + 4096);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 8388608, MaxCharactersFromEntities = 1024 };
        try
        {
            using (var preflight = XmlReader.Create(new StringReader(xml), settings))
                while (preflight.Read())
                {
                    c.Checkpoint();
                    if (preflight.Depth >= c.Policy.Limits.MaximumDepth)
                        c.RejectLimit(PdfResourceKind.Depth);
                    c.Consume(PdfResourceKind.Objects, 1 + preflight.AttributeCount);
                    c.Consume(PdfResourceKind.ExpandedBytes, 256L * (1 + preflight.AttributeCount) + preflight.Value.Length * 2L);
                }
            using var reader = XmlReader.Create(new StringReader(xml), settings);
            return XDocument.Load(reader, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException) { c.Checkpoint(); throw PdfWriterCore.Error(c, "Xfa.Xml", "Malformed XML, entities and external references are rejected."); }
    }
}
