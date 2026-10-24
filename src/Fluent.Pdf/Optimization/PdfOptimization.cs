using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Bounded explicit optimization presets; no renderer or codec is selected implicitly.</summary>
public enum PdfOptimizationPreset
{
    /// <summary>Preserve decoded bytes and document semantics without resampling images.</summary>
    Lossless,
    /// <summary>Additionally resample supported eight-bit image planes; requires permissive loss policy.</summary>
    CompactImages
}

/// <summary>Immutable full-rewrite choices. Signature invalidation and image fidelity loss require separate explicit authority.</summary>
public sealed class PdfOptimizationOptions
{
    /// <summary>Captures bounded settings. CompactImages defaults to a maximum dimension of1600 samples; an explicit override is1–32768. Lossless requires dimension zero.</summary>
    public PdfOptimizationOptions(PdfOptimizationPreset preset = PdfOptimizationPreset.Lossless, bool cleanupObjects = true, bool compressStreams = true, bool deduplicateResources = true, bool removeUnusedResources = false, bool allowSignatureInvalidation = false, int maximumImageDimension = 0)
    {
        if (!Enum.IsDefined(typeof(PdfOptimizationPreset), preset))
            throw new ArgumentOutOfRangeException(nameof(preset));
        if (maximumImageDimension < 0 || maximumImageDimension > 32768 || preset == PdfOptimizationPreset.Lossless && maximumImageDimension != 0)
            throw new ArgumentOutOfRangeException(nameof(maximumImageDimension));
        Preset = preset;
        CleanupObjects = cleanupObjects;
        CompressStreams = compressStreams;
        DeduplicateResources = deduplicateResources;
        RemoveUnusedResources = removeUnusedResources;
        AllowSignatureInvalidation = allowSignatureInvalidation;
        MaximumImageDimension = preset == PdfOptimizationPreset.CompactImages ? maximumImageDimension == 0 ? 1600 : maximumImageDimension : 0;
    }

    /// <summary>Gets the selected explicit preset.</summary>
    public PdfOptimizationPreset Preset { get; }
    /// <summary>Gets whether unreachable active objects are removed.</summary>
    public bool CleanupObjects { get; }
    /// <summary>Gets whether unfiltered internal streams are losslessly compressed when their encoded bytes shrink.</summary>
    public bool CompressStreams { get; }
    /// <summary>Gets whether exactly equal safe image/font-program streams are shared.</summary>
    public bool DeduplicateResources { get; }
    /// <summary>Gets whether conservative complete-content resource-name analysis removes unused bindings.</summary>
    public bool RemoveUnusedResources { get; }
    /// <summary>Gets explicit authority to rewrite signature/certification byte ranges without a validity claim.</summary>
    public bool AllowSignatureInvalidation { get; }
    /// <summary>Gets the image-plane dimension bound, or zero for lossless.</summary>
    public int MaximumImageDimension { get; }
}

/// <summary>Completed immutable optimized bytes and measured transformation counts, without a standards/fidelity qualification claim.</summary>
public sealed class PdfOptimizationResult
{
    private readonly PdfByteStore _bytes;
    internal PdfOptimizationResult(PdfOptimizationEngine engine, PdfOperationReport report)
    {
        _bytes = engine.Output;
        SourceBytes = engine.SourceBytes;
        SourceSha256 = engine.SourceHash;
        OutputSha256 = engine.OutputHash;
        RemovedObjects = engine.Removed;
        SharedStreams = engine.Shared;
        CompressedStreams = engine.Compressed;
        DownsampledImages = engine.Downsampled;
        RemovedResourceBindings = engine.RemovedBindings;
        OperationReport = report;
    }

    /// <summary>Gets the normalized pre-optimization PDF byte length.</summary>
    public long SourceBytes { get; }
    /// <summary>Gets completed optimized PDF byte length.</summary>
    public long OutputBytes => _bytes.Length;
    /// <summary>Gets measured byte savings; negative values disclose growth.</summary>
    public long SavedBytes => SourceBytes - OutputBytes;
    /// <summary>Gets exact normalized source SHA256.</summary>
    public string SourceSha256 { get; }
    /// <summary>Gets exact output SHA256.</summary>
    public string OutputSha256 { get; }
    /// <summary>Gets the number of removed unreachable objects.</summary>
    public int RemovedObjects { get; }
    /// <summary>Gets the number of exact duplicate image/font-program stream identities shared.</summary>
    public int SharedStreams { get; }
    /// <summary>Gets the number of streams whose encoded payload shrank through lossless compression.</summary>
    public int CompressedStreams { get; }
    /// <summary>Gets the number of explicitly resampled image planes.</summary>
    public int DownsampledImages { get; }
    /// <summary>Gets the number of resource-name bindings removed by conservative usage analysis.</summary>
    public int RemovedResourceBindings { get; }
    /// <summary>Gets the complete operation evidence and explicit losses.</summary>
    public PdfOperationReport OperationReport { get; }

    /// <summary>Opens an independent read-only detached view of completed output.</summary>
    public Stream OpenPdfStream() => _bytes.Open(0, _bytes.Length);
}

/// <summary>Original bounded PDF graph optimization with atomic output, exact resource sharing and opt-in image resampling.</summary>
public static class PdfOptimization
{
    /// <summary>Stages, reparses and verifies the complete optimized PDF before returning owned bytes.</summary>
    public static PdfOptimizationResult Optimize(PdfDocumentSnapshot source, PdfOptimizationOptions? options = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Run(source, options, providers, policy, cancellationToken, null, null);
    /// <summary>Stages all transformations and validation before copying to a borrowed output.</summary>
    public static PdfOptimizationResult Write(PdfDocumentSnapshot source, Stream output, PdfOptimizationOptions? options = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        return Run(source, options, providers, policy, cancellationToken, output, null);
    }

    /// <summary>Stages all transformations and validation before atomically replacing a file.</summary>
    public static PdfOptimizationResult WriteFile(PdfDocumentSnapshot source, string path, PdfOptimizationOptions? options = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        return Run(source, options, providers, policy, cancellationToken, null, path);
    }

    private static PdfOptimizationResult Run(PdfDocumentSnapshot source, PdfOptimizationOptions? options, PdfProviderRegistry? providers, PdfOperationPolicy? policy, CancellationToken token, Stream? output, string? path)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        options ??= new();
        providers ??= PdfProviderRegistry.Empty;
        PdfOptimizationEngine? engine = null;
        var report = PdfFont.Run("OptimizePdf", "managed.optimization", policy ?? source.Options.OperationPolicy, token, c =>
        {
            c.Consume(PdfResourceKind.ExpandedBytes, 1024);
            using var graph = new PdfGraphEdit(source, new(allowSignatureInvalidation: options.AllowSignatureInvalidation), c);
            if (graph.Graph.HasSignatureByteRanges && !options.AllowSignatureInvalidation)
                throw PdfOptimizationEngine.Error(c, "SignatureRewrite", "Optimization rewrites bytes; explicit signature invalidation authority is required.");
            PdfSignaturePolicy.RequireWriter(graph.Graph, new(allowSignatureInvalidation: options.AllowSignatureInvalidation), c, false);
            engine = new(graph, options, providers);
            engine.Run();
            if (path != null)
                PdfAtomicFile.Write(path, c, target => PdfProfileIo.Copy(engine.Output, target, c));
            else if (output != null)
                PdfProfileIo.Copy(engine.Output, output, c);
        }, PdfCapability.WriteDocument);
        return new(engine!, report);
    }
}
