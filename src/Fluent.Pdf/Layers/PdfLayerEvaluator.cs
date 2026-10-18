namespace Fluent.Pdf;
// Original bounded optional-content state machine. No actions, UI state, clock or environment are inferred.
internal sealed class PdfLayerEvaluator
{
    internal readonly PdfGraphEdit Graph;
    internal readonly Dictionary<PdfObjectId, PdfValue> Groups = new();
    internal readonly Dictionary<PdfObjectId, bool> States = new();
    internal readonly PdfValue Properties;
    internal readonly PdfValue Configuration;
    private readonly HashSet<string> _intents = new(StringComparer.Ordinal);
    private PdfOperationContext Context => Graph.Context;

    internal PdfLayerEvaluator(PdfGraphEdit graph, PdfValue? configuration = null, PdfLayerUsage usage = PdfLayerUsage.View, bool evaluate = true)
    {
        Graph = graph;
        if (!Enum.IsDefined(typeof(PdfLayerUsage), usage))
            throw new ArgumentOutOfRangeException(nameof(usage));
        Properties = graph.Get(graph.Catalog, "OCProperties");
        if (Properties.Kind != PdfValueKind.Null && Properties.Kind != PdfValueKind.Dictionary)
            throw Error("Properties", "OCProperties must be a dictionary.");
        var list = graph.Get(Properties, "OCGs");
        if (Properties.Kind != PdfValueKind.Null && list.Kind != PdfValueKind.Array)
            throw Error("GroupArray", "OCProperties requires an OCGs array.");
        if (list.Kind == PdfValueKind.Array)
            foreach (var reference in list.Items)
            {
                Context.Checkpoint();
                Context.Consume(PdfResourceKind.Objects, 1);
                Context.Consume(PdfResourceKind.ExpandedBytes, 192);
                if (reference.Kind != PdfValueKind.Reference || Groups.ContainsKey(reference.ReferenceId))
                    throw Error("GroupIdentity", "OCGs requires distinct indirect group identities.");
                var value = graph.Resolve(reference);
                if (value.Kind != PdfValueKind.Dictionary || Name(value, "Type") != "OCG")
                    throw Error("GroupDictionary", "A registered group must be an OCG dictionary.");
                PdfTextStrings.Decode(Get(value, "Name"), Context);
                Groups.Add(reference.ReferenceId, value);
                States.Add(reference.ReferenceId, true);
            }

        var baseline = graph.Get(Properties, "D");
        if (Properties.Kind != PdfValueKind.Null && baseline.Kind != PdfValueKind.Dictionary)
            throw Error("DefaultConfiguration", "OCProperties requires a default configuration.");
        if (!evaluate)
        {
            Configuration = baseline;
            return;
        }

        if (baseline.Kind != PdfValueKind.Null)
        {
            string basis = Name(baseline, "BaseState");
            if (basis != "" && basis != "ON")
                throw Error("DefaultBaseState", "The default configuration BaseState must be ON.");
            Apply(baseline);
        }

        Configuration = configuration == null || configuration.Kind == PdfValueKind.Null ? baseline : graph.Resolve(configuration);
        if (!ReferenceEquals(Configuration, baseline) && Configuration.Kind != PdfValueKind.Null)
            Apply(Configuration);
        var intents = Get(Configuration, "Intent");
        foreach (var intent in IntentNames(intents))
            _intents.Add(intent);
        var applications = Get(Configuration, "AS");
        if (applications.Kind != PdfValueKind.Null && usage != PdfLayerUsage.Configured)
        {
            if (applications.Kind != PdfValueKind.Array)
                throw Error("UsageApplications", "Configuration AS must be an array.");
            foreach (var phase in usage == PdfLayerUsage.View ? new[]
            {
                PdfLayerUsage.View
            }

            : new[]
            {
                PdfLayerUsage.View,
                usage
            }

            )
            {
                Context.Consume(PdfResourceKind.ExpandedBytes, Groups.Count * 64L + 64);
                var recommendations = new Dictionary<PdfObjectId, bool>();
                foreach (var raw in applications.Items)
                {
                    var application = Graph.Resolve(raw);
                    if (application.Kind != PdfValueKind.Dictionary)
                        throw Error("UsageApplication", "An AS application must be a dictionary.");
                    string eventName = Name(application, "Event");
                    if (eventName != "View" && eventName != "Print" && eventName != "Export")
                        throw Error("UsageEvent", "An AS application requires a standard Event.");
                    if (eventName != phase.ToString())
                        continue;
                    var categories = Get(application, "Category");
                    if (categories.Kind != PdfValueKind.Array)
                        throw Error("UsageCategory", "An AS application requires a Category array.");
                    var selected = Get(application, "OCGs");
                    var targets = selected.Kind == PdfValueKind.Null ? Array.Empty<PdfObjectId>() : GroupIds(selected);
                    foreach (var id in targets)
                    {
                        bool? state = null;
                        var groupUsage = Get(Groups[id], "Usage");
                        foreach (var categoryValue in categories.Items)
                        {
                            Context.Checkpoint();
                            var category = Graph.Resolve(categoryValue);
                            if (category.Kind != PdfValueKind.Name)
                                throw Error("UsageCategory", "Usage categories must be names.");
                            if (category.NameValue != "View" && category.NameValue != "Print" && category.NameValue != "Export")
                                throw Error("UsageContextRequired", "The requested usage category needs an explicit environmental context profile.");
                            var specification = Get(groupUsage, category.NameValue);
                            if (specification.Kind == PdfValueKind.Null)
                            {
                                state = (state ?? true) && States[id];
                                continue;
                            }

                            if (specification.Kind != PdfValueKind.Dictionary)
                                throw Error("UsageDictionary", "Group usage entries must be dictionaries.");
                            string declared = Name(specification, category.NameValue + "State");
                            if (category.NameValue == "Print" && declared == "")
                            {
                                state = (state ?? true) && States[id];
                                continue;
                            }

                            if (declared != "ON" && declared != "OFF")
                                throw Error("UsageState", "A supported usage state must be ON or OFF.");
                            state = (state ?? true) && declared == "ON";
                        }

                        if (state.HasValue)
                            recommendations[id] = (!recommendations.TryGetValue(id, out bool prior) || prior) && state.Value;
                    }
                }

                foreach (var recommendation in recommendations)
                    States[recommendation.Key] = recommendation.Value;
            }
        }
    }

