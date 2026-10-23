using System.Collections.ObjectModel;
using System.Globalization;
using System.Threading;

namespace Fluent.Pdf.Intelligence;
/// <summary>Explicit tenant/document/exact PDF revision. Identity is supplied by the trusted host, never by document/model output.</summary>
public sealed class PdfRetrievalScope
{
    /// <summary>Captures three separately compared immutable scope components.</summary>
    public PdfRetrievalScope(string tenantId, string documentId, string documentSha256)
    {
        PdfIntelligenceModel.Identity(tenantId, nameof(tenantId));
        PdfIntelligenceModel.Identity(documentId, nameof(documentId));
        Hash(documentSha256, nameof(documentSha256));
        TenantId = tenantId;
        DocumentId = documentId;
        DocumentSha256 = documentSha256;
    }

    /// <summary>Gets trusted host tenant identity.</summary>
    public string TenantId { get; }
    /// <summary>Gets trusted host document identity.</summary>
    public string DocumentId { get; }
    /// <summary>Gets the exact complete source PDF SHA256.</summary>
    public string DocumentSha256 { get; }
    internal string Key => TenantId.Length.ToString(CultureInfo.InvariantCulture) + ":" + TenantId + DocumentId.Length.ToString(CultureInfo.InvariantCulture) + ":" + DocumentId + DocumentSha256;

