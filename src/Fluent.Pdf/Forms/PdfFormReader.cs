namespace Fluent.Pdf;
// Original passive field-tree capture with explicit inherited attributes and exact widget identities.
internal sealed class PdfFormReader
{
    internal readonly PdfGraphEdit Graph;
    internal readonly PdfValue Dictionary;
    internal readonly List<PdfFormField> Fields = new();
    internal readonly Dictionary<PdfObjectId, PdfFormField> ById = new();
    internal readonly Dictionary<PdfObjectId, int> Pages = new();
    internal readonly Dictionary<PdfObjectId, PdfObjectId?> Parents = new();
    internal readonly Dictionary<string, PdfObjectId> NameOwners = new(StringComparer.Ordinal);
    private readonly HashSet<PdfObjectId> _visited = new();
    internal bool IsFieldIdentity(PdfObjectId id) => _visited.Contains(id);
    private readonly HashSet<PdfValue> _definitions = new();
    private readonly Dictionary<string, MutableField> _names = new(StringComparer.Ordinal);
    private PdfOperationContext Context => Graph.Context;

    private sealed class MutableField
    {
        internal readonly string Name;
        internal readonly PdfFormFieldKind Kind;
        internal readonly uint Flags;
        internal readonly PdfValue Value, Reset, Inherited;
        internal readonly List<PdfObjectId> Ids = new();
        internal readonly List<PdfFormWidget> Widgets = new();
        internal readonly PdfObjectId? NamedAncestor;
        internal MutableField(string name, PdfFormFieldKind kind, uint flags, PdfValue value, PdfValue reset, PdfValue inherited, PdfObjectId? ancestor)
        {
            Name = name;
            Kind = kind;
            Flags = flags;
            Value = value;
            Reset = reset;
            Inherited = inherited;
            NamedAncestor = ancestor;
        }
    }

    internal PdfFormReader(PdfGraphEdit graph)
    {
        Graph = graph;
        Dictionary = graph.Get(graph.Catalog, "AcroForm");
        if (Dictionary.Kind == PdfValueKind.Null)
            return;
        if (Dictionary.Kind != PdfValueKind.Dictionary)
            throw Error("Dictionary", "Catalog AcroForm must be a dictionary.");
        var need = graph.Get(Dictionary, "NeedAppearances");
        if (need.Kind != PdfValueKind.Null && need.Kind != PdfValueKind.Boolean)
            throw Error("NeedAppearances", "NeedAppearances must be Boolean.");
        var defaults = graph.Get(Dictionary, "DR");
        if (defaults.Kind != PdfValueKind.Null && defaults.Kind != PdfValueKind.Dictionary)
            throw Error("DefaultResources", "AcroForm DR must be a dictionary.");
        var defaultFonts = graph.Get(defaults, "Font");
        if (defaultFonts.Kind != PdfValueKind.Null && defaultFonts.Kind != PdfValueKind.Dictionary)
            throw Error("DefaultFonts", "Default font resources must be a dictionary.");
        var roots = graph.Get(Dictionary, "Fields");
        if (roots.Kind != PdfValueKind.Array)
            throw Error("Fields", "AcroForm requires an array of root fields.");
        if (Dictionary.Entries.ContainsKey("XFA"))
            Context.Report(new("Forms.XfaPresent", "The source contains passive XFA data; AcroForm edits require an explicit XFA synchronization/removal profile.", severity: PdfDiagnosticSeverity.Warning));
        for (int i = 0; i < graph.Snapshot.Pages.Count; i++)
        {
            var page = graph.Snapshot.Pages[i].Source!;
            var annotations = graph.Get(graph.Edit.Resolve(page.ObjectId), "Annots");
            if (annotations.Kind == PdfValueKind.Null)
                continue;
            if (annotations.Kind != PdfValueKind.Array)
                throw Error("Annotations", "Page Annots must resolve to an array.");
            foreach (var reference in annotations.Items)
            {
                Context.Checkpoint();
                if (reference.Kind != PdfValueKind.Reference)
                    throw Error("AnnotationIdentity", "Source annotation arrays require indirect identities.");
                var annotation = graph.Resolve(reference);
                if (annotation.Kind != PdfValueKind.Dictionary)
                    throw Error("AnnotationDictionary", "An annotation must be a dictionary.");
                if (Name(annotation, "Subtype") != "Widget")
                    continue;
                Context.Consume(PdfResourceKind.ExpandedBytes, 64);
                if (Pages.ContainsKey(reference.ReferenceId))
                    throw Error("SharedWidget", "One widget cannot belong to several page annotation positions.");
                Pages.Add(reference.ReferenceId, i);
            }
        }

        var initial = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        foreach (var key in new[]
        {
            "DA",
            "Q",
            "DR"
        }

        )
            if (Dictionary.Entries.TryGetValue(key, out var value))
                initial[key] = graph.Resolve(value);
        CheckSiblings(roots.Items);
        foreach (var root in roots.Items)
            Visit(root, null, "", initial, null);
        foreach (var mutable in _names.Values)
        {
            Context.Consume(PdfResourceKind.ExpandedBytes, mutable.Ids.Count * 64L + 128);
            var field = new PdfFormField(graph.Snapshot, mutable.Name, mutable.Kind, mutable.Flags, mutable.Value, mutable.Reset, mutable.Ids, mutable.Widgets, mutable.Inherited);
            Fields.Add(field);
            foreach (var id in mutable.Ids)
                ById.Add(id, field);
            if (mutable.NamedAncestor.HasValue)
                NameOwners.Add(mutable.Name, mutable.NamedAncestor.Value);
        }

        var seen = new HashSet<PdfObjectId>();
        var order = graph.Get(Dictionary, "CO");
        if (order.Kind != PdfValueKind.Null)
        {
            if (order.Kind != PdfValueKind.Array)
                throw Error("CalculationOrder", "CO must be an array of terminal field references.");
            foreach (var reference in order.Items)
            {
                Context.Consume(PdfResourceKind.ExpandedBytes, 48);
                if (reference.Kind != PdfValueKind.Reference || !ById.ContainsKey(reference.ReferenceId) || !seen.Add(reference.ReferenceId))
                    throw Error("CalculationOrder", "CO must reference distinct terminal fields.");
            }
        }

        foreach (var id in ById.Keys)
        {
            var actions = graph.Get(graph.Edit.Resolve(id), "AA");
            if (actions.Kind != PdfValueKind.Null && actions.Kind != PdfValueKind.Dictionary)
                throw Error("FieldActions", "Field AA must resolve to a dictionary.");
            if (graph.Get(actions, "C").Kind != PdfValueKind.Null && !seen.Contains(id))
                throw Error("CalculationOrder", "Every terminal field with a calculation action must appear in CO.");
            if (seen.Contains(id) && graph.Get(actions, "C").Kind == PdfValueKind.Null)
                throw Error("CalculationOrder", "CO entries must identify fields with calculation actions.");
        }
    }

