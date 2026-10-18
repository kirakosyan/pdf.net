using System.Threading;

namespace Fluent.Pdf;
public static partial class PdfPageOrganization
{
    /// <summary>Splits detached selections under one cumulative operation. The caller owns every returned document.</summary>
    public static IReadOnlyList<PdfDocument> Split(PdfDocumentSnapshot source, IEnumerable<PdfPageSelection> selections, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (selections == null)
            throw new ArgumentNullException(nameof(selections));
        options ??= new();
        var results = new List<PdfDocument>();
        try
        {
            var report = PdfFont.Run("SplitPages", "managed.organization", policy, cancellationToken, context =>
            {
                foreach (var selection in selections)
                {
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.Objects, 1);
                    context.Consume(PdfResourceKind.ExpandedBytes, 128);
                    using var engine = new PdfPageImportEngine(options, context);
                    engine.Add(new(source, selection ?? throw new ArgumentException("Selections cannot contain null.", nameof(selections))));
                    results.Add(engine.Finish());
                }

                if (results.Count == 0)
                    throw PdfWriterCore.Error(context, "Organization.EmptySplit", "Splitting requires at least one selection.");
            }, PdfCapability.WriteDocument);
            foreach (var result in results)
                result.SetCompositionReport(report);
            return results.AsReadOnly();
        }
        catch
        {
            foreach (var result in results)
                result.Dispose();
            throw;
        }
    }

    /// <summary>Inserts a detached source selection before a zero-based host index, preserving navigation through shared-context import and reordering.</summary>
    public static PdfDocument Insert(PdfDocumentSnapshot host, int index, PdfPageInput input, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (host == null)
            throw new ArgumentNullException(nameof(host));
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (index < 0 || index > host.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(index));
        options ??= new();
        PdfDocument? result = null;
        try
        {
            var report = PdfFont.Run("InsertPages", "managed.organization", policy, cancellationToken, context =>
            {
                using var first = new PdfPageImportEngine(options, context);
                first.Add(new(host));
                first.Add(input);
                using var combined = first.Finish();
                context.Consume(PdfResourceKind.ExpandedBytes, combined.Pages.Count * 256L + 256);
                var snapshot = combined.CreateSnapshot();
                var order = Enumerable.Range(0, index).Concat(Enumerable.Range(host.Pages.Count, combined.Pages.Count - host.Pages.Count)).Concat(Enumerable.Range(index, host.Pages.Count - index));
                var selection = PdfPageSelection.CreateCore(order, context);
                using var second = new PdfPageImportEngine(options, context);
                second.Add(new(snapshot, selection));
                result = second.Finish();
            }, PdfCapability.WriteDocument);
            result!.SetCompositionReport(report);
            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }

    /// <summary>Deletes the selected physical pages, rejecting an empty result and applying the explicit omitted-destination policy.</summary>
    public static PdfDocument Delete(PdfDocumentSnapshot source, PdfPageSelection selection, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (selection == null)
            throw new ArgumentNullException(nameof(selection));
        options ??= new();
        PdfDocument? result = null;
        try
        {
            var report = PdfFont.Run("DeletePages", "managed.organization", policy, cancellationToken, context =>
            {
                ContextValidate(source, selection, options, context);
                context.Consume(PdfResourceKind.ExpandedBytes, selection.Indices.Count * 32L + 128);
                var excluded = new HashSet<int>(selection.Indices);
                var retained = PdfPageSelection.CreateCore(Enumerable.Range(0, source.Pages.Count).Where(i => !excluded.Contains(i)), context);
                using var engine = new PdfPageImportEngine(options, context);
                engine.Add(new(source, retained));
                result = engine.Finish();
            }, PdfCapability.WriteDocument);
            result!.SetCompositionReport(report);
            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }
}
