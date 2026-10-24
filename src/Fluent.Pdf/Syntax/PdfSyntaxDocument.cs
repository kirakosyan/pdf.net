using System.Collections.ObjectModel;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>A cross-reference entry kind.</summary>
public enum PdfCrossReferenceKind
{
    /// <summary>A free object, which shadows older generations.</summary>
    Free,
    /// <summary>An indirect object at a file offset.</summary>
    Offset,
    /// <summary>An object inside an object stream.</summary>
    Compressed
}

/// <summary>An immutable cross-reference observation.</summary>
public sealed class PdfCrossReference
{
    internal PdfCrossReference(PdfObjectId id, PdfCrossReferenceKind kind, long offset, int container = 0, int member = 0)
    {
        Id = id;
        Kind = kind;
        Offset = offset;
        ContainerObjectNumber = container;
        MemberIndex = member;
    }

    /// <summary>Gets the object identity.</summary>
    public PdfObjectId Id { get; }
    /// <summary>Gets the entry kind.</summary>
    public PdfCrossReferenceKind Kind { get; }
    /// <summary>Gets the file offset for an in-use entry, or the next-free object number for a free entry.</summary>
    public long Offset { get; }
    /// <summary>Gets the object stream number for a compressed entry.</summary>
    public int ContainerObjectNumber { get; }
    /// <summary>Gets the zero-based object-stream member index.</summary>
    public int MemberIndex { get; }
}

/// <summary>A revision's trailer and cross-reference entries. Revisions are ordered newest first.</summary>
public sealed class PdfRevision
{
    internal readonly Dictionary<int, PdfCrossReference> Index;
    internal readonly Dictionary<int, PdfValue> Values = new();
    internal PdfRevision(long offset, PdfValue trailer, Dictionary<int, PdfCrossReference> index, bool hybrid)
    {
        StartXref = offset;
        Trailer = trailer;
        Index = index;
        Entries = new ReadOnlyCollection<PdfCrossReference>(index.Values.OrderBy(x => x.Id.Number).ToArray());
        IsHybrid = hybrid;
    }

    /// <summary>Gets this revision's startxref offset.</summary>
    public long StartXref { get; }
    /// <summary>Gets its preserved trailer dictionary or cross-reference stream dictionary.</summary>
    public PdfValue Trailer { get; }
    /// <summary>Gets entries sorted by object number.</summary>
    public IReadOnlyList<PdfCrossReference> Entries { get; }
    /// <summary>Gets whether a classic table was supplemented by an XRefStm.</summary>
    public bool IsHybrid { get; }
}

/// <summary>An eagerly parsed immutable object graph and revision history. It owns no caller stream and executes no actions.</summary>
public sealed class PdfSyntaxDocument
{
    internal PdfByteStore Source { get; }
    internal bool DecryptionApplied { get; }

    private readonly PdfOperationPolicy _policy;
    private readonly Func<PdfObjectId, int, PdfValue>? _lazyResolve;
    internal PdfOperationPolicy ReadPolicy=>_policy;
    internal PdfSyntaxDocument(string version, PdfByteStore source, List<PdfRevision> revisions, PdfOperationPolicy policy, bool decryptionApplied = false, Func<PdfObjectId, int, PdfValue>? lazyResolve = null)
    {
        Version = version;
        Source = source;
        Revisions = revisions.AsReadOnly();
        _policy = policy;
        DecryptionApplied = decryptionApplied;
        _lazyResolve = lazyResolve;
        HasSignatureByteRanges=revisions.Any(revision=>revision.Values.Values.Any(value=>value.HasSignatureByteRanges));
    }

    /// <summary>Gets the header version, from 1.2 through 1.7 or 2.0.</summary>
    public string Version { get; }
    internal bool HasSignatureByteRanges{get;}
    /// <summary>Gets immutable revisions, newest first.</summary>
    public IReadOnlyList<PdfRevision> Revisions { get; }
    /// <summary>Gets the source-read operation report. Internal provider reads use their enclosing document report.</summary>
    public PdfOperationReport? OperationReport { get; internal set; }

    /// <summary>Resolves the exact generation at a revision. A free, missing, zero or mismatched-generation reference resolves to null.</summary>
    public PdfValue Resolve(PdfObjectId id, int revisionIndex = 0)
    {
        if (revisionIndex < 0 || revisionIndex >= Revisions.Count)
            throw new ArgumentOutOfRangeException(nameof(revisionIndex));
        if (id.Number == 0)
            return PdfValue.Null;
        for (int i = revisionIndex; i < Revisions.Count; i++)
            if (Revisions[i].Index.TryGetValue(id.Number, out var entry))
                return entry.Kind == PdfCrossReferenceKind.Free || entry.Id.Generation != id.Generation ? PdfValue.Null : _lazyResolve != null ? _lazyResolve(id, revisionIndex) : Revisions[revisionIndex].Values[id.Number];
        return PdfValue.Null;
    }

    /// <summary>Opens a detached read-only view of the exact original file, including historical revisions.</summary>
    public Stream OpenOriginalStream() => Source.Open(0, Source.Length);
    /// <summary>Decodes an indirect stream eagerly under a fresh policy, resolving filter parameter references at the selected revision.</summary>
    public PdfDecodedData DecodeStream(PdfObjectId id, int revisionIndex = 0, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default,PdfProviderRegistry? providers=null)
    {
        var value = Resolve(id, revisionIndex);
        var context = new PdfOperationContext("DecodeStream", "managed.filters", PdfCapability.ReadDocument, policy ?? _policy, cancellationToken);
        try
        {
            context.Checkpoint();
            if (value.Kind != PdfValueKind.Stream)
                throw PdfStreamFilters.Error(context, "Filter.StreamRequired", "The requested indirect object is not a stream.");
            context.Consume(PdfResourceKind.InputBytes, value.RawStreamLength);
            var resolved = PdfFilterDeclarations.Resolve(value, reference => Resolve(reference, revisionIndex), context);
            using var decoded = PdfFilters.Decode(resolved, context,providers);
            var bytes = PdfByteStore.Read(decoded, context);
            context.EnsureComplete();
            return new PdfDecodedData(bytes, context.Finish(true));
        }
        catch
        {
            if (context.IsActive)
                context.Checkpoint();
            throw;
        }
        finally
        {
            if (context.IsActive)
                context.Finish(false);
            context.DisposeSources();
        }
    }
}

/// <summary>Original managed syntax inspection. No provider, file repair, decryption or document action is invoked.</summary>
public static class PdfSyntax
{
    /// <summary>Reads from the current position of a borrowed stream into a bounded immutable snapshot.</summary>
    public static PdfSyntaxDocument Read(Stream input, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input is null)
            throw new ArgumentNullException(nameof(input));
        if (!input.CanRead)
            throw new ArgumentException("A readable stream is required.", nameof(input));
        var context = new PdfOperationContext("ReadSyntax", "managed.syntax", PdfCapability.ReadDocument, policy ?? PdfOperationPolicy.Default, cancellationToken);
        PdfSyntaxDocument? result = null;
        bool succeeded = false;
        try
        {
            using var lease = new BorrowedStream(input, true, context);
            result = ReadCore(lease, context);
            context.EnsureComplete();
            succeeded = true;
            return result;
        }
        catch
        {
            context.Checkpoint();
            throw;
        }
        finally
        {
            var report = context.Finish(succeeded);
            if (result is not null)
                result.OperationReport = report;
            context.DisposeSources();
        }
    }

    internal static PdfSyntaxDocument ReadCore(Stream input, PdfOperationContext context) => new PdfSyntaxReader(PdfByteStore.Read(input, context), context).Read();
}
