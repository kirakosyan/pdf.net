namespace Fluent.Pdf;
// Original standard portable-collection validation. No UI or active file opening is implied.
internal static class PdfAttachmentCollection
{
    internal static void Validate(PdfValue collection, PdfGraphEdit graph)
    {
        Direct(collection, graph);
        Dictionary(collection, graph, "Collection", "Type", "Schema", "D", "View", "Sort");
        var view = Get(collection, "View");
        if (view.Kind != PdfValueKind.Null && (view.Kind != PdfValueKind.Name || !new[]
        {
            "D",
            "T",
            "H"
        }.Contains(view.NameValue)))
            throw graph.Error("Attachments.CollectionView", "Collection View must be D, T or H.");
        var names = PdfAttachmentGraph.Names(graph);
        var initial = Get(collection, "D");
        if (initial.Kind != PdfValueKind.Null && (initial.Kind != PdfValueKind.String || !names.Any(p => p.Key.StringBytes.SequenceEqual(initial.StringBytes))))
            throw graph.Error("Attachments.CollectionInitial", "Collection D must identify an existing EmbeddedFiles byte key.");
        var schema = Get(collection, "Schema");
        if (schema.Kind != PdfValueKind.Null)
        {
            if (schema.Kind != PdfValueKind.Dictionary)
                throw graph.Error("Attachments.CollectionSchema", "Collection Schema must be a dictionary.");
            Type(schema, "CollectionSchema", graph);
            foreach (var field in schema.Entries)
            {
                graph.Context.Checkpoint();
                if (field.Key == "Type")
                    continue;
                Dictionary(field.Value, graph, "CollectionField", "Type", "Subtype", "N", "O", "V", "E");
                var subtype = Get(field.Value, "Subtype");
                if (subtype.Kind != PdfValueKind.Name || !new[]
                {
                    "S",
                    "D",
                    "N",
                    "F",
                    "Desc",
                    "ModDate",
                    "CreationDate",
                    "Size"
                }.Contains(subtype.NameValue))
                    throw graph.Error("Attachments.CollectionField", "A collection field requires a standard Subtype.");
                PdfTextStrings.Decode(Get(field.Value, "N"), graph.Context);
                var order = Get(field.Value, "O");
                if (order.Kind != PdfValueKind.Null && !order.TryGetInteger(out _))
                    throw graph.Error("Attachments.CollectionFieldOrder", "Collection field O must be an integer.");
                foreach (var key in new[]
                {
                    "V",
                    "E"
                }

                )
                    if (Get(field.Value, key).Kind != PdfValueKind.Null && Get(field.Value, key).Kind != PdfValueKind.Boolean)
                        throw graph.Error("Attachments.CollectionFieldFlag", "Collection field flags must be boolean.");
            }
        }

        var sort = Get(collection, "Sort");
        if (sort.Kind != PdfValueKind.Null)
        {
            Dictionary(sort, graph, "CollectionSort", "Type", "S", "A");
            var fields = Get(sort, "S");
            if (fields.Kind != PdfValueKind.Name && (fields.Kind != PdfValueKind.Array || fields.Items.Count == 0))
                throw graph.Error("Attachments.CollectionSort", "A collection sort requires one or more field names.");
            var keys = fields.Kind == PdfValueKind.Name ? new[]
            {
                fields
            }

            : fields.Items;
            foreach (var key in keys)
                if (key.Kind != PdfValueKind.Name || schema.Kind != PdfValueKind.Dictionary || key.NameValue == "Type" || !schema.Entries.ContainsKey(key.NameValue))
                    throw graph.Error("Attachments.CollectionSortField", "Sort names must identify fields in the explicit schema.");
            var ascending = Get(sort, "A");
            if (ascending.Kind != PdfValueKind.Null && ascending.Kind != PdfValueKind.Boolean && (ascending.Kind != PdfValueKind.Array || ascending.Items.Any(x => x.Kind != PdfValueKind.Boolean)))
                throw graph.Error("Attachments.CollectionSortDirection", "Collection sort directions must be booleans.");
        }

        foreach (var pair in names)
        {
            var specification = graph.Resolve(pair.Value);
            var item = graph.Get(specification, "CI");
            if (item.Kind != PdfValueKind.Null)
                ValidateItem(item, collection, graph);
        }
    }

