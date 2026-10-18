namespace Fluent.Pdf;
internal sealed partial class PdfPageImportSource
{
    internal readonly PdfPageImportEngine Engine;
    internal readonly PdfSyntaxDocument Graph;
    internal readonly PdfValue Catalog;
    internal readonly PdfPageSource[] OriginalPages;
    internal readonly Dictionary<PdfObjectId, PdfObjectId> Map = new();
    internal readonly HashSet<PdfObjectId> AllPages;
    internal readonly Dictionary<PdfObjectId, PdfObjectId> Selected = new();
    internal readonly Dictionary<long, long> ParentKeys = new();
    private readonly IReadOnlyList<int> _indices;
    private readonly List<PdfObjectId> _outputIds = new();
    private readonly int _ordinal;
    private readonly HashSet<PdfObjectId> _omittedWidgets = new();
    private PdfOperationContext Context => Engine.Context;

    internal PdfPageImportSource(PdfPageImportEngine engine, PdfDocument document, IReadOnlyList<int> indices, int ordinal)
    {
        Engine = engine;
        Graph = document.SourceDocument!;
        _indices = indices;
        _ordinal = ordinal;
        OriginalPages = document.Pages.Select(p => p.Source!).ToArray();
        AllPages = new(OriginalPages.Select(p => p.ObjectId));
        Catalog = Resolve(Graph.Revisions[0].Trailer.Entries["Root"]);
        foreach (int index in indices)
        {
            Context.Consume(PdfResourceKind.Objects, 1);
            Context.Consume(PdfResourceKind.ExpandedBytes, 128);
            var id = Engine.Edit.Add(PdfValue.Null);
            _outputIds.Add(id);
            if (!Selected.ContainsKey(OriginalPages[index].ObjectId))
                Selected.Add(OriginalPages[index].ObjectId, id);
        }

        foreach (var pair in Selected)
            Map.Add(pair.Key, pair.Value);
        for (int i = 0; i < OriginalPages.Length; i++)
        {
            if (Selected.ContainsKey(OriginalPages[i].ObjectId))
                continue;
            var page = OriginalPages[i].Dictionary;
            if (page.Entries.TryGetValue("Annots", out var annots))
                foreach (var item in Array(annots))
                    if (item.Kind == PdfValueKind.Reference && Name(Resolve(item), "Subtype") == "Widget")
                        _omittedWidgets.Add(item.ReferenceId);
        }
    }

    internal void Import()
    {
        ImportFields();
        ImportStructure();
        for (int i = 0; i < _indices.Count; i++)
        {
            Context.Checkpoint();
            var source = OriginalPages[_indices[i]];
            bool duplicate = Selected[source.ObjectId] != _outputIds[i];
            if (duplicate && (Catalog.Entries.ContainsKey("StructTreeRoot") || Catalog.Entries.ContainsKey("AcroForm")))
                throw Error("Organization.DuplicateInteractivePage", "Duplicating structured or form pages requires an explicit independent structure/field-copy profile.");
            var saved = Map[source.ObjectId];
            Dictionary<PdfObjectId, PdfObjectId>? originalMap = null;
            if (duplicate)
            {
                Context.Consume(PdfResourceKind.ExpandedBytes, Map.Count * 64L + 256);
                originalMap = new(Map);
                Map.Clear();
                foreach (var pair in Selected)
                    Map.Add(pair.Key, pair.Value);
            }

            Map[source.ObjectId] = _outputIds[i];
            var entries = CloneEntries(source.Dictionary, "Parent");
            foreach (var inherited in source.EffectiveAttributes.Entries)
                if (!entries.ContainsKey(inherited.Key))
                    entries[inherited.Key] = Clone(inherited.Value);
            entries["Parent"] = PdfValue.Reference(Engine.PagesId);
            Engine.Edit.Set(_outputIds[i], PdfValue.OwnedDictionary(entries));
            Engine.Pages.Add(PdfValue.Reference(_outputIds[i]));
            if (originalMap != null)
            {
                Map.Clear();
                foreach (var pair in originalMap)
                    Map.Add(pair.Key, pair.Value);
            }
            else
                Map[source.ObjectId] = saved;
            ImportLabel(_indices[i], Engine.Pages.Count - 1);
        }

        ImportOutlines();
        ImportCatalog();
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
                throw Error("Organization.ReferenceCycle", "A source reference chain contains a cycle.");
            value = Graph.Resolve(value.ReferenceId);
        }