    private void Visit(PdfValue reference, PdfValue? parent, string prefix, Dictionary<string, PdfValue> inherited, PdfObjectId? namedAncestor, PdfObjectId? parentIdentity = null)
    {
        Context.Checkpoint();
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        Context.Consume(PdfResourceKind.Objects, 1);
        Context.Consume(PdfResourceKind.ExpandedBytes, 192);
        if (reference.Kind != PdfValueKind.Reference || !_visited.Add(reference.ReferenceId))
            throw Error("FieldCycle", "Fields require a finite tree of unique indirect nodes.");
        Context.Consume(PdfResourceKind.ExpandedBytes, 64);
        Parents.Add(reference.ReferenceId, parentIdentity);
        var value = Graph.Resolve(reference);
        if (value.Kind != PdfValueKind.Dictionary || !_definitions.Add(value))
            throw Error("FieldIdentity", "A field node must be a unique dictionary.");
        var owner = Graph.Get(value, "Parent");
        if (parent == null ? owner.Kind != PdfValueKind.Null : !ReferenceEquals(owner, parent))
            throw Error("Parent", "Field Parent/Kids links must agree.");
        var properties = Graph.Copy(PdfValue.OwnedDictionary(inherited));
        foreach (var key in new[]
        {
            "FT",
            "Ff",
            "V",
            "DV",
            "DA",
            "Q",
            "MaxLen",
            "Opt",
            "I",
            "TI",
            "RV",
            "DS"
        }

        )
            if (value.Entries.TryGetValue(key, out var attribute))
            {
                var resolved = Graph.Resolve(attribute);
                if (resolved.Kind != PdfValueKind.Null)
                    properties[key] = resolved;
            }

        bool ownButtonScope = properties.TryGetValue("FT", out var optionType) && optionType.Kind == PdfValueKind.Name && optionType.NameValue == "Btn" && (Flags(properties.TryGetValue("Ff", out var optionFlags) ? optionFlags : PdfValue.Null) & 65536) == 0;
        if (!ownButtonScope && (!value.Entries.TryGetValue("Opt", out var ownOptions) || Graph.Resolve(ownOptions).Kind == PdfValueKind.Null))
            properties.Remove("Opt");
        var partial = Graph.Get(value, "T");
        string name = prefix;
        if (partial.Kind != PdfValueKind.Null)
        {
            string segment = PdfTextStrings.Decode(partial, Context);
            if (segment.Contains('.'))
                throw Error("PartialName", "A partial field name cannot contain a period.");
            Context.Consume(PdfResourceKind.ExpandedBytes, (prefix.Length + (long)segment.Length + 1) * 4 + 64);
            name = prefix.Length == 0 ? segment : prefix + "." + segment;
            namedAncestor = reference.ReferenceId;
        }

        var kids = Graph.Get(value, "Kids");
        bool widget = Name(value, "Subtype") == "Widget";
        if (kids.Kind != PdfValueKind.Null && (kids.Kind != PdfValueKind.Array || kids.Items.Count == 0))
            throw Error("Kids", "Field Kids must be a nonempty array.");
        if (widget && kids.Kind != PdfValueKind.Null)
            throw Error("MergedWidgetKids", "A merged field/widget must not contain Kids.");
        bool widgetsOnly = kids.Kind == PdfValueKind.Array && kids.Items.All(raw => PureWidget(Graph.Resolve(raw)));
        if (kids.Kind == PdfValueKind.Array && !widgetsOnly)
        {
            CheckSiblings(kids.Items);
            if (kids.Items.Any(raw => PureWidget(Graph.Resolve(raw))))
                throw Error("MixedKids", "A field cannot mix child fields and pure widget children.");
            var descendants = Graph.Copy(PdfValue.OwnedDictionary(properties));
            foreach (string key in new[]
            {
                "I",
                "TI",
                "RV",
                "DS"
            }

            )
                descendants.Remove(key);
            if (!ownButtonScope)
                descendants.Remove("Opt");
            foreach (var child in kids.Items)
                Visit(child, value, name, descendants, namedAncestor, reference.ReferenceId);
            return;
        }

        uint flags = Flags(properties.TryGetValue("Ff", out var rawFlags) ? rawFlags : PdfValue.Null);
        string type = properties.TryGetValue("FT", out var ft) && ft.Kind == PdfValueKind.Name ? ft.NameValue : "";
        var kind = Kind(type, flags);
        if (kind == PdfFormFieldKind.Unknown)
            Context.Report(new("Forms.UnsupportedType", "An unsupported source terminal field remains inspectable.", severity: PdfDiagnosticSeverity.Warning));
        var current = properties.TryGetValue("V", out var v) ? v : PdfValue.Null;
        var reset = properties.TryGetValue("DV", out var dv) ? dv : PdfValue.Null;
        if (!_names.TryGetValue(name, out var logical))
        {
            Context.Consume(PdfResourceKind.ExpandedBytes, 192);
            logical = new(name, kind, flags, current, reset, PdfValue.OwnedDictionary(properties), namedAncestor);
            _names.Add(name, logical);
        }
        else if (name.Length == 0 || logical.NamedAncestor != namedAncestor || logical.Kind != kind || !Equal(logical.Value, current) || !Equal(logical.Reset, reset) || logical.Flags != flags)
            throw Error("DuplicateName", "Same-name terminal representations must share a named ancestor, type, value, default and flags.");
        Context.Consume(PdfResourceKind.ExpandedBytes, 32);
        logical.Ids.Add(reference.ReferenceId);
        if (widget)
            AddWidget(reference, value, logical, value);
        if (widgetsOnly)
            foreach (var child in kids.Items)
            {
                if (child.Kind != PdfValueKind.Reference || !_visited.Add(child.ReferenceId))
                    throw Error("WidgetCycle", "Widget children require unique indirect identities.");
                Context.Consume(PdfResourceKind.ExpandedBytes, 64);
                Parents.Add(child.ReferenceId, reference.ReferenceId);
                var dictionary = Graph.Resolve(child);
                if (!_definitions.Add(dictionary))
                    throw Error("SharedWidget", "One widget cannot occur under several field nodes.");
                AddWidget(child, dictionary, logical, value);
            }
    }

