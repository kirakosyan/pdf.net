namespace Fluent.Pdf;
// Original passive ISO structure associations. Automated source checks never substitute for complete independent PDF/UA validation or human review.
internal sealed class PdfStructureReader
{
    internal readonly PdfGraphEdit Graph;
    internal readonly PdfAccessibilityProfile Profile;
    internal readonly List<PdfStructureElementInfo> Elements = new();
    internal readonly List<PdfStructureContent> Content = new();
    internal readonly List<PdfAccessibilityFinding> Findings = new();
    internal readonly Dictionary<(PdfObjectId Owner, int Mcid), PdfStructureContent> Actual = new();
    internal readonly Dictionary<PdfObjectId, int> Pages = new();
    internal readonly Dictionary<PdfObjectId, int> Annotations = new();
    internal readonly Dictionary<PdfObjectId, PdfValue> Owners = new();
    internal readonly Dictionary<(PdfObjectId Owner, int Mcid), PdfObjectId> Claims = new();
    internal readonly Dictionary<PdfObjectId, PdfObjectId> AnnotationClaims = new();
    private readonly Dictionary<int, PdfValue> _parents = new();
    private readonly HashSet<PdfObjectId> _elements = new(), _numberNodes = new(), _forms = new();
    private readonly PdfProviderRegistry _providers;
    private PdfValue _roleMap = PdfValue.Null;
    private PdfOperationContext C => Graph.Context;

    internal string? Language;
    internal PdfStructureReader(PdfGraphEdit graph, PdfAccessibilityProfile profile, PdfProviderRegistry providers, bool structure = true)
    {
        Graph = graph;
        Profile = profile;
        _providers = providers;
        for (int i = 0; i < graph.Snapshot.Pages.Count; i++)
        {
            Tick();
            var page = graph.Snapshot.Pages[i].Source ?? throw Error("PageIdentity", "An immutable source page identity is required.");
            if (!Put(Pages, page.ObjectId, i))
                throw Error("PageIdentity", "Page identities must be unique.");
            Owners.Add(page.ObjectId, Resolve(PdfValue.Reference(page.ObjectId)));
            var annotations = Get(Owners[page.ObjectId], "Annots");
            if (annotations.Kind != PdfValueKind.Null && annotations.Kind != PdfValueKind.Array)
                throw Error("Annotations", "Page annotations must be an array.");
            if (annotations.Kind == PdfValueKind.Array)
                foreach (var raw in annotations.Items)
                {
                    Tick();
                    if (raw.Kind != PdfValueKind.Reference || !Put(Annotations, raw.ReferenceId, i))
                        throw Error("AnnotationIdentity", "Annotation identities must belong to exactly one page.");
                    var annotation = Resolve(raw);
                    if (annotation.Kind != PdfValueKind.Dictionary)
                        throw Error("AnnotationIdentity", "An annotation must be a dictionary.");
                }

            var resources = page.EffectiveAttributes.Entries.TryGetValue("Resources", out var declared) ? Resolve(declared) : PdfValue.Null;
            Scan(PdfPageContent.Read(page, PdfUnknownOperatorPolicy.Reject, null, providers, C), resources, i, page.ObjectId, null, false, false);
        }

        Language = Text(Get(graph.Catalog, "Lang"));
        if (Language == null)
            Finding("Language", "Catalog", "An explicit document language is missing.");
        else
        {
            try
            {
                PdfStructureRules.Language(Language);
            }
            catch (ArgumentException)
            {
                Finding("Language", "Catalog", "The declared document language is outside the bounded language-tag profile.");
            }
        }

        if (structure)
            ReadStructure();
    }

