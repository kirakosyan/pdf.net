using System.IO;
using System.Threading;

namespace Fluent.Pdf;
// Shared bounded capture/edit boundary. Immutable raw snapshots need no rewrite for inspection.
internal sealed class PdfGraphEdit : IDisposable
{
    internal readonly PdfDocumentSnapshot Snapshot;
    internal readonly PdfSyntaxDocument Graph;
    internal readonly PdfTransaction Edit;
    internal readonly PdfOperationContext Context;
    internal PdfValue Catalog => Resolve(Edit.Trailer.Entries["Root"]);

    internal PdfGraphEdit(PdfDocumentSnapshot source, PdfOrganizationOptions options, PdfOperationContext context)
    {
        Context = context;
        bool raw = source.SourceDocument != null && source.PageNumbering == null && source.Pages.Count == source.OriginalOrder.Count;
        for (int i = 0; raw && i < source.Pages.Count; i++)
        {
            var p = source.Pages[i];
            raw = p.Source != null && p.Source.ObjectId == source.OriginalOrder[i] && ReferenceEquals(p.Source.Document, source.SourceDocument) && p.Geometry.Equals(p.Source.Geometry) && p.Content == null && p.FontResources.Count == 0 && p.GraphicsResources.Bindings.Count == 0 && p.CanvasLayers.Count == 0 && p.StructureRoles.Count == 0 && p.Links.Count == 0;
        }

        if (raw)
            Snapshot = source;
        else
        {
            using var normalized = PdfPageOrganization.Normalize(source, options, context);
            Snapshot = normalized.CreateSnapshot();
        }

        Graph = Snapshot.SourceDocument!;
        Edit = new(Graph, context.Policy);
    }

    internal PdfValue Resolve(PdfValue value)
    {
        var seen = new HashSet<PdfObjectId>();
        while (value.Kind == PdfValueKind.Reference)
        {
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.Objects, 1);
            Context.Consume(PdfResourceKind.ExpandedBytes, 48);
            if (!seen.Add(value.ReferenceId))
                throw Error("Graph.ReferenceCycle", "The object reference chain contains a cycle.");
            value = Edit.Resolve(value.ReferenceId);
        }

        return value;
    }

    internal PdfValue Get(PdfValue dictionary, string key) => dictionary.Kind != PdfValueKind.Null && dictionary.Entries.TryGetValue(key, out var value) ? Resolve(value) : PdfValue.Null;
    internal Dictionary<string, PdfValue> Copy(PdfValue dictionary) => dictionary.Kind == PdfValueKind.Null ? new(StringComparer.Ordinal) : PdfWriterCore.CopyDictionary(dictionary.Entries, Context);
    internal void SetCatalog(Dictionary<string, PdfValue> entries)
    {
        var root = Edit.Trailer.Entries["Root"];
        if (root.Kind != PdfValueKind.Reference)
            throw Error("Graph.Catalog", "An indirect catalog is required.");
        Edit.Set(root.ReferenceId, PdfValue.OwnedDictionary(entries));
    }

    internal void RequireUnstructuredContent()
    {
        if (Catalog.Entries.ContainsKey("StructTreeRoot"))
            throw Error("Structure.SourceEditPolicy", "Editing source content requires an explicit structure-association preservation profile.");
    }

    internal PdfDocument Finish(PdfOrganizationOptions options, string minimumVersion = "1.2")
    {
        using var buffer = new PdfCodecBufferStream(Context, Context.Available(PdfResourceKind.ExpandedBytes));
        using (var output = new BorrowedStream(buffer, false, Context))
            Edit.WriteCore(output, new(allowSignatureInvalidation: options.AllowSignatureInvalidation, minimumVersion: minimumVersion), Context);
        return PdfPageOrganization.Read(buffer.Finish(), Snapshot.Pages.Count, Context);
    }

    internal PdfException Error(string code, string message) => PdfWriterCore.Error(Context, code, message);
    public void Dispose() => Edit.Dispose();
    internal static PdfDocument Run(string operation, PdfDocumentSnapshot source, PdfOrganizationOptions? options, PdfOperationPolicy? policy, CancellationToken token, Action<PdfGraphEdit> action, string minimumVersion = "1.2")
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        options ??= new();
        PdfDocument? result = null;
        try
        {
            var report = PdfFont.Run(operation, "managed.editing", policy, token, c =>
            {
                using var graph = new PdfGraphEdit(source, options, c);
                action(graph);
                result = graph.Finish(options, minimumVersion);
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
