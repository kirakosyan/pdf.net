namespace Fluent.Pdf;
internal sealed partial class PdfPageImportSource
{
    private PdfValue? NamedDestination(string key)
    {
        if (Catalog.Entries.TryGetValue("Dests", out var legacy))
            foreach (var pair in Resolve(legacy).Entries)
                if (Key(PdfValue.Name(pair.Key)) == key)
                    return pair.Value;
        if (Catalog.Entries.TryGetValue("Names", out var names) && Resolve(names).Entries.TryGetValue("Dests", out var tree))
            foreach (var pair in NameTree(tree))
                if (Key(pair.Key) == key || pair.Key.Kind == PdfValueKind.String && key.Length > 0 && key == string.Concat(pair.Key.StringBytes.Select(b => ((char)b).ToString())))
                    return pair.Value;
        return null;
    }

    private List<(PdfValue Key, PdfValue Value)> NameTree(PdfValue root)
    {
        var result = new List<(PdfValue, PdfValue)>();
        var seen = new HashSet<PdfObjectId>();
        void Visit(PdfValue node)
        {
            using var depth = Context.EnterScope(PdfResourceKind.Depth);
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.Objects, 1);
            if (node.Kind == PdfValueKind.Reference && !seen.Add(node.ReferenceId))
                throw Error("Organization.NameTreeCycle", "A source name tree repeats a node.");
            node = Resolve(node);
            if (node.Kind != PdfValueKind.Dictionary)
                throw Error("Organization.NameTree", "Name-tree nodes must be dictionaries.");
            if (node.Entries.TryGetValue("Names", out var entries))
            {
                var values = Array(entries);
                if ((values.Count & 1) != 0)
                    throw Error("Organization.NameTree", "Name-tree entries must be key/value pairs.");
                Context.Consume(PdfResourceKind.ExpandedBytes, values.Count * 32L + 32);
                for (int i = 0; i < values.Count; i += 2)
                {
                    if (values[i].Kind != PdfValueKind.String)
                        throw Error("Organization.NameTree", "Name-tree keys must be strings.");
                    Context.Consume(PdfResourceKind.ExpandedBytes, values[i].StringBytes.Count * 8L + 32);
                    result.Add((values[i], values[i + 1]));
                }
            }

            if (node.Entries.TryGetValue("Kids", out var kids))
                foreach (var child in Array(kids))
                    Visit(child);
        }

