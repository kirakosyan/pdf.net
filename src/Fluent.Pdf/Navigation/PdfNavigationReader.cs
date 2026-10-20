namespace Fluent.Pdf;
// Shared passive destination/name-tree handling. No action payload or stream is evaluated.
internal sealed class PdfNavigationReader
{
    internal readonly PdfTransaction Edit;
    internal readonly PdfOperationContext Context;
    internal readonly PdfValue Catalog;
    internal readonly IReadOnlyList<PdfValue> Pages;
    internal PdfNavigationReader(PdfTransaction edit, PdfOperationContext context, PdfValue catalog, IReadOnlyList<PdfValue> pages)
    {
        Edit = edit;
        Context = context;
        Catalog = Resolve(catalog);
        Pages = pages;
    }

    internal PdfNavigationReader(PdfGraphEdit graph) : this(graph.Edit, graph.Context, graph.Catalog, graph.Snapshot.Pages.Select(p => PdfValue.Reference(p.Source!.ObjectId)).ToArray())
    {
    }

    internal PdfException Error(string code, string message) => PdfWriterCore.Error(Context, code, message);
    internal PdfValue Resolve(PdfValue value)
    {
        var seen = new HashSet<PdfObjectId>();
        while (value.Kind == PdfValueKind.Reference)
        {
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.Objects, 1);
            Context.Consume(PdfResourceKind.ExpandedBytes, 48);
            if (!seen.Add(value.ReferenceId))
                throw Error("Navigation.ReferenceCycle", "A navigation reference chain contains a cycle.");
            value = Edit.Resolve(value.ReferenceId);
        }