    internal static void Hash(string value, string name)
    {
        if (value == null || value.Length != 64 || value.Any(ch => !(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f')))
            throw new ArgumentException("A lowercase SHA256 is required.", name);
    }
}

/// <summary>Application-authorized immutable permission provenance. This is not authentication or inferred PDF permission authority.</summary>
public sealed class PdfRetrievalPermissions
{
    /// <summary>Captures explicit policy/version evidence, one writer and 1..128 readers. Host code must authenticate principals and establish source extraction authority before capture.</summary>
    public PdfRetrievalPermissions(string policyVersion, string writer, IEnumerable<string> readers)
    {
        PdfIntelligenceModel.Identity(policyVersion, nameof(policyVersion));
        PdfIntelligenceModel.Identity(writer, nameof(writer));
        if (readers == null)
            throw new ArgumentNullException(nameof(readers));
        var list = new List<string>();
        foreach (var reader in readers)
        {
            PdfIntelligenceModel.Identity(reader, nameof(readers));
            if (list.Count == 128 || list.Contains(reader))
                throw new ArgumentException("Select bounded distinct authorized readers.", nameof(readers));
            list.Add(reader);
        }

        if (list.Count == 0)
            throw new ArgumentException("Explicit readers are required.", nameof(readers));
        list.Sort(StringComparer.Ordinal);
        PolicyVersion = policyVersion;
        Writer = writer;
        Readers = new ReadOnlyCollection<string>(list.ToArray());
    }

    /// <summary>Gets host policy/version evidence, carried with every stored index.</summary>
    public string PolicyVersion { get; }
    /// <summary>Gets the explicit writer principal.</summary>
    public string Writer { get; }
    /// <summary>Gets the immutable authorized readers.</summary>
    public IReadOnlyList<string> Readers { get; }

    internal bool Matches(PdfRetrievalPermissions other) => PolicyVersion == other.PolicyVersion && Writer == other.Writer && Readers.SequenceEqual(other.Readers);
}

/// <summary>Authorized index state used for compare-and-swap writes and consistent query model selection.</summary>
public sealed class PdfVectorIndexInfo
{
    /// <summary>Captures authoritative stored state; version zero denotes owner-authorized creation only.</summary>
    public PdfVectorIndexInfo(long version, PdfEmbeddingModel? model, PdfRetrievalPermissions? permissions)
    {
        if (version < 0 || version == 0 && (model != null || permissions != null) || version > 0 && (model == null || permissions == null))
            throw new ArgumentException("Version and index metadata disagree.", nameof(version));
        Version = version;
        Model = model;
        Permissions = permissions;
    }

    /// <summary>Gets monotonic store generation, used to prevent lost concurrent writes.</summary>
    public long Version { get; }
    /// <summary>Gets stored pinned model or null only for authorized creation.</summary>
    public PdfEmbeddingModel? Model { get; }
    /// <summary>Gets authoritative existing permissions or null only for authorized creation.</summary>
    public PdfRetrievalPermissions? Permissions { get; }
}

/// <summary>Complete immutable revision-bound embedding index prepared before an atomic store replacement.</summary>
public sealed class PdfVectorIndex
{
    internal PdfVectorIndex(PdfRetrievalScope scope, PdfRetrievalPermissions permissions, PdfSemanticChunkResult capture, PdfEmbeddingModel model, PdfEmbeddingResponse embeddings)
    {
        Scope = scope;
        Permissions = permissions;
        Capture = capture;
        Model = model;
        Embeddings = embeddings;
    }

    /// <summary>Gets exact tenant/document/revision identity.</summary>
    public PdfRetrievalScope Scope { get; }
    /// <summary>Gets explicit application permission provenance.</summary>
    public PdfRetrievalPermissions Permissions { get; }
    /// <summary>Gets original chunk/page/layout/mapping provenance.</summary>
    public PdfSemanticChunkResult Capture { get; }
    /// <summary>Gets exact model/version/dimension identity.</summary>
    public PdfEmbeddingModel Model { get; }
    /// <summary>Gets ordered immutable vectors and exact scoped request binding.</summary>
    public PdfEmbeddingResponse Embeddings { get; }
}

/// <summary>Original optional vector-store boundary. Implementations must authorize before returning metadata, enforce authoritative ACLs at every operation and commit atomically with generation checks. Remote store authority must be separately explicit in the host.</summary>
public interface IPdfVectorStore
{
    /// <summary>Authorizes read/write before any embedding dispatch. Only an explicitly configured administrator may authorize absent-index creation.</summary>
    PdfVectorIndexInfo Authorize(PdfRetrievalScope scope, string principal, bool write, PdfOperationContext context);
    /// <summary>Reauthorizes and atomically replaces one exact scope if its generation matches. Existing permission mutation must reject. No partial batch visibility.</summary>
    long Replace(PdfVectorIndex index, string principal, long expectedVersion, PdfOperationContext context);
    /// <summary>Reauthorizes and removes exactly one generation atomically. A removed scope cannot be searched or recreated by ordinary writers/readers.</summary>
    void Remove(PdfRetrievalScope scope, string principal, long expectedVersion, PdfOperationContext context);
    /// <summary>Reauthorizes, verifies generation/model/dimensions, and returns bounded cosine-ranked matches from this exact scope only.</summary>
    IReadOnlyList<PdfVectorMatch> Search(PdfRetrievalScope scope, string principal, long expectedVersion, PdfEmbeddingModel model, PdfEmbeddingVector query, int maximumResults, PdfOperationContext context);
}

/// <summary>One match with exact source and permission provenance; similarity is not a factual confidence measure.</summary>
public sealed class PdfVectorMatch
{
    /// <summary>Captures adapter output; SDK independently checks the requested scope and score bounds.</summary>
    public PdfVectorMatch(PdfVectorIndex index, int chunkIndex, double score)
    {
        Index = index ?? throw new ArgumentNullException(nameof(index));
        if (chunkIndex < 0 || chunkIndex >= index.Capture.Chunks.Count)
            throw new ArgumentOutOfRangeException(nameof(chunkIndex));
        if (double.IsNaN(score) || score < -1 || score > 1)
            throw new ArgumentOutOfRangeException(nameof(score));
        Chunk = index.Capture.Chunks[chunkIndex];
        Score = score;
    }

    /// <summary>Gets the authoritative matched index, scope, model and ACL.</summary>
    public PdfVectorIndex Index { get; }
    /// <summary>Gets exact chunk text, page, offsets, mappings and inferred table identity.</summary>
    public PdfSemanticChunk Chunk { get; }
    /// <summary>Gets cosine score in [-1,1], not calibrated relevance.</summary>
    public double Score { get; }
}

/// <summary>Bounded original managed cosine store. No global cache, model/network calls, serialization or implicit scope expansion. Host authenticates each principal.</summary>
public sealed class PdfMemoryVectorStore : IPdfVectorStore
{
    private sealed class Entry
    {
        internal PdfVectorIndex Index = null!;
        internal long Version, Bytes;
    }

    private readonly object _gate = new();
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);
    private readonly string _administrator;
    private readonly int _maximumDocuments, _maximumRecords;
    private readonly long _maximumResidentBytes;
    private long _generation, _bytes;
    private int _records;
    /// <summary>Creates a private store with explicit administrator, finite document/record ceilings and estimated retained-byte ceiling. Separate stores may be used per tenant.</summary>
    public PdfMemoryVectorStore(string administrator, int maximumDocuments = 128, int maximumRecords = 65536, long maximumResidentBytes = 67108864)
    {
        PdfIntelligenceModel.Identity(administrator, nameof(administrator));
        if (maximumDocuments < 1 || maximumDocuments > 65536 || maximumRecords < 1 || maximumRecords > 1000000 || maximumResidentBytes < 1024 || maximumResidentBytes > 1073741824)
            throw new ArgumentOutOfRangeException(nameof(maximumDocuments));
        _administrator = administrator;
        _maximumDocuments = maximumDocuments;
        _maximumRecords = maximumRecords;
        _maximumResidentBytes = maximumResidentBytes;
    }

    /// <summary>Checks stored ACL under a private lock. Absent scopes are disclosed only to the administrator for creation.</summary>
    public PdfVectorIndexInfo Authorize(PdfRetrievalScope scope, string principal, bool write, PdfOperationContext context)
    {
        Arguments(scope, principal, context);
        lock (_gate)
        {
            var entry = Allowed(scope, principal, write, context);
            return entry == null ? new(0, null, null) : new(entry.Version, entry.Index.Model, entry.Index.Permissions);
        }
    }

    private Entry? Allowed(PdfRetrievalScope scope, string principal, bool write, PdfOperationContext c)
    {
        c.Checkpoint();
        _entries.TryGetValue(scope.Key, out var entry);
        if (entry == null ? !write || principal != _administrator : principal != _administrator && (write ? principal != entry.Index.Permissions.Writer : !entry.Index.Permissions.Readers.Contains(principal)))
            throw PdfSemanticChunks.Error(c, "ScopeDenied", "The authenticated host principal has no authority for this exact scope and operation.");
        return entry;
    }

    /// <summary>Atomically commits a complete index after ACL, exact-revision, capacity and generation checks. Cancellation/failure before commit preserves the old index.</summary>
    public long Replace(PdfVectorIndex index, string principal, long expectedVersion, PdfOperationContext context)
    {
        if (index == null)
            throw new ArgumentNullException(nameof(index));
        Arguments(index.Scope, principal, context);
        if (expectedVersion < 0)
            throw new ArgumentOutOfRangeException(nameof(expectedVersion));
        long bytes = Size(index, context);
        lock (_gate)
        {
            var old = Allowed(index.Scope, principal, true, context);
            if ((old?.Version ?? 0) != expectedVersion)
                throw PdfSemanticChunks.Error(context, "Generation", "The index changed after authorization; no stale write was committed.");
            if (old != null && !old.Index.Permissions.Matches(index.Permissions))
                throw PdfSemanticChunks.Error(context, "PermissionMutation", "Existing index permissions require a separate authorized administrative lifecycle.");
            if (index.Scope.DocumentSha256 != index.Capture.DocumentSha256 || index.Capture.Chunks.Count == 0 || index.Embeddings.Vectors.Count != index.Capture.Chunks.Count || index.Embeddings.ExactModelVersion != index.Model.Model.ExactModelVersion || index.Embeddings.Vectors.Any(vector => vector.Values.Count != index.Model.Dimensions))
                throw PdfSemanticChunks.Error(context, "IndexBinding", "The complete index metadata disagrees with its exact source/model/vectors.");
            long retained = _bytes - (old?.Bytes ?? 0) + bytes;
            int count = _records - (old?.Index.Capture.Chunks.Count ?? 0) + index.Capture.Chunks.Count;
            if (old == null && _entries.Count == _maximumDocuments || count > _maximumRecords || retained > _maximumResidentBytes || _generation == long.MaxValue)
                throw PdfSemanticChunks.Error(context, "StoreCapacity", "The private store reached a finite resident/document/record/generation ceiling.");
            context.Consume(PdfResourceKind.ExpandedBytes, bytes);
            context.Consume(PdfResourceKind.Objects, index.Capture.Chunks.Count + 1);
            context.Checkpoint();
            long version = _generation + 1;
            var entry = new Entry
            {
                Index = index,
                Version = version,
                Bytes = bytes
            };
            _entries[index.Scope.Key] = entry;
            _generation = version;
            _bytes = retained;
            _records = count;
            return version;
        }
    }

    /// <summary>Atomically revokes an exact index after writer/administrator and generation checks. Subsequent access fails before embedding.</summary>
    public void Remove(PdfRetrievalScope scope, string principal, long expectedVersion, PdfOperationContext context)
    {
        Arguments(scope, principal, context);
        lock (_gate)
        {
            var entry = Allowed(scope, principal, true, context);
            if (entry == null || entry.Version != expectedVersion)
                throw PdfSemanticChunks.Error(context, "Generation", "The exact removable index generation is absent or changed.");
            context.Checkpoint();
            _entries.Remove(scope.Key);
            _bytes -= entry.Bytes;
            _records -= entry.Index.Capture.Chunks.Count;
        }
    }

    private static long Size(PdfVectorIndex index, PdfOperationContext c)
    {
        long size = 65536 + index.Capture.PageIndices.Count * 8L + index.Capture.OperationReport.Diagnostics.Count * 12288L;
        foreach (var chunk in index.Capture.Chunks)
        {
            c.Checkpoint();
            size = checked(size + 2048 + chunk.Text.Length * 4L + chunk.Characters.Sum(character => 1024L + character.FontName.Length * 2L + character.Text.Length * 2L) + index.Model.Dimensions * 16L);
        }

        return size;
    }

    /// <summary>Scores only authorized exact-revision records using scaled compensated cosine sums and deterministic chunk-order ties. A concurrent replacement rejects the stale search.</summary>
    public IReadOnlyList<PdfVectorMatch> Search(PdfRetrievalScope scope, string principal, long expectedVersion, PdfEmbeddingModel model, PdfEmbeddingVector query, int maximumResults, PdfOperationContext context)
    {
        Arguments(scope, principal, context);
        if (model == null)
            throw new ArgumentNullException(nameof(model));
        if (query == null)
            throw new ArgumentNullException(nameof(query));
        if (maximumResults < 1 || maximumResults > 1000)
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        Entry entry;
        lock (_gate)
        {
            entry = Allowed(scope, principal, false, context)!;
            if (entry.Version != expectedVersion || !entry.Index.Model.Matches(model) || query.Values.Count != model.Dimensions)
                throw PdfSemanticChunks.Error(context, "Generation", "The query generation/model/dimensions disagree with the authorized index.");
        }

        context.Consume(PdfResourceKind.ExpandedBytes, entry.Bytes + maximumResults * 128L);
        context.Consume(PdfResourceKind.Objects, entry.Index.Capture.Chunks.Count);
        var best = new List<PdfVectorMatch>();
        for (int i = 0; i < entry.Index.Capture.Chunks.Count; i++)
        {
            context.Checkpoint();
            double score = Cosine(query.Values, entry.Index.Embeddings.Vectors[i].Values, context);
            int position = 0;
            while (position < best.Count && best[position].Score >= score)
                position++;
            if (position < maximumResults)
            {
                best.Insert(position, new(entry.Index, i, score));
                if (best.Count > maximumResults)
                    best.RemoveAt(best.Count - 1);
            }
        }

        lock (_gate)
        {
            var current = Allowed(scope, principal, false, context);
            if (!ReferenceEquals(current, entry))
                throw PdfSemanticChunks.Error(context, "Generation", "The index changed during scoring; no stale results were returned.");
            context.Checkpoint();
            return new ReadOnlyCollection<PdfVectorMatch>(best);
        }
    }

    private static void Arguments(PdfRetrievalScope scope, string principal, PdfOperationContext c)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        PdfIntelligenceModel.Identity(principal, nameof(principal));
        if (c == null)
            throw new ArgumentNullException(nameof(c));
        c.Consume(PdfResourceKind.ExpandedBytes, 4096);
        c.Consume(PdfResourceKind.Objects, 1);
        c.Checkpoint();
    }

    internal static double Cosine(IReadOnlyList<double> a, IReadOnlyList<double> b, PdfOperationContext c)
    {
        double ma = 0, mb = 0;
        for (int i = 0; i < a.Count; i++)
        {
            c.Checkpoint();
            ma = Math.Max(ma, Math.Abs(a[i]));
            mb = Math.Max(mb, Math.Abs(b[i]));
        }

        double aa = 0, bb = 0, ab = 0, ca = 0, cb = 0, cc = 0;
        void Add(ref double sum, ref double correction, double value)
        {
            double adjusted = value - correction, next = sum + adjusted;
            correction = next - sum - adjusted;
            sum = next;
        }

        for (int i = 0; i < a.Count; i++)
        {
            c.Checkpoint();
            double x = a[i] / ma, y = b[i] / mb;
            Add(ref aa, ref ca, x * x);
            Add(ref bb, ref cb, y * y);
            Add(ref ab, ref cc, x * y);
        }

        return Math.Max(-1, Math.Min(1, ab / Math.Sqrt(aa) / Math.Sqrt(bb)));
    }
}

/// <summary>Complete index commit evidence.</summary>
public sealed class PdfVectorIndexResult
{
    internal PdfVectorIndexResult(long version, PdfVectorIndex index, PdfOperationReport report)
    {
        Version = version;
        Index = index;
        OperationReport = report;
    }

