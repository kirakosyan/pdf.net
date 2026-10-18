namespace Fluent.Pdf;
/// <summary>A passive action or opaque active-content observation at a physical dictionary/array location.</summary>
public sealed class PdfActionRecord
{
    internal PdfActionRecord(string location, string subtype, PdfValue definition, bool allowed, bool attachment)
    {
        Location = location;
        Subtype = subtype;
        Definition = definition;
        IsAllowed = allowed;
        IsAttachment = attachment;
    }

    /// <summary>Gets the object identity and direct key/index path. Shared references have separate attachment records.</summary>
    public string Location { get; }
    /// <summary>Gets the action S name, or an ActiveContent:key observation.</summary>
    public string Subtype { get; }
    /// <summary>Gets the immutable source definition without executing or decoding it.</summary>
    public PdfValue Definition { get; }
    /// <summary>Gets whether the inspection policy permits this definition.</summary>
    public bool IsAllowed { get; }
    /// <summary>Gets whether this record is a known attachment slot rather than an object definition.</summary>
    public bool IsAttachment { get; }
}

internal static class PdfActionInventory
{
    private static readonly HashSet<string> Kinds = new(StringComparer.Ordinal)
    {
        "GoTo",
        "GoToR",
        "GoToE",
        "Launch",
        "Thread",
        "URI",
        "Sound",
        "Movie",
        "Hide",
        "Named",
        "SubmitForm",
        "ResetForm",
        "ImportData",
        "JavaScript",
        "SetOCGState",
        "Rendition",
        "Trans",
        "GoTo3DView",
        "RichMediaExecute"
    };
    private static readonly HashSet<string> ActiveKeys = new(StringComparer.Ordinal)
    {
        "XFA",
        "RichMediaContent",
        "RichMediaSettings",
        "3DD",
        "3DA",
        "OnInstantiate"
    };
    internal static bool IsAction(PdfValue value, PdfNavigationReader reader) => value.Kind == PdfValueKind.Dictionary && (reader.Name(value, "Type") == "Action" || Kinds.Contains(reader.Name(value, "S")));
    internal static List<PdfActionRecord> Read(PdfNavigationReader reader, PdfActionPolicy policy) => Collect(reader, policy, null, null);
    private static List<PdfActionRecord> Collect(PdfNavigationReader reader, PdfActionPolicy policy, HashSet<PdfObjectId>? blocked, HashSet<PdfObjectId>? additionalContainers)
    {
        var records = new List<PdfActionRecord>();
        void Add(string path, string kind, PdfValue definition, bool allowed, bool slot)
        {
            reader.Context.Consume(PdfResourceKind.ExpandedBytes, path.Length * 2L + 192);
            records.Add(new(path, kind, definition, allowed, slot));
        }

        void Walk(PdfValue value, string path, bool slot = false, bool additional = false)
        {
            using var depth = reader.Context.EnterScope(PdfResourceKind.Depth);
            reader.Context.Checkpoint();
            reader.Context.Consume(PdfResourceKind.Objects, 1);
            if (additional && value.Kind != PdfValueKind.Reference && value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Null)
                throw reader.Error("Navigation.AdditionalActions", "Additional actions must resolve to a dictionary.");
            // Each active object is visited independently. Reference attachment edges are recorded here,
            // while their definitions and descendants are visited once at their own object identity.
            var resolved = value.Kind == PdfValueKind.Reference && slot ? reader.Resolve(value) : value;
            bool action = IsAction(resolved, reader);
            if (slot && resolved.Kind != PdfValueKind.Null || action)
            {
                bool allowed = policy.Allows(resolved, reader);
                Add(path, reader.Name(resolved, "S"), resolved, allowed, slot);
                if (!allowed && value.Kind == PdfValueKind.Reference && blocked != null)
                {
                    var reference = value;
                    while (reference.Kind == PdfValueKind.Reference)
                    {
                        reader.Context.Consume(PdfResourceKind.ExpandedBytes, 48);
                        blocked.Add(reference.ReferenceId);
                        reference = reader.Edit.Resolve(reference.ReferenceId);
                    }
                }
            }

            if (value.Kind == PdfValueKind.Reference)
            {
                if (additional)
                {
                    reader.Context.Consume(PdfResourceKind.ExpandedBytes, 48);
                    additionalContainers?.Add(value.ReferenceId);
                    Walk(reader.Resolve(value), path, false, true);
                }

                return;
            }

            if (value.Kind == PdfValueKind.Array)
            {
                for (int i = 0; i < value.Items.Count; i++)
                    Walk(value.Items[i], Child(path, "[" + i + "]"), slot);
                return;
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return;
            foreach (var pair in value.Entries)
            {
                reader.Context.Checkpoint();
                string child = Child(path, "/" + pair.Key);
                if (ActiveKeys.Contains(pair.Key))
                    Add(child, "ActiveContent:" + pair.Key, pair.Value, policy.AllowOtherActions, true);
                bool isSlot = additional || pair.Key == "A" || pair.Key == "OpenAction" && reader.Resolve(pair.Value).Kind != PdfValueKind.Array || action && pair.Key == "Next";
                Walk(pair.Value, child, isSlot && !(action && pair.Key == "Next" && reader.Resolve(pair.Value).Kind == PdfValueKind.Array), pair.Key == "AA");
                // Next may be an array of action references, not one action dictionary.
                if (action && pair.Key == "Next" && reader.Resolve(pair.Value).Kind == PdfValueKind.Array)
                {
                    var array = reader.Resolve(pair.Value);
                    for (int i = 0; i < array.Items.Count; i++)
                        Walk(array.Items[i], Child(child, "[" + i + "]"), true);
                }
            }
        }

        string Child(string path, string suffix)
        {
            reader.Context.Consume(PdfResourceKind.ExpandedBytes, (path.Length + suffix.Length) * 2L + 32);
            return path + suffix;
        }

        Walk(reader.Edit.Trailer, "trailer");
        foreach (var id in reader.Edit.ActiveIds(reader.Context))
            Walk(reader.Edit.Resolve(id), id.ToString());
        var scriptTree = reader.Get(reader.Get(reader.Catalog, "Names"), "JavaScript");
        if (scriptTree.Kind != PdfValueKind.Null)
        {
            int index = 0;
            foreach (var pair in reader.NameTree(scriptTree))
                Walk(pair.Value, "Catalog/Names/JavaScript[" + index++ + "]", true);
        }

        ValidateChains(reader, records);
        return records;
    }

    private static void ValidateChains(PdfNavigationReader reader, IReadOnlyList<PdfActionRecord> records)
    {
        var done = new HashSet<PdfObjectId>();
        var active = new HashSet<PdfObjectId>();
        void Visit(PdfValue value)
        {
            using var depth = reader.Context.EnterScope(PdfResourceKind.Depth);
            reader.Context.Checkpoint();
            reader.Context.Consume(PdfResourceKind.Objects, 1);
            PdfObjectId? id = value.Kind == PdfValueKind.Reference ? value.ReferenceId : null;
            if (id.HasValue)
            {
                if (active.Contains(id.Value))
                    throw reader.Error("Navigation.ActionCycle", "The action Next graph contains a cycle.");
                if (done.Contains(id.Value))
                    return;
                reader.Context.Consume(PdfResourceKind.ExpandedBytes, 96);
                active.Add(id.Value);
            }

            value = reader.Resolve(value);
            if (value.Kind != PdfValueKind.Dictionary)
                throw reader.Error("Navigation.ActionDictionary", "An action chain requires dictionaries.");
            var next = reader.Get(value, "Next");
            if (next.Kind == PdfValueKind.Array)
                foreach (var child in next.Items)
                    Visit(child);
            else if (next.Kind != PdfValueKind.Null)
                Visit(value.Entries["Next"]);
            if (id.HasValue)
            {
                active.Remove(id.Value);
                done.Add(id.Value);
            }
        }

        foreach (var id in reader.Edit.ActiveIds(reader.Context))
            if (IsAction(reader.Edit.Resolve(id), reader))
                Visit(PdfValue.Reference(id));
        foreach (var record in records)
            if (record.Definition.Kind == PdfValueKind.Dictionary && record.Definition.Entries.ContainsKey("Next"))
                Visit(record.Definition);
    }

    internal static void Sanitize(PdfNavigationReader reader, PdfActionPolicy policy)
    {
        var blocked = new HashSet<PdfObjectId>();
        var additionalContainers = new HashSet<PdfObjectId>();
        Collect(reader, policy, blocked, additionalContainers);
        var ids = new List<PdfObjectId>();
        foreach (var id in reader.Edit.ActiveIds(reader.Context))
        {
            reader.Context.Consume(PdfResourceKind.ExpandedBytes, 24);
            ids.Add(id);
        }

        foreach (var id in ids)
        {
            var value = reader.Edit.Resolve(id);
            if (IsAction(value, reader) && !policy.Allows(value, reader))
            {
                reader.Context.Consume(PdfResourceKind.ExpandedBytes, 48);
                blocked.Add(id);
            }
        }

        PdfValue Transform(PdfValue value, bool slot = false, bool additional = false)
        {
            reader.Context.Checkpoint();
            reader.Context.Consume(PdfResourceKind.Objects, 1);
            using var depth = reader.Context.EnterScope(PdfResourceKind.Depth);
            if (value.Kind == PdfValueKind.Reference)
            {
                if (slot && reader.Resolve(value).Kind == PdfValueKind.Array)
                    return Transform(reader.Resolve(value), true);
                if (blocked.Contains(value.ReferenceId) || slot && !policy.Allows(value, reader))
                    return PdfValue.Null;
                return value;
            }

            bool action = IsAction(value, reader);
            if ((action || slot) && value.Kind != PdfValueKind.Array && !policy.Allows(value, reader))
                return PdfValue.Null;
            if (value.Kind == PdfValueKind.Array)
            {
                reader.Context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 32L);
                var children = new List<PdfValue>();
                foreach (var child in value.Items)
                {
                    var changed = Transform(child, slot);
                    if (changed.Kind != PdfValueKind.Null || child.Kind == PdfValueKind.Null && !slot)
                        children.Add(changed);
                }

                return PdfValue.OwnedArray(children);
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return value;
            var values = PdfWriterCore.CopyDictionary(value.Entries, reader.Context);
            foreach (var key in value.Entries.Keys)
            {
                if (ActiveKeys.Contains(key) && !policy.AllowOtherActions || key == "JavaScript" && !policy.AllowJavaScript)
                {
                    values.Remove(key);
                    continue;
                }

                var original = value.Entries[key];
                bool isSlot = additional || key == "A" || key == "OpenAction" && reader.Resolve(original).Kind != PdfValueKind.Array || action && key == "Next";
                var transformed = Transform(original, isSlot, key == "AA");
                if (transformed.Kind == PdfValueKind.Null && original.Kind != PdfValueKind.Null)
                    values.Remove(key);
                else
                    values[key] = transformed;
            }

            return value.WithDictionary(PdfValue.OwnedDictionary(values));
        }

        foreach (var id in ids)
            reader.Edit.Set(id, blocked.Contains(id) ? PdfValue.Null : Transform(reader.Edit.Resolve(id), additional: additionalContainers.Contains(id)));
        reader.Edit.Trailer = Transform(reader.Edit.Trailer);
    }
}
