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
            PdfDocumentSnapshot captured;
            using (normalized.UseModelContext(context))
                captured = normalized.CreateSnapshot();
            context.Consume(PdfResourceKind.ExpandedBytes, captured.Pages.Count * 16L + 64);
            Snapshot = new(source.Options, captured.Pages.ToArray(), captured.SourceDocument, captured.OriginalOrder, captured.WriteOptions, captured.PageNumbering);
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

    internal PdfDocument Finish(PdfOrganizationOptions options, string minimumVersion = "1.2", PdfWriteMode writeMode = PdfWriteMode.Full)
    {
        using var buffer = new PdfCodecBufferStream(Context, Context.Available(PdfResourceKind.ExpandedBytes));
        using (var output = new BorrowedStream(buffer, false, Context))
            Edit.WriteCore(output, new(mode: writeMode, allowSignatureInvalidation: options.AllowSignatureInvalidation, minimumVersion: minimumVersion), Context);
        return PdfPageOrganization.Read(buffer.Finish(), Snapshot.Options.MaximumPages, Context, Snapshot.Options.DefaultPageSize);
    }

    internal PdfException Error(string code, string message) => PdfWriterCore.Error(Context, code, message);
    internal void PruneUnreachable()
    {
        var reachable = new HashSet<PdfObjectId>();
        var pending = new Queue<PdfValue>();
        Enqueue(Edit.Trailer);
        while (pending.Count != 0)
        {
            Context.Checkpoint();
            Visit(pending.Dequeue());
        }

        var remove = new List<PdfObjectId>();
        foreach (var id in Edit.ActiveIds(Context))
            if (!reachable.Contains(id))
            {
                Context.Consume(PdfResourceKind.ExpandedBytes, 32);
                remove.Add(id);
            }
        foreach (var id in remove)
            Edit.Remove(id);

        void Enqueue(PdfValue value)
        {
            Context.Consume(PdfResourceKind.ExpandedBytes, 16);
            pending.Enqueue(value);
        }

        void Visit(PdfValue value)
        {
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Reference)
            {
                Context.Consume(PdfResourceKind.ExpandedBytes, 48);
                if (reachable.Add(value.ReferenceId))
                    Enqueue(Edit.Resolve(value.ReferenceId));
                return;
            }
            using var depth = Context.EnterScope(PdfResourceKind.Depth);
            if (value.Kind == PdfValueKind.Array)
                foreach (var child in value.Items)
                    Visit(child);
            else if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
                foreach (var child in value.Entries.Values)
                    Visit(child);
        }
    }

    public void Dispose() => Edit.Dispose();
    internal static PdfDocument Run(string operation, PdfDocumentSnapshot source, PdfOrganizationOptions? options, PdfOperationPolicy? policy, CancellationToken token, Action<PdfGraphEdit> action, string minimumVersion = "1.2", PdfWriteMode writeMode = PdfWriteMode.Full)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (!Enum.IsDefined(typeof(PdfWriteMode), writeMode))
            throw new ArgumentOutOfRangeException(nameof(writeMode));
        options ??= new();
        PdfDocument? result = null;
        try
        {
            var report = PdfFont.Run(operation, "managed.editing", policy, token, c =>
            {
                using var graph = new PdfGraphEdit(source, options, c);
                action(graph);
                result = graph.Finish(options, minimumVersion, writeMode);
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
