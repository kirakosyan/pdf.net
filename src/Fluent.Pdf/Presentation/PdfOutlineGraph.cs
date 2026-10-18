namespace Fluent.Pdf;
internal static class PdfOutlineGraph
{
    internal static IReadOnlyList<PdfOutlineRecord> Read(PdfNavigationReader reader)
    {
        var root = reader.Get(reader.Catalog, "Outlines");
        if (root.Kind == PdfValueKind.Null)
            return System.Array.Empty<PdfOutlineRecord>();
        if (root.Kind != PdfValueKind.Dictionary || !reader.Catalog.Entries.TryGetValue("Outlines", out var rootReference) || rootReference.Kind != PdfValueKind.Reference)
            throw reader.Error("Presentation.OutlineRoot", "Outlines must use an indirect dictionary root.");
        var seen = new HashSet<PdfObjectId>();
        IReadOnlyList<PdfOutlineRecord> Siblings(PdfValue parentReference, PdfValue parent)
        {
            using var depth = reader.Context.EnterScope(PdfResourceKind.Depth);
            var records = new List<PdfOutlineRecord>();
            var current = parent.Entries.TryGetValue("First", out var first) ? first : PdfValue.Null;
            PdfObjectId? previous = null;
            while (current.Kind != PdfValueKind.Null)
            {
                reader.Context.Checkpoint();
                reader.Context.Consume(PdfResourceKind.Objects, 1);
                reader.Context.Consume(PdfResourceKind.ExpandedBytes, 256);
                if (current.Kind != PdfValueKind.Reference || !seen.Add(current.ReferenceId))
                    throw reader.Error("Presentation.OutlineCycle", "Outline nodes must form a finite unique indirect tree.");
                var value = reader.Resolve(current);
                if (value.Kind != PdfValueKind.Dictionary)
                    throw reader.Error("Presentation.OutlineNode", "An outline node must be a dictionary.");
                var declaredParent = value.Entries.TryGetValue("Parent", out var p) ? p : PdfValue.Null;
                var declaredPrev = value.Entries.TryGetValue("Prev", out var prev) ? prev : PdfValue.Null;
                if (declaredParent.Kind != PdfValueKind.Reference || declaredParent.ReferenceId != parentReference.ReferenceId || previous.HasValue && (declaredPrev.Kind != PdfValueKind.Reference || declaredPrev.ReferenceId != previous.Value) || !previous.HasValue && declaredPrev.Kind != PdfValueKind.Null)
                    throw reader.Error("Presentation.OutlineLinks", "Outline Parent/Prev links are inconsistent.");
                string title = PdfTextStrings.Decode(reader.Get(value, "Title"), reader.Context);
                if (value.Entries.ContainsKey("Dest") && value.Entries.ContainsKey("A"))
                    throw reader.Error("Presentation.OutlineDestination", "An outline item cannot specify both Dest and A.");
                PdfValue destination = reader.Get(value, "Dest");
                var action = reader.Get(value, "A");
                if (reader.Name(action, "S") == "GoTo")
                    destination = reader.Get(action, "D");
                int? target = null;
                if (destination.Kind != PdfValueKind.Null)
                {
                    var resolved = reader.ResolveDestination(destination);
                    for (int i = 0; i < reader.Pages.Count; i++)
                        if (reader.Pages[i].ReferenceId == resolved.Items[0].ReferenceId)
                        {
                            target = i;
                            break;
                        }
                }

                var children = Siblings(current, value);
                var count = reader.Get(value, "Count");
                if (count.Kind != PdfValueKind.Null && !count.TryGetInteger(out _))
                    throw reader.Error("Presentation.OutlineCount", "Outline counts must be bounded integers.");
                int visible = children.Sum(c => c.VisibleCount);
                if (children.Count > 0 && (count.Kind == PdfValueKind.Null || count.IntegerValue != visible && count.IntegerValue != -visible))
                    PdfPresentationValues.Warning(reader, "Presentation.OutlineCount", "An inconsistent outline expansion count is preserved for inspection.");
                var known = new HashSet<string>(StringComparer.Ordinal)
                {
                    "Title",
                    "Parent",
                    "Prev",
                    "Next",
                    "First",
                    "Last",
                    "Count",
                    "Dest",
                    "A",
                    "C",
                    "F",
                    "SE",
                    "Lang"
                };
                if (value.Entries.Keys.Any(k => !known.Contains(k)) || value.Entries.ContainsKey("SE"))
                    PdfPresentationValues.Warning(reader, "Presentation.UnsupportedOutlineSetting", "Unsupported outline settings or structure associations remain available in the source definition.");
                var flags = reader.Get(value, "F");
                if (flags.Kind != PdfValueKind.Null && (!flags.TryGetInteger(out long f) || f < 0 || f > 3))
                    PdfPresentationValues.Warning(reader, "Presentation.UnsupportedOutlineSetting", "Unknown outline style flags remain available in the source definition.");
                var color = reader.Get(value, "C");
                if (color.Kind != PdfValueKind.Null)
                {
                    bool valid = color.Kind == PdfValueKind.Array && color.Items.Count == 3;
                    if (valid)
                        foreach (var component in color.Items)
                        {
                            var c = reader.Resolve(component);
                            if (c.Kind != PdfValueKind.Number)
                            {
                                valid = false;
                                break;
                            }

                            try
                            {
                                if (c.NumberValue < 0 || c.NumberValue > 1)
                                    valid = false;
                            }
                            catch (InvalidOperationException)
                            {
                                valid = false;
                            }
                        }

                    if (!valid)
                        PdfPresentationValues.Warning(reader, "Presentation.UnsupportedOutlineSetting", "Unsupported outline colors remain available in the source definition.");
                }

                records.Add(new(current.ReferenceId, title, value, target, count.Kind == PdfValueKind.Null || count.IntegerValue >= 0, children));
                previous = current.ReferenceId;
                current = value.Entries.TryGetValue("Next", out var next) ? next : PdfValue.Null;
            }

            if (parent.Entries.TryGetValue("Last", out var last) && (last.Kind != PdfValueKind.Reference || !previous.HasValue || last.ReferenceId != previous.Value) || previous.HasValue && !parent.Entries.ContainsKey("Last"))
                throw reader.Error("Presentation.OutlineLinks", "Outline Last must identify the final sibling.");
            return records.AsReadOnly();
        }

        return Siblings(rootReference, root);
    }