        return value;
    }

    internal PdfValue Get(PdfValue dictionary, string key) => (dictionary.Kind == PdfValueKind.Dictionary || dictionary.Kind == PdfValueKind.Stream) && dictionary.Entries.TryGetValue(key, out var value) ? Resolve(value) : PdfValue.Null;
    internal string Name(PdfValue dictionary, string key)
    {
        var v = Get(dictionary, key);
        return v.Kind == PdfValueKind.Name ? v.NameValue : "";
    }

    internal string Text(PdfValue value)
    {
        if (value.Kind == PdfValueKind.Name)
            return value.NameValue;
        if (value.Kind != PdfValueKind.String)
            throw Error("Navigation.NameKey", "A destination key must be a name or string.");
        var bytes = value.StringBytes;
        if (bytes.Count >= 2 && bytes[0] == 254 && bytes[1] == 255 || bytes.Count >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191)
            return PdfTextStrings.Decode(value, Context);
        Context.Consume(PdfResourceKind.ExpandedBytes, bytes.Count * 4L + 32);
        return BytesKey(value);
    }

    internal static string Key(PdfValue value) => value.Kind == PdfValueKind.Name ? value.NameValue : BytesKey(value);
    internal List<(PdfValue Key, PdfValue Value)> NameTree(PdfValue root)
    {
        var result = new List<(PdfValue, PdfValue)>();
        var visited = new HashSet<PdfObjectId>();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        string? previous = null;
        void Visit(PdfValue value)
        {
            using var depth = Context.EnterScope(PdfResourceKind.Depth);
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Reference)
            {
                Context.Consume(PdfResourceKind.ExpandedBytes, 48);
                if (!visited.Add(value.ReferenceId))
                    throw Error("Navigation.NameTreeCycle", "A name tree repeats a node.");
            }

            value = Resolve(value);
            if (value.Kind != PdfValueKind.Dictionary)
                throw Error("Navigation.NameTree", "Name-tree nodes must be dictionaries.");
            var names = Get(value, "Names");
            var kids = Get(value, "Kids");
            if (names.Kind != PdfValueKind.Null && kids.Kind != PdfValueKind.Null)
                throw Error("Navigation.NameTree", "Name-tree nodes cannot contain both Names and Kids.");
            if (names.Kind != PdfValueKind.Null)
            {
                if (names.Kind != PdfValueKind.Array || (names.Items.Count & 1) != 0)
                    throw Error("Navigation.NameTree", "Name-tree leaves require string/value pairs.");
                Context.Consume(PdfResourceKind.ExpandedBytes, names.Items.Count * 64L);
                for (int i = 0; i < names.Items.Count; i += 2)
                {
                    var key = Resolve(names.Items[i]);
                    if (key.Kind != PdfValueKind.String)
                        throw Error("Navigation.NameTree", "Name-tree keys must be strings.");
                    Context.Consume(PdfResourceKind.ExpandedBytes, key.StringBytes.Count * 4L + 48);
                    string identity = BytesKey(key);
                    if (!keys.Add(identity) || previous != null && string.CompareOrdinal(previous, identity) >= 0)
                        throw Error("Navigation.NameTreeOrder", "Name-tree keys must be distinct and byte-sorted.");
                    previous = identity;
                    result.Add((key, names.Items[i + 1]));
                }
            }

            if (kids.Kind != PdfValueKind.Null)
            {
                if (kids.Kind != PdfValueKind.Array || kids.Items.Count == 0)
                    throw Error("Navigation.NameTree", "Name-tree Kids must be a nonempty array.");
                foreach (var child in kids.Items)
                    Visit(child);
            }
        }

        Visit(root);
        return result;
    }

    internal static string BytesKey(PdfValue key) => new string (key.StringBytes.Select(b => (char)b).ToArray());
    internal List<(PdfValue Key, PdfValue Value)> NamedDestinations()
    {
        var result = new List<(PdfValue, PdfValue)>();
        var names = Get(Catalog, "Names");
        var tree = Get(names, "Dests");
        if (tree.Kind != PdfValueKind.Null)
            result.AddRange(NameTree(tree));
        var legacy = Get(Catalog, "Dests");
        if (legacy.Kind != PdfValueKind.Null)
        {
            if (legacy.Kind != PdfValueKind.Dictionary)
                throw Error("Navigation.LegacyDestinations", "Catalog Dests must be a dictionary.");
            foreach (var entry in legacy.Entries)
            {
                Context.Consume(PdfResourceKind.ExpandedBytes, entry.Key.Length * 4L + 96);
                result.Add((PdfValue.Name(entry.Key), entry.Value));
            }
        }

        return result;
    }

    internal PdfValue ResolveDestination(PdfValue value, bool validateTarget = true)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        var dictionaries = new HashSet<PdfValue>();
        while (true)
        {
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.Objects, 1);
            value = Resolve(value);
            if (value.Kind == PdfValueKind.Dictionary)
            {
                Context.Consume(PdfResourceKind.ExpandedBytes, 48);
                if (!dictionaries.Add(value))
                    throw Error("Navigation.DestinationCycle", "Destination dictionaries contain a cycle.");
                value = Get(value, "D");
                continue;
            }

            if (value.Kind != PdfValueKind.Name && value.Kind != PdfValueKind.String)
                break;
            string name = Key(value);
            Context.Consume(PdfResourceKind.ExpandedBytes, name.Length * 4L + 48);
            if (!names.Add(name))
                throw Error("Navigation.DestinationCycle", "Named destinations contain a cycle.");
            var matches = NamedDestinations().Where(p => Key(p.Key) == name).ToArray();
            if (matches.Length != 1)
                throw Error("Navigation.NamedDestination", "A named destination is missing or ambiguous.");
            value = matches[0].Value;
        }

        if (value.Kind != PdfValueKind.Array || value.Items.Count < 2 || value.Items[1].Kind != PdfValueKind.Name || !Enum.TryParse<PdfDestinationView>(value.Items[1].NameValue, out var view) || !Enum.IsDefined(typeof(PdfDestinationView), view))
            throw Error("Navigation.Destination", "A destination requires a supported explicit view array.");
        int expected = view == PdfDestinationView.XYZ ? 5 : view == PdfDestinationView.FitR ? 6 : view == PdfDestinationView.Fit || view == PdfDestinationView.FitB ? 2 : 3;
        if (value.Items.Count != expected)
            throw Error("Navigation.Destination", "A destination view has an invalid coordinate count.");
        for (int i = 2; i < expected; i++)
        {
            var n = Resolve(value.Items[i]);
            if (n.Kind == PdfValueKind.Null && view != PdfDestinationView.FitR)
                continue;
            if (n.Kind != PdfValueKind.Number)
                throw Error("Navigation.Destination", "Destination coordinates must be finite numbers or permitted nulls.");
            try
            {
                _ = n.NumberValue;
            }
            catch (InvalidOperationException)
            {
                throw Error("Navigation.Destination", "A destination coordinate is outside its finite range.");
            }
        }

        if (view == PdfDestinationView.FitR && (Resolve(value.Items[4]).NumberValue <= Resolve(value.Items[2]).NumberValue || Resolve(value.Items[5]).NumberValue <= Resolve(value.Items[3]).NumberValue))
            throw Error("Navigation.Destination", "FitR requires a positive rectangle.");
        if (view == PdfDestinationView.XYZ && Resolve(value.Items[4]).Kind == PdfValueKind.Number && Resolve(value.Items[4]).NumberValue < 0)
            throw Error("Navigation.Destination", "Destination zoom cannot be negative.");
        if (validateTarget && (value.Items[0].Kind != PdfValueKind.Reference || !Pages.Any(p => p.ReferenceId == value.Items[0].ReferenceId)))
            throw Error("Navigation.RemovedTarget", "A local destination identifies a page outside the current page tree.");
        return value;
    }

    internal static void ValidateTargets(PdfTransaction edit, PdfValue root, IReadOnlyList<PdfValue> pages, PdfOperationContext context)
    {
        var reader = new PdfNavigationReader(edit, context, root, pages);
        var seen = new HashSet<PdfObjectId>();
        var pending = new Queue<PdfValue>();
        foreach (var pair in reader.NamedDestinations())
            reader.ResolveDestination(pair.Value);
        void Visit(PdfValue value)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Reference)
            {
                if (seen.Contains(value.ReferenceId))
                    return;
                context.Consume(PdfResourceKind.ExpandedBytes, 48);
                seen.Add(value.ReferenceId);
                pending.Enqueue(edit.Resolve(value.ReferenceId));
                return;
            }

            using var depth = context.EnterScope(PdfResourceKind.Depth);
            if (value.Kind == PdfValueKind.Array)
            {
                foreach (var child in value.Items)
                    Visit(child);
                return;
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return;
            if (reader.Name(value, "S") == "GoTo")
                reader.ResolveDestination(reader.Get(value, "D"));
            if (value.Entries.TryGetValue("Dest", out var destination) && (reader.Name(value, "Subtype") == "Link" || value.Entries.ContainsKey("Title")))
                reader.ResolveDestination(destination);
            if (value.Entries.TryGetValue("OpenAction", out var open) && reader.Resolve(open).Kind == PdfValueKind.Array)
                reader.ResolveDestination(open);
            foreach (var child in value.Entries)
            {
                if (child.Key == "Parent" || child.Key == "P" || child.Key == "Prev")
                    continue;
                Visit(child.Value);
            }
        }

        pending.Enqueue(root);
        while (pending.Count != 0)
            Visit(pending.Dequeue());
    }
}