    private void Scan(PdfContentProgram program, PdfValue resources, int page, PdfObjectId owner, PdfObjectId? stream, bool inheritedMarked, bool inheritedArtifact)
    {
        using var depth = C.EnterScope(PdfResourceKind.Depth);
        Tick();
        var scopes = new Stack<(bool Marked, bool Artifact)>();
        bool marked = inheritedMarked, artifact = inheritedArtifact;
        foreach (var op in program.Operations)
        {
            Tick();
            if (op.Operation == PdfOperator.BeginMarkedContent || op.Operation == PdfOperator.BeginMarkedContentProperties)
            {
                scopes.Push((marked, artifact));
                if (op.Operands[0].NameValue == "Artifact")
                    artifact = true;
                if (op.Operation == PdfOperator.BeginMarkedContentProperties)
                {
                    var properties = op.Operands[1];
                    if (properties.Kind == PdfValueKind.Name)
                        properties = Get(Get(resources, "Properties"), properties.NameValue);
                    else
                        properties = Resolve(properties);
                    var id = Get(properties, "MCID");
                    if (id.Kind != PdfValueKind.Null)
                    {
                        int mcid = Integer(id);
                        if (artifact || inheritedMarked)
                            throw Error("MarkedNesting", "Artifact or inherited whole-Form content cannot contain a second structural MCID association.");
                        var content = PdfStructureContent.MarkedContent(page, mcid, stream);
                        if (!Put(Actual, (owner, mcid), content))
                            throw Error("DuplicateMcid", "A content owner has duplicate MCIDs or a tagged Form is invoked more than once.");
                        marked = true;
                    }
                }

                continue;
            }

            if (op.Operation == PdfOperator.EndMarkedContent)
            {
                if (scopes.Count == 0)
                    throw Error("MarkedBalance", "Marked-content scopes must balance.");
                (marked, artifact) = scopes.Pop();
                continue;
            }

            if (op.Operation == PdfOperator.DrawObject)
            {
                var raw = Raw(Get(resources, "XObject"), op.Operands[0].NameValue);
                var obj = Resolve(raw);
                if (Name(obj, "Subtype") == "Form")
                {
                    if (raw.Kind != PdfValueKind.Reference || !_forms.Add(raw.ReferenceId))
                        throw Error("FormCycle", "Forms require explicit identities and acyclic invocation paths.");
                    try
                    {
                        var child = Raw(obj, "Resources");
                        if (child.Kind == PdfValueKind.Null)
                            child = resources;
                        else
                            child = Resolve(child);
                        Owners[raw.ReferenceId] = obj;
                        var parsed = PdfContentProgram.ParseCore(PdfExtractionData.Read(obj, Graph, _providers), PdfContentKind.Page, PdfUnknownOperatorPolicy.Reject, null, C, color => PdfImageColorResolver.Components(color, child, Resolve, C));
                        Scan(parsed, child, page, raw.ReferenceId, raw.ReferenceId, marked, artifact);
                    }
                    finally
                    {
                        _forms.Remove(raw.ReferenceId);
                    }

                    continue;
                }
            }

            if (!marked && !artifact && Paints(op.Operation))
                Finding("UnmarkedPainting", "page:" + page, "Painting outside structural marked content or artifacts requires explicit author remediation.");
        }

        if (scopes.Count != 0)
            throw Error("MarkedBalance", "Marked-content scopes must balance.");
    }

