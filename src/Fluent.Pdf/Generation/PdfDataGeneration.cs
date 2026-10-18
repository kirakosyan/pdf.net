using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Typed offline generation with shared binding/layout/provider/write budgets and ordered precommit hooks.</summary>
public static class PdfDataGeneration
{
    /// <summary>Generates a caller-owned document. Hooks and immutable fragments complete before return; a later public Save is a separate operation.</summary>
    public static PdfDocument Generate<T>(IEnumerable<T> source, PdfGenerationTemplate<T> template, PdfGenerationOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Run(source, template, options, providers, null, null, null, policy, cancellationToken, out _);
    /// <summary>Generates and writes under one context at the borrowed output's current position. Hooks can fail before return; stream output can contain a prefix after failure.</summary>
    public static PdfOperationReport Write<T>(IEnumerable<T> source, PdfGenerationTemplate<T> template, PdfGenerationOptions options, PdfProviderRegistry providers, Stream output, PdfWriteOptions? writeOptions = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        using var document = Run(source, template, options, providers, output, null, writeOptions, policy, cancellationToken, out var report);
        return report;
    }

    /// <summary>Generates, runs every hook and serializes privately under one context before atomic destination replacement. Hook/provider/cancellation failures preserve an existing destination.</summary>
    public static PdfOperationReport WriteFile<T>(IEnumerable<T> source, PdfGenerationTemplate<T> template, PdfGenerationOptions options, PdfProviderRegistry providers, string path, PdfWriteOptions? writeOptions = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        using var document = Run(source, template, options, providers, null, path, writeOptions, policy, cancellationToken, out var report);
        return report;
    }

    private static PdfDocument Run<T>(IEnumerable<T> source, PdfGenerationTemplate<T> template, PdfGenerationOptions options, PdfProviderRegistry providers, Stream? output, string? path, PdfWriteOptions? writeOptions, PdfOperationPolicy? policy, CancellationToken token, out PdfOperationReport report)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        PdfDocument? document = null;
        PdfGenerationContext? generation = null;
        List<Action<PdfOperationReport>>? finish = null;
        try
        {
            report = PdfFont.Run(path != null ? "GenerateDataFile" : output != null ? "WriteData" : "GenerateData", "managed.generation", policy ?? options.DocumentOptions.OperationPolicy, token, c =>
            {
                c.Consume(PdfResourceKind.ExpandedBytes, 4096);
                c.Consume(PdfResourceKind.Objects, 16);
                finish = new();
                PdfTableData.ValidateCulture(options.Culture, c);
                generation = new(options, providers, c, finish);
                void Prepare(Stream? target)
                {
                    generation.Emit(PdfGenerationEventKind.Starting);
                    using (var enumerator = source.GetEnumerator())
                    {
                        int index = 0;
                        while (true)
                        {
                            c.Checkpoint();
                            c.Consume(PdfResourceKind.Objects, 1);
                            if (!enumerator.MoveNext())
                            {
                                c.Checkpoint();
                                break;
                            }

                            c.Checkpoint();
                            if (index == options.MaximumRecords)
                                Failure("RecordLimit", "The explicit generation record ceiling was exceeded.");
                            T record = enumerator.Current;
                            if (!typeof(T).IsValueType && ReferenceEquals(record, source))
                                Failure("DataCycle", "The source directly contains itself.");
                            generation.RecordIndex = index;
                            generation.Emit(PdfGenerationEventKind.RecordStarting);
                            generation.ApplyRoot(template, record);
                            generation.RecordsPrepared = ++index;
                            generation.Emit(PdfGenerationEventKind.RecordPrepared);
                        }
                    }

                    c.Checkpoint();
                    generation.Emit(PdfGenerationEventKind.LayoutStarting);
                    c.Consume(PdfResourceKind.ExpandedBytes, 256);
                    PdfFlowResult flow;
                    Action<PdfOperationReport> complete;
                    if (options.PageTemplate == null)
                    {
                        flow = PdfFlow.LayoutCore(generation.Blocks, options.Layout, providers, c, out complete);
                        finish.Add(complete);
                        document = flow.CreateDocumentCore(options.DocumentOptions, providers, c, finish);
                    }
                    else
                    {
                        var layout = PdfTemplateLayout.LayoutCore(generation.Blocks, options.Layout, providers, options.PageTemplate, c, out complete);
                        finish.Add(complete);
                        flow = layout.Body;
                        document = layout.CreateDocumentCore(options.DocumentOptions, providers, c, finish);
                    }

                    generation.SetLayout(flow);
                    generation.Emit(PdfGenerationEventKind.LayoutPrepared);
                    for (int i = 0; i < flow.Pages.Count; i++)
                        generation.Emit(PdfGenerationEventKind.PagePrepared, i);
                    if (target != null)
                    {
                        generation.Emit(PdfGenerationEventKind.SaveStarting);
                        var writer = providers.GetRequired<IPdfDocumentWriter>(PdfCapability.WriteDocument);
                        document.WriteWithinContext(writer, target, c, writeOptions ?? new(minimumVersion: "1.7"));
                        generation.Emit(PdfGenerationEventKind.SavePrepared);
                    }

                    generation.Emit(PdfGenerationEventKind.Prepared);
                    generation.Expire();
                    c.EnsureComplete();
                }

                if (path != null)
                    PdfAtomicFile.Write(path, c, stream => Prepare(stream));
                else
                    Prepare(output);
                void Failure(string code, string message)
                {
                    c.Report(new("Generation." + code, message));
                    throw new InvalidOperationException("A fatal generation diagnostic unexpectedly returned.");
                }
            }, PdfCapability.WriteDocument);
            foreach (var complete in finish!)
                complete(report);
            document!.SetCompositionReport(report);
            return document;
        }
        catch
        {
            document?.Dispose();
            throw;
        }
        finally
        {
            generation?.Expire();
        }
    }
}
