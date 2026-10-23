using System.IO;
using System.Globalization;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>A caller-owned, nonconcurrent range session. Syntax indexes and requested objects are parsed per operation; bounded transport blocks persist until disposal. No whole-file snapshot or document edit model is implied.</summary>
public sealed class PdfProgressiveDocument : IDisposable
{
    private readonly IPdfRangeSource _source;
    private readonly PdfProgressiveOptions _options;
    private readonly PdfOperationPolicy _policy;
    private readonly Dictionary<long, LinkedListNode<Block>> _cache = new();
    private readonly LinkedList<Block> _lru = new();
    private PdfRangeIdentity? _identity;
    private PdfOperationContext? _context;
    private int _busy;
    private bool _disposed;
    private long _cached;
    private sealed class Block
    {
        internal readonly long Offset;
        internal readonly byte[] Bytes;
        internal Block(long offset, byte[] bytes)
        {
            Offset = offset;
            Bytes = bytes;
        }
    }

    /// <summary>Creates a session without reading or taking ownership of the source. Every operation rechecks source authorization and revision, including warm cache operations.</summary>
    public PdfProgressiveDocument(IPdfRangeSource source, PdfProgressiveOptions? options = null, PdfOperationPolicy? policy = null)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _options = options ?? new();
        _policy = policy ?? PdfOperationPolicy.Default;
    }

    /// <summary>Reads only the indexes, catalog and page-tree paths needed for this page. Resources and content payloads are fetched only when separately requested. Declared subtree counts guide traversal; this is not full document validation.</summary>
    public PdfProgressivePage ReadPage(int pageIndex, CancellationToken cancellationToken = default)
    {
        if (pageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        PdfProgressivePage? result = null;
        var report = Run("ReadProgressivePage", cancellationToken, (graph, c) =>
        {
            PdfValue Follow(PdfValue v) => ManagedPdfProvider.Resolve(graph, v, c);
            var trailer = graph.Revisions[0].Trailer;
            var catalog = Follow(Get(trailer, "Root"));
            if (!Named(catalog, "Catalog"))
                throw Error(c, "Catalog", "A catalog dictionary is required.");
            var root = Get(catalog, "Pages");
            if (root.Kind != PdfValueKind.Reference)
                throw Error(c, "PageTree", "An indirect Pages root is required.");
            var rootNode = Follow(root);
            int total = Count(rootNode, c);
            if (pageIndex >= total)
                throw Error(c, "PageIndex", "The requested page is outside the declared page inventory.");
            var inherited = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
            var visited = new HashSet<PdfObjectId>();
            int wanted = pageIndex;
            var page = Visit(root, null, inherited);
            result = new(pageIndex, total, page.Id, page.Value);
            (PdfObjectId Id, PdfValue Value) Visit(PdfValue reference, PdfObjectId? parent, Dictionary<string, PdfValue> attributes)
            {
                using var depth = c.EnterScope(PdfResourceKind.Depth);
                c.Checkpoint();
                c.Consume(PdfResourceKind.ExpandedBytes, 512);
                if (reference.Kind != PdfValueKind.Reference || !visited.Add(reference.ReferenceId))
                    throw Error(c, "PageTreeCycle", "The visited page-tree path contains a cycle or duplicate identity.");
                var node = Follow(reference);
                if (node.Kind != PdfValueKind.Dictionary)
                    throw Error(c, "PageTree", "A page-tree node must be a dictionary.");
                var declaredParent = Get(node, "Parent");
                if (parent.HasValue && (declaredParent.Kind != PdfValueKind.Reference || declaredParent.ReferenceId != parent.Value) || !parent.HasValue && declaredParent.Kind != PdfValueKind.Null)
                    throw Error(c, "PageParent", "A visited page-tree Parent disagrees with its owner.");
                var next = new Dictionary<string, PdfValue>(attributes, StringComparer.Ordinal);
                foreach (string key in new[]
                {
                    "Resources",
                    "MediaBox",
                    "CropBox",
                    "Rotate"
                }

                )
                    if (node.Entries.TryGetValue(key, out var value) && Follow(value).Kind != PdfValueKind.Null)
                        next[key] = value;
                if (Named(node, "Page"))
                {
                    if (wanted != 0 || !next.ContainsKey("MediaBox"))
                        throw Error(c, "PageTree", "A visited page lacks required geometry or disagrees with subtree counts.");
                    var effective = PdfWriterCore.CopyDictionary(node.Entries, c);
                    foreach (var pair in next)
                        effective[pair.Key] = pair.Value;
                    return (reference.ReferenceId, PdfValue.OwnedDictionary(effective));
                }

                int expected = Count(node, c);
                var kids = Follow(Get(node, "Kids"));
                if (kids.Kind != PdfValueKind.Array || kids.Items.Count == 0)
                    throw Error(c, "PageTree", "A nonempty indirect child array is required.");
                var children = new List<(PdfValue Reference, int Count)>();
                var siblings = new HashSet<PdfObjectId>();
                long sum = 0;
                foreach (var child in kids.Items)
                {
                    c.Checkpoint();
                    c.Consume(PdfResourceKind.ExpandedBytes, 80);
                    if (child.Kind != PdfValueKind.Reference || !siblings.Add(child.ReferenceId))
                        throw Error(c, "PageTree", "Page children must have distinct indirect identities.");
                    var value = Follow(child);
                    int count = Named(value, "Page") ? 1 : Count(value, c);
                    sum += count;
                    children.Add((child, count));
                }

                if (sum != expected)
                    throw Error(c, "PageCount", "Immediate child counts disagree with a visited Pages node.");
                foreach (var child in children)
                    if (wanted < child.Count)
                        return Visit(child.Reference, reference.ReferenceId, next);
                    else
                        wanted -= child.Count;
                throw Error(c, "PageCount", "No child owns the requested page.");
            }
        });
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Reads one indirect object lazily, retaining references to dependencies. Raw stream bytes are captured into a detached immutable store before returning.</summary>
    public PdfValue ReadObject(PdfObjectId id, CancellationToken cancellationToken = default)
    {
        PdfValue result = PdfValue.Null;
        Run("ReadProgressiveObject", cancellationToken, (graph, c) =>
        {
            var value = graph.Resolve(id);
            if (value.Kind != PdfValueKind.Stream)
            {
                result = value;
                return;
            }

            using var raw = value.OpenRawStream();
            using var lease = new BorrowedStream(raw, true, c, null);
            var captured = PdfByteStore.Read(lease, c);
            result = PdfValue.StreamValue(value, captured, 0, captured.Length);
        });
        return result;
    }

    /// <summary>Decodes one stream and its declared filter dependencies under the same range/read/decode budget, returning detached bytes.</summary>
    public PdfDecodedData DecodeStream(PdfObjectId id, PdfProviderRegistry? providers = null, CancellationToken cancellationToken = default)
    {
        PdfByteStore? bytes = null;
        var report = Run("DecodeProgressiveStream", cancellationToken, (graph, c) =>
        {
            var value = graph.Resolve(id);
            if (value.Kind != PdfValueKind.Stream)
                throw Error(c, "StreamRequired", "The requested indirect object is not a stream.");
            var resolved = PdfFilterDeclarations.Resolve(value, reference => graph.Resolve(reference), c);
            using var decoded = PdfFilters.Decode(resolved, c, providers);
            bytes = PdfByteStore.Read(decoded, c);
        });
        return new(bytes!, report);
    }

    /// <summary>Inspects the first linearization dictionary through ranges. Its bounds are observations, not complete hint-table validation.</summary>
    public PdfLinearizationInfo? InspectLinearization(CancellationToken cancellationToken = default)
    {
        PdfLinearizationInfo? result = null;
        Run("InspectProgressiveLinearization", cancellationToken, (graph, c) => result = PdfLinearization.InspectCore(graph.Source, c));
        return result;
    }

    /// <summary>Clears cached blocks without disposing the caller's source. Concurrent operations/disposal are rejected.</summary>
    public void Dispose()
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            throw new InvalidOperationException("A progressive session cannot be used concurrently.");
        try
        {
            _disposed = true;
            Clear();
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    private PdfOperationReport Run(string operation, CancellationToken token, Action<PdfSyntaxDocument, PdfOperationContext> action)
    {
        if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0)
            throw new InvalidOperationException("A progressive session cannot be used concurrently.");
        try
        {
            if (_disposed)
                throw new ObjectDisposedException(nameof(PdfProgressiveDocument));
            return PdfFont.Run(operation, "managed.progressive", _policy, token, c =>
            {
                _context = c;
                try
                {
                    c.Checkpoint();
                    var current = Identity(c);
                    if (_identity != null && !Same(_identity, current))
                    {
                        Clear();
                        throw Error(c, "SourceChanged", "The range source revision changed; create a new session.");
                    }

                    _identity = current;
                    c.Consume(PdfResourceKind.ExpandedBytes, _cached + _cache.Count * 128L);
                    var bytes = new PdfByteStore(current.Length, At);
                    var graph = new PdfSyntaxReader(bytes, c).Read(lazy: true);
                    if (graph.Revisions.Any(r => Get(r.Trailer, "Encrypt").Kind != PdfValueKind.Null))
                        throw Error(c, "EncryptionUnavailable", "Progressive decryption requires a separately qualified range-aware security provider.");
                    action(graph, c);
                    if (!Same(current, Identity(c)))
                    {
                        Clear();
                        throw Error(c, "SourceChanged", "The range source changed during the operation.");
                    }
                }
                catch
                {
                    Clear();
                    c.Checkpoint();
                    throw;
                }
                finally
                {
                    _context = null;
                }
            }, PdfCapability.ReadDocument);
        }
        finally
        {
            Volatile.Write(ref _busy, 0);
        }
    }

    private int At(long position)
    {
        var c = _context ?? throw new ObjectDisposedException(nameof(PdfProgressiveDocument));
        c.Checkpoint();
        long start = position / _options.BlockSize * _options.BlockSize;
        if (_cache.TryGetValue(start, out var existing))
        {
            _lru.Remove(existing);
            _lru.AddFirst(existing);
            return existing.Value.Bytes[(int)(position - start)];
        }

        int size = (int)Math.Min(_options.BlockSize, _identity!.Length - start);
        while (_cached > _options.MaximumCacheBytes - size)
        {
            var last = _lru.Last!;
            _cache.Remove(last.Value.Offset);
            _cached -= last.Value.Bytes.Length;
            _lru.RemoveLast();
        }

        c.Consume(PdfResourceKind.ExpandedBytes, size + 128L);
        c.Consume(PdfResourceKind.Objects, 1);
        var data = new byte[size];
        try
        {
            using var response = _source.OpenRange(start, size, _identity, c);
            if (response == null || response.Offset != start || response.Length != size || !Same(response.Identity, _identity))
                throw Error(c, "ResponseBinding", "The range response bounds or source identity do not match the request.");
            using var lease = new BorrowedStream(response.Body, true, c);
            int offset = 0;
            while (offset < size)
            {
                int read = lease.Read(data, offset, size - offset);
                if (read == 0)
                    throw Error(c, "TruncatedResponse", "A range response ended before its declared length.");
                offset += read;
            }

            if (lease.ReadByte() != -1)
                throw Error(c, "ResponseLength", "A range response contains bytes beyond its declared length.");
            if (!Same(_identity, Identity(c)))
                throw Error(c, "SourceChanged", "The source revision changed during a range read.");
        }
        catch
        {
            c.Checkpoint();
            throw Error(c, "ProviderFailed", "The configured authenticated range provider failed.");
        }

        var node = _lru.AddFirst(new Block(start, data));
        _cache.Add(start, node);
        _cached += size;
        return data[(int)(position - start)];
    }

    private PdfRangeIdentity Identity(PdfOperationContext c)
    {
        try
        {
            var result = _source.GetIdentity(c);
            c.Checkpoint();
            return result ?? throw Error(c, "Identity", "The range provider returned no source identity.");
        }
        catch
        {
            c.Checkpoint();
            throw Error(c, "ProviderFailed", "The configured authenticated range provider failed.");
        }
    }

    private void Clear()
    {
        _cache.Clear();
        _lru.Clear();
        _cached = 0;
    }

    private static bool Same(PdfRangeIdentity a, PdfRangeIdentity b) => a.Length == b.Length && a.Version == b.Version;
    private static PdfValue Get(PdfValue value, string key) => (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream) && value.Entries.TryGetValue(key, out var item) ? item : PdfValue.Null;
    private static bool Named(PdfValue value, string name) => value.Kind == PdfValueKind.Dictionary && Get(value, "Type").Kind == PdfValueKind.Name && Get(value, "Type").NameValue == name;
    private static int Count(PdfValue value, PdfOperationContext c)
    {
        var count = Get(value, "Count");
        if (!Named(value, "Pages") || count.Kind != PdfValueKind.Number || !int.TryParse(count.NumberText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int pages) || pages <= 0)
            throw Error(c, "PageCount", "A Pages node requires a positive supported integer Count.");
        return pages;
    }

    internal static PdfException Error(PdfOperationContext c, string code, string message)
    {
        var diagnostic = new PdfDiagnostic("Progressive." + code, message, PdfCapability.ReadDocument, "managed.progressive");
        c.Report(diagnostic);
        return new(diagnostic);
    }
}