    internal static void Write(PdfGraphEdit graph, IEnumerable<PdfBookmark> bookmarks, PdfActionPolicy policy)
    {
        var reader = new PdfNavigationReader(graph);
        var root = graph.Edit.Add(PdfValue.Null);
        int total = 0;
        List<(PdfValue Reference, int Visible)> Nodes(IEnumerable<PdfBookmark> items, PdfValue parent)
        {
            using var depth = graph.Context.EnterScope(PdfResourceKind.Depth);
            var nodes = new List<(PdfValue, int)>();
            foreach (var item in items)
            {
                graph.Context.Checkpoint();
                graph.Context.Consume(PdfResourceKind.Objects, 1);
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, 768);
                if (++total > 100000)
                    throw graph.Error("Presentation.OutlineLimit", "Outline batches are limited to one hundred thousand nodes.");
                if (item == null)
                    throw new ArgumentException("Bookmarks cannot contain null.", nameof(bookmarks));
                var id = graph.Edit.Add(PdfValue.Null);
                var reference = PdfValue.Reference(id);
                var children = Nodes(item.Children, reference);
                int visible = children.Sum(c => c.Visible);
                var values = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
                {
                    ["Title"] = PdfTextStrings.Encode(item.Title, graph.Context),
                    ["Parent"] = parent
                };
                if (item.Destination != null)
                    values["Dest"] = item.Destination.Materialize(reader.Pages, graph.Context);
                if (item.Action != null)
                    values["A"] = item.Action.Materialize(reader, policy);
                if (item.Bold || item.Italic)
                    values["F"] = PdfValue.Integer((item.Bold ? 2 : 0) | (item.Italic ? 1 : 0));
                if (item.Color != null)
                    values["C"] = PdfGraphicsNumbers.Array(item.Color.Components.ToArray());
                if (children.Count > 0)
                {
                    values["First"] = children[0].Reference;
                    values["Last"] = children[children.Count - 1].Reference;
                    values["Count"] = PdfValue.Integer(item.IsOpen ? visible : -visible);
                }

                graph.Edit.Set(id, PdfValue.OwnedDictionary(values));
                nodes.Add((reference, 1 + (item.IsOpen ? visible : 0)));
            }

            for (int i = 0; i < nodes.Count; i++)
            {
                var values = graph.Copy(graph.Edit.Resolve(nodes[i].Item1.ReferenceId));
                if (i > 0)
                    values["Prev"] = nodes[i - 1].Item1;
                if (i + 1 < nodes.Count)
                    values["Next"] = nodes[i + 1].Item1;
                graph.Edit.Set(nodes[i].Item1.ReferenceId, PdfValue.OwnedDictionary(values));
            }

            return nodes;
        }

        var children = Nodes(bookmarks, PdfValue.Reference(root));
        var catalog = graph.Copy(graph.Catalog);
        if (children.Count == 0)
        {
            catalog.Remove("Outlines");
            graph.Edit.Remove(root);
        }
        else
        {
            var rootValues = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
            {
                ["Type"] = PdfValue.Name("Outlines"),
                ["First"] = children[0].Reference,
                ["Last"] = children[children.Count - 1].Reference
            };
            if (children.Any(c => reader.Get(graph.Edit.Resolve(c.Reference.ReferenceId), "Count").Kind == PdfValueKind.Number && reader.Get(graph.Edit.Resolve(c.Reference.ReferenceId), "Count").IntegerValue > 0))
                rootValues["Count"] = PdfValue.Integer(children.Sum(c => c.Visible));
            graph.Edit.Set(root, PdfValue.OwnedDictionary(rootValues));
            catalog["Outlines"] = PdfValue.Reference(root);
        }

        graph.SetCatalog(catalog);
        PdfNavigationReader.ValidateTargets(graph.Edit, graph.Edit.Trailer.Entries["Root"], reader.Pages, graph.Context);
    }
}