    private void AddWidget(PdfValue reference, PdfValue dictionary, MutableField field, PdfValue parent)
    {
        Context.Checkpoint();
        Context.Consume(PdfResourceKind.Objects, 1);
        Context.Consume(PdfResourceKind.ExpandedBytes, 256);
        if (PureWidget(dictionary) && !ReferenceEquals(Graph.Get(dictionary, "Parent"), parent))
            throw Error("WidgetParent", "A widget Parent must match its terminal field.");
        int? page = Pages.TryGetValue(reference.ReferenceId, out int index) ? index : null;
        if (dictionary.Entries.TryGetValue("P", out var declared))
        {
            if (declared.Kind != PdfValueKind.Reference || page.HasValue && declared.ReferenceId != Graph.Snapshot.Pages[page.Value].Source!.ObjectId)
                throw Error("WidgetPage", "A widget page pointer must agree with its annotation membership.");
        }

        var rect = Graph.Get(dictionary, "Rect");
        var bounds = PdfAnnotation.ReadRect(rect, Graph.Resolve, Context);
        var appearance = Graph.Get(dictionary, "AP");
        if (appearance.Kind == PdfValueKind.Null)
            Context.Report(new("Forms.AppearanceMissing", "A source widget has no authored appearance; it remains inspectable.", severity: PdfDiagnosticSeverity.Warning));
        else if (appearance.Kind != PdfValueKind.Dictionary)
            throw Error("AppearanceDictionary", "Widget AP must resolve to a dictionary.");
        field.Widgets.Add(new(reference.ReferenceId, page, bounds, dictionary));
    }

