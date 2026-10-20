namespace Fluent.Pdf;
internal sealed class PdfStructureWriter
{
    private readonly PdfGraphEdit _g;
    private readonly PdfStructureReader _r;
    private readonly PdfStructurePlan _plan;
    private readonly Dictionary<(PdfObjectId Owner, int Mcid), PdfObjectId> _marked = new();
    private readonly Dictionary<PdfObjectId, PdfObjectId> _annotations = new();
    private readonly Dictionary<string, (PdfObjectId Id, string Role)> _ids = new(StringComparer.Ordinal);
    private readonly List<(string Role, string? Id, PdfStructureTableAttributes Attributes)> _tables = new();
    private readonly HashSet<PdfStructureElement> _definitions = new();
    private readonly PdfObjectId _root;
    private PdfObjectId? _namespace;
    internal PdfStructureWriter(PdfGraphEdit graph, PdfStructurePlan plan, PdfProviderRegistry providers)
    {
        _g = graph;
        _plan = plan;
        _providers = providers;
        _r = new(graph, plan.Profile, providers, false);
        _r.Tick(4096);
        _root = graph.Edit.Add(PdfValue.Null);
    }

    internal void Write()
    {
        if (_g.Catalog.Entries.ContainsKey("StructTreeRoot"))
        {
            if (!_plan.ReplaceExisting)
                throw _r.Error("ExistingStructure", "Replacing existing semantics requires an explicit complete author plan and replacement choice.");
            _g.Context.Report(new("Accessibility.ReplaceStructure", "The explicitly selected complete structure plan replaces existing semantic structure; author review is required.", lossCategory: PdfLossCategory.FidelityReduction));
        }

        if (_plan.Profile == PdfAccessibilityProfile.Ua2)
            _namespace = _g.Edit.Add(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Namespace")), ("NS", PdfTextStrings.Encode(PdfStructureRules.Pdf2Namespace, _g.Context))));
        var element = Element(_plan.Root, _root);
        if (_marked.Count != _r.Actual.Count)
            throw _r.Error("PlanCoverage", "The complete author plan must cover every actual page/Form MCID exactly once.");
        foreach (var table in _tables)
            foreach (var id in table.Attributes.Headers)
                if (!_ids.TryGetValue(id, out var target) || target.Role != "TH")
                    throw _r.Error("HeaderIdentity", "Table header IDs must identify explicit TH elements in this plan.");
        _r.Tick(_tables.Count * 128L + 256);
        var headers = _tables.Where(t => t.Role == "TH" && t.Id != null).ToDictionary(t => t.Id!, t => t.Attributes, StringComparer.Ordinal);
        var done = new HashSet<string>(StringComparer.Ordinal);
        var path = new HashSet<string>(StringComparer.Ordinal);
        void Header(string id)
        {
            _r.Tick();
            using var depth = _g.Context.EnterScope(PdfResourceKind.Depth);
            if (done.Contains(id))
                return;
            if (!path.Add(id))
                throw _r.Error("HeaderCycle", "Header relationships must be acyclic.");
            if (headers.TryGetValue(id, out var attributes))
                foreach (var child in attributes.Headers)
                    Header(child);
            path.Remove(id);
            done.Add(id);
        }

        foreach (var header in headers.Keys)
            Header(header);
        var nums = new List<PdfValue>();
        int key = 0;
        _r.Tick(_marked.Count * 64L + 256);
        var claimsByOwner = _marked.ToLookup(x => x.Key.Owner);
        foreach (var pair in _r.Owners)
        {
            _r.Tick();
            var claims = claimsByOwner[pair.Key];
            var dictionary = _g.Copy(pair.Value);
            dictionary.Remove("StructParents");
            if (claims.Any())
            {
                int length = claims.Max(x => x.Key.Mcid) + 1;
                _r.Tick(length * 16L + 128);
                var values = Enumerable.Repeat(PdfValue.Null, length).ToList();
                foreach (var claim in claims)
                    values[claim.Key.Mcid] = PdfValue.Reference(claim.Value);
                dictionary["StructParents"] = PdfValue.Integer(key);
                nums.Add(PdfValue.Integer(key++));
                nums.Add(PdfValue.OwnedArray(values));
            }

            _g.Edit.Set(pair.Key, Value(pair.Value, dictionary));
        }

        foreach (var pair in _r.Annotations)
        {
            _r.Tick();
            var original = _g.Resolve(PdfValue.Reference(pair.Key));
            var dictionary = _g.Copy(original);
            dictionary.Remove("StructParent");
            if (_annotations.TryGetValue(pair.Key, out var owner))
            {
                dictionary["StructParent"] = PdfValue.Integer(key);
                nums.Add(PdfValue.Integer(key++));
                nums.Add(PdfValue.Reference(owner));
            }

            _g.Edit.Set(pair.Key, PdfValue.OwnedDictionary(dictionary));
        }

        var root = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
        {
            ["Type"] = PdfValue.Name("StructTreeRoot"),
            ["K"] = PdfValue.Reference(element),
            ["ParentTree"] = PdfValue.Reference(_g.Edit.Add(PdfGraphicsNumbers.Dict(("Nums", PdfValue.OwnedArray(nums))))),
            ["ParentTreeNextKey"] = PdfValue.Integer(key)
        };
        if (_namespace != null)
            root["Namespaces"] = PdfValue.Array(PdfValue.Reference(_namespace.Value));
        if (_ids.Count > 0)
        {
            _r.Tick(_ids.Count * 64L + 256);
            var names = new List<PdfValue>();
            foreach (var id in _ids.OrderBy(x => x.Key, StringComparer.Ordinal))
            {
                names.Add(PdfTextStrings.Encode(id.Key, _g.Context));
                names.Add(PdfValue.Reference(id.Value.Id));
            }

            root["IDTree"] = PdfValue.Reference(_g.Edit.Add(PdfGraphicsNumbers.Dict(("Names", PdfValue.OwnedArray(names)))));
        }

        _g.Edit.Set(_root, PdfValue.OwnedDictionary(root));
        var catalog = _g.Copy(_g.Catalog);
        catalog["StructTreeRoot"] = PdfValue.Reference(_root);
        catalog["Lang"] = PdfTextStrings.Encode(_plan.Language, _g.Context);
        var declaredMark = _g.Get(_g.Catalog, "MarkInfo");
        if (declaredMark.Kind != PdfValueKind.Null && declaredMark.Kind != PdfValueKind.Dictionary)
            throw _r.Error("MarkInfo", "The source MarkInfo entry must be a dictionary.");
        var mark = _g.Copy(declaredMark);
        mark["Marked"] = PdfValue.Boolean(true);
        mark["Suspects"] = PdfValue.Boolean(false);
        catalog["MarkInfo"] = PdfValue.OwnedDictionary(mark);
        _g.SetCatalog(catalog);
        // Validate exact new associations under the same root before model/output commit.
        _ = new PdfStructureReader(_g, _plan.Profile, _providers);
    }

    private readonly PdfProviderRegistry _providers;
    private PdfObjectId Element(PdfStructureElement definition, PdfObjectId parent)
    {
        using var depth = _g.Context.EnterScope(PdfResourceKind.Depth);
        _r.Tick(definition.Items.Count * 32L + 512);
        if (!_definitions.Add(definition))
            throw _r.Error("DefinitionReuse", "Each immutable semantic element must appear exactly once in the authored tree.");
        var id = _g.Edit.Add(PdfValue.Null);
        var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
        {
            ["Type"] = PdfValue.Name("StructElem"),
            ["S"] = PdfValue.Name(definition.Role),
            ["P"] = PdfValue.Reference(parent)
        };
        void Text(string key, string? text)
        {
            if (text != null)
                entries[key] = PdfTextStrings.Encode(text, _g.Context);
        }

        Text("Lang", definition.Language);
        Text("Alt", definition.AlternativeText);
        Text("ActualText", definition.ActualText);
        Text("ID", definition.Identifier);
        if (_namespace != null)
            entries["NS"] = PdfValue.Reference(_namespace.Value);
        if (definition.Identifier != null)
        {
            if (_ids.ContainsKey(definition.Identifier))
                throw _r.Error("ElementIdentity", "Element IDs must be unique.");
            _ids.Add(definition.Identifier, (id, definition.Role));
        }

        if (definition.Table != null)
        {
            entries["A"] = Table(definition.Table, _g.Context);
            _tables.Add((definition.Role, definition.Identifier, definition.Table));
        }

        var children = new List<PdfValue>();
        foreach (var item in definition.Items)
        {
            _r.Tick();
            if (item is PdfStructureElement child)
                children.Add(PdfValue.Reference(Element(child, id)));
            else if (item is PdfStructureContent content)
            {
                if (content.PageIndex >= _g.Snapshot.Pages.Count)
                    throw _r.Error("PlanPage", "The author plan references a page outside its source.");
                var page = _g.Snapshot.Pages[content.PageIndex].Source!.ObjectId;
                if (content.AnnotationId != null)
                {
                    var annotation = content.AnnotationId.Value;
                    if (!_r.Annotations.TryGetValue(annotation, out int index) || index != content.PageIndex || _annotations.ContainsKey(annotation))
                        throw _r.Error("PlanAnnotation", "Annotation ownership/page must match exactly once.");
                    string? subtype = _r.Name(_g.Resolve(PdfValue.Reference(annotation)), "Subtype");
                    if (subtype == "Link" && definition.Role != "Link" || subtype == "Widget" && definition.Role != "Form")
                        throw _r.Error("AnnotationRole", "Links and widgets require explicit Link and Form semantic owners.");
                    _annotations.Add(annotation, id);
                    children.Add(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("OBJR")), ("Pg", PdfValue.Reference(page)), ("Obj", PdfValue.Reference(annotation))));
                }
                else
                {
                    var owner = content.StreamId ?? page;
                    var key = (owner, content.Mcid);
                    if (!_r.Actual.TryGetValue(key, out var actual) || actual.PageIndex != content.PageIndex || _marked.ContainsKey(key))
                        throw _r.Error("PlanMcid", "The plan requires an actual page/Form MCID with unique semantic ownership.");
                    _marked.Add(key, id);
                    var mcr = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
                    {
                        ["Type"] = PdfValue.Name("MCR"),
                        ["Pg"] = PdfValue.Reference(page),
                        ["MCID"] = PdfValue.Integer(content.Mcid)
                    };
                    if (content.StreamId != null)
                        mcr["Stm"] = PdfValue.Reference(content.StreamId.Value);
                    children.Add(PdfValue.OwnedDictionary(mcr));
                }
            }
        }

