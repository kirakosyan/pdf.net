namespace Fluent.Pdf;
internal sealed partial class PdfPageImportSource
{
    private PdfValue? _structureRoot;
    private readonly HashSet<PdfObjectId> _prunedStructure = new();
    private void ImportStructure()
    {
        if (!Catalog.Entries.TryGetValue("StructTreeRoot", out var declared))
            return;
        if (declared.Kind != PdfValueKind.Reference)
            throw Error("Organization.StructureRoot", "The source structure root must have an indirect identity.");
        Engine.HasStructure = true;
        Map[declared.ReferenceId] = Engine.StructureId;
        var root = Resolve(declared);
        _structureRoot = root;
        foreach (string key in root.Entries.Keys)
            if (!new[]
            {
                "Type",
                "K",
                "ParentTree",
                "ParentTreeNextKey",
                "RoleMap"
            }.Contains(key))
                throw Error("Organization.StructureProfile", "This source uses structure-root metadata requiring an additional explicit remapping profile.");
        if (root.Entries.TryGetValue("RoleMap", out var roles))
            foreach (var pair in Resolve(roles).Entries)
            {
                var value = Resolve(pair.Value);
                if (Engine.Roles.TryGetValue(pair.Key, out var previous) && !EqualScalar(previous, value))
                    throw Error("Organization.RoleCollision", "Custom structure roles conflict between source documents.");
                Engine.Roles[pair.Key] = Clone(value);
            }

        if (root.Entries.TryGetValue("K", out var children))
        {
            var value = StructureNode(children, PdfValue.Null, PdfValue.Reference(Engine.StructureId));
            if (value != null)
            {
                if (value.Kind == PdfValueKind.Array)
                    Engine.StructureChildren.AddRange(value.Items);
                else
                    Engine.StructureChildren.Add(value);
            }
        }
    }

    private PdfValue? StructureNode(PdfValue declared, PdfValue inheritedPage, PdfValue parent)
    {
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        Context.Checkpoint();
        Context.Consume(PdfResourceKind.Objects, 1);
        Context.Consume(PdfResourceKind.ExpandedBytes, 128);
        var node = Resolve(declared);
        if (node.Kind == PdfValueKind.Null)
            return null;
        if (node.Kind == PdfValueKind.Array)
        {
            var values = new List<PdfValue>();
            foreach (var child in node.Items)
            {
                var value = StructureNode(child, inheritedPage, parent);
                if (value != null)
                {
                    Context.Consume(PdfResourceKind.ExpandedBytes, 16);
                    values.Add(value);
                }
            }

            return values.Count == 0 ? null : PdfValue.OwnedArray(values);
        }

        if (node.Kind == PdfValueKind.Number)
            return inheritedPage.Kind == PdfValueKind.Reference && Selected.ContainsKey(inheritedPage.ReferenceId) ? node : null;
        if (node.Kind != PdfValueKind.Dictionary)
            throw Error("Organization.StructureNode", "Structure children must be elements or marked/object references.");
        var page = node.Entries.TryGetValue("Pg", out var explicitPage) ? explicitPage : inheritedPage;
        bool selected = page.Kind == PdfValueKind.Reference && Selected.ContainsKey(page.ReferenceId);
        if (Name(node, "Type") == "MCR" || Name(node, "Type") == "OBJR")
        {
            if (!selected)
                return null;
            var values = CloneEntries(node);
            values["Pg"] = Clone(page);
            return PdfValue.OwnedDictionary(values);
        }

        if (!node.Entries.ContainsKey("S") || declared.Kind != PdfValueKind.Reference)
            throw Error("Organization.StructureElement", "Structure elements require an indirect identity and a role.");
        if (!_structureVisiting.Add(declared.ReferenceId))
            throw Error("Organization.StructureCycle", "Structure elements must form a tree of unique nodes.");
        var id = Engine.Edit.Add(PdfValue.Null);
        Map[declared.ReferenceId] = id;
        var reference = PdfValue.Reference(id);
        var children = node.Entries.TryGetValue("K", out var k) ? StructureNode(k, page, reference) : null;
        if (children == null && !selected)
        {
            _prunedStructure.Add(declared.ReferenceId);
            return null;
        }

        var entries = CloneEntries(node, "P", "K", "Pg");
        entries["P"] = parent;
        if (children != null)
            entries["K"] = children;
        if (selected)
            entries["Pg"] = Clone(page);
        Engine.Edit.Set(id, PdfValue.OwnedDictionary(entries));
        return reference;
    }

    private readonly HashSet<PdfObjectId> _structureVisiting = new();
    internal void FinishStructureParents()
    {
        if (_structureRoot == null)
        {
            if (ParentKeys.Count > 0)
                throw Error("Organization.StructureRoot", "Structure parent keys have no structure root.");
            return;
        }

        var parents = _structureRoot.Entries.TryGetValue("ParentTree", out var declared) ? NumberTree(declared) : new();
        foreach (var pair in ParentKeys.OrderBy(p => p.Value).ToArray())
        {
            if (!parents.TryGetValue(pair.Key, out var value))
                throw Error("Organization.ParentTree", "A retained structure parent key has no parent-tree association.");
            Context.Consume(PdfResourceKind.ExpandedBytes, 64);
            Engine.ParentNumbers.Add(PdfValue.Integer(pair.Value));
            Engine.ParentNumbers.Add(Clone(value));
        }
    }
}