    internal void Editable()
    {
        if (Dictionary.Kind != PdfValueKind.Null && Dictionary.Entries.ContainsKey("XFA"))
            throw Error("XfaProfile", "AcroForm edits require an explicit XFA synchronization/removal profile.");
        foreach (var id in _visited)
        {
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.Objects, 1);
            if (Graph.Edit.Resolve(id).Kind != PdfValueKind.Dictionary)
                throw Error("FieldAliasProfile", "Editing aliased field/widget identities requires an explicit canonical ownership profile.");
        }
    }

    internal string Name(PdfValue value, string key)
    {
        var name = Graph.Get(value, key);
        return name.Kind == PdfValueKind.Name ? name.NameValue : "";
    }

    private bool PureWidget(PdfValue value) => value.Kind == PdfValueKind.Dictionary && Name(value, "Subtype") == "Widget" && !new[]
    {
        "T",
        "FT",
        "V",
        "DV",
        "Kids"
    }.Any(value.Entries.ContainsKey);
    internal uint Flags(PdfValue value)
    {
        if (value.Kind == PdfValueKind.Null)
            return 0;
        if (!value.TryGetInteger(out long flags) || flags < 0 || flags > uint.MaxValue)
            throw Error("Flags", "Field flags must be unsigned 32-bit integers.");
        return (uint)flags;
    }

    internal static PdfFormFieldKind Kind(string type, uint flags) => type == "Tx" ? PdfFormFieldKind.Text : type == "Sig" ? PdfFormFieldKind.Signature : type == "Ch" ? (flags & 131072) != 0 ? PdfFormFieldKind.ComboBox : PdfFormFieldKind.ListBox : type == "Btn" ? (flags & 65536) != 0 ? (flags & 32768) != 0 ? PdfFormFieldKind.Unknown : PdfFormFieldKind.PushButton : (flags & 32768) != 0 ? PdfFormFieldKind.RadioButton : PdfFormFieldKind.CheckBox : PdfFormFieldKind.Unknown;
    internal bool Equal(PdfValue a, PdfValue b)
    {
        Context.Checkpoint();
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        Context.Consume(PdfResourceKind.Objects, 1);
        a = Graph.Resolve(a);
        b = Graph.Resolve(b);
        if (ReferenceEquals(a, b))
            return true;
        if (a.Kind != b.Kind)
            return false;
        switch (a.Kind)
        {
            case PdfValueKind.Null:
                return true;
            case PdfValueKind.Name:
                return a.NameValue == b.NameValue;
            case PdfValueKind.String:
                return a.StringBytes.SequenceEqual(b.StringBytes);
            case PdfValueKind.Number:
                return a.NumberText == b.NumberText;
            case PdfValueKind.Boolean:
                return a.BooleanValue == b.BooleanValue;
            case PdfValueKind.Array:
                if (a.Items.Count != b.Items.Count)
                    return false;
                for (int i = 0; i < a.Items.Count; i++)
                    if (!Equal(a.Items[i], b.Items[i]))
                        return false;
                return true;
            default:
                return false;
        }
    }

    internal PdfException Error(string code, string message) => Graph.Error("Forms." + code, message);
    private void CheckSiblings(IReadOnlyList<PdfValue> children)
    {
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var child in children)
        {
            Context.Checkpoint();
            var value = Graph.Resolve(child);
            if (value.Kind != PdfValueKind.Dictionary)
                throw Error("FieldDictionary", "Field children must resolve to dictionaries.");
            var partial = Graph.Get(value, "T");
            if (partial.Kind == PdfValueKind.Null)
                continue;
            string name = PdfTextStrings.Decode(partial, Context);
            Context.Consume(PdfResourceKind.ExpandedBytes, 64);
            if (!names.Add(name))
                throw Error("DuplicateName", "Named siblings must have distinct partial names; unnamed representations may share their ancestor's name.");
        }
    }
}
