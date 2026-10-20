namespace Fluent.Pdf;
// Structural source profile for browser page MCIDs. It deliberately makes no PDF/UA claim.
internal static class PdfHtmlTagStructure
{
    internal static void Validate(PdfDocumentSnapshot snapshot, PdfProviderRegistry providers, PdfOperationContext context)
    {
        using var graph = new PdfGraphEdit(snapshot, new(), context);
        var catalog = graph.Catalog;
        var marked = Get(Get(catalog, "MarkInfo"), "Marked");
        var rootReference = Raw(catalog, "StructTreeRoot");
        var root = Resolve(rootReference);
        if (marked.Kind != PdfValueKind.Boolean || !marked.BooleanValue || rootReference.Kind != PdfValueKind.Reference || !Named(root, "Type", "StructTreeRoot"))
            throw Error();
        context.Consume(PdfResourceKind.ExpandedBytes, snapshot.Pages.Count * 256L + 2048);
        var pages = new Dictionary<PdfObjectId, int>();
        var pageKeys = new Dictionary<int, PdfObjectId>();
        var actual = new HashSet<(PdfObjectId Page, int Mcid)>();
        var owners = new Dictionary<(PdfObjectId Page, int Mcid), PdfObjectId>();
        var elements = new HashSet<PdfObjectId>();
        var parentEntries = new Dictionary<int, PdfValue>();
        var numberNodes = new HashSet<PdfObjectId>();
        foreach (var page in snapshot.Pages)
        {
            Tick();
            var source = page.Source ?? throw Error();
            if (pages.ContainsKey(source.ObjectId))
                throw Error();
            pages.Add(source.ObjectId, pages.Count);
            var parent = Get(source.Dictionary, "StructParents");
            if (parent.Kind != PdfValueKind.Null)
            {
                int key = Integer(parent);
                if (pageKeys.ContainsKey(key))
                    throw Error();
                pageKeys.Add(key, source.ObjectId);
            }

            var resources = source.EffectiveAttributes.Entries.TryGetValue("Resources", out var r) ? Resolve(r) : PdfValue.Null;
            foreach (var op in PdfPageContent.Read(source, PdfUnknownOperatorPolicy.Reject, null, providers, context).Operations)
            {
                Tick();
                if (op.Operation == PdfOperator.DrawObject)
                {
                    var obj = Get(Get(resources, "XObject"), op.Operands[0].NameValue);
                    if (Named(obj, "Subtype", "Form"))
                        throw Profile();
                }

                if (op.Operation != PdfOperator.BeginMarkedContentProperties)
                    continue;
                var properties = op.Operands[1];
                if (properties.Kind == PdfValueKind.Name)
                    properties = Get(Get(resources, "Properties"), properties.NameValue);
                var mcid = Get(properties, "MCID");
                if (mcid.Kind == PdfValueKind.Null)
                    continue;
                if (op.Operands[0].NameValue == "Artifact" || parent.Kind == PdfValueKind.Null || !actual.Add((source.ObjectId, Integer(mcid))))
                    throw Error();
            }
        }

        VisitKids(Raw(root, "K"), rootReference.ReferenceId, null);
        if (elements.Count == 0)
            throw Error();
        ReadNumberTree(Raw(root, "ParentTree"));
        var nextKey = Get(root, "ParentTreeNextKey");
        if (nextKey.Kind != PdfValueKind.Null && Integer(nextKey) <= (parentEntries.Count == 0 ? -1 : parentEntries.Keys.Max()))
            throw Error();
        if (parentEntries.Count != pageKeys.Count || owners.Count != actual.Count)
            throw Error();
        foreach (var pair in parentEntries)
        {
            Tick();
            if (!pageKeys.TryGetValue(pair.Key, out var page))
                throw Profile();
            var entries = Resolve(pair.Value);
            if (entries.Kind != PdfValueKind.Array || entries.Items.Count > 100000)
                throw Error();
            for (int i = 0; i < entries.Items.Count; i++)
            {
                Tick();
                var owner = entries.Items[i];
                bool claimed = owners.TryGetValue((page, i), out var expected);
                if (owner.Kind == PdfValueKind.Null)
                {
                    if (claimed)
                        throw Error();
                }
                else if (!claimed || owner.Kind != PdfValueKind.Reference || owner.ReferenceId != expected)
                    throw Error();
            }
        }

        foreach (var pair in owners)
        {
            Tick();
            if (!actual.Contains(pair.Key))
                throw Error();
            int key = Integer(Get(snapshot.Pages[pages[pair.Key.Page]].Source!.Dictionary, "StructParents"));
            if (!parentEntries.TryGetValue(key, out var entry))
                throw Error();
            var array = Resolve(entry);
            if (array.Kind != PdfValueKind.Array || pair.Key.Mcid >= array.Items.Count)
                throw Error();
        }

        void VisitKids(PdfValue value, PdfObjectId parent, PdfObjectId? inheritedPage)
        {
            Tick();
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            if (value.Kind == PdfValueKind.Null)
                return;
            if (value.Kind == PdfValueKind.Array)
            {
                foreach (var item in value.Items)
                    VisitKids(item, parent, inheritedPage);
                return;
            }

            if (value.Kind == PdfValueKind.Number)
            {
                Claim(inheritedPage, Integer(value), parent);
                return;
            }

            var resolved = Resolve(value);
            if (resolved.Kind != PdfValueKind.Dictionary)
                throw Error();
            PdfObjectId? page = inheritedPage;
            var pg = Raw(resolved, "Pg");
            if (pg.Kind != PdfValueKind.Null)
            {
                if (pg.Kind != PdfValueKind.Reference || !pages.ContainsKey(pg.ReferenceId))
                    throw Error();
                page = pg.ReferenceId;
            }

            if (Named(resolved, "Type", "MCR"))
            {
                if (Raw(resolved, "Stm").Kind != PdfValueKind.Null)
                    throw Profile();
                Claim(page, Integer(Get(resolved, "MCID")), parent);
                return;
            }

            if (Named(resolved, "Type", "OBJR"))
                throw Profile();
            if (value.Kind != PdfValueKind.Reference || Raw(resolved, "Type").Kind != PdfValueKind.Null && !Named(resolved, "Type", "StructElem") || !elements.Add(value.ReferenceId))
                throw Error();
            var p = Raw(resolved, "P");
            if (p.Kind != PdfValueKind.Reference || p.ReferenceId != parent || Get(resolved, "S").Kind != PdfValueKind.Name)
                throw Error();
            VisitKids(Raw(resolved, "K"), value.ReferenceId, page);
        }

        void Claim(PdfObjectId? page, int mcid, PdfObjectId owner)
        {
            if (page == null || !pages.ContainsKey(page.Value) || !elements.Contains(owner) || owners.ContainsKey((page.Value, mcid)))
                throw Error();
            owners.Add((page.Value, mcid), owner);
        }

        (int Min, int Max) ReadNumberTree(PdfValue value)
        {
            Tick();
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            if (value.Kind == PdfValueKind.Reference && !numberNodes.Add(value.ReferenceId))
                throw Error();
            var node = Resolve(value);
            if (node.Kind != PdfValueKind.Dictionary)
                throw Error();
            var nums = Get(node, "Nums");
            var kids = Get(node, "Kids");
            int min = -1, max = -1;
            if (nums.Kind == PdfValueKind.Array && kids.Kind == PdfValueKind.Null)
            {
                if (nums.Items.Count % 2 != 0)
                    throw Error();
                for (int i = 0; i < nums.Items.Count; i += 2)
                {
                    Tick();
                    int key = Integer(Resolve(nums.Items[i]));
                    if (key <= max || parentEntries.ContainsKey(key))
                        throw Error();
                    if (min < 0)
                        min = key;
                    max = key;
                    parentEntries.Add(key, nums.Items[i + 1]);
                }
            }
            else if (kids.Kind == PdfValueKind.Array && nums.Kind == PdfValueKind.Null && kids.Items.Count > 0)
            {
                foreach (var child in kids.Items)
                {
                    var extent = ReadNumberTree(child);
                    if (extent.Min < 0 || extent.Min <= max)
                        throw Error();
                    if (min < 0)
                        min = extent.Min;
                    max = extent.Max;
                }
            }
            else
                throw Error();
            var limits = Get(node, "Limits");
            if (limits.Kind != PdfValueKind.Null && (limits.Kind != PdfValueKind.Array || limits.Items.Count != 2 || Integer(Resolve(limits.Items[0])) != min || Integer(Resolve(limits.Items[1])) != max))
                throw Error();
            return (min, max);
        }

        PdfValue Resolve(PdfValue value) => graph.Resolve(value);
        PdfValue Raw(PdfValue dictionary, string key) => dictionary.Kind == PdfValueKind.Dictionary || dictionary.Kind == PdfValueKind.Stream ? dictionary.Entries.TryGetValue(key, out var value) ? value : PdfValue.Null : PdfValue.Null;
        PdfValue Get(PdfValue dictionary, string key) => Resolve(Raw(dictionary, key));
        bool Named(PdfValue dictionary, string key, string name)
        {
            var value = Get(dictionary, key);
            return value.Kind == PdfValueKind.Name && value.NameValue == name;
        }

        int Integer(PdfValue value) => value.TryGetInteger(out long n) && n >= 0 && n <= int.MaxValue ? (int)n : throw Error();
        void Tick()
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 192);
        }

        PdfException Error() => graph.Error("Html.TagStructure", "Requested tagged output has missing, cyclic, duplicate or inconsistent page/structure/parent-tree associations.");
        PdfException Profile() => graph.Error("Html.TagStructureProfile", "This structural source profile requires page MCIDs without Form-stream or object-reference structure associations. Full structure profiles remain unqualified.");
    }
}
