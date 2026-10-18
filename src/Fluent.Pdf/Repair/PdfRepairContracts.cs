using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace Fluent.Pdf;
/// <summary>Explicit treatment of indexed objects unreachable from the recovered trailer.</summary>
public enum PdfRepairOrphanPolicy
{
    /// <summary>Retain and identify every unreachable indexed object.</summary>
    Preserve,
    /// <summary>Discard and identify every unreachable indexed object; strict mode rejects this reported data loss.</summary>
    Discard
}

/// <summary>A caller-authorized exact raw stream length; recovery never guesses an endstream marker inside payload bytes.</summary>
public sealed class PdfRepairStreamLength
{
    /// <summary>Captures an exact object identity and nonnegative payload byte count.</summary>
    public PdfRepairStreamLength(PdfObjectId objectId, long length)
    {
        if (objectId.Number <= 0)
            throw new ArgumentOutOfRangeException(nameof(objectId));
        if (length < 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        ObjectId = objectId;
        Length = length;
    }

    /// <summary>Gets the exact stream object identity.</summary>
    public PdfObjectId ObjectId { get; }
    /// <summary>Gets the explicitly authorized raw payload length.</summary>
    public long Length { get; }
}

/// <summary>Explicit recovery authority. Normal parsing never uses these options.</summary>
public sealed class PdfRepairOptions
{
    /// <summary>Captures root/orphan/length/signature choices. ActiveObjects, when supplied, is a complete caller-authorized identity inventory used when xref status rows cannot establish active objects; source bodies are never automatically resurrected.</summary>
    public PdfRepairOptions(PdfObjectId? root = null, PdfRepairOrphanPolicy orphans = PdfRepairOrphanPolicy.Preserve, PdfRepairStreamLength[]? streamLengths = null, PdfObjectId[]? activeObjects = null, bool rejectDanglingReferences = true, bool allowSignatureInvalidation = false)
    {
        if (root.HasValue && root.Value.Number <= 0)
            throw new ArgumentOutOfRangeException(nameof(root));
        if (!Enum.IsDefined(typeof(PdfRepairOrphanPolicy), orphans))
            throw new ArgumentOutOfRangeException(nameof(orphans));
        var lengths = streamLengths ?? Array.Empty<PdfRepairStreamLength>();
        if (lengths.Length > 4096 || lengths.Any(x => x == null) || lengths.Select(x => x.ObjectId).Distinct().Count() != lengths.Length)
            throw new ArgumentException("A bounded unique stream-length inventory is required.", nameof(streamLengths));
        if (activeObjects != null && (activeObjects.Length < 1 || activeObjects.Length > 100000 || activeObjects.Any(id => id.Number <= 0) || activeObjects.Select(id => id.Number).Distinct().Count() != activeObjects.Length))
            throw new ArgumentException("A nonempty bounded inventory with one active generation per object number is required.", nameof(activeObjects));
        Root = root;
        Orphans = orphans;
        StreamLengths = Array.AsReadOnly((PdfRepairStreamLength[])lengths.Clone());
        ActiveObjects = activeObjects == null ? null : Array.AsReadOnly((PdfObjectId[])activeObjects.Clone());
        RejectDanglingReferences = rejectDanglingReferences;
        AllowSignatureInvalidation = allowSignatureInvalidation;
    }

    /// <summary>Gets an explicit root, or null to retain the recovered trailer/infer a unique active Catalog.</summary>
    public PdfObjectId? Root { get; }
    /// <summary>Gets the explicit orphan policy.</summary>
    public PdfRepairOrphanPolicy Orphans { get; }
    /// <summary>Gets exact caller-authorized stream payload lengths.</summary>
    public IReadOnlyList<PdfRepairStreamLength> StreamLengths { get; }
    /// <summary>Gets a complete caller-authorized active identity inventory, or null to require recoverable xref status rows.</summary>
    public IReadOnlyList<PdfObjectId>? ActiveObjects { get; }
    /// <summary>Gets whether missing/generation-mismatched referenced objects block recovery.</summary>
    public bool RejectDanglingReferences { get; }
    /// <summary>Gets whether an explicitly reported signature-invalidating full rewrite is authorized.</summary>
    public bool AllowSignatureInvalidation { get; }
}

/// <summary>A deterministic recovery/validation ledger record. Messages contain no document strings or stream payloads.</summary>
public sealed class PdfRepairRecord
{
    internal PdfRepairRecord(string code, PdfObjectId? id, long? offset, string message)
    {
        Code = code;
        ObjectId = id;
        SourceOffset = offset;
        Message = message;
    }

