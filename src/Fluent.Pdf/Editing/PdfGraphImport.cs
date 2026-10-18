namespace Fluent.Pdf;
// Preserve indirect identity, sharing and cycles when importing a passive object graph.
internal sealed class PdfGraphImport
{
    private readonly PdfGraphEdit _target;
    private readonly PdfSyntaxDocument _source;
    internal readonly Dictionary<PdfObjectId, PdfObjectId> Map = new();
    private readonly HashSet<PdfObjectId> _pages;
    internal PdfGraphImport(PdfGraphEdit target, PdfDocumentSnapshot source)
    {
        _target = target;
        _source = source.SourceDocument!;
        _pages = new(source.Pages.Select(p => p.Source!.ObjectId));
    }

    internal PdfValue Clone(PdfValue value)
    {
        var context = _target.Context;
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        context.Consume(PdfResourceKind.ExpandedBytes, 64);
        using var depth = context.EnterScope(PdfResourceKind.Depth);
        if (value.Kind == PdfValueKind.Reference)
        {
            if (Map.TryGetValue(value.ReferenceId, out var existing))
                return PdfValue.Reference(existing);
            if (_pages.Contains(value.ReferenceId))
                throw _target.Error("Graph.PageImportRequired", "An imported object refers to a page without an explicit target mapping.");
            var id = _target.Edit.Add(PdfValue.Null);
            Map.Add(value.ReferenceId, id);
            _target.Edit.Set(id, Clone(_source.Resolve(value.ReferenceId)));
            return PdfValue.Reference(id);
        }

        if (value.Kind == PdfValueKind.Array)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 16L);
            return PdfValue.OwnedArray(value.Items.Select(Clone).ToList());
        }

        if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
        {
            var entries = _target.Copy(value);
            string? ownerType = ResolvedName(value, "Type");
            if (ownerType == "Catalog" || ownerType == "Pages" || ownerType == "Page")
                throw _target.Error("Graph.DocumentOwnershipImport", "Importing document/catalog ownership requires an explicit mapping profile.");
            bool localAction = ResolvedName(value, "S") == "GoTo";
            bool localLink = ResolvedName(value, "Subtype") == "Link";
            string destinationKey = localAction ? "D" : "Dest";
            if ((localAction || localLink) && entries.TryGetValue(destinationKey, out var declared))
            {
                var destination = ManagedPdfProvider.Resolve(_source, declared, context);
                if (destination.Kind == PdfValueKind.Name || destination.Kind == PdfValueKind.String)
                    throw _target.Error("Graph.NamedDestinationImportProfile", "Named local navigation requires an explicit destination import profile.");
            }

            if (entries.ContainsKey("StructParent") || entries.ContainsKey("StructParents") || entries.ContainsKey("OC"))
                throw _target.Error("Graph.OwnerDependentImport", "Structure and optional-content relationships require an explicit owner-preserving import profile.");
            foreach (var key in entries.Keys.ToArray())
                entries[key] = Clone(entries[key]);
            var dictionary = PdfValue.OwnedDictionary(entries);
            return value.Kind == PdfValueKind.Stream ? value.WithStreamDictionary(dictionary) : dictionary;
        }

        return value;
    }

    private string? ResolvedName(PdfValue dictionary, string key)
    {
        if (!dictionary.Entries.TryGetValue(key, out var value))
            return null;
        value = ManagedPdfProvider.Resolve(_source, value, _target.Context);
        return value.Kind == PdfValueKind.Name ? value.NameValue : null;
    }

    internal static int ValidateDetached(PdfValue value, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        context.Consume(PdfResourceKind.ExpandedBytes, 64);
        using var depth = context.EnterScope(PdfResourceKind.Depth);
        if (value.Kind == PdfValueKind.Reference)
            throw PdfWriterCore.Error(context, "Graph.ForeignReference", "Authored detached properties cannot contain source-owned references.");
        int floor = 12;
        if (value.Kind == PdfValueKind.Array)
        {
            floor = Math.Max(floor, PdfFeatureVersion.Array(value, v => v));
            foreach (var child in value.Items)
                floor = Math.Max(floor, ValidateDetached(child, context));
        }

        if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
        {
            floor = Math.Max(floor, PdfFeatureVersion.Dictionary(value, v => v));
            foreach (var child in value.Entries.Values)
                floor = Math.Max(floor, ValidateDetached(child, context));
        }

        return floor;
    }
}