    internal PdfValue Get(PdfValue value, string key) => Graph.Get(value, key);
    internal string Name(PdfValue value, string key)
    {
        var name = Get(value, key);
        if (name.Kind != PdfValueKind.Null && name.Kind != PdfValueKind.Name)
            throw Error("NameRequired", "An optional-content name has an invalid value type.");
        return name.Kind == PdfValueKind.Name ? name.NameValue : "";
    }

    internal PdfException Error(string code, string message) => Graph.Error("Layers." + code, message);
    internal IEnumerable<PdfObjectId> GroupIds(PdfValue value)
    {
        value = Graph.Resolve(value);
        if (value.Kind != PdfValueKind.Array)
            throw Error("ConfigurationGroups", "Configuration group lists must be arrays.");
        var seen = new HashSet<PdfObjectId>();
        foreach (var reference in value.Items)
        {
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.ExpandedBytes, 48);
            if (reference.Kind != PdfValueKind.Reference || !Groups.ContainsKey(reference.ReferenceId) || !seen.Add(reference.ReferenceId))
                throw Error("UnknownGroup", "A group list requires distinct registered OCG references.");
            yield return reference.ReferenceId;
        }
    }

    private IEnumerable<string> IntentNames(PdfValue value)
    {
        value = Graph.Resolve(value);
        if (value.Kind == PdfValueKind.Null)
        {
            yield return "View";
            yield break;
        }

        var values = value.Kind == PdfValueKind.Array ? value.Items : new[]
        {
            value
        };
        foreach (var raw in values)
        {
            Context.Checkpoint();
            var name = Graph.Resolve(raw);
            if (name.Kind != PdfValueKind.Name || name.NameValue != "View" && name.NameValue != "Design")
                throw Error("Intent", "Intents must be View or Design names.");
            yield return name.NameValue;
        }
    }

    private void Apply(PdfValue configuration)
    {
        if (configuration.Kind != PdfValueKind.Dictionary)
            throw Error("Configuration", "An optional-content configuration must be a dictionary.");
        string basis = Name(configuration, "BaseState");
        if (basis == "" || basis == "ON" || basis == "OFF")
            foreach (var id in Groups.Keys)
                States[id] = basis != "OFF";
        else if (basis != "Unchanged")
            throw Error("BaseState", "Configuration BaseState must be ON, OFF or Unchanged.");
        var assigned = new HashSet<PdfObjectId>();
        foreach (var key in new[]
        {
            "ON",
            "OFF"
        }

        )
        {
            var list = Get(configuration, key);
            if (list.Kind == PdfValueKind.Null)
                continue;
            foreach (var id in GroupIds(list))
            {
                Context.Consume(PdfResourceKind.ExpandedBytes, 48);
                if (!assigned.Add(id))
                    throw Error("ConflictingState", "A group cannot appear in both ON and OFF.");
                States[id] = key == "ON";
            }
        }
    }

    internal bool Visible(PdfValue membership) => Evaluate(membership, new HashSet<PdfObjectId>()) ?? true;
    private bool? Evaluate(PdfValue membership, HashSet<PdfObjectId> path)
    {
        Context.Checkpoint();
        Context.Consume(PdfResourceKind.Objects, 1);
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        PdfObjectId? entered = null;
        if (membership.Kind == PdfValueKind.Reference)
        {
            Context.Consume(PdfResourceKind.ExpandedBytes, 48);
            entered = membership.ReferenceId;
            if (!path.Add(entered.Value))
                throw Error("VisibilityCycle", "Optional-content visibility expressions contain a cycle.");
        }

        try
        {
            var value = Graph.Resolve(membership);
            if (value.Kind == PdfValueKind.Array)
                return Expression(value, path);
            if (value.Kind != PdfValueKind.Dictionary)
                throw Error("Membership", "Optional content requires an OCG or OCMD dictionary.");
            string type = Name(value, "Type");
            if (type == "OCG")
            {
                if (membership.Kind != PdfValueKind.Reference || !Groups.ContainsKey(membership.ReferenceId))
                    throw Error("UnknownGroup", "A group membership requires its registered indirect identity.");
                return IntentNames(Get(value, "Intent")).Any(x => _intents.Contains(x)) ? States[membership.ReferenceId] : null;
            }

            if (type != "OCMD")
                throw Error("Membership", "A membership dictionary requires Type OCMD.");
            var expression = Get(value, "VE");
            if (expression.Kind != PdfValueKind.Null)
                return Evaluate(expression, path);
            var rawList = value.Entries.TryGetValue("OCGs", out var listed) ? listed : PdfValue.Null;
            if (rawList.Kind == PdfValueKind.Reference && Deleted(rawList.ReferenceId))
                return null;
            var list = Graph.Resolve(rawList);
            var values = list.Kind == PdfValueKind.Array ? list.Items : list.Kind == PdfValueKind.Null ? Array.Empty<PdfValue>() : new[]
            {
                value.Entries["OCGs"]
            };
            bool any = false, anyOn = false, anyOff = false;
            foreach (var group in values)
            {
                if (group.Kind == PdfValueKind.Null || group.Kind == PdfValueKind.Reference && Deleted(group.ReferenceId))
                    continue;
                var groupValue = Graph.Resolve(group);
                if (groupValue.Kind == PdfValueKind.Null)
                    continue;
                if (groupValue.Kind != PdfValueKind.Dictionary || Name(groupValue, "Type") != "OCG")
                    throw Error("MembershipGroup", "OCMD OCGs must contain OCG references.");
                var state = Evaluate(group, path);
                if (!state.HasValue)
                    continue;
                any = true;
                anyOn |= state.Value;
                anyOff |= !state.Value;
            }

            string policy = Name(value, "P");
            if (policy != "" && policy != "AnyOn" && policy != "AllOn" && policy != "AnyOff" && policy != "AllOff")
                throw Error("MembershipPolicy", "A membership policy must be AnyOn, AllOn, AnyOff or AllOff.");
            if (!any)
                return null;
            return policy == "AllOn" ? !anyOff : policy == "AllOff" ? !anyOn : policy == "AnyOff" ? anyOff : anyOn;
        }
        finally
        {
            if (entered.HasValue)
                path.Remove(entered.Value);
        }
    }

    private bool? Expression(PdfValue value, HashSet<PdfObjectId> path)
    {
        if (value.Items.Count < 2)
            throw Error("VisibilityExpression", "A visibility expression requires an operator and operands.");
        var operation = Graph.Resolve(value.Items[0]);
        if (operation.Kind != PdfValueKind.Name || operation.NameValue != "And" && operation.NameValue != "Or" && operation.NameValue != "Not" || operation.NameValue == "Not" && value.Items.Count != 2)
            throw Error("VisibilityExpression", "Visibility expressions use And, Or or unary Not.");
        bool? result = null;
        for (int i = 1; i < value.Items.Count; i++)
        {
            var operand = Graph.Resolve(value.Items[i]);
            if (operand.Kind != PdfValueKind.Array && (operand.Kind != PdfValueKind.Dictionary || Name(operand, "Type") != "OCG"))
                throw Error("VisibilityOperand", "Visibility operands must be groups or nested expressions.");
            var state = Evaluate(value.Items[i], path);
            if (!state.HasValue)
                continue;
            result = operation.NameValue == "Not" ? !state.Value : operation.NameValue == "And" ? (result ?? true) && state.Value : (result ?? false) || state.Value;
        }

        return result;
    }

    private bool Deleted(PdfObjectId id)
    {
        if (Groups.ContainsKey(id))
            return false;
        foreach (var revision in Graph.Graph.Revisions)
        {
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.Objects, 1);
            if (revision.Index.TryGetValue(id.Number, out var entry))
                return entry.Kind == PdfCrossReferenceKind.Free || entry.Id != id;
        }

        return true;
    }
}