        entries["K"] = PdfValue.OwnedArray(children);
        _g.Edit.Set(id, PdfValue.OwnedDictionary(entries));
        return id;
    }

    internal static PdfValue Table(PdfStructureTableAttributes attributes, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, attributes.Headers.Count * 64L + 512);
        c.Consume(PdfResourceKind.Objects, attributes.Headers.Count + 1L);
        var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
        {
            ["O"] = PdfValue.Name("Table")
        };
        if (attributes.RowSpan != 1)
            entries["RowSpan"] = PdfValue.Integer(attributes.RowSpan);
        if (attributes.ColumnSpan != 1)
            entries["ColSpan"] = PdfValue.Integer(attributes.ColumnSpan);
        if (attributes.HeaderScope != null)
            entries["Scope"] = PdfValue.Name(attributes.HeaderScope);
        if (attributes.Headers.Count != 0)
            entries["Headers"] = PdfValue.OwnedArray(attributes.Headers.Select(id => PdfTextStrings.Encode(id, c)).ToList());
        if (attributes.Summary != null)
            entries["Summary"] = PdfTextStrings.Encode(attributes.Summary, c);
        return PdfValue.OwnedDictionary(entries);
    }

    private static PdfValue Value(PdfValue original, Dictionary<string, PdfValue> entries) => original.WithDictionary(PdfValue.OwnedDictionary(entries));
}
