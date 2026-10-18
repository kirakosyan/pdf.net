namespace Fluent.Pdf;
// Original identity-preserving materialization of explicit Video/Sound rich-media property graphs.
internal static class PdfAnnotationMedia
{
    internal static void ValidateFile(PdfValue file, PdfOperationContext context)
    {
        if (file.Kind != PdfValueKind.Dictionary || !file.Entries.TryGetValue("EF", out var ef) || ef.Kind != PdfValueKind.Dictionary || !ef.Entries.TryGetValue("F", out var stream) || stream.Kind != PdfValueKind.Stream || !file.Entries.TryGetValue("F", out var name) || name.Kind != PdfValueKind.String || name.StringBytes.Count == 0)
            throw Error("Annotation.EmbeddedFile", "An authored embedded file requires an explicit file name and F embedded stream.");
        PdfTextStrings.Decode(name, context);
        foreach (var pair in ef.Entries)
            if (pair.Key != "F" && pair.Key != "UF" || pair.Value.Kind != PdfValueKind.Stream)
                throw Error("Annotation.EmbeddedFile", "Embedded file entries must be explicit F/UF streams.");
        PdfException Error(string code, string message) => PdfWriterCore.Error(context, code, message);
    }

    internal static void Validate(PdfValue properties, PdfOperationContext context)
    {
        var content = Need(properties, "RichMediaContent", PdfValueKind.Dictionary);
        var settings = Need(properties, "RichMediaSettings", PdfValueKind.Dictionary);
        var assets = Need(Need(content, "Assets", PdfValueKind.Dictionary), "Names", PdfValueKind.Array);
        var files = new HashSet<PdfValue>();
        if (assets.Items.Count == 0 || assets.Items.Count % 2 != 0 || assets.Items.Count > 8192)
            throw Error("Annotation.RichMediaAssets", "The explicit media asset name tree must contain one through 4096 name/file pairs.");
        PdfValue? previous = null;
        for (int i = 0; i < assets.Items.Count; i += 2)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 128);
            var name = assets.Items[i];
            if (name.Kind != PdfValueKind.String || name.StringBytes.Count == 0 || previous != null && Compare(previous, name) >= 0)
                throw Error("Annotation.RichMediaAssets", "Media asset names must be nonempty, unique and sorted by unsigned string bytes.");
            ValidateFile(assets.Items[i + 1], context);
            var stream = assets.Items[i + 1].Entries["EF"].Entries["F"];
            if (!stream.Entries.TryGetValue("Subtype", out var mime) || mime.Kind != PdfValueKind.Name || mime.NameValue.IndexOf('/') <= 0)
                throw Error("Annotation.RichMediaMime", "A rich-media embedded stream requires an explicit MIME subtype.");
            files.Add(assets.Items[i + 1]);
            previous = name;
        }

        var configs = Need(content, "Configurations", PdfValueKind.Array);
        if (configs.Items.Count == 0 || configs.Items.Count > 4096)
            throw Error("Annotation.RichMediaConfigurations", "Media requires a bounded nonempty configuration array.");
        foreach (var config in configs.Items)
        {
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            string kind = Need(config, "Subtype", PdfValueKind.Name).NameValue;
            if (kind != "Video" && kind != "Sound")
                throw Error("Annotation.RichMediaProfile", "Authored rich-media configurations currently require an explicit Video or Sound profile.");
            var instances = Need(config, "Instances", PdfValueKind.Array);
            if (instances.Items.Count == 0 || instances.Items.Count > 4096)
                throw Error("Annotation.RichMediaInstances", "A media configuration requires a bounded nonempty instance array.");
            foreach (var instance in instances.Items)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                if (Need(instance, "Subtype", PdfValueKind.Name).NameValue != kind || !files.Contains(Need(instance, "Asset", PdfValueKind.Dictionary)))
                    throw Error("Annotation.RichMediaAssetIdentity", "Each media instance must share the exact explicit file specification object in the asset table and match its configuration subtype.");
            }
        }

        // Additional view/script identity graphs require their own authoring profile. Imported graphs remain passive and preserve them.
        if (content.Entries.ContainsKey("Views"))
            throw Error("Annotation.RichMediaProfile", "Authored 3D view graphs require an explicit additional profile.");
        if (settings.Entries.TryGetValue("Activation", out var activation))
        {
            if (activation.Kind != PdfValueKind.Dictionary)
                throw Error("Annotation.RichMediaActivation", "Media activation must be a dictionary.");
            if (activation.Entries.TryGetValue("Configuration", out var config) && !configs.Items.Contains(config))
                throw Error("Annotation.RichMediaConfigurationIdentity", "Activation must share an exact configured object from the explicit configuration array.");
            if (activation.Entries.ContainsKey("Scripts") || activation.Entries.ContainsKey("View"))
                throw Error("Annotation.RichMediaProfile", "Authored script/view identity graphs require an explicit additional profile.");
        }

        PdfValue Need(PdfValue value, string key, PdfValueKind kind) => value.Kind == PdfValueKind.Dictionary && value.Entries.TryGetValue(key, out var child) && child.Kind == kind ? child : throw Error("Annotation.RichMediaProperties", "The media graph is missing a required explicitly typed property.");
        PdfException Error(string code, string message) => PdfWriterCore.Error(context, code, message);
    }

    internal static PdfValue Materialize(PdfValue properties, PdfResourceWriter writer, PdfGraphEdit graph)
    {
        var context = graph.Context;
        var values = graph.Copy(properties);
        var content = graph.Copy(properties.Entries["RichMediaContent"]);
        var assetTree = graph.Copy(content["Assets"]);
        var declaredNames = assetTree["Names"].Items;
        context.Consume(PdfResourceKind.ExpandedBytes, declaredNames.Count * 64L + 256);
        var names = new List<PdfValue>();
        var files = new Dictionary<PdfValue, PdfValue>();
        PdfValue File(PdfValue file)
        {
            if (!files.TryGetValue(file, out var result))
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 128);
                result = PdfValue.Reference(graph.Edit.Add(writer.EmbedValue(file)));
                files.Add(file, result);
            }

            return result;
        }

        for (int i = 0; i < declaredNames.Count; i += 2)
        {
            context.Checkpoint();
            names.Add(declaredNames[i]);
            names.Add(File(declaredNames[i + 1]));
        }

        assetTree["Names"] = PdfValue.OwnedArray(names);
        content["Assets"] = PdfValue.OwnedDictionary(assetTree);
        var configured = new Dictionary<PdfValue, PdfValue>();
        var configs = new List<PdfValue>();
        foreach (var declared in content["Configurations"].Items)
        {
            context.Checkpoint();
            var config = graph.Copy(declared);
            var instances = new List<PdfValue>();
            foreach (var item in declared.Entries["Instances"].Items)
            {
                var instance = graph.Copy(item);
                instance["Type"] = PdfValue.Name("RichMediaInstance");
                instance["Asset"] = File(instance["Asset"]);
                context.Consume(PdfResourceKind.ExpandedBytes, 16);
                instances.Add(PdfValue.Reference(graph.Edit.Add(writer.EmbedValue(PdfValue.OwnedDictionary(instance)))));
            }

            config["Type"] = PdfValue.Name("RichMediaConfiguration");
            config["Instances"] = PdfValue.OwnedArray(instances);
            var reference = PdfValue.Reference(graph.Edit.Add(writer.EmbedValue(PdfValue.OwnedDictionary(config))));
            context.Consume(PdfResourceKind.ExpandedBytes, 128);
            configs.Add(reference);
            configured[declared] = reference;
        }

        content["Type"] = PdfValue.Name("RichMediaContent");
        content["Configurations"] = PdfValue.OwnedArray(configs);
        values["RichMediaContent"] = PdfValue.Reference(graph.Edit.Add(writer.EmbedValue(PdfValue.OwnedDictionary(content))));
        var settings = graph.Copy(properties.Entries["RichMediaSettings"]);
        settings["Type"] = PdfValue.Name("RichMediaSettings");
        if (settings.TryGetValue("Activation", out var declaredActivation))
        {
            var activation = graph.Copy(declaredActivation);
            if (activation.TryGetValue("Configuration", out var config))
                activation["Configuration"] = configured[config];
            settings["Activation"] = PdfValue.OwnedDictionary(activation);
        }

        values["RichMediaSettings"] = PdfValue.Reference(graph.Edit.Add(writer.EmbedValue(PdfValue.OwnedDictionary(settings))));
        return writer.EmbedValue(PdfValue.OwnedDictionary(values));
    }

    private static int Compare(PdfValue a, PdfValue b)
    {
        int count = Math.Min(a.StringBytes.Count, b.StringBytes.Count);
        for (int i = 0; i < count; i++)
        {
            int order = a.StringBytes[i].CompareTo(b.StringBytes[i]);
            if (order != 0)
                return order;
        }

        return a.StringBytes.Count.CompareTo(b.StringBytes.Count);
    }
}
