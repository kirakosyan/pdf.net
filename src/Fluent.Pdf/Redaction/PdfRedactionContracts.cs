using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An explicit removal rectangle in displayed top-left CropBox physical points.</summary>
public sealed class PdfRedactionRegion
{
    /// <summary>Captures a zero-based page and positive displayed rectangle. No document text or identifier is retained in the removal audit.</summary>
    public PdfRedactionRegion(int pageIndex, PdfRect bounds)
    {
        if (pageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (bounds.X < 0 || bounds.Y < 0 || bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        PageIndex = pageIndex;
        Bounds = bounds;
    }

    /// <summary>Gets the zero-based source page.</summary>
    public int PageIndex { get; }
    /// <summary>Gets displayed top-left physical-point removal bounds.</summary>
    public PdfRect Bounds { get; }
}

/// <summary>Explicit destructive raster/full-rewrite policy. All original object programs, tags, forms, metadata, actions, attachments, hidden layers and revision bytes are discarded; unmasked page content is retained only as RGB pixels.</summary>
public sealed class PdfRedactionRasterOptions
{
    /// <summary>Captures explicit DPI, mask/matte colors, outward pixel padding, annotation appearance policy, signature invalidation authority and finite work/extent limits. Output normalizes each visible crop to rotation0/UserUnit1.</summary>
    public PdfRedactionRasterOptions(int dpi, uint fillRgb = 0, uint backgroundRgb = 0xffffff, int paddingPixels = 1, bool includeAnnotationAppearances = false, bool allowSignatureInvalidation = false, int maximumRegions = 10000, long maximumRasterBytes = 67108864, long maximumMaskWork = 100000000)
    {
        if (dpi < 72 || dpi > 1200 || fillRgb > 0xffffff || backgroundRgb > 0xffffff || paddingPixels < 0 || paddingPixels > 1024 || maximumRegions < 1 || maximumRegions > 100000 || maximumRasterBytes < 4 || maximumRasterBytes > 2147483615 || maximumMaskWork < 1 || maximumMaskWork > 1000000000)
            throw new ArgumentOutOfRangeException(nameof(dpi));
        Dpi = dpi;
        FillRgb = fillRgb;
        BackgroundRgb = backgroundRgb;
        PaddingPixels = paddingPixels;
        IncludeAnnotationAppearances = includeAnnotationAppearances;
        AllowSignatureInvalidation = allowSignatureInvalidation;
        MaximumRegions = maximumRegions;
        MaximumRasterBytes = maximumRasterBytes;
        MaximumMaskWork = maximumMaskWork;
    }

    /// <summary>Gets explicitly requested raster DPI.</summary>
    public int Dpi { get; }
    /// <summary>Gets replacing RGB mask color; original selected samples are not encoded.</summary>
    public uint FillRgb { get; }
    /// <summary>Gets explicit opaque page matte.</summary>
    public uint BackgroundRgb { get; }
    /// <summary>Gets extra outward pixel coverage around each requested rectangle.</summary>
    public int PaddingPixels { get; }
    /// <summary>Gets whether visible annotation/widget appearances are explicitly adopted as static pixels. All original annotation/form objects are discarded either way.</summary>
    public bool IncludeAnnotationAppearances { get; }
    /// <summary>Gets explicit authority to discard existing signatures; false rejects signed sources before rendering.</summary>
    public bool AllowSignatureInvalidation { get; }
    /// <summary>Gets finite removal-rectangle count.</summary>
    public int MaximumRegions { get; }
    /// <summary>Gets maximum BGRA extent per page, checked before invoking a renderer.</summary>
    public long MaximumRasterBytes { get; }
    /// <summary>Gets cumulative pixel/scanline/interval comparison work ceiling.</summary>
    public long MaximumMaskWork { get; }
}

/// <summary>Audited requested and actual outward-snapped removal coverage, without removed payloads or their hashes.</summary>
public sealed class PdfRedactionCoverage
{
    internal PdfRedactionCoverage(int page, PdfRect requested, PdfRect pixels)
    {
        PageIndex = page;
        RequestedBounds = requested;
        PixelBounds = pixels;
    }

    /// <summary>Gets source/output page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets caller-requested displayed physical-point bounds.</summary>
    public PdfRect RequestedBounds { get; }
    /// <summary>Gets actual clipped outward mask in top-left output-raster pixels.</summary>
    public PdfRect PixelBounds { get; }
}

/// <summary>A payload-free transformation audit. It is not independent recovery/renderer qualification.</summary>
public sealed class PdfRedactionAudit
{
    internal PdfRedactionAudit(int pages, long revisions, bool signatures, long masked, PdfRedactionCoverage[] coverage, string digest)
    {
        PageCount = pages;
        SourceRevisionCount = revisions;
        SourceHadSignatures = signatures;
        MaskedPixels = masked;
        Coverage = Array.AsReadOnly(coverage);
        OutputSha256 = digest;
    }

    /// <summary>Gets output page count.</summary>
    public int PageCount { get; }
    /// <summary>Gets revision count across distinct original source graphs, all discarded rather than copied.</summary>
    public long SourceRevisionCount { get; }
    /// <summary>Gets whether original signature byte ranges were detected and explicitly discarded.</summary>
    public bool SourceHadSignatures { get; }
    /// <summary>Gets unique replacing pixels, without double-counting overlapping rectangles.</summary>
    public long MaskedPixels { get; }
    /// <summary>Gets requested/actual removal coverage, containing no source text, names, URIs or removed-content hashes.</summary>
    public IReadOnlyList<PdfRedactionCoverage> Coverage { get; }
    /// <summary>Gets the exact newly serialized output hash, never a removed-payload digest.</summary>
    public string OutputSha256 { get; }
    /// <summary>Gets true: this profile builds a fresh graph solely from masked RGB samples and fixed structural names.</summary>
    public bool OriginalGraphDiscarded => true;
    /// <summary>Gets true: output has one fresh full revision and no original byte prefix/history.</summary>
    public bool FullRewrite => true;
}

/// <summary>An immutable serialized redaction result. Saving always copies these exact fresh bytes, with no incremental/old-history option.</summary>
public sealed class PdfRedactionResult
{
    private readonly PdfByteStore _bytes;
    private readonly PdfOperationPolicy _policy;
    internal PdfRedactionResult(PdfByteStore bytes, PdfRedactionAudit audit, PdfOperationReport report, PdfOperationPolicy policy)
    {
        _bytes = bytes;
        Audit = audit;
        OperationReport = report;
        _policy = policy;
    }

    /// <summary>Gets safe structural/count/coverage audit data.</summary>
    public PdfRedactionAudit Audit { get; }
    /// <summary>Gets the complete creation root report.</summary>
    public PdfOperationReport OperationReport { get; }
    /// <summary>Gets exact complete sanitized PDF extent.</summary>
    public long ByteLength => _bytes.Length;

    /// <summary>Opens a detached read-only view of the exact fresh PDF, containing no original graph/history.</summary>
    public Stream OpenPdfStream() => _bytes.Open(0, _bytes.Length);
    /// <summary>Copies exact sanitized bytes to an empty seekable borrowed destination. Nonempty/prefixed sinks reject so old tails or revisions cannot survive this API.</summary>
    public PdfOperationReport Save(Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        return PdfFont.Run("SaveRedactedPdf", "managed.redaction", policy ?? _policy, cancellationToken, c =>
        {
            PdfRedaction.RequireEmpty(output, c);
            PdfRedaction.Copy(_bytes, output, c);
        }, PdfCapability.WriteDocument);
    }

    /// <summary>Atomically replaces a file with the exact fresh sanitized PDF. Filesystem storage/backup erasure is outside PDF full-rewrite semantics.</summary>
    public PdfOperationReport SaveFile(string path, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        return PdfFont.Run("SaveRedactedPdfFile", "managed.redaction", policy ?? _policy, cancellationToken, c => PdfAtomicFile.Write(path, c, output =>
        {
            PdfRedaction.RequireEmpty(output, c);
            PdfRedaction.Copy(_bytes, output, c);
        }), PdfCapability.WriteDocument);
    }
}
