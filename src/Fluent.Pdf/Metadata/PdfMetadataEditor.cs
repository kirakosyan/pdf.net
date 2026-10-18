using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace Fluent.Pdf;
/// <summary>A synchronous, operation-owned metadata editor. It expires when its callback returns.</summary>
public sealed class PdfMetadataEditor
{
    private readonly PdfGraphEdit _graph;
    private readonly int _thread = Thread.CurrentThread.ManagedThreadId;
    private readonly Dictionary<string, PdfValue> _information;
    private bool _active = true, _infoDirty, _xmpDirty, _xmlLoaded;
    private PdfMetadataXml? _xml;
    internal PdfMetadataEditor(PdfGraphEdit graph)
    {
        _graph = graph;
        _information = graph.Copy(PdfMetadataValues.Information(graph));
    }

    private void Check()
    {
        if (!_active)
            throw new InvalidOperationException("The metadata editor has expired.");
        if (Thread.CurrentThread.ManagedThreadId != _thread)
            throw new InvalidOperationException("Metadata edits must remain on their owning thread.");
        _graph.Context.Checkpoint();
    }

    private PdfMetadataXml? Xml()
    {
        Check();
        if (!_xmlLoaded)
        {
            _xml = PdfMetadataXml.Read(_graph, _graph.Get(_graph.Catalog, "Metadata"));
            _xmlLoaded = true;
        }

        return _xml;
    }

    private PdfMetadataXml WritableXml() => Xml() ?? (_xml = PdfMetadataXml.Empty(_graph.Context));
    /// <summary>Gets a resolved Info text field, or null when absent. Unsupported field types reject this typed access.</summary>
    public string? GetInformation(string name)
    {
        Check();
        PdfMetadataValues.Key(name);
        _graph.Context.Consume(PdfResourceKind.Objects, 1);
        return _information.TryGetValue(name, out var value) ? PdfTextStrings.Decode(_graph.Resolve(value), _graph.Context) : null;
    }

    /// <summary>Writes exact explicitly encoded Info text and raises the catalog version floor for UTF-8. Date/Trapped semantic validation remains explicit.</summary>
    public PdfMetadataEditor SetInformationText(string name, PdfTextString text)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        Check();
        if (name == "Trapped")
            throw new ArgumentException("Trapped is a PDF name; use SetInformation for its semantic value.", nameof(name));
        PdfMetadataValues.Key(name);
        _graph.Context.Consume(PdfResourceKind.Objects, 1);
        if (name == "CreationDate" || name == "ModDate")
            PdfMetadataDates.Validate(text.Text, _graph.Context);
        _graph.Context.Consume(PdfResourceKind.ExpandedBytes, text.Value.StringBytes.Count + 64L);
        _information[name] = text.Value;
        _infoDirty = true;
        if (text.MinimumVersion == "2.0")
        {
            var catalog = _graph.Copy(_graph.Catalog);
            catalog["Version"] = PdfValue.Name("2.0");
            _graph.SetCatalog(catalog);
        }