    internal static void ValidateItem(PdfValue item, PdfValue collection, PdfGraphEdit graph)
    {
        Direct(item, graph);
        if (item.Kind != PdfValueKind.Dictionary)
            throw graph.Error("Attachments.CollectionItem", "CI must be a dictionary.");
        Type(item, "CollectionItem", graph);
        var schema = graph.Get(collection, "Schema");
        if (schema.Kind != PdfValueKind.Dictionary)
            throw graph.Error("Attachments.CollectionSchemaRequired", "Editing collection items requires an explicit collection schema.");
        foreach (var pair in item.Entries)
        {
            graph.Context.Checkpoint();
            if (pair.Key == "Type")
                continue;
            if (!schema.Entries.TryGetValue(pair.Key, out var field))
                throw graph.Error("Attachments.CollectionItemField", "A collection item key is absent from its schema.");
            field = graph.Resolve(field);
            var subtype = graph.Get(field, "Subtype");
            var value = pair.Value;
            if (value.Kind == PdfValueKind.Dictionary)
            {
                Dictionary(value, graph, "CollectionSubitem", "Type", "D", "P");
                var prefix = Get(value, "P");
                if (prefix.Kind != PdfValueKind.Null)
                    PdfTextStrings.Decode(prefix, graph.Context);
                value = Get(value, "D");
            }

            if (subtype.Kind != PdfValueKind.Name)
                throw graph.Error("Attachments.CollectionField", "The schema field requires a Subtype.");
            if (subtype.NameValue == "N")
            {
                if (value.Kind != PdfValueKind.Number)
                    throw graph.Error("Attachments.CollectionNumber", "A numeric collection field requires a PDF number.");
            }
            else if (subtype.NameValue == "S" || subtype.NameValue == "D")
            {
                var text = PdfTextStrings.Decode(value, graph.Context);
                if (subtype.NameValue == "D")
                    PdfMetadataDates.Validate(text, graph.Context);
            }
            else
                throw graph.Error("Attachments.CollectionDerivedField", "File-derived fields cannot be overridden in CI.");
        }
    }

    private static PdfValue Get(PdfValue value, string key) => value.Kind == PdfValueKind.Dictionary && value.Entries.TryGetValue(key, out var child) ? child : PdfValue.Null;
    private static void Type(PdfValue value, string expected, PdfGraphEdit graph)
    {
        var type = Get(value, "Type");
        if (type.Kind != PdfValueKind.Null && (type.Kind != PdfValueKind.Name || type.NameValue != expected))
            throw graph.Error("Attachments.CollectionType", "A collection dictionary has an invalid Type.");
    }

    private static void Dictionary(PdfValue value, PdfGraphEdit graph, string type, params string[] keys)
    {
        if (value.Kind != PdfValueKind.Dictionary)
            throw graph.Error("Attachments.CollectionDictionary", "A collection component must be a dictionary.");
        Type(value, type, graph);
        foreach (var key in value.Entries.Keys)
            if (!keys.Contains(key))
                throw graph.Error("Attachments.CollectionUnsupported", "An authored collection key requires an explicit supported profile.");
    }

    private static void Direct(PdfValue value, PdfGraphEdit graph)
    {
        graph.Context.Checkpoint();
        using var depth = graph.Context.EnterScope(PdfResourceKind.Depth);
        graph.Context.Consume(PdfResourceKind.Objects, 1);
        if (value.Kind == PdfValueKind.Reference || value.Kind == PdfValueKind.Stream)
            throw graph.Error("Attachments.CollectionOwnership", "Authored collection values must be direct data; source references are not imported implicitly.");
        if (value.Kind == PdfValueKind.Array)
            foreach (var item in value.Items)
                Direct(item, graph);
        if (value.Kind == PdfValueKind.Dictionary)
            foreach (var item in value.Entries.Values)
                Direct(item, graph);
    }
}