    /// <summary>Gets the stable machine-readable record code.</summary>
    public string Code { get; }
    /// <summary>Gets the affected exact object identity, when applicable.</summary>
    public PdfObjectId? ObjectId { get; }
    /// <summary>Gets an exact original byte offset, or null for a graph-level observation.</summary>
    public long? SourceOffset { get; }
    /// <summary>Gets a bounded description without imported payload content.</summary>
    public string Message { get; }
}

/// <summary>A detached explicit recovery result with both exact source/output views and a complete repair ledger.</summary>
public sealed class PdfRepairResult
{
    private readonly PdfByteStore _original, _repaired;
    internal PdfRepairResult(PdfByteStore original, PdfByteStore repaired, string originalHash, string repairedHash, PdfRepairRecord[] records, PdfOperationReport report)
    {
        _original = original;
        _repaired = repaired;
        OriginalSha256 = originalHash;
        RepairedSha256 = repairedHash;
        Ledger = Array.AsReadOnly(records);
        OperationReport = report;
    }

    /// <summary>Gets the untouched source digest.</summary>
    public string OriginalSha256 { get; }
    /// <summary>Gets the exact recovered output digest.</summary>
    public string RepairedSha256 { get; }
    /// <summary>Gets every recovered/repaired/discarded-object and structural decision.</summary>
    public IReadOnlyList<PdfRepairRecord> Ledger { get; }
    /// <summary>Gets operation diagnostics and shared budget usage.</summary>
    public PdfOperationReport OperationReport { get; }

    /// <summary>Opens the exact original bytes, independently of caller stream lifetime.</summary>
    public Stream OpenOriginalStream() => _original.Open(0, _original.Length);
    /// <summary>Opens the exact reconstructed bytes, independently of caller output lifetime.</summary>
    public Stream OpenRepairedStream() => _repaired.Open(0, _repaired.Length);
    /// <summary>Writes a versioned UTF-8 XML ledger to borrowed output, including exact source/output digests.</summary>
    public PdfOperationReport WriteLedger(Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (!output.CanWrite)
            throw new ArgumentException("Writable borrowed output is required.", nameof(output));
        return PdfFont.Run("WriteRepairLedger", "managed.repair", policy, cancellationToken, context =>
        {
            context.Consume(PdfResourceKind.ExpandedBytes, 65536);
            using var lease = new BorrowedStream(output, false, context);
            using var writer = XmlWriter.Create(lease, new XmlWriterSettings { Encoding = new UTF8Encoding(false), CloseOutput = false });
            writer.WriteStartElement("pdfRepairLedger");
            writer.WriteAttributeString("version", "1");
            writer.WriteAttributeString("originalSha256", OriginalSha256);
            writer.WriteAttributeString("repairedSha256", RepairedSha256);
            foreach (var record in Ledger)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                writer.WriteStartElement("record");
                writer.WriteAttributeString("code", record.Code);
                if (record.ObjectId.HasValue)
                {
                    writer.WriteAttributeString("number", record.ObjectId.Value.Number.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    writer.WriteAttributeString("generation", record.ObjectId.Value.Generation.ToString(System.Globalization.CultureInfo.InvariantCulture));
                }

                if (record.SourceOffset.HasValue)
                    writer.WriteAttributeString("sourceOffset", record.SourceOffset.Value.ToString(System.Globalization.CultureInfo.InvariantCulture));
                writer.WriteString(record.Message);
                writer.WriteEndElement();
            }

            writer.WriteEndElement();
            writer.Flush();
            context.Checkpoint();
        }, PdfCapability.WriteDocument);
    }
}

/// <summary>Passive strict-parser structural observations; syntax failures are never silently repaired.</summary>
public sealed class PdfStructuralInspection
{
    internal PdfStructuralInspection(PdfSyntaxDocument syntax, PdfRepairRecord[] findings, PdfOperationReport report)
    {
        Syntax = syntax;
        Findings = Array.AsReadOnly(findings);
        OperationReport = report;
    }

    /// <summary>Gets the unmodified syntax graph and exact original-source view.</summary>
    public PdfSyntaxDocument Syntax { get; }
    /// <summary>Gets missing-reference, orphan and structural observations.</summary>
    public IReadOnlyList<PdfRepairRecord> Findings { get; }
    /// <summary>Gets diagnostics and shared resource usage.</summary>
    public PdfOperationReport OperationReport { get; }
}
