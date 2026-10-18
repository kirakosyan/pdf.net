using System.IO;

namespace Fluent.Pdf;
/// <summary>Explicit embedded-file metadata. The SDK never reads a file clock or executes payloads.</summary>
public sealed class PdfAttachmentOptions
{
    /// <summary>Captures a portable leaf filename, MIME type, explicit dates and optional standard/custom associated-file relationship.</summary>
    public PdfAttachmentOptions(string fileName, string mimeType = "application/octet-stream", string? description = null, DateTimeOffset? creationTime = null, DateTimeOffset? modificationTime = null, string? relationship = null)
    {
        FileName = PortableName(fileName);
        MimeType = Guard.Text(mimeType, nameof(mimeType), 127);
        int separator = mimeType.IndexOf('/');
        if (separator < 1 || separator == mimeType.Length - 1 || mimeType.Count(c => c == '/') != 1 || mimeType.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || "!#$%&'*+-.^_`|~/".IndexOf(c) >= 0)))
            throw new ArgumentException("A bounded MIME type without parameters is required.", nameof(mimeType));
        Description = description == null ? null : Guard.Text(description, nameof(description), 8192);
        CreationTime = creationTime;
        ModificationTime = modificationTime;
        if (creationTime.HasValue)
            PdfMetadataDates.Format(creationTime.Value);
        if (modificationTime.HasValue)
            PdfMetadataDates.Format(modificationTime.Value);
        if (relationship != null)
        {
            PdfMetadataValues.Key(relationship);
            if (!modificationTime.HasValue)
                throw new ArgumentException("Associated-file parameters require an explicit modification time.", nameof(modificationTime));
            if (relationship == "EncryptedPayload")
                throw new ArgumentException("Encrypted payloads require a separately qualified wrapper/encryption profile.", nameof(relationship));
        }

        Relationship = relationship;
    }

    /// <summary>Gets the portable leaf filename written to UF and used as the name-tree key.</summary>
    public string FileName { get; }
    /// <summary>Gets the embedded stream MIME subtype.</summary>
    public string MimeType { get; }
    /// <summary>Gets the optional Unicode description.</summary>
    public string? Description { get; }
    /// <summary>Gets the explicit creation timestamp.</summary>
    public DateTimeOffset? CreationTime { get; }
    /// <summary>Gets the explicit modification timestamp.</summary>
    public DateTimeOffset? ModificationTime { get; }
    /// <summary>Gets the associated-file relationship, or null for an ordinary attachment.</summary>
    public string? Relationship { get; }

    internal static string PortableName(string name)
    {
        Guard.Text(name, nameof(name), 240);
        try
        {
            if (new System.Text.UTF8Encoding(false, true).GetByteCount(name) > 240)
                throw new ArgumentException("The portable filename exceeds 240 UTF-8 bytes.", nameof(name));
        }
        catch (System.Text.EncoderFallbackException)
        {
            throw new ArgumentException("A well-formed Unicode filename is required.", nameof(name));
        }

        if (name == "." || name == ".." || name.Any(c => char.IsControl(c) || "<>:\"/\\|?*".IndexOf(c) >= 0) || name.EndsWith(".", StringComparison.Ordinal) || name.EndsWith(" ", StringComparison.Ordinal) || Path.IsPathRooted(name))
            throw new ArgumentException("A portable filename with no directory, drive or control characters is required.", nameof(name));
        string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CLOCK$" || (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem.Length == 4 && (stem[3] >= '0' && stem[3] <= '9' || "¹²³".IndexOf(stem[3]) >= 0))
            throw new ArgumentException("A reserved device filename is not portable.", nameof(name));
        return name;
    }
}

/// <summary>A detached passive file specification, including all embedded platform variants and raw custom values.</summary>
public sealed class PdfAttachment
{
    internal PdfAttachment(PdfDocumentSnapshot source, PdfObjectId? id, PdfValue specification, string? name, string fileName, string? description, string? relationship, Dictionary<string, PdfValue> variants)
    {
        Source = source;
        Id = id;
        Specification = specification;
        Name = name;
        FileName = fileName;
        Description = description;
        Relationship = relationship;
        Variants = new System.Collections.ObjectModel.ReadOnlyDictionary<string, PdfValue>(variants);
    }

    /// <summary>Gets the immutable captured source; later extraction does not need the original live document.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets the indirect file specification identity, or null for a malformed direct source specification.</summary>
    public PdfObjectId? Id { get; }
    /// <summary>Gets the raw resolved file specification.</summary>
    public PdfValue Specification { get; }
    /// <summary>Gets its first EmbeddedFiles name-tree name, or null when it is not in that tree.</summary>
    public string? Name { get; }
    /// <summary>Gets UF, falling back to F; no path is accessed while inspecting this value.</summary>
    public string FileName { get; }
    /// <summary>Gets the optional Unicode description.</summary>
    public string? Description { get; }
    /// <summary>Gets the passive AFRelationship name, including custom values.</summary>
    public string? Relationship { get; }
    /// <summary>Gets resolved embedded streams by their EF keys; absent variants denote an external specification.</summary>
    public IReadOnlyDictionary<string, PdfValue> Variants { get; }
}

/// <summary>A passive all-active-object file inventory and raw portable-collection dictionary.</summary>
public sealed class PdfAttachmentSet
{
    internal PdfAttachmentSet(PdfDocumentSnapshot source, List<PdfAttachment> items, List<PdfValue> streams, PdfValue collection, PdfOperationReport report)
    {
        Source = source;
        Items = items.AsReadOnly();
        EmbeddedStreams = streams.AsReadOnly();
        Collection = collection;
        OperationReport = report;
    }

    /// <summary>Gets the captured source.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets file specifications, including orphan and external definitions.</summary>
    public IReadOnlyList<PdfAttachment> Items { get; }
    /// <summary>Gets all active EmbeddedFile streams, including orphans without a file specification.</summary>
    public IReadOnlyList<PdfValue> EmbeddedStreams { get; }
    /// <summary>Gets the raw resolved Collection dictionary, or null; inspection does not open its initial file.</summary>
    public PdfValue Collection { get; }
    /// <summary>Gets operation diagnostics and resource usage.</summary>
    public PdfOperationReport OperationReport { get; }
}
