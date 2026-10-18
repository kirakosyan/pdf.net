namespace Fluent.Pdf;
internal static class PdfLayerValues
{
    internal static void Usage(PdfValue usage, PdfGraphEdit graph)
    {
        if (usage.Kind == PdfValueKind.Null)
            return;
        if (usage.Kind != PdfValueKind.Dictionary)
            throw graph.Error("Layers.UsageDictionary", "Authored usage must be a direct dictionary.");
        foreach (var pair in usage.Entries)
        {
            graph.Context.Checkpoint();
            graph.Context.Consume(PdfResourceKind.Objects, 1);
            if (pair.Key != "View" && pair.Key != "Print" && pair.Key != "Export")
                throw graph.Error("Layers.UsageContextRequired", "Only explicit View, Print and Export usage is authored by this profile.");
            if (pair.Value.Kind != PdfValueKind.Dictionary || pair.Value.Entries.Count != 1 || !pair.Value.Entries.TryGetValue(pair.Key + "State", out var state) || state.Kind != PdfValueKind.Name || state.NameValue != "ON" && state.NameValue != "OFF")
                throw graph.Error("Layers.UsageState", "A supported usage dictionary requires its ON/OFF state.");
        }
    }

    internal static void Intents(PdfValue intents, PdfGraphEdit graph)
    {
        if (intents.Kind == PdfValueKind.Null)
            return;
        var values = intents.Kind == PdfValueKind.Array ? intents.Items : new[]
        {
            intents
        };
        foreach (var value in values)
        {
            graph.Context.Checkpoint();
            graph.Context.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind != PdfValueKind.Name || value.NameValue != "View" && value.NameValue != "Design")
                throw graph.Error("Layers.Intent", "Authored intents must be direct View/Design names.");
        }
    }

    internal static void Configuration(PdfValue value, PdfLayerEvaluator reader, bool isDefault)
    {
        var graph = reader.Graph;
        if (value.Kind != PdfValueKind.Dictionary)
            throw reader.Error("Configuration", "An authored configuration must be a direct dictionary.");
        foreach (var key in value.Entries.Keys)
            if (!new[]
            {
                "Name",
                "Creator",
                "BaseState",
                "ON",
                "OFF",
                "Intent",
                "AS",
                "Order",
                "ListMode",
                "RBGroups",
                "Locked"
            }.Contains(key))
                throw reader.Error("ConfigurationUnsupported", "The authored configuration contains an unsupported key.");
        foreach (var key in new[]
        {
            "Name",
            "Creator"
        }

        )
        {
            var text = reader.Get(value, key);
            if (text.Kind != PdfValueKind.Null)
                PdfTextStrings.Decode(text, graph.Context);
        }

        string basis = reader.Name(value, "BaseState");
        if (isDefault && basis != "" && basis != "ON")
            throw reader.Error("DefaultBaseState", "The default BaseState must be ON.");
        Intents(reader.Get(value, "Intent"), graph);
        var locked = reader.Get(value, "Locked");
        if (locked.Kind != PdfValueKind.Null)
            foreach (var _ in reader.GroupIds(locked))
            {
            }

        string list = reader.Name(value, "ListMode");
        if (list != "" && list != "AllPages" && list != "VisiblePages")
            throw reader.Error("ListMode", "ListMode must be AllPages or VisiblePages.");
        var order = reader.Get(value, "Order");
        if (order.Kind != PdfValueKind.Null)
            Order(order, false);
        var radio = reader.Get(value, "RBGroups");
        if (radio.Kind != PdfValueKind.Null)
        {
            if (radio.Kind != PdfValueKind.Array)
                throw reader.Error("RadioGroups", "RBGroups must be an array of group arrays.");
            foreach (var group in radio.Items)
                foreach (var _ in reader.GroupIds(group))
                {
                }
        }

        var applications = reader.Get(value, "AS");
        if (applications.Kind != PdfValueKind.Null)
        {
            if (applications.Kind != PdfValueKind.Array)
                throw reader.Error("UsageApplications", "Configuration AS must be an array.");
            foreach (var raw in applications.Items)
            {
                var application = graph.Resolve(raw);
                if (application.Kind != PdfValueKind.Dictionary)
                    throw reader.Error("UsageApplication", "An AS application must be a dictionary.");
                foreach (var key in application.Entries.Keys)
                    if (key != "Event" && key != "Category" && key != "OCGs")
                        throw reader.Error("UsageUnsupported", "The authored usage application contains an unsupported key.");
            }
        }

        _ = new PdfLayerEvaluator(graph, value);
        void Order(PdfValue array, bool nested)
        {
            graph.Context.Checkpoint();
            using var depth = graph.Context.EnterScope(PdfResourceKind.Depth);
            array = graph.Resolve(array);
            if (array.Kind != PdfValueKind.Array)
                throw reader.Error("Order", "Layer order must contain nested group arrays.");
            for (int i = 0; i < array.Items.Count; i++)
            {
                var raw = array.Items[i];
                var item = graph.Resolve(raw);
                graph.Context.Consume(PdfResourceKind.Objects, 1);
                if (item.Kind == PdfValueKind.Array)
                    Order(raw, true);
                else if (nested && i == 0 && item.Kind == PdfValueKind.String)
                    PdfTextStrings.Decode(item, graph.Context);
                else if (raw.Kind != PdfValueKind.Reference || !reader.Groups.ContainsKey(raw.ReferenceId))
                    throw reader.Error("Order", "Order leaves must be registered OCG references; nested arrays may start with a text label.");
            }
        }
    }
}