    /// <summary>Gets committed monotonic store generation.</summary>
    public long Version { get; }
    /// <summary>Gets exact committed source/model/permission provenance.</summary>
    public PdfVectorIndex Index { get; }
    /// <summary>Gets shared embedding/commit diagnostics and accounting.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Authorized exact-scope search evidence.</summary>
public sealed class PdfVectorSearchResult
{
    internal PdfVectorSearchResult(IReadOnlyList<PdfVectorMatch> matches, PdfOperationReport report)
    {
        Matches = matches;
        OperationReport = report;
    }

    /// <summary>Gets bounded ranked authorized matches.</summary>
    public IReadOnlyList<PdfVectorMatch> Matches { get; }
    /// <summary>Gets shared authorization/query-embedding/search accounting.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Scoped retrieval orchestration. Capture is a separate explicit operation; indexing and search each share one model/store budget and reauthorize at commit/read.</summary>
public static class PdfVectorRetrieval
{
    /// <summary>Revokes an exact authorized index generation. Readers and ordinary writers cannot recreate an absent scope.</summary>
    public static PdfOperationReport Remove(PdfRetrievalScope scope, string principal, IPdfVectorStore store, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        PdfIntelligenceModel.Identity(principal, nameof(principal));
        if (store == null)
            throw new ArgumentNullException(nameof(store));
        return PdfFont.Run("RemoveIndex", "explicit.retrieval", policy, cancellationToken, c =>
        {
            var authorized = store.Authorize(scope, principal, true, c);
            c.Checkpoint();
            store.Remove(scope, principal, authorized.Version, c);
            c.Checkpoint();
        }, PdfCapability.ReadDocument);
    }

