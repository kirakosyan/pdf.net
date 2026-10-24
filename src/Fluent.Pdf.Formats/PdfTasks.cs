using System.IO;
using System.Threading;

namespace Fluent.Pdf.Formats;

/// <summary>Immutable per-instance task composition over the existing SDK engines. Providers/adapters stay caller-owned; no plugin discovery, download or global registration occurs.</summary>
public sealed class PdfTasks
{
    /// <summary>Captures optional service and collection registries and one policy used by every new task. Default policy is strict; source-document policy is not silently substituted.</summary>
    public PdfTasks(PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, PdfCollectionAdapters? adapters = null)
    {
        Providers = providers ?? PdfProviderRegistry.Empty;
        Policy = policy ?? PdfOperationPolicy.Default;
        Adapters = adapters ?? new(Array.Empty<KeyValuePair<string, PdfCollectionAdapter>>());
    }
    /// <summary>Gets explicitly injected optional services. Sharing mutable services across concurrent tasks is the caller's responsibility.</summary>
    public PdfProviderRegistry Providers { get; }
    /// <summary>Gets the policy applied to each task, with a fresh operation budget per call.</summary>
    public PdfOperationPolicy Policy { get; }
    /// <summary>Gets explicit trusted collection adapters.</summary>
    public PdfCollectionAdapters Adapters { get; }

    /// <summary>Lays out explicit blocks, creates the document and stages a managed PDF under one budget before writing a borrowed stream. Fonts and shaping are explicit; output may be partial only if final stream copy fails.</summary>
    public PdfOperationReport Generate(IEnumerable<PdfFlowBlock> blocks, PdfFlowOptions options, Stream output, PdfWriteOptions? writing = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite) throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        return GenerateCore(blocks, options, output, null, writing, cancellationToken);
    }
    /// <summary>Stages a generated managed PDF under one budget before atomic file replacement.</summary>
    public PdfOperationReport GenerateFile(IEnumerable<PdfFlowBlock> blocks, PdfFlowOptions options, string path, PdfWriteOptions? writing = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        return GenerateCore(blocks, options, null, path, writing, cancellationToken);
    }
    private PdfOperationReport GenerateCore(IEnumerable<PdfFlowBlock> blocks, PdfFlowOptions options, Stream? output, string? path, PdfWriteOptions? writing, CancellationToken token)
    {
        if (blocks == null || options == null) throw new ArgumentNullException(blocks == null ? nameof(blocks) : nameof(options));
        writing ??= PdfWriteOptions.Default;
        var completed = new List<Action<PdfOperationReport>>();
        var report = PdfFont.Run("GenerateTaskPdf", "managed.task-generation", Policy, token, c =>
        {
            var layout = PdfFlow.LayoutCore(blocks, options, Providers, c, out var finish);
            completed.Add(finish);
            using var document = layout.CreateDocumentCore(new(maximumPages: options.MaximumPages, operationPolicy: Policy), null, c, completed);
            using var staged = new PdfCodecBufferStream(c, 67108864);
            document.WriteWithinContext(ManagedPdfProvider.Instance, staged, c, writing);
            var bytes = staged.Finish();
            if (path == null) FormatIo.Copy(bytes, output!, c); else PdfAtomicFile.Write(path, c, target => FormatIo.Copy(bytes, target, c));
        }, PdfCapability.WriteDocument);
        foreach (var finish in completed) finish(report);
        return report;
    }
    /// <summary>Delegates exact conversion options, selected page order, providers and policy to the existing conversion engine. Caller source/output stay owned by the caller.</summary>
    public PdfOperationReport Convert(PdfDocumentSnapshot source, IEnumerable<int> pages, Stream output, PdfConversionOptions options, CancellationToken cancellationToken = default)
        => PdfConversions.Export(source, pages, output, options, Providers, policy: Policy, cancellationToken: cancellationToken);
    /// <summary>Delegates conversion with atomic file replacement.</summary>
    public PdfOperationReport ConvertFile(PdfDocumentSnapshot source, IEnumerable<int> pages, string path, PdfConversionOptions options, CancellationToken cancellationToken = default)
        => PdfConversions.ExportFile(source, pages, path, options, Providers, policy: Policy, cancellationToken: cancellationToken);
    /// <summary>Returns detached page extraction with unchanged core mapping, geometry and diagnostics.</summary>
    public PdfTextExtractionResult Extract(PdfDocumentSnapshot source, int pageIndex, PdfTextExtractionOptions? options = null, CancellationToken cancellationToken = default)
        => PdfTextExtraction.ReadPage(source, pageIndex, options, Providers, Policy, cancellationToken);
    /// <summary>Returns a caller-owned filled document. Text appearance fonts/shaping come from the explicit textStyle; no calculation/action executes. Save remains a separate operation.</summary>
    public PdfDocument Fill(PdfDocumentSnapshot source, PdfFormData data, PdfFormTextStyle? textStyle = null, bool retainSourceRegenerationRequest = false, PdfWriteMode writeMode = PdfWriteMode.Full, PdfOrganizationOptions? options = null, CancellationToken cancellationToken = default)
        => PdfForms.FillData(source, data, textStyle, retainSourceRegenerationRequest, writeMode, options, Policy, cancellationToken);
    /// <summary>Delegates bounded optimization and staged stream output; result retains the core transformation counts and hashes.</summary>
    public PdfOptimizationResult Optimize(PdfDocumentSnapshot source, Stream output, PdfOptimizationOptions? options = null, CancellationToken cancellationToken = default)
        => PdfOptimization.Write(source, output, options, Providers, Policy, cancellationToken);
    /// <summary>Delegates optimization with atomic file output.</summary>
    public PdfOptimizationResult OptimizeFile(PdfDocumentSnapshot source, string path, PdfOptimizationOptions? options = null, CancellationToken cancellationToken = default)
        => PdfOptimization.WriteFile(source, path, options, Providers, Policy, cancellationToken);
    /// <summary>Performs the existing passive preflight against borrowed input with injected provider declarations.</summary>
    public PdfPreflightReport Inspect(Stream input, CancellationToken cancellationToken = default)
        => PdfPreflight.Inspect(input, Providers, Policy, cancellationToken);
    /// <summary>Captures a named collection without filesystem discovery or source execution; input streams remain caller-owned.</summary>
    public PdfDocumentCollection Collect(IEnumerable<KeyValuePair<string, Stream>> inputs, CancellationToken cancellationToken = default)
        => PdfDocumentCollection.Capture(inputs, Policy, cancellationToken);
    /// <summary>Runs the selected explicit adapter sequentially under one shared batch budget. Inspect every item outcome; ordinary item failure does not imply whole-batch exception.</summary>
    public PdfCollectionBatchResult Process(PdfDocumentCollection collection, string adapterId, CancellationToken cancellationToken = default)
    {
        if (collection == null) throw new ArgumentNullException(nameof(collection));
        return collection.Process(adapterId, Adapters, Policy, cancellationToken);
    }
}
