using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Passive engineering content families; no record implies execution or rendering support.</summary>
public enum PdfEngineeringContentKind
{
    /// <summary>Opaque Universal3D stream; preserve-only.</summary>
    U3D,
    /// <summary>Opaque PRC stream; preserve-only.</summary>
    Prc,
    /// <summary>Other declared3D stream; preserve-only.</summary>
    Other3D,
    /// <summary>3D annotation dictionary; preserve-only.</summary>
    ThreeDAnnotation,
    /// <summary>Rich-media dictionary; preserve-only.</summary>
    RichMedia,
    /// <summary>Geospatial dictionary/metadata; preserve-only.</summary>
    Geospatial,
    /// <summary>Encoded embedded-file payload; preserve-only.</summary>
    EmbeddedFile
}

/// <summary>Immutable physical graph observation without loading a media engine, decoding payloads or running actions.</summary>
public sealed class PdfEngineeringRecord
{
    internal PdfEngineeringRecord(PdfEngineeringContentKind kind, string location, PdfValue definition, string? digest)
    {
        Kind = kind;
        Location = location;
        Definition = definition;
        EncodedSha256 = digest;
    }

    /// <summary>Gets passive content family.</summary>
    public PdfEngineeringContentKind Kind { get; }
    /// <summary>Gets physical object identity and direct key/index provenance.</summary>
    public string Location { get; }
    /// <summary>Gets the detached immutable source definition. Reading a stream is explicit; no payload executes.</summary>
    public PdfValue Definition { get; }
    /// <summary>Gets SHA256 of exact encoded stream bytes, or null for a dictionary observation. This is not semantic/decoded equivalence.</summary>
    public string? EncodedSha256 { get; }
}

/// <summary>Read-only passive inventory; zero records never establishes PDF/E conformance.</summary>
public sealed class PdfEngineeringInspection
{
    internal PdfEngineeringInspection(List<PdfEngineeringRecord> records, PdfOperationReport report)
    {
        Records = Array.AsReadOnly(records.ToArray());
        OperationReport = report;
    }

    /// <summary>Gets immutable content/provenance observations.</summary>
    public IReadOnlyList<PdfEngineeringRecord> Records { get; }
    /// <summary>Gets complete shared operation diagnostics.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Exact-byte preserve-only evidence; not a signature, media rendering or conformance verdict.</summary>
public sealed class PdfEngineeringPreservation
{
    internal PdfEngineeringPreservation(string digest, long length, PdfOperationReport report)
    {
        Sha256 = digest;
        Length = length;
        OperationReport = report;
    }

    /// <summary>Gets the exact input/output SHA256.</summary>
    public string Sha256 { get; }
    /// <summary>Gets the exact retained file length.</summary>
    public long Length { get; }
    /// <summary>Gets completed capture/parse/copy diagnostics.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Passive cross-platform advanced-content inspection and exact-byte preservation. No U3D/PRC/media/geospatial renderer is bundled.</summary>
public static class PdfEngineering
{
    /// <summary>Inspects every active physical object/direct value, including unattached payloads. Reads encoded bytes for hashes without decoding/executing them; snapshot and graph remain unchanged.</summary>
    public static PdfEngineeringInspection Inspect(PdfDocumentSnapshot source, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        List<PdfEngineeringRecord>? records = null;
        var report = PdfFont.Run("InspectEngineering", "managed.engineering", policy ?? source.Options.OperationPolicy, cancellationToken, c => records = ReadCore(source, c), PdfCapability.ReadDocument);
        return new(records!, report);
    }

    /// <summary>Captures and validates a nonempty PDF then copies the exact original bytes, retaining all payloads, associations/history and signature bytes. This does not assert signature validity or PDF/E conformance.</summary>
    public static PdfEngineeringPreservation Preserve(Stream input, Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("Readable borrowed PDF input is required.", nameof(input));
        if (output == null || !output.CanWrite || ReferenceEquals(input, output))
            throw new ArgumentException("A distinct writable borrowed output is required.", nameof(output));
        string digest = "";
        long length = 0;
        var report = PdfFont.Run("PreserveEngineering", "managed.engineering", policy, cancellationToken, c =>
        {
            var bytes = PdfProfileIo.Capture(input, c);
            using var doc = PdfPageOrganization.Read(bytes, 10000, c);
            if (doc.Pages.Count == 0)
                throw PdfProfileConversions.Error(c, "EmptyInput", "A nonempty valid PDF is required.");
            digest = PdfArchival.Digest(bytes, c);
            length = bytes.Length;
            PdfProfileIo.Copy(bytes, output, c);
        }, PdfCapability.ReadDocument);
        return new(digest, length, report);
    }

    internal static List<PdfEngineeringRecord> ReadCore(PdfDocumentSnapshot source, PdfOperationContext c)
    {
        using var graph = new PdfGraphEdit(source, new(), c);
        var records = new List<PdfEngineeringRecord>();
        foreach (var id in graph.Edit.ActiveIds(c))
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.ExpandedBytes, 128);
            Visit(graph.Edit.Resolve(id), id.Number + " " + id.Generation + " obj");
        }

        return records;
        void Visit(PdfValue value, string location)
        {
            using var scope = c.EnterScope(PdfResourceKind.Depth);
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Array)
            {
                for (int n = 0; n < value.Items.Count; n++)
                {
                    c.Consume(PdfResourceKind.ExpandedBytes, location.Length * 2L + 64);
                    Visit(value.Items[n], location + "[" + n + "]");
                }

                return;
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return;
            string Name(string key)
            {
                var v = graph.Get(value, key);
                return v.Kind == PdfValueKind.Name ? v.NameValue : "";
            }

            string type = Name("Type"), subtype = Name("Subtype");
            PdfEngineeringContentKind? kind = type == "3D" ? (subtype == "U3D" ? PdfEngineeringContentKind.U3D : subtype == "PRC" ? PdfEngineeringContentKind.Prc : PdfEngineeringContentKind.Other3D) : type == "EmbeddedFile" ? PdfEngineeringContentKind.EmbeddedFile : subtype == "3D" ? PdfEngineeringContentKind.ThreeDAnnotation : subtype == "RichMedia" || value.Entries.ContainsKey("RichMediaContent") ? PdfEngineeringContentKind.RichMedia : subtype == "GEO" || type == "Viewport" || value.Entries.ContainsKey("LGIDict") ? PdfEngineeringContentKind.Geospatial : null;
            if (kind.HasValue)
            {
                c.Consume(PdfResourceKind.Objects, 1);
                c.Consume(PdfResourceKind.ExpandedBytes, location.Length * 2L + 512);
                string? digest = null;
                if (value.Kind == PdfValueKind.Stream)
                {
                    c.Consume(PdfResourceKind.ExpandedBytes, 8192);
                    var buffer = new byte[8192];
                    using var hash = SHA256.Create();
                    using var raw = value.OpenRawStream();
                    using var lease = new BorrowedStream(raw, true, c);
                    int n;
                    while ((n = lease.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        c.Checkpoint();
                        hash.TransformBlock(buffer, 0, n, buffer, 0);
                    }

                    hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                    digest = BitConverter.ToString(hash.Hash!).Replace("-", "").ToLowerInvariant();
                }

                records.Add(new(kind.Value, location, value, digest));
            }

            foreach (var pair in value.Entries)
            {
                c.Consume(PdfResourceKind.ExpandedBytes, location.Length * 2L + pair.Key.Length * 2L + 64);
                Visit(pair.Value, location + "/" + pair.Key);
            }
        }
    }
}