        return value;
    }

    internal IReadOnlyList<PdfValue> Array(PdfValue value)
    {
        value = Resolve(value);
        if (value.Kind != PdfValueKind.Array)
            throw Error("Organization.Array", "An organization structure must resolve to an array.");
        return value.Items;
    }

    internal static string? Name(PdfValue value, string key) => value.Entries.TryGetValue(key, out var item) && item.Kind == PdfValueKind.Name ? item.NameValue : null;
    internal PdfValue Clone(PdfValue value)
    {
        Context.Checkpoint();
        Context.Consume(PdfResourceKind.Objects, 1);
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        if (value.Kind == PdfValueKind.Reference)
        {
            var id = value.ReferenceId;
            if (_prunedStructure.Contains(id) || _prunedFields.Contains(id))
                return PdfValue.Null;
            if (Map.TryGetValue(id, out var existing))
                return PdfValue.Reference(existing);
            if (AllPages.Contains(id))
                throw Error("Organization.OmittedPageReference", "A retained object references an omitted source page; select its target or explicitly remove the affected feature.");
            Context.Consume(PdfResourceKind.ExpandedBytes, 96);
            var created = Engine.Edit.Add(PdfValue.Null);
            Map.Add(id, created);
            Engine.Edit.Set(created, Clone(Graph.Resolve(id)));
            return PdfValue.Reference(created);
        }

        if (value.Kind == PdfValueKind.Array)
        {
            Context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 16L + 32);
            return PdfValue.OwnedArray(value.Items.Select(Clone).ToList());
        }

        if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
        {
            var entries = CloneEntries(value);
            return value.Kind == PdfValueKind.Stream ? value.WithStreamDictionary(PdfValue.OwnedDictionary(entries)) : PdfValue.OwnedDictionary(entries);
        }

        return value;
    }

    internal Dictionary<string, PdfValue> CloneEntries(PdfValue dictionary, params string[] skip)
    {
        Context.Consume(PdfResourceKind.ExpandedBytes, dictionary.Entries.Count * 96L + 64);
        var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        foreach (var pair in dictionary.Entries)
        {
            Context.Checkpoint();
            if (skip.Contains(pair.Key))
                continue;
            if (pair.Key == "Dest" && Name(dictionary, "Subtype") == "Link" || pair.Key == "D" && Name(dictionary, "S") == "GoTo")
            {
                var destination = Destination(pair.Value);
                if (destination != null)
                    entries[pair.Key] = destination;
                continue;
            }

            if ((pair.Key == "StructParents" || pair.Key == "StructParent") && Resolve(pair.Value).Kind == PdfValueKind.Number)
            {
                entries[pair.Key] = PdfValue.Integer(ParentKey(Resolve(pair.Value).IntegerValue));
                continue;
            }

            if (pair.Key == "A" && Resolve(pair.Value).Kind == PdfValueKind.Dictionary && Name(Resolve(pair.Value), "S") == "GoTo")
            {
                var action = Resolve(pair.Value);
                if (!action.Entries.TryGetValue("D", out var actionDestination))
                    throw Error("Organization.Destination", "A local GoTo action requires a destination.");
                var destination = Destination(actionDestination);
                if (destination == null)
                    continue;
                var actionEntries = CloneEntries(action, "D");
                actionEntries["D"] = destination;
                entries[pair.Key] = PdfValue.OwnedDictionary(actionEntries);
                continue;
            }

            entries[pair.Key] = Clone(pair.Value);
        }

        return entries;
    }

    internal PdfValue? Destination(PdfValue declared)
    {
        var destination = Resolve(declared);
        if (destination.Kind == PdfValueKind.Name || destination.Kind == PdfValueKind.String)
        {
            string key = Key(destination);
            destination = NamedDestination(key) ?? throw Error("Organization.NamedDestination", "A named destination cannot be resolved from this source.");
            destination = Resolve(destination);
        }

        if (destination.Kind == PdfValueKind.Dictionary && destination.Entries.TryGetValue("D", out var d))
            destination = Resolve(d);
        if (destination.Kind != PdfValueKind.Array || destination.Items.Count < 2 || destination.Items[0].Kind != PdfValueKind.Reference)
            throw Error("Organization.Destination", "A local destination must identify a source page and view.");
        if (!Selected.ContainsKey(destination.Items[0].ReferenceId))
        {
            if (Engine.Options.MissingDestinations == PdfMissingDestinationPolicy.Reject)
                throw Error("Organization.MissingDestination", "A retained navigation entry points to an omitted page.");
            Context.Report(new("Organization.DestinationOmitted", "The explicit selection policy omitted navigation to an excluded page.", severity: PdfDiagnosticSeverity.Warning, lossCategory: PdfLossCategory.OmittedContent));
            return null;
        }

        return Clone(destination);
    }

    internal static string Key(PdfValue value) => string.Join("", (value.Kind == PdfValueKind.Name ? value.NameValue.Select(c => (byte)c) : value.StringBytes).Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture)));
    internal long ParentKey(long old)
    {
        if (old < 0)
            throw Error("Organization.StructureParentKey", "Structure parent keys must be nonnegative integers.");
        if (!ParentKeys.TryGetValue(old, out var key))
        {
            Context.Consume(PdfResourceKind.ExpandedBytes, 64);
            if (Engine.NextParentKey == int.MaxValue)
                throw Error("Organization.StructureParentLimit", "The structure parent-key space is exhausted.");
            key = Engine.NextParentKey++;
            ParentKeys.Add(old, key);
        }

        return key;
    }

    internal PdfException Error(string code, string message) => Engine.Error(code, message);
}
