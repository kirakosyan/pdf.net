using System.Security.Cryptography;

namespace Fluent.Pdf;

/// <summary>A private, bounded, thread-safe LRU store of immutable content addressed by SHA-256.</summary>
/// <remarks>Create a separate instance for each tenant and retain it only in that tenant's trusted scope. Possession of an instance grants access to its content; this is not an authentication service. No static store, tenant lookup, disk persistence or credential inheritance exists. Capacity bounds retained payload bytes; operation copies additionally consume the supplied context's expanded-byte budget. Returned copies belong to the caller.</remarks>
public sealed class PdfContentCache : IDisposable
{
    private readonly object _gate = new();
    private readonly Dictionary<string, LinkedListNode<Entry>> _entries = new(StringComparer.Ordinal);
    private readonly LinkedList<Entry> _lru = new();
    private long _resident;
    private bool _disposed;

    /// <summary>Creates private storage with 1–65536 entries, 1 byte–1 GiB retained payload and a per-entry ceiling of at most16 MiB and the retained capacity.</summary>
    public PdfContentCache(int maximumEntries = 1024, long maximumResidentBytes = 67108864, int maximumEntryBytes = 4194304)
    {
        if (maximumEntries < 1 || maximumEntries > 65536) throw new ArgumentOutOfRangeException(nameof(maximumEntries));
        if (maximumResidentBytes < 1 || maximumResidentBytes > 1073741824) throw new ArgumentOutOfRangeException(nameof(maximumResidentBytes));
        if (maximumEntryBytes < 1 || maximumEntryBytes > 16777216 || maximumEntryBytes > maximumResidentBytes) throw new ArgumentOutOfRangeException(nameof(maximumEntryBytes));
        MaximumEntries = maximumEntries; MaximumResidentBytes = maximumResidentBytes; MaximumEntryBytes = maximumEntryBytes;
    }
    /// <summary>Gets the retained entry ceiling, including empty values.</summary>
    public int MaximumEntries { get; }
    /// <summary>Gets the retained payload-byte ceiling; dictionary metadata is separately bounded by MaximumEntries.</summary>
    public long MaximumResidentBytes { get; }
    /// <summary>Gets the maximum size of one captured value.</summary>
    public int MaximumEntryBytes { get; }
    /// <summary>Gets the synchronized retained entry count, including after disposal.</summary>
    public int Count { get { lock (_gate) return _entries.Count; } }
    /// <summary>Gets the synchronized retained payload size, including after disposal.</summary>
    public long ResidentBytes { get { lock (_gate) return _resident; } }

    /// <summary>Captures content, computes its lowercase SHA-256 address and inserts or refreshes it. Input must remain unchanged during this call. There is no caller-supplied key that could poison another address.</summary>
    public string Store(byte[] content, PdfOperationContext context)
    {
        if (content == null) throw new ArgumentNullException(nameof(content));
        if (context == null) throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        lock (_gate) CheckActive();
        if (content.Length > MaximumEntryBytes) throw PdfWriterCore.Error(context, "Cache.EntryLimit", "The content exceeds the configured cache entry ceiling.");
        context.Consume(PdfResourceKind.InputBytes, content.Length);
        context.Consume(PdfResourceKind.Objects, 1);
        byte[]? copy = Copy(content, context);
        try
        {
            string address;
            using (var hash = SHA256.Create())
            {
                for (int offset = 0; offset < copy.Length; offset += 8192)
                {
                    context.Checkpoint();
                    int count = Math.Min(8192, copy.Length - offset);
                    hash.TransformBlock(copy, offset, count, copy, offset);
                }
                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                address = BitConverter.ToString(hash.Hash!).Replace("-", "").ToLowerInvariant();
            }
            lock (_gate)
            {
                CheckActive(); context.Checkpoint();
                if (_entries.TryGetValue(address, out var present))
                {
                    // A digest is not an instruction to overwrite an existing value.
                    if (!present.Value.Bytes.SequenceEqual(copy)) throw PdfWriterCore.Error(context, "Cache.HashCollision", "Different content produced the same cache address.");
                    Touch(present); return address;
                }
                while (_entries.Count >= MaximumEntries || _resident > MaximumResidentBytes - copy.LongLength) EvictFirst();
                var entry = new Entry(address, copy);
                var node = _lru.AddLast(entry);
                try { _entries.Add(address, node); }
                catch { _lru.Remove(node); throw; }
                _resident += copy.LongLength;
                copy = null; // Only private cache ownership can retain the captured array.
                return address;
            }
        }
        finally { if (copy != null) Array.Clear(copy, 0, copy.Length); }
    }
    /// <summary>Returns a detached content copy on a hit, or null on a miss. A hit consumes Objects and ExpandedBytes before allocating; failures never expose a partial array.</summary>
    public bool TryGet(string sha256, PdfOperationContext context, out byte[]? content)
    {
        if (sha256 == null) throw new ArgumentNullException(nameof(sha256));
        if (sha256.Length != 64 || sha256.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f'))) throw new ArgumentException("A canonical lowercase SHA-256 address is required.", nameof(sha256));
        if (context == null) throw new ArgumentNullException(nameof(context));
        content = null;
        lock (_gate)
        {
            CheckActive(); context.Checkpoint();
            if (!_entries.TryGetValue(sha256, out var node)) return false;
            context.Consume(PdfResourceKind.Objects, 1);
            var copy = Copy(node.Value.Bytes, context);
            Touch(node); content = copy; return true;
        }
    }
    /// <summary>Removes and clears all retained arrays. Existing detached caller copies remain caller-owned.</summary>
    public void Clear() { lock (_gate) { CheckActive(); ClearCore(); } }
    /// <summary>Idempotently closes the cache and clears retained arrays. Managed cleanup is not a guarantee of cryptographic erasure of all process memory.</summary>
    public void Dispose() { lock (_gate) { if (_disposed) return; _disposed = true; ClearCore(); } }

    private static byte[] Copy(byte[] source, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, source.Length);
        var copy = new byte[source.Length];
        try
        {
            for (int offset = 0; offset < source.Length; offset += 8192)
            { context.Checkpoint(); Buffer.BlockCopy(source, offset, copy, offset, Math.Min(8192, source.Length - offset)); }
            context.Checkpoint(); return copy;
        }
        catch { Array.Clear(copy, 0, copy.Length); throw; }
    }
    private void CheckActive() { if (_disposed) throw new ObjectDisposedException(nameof(PdfContentCache)); }
    private void Touch(LinkedListNode<Entry> node) { _lru.Remove(node); _lru.AddLast(node); }
    private void EvictFirst()
    {
        var entry = _lru.First!.Value;
        _lru.RemoveFirst(); _entries.Remove(entry.Address); _resident -= entry.Bytes.LongLength;
        Array.Clear(entry.Bytes, 0, entry.Bytes.Length);
    }
    private void ClearCore() { while (_lru.Count != 0) EvictFirst(); }
    private sealed class Entry
    {
        internal Entry(string address, byte[] bytes) { Address = address; Bytes = bytes; }
        internal string Address { get; }
        internal byte[] Bytes { get; }
    }
}