    /// <summary>Authorizes before embedding, validates exact source/model/batch metadata, then atomically commits a complete index. No shared embedding cache or automatic retry.</summary>
    public static PdfVectorIndexResult Index(PdfSemanticChunkResult capture, PdfRetrievalScope scope, PdfRetrievalPermissions permissions, string principal, IPdfEmbeddingProvider provider, IPdfVectorStore store, PdfIntelligenceOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (capture == null)
            throw new ArgumentNullException(nameof(capture));
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        if (permissions == null)
            throw new ArgumentNullException(nameof(permissions));
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));
        if (store == null)
            throw new ArgumentNullException(nameof(store));
        PdfIntelligenceModel.Identity(principal, nameof(principal));
        if (scope.DocumentSha256 != capture.DocumentSha256 || capture.Chunks.Count == 0 || capture.Chunks.Count > 4096)
            throw new ArgumentException("A nonempty bounded capture of the exact scope revision is required.", nameof(capture));
        options ??= new();
        long version = 0;
        PdfVectorIndex? index = null;
        var report = PdfFont.Run("IndexChunks", "explicit.retrieval", policy, cancellationToken, c =>
        {
            var authorization = store.Authorize(scope, principal, true, c);
            c.Checkpoint();
            if (authorization.Permissions != null && !authorization.Permissions.Matches(permissions))
                throw PdfSemanticChunks.Error(c, "PermissionMutation", "The requested permission provenance differs from the authoritative existing index.");
            var model = Model(provider, c);
            if (model == null)
                throw PdfSemanticChunks.Error(c, "Model", "An explicit embedding model is required.");
            var response = PdfEmbeddingExecution.Run(capture.Chunks.Select(chunk => chunk.Text).ToArray(), scope.Key + capture.CaptureSha256 + PermissionBinding(permissions, c), provider, model, options, c);
            index = new(scope, permissions, capture, model, response);
            version = store.Replace(index, principal, authorization.Version, c);
            c.Checkpoint();
        }, PdfCapability.ReadDocument);
        return new(version, index!, report);
    }

