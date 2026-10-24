using System.IO;

namespace Fluent.Pdf;
/// <summary>Immutable source identity. Version must be an opaque nonsecret immutable revision identifier, never a credential or URL.</summary>
public sealed class PdfRangeIdentity
{
    /// <summary>Captures a positive byte length and a bounded opaque version. Providers must change the version whenever any byte changes.</summary>
    public PdfRangeIdentity(long length, string version)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length));
        Guard.Text(version, nameof(version), 256);
        Length = length;
        Version = version;
    }

    /// <summary>Gets the complete source length.</summary>
    public long Length { get; }
    /// <summary>Gets the immutable nonsecret revision binding.</summary>
    public string Version { get; }
}

/// <summary>A range response whose body ownership transfers to the SDK. Disposal closes the body.</summary>
public sealed class PdfRangeResponse : IDisposable
{
    /// <summary>Captures exact response bounds, source identity and a readable body. The body must contain precisely length bytes.</summary>
    public PdfRangeResponse(long offset, long length, PdfRangeIdentity identity, Stream body)
    {
        if (identity == null)
            throw new ArgumentNullException(nameof(identity));
        if (offset < 0 || length <= 0 || offset > identity.Length - length)
            throw new ArgumentOutOfRangeException(nameof(offset));
        if (body == null || !body.CanRead)
            throw new ArgumentException("A readable owned response body is required.", nameof(body));
        Offset = offset;
        Length = length;
        Identity = identity;
        Body = body;
    }

    /// <summary>Gets the response's absolute start offset.</summary>
    public long Offset { get; }
    /// <summary>Gets the exact body byte count.</summary>
    public long Length { get; }
    /// <summary>Gets the actual response revision and total length.</summary>
    public PdfRangeIdentity Identity { get; }
    /// <summary>Gets the SDK-owned body. Providers must not retain or concurrently use it.</summary>
    public Stream Body { get; }

    /// <summary>Closes the owned response body.</summary>
    public void Dispose() => Body.Dispose();
}

/// <summary>Explicit caller-owned random-access transport. Authentication, origins, TLS and conditional requests belong to this provider; no network is enabled implicitly.</summary>
public interface IPdfRangeSource
{
    /// <summary>Checks authorization and returns the current immutable revision under the supplied cancellation, deadline and budget context. Called even for cache hits.</summary>
    PdfRangeIdentity GetIdentity(PdfOperationContext context);
    /// <summary>Returns an exact range conditioned on the supplied revision, or fails. Honor cancellation/authentication before transport and while reading the body. The SDK checks identity, bounds, truncation and extra bytes.</summary>
    PdfRangeResponse OpenRange(long offset, int length, PdfRangeIdentity expectedIdentity, PdfOperationContext context);
}

/// <summary>Finite session cache settings; the cache is private to one source and revision.</summary>
public sealed class PdfProgressiveOptions
{
    /// <summary>Captures block sizes of1024–1048576 bytes and a cache of at least one block and at most64MiB.</summary>
    public PdfProgressiveOptions(int blockSize = 4096, int maximumCacheBytes = 1048576)
    {
        if (blockSize < 1024 || blockSize > 1048576)
            throw new ArgumentOutOfRangeException(nameof(blockSize));
        if (maximumCacheBytes < blockSize || maximumCacheBytes > 67108864)
            throw new ArgumentOutOfRangeException(nameof(maximumCacheBytes));
        BlockSize = blockSize;
        MaximumCacheBytes = maximumCacheBytes;
    }

    /// <summary>Gets the maximum single range size.</summary>
    public int BlockSize { get; }
    /// <summary>Gets the hard resident payload cache ceiling. Bookkeeping is also charged to the operation budget.</summary>
    public int MaximumCacheBytes { get; }
}

/// <summary>Detached page dictionary with the four inheritable attributes applied. Indirect resources remain lazy references; no actions execute.</summary>
public sealed class PdfProgressivePage
{
    internal PdfProgressivePage(int index, int count, PdfObjectId id, PdfValue dictionary)
    {
        PageIndex = index;
        PageCount = count;
        ObjectId = id;
        Dictionary = dictionary;
    }

    /// <summary>Gets the zero-based requested page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets the declared root page count. This selective inspection does not validate unvisited subtrees.</summary>
    public int PageCount { get; }
    /// <summary>Gets the page's indirect identity.</summary>
    public PdfObjectId ObjectId { get; }
    /// <summary>Gets the detached page dictionary, including effective Resources, MediaBox, CropBox and Rotate declarations.</summary>
    public PdfValue Dictionary { get; }
    /// <summary>Gets the completed selective-read report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;
}
