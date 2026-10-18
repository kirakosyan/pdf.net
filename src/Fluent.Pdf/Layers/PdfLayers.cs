using System.Threading;

namespace Fluent.Pdf;
/// <summary>Managed passive optional-content authoring, configuration evaluation and explicit flattening.</summary>
public static class PdfLayers
{
    /// <summary>Inspects registered groups and raw configurations without applying usage or evaluating document actions.</summary>
    public static PdfLayerSet Inspect(PdfDocumentSnapshot source, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        PdfDocumentSnapshot? captured = null;
        PdfValue properties = PdfValue.Null;
        var groups = new List<PdfLayerGroup>();
        var configurations = new List<PdfValue>();
        var report = PdfFont.Run("InspectLayers", "managed.layers", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            captured = graph.Snapshot;
            var reader = new PdfLayerEvaluator(graph, evaluate: false);
            properties = reader.Properties;
            foreach (var group in reader.Groups)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 96);
                groups.Add(new(captured, group.Key, PdfTextStrings.Decode(graph.Get(group.Value, "Name"), context), group.Value));
            }

            var configs = graph.Get(properties, "Configs");
            if (configs.Kind != PdfValueKind.Null && configs.Kind != PdfValueKind.Array)
                throw reader.Error("Configurations", "OCProperties Configs must be an array.");
            if (configs.Kind == PdfValueKind.Array)
                foreach (var raw in configs.Items)
                {
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.ExpandedBytes, 32);
                    var config = graph.Resolve(raw);
                    if (config.Kind != PdfValueKind.Dictionary)
                        throw reader.Error("Configuration", "An alternate configuration must be a dictionary.");
                    configurations.Add(config);
                }
        }, PdfCapability.ReadDocument);
        return new(captured!, groups, properties, configurations, report);
    }

    /// <summary>Adds one Unicode OCG with explicit initial state and optional View/Print/Export usage and View/Design intents.</summary>
    public static PdfDocument AddGroup(PdfDocumentSnapshot source, string name, bool visible = true, PdfValue? usage = null, PdfValue? intents = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(name, nameof(name), 8192);
        return PdfGraphEdit.Run("AddLayer", source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfLayerEvaluator(graph);
            PdfLayerValues.Usage(usage ?? PdfValue.Null, graph);
            PdfLayerValues.Intents(intents ?? PdfValue.Null, graph);
            graph.Context.Consume(PdfResourceKind.Objects, 12);
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, reader.Groups.Count * 64L + 2048);
            var definition = new Dictionary<string, PdfValue>
            {
                {
                    "Type",
                    PdfValue.Name("OCG")
                },
                {
                    "Name",
                    PdfTextStrings.Encode(name, graph.Context)
                }
            };
            if (usage != null && usage.Kind != PdfValueKind.Null)
                definition["Usage"] = usage;
            if (intents != null && intents.Kind != PdfValueKind.Null)
                definition["Intent"] = intents;
            var reference = PdfValue.Reference(graph.Edit.Add(PdfValue.OwnedDictionary(definition)));
            var properties = graph.Copy(reader.Properties);
            var all = reader.Groups.Keys.Select(PdfValue.Reference).ToList();
            all.Add(reference);
            properties["OCGs"] = PdfValue.OwnedArray(all);
            var config = graph.Copy(reader.Configuration);
            config["BaseState"] = PdfValue.Name("ON");
            if (!visible)
            {
                var off = graph.Get(reader.Configuration, "OFF");
                var entries = off.Kind == PdfValueKind.Array ? off.Items.ToList() : new List<PdfValue>();
                entries.Add(reference);
                config["OFF"] = PdfValue.OwnedArray(entries);
            }

            var order = graph.Get(reader.Configuration, "Order");
            var display = order.Kind == PdfValueKind.Array ? order.Items.ToList() : new List<PdfValue>();
            display.Add(reference);
            config["Order"] = PdfValue.OwnedArray(display);
            if (usage != null && usage.Kind != PdfValueKind.Null)
            {
                var previous = graph.Get(reader.Configuration, "AS");
                var applications = previous.Kind == PdfValueKind.Array ? previous.Items.ToList() : new List<PdfValue>();
                foreach (var category in usage.Entries.Keys)
                    applications.Add(PdfGraphicsNumbers.Dict(("Event", PdfValue.Name(category)), ("Category", PdfValue.Array(PdfValue.Name(category))), ("OCGs", PdfValue.Array(reference))));
                config["AS"] = PdfValue.OwnedArray(applications);
            }

            properties["D"] = PdfValue.OwnedDictionary(config);
            var catalog = graph.Copy(graph.Catalog);
            catalog["OCProperties"] = PdfValue.OwnedDictionary(properties);
            graph.SetCatalog(catalog);
        }, "1.5");
    }

    /// <summary>Changes a captured group; null preserves a field and PDF null removes usage/intents. The edit stages a new document.</summary>
    public static PdfDocument EditGroup(PdfLayerGroup group, string? name = null, PdfValue? usage = null, PdfValue? intents = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (group == null)
            throw new ArgumentNullException(nameof(group));
        if (name != null)
            Guard.Text(name, nameof(name), 8192);
        return PdfGraphEdit.Run("EditLayer", group.Source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfLayerEvaluator(graph, evaluate: false);
            if (!reader.Groups.TryGetValue(group.Id, out var value))
                throw reader.Error("UnknownGroup", "The captured group is not registered.");
            var entries = graph.Copy(value);
            if (name != null)
                entries["Name"] = PdfTextStrings.Encode(name, graph.Context);
            if (usage != null)
            {
                PdfLayerValues.Usage(usage, graph);
                if (usage.Kind == PdfValueKind.Null)
                    entries.Remove("Usage");
                else
                    entries["Usage"] = usage;
            }

            if (intents != null)
            {
                PdfLayerValues.Intents(intents, graph);
                if (intents.Kind == PdfValueKind.Null)
                    entries.Remove("Intent");
                else
                    entries["Intent"] = intents;
            }

            graph.Edit.Set(group.Id, PdfValue.OwnedDictionary(entries));
        }, "1.5");
    }

    /// <summary>Sets the default configuration or appends an alternate configuration, validating group references and standard supported keys.</summary>
    public static PdfDocument SetConfiguration(PdfDocumentSnapshot source, PdfValue configuration, bool makeDefault = true, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));
        return PdfGraphEdit.Run("SetLayerConfiguration", source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfLayerEvaluator(graph, evaluate: false);
            PdfLayerValues.Configuration(configuration, reader, makeDefault);
            var properties = graph.Copy(reader.Properties);
            if (makeDefault)
                properties["D"] = configuration;
            else
            {
                var old = graph.Get(properties.Count == 0 ? PdfValue.Null : PdfValue.OwnedDictionary(properties), "Configs");
                if (old.Kind != PdfValueKind.Null && old.Kind != PdfValueKind.Array)
                    throw reader.Error("Configurations", "Configs must be an array.");
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, (old.Kind == PdfValueKind.Array ? old.Items.Count : 0) * 16L + 64);
                var configs = old.Kind == PdfValueKind.Array ? old.Items.ToList() : new List<PdfValue>();
                configs.Add(configuration);
                properties["Configs"] = PdfValue.OwnedArray(configs);
            }

            if (!properties.ContainsKey("OCGs"))
                properties["OCGs"] = PdfValue.Array();
            if (!properties.ContainsKey("D"))
                properties["D"] = PdfGraphicsNumbers.Dict(("BaseState", PdfValue.Name("ON")));
            var catalog = graph.Copy(graph.Catalog);
            catalog["OCProperties"] = PdfValue.OwnedDictionary(properties);
            graph.SetCatalog(catalog);
        }, "1.6");
    }

    /// <summary>Applies an alternate configuration to the initial view, resolving Unchanged and usage into explicit states.</summary>
    public static PdfDocument SelectConfiguration(PdfDocumentSnapshot source, int configurationIndex, PdfLayerUsage usage = PdfLayerUsage.View, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfGraphEdit.Run("SelectLayerConfiguration", source, options, policy, cancellationToken, graph =>
    {
        var properties = graph.Get(graph.Catalog, "OCProperties");
        var configurations = graph.Get(properties, "Configs");
        if (configurations.Kind != PdfValueKind.Array || configurationIndex < 0 || configurationIndex >= configurations.Items.Count)
            throw new ArgumentOutOfRangeException(nameof(configurationIndex));
        var selected = graph.Resolve(configurations.Items[configurationIndex]);
        var reader = new PdfLayerEvaluator(graph, selected, usage);
        var config = graph.Copy(selected);
        config["BaseState"] = PdfValue.Name("ON");
        config.Remove("ON");
        config.Remove("AS");
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, reader.Groups.Count * 32L + 64);
        config["OFF"] = PdfValue.OwnedArray(reader.States.Where(x => !x.Value).Select(x => PdfValue.Reference(x.Key)).ToList());
        var baseline = graph.Get(properties, "D");
        foreach (var key in new[]
        {
            "Order",
            "RBGroups"
        }

        )
            if (!config.ContainsKey(key) && baseline.Kind == PdfValueKind.Dictionary && baseline.Entries.TryGetValue(key, out var original))
                config[key] = original;
        var updated = graph.Copy(properties);
        updated["D"] = PdfValue.OwnedDictionary(config);
        var catalog = graph.Copy(graph.Catalog);
        catalog["OCProperties"] = PdfValue.OwnedDictionary(updated);
        graph.SetCatalog(catalog);
    }, "1.6");
    /// <summary>Evaluates an OCG reference or OCMD, including nested VE expressions, with explicit configuration and usage.</summary>
    public static PdfLayerVisibility IsVisible(PdfDocumentSnapshot source, PdfValue membership, PdfValue? configuration = null, PdfLayerUsage usage = PdfLayerUsage.View, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (membership == null)
            throw new ArgumentNullException(nameof(membership));
        bool visible = false;
        var report = PdfFont.Run("EvaluateLayerVisibility", "managed.layers", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            visible = new PdfLayerEvaluator(graph, configuration, usage).Visible(membership);
        }, PdfCapability.ReadDocument);
        return new(visible, report);
    }

    /// <summary>Binds a selected page XObject to an OCG or OCMD, cloning the stream and page resource scope.</summary>
    public static PdfDocument BindXObject(PdfDocumentSnapshot source, int pageIndex, string resourceName, PdfValue membership, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfMetadataValues.Key(resourceName);
        if (membership == null)
            throw new ArgumentNullException(nameof(membership));
        return PdfGraphEdit.Run("BindLayerXObject", source, options, policy, cancellationToken, graph =>
        {
            if (pageIndex < 0 || pageIndex >= graph.Snapshot.Pages.Count)
                throw new ArgumentOutOfRangeException(nameof(pageIndex));
            var reader = new PdfLayerEvaluator(graph);
            reader.Visible(membership);
            var page = graph.Snapshot.Pages[pageIndex].Source!;
            var resources = graph.Copy(graph.Get(page.EffectiveAttributes, "Resources"));
            var objects = graph.Copy(resources.TryGetValue("XObject", out var value) ? graph.Resolve(value) : PdfValue.Null);
            if (!objects.TryGetValue(resourceName, out var raw))
                throw reader.Error("XObjectMissing", "The selected page XObject does not exist.");
            var stream = graph.Resolve(raw);
            if (stream.Kind != PdfValueKind.Stream || reader.Name(stream, "Subtype") != "Form" && reader.Name(stream, "Subtype") != "Image")
                throw reader.Error("XObjectType", "Layer binding requires a Form or image XObject.");
            var entries = graph.Copy(stream);
            entries["OC"] = membership.Kind == PdfValueKind.Reference ? membership : PdfValue.Reference(graph.Edit.Add(membership));
            objects[resourceName] = PdfValue.Reference(graph.Edit.Add(stream.WithDictionary(PdfValue.OwnedDictionary(entries))));
            resources["XObject"] = PdfValue.OwnedDictionary(objects);
            var dictionary = graph.Copy(page.Dictionary);
            dictionary["Resources"] = PdfValue.OwnedDictionary(resources);
            graph.Edit.Set(page.ObjectId, PdfValue.OwnedDictionary(dictionary));
        }, "1.6");
    }

    /// <summary>Flattens the selected configuration, removing hidden painting while preserving supported graphics-state effects. Unsupported state-dependent cases reject.</summary>
    public static PdfDocument Flatten(PdfDocumentSnapshot source, PdfValue? configuration = null, PdfLayerUsage usage = PdfLayerUsage.View, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfGraphEdit.Run("FlattenLayers", source, options, policy, cancellationToken, graph => new PdfLayerFlattener(new PdfLayerEvaluator(graph, configuration, usage)).Run(), "1.5");
}