    /// <summary>Authorizes the exact scope before model preparation or dispatch, rejects a different model/dimension, and reauthorizes before returning ranked matches.</summary>
    public static PdfVectorSearchResult Search(PdfRetrievalScope scope, string principal, string query, IPdfEmbeddingProvider provider, IPdfVectorStore store, int maximumResults = 10, PdfIntelligenceOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (scope == null)
            throw new ArgumentNullException(nameof(scope));
        PdfIntelligenceModel.Identity(principal, nameof(principal));
        if (query == null || query.Length == 0 || query.Length > 65536)
            throw new ArgumentException("A bounded nonempty query is required.", nameof(query));
        if (provider == null)
            throw new ArgumentNullException(nameof(provider));
        if (store == null)
            throw new ArgumentNullException(nameof(store));
        if (maximumResults < 1 || maximumResults > 1000)
            throw new ArgumentOutOfRangeException(nameof(maximumResults));
        options ??= new();
        IReadOnlyList<PdfVectorMatch>? matches = null;
        var report = PdfFont.Run("SearchChunks", "explicit.retrieval", policy, cancellationToken, c =>
        {
            var authorized = store.Authorize(scope, principal, false, c);
            c.Checkpoint();
            var model = Model(provider, c);
            if (model == null || authorized.Model == null || !authorized.Model.Matches(model))
                throw PdfSemanticChunks.Error(c, "Model", "The query embedding model/version/dimensions differ from the authorized index.");
            var response = PdfEmbeddingExecution.Run(new[] { query }, scope.Key + authorized.Version.ToString(CultureInfo.InvariantCulture) + PermissionBinding(authorized.Permissions!, c), provider, model, options, c);
            var found = store.Search(scope, principal, authorized.Version, model, response.Vectors[0], maximumResults, c);
            c.Checkpoint();
            if (found == null || found.Count > maximumResults || found.Any(match => match == null || match.Index.Scope.Key != scope.Key || !match.Index.Model.Matches(model) || !match.Index.Permissions.Matches(authorized.Permissions!)))
                throw PdfSemanticChunks.Error(c, "StoreBinding", "A store returned a match outside the exact authorized scope/model/permissions.");
            c.Consume(PdfResourceKind.ExpandedBytes, found.Count * 16L);
            matches = new ReadOnlyCollection<PdfVectorMatch>(found.ToArray());
        }, PdfCapability.ReadDocument);
        return new(matches!, report);
    }

    private static PdfEmbeddingModel Model(IPdfEmbeddingProvider provider, PdfOperationContext c)
    {
        try
        {
            var model = provider.Model;
            c.Checkpoint();
            return model;
        }
        catch
        {
            c.Checkpoint();
            throw PdfSemanticChunks.Error(c, "ModelUnavailable", "Explicit offline embedding model metadata is unavailable.");
        }
    }

    private static string PermissionBinding(PdfRetrievalPermissions permissions, PdfOperationContext c) => PdfArchival.Digest(IntelligenceJson.Bytes(new Dictionary<string, object?> { ["policy"] = permissions.PolicyVersion, ["writer"] = permissions.Writer, ["readers"] = permissions.Readers.Select(reader => (object? )reader).ToList() }, c), c);
}