    private static bool Paints(PdfOperator op) => op is PdfOperator.ShowText or PdfOperator.ShowPositionedText or PdfOperator.NextLineShowText or PdfOperator.SpacedNextLineShowText or PdfOperator.DrawObject or PdfOperator.Shading or PdfOperator.InlineImage or PdfOperator.Stroke or PdfOperator.CloseStroke or PdfOperator.Fill or PdfOperator.LegacyFill or PdfOperator.FillEvenOdd or PdfOperator.FillStroke or PdfOperator.FillStrokeEvenOdd or PdfOperator.CloseFillStroke or PdfOperator.CloseFillStrokeEvenOdd;
    private void ReadStructure()
    {
        var rootRaw = Raw(Graph.Catalog, "StructTreeRoot");
        if (rootRaw.Kind == PdfValueKind.Null)
        {
            Finding("StructureMissing", "Catalog", "A logical structure tree is missing.");
            return;
        }

        var root = Resolve(rootRaw);
        if (rootRaw.Kind != PdfValueKind.Reference || root.Kind != PdfValueKind.Dictionary || Name(root, "Type") != "StructTreeRoot")
            throw Error("Root", "The structure root requires an indirect dictionary identity and type.");
        _roleMap = Get(root, "RoleMap");
        if (_roleMap.Kind != PdfValueKind.Null)
        {
            if (_roleMap.Kind != PdfValueKind.Dictionary) throw Error("RoleMap", "RoleMap must be a dictionary of named role mappings.");
            foreach (var mapping in _roleMap.Entries)
            {
                Tick();
                if (PdfStructureRules.StandardRole(mapping.Key))
                {
                    Finding("RoleMap", "role:" + mapping.Key, "A standard structure role cannot be remapped; this mapping is ignored and requires remediation.");
                    continue;
                }
                _ = EffectiveRole(mapping.Key);
            }
        }
        var mark = Get(Get(Graph.Catalog, "MarkInfo"), "Marked");
        if (mark.Kind != PdfValueKind.Boolean || !mark.BooleanValue)
            Finding("MarkInfo", "Catalog", "Tagged source must declare MarkInfo.Marked.");
        Visit(Raw(root, "K"), rootRaw.ReferenceId, null);
        ReadParents(Raw(root, "ParentTree"));
        var next = Get(root, "ParentTreeNextKey");
        if (next.Kind != PdfValueKind.Null && Integer(next) <= (_parents.Count == 0 ? -1 : _parents.Keys.Max()))
            throw Error("ParentNextKey", "The parent-tree next key must exceed all existing keys.");
        var used = new HashSet<int>();
        foreach (var pair in Owners)
        {
            Tick();
            var keyValue = Get(pair.Value, "StructParents");
            Tick(Claims.Count * 48L + 32);
            var claimed = Claims.Where(x => x.Key.Owner == pair.Key).ToArray();
            if (keyValue.Kind == PdfValueKind.Null)
            {
                if (claimed.Length != 0)
                    throw Error("ParentOwner", "Marked content has no owner parent-tree key.");
                continue;
            }

            int key = Integer(keyValue);
            if (!used.Add(key) || !_parents.TryGetValue(key, out var raw))
                throw Error("ParentOwner", "Content-owner parent-tree keys must be unique and present.");
            var values = Resolve(raw);
            if (values.Kind != PdfValueKind.Array || values.Items.Count > 1000001)
                throw Error("ParentArray", "A marked-content owner requires a bounded parent array.");
            for (int i = 0; i < values.Items.Count; i++)
            {
                Tick();
                bool exists = Claims.TryGetValue((pair.Key, i), out var id);
                var value = values.Items[i];
                if (exists ? value.Kind != PdfValueKind.Reference || value.ReferenceId != id : value.Kind != PdfValueKind.Null)
                    throw Error("ParentAssociation", "A parent-tree MCID entry does not match its unique owning structure element.");
            }

            if (claimed.Any(x => x.Key.Mcid >= values.Items.Count))
                throw Error("ParentAssociation", "A claimed MCID is outside its parent array.");
        }

        foreach (var pair in Annotations)
        {
            Tick();
            var annotation = Resolve(PdfValue.Reference(pair.Key));
            var key = Get(annotation, "StructParent");
            bool claimed = AnnotationClaims.TryGetValue(pair.Key, out var owner);
            if (key.Kind == PdfValueKind.Null)
            {
                if (claimed)
                    throw Error("AnnotationParent", "A structured annotation has no parent-tree key.");
            }
            else if (!claimed || !used.Add(Integer(key)) || !_parents.TryGetValue(Integer(key), out var raw) || raw.Kind != PdfValueKind.Reference || raw.ReferenceId != owner)
                throw Error("AnnotationParent", "An annotation parent-tree key does not match its structure owner.");
            string? subtype = Name(annotation, "Subtype");
            if (!claimed && (subtype == "Link" || subtype == "Widget"))
                Finding("AnnotationUnassociated", "annotation:" + pair.Key, "Link and form widgets require explicit semantic structure associations.");
            if (subtype == "Widget" && Text(Get(annotation, "TU")) == null)
                Finding("FormDescription", "annotation:" + pair.Key, "Widget/field alternative-name inheritance requires complete form semantics and author review.");
        }

        if (used.Count != _parents.Count || Claims.Count != Actual.Count || Claims.Keys.Any(k => !Actual.ContainsKey(k)))
            throw Error("Coverage", "Structure, actual MCIDs and parent-tree associations must have identical unique coverage.");
        if (Elements.Count == 0)
            Finding("StructureEmpty", "StructTreeRoot", "A logical structure tree has no elements.");
        var unmappedRoles = new HashSet<string>(StringComparer.Ordinal);
        foreach (var element in Elements)
        {
            string role = EffectiveRole(element.Role);
            if (!PdfStructureRules.StandardRole(role) && !unmappedRoles.Contains(role))
            {
                Tick(role.Length * 2L + 64);
                unmappedRoles.Add(role);
                Finding("RoleMapping", "role:" + role, "The custom role needs an explicit standard-role mapping and semantic review.");
            }
            if ((role == "Figure" || role == "Formula" || role == "Form") && string.IsNullOrWhiteSpace(element.AlternativeText) && string.IsNullOrWhiteSpace(element.ActualText))
                Finding("AlternativeText", "element:" + element.ObjectId, "This semantic role requires meaningful author-provided alternative or replacement text.");
            if (role == "TH" && Get(Get(element.Dictionary, "A"), "Scope").Kind == PdfValueKind.Null && Get(Get(element.Dictionary, "A"), "Headers").Kind == PdfValueKind.Null)
                Finding("TableHeaders", "element:" + element.ObjectId, "Header relationships require explicit scope/IDs and human table review.");
        }

        if (Profile == PdfAccessibilityProfile.Ua2 && Get(root, "Namespaces").Kind != PdfValueKind.Array)
            Finding("Namespaces", "StructTreeRoot", "PDF/UA-2 needs explicit PDF2 structure namespaces and complete independent validation.");
        Finding("IndependentValidationRequired", "document", "Complete requested PDF/UA conformance requires an explicit independent validator; managed source findings are incomplete.");
    }