        Visit(root);
        return result;
    }

    private SortedDictionary<long, PdfValue> NumberTree(PdfValue root)
    {
        var result = new SortedDictionary<long, PdfValue>();
        var seen = new HashSet<PdfObjectId>();
        void Visit(PdfValue node)
        {
            using var depth = Context.EnterScope(PdfResourceKind.Depth);
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.Objects, 1);
            if (node.Kind == PdfValueKind.Reference && !seen.Add(node.ReferenceId))
                throw Error("Organization.NumberTreeCycle", "A source number tree repeats a node.");
            node = Resolve(node);
            if (node.Kind != PdfValueKind.Dictionary)
                throw Error("Organization.NumberTree", "Number-tree nodes must be dictionaries.");
            if (node.Entries.TryGetValue("Nums", out var entries))
            {
                var values = Array(entries);
                if ((values.Count & 1) != 0)
                    throw Error("Organization.NumberTree", "Number-tree entries must be key/value pairs.");
                for (int i = 0; i < values.Count; i += 2)
                {
                    long key = Resolve(values[i]).IntegerValue;
                    Context.Consume(PdfResourceKind.ExpandedBytes, 96);
                    if (key < 0 || result.ContainsKey(key))
                        throw Error("Organization.NumberTree", "Number-tree keys must be distinct nonnegative integers.");
                    result.Add(key, values[i + 1]);
                }
            }

            if (node.Entries.TryGetValue("Kids", out var kids))
                foreach (var child in Array(kids))
                    Visit(child);
        }

        Visit(root);
        return result;
    }

    private SortedDictionary<long, PdfValue>? _labels;
    private void ImportLabel(int sourceIndex, int outputIndex)
    {
        if (_labels == null)
            _labels = Catalog.Entries.TryGetValue("PageLabels", out var labels) ? NumberTree(labels) : new();
        var current = _labels.LastOrDefault(p => p.Key <= sourceIndex);
        var values = current.Value == null ? new Dictionary<string, PdfValue>(StringComparer.Ordinal)
        {
            ["S"] = PdfValue.Name("D"),
            ["St"] = PdfValue.Integer(sourceIndex + 1)
        }

        : CloneEntries(Resolve(current.Value));
        if (current.Value != null && values.ContainsKey("S"))
        {
            long start = values.TryGetValue("St", out var n) ? n.IntegerValue : 1;
            if (start < 1 || sourceIndex - current.Key > int.MaxValue - start)
                throw Error("Organization.PageLabels", "A page-label counter exceeds its positive bounded range.");
            values["St"] = PdfValue.Integer(start + sourceIndex - current.Key);
        }

        Context.Consume(PdfResourceKind.ExpandedBytes, 256);
        Engine.LabelNumbers.Add(PdfValue.Integer(outputIndex));
        Engine.LabelNumbers.Add(PdfValue.OwnedDictionary(values));
    }

    private void ImportOutlines()
    {
        if (!Catalog.Entries.TryGetValue("Outlines", out var root))
            return;
        var value = Resolve(root);
        if (value.Entries.TryGetValue("First", out var first))
            Engine.OutlineChildren.AddRange(OutlineSiblings(first));
    }

    private readonly HashSet<PdfObjectId> _outlineSeen = new();
    private List<PdfValue> OutlineSiblings(PdfValue first)
    {
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        var result = new List<PdfValue>();
        var current = first;
        while (current.Kind != PdfValueKind.Null)
        {
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.Objects, 1);
            Context.Consume(PdfResourceKind.ExpandedBytes, 192);
            if (current.Kind != PdfValueKind.Reference || !_outlineSeen.Add(current.ReferenceId))
                throw Error("Organization.OutlineCycle", "Outlines must use a finite tree of unique indirect nodes.");
            var original = Resolve(current);
            var entries = CloneEntries(original, "Parent", "Next", "Prev", "First", "Last", "Count", "Dest");
            if (original.Entries.TryGetValue("Dest", out var outlineDestination))
            {
                var destination = Destination(outlineDestination);
                if (destination != null)
                    entries["Dest"] = destination;
            }

            var children = original.Entries.TryGetValue("First", out var child) ? OutlineSiblings(child) : new();
            var id = Engine.Edit.Add(PdfValue.Null);
            var reference = PdfValue.Reference(id);
            if (children.Count > 0)
            {
                LinkSiblings(Engine.Edit, children, reference, Context);
                entries["First"] = children[0];
                entries["Last"] = children[children.Count - 1];
                int count = children.Sum(x => OutlineVisible(Engine.Edit, x, Context));
                if (original.Entries.TryGetValue("Count", out var old) && old.IntegerValue < 0)
                    count = -count;
                entries["Count"] = PdfValue.Integer(count);
            }

            Engine.Edit.Set(id, PdfValue.OwnedDictionary(entries));
            result.Add(reference);
            current = original.Entries.TryGetValue("Next", out var next) ? next : PdfValue.Null;
        }

        return result;
    }

    internal static int OutlineVisible(PdfTransaction edit, PdfValue reference, PdfOperationContext context)
    {
        context.Checkpoint();
        var value = edit.Resolve(reference.ReferenceId);
        return value.Entries.TryGetValue("Count", out var count) && count.IntegerValue > 0 ? checked((int)count.IntegerValue + 1) : 1;
    }

    internal static void LinkSiblings(PdfTransaction edit, IReadOnlyList<PdfValue> siblings, PdfValue parent, PdfOperationContext context)
    {
        for (int i = 0; i < siblings.Count; i++)
        {
            var values = PdfWriterCore.CopyDictionary(edit.Resolve(siblings[i].ReferenceId).Entries, context);
            values["Parent"] = parent;
            if (i > 0)
                values["Prev"] = siblings[i - 1];
            if (i + 1 < siblings.Count)
                values["Next"] = siblings[i + 1];
            edit.Set(siblings[i].ReferenceId, PdfValue.OwnedDictionary(values));
        }
    }

    private void ImportCatalog()
    {
        if (Catalog.Entries.TryGetValue("MarkInfo", out var markInfo))
            foreach (var pair in Resolve(markInfo).Entries)
            {
                var value = Resolve(pair.Value);
                if (Engine.MarkInfo.TryGetValue(pair.Key, out var previous))
                {
                    if (value.Kind != PdfValueKind.Boolean || previous.Kind != PdfValueKind.Boolean)
                        throw Error("Organization.MarkInfoConflict", "Document marking settings require explicit reconciliation.");
                    value = PdfValue.Boolean(value.BooleanValue || previous.BooleanValue);
                }

                Engine.MarkInfo[pair.Key] = Clone(value);
            }

        foreach (var pair in Catalog.Entries)
        {
            if (new[]
            {
                "Type",
                "Pages",
                "Version",
                "StructTreeRoot",
                "MarkInfo",
                "Outlines",
                "PageLabels",
                "AcroForm",
                "Dests",
                "Names"
            }.Contains(pair.Key))
                continue;
            var copied = pair.Key == "OpenAction" && Resolve(pair.Value).Kind == PdfValueKind.Array ? Destination(pair.Value) : Clone(pair.Value);
            if (copied == null)
                continue;
            if (Engine.Catalog.TryGetValue(pair.Key, out var existing) && !EqualScalar(existing, copied))
                throw Error("Organization.CatalogConflict", "Source catalog settings conflict and require explicit reconciliation before merging.");
            Engine.Catalog[pair.Key] = copied;
        }

        var names = Catalog.Entries.TryGetValue("Names", out var declared) ? PdfWriterCore.CopyDictionary(Resolve(declared).Entries, Context) : new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        if (Catalog.Entries.TryGetValue("Dests", out var legacy))
        {
            var pairs = names.TryGetValue("Dests", out var existingDestinations) ? NameTree(existingDestinations) : new List<(PdfValue Key, PdfValue Value)>();
            foreach (var entry in Resolve(legacy).Entries)
            {
                Context.Consume(PdfResourceKind.ExpandedBytes, entry.Key.Length * 8L + 128);
                pairs.Add((PdfValue.String(entry.Key.Select(c => (byte)c).ToArray()), entry.Value));
            }

            names["Dests"] = PdfGraphicsNumbers.Dict(("Names", PdfValue.OwnedArray(pairs.SelectMany(p => new[] { p.Key, p.Value }).ToList())));
        }

        foreach (var pair in names)
        {
            var combined = Engine.Catalog.TryGetValue("Names", out var previous) ? PdfWriterCore.CopyDictionary(previous.Entries, Context) : new Dictionary<string, PdfValue>(StringComparer.Ordinal);
            var items = combined.TryGetValue(pair.Key, out var oldTree) ? oldTree.Entries["Names"].Items.ToList() : new List<PdfValue>();
            var keys = new HashSet<string>(Enumerable.Range(0, items.Count / 2).Select(i => Key(items[i * 2])), StringComparer.Ordinal);
            foreach (var entry in NameTree(pair.Value))
            {
                var value = pair.Key == "Dests" ? Destination(entry.Value) : Clone(entry.Value);
                if (value == null)
                    continue;
                if (!keys.Add(Key(entry.Key)))
                    throw Error("Organization.NameCollision", "Merged name trees contain colliding public names.");
                Context.Consume(PdfResourceKind.ExpandedBytes, 64);
                items.Add(entry.Key);
                items.Add(value);
            }

            var ordered = Enumerable.Range(0, items.Count / 2).OrderBy(i => Key(items[i * 2]), StringComparer.Ordinal).SelectMany(i => new[] { items[i * 2], items[i * 2 + 1] }).ToList();
            combined[pair.Key] = PdfGraphicsNumbers.Dict(("Names", PdfValue.OwnedArray(ordered)));
            Engine.Catalog["Names"] = PdfValue.OwnedDictionary(combined);
        }
    }

    private static bool EqualScalar(PdfValue a, PdfValue b) => a.Kind == b.Kind && (a.Kind == PdfValueKind.Null || a.Kind == PdfValueKind.Boolean && a.BooleanValue == b.BooleanValue || a.Kind == PdfValueKind.Number && a.NumberText == b.NumberText || a.Kind == PdfValueKind.Name && a.NameValue == b.NameValue || a.Kind == PdfValueKind.String && a.StringBytes.SequenceEqual(b.StringBytes));
}