        return this;
    }

    /// <summary>Sets or removes a standard/custom Info text field. Trapped uses its standard name representation.</summary>
    public PdfMetadataEditor SetInformation(string name, string? value)
    {
        Check();
        PdfMetadataValues.Key(name);
        _graph.Context.Consume(PdfResourceKind.Objects, 1);
        if (value == null)
            _information.Remove(name);
        else
        {
            if (name == "CreationDate" || name == "ModDate")
                PdfMetadataDates.Validate(value, _graph.Context);
            if (name == "Trapped")
            {
                if (value != "True" && value != "False" && value != "Unknown")
                    throw new ArgumentException("Trapped must be True, False or Unknown.", nameof(value));
                _information[name] = PdfValue.Name(value);
            }
            else
                _information[name] = PdfTextStrings.Encode(value, _graph.Context);
        }

        _infoDirty = true;
        return this;
    }

    /// <summary>Sets or removes an explicit whole-second creation timestamp, retaining its supplied offset.</summary>
    public PdfMetadataEditor SetCreationTime(DateTimeOffset? value) => SetInformation("CreationDate", value.HasValue ? PdfMetadataDates.Format(value.Value) : null);
    /// <summary>Sets or removes an explicit whole-second modification timestamp, retaining its supplied offset.</summary>
    public PdfMetadataEditor SetModificationTime(DateTimeOffset? value) => SetInformation("ModDate", value.HasValue ? PdfMetadataDates.Format(value.Value) : null);
    /// <summary>Sets caller-chosen permanent/revision ID bytes. Omitting revision uses the permanent ID for both entries.</summary>
    public PdfMetadataEditor SetIdentity(byte[] permanent, byte[]? revision = null)
    {
        Check();
        if (permanent == null)
            throw new ArgumentNullException(nameof(permanent));
        revision ??= permanent;
        if (permanent.Length == 0 || revision.Length == 0)
            throw new ArgumentException("Both document identifiers must be nonempty byte strings.");
        _graph.Context.Consume(PdfResourceKind.ExpandedBytes, permanent.LongLength * 2 + revision.LongLength * 2 + 256);
        _graph.Context.Consume(PdfResourceKind.Objects, 3);
        if (_graph.Get(_graph.Edit.Trailer, "ID").Kind == PdfValueKind.Null && !permanent.SequenceEqual(revision))
            throw _graph.Error("Metadata.InitialIdentity", "The first identifier pair must use the same permanent and revision bytes.");
        var entries = _graph.Copy(_graph.Edit.Trailer);
        entries["ID"] = PdfValue.Array(new[] { PdfValue.String(permanent), PdfValue.String(revision) });
        _graph.Edit.Trailer = PdfValue.OwnedDictionary(entries);
        return this;
    }

    /// <summary>Removes trailer ID bytes in this full-rewrite edit.</summary>
    public PdfMetadataEditor RemoveIdentity()
    {
        Check();
        var entries = _graph.Copy(_graph.Edit.Trailer);
        entries.Remove("ID");
        _graph.Edit.Trailer = PdfValue.OwnedDictionary(entries);
        return this;
    }

    /// <summary>Replaces or removes the XMP packet. XML remains offline and prohibits DTDs and external entities.</summary>
    public PdfMetadataEditor SetXmp(string? xml)
    {
        Check();
        _xml = xml == null ? null : PdfMetadataXml.Parse(xml, _graph.Context);
        _xmlLoaded = true;
        _xmpDirty = true;
        return this;
    }

    /// <summary>Sets or removes one scalar property by namespace URI/local name, preserving unrelated namespaces and structured properties.</summary>
    public PdfMetadataEditor SetXmpProperty(string namespaceUri, string localName, string? value)
    {
        Check();
        Guard.Text(namespaceUri, nameof(namespaceUri), 4096);
        Guard.Text(localName, nameof(localName), 256);
        try
        {
            XmlConvert.VerifyNCName(localName);
        }
        catch (XmlException)
        {
            throw new ArgumentException("An XML local name is required.", nameof(localName));
        }

        if (!Uri.TryCreate(namespaceUri, UriKind.Absolute, out _) || namespaceUri == PdfMetadataXml.Rdf.NamespaceName || namespaceUri == XNamespace.Xml.NamespaceName || namespaceUri == XNamespace.Xmlns.NamespaceName)
            throw new ArgumentException("A non-structural absolute metadata namespace URI is required.", nameof(namespaceUri));
        WritableXml().Set(XName.Get(localName, namespaceUri), value, 0, _graph.Context);
        _xmpDirty = true;
        return this;
    }

    /// <summary>Synchronizes representable fields using explicit authority. Structured values that cannot fit Info reject rather than flatten silently.</summary>
    public PdfMetadataEditor Synchronize(PdfMetadataSynchronization authority)
    {
        Check();
        if (!Enum.IsDefined(typeof(PdfMetadataSynchronization), authority))
            throw new ArgumentOutOfRangeException(nameof(authority));
        var xml = Xml();
        if (authority == PdfMetadataSynchronization.PreferNewest)
        {
            var infoTime = GetInformation("ModDate");
            var packetTime = xml?.Scalar(PdfMetadataXml.Xmp + "MetadataDate", _graph.Context);
            if (infoTime == null || packetTime == null)
                throw _graph.Error("Metadata.AuthorityUndetermined", "Newest-authority synchronization requires explicit Info ModDate and XMP MetadataDate values.");
            var information = PdfMetadataDates.Instant(infoTime, false, _graph.Context);
            var packet = PdfMetadataDates.Instant(packetTime, true, _graph.Context);
            if (!information.HasValue || !packet.HasValue)
                throw _graph.Error("Metadata.AuthorityUndetermined", "Newest-authority synchronization requires full dates with explicit representable offsets.");
            authority = packet.Value >= information.Value ? PdfMetadataSynchronization.XmpToInformation : PdfMetadataSynchronization.InformationToXmp;
        }

        if (authority == PdfMetadataSynchronization.InformationToXmp)
        {
            xml = WritableXml();
            foreach (var mapping in PdfMetadataValues.Mappings)
            {
                string? value;
                if (mapping.Info == "Trapped" && _information.TryGetValue("Trapped", out var trapped))
                {
                    var resolved = _graph.Resolve(trapped);
                    if (resolved.Kind != PdfValueKind.Name || !new[]
                    {
                        "True",
                        "False",
                        "Unknown"
                    }.Contains(resolved.NameValue))
                        throw _graph.Error("Metadata.InvalidTrapped", "Trapped must use its standard name representation.");
                    value = resolved.NameValue;
                }
                else
                    value = GetInformation(mapping.Info);
                if (value != null && mapping.Date)
                    value = PdfMetadataDates.ToXmp(value, _graph.Context);
                xml.Set(mapping.Name, value, mapping.Kind, _graph.Context);
            }

            // Explicit ModDate supplies the synchronization clock; no system time is consulted.
            var modified = GetInformation("ModDate");
            if (modified != null)
                xml.Set(PdfMetadataXml.Xmp + "MetadataDate", PdfMetadataDates.ToXmp(modified, _graph.Context), 0, _graph.Context);
            _xmpDirty = true;
        }
        else
        {
            if (xml == null)
                throw _graph.Error("Metadata.PacketRequired", "XMP-authoritative synchronization requires an XMP packet.");
            foreach (var mapping in PdfMetadataValues.Mappings)
            {
                string? value = mapping.Kind == 0 ? xml.Scalar(mapping.Name, _graph.Context) : xml.Text(mapping.Name, mapping.Kind == 1, _graph.Context);
                if (value != null && mapping.Date)
                    value = PdfMetadataDates.ToPdf(value, _graph.Context);
                SetInformation(mapping.Info, value);
            }
        }

        return this;
    }

    internal void Commit()
    {
        Check();
        if (_infoDirty)
        {
            var trailer = _graph.Copy(_graph.Edit.Trailer);
            if (_information.Count == 0)
                trailer.Remove("Info");
            else
            {
                _graph.Context.Consume(PdfResourceKind.Objects, _information.Count + 1);
                var info = PdfValue.OwnedDictionary(_information);
                if (trailer.TryGetValue("Info", out var old) && old.Kind == PdfValueKind.Reference)
                    _graph.Edit.Set(old.ReferenceId, info);
                else
                    trailer["Info"] = PdfValue.Reference(_graph.Edit.Add(info));
            }

            _graph.Edit.Trailer = PdfValue.OwnedDictionary(trailer);
        }

        if (_xmpDirty)
        {
            var catalog = _graph.Copy(_graph.Catalog);
            if (_xml == null)
                catalog.Remove("Metadata");
            else
            {
                var bytes = _xml.Bytes(_graph.Context);
                var dictionary = PdfValue.Dictionary(new Dictionary<string, PdfValue> { { "Type", PdfValue.Name("Metadata") }, { "Subtype", PdfValue.Name("XML") } });
                catalog["Metadata"] = PdfValue.Reference(_graph.Edit.Add(PdfValue.StreamValue(dictionary, bytes, 0, bytes.Length)));
            }

            _graph.SetCatalog(catalog);
        }
    }

    internal void Expire()
    {
        _active = false;
    }
}