    internal string EffectiveRole(string role)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (!PdfStructureRules.StandardRole(role) && _roleMap.Kind == PdfValueKind.Dictionary && _roleMap.Entries.TryGetValue(role, out var raw))
        {
            Tick();
            if (!seen.Add(role)) throw Error("RoleMapCycle", "Role mappings must be acyclic.");
            var mapped = Resolve(raw);
            if (mapped.Kind != PdfValueKind.Name) throw Error("RoleMap", "Mapped roles must be names.");
            role = mapped.NameValue;
        }
        return role;
    }

    private void Visit(PdfValue raw, PdfObjectId parent, PdfObjectId? inheritedPage)
    {
        Tick();
        using var depth = C.EnterScope(PdfResourceKind.Depth);
        var node = Resolve(raw);
        if (node.Kind == PdfValueKind.Null)
            return;
        if (node.Kind == PdfValueKind.Array)
        {
            foreach (var child in node.Items)
                Visit(child, parent, inheritedPage);
            return;
        }

        if (node.Kind == PdfValueKind.Number)
        {
            Claim(inheritedPage, null, Integer(node), parent);
            return;
        }

        if (node.Kind != PdfValueKind.Dictionary)
            throw Error("Node", "Structure children must be elements, MCIDs or explicit references.");
        var page = Raw(node, "Pg");
        if (page.Kind != PdfValueKind.Null && (page.Kind != PdfValueKind.Reference || !Pages.ContainsKey(page.ReferenceId)))
            throw Error("Page", "Structure content must refer to a retained page.");
        var pg = page.Kind == PdfValueKind.Reference ? page.ReferenceId : inheritedPage;
        if (Name(node, "Type") == "MCR")
        {
            var stream = Raw(node, "Stm");
            if (stream.Kind != PdfValueKind.Null && stream.Kind != PdfValueKind.Reference || Raw(node, "StmOwn").Kind != PdfValueKind.Null)
                throw Error("McrProfile", "This source profile requires an explicit page/Form stream without appearance ownership.");
            Claim(pg, stream.Kind == PdfValueKind.Reference ? stream.ReferenceId : null, Integer(Get(node, "MCID")), parent);
            return;
        }

        if (Name(node, "Type") == "OBJR")
        {
            var obj = Raw(node, "Obj");
            if (pg == null || obj.Kind != PdfValueKind.Reference || !Annotations.TryGetValue(obj.ReferenceId, out int p) || Pages[pg.Value] != p || !Put(AnnotationClaims, obj.ReferenceId, parent))
                throw Error("ObjectReference", "Object references require a uniquely associated annotation on the declared page.");
            Content.Add(PdfStructureContent.Annotation(p, obj.ReferenceId));
            return;
        }

        if (raw.Kind != PdfValueKind.Reference || !_elements.Add(raw.ReferenceId) || Raw(node, "P").Kind != PdfValueKind.Reference || Raw(node, "P").ReferenceId != parent || Name(node, "S") == null)
            throw Error("Element", "Structure elements must be unique indirect nodes with consistent parents and named roles.");
        var kids = Resolve(Raw(node, "K"));
        Tick(kids.Kind == PdfValueKind.Array ? kids.Items.Count * 16L : 16);
        var values = kids.Kind == PdfValueKind.Array ? kids.Items.ToArray() : kids.Kind == PdfValueKind.Null ? Array.Empty<PdfValue>() : new[]
        {
            Raw(node, "K")
        };
        Elements.Add(new(raw.ReferenceId, node, Name(node, "S")!, Text(Get(node, "Lang")), Text(Get(node, "Alt")), Text(Get(node, "ActualText")), values));
        Visit(Raw(node, "K"), raw.ReferenceId, pg);
    }

    private void Claim(PdfObjectId? page, PdfObjectId? stream, int mcid, PdfObjectId owner)
    {
        if (page == null || !Pages.TryGetValue(page.Value, out int index))
            throw Error("ContentPage", "Content needs an explicit retained page.");
        var key = (stream ?? page.Value, mcid);
        if (!Actual.TryGetValue(key, out var actual) || actual.PageIndex != index || !_elements.Contains(owner) || !Put(Claims, key, owner))
            throw Error("ContentAssociation", "Every structural MCID must refer to actual uniquely owned content on the declared page.");
        Content.Add(actual);
    }

    private (int Min, int Max) ReadParents(PdfValue raw)
    {
        Tick();
        using var depth = C.EnterScope(PdfResourceKind.Depth);
        if (raw.Kind == PdfValueKind.Reference && !_numberNodes.Add(raw.ReferenceId))
            throw Error("ParentCycle", "Parent number-tree nodes must be unique.");
        var node = Resolve(raw);
        if (node.Kind != PdfValueKind.Dictionary)
            throw Error("ParentTree", "A bounded parent number tree is required.");
        var nums = Get(node, "Nums");
        var kids = Get(node, "Kids");
        int min = -1, max = -1;
        if (nums.Kind == PdfValueKind.Array && kids.Kind == PdfValueKind.Null && nums.Items.Count % 2 == 0)
        {
            for (int i = 0; i < nums.Items.Count; i += 2)
            {
                Tick();
                int key = Integer(Resolve(nums.Items[i]));
                if (key <= max || !Put(_parents, key, nums.Items[i + 1]))
                    throw Error("ParentOrder", "Parent-tree keys must be unique and strictly ordered.");
                if (min < 0)
                    min = key;
                max = key;
            }
        }
        else if (kids.Kind == PdfValueKind.Array && kids.Items.Count > 0 && nums.Kind == PdfValueKind.Null)
        {
            foreach (var child in kids.Items)
            {
                var extent = ReadParents(child);
                if (extent.Min < 0 || extent.Min <= max)
                    throw Error("ParentOrder", "Parent-tree ranges must be strictly ordered.");
                if (min < 0)
                    min = extent.Min;
                max = extent.Max;
            }
        }
        else
            throw Error("ParentTree", "A number tree requires either ordered Nums or nonempty Kids.");
        var limits = Get(node, "Limits");
        if (limits.Kind != PdfValueKind.Null && (limits.Kind != PdfValueKind.Array || limits.Items.Count != 2 || Integer(Resolve(limits.Items[0])) != min || Integer(Resolve(limits.Items[1])) != max))
            throw Error("ParentLimits", "Parent-tree limits must match actual child ranges.");
        return (min, max);
    }

    private static bool Put<K, V>(Dictionary<K, V> dictionary, K key, V value)
        where K : notnull
    {
        if (dictionary.ContainsKey(key))
            return false;
        dictionary.Add(key, value);
        return true;
    }

    internal PdfValue Resolve(PdfValue v) => Graph.Resolve(v);
    internal PdfValue Raw(PdfValue d, string key) => d.Kind == PdfValueKind.Dictionary || d.Kind == PdfValueKind.Stream ? d.Entries.TryGetValue(key, out var v) ? v : PdfValue.Null : PdfValue.Null;
    internal PdfValue Get(PdfValue d, string key) => Resolve(Raw(d, key));
    internal string? Name(PdfValue d, string key)
    {
        var v = Get(d, key);
        return v.Kind == PdfValueKind.Name ? v.NameValue : null;
    }

    internal string? Text(PdfValue v) => v.Kind == PdfValueKind.Null ? null : v.Kind == PdfValueKind.String ? PdfTextStrings.Decode(v, C) : throw Error("SemanticText", "Semantic text must be an explicit PDF text string.");
    internal int Integer(PdfValue v) => v.TryGetInteger(out long n) && n >= 0 && n <= 1000000 ? (int)n : throw Error("Integer", "A structure index must be a bounded nonnegative integer.");
    internal void Tick(long bytes = 256)
    {
        C.Checkpoint();
        C.Consume(PdfResourceKind.Objects, 1);
        C.Consume(PdfResourceKind.ExpandedBytes, bytes);
    }

    private void Finding(string code, string location, string message)
    {
        Tick(512);
        if (Findings.Count == 10000)
            throw Error("FindingLimit", "The bounded source finding inventory was exhausted.");
        Findings.Add(new("Accessibility." + code, location, message));
    }

    internal PdfException Error(string code, string message) => Graph.Error("Accessibility." + code, message);
}
