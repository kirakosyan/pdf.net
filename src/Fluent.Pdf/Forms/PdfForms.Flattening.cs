using System.Threading;

namespace Fluent.Pdf;
public static partial class PdfForms
{
    /// <summary>Paints selected/all authored widget appearances and removes owned field/widget nodes. Incremental mode preserves original signed bytes; signature verification/transform permission is a separate requirement.</summary>
    public static PdfDocument Flatten(PdfDocumentSnapshot source, IEnumerable<string>? fieldNames = null, PdfAnnotationView view = PdfAnnotationView.Screen, bool acceptRequestedRegeneration = false, bool retainOpaqueNameReferences = false, PdfWriteMode writeMode = PdfWriteMode.Full, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(PdfAnnotationView), view))
            throw new ArgumentOutOfRangeException(nameof(view));
        return PdfGraphEdit.Run("FlattenFormFields", source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfFormReader(graph);
            reader.Editable();
            var chosen = SelectFields(reader, fieldNames);
            if (chosen.Count == 0)
                return;
            Regeneration(reader, acceptRequestedRegeneration);
            SignaturePermissions(graph, options);
            CheckNameReferences(graph, retainOpaqueNameReferences);
            var removed = new HashSet<PdfObjectId>();
            var widgetIds = new HashSet<PdfObjectId>();
            var programs = new Dictionary<int, List<PdfContentOperation>>();
            var resources = new Dictionary<int, Dictionary<string, PdfValue>>();
            foreach (var field in chosen)
            {
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, field.Representations.Count * 64L + field.Widgets.Count * 96L + 128);
                if (field.Kind == PdfFormFieldKind.Signature && field.Value.Kind != PdfValueKind.Null)
                    throw reader.Error("SignedFieldFlattening", "Removing a signed signature field requires a dedicated signature-removal profile.");
                if (field.Kind == PdfFormFieldKind.Unknown)
                    throw reader.Error("UnsupportedType", "Flattening an unsupported field type requires an explicit semantic profile.");
                if (field.Kind == PdfFormFieldKind.CheckBox || field.Kind == PdfFormFieldKind.RadioButton)
                    PdfFormValues.Value(field.Kind, field.Flags, field.Value, Array.Empty<PdfFormChoice>(), null, States(field, graph), graph);
                foreach (var id in field.Representations)
                    removed.Add(id);
                foreach (var widget in field.Widgets)
                {
                    graph.Context.Checkpoint();
                    removed.Add(widget.Id);
                    widgetIds.Add(widget.Id);
                    if (widget.Definition.Entries.ContainsKey("StructParent"))
                        throw reader.Error("StructureFlattenProfile", "Tagged widget flattening requires a structure-association conversion profile.");
                    if (!widget.PageIndex.HasValue)
                        continue;
                    var flags = graph.Get(widget.Definition, "F");
                    long bits = 0;
                    if (flags.Kind != PdfValueKind.Null && (!flags.TryGetInteger(out bits) || bits < 0 || bits > 1023))
                        throw reader.Error("WidgetFlags", "Widget flags must fit the standard ten-bit field.");
                    if ((bits & 2) != 0 || view == PdfAnnotationView.Screen && (bits & 32) != 0 || view == PdfAnnotationView.Print && (bits & 4) == 0 || widget.Bounds.Width == 0 || widget.Bounds.Height == 0)
                        continue;
                    if ((bits & (8 | 16 | 256)) != 0)
                        throw reader.Error("DynamicViewFlags", "NoZoom, NoRotate and ToggleNoView require an explicit view-dependent flattening profile.");
                    if (widget.Definition.Entries.ContainsKey("BM") || widget.Definition.Entries.ContainsKey("CA") || widget.Definition.Entries.ContainsKey("ca"))
                        throw reader.Error("AppearanceCompositing", "Widget-level blend/opacity overrides require a compositing profile.");
                    var page = graph.Snapshot.Pages[widget.PageIndex.Value].Source!;
                    if (page.Dictionary.Entries.ContainsKey("Group"))
                        throw reader.Error("PageGroupFlattening", "Page transparency groups require an explicit compositing profile.");
                    var ap = graph.Get(widget.Definition, "AP");
                    var normal = graph.Get(ap, "N");
                    PdfValue reference = ap.Kind == PdfValueKind.Dictionary && ap.Entries.TryGetValue("N", out var raw) ? raw : PdfValue.Null;
                    if (normal.Kind == PdfValueKind.Dictionary)
                    {
                        var state = graph.Get(widget.Definition, "AS");
                        if (state.Kind != PdfValueKind.Name || !normal.Entries.TryGetValue(state.NameValue, out var selected))
                            throw reader.Error("AppearanceState", "A normal-state appearance requires a matching AS entry.");
                        if (field.Kind == PdfFormFieldKind.CheckBox || field.Kind == PdfFormFieldKind.RadioButton)
                        {
                            string expected = field.Value.Kind == PdfValueKind.Name && normal.Entries.ContainsKey(field.Value.NameValue) ? field.Value.NameValue : "Off";
                            if (state.NameValue != expected)
                                throw reader.Error("AppearanceStateMismatch", "Button AS and the logical current value disagree.");
                        }

                        reference = selected;
                        normal = graph.Resolve(selected);
                    }

                    RequireForm(normal, graph);
                    var mapping = AppearanceMapping(normal, widget.Bounds, graph);
                    if (reference.Kind != PdfValueKind.Reference)
                        reference = PdfValue.Reference(graph.Edit.Add(normal));
                    if (widget.Definition.Entries.TryGetValue("OC", out var visibility))
                    {
                        // Encapsulate the annotation's outer visibility independently of any Form-level OC.
                        // Preserve an existing inner OC by a one-invocation wrapper.
                        var wrapper = AppearanceWrapper(reference, normal, visibility, graph);
                        reference = PdfValue.Reference(graph.Edit.Add(wrapper));
                    }

                    if (!programs.TryGetValue(widget.PageIndex.Value, out var operations))
                    {
                        graph.Context.Consume(PdfResourceKind.ExpandedBytes, 512);
                        programs.Add(widget.PageIndex.Value, operations = new());
                        resources.Add(widget.PageIndex.Value, graph.Copy(graph.Get(page.EffectiveAttributes, "Resources")));
                    }

                    var scope = resources[widget.PageIndex.Value];
                    var xobjects = scope.TryGetValue("XObject", out var old) ? graph.Copy(graph.Resolve(old)) : new Dictionary<string, PdfValue>(StringComparer.Ordinal);
                    string name = ResourceName(xobjects, "FormAppearance", graph);
                    xobjects.Add(name, reference);
                    scope["XObject"] = PdfValue.OwnedDictionary(xobjects);
                    graph.Context.Consume(PdfResourceKind.ExpandedBytes, 768);
                    operations.Add(new(PdfOperator.SaveState));
                    operations.Add(new(PdfOperator.Transform, mapping.Value.Items.ToArray()));
                    operations.Add(new(PdfOperator.DrawObject, PdfValue.Name(name)));
                    operations.Add(new(PdfOperator.RestoreState));
                }
            }

            var form = graph.Copy(reader.Dictionary);
            var roots = graph.Get(reader.Dictionary, "Fields");
            form["Fields"] = Filter(roots);
            var co = graph.Get(reader.Dictionary, "CO");
            if (co.Kind == PdfValueKind.Array)
            {
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, co.Items.Count * 32L + 64);
                var remaining = co.Items.Where(v => !removed.Contains(v.ReferenceId)).ToArray();
                if (remaining.Length == 0)
                    form.Remove("CO");
                else
                    form["CO"] = PdfValue.Array(remaining);
            }

            if (form["Fields"].Items.Count == 0)
            {
                var catalog = graph.Copy(graph.Catalog);
                catalog.Remove("AcroForm");
                graph.SetCatalog(catalog);
            }
            else
            {
                if (chosen.Any(f => f.Kind == PdfFormFieldKind.Signature) && form.TryGetValue("SigFlags", out var sig))
                {
                    uint flags = reader.Flags(graph.Resolve(sig)) & ~1u;
                    if (reader.Fields.Any(f => f.Kind == PdfFormFieldKind.Signature && !chosen.Contains(f)))
                        flags |= 1u;
                    form["SigFlags"] = PdfValue.Integer(flags);
                }

                PdfFormValues.Form(graph, form);
            }

            for (int i = 0; i < graph.Snapshot.Pages.Count; i++)
            {
                var page = graph.Snapshot.Pages[i].Source!;
                var dictionary = graph.Copy(graph.Edit.Resolve(page.ObjectId));
                var annotations = graph.Get(PdfValue.OwnedDictionary(dictionary), "Annots");
                if (annotations.Kind == PdfValueKind.Array)
                {
                    graph.Context.Consume(PdfResourceKind.ExpandedBytes, annotations.Items.Count * 32L + 64);
                    dictionary["Annots"] = PdfValue.Array(annotations.Items.Where(v => !widgetIds.Contains(v.ReferenceId)).ToArray());
                    graph.Edit.Set(page.ObjectId, PdfValue.OwnedDictionary(dictionary));
                }
            }

            AppendAppearances(graph, programs, resources);
            foreach (var id in removed)
                graph.Edit.Remove(id);
            RequireNoReferences(graph, removed);
            if (writeMode == PdfWriteMode.Full)
                graph.PruneUnreachable();
            _ = new PdfFormReader(graph);
            PdfValue Filter(PdfValue array)
            {
                using var depth = graph.Context.EnterScope(PdfResourceKind.Depth);
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, array.Items.Count * 48L + 64);
                var kept = new List<PdfValue>();
                foreach (var reference in array.Items)
                {
                    graph.Context.Checkpoint();
                    if (removed.Contains(reference.ReferenceId))
                        continue;
                    if (!reader.ById.ContainsKey(reference.ReferenceId))
                    {
                        var entries = graph.Copy(graph.Edit.Resolve(reference.ReferenceId));
                        var kids = graph.Get(PdfValue.OwnedDictionary(entries), "Kids");
                        if (kids.Kind != PdfValueKind.Array)
                            throw reader.Error("ContainerKids", "A field container requires Kids.");
                        var remaining = Filter(kids);
                        if (remaining.Items.Count == 0)
                        {
                            removed.Add(reference.ReferenceId);
                            continue;
                        }

                        entries["Kids"] = remaining;
                        graph.Edit.Set(reference.ReferenceId, PdfValue.OwnedDictionary(entries));
                    }

                    kept.Add(reference);
                }

                return PdfValue.OwnedArray(kept);
            }
        }, "1.7", writeMode);
    }

    private static List<PdfFormField> SelectFields(PdfFormReader reader, IEnumerable<string>? names)
    {
        var result = new List<PdfFormField>();
        var selected = new HashSet<string>(StringComparer.Ordinal);
        reader.Graph.Context.Consume(PdfResourceKind.ExpandedBytes, reader.Fields.Count * 128L + 64);
        var map = reader.Fields.ToDictionary(f => f.Name, StringComparer.Ordinal);
        using var enumerator = (names ?? reader.Fields.Select(f => f.Name)).GetEnumerator();
        while (true)
        {
            reader.Graph.Context.Checkpoint();
            reader.Graph.Context.Consume(PdfResourceKind.Objects, 1);
            if (!enumerator.MoveNext())
                break;
            reader.Graph.Context.Consume(PdfResourceKind.ExpandedBytes, 128);
            string name = enumerator.Current ?? throw new ArgumentException("Field names cannot be null.", nameof(names));
            if (!selected.Add(name))
                throw reader.Error("DuplicateSelection", "A field selection requires distinct exact names.");
            if (!map.TryGetValue(name, out var field))
                throw reader.Error("UnknownField", "The selected field name is unavailable.");
            result.Add(field);
        }

        return result;
    }

    private static PdfMatrix AppearanceMapping(PdfValue form, PdfRect target, PdfGraphEdit graph)
    {
        var bounds = PdfAnnotation.ReadRect(graph.Get(form, "BBox"), graph.Resolve, graph.Context);
        var matrix = form.Entries.TryGetValue("Matrix", out var raw) ? PdfAnnotation.ReadMatrix(raw, graph.Resolve, graph.Context) : PdfMatrix.Identity;
        var transformed = PdfGeometryMath.Transform(bounds, matrix).Bounds;
        if (transformed.Width <= 0 || transformed.Height <= 0)
            throw graph.Error("Forms.AppearanceBounds", "The transformed appearance bounds must be positive.");
        double sx = target.Width / transformed.Width, sy = target.Height / transformed.Height;
        return new(sx, 0, 0, sy, target.X - transformed.X * sx, target.Y - transformed.Y * sy);
    }

    private static string ResourceName(Dictionary<string, PdfValue> entries, string prefix, PdfGraphEdit graph)
    {
        int index = 0;
        string name = prefix;
        while (entries.ContainsKey(name))
        {
            graph.Context.Checkpoint();
            if (index == int.MaxValue)
                throw graph.Error("Forms.ResourceNames", "The private resource namespace is exhausted.");
            name = prefix + (++index).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return name;
    }

    private static PdfValue AppearanceWrapper(PdfValue reference, PdfValue form, PdfValue visibility, PdfGraphEdit graph)
    {
        var program = PdfContentProgram.CreateCore(new[] { new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Inner")) }, PdfContentKind.Page, graph.Context).ToStream(graph.Context);
        var entries = graph.Copy(program);
        entries["Type"] = PdfValue.Name("XObject");
        entries["Subtype"] = PdfValue.Name("Form");
        entries["BBox"] = graph.Get(form, "BBox");
        entries["OC"] = visibility;
        // The inner Form applies its own Matrix; the wrapper retains transformed outer bounds.
        var matrix = form.Entries.TryGetValue("Matrix", out var raw) ? PdfAnnotation.ReadMatrix(raw, graph.Resolve, graph.Context) : PdfMatrix.Identity;
        entries["BBox"] = PdfGeometryMath.Transform(PdfAnnotation.ReadRect(entries["BBox"], graph.Resolve, graph.Context), matrix).Bounds.Value;
        entries["Resources"] = PdfGraphicsNumbers.Dict(("XObject", PdfGraphicsNumbers.Dict(("Inner", reference))));
        return program.WithStreamDictionary(PdfValue.OwnedDictionary(entries));
    }

    private static void AppendAppearances(PdfGraphEdit graph, Dictionary<int, List<PdfContentOperation>> programs, Dictionary<int, Dictionary<string, PdfValue>> resources)
    {
        foreach (var pair in programs)
        {
            var binding = graph.Snapshot.Pages[pair.Key].Source!;
            PdfPageContent.Read(binding, PdfUnknownOperatorPolicy.Reject, null, PdfProviderRegistry.Default, graph.Context);
            var entries = graph.Copy(graph.Edit.Resolve(binding.ObjectId));
            var previous = entries.TryGetValue("Contents", out var raw) ? raw : PdfValue.Null;
            var prefix = new PdfByteBuilder(graph.Context);
            prefix.Add(113);
            prefix.Add(10);
            var pre = prefix.Build();
            var suffix = new PdfByteBuilder(graph.Context);
            suffix.Add(81);
            suffix.Add(10);
            bool tagged = graph.Catalog.Entries.ContainsKey("StructTreeRoot");
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, pair.Value.Count * 16L + 128);
            var operations = new List<PdfContentOperation>();
            if (tagged)
                operations.Add(new(PdfOperator.BeginMarkedContent, PdfValue.Name("Artifact")));
            operations.AddRange(pair.Value);
            if (tagged)
                operations.Add(new(PdfOperator.EndMarkedContent));
            var program = PdfContentProgram.CreateCore(operations, PdfContentKind.Page, graph.Context).ToStream(graph.Context);
            using (var input = program.OpenRawStream())
            {
                int b;
                while ((b = input.ReadByte()) >= 0)
                {
                    graph.Context.Checkpoint();
                    suffix.Add((byte)b);
                }
            }

            var post = suffix.Build();
            var streams = new List<PdfValue>
            {
                PdfValue.Reference(graph.Edit.Add(PdfValue.StreamValue(PdfGraphicsNumbers.Dict(), pre, 0, pre.Length)))
            };
            Append(previous);
            streams.Add(PdfValue.Reference(graph.Edit.Add(PdfValue.StreamValue(PdfGraphicsNumbers.Dict(), post, 0, post.Length))));
            entries["Contents"] = PdfValue.OwnedArray(streams);
            entries["Resources"] = PdfValue.OwnedDictionary(resources[pair.Key]);
            graph.Edit.Set(binding.ObjectId, PdfValue.OwnedDictionary(entries));
            void Append(PdfValue value)
            {
                graph.Context.Checkpoint();
                using var depth = graph.Context.EnterScope(PdfResourceKind.Depth);
                graph.Context.Consume(PdfResourceKind.Objects, 1);
                var resolved = graph.Resolve(value);
                if (resolved.Kind == PdfValueKind.Null)
                    return;
                if (resolved.Kind == PdfValueKind.Array)
                {
                    foreach (var child in resolved.Items)
                        Append(child);
                    return;
                }

                if (resolved.Kind != PdfValueKind.Stream)
                    throw graph.Error("Forms.SourceContents", "Page content must be a stream or array of streams.");
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, 32);
                streams.Add(value.Kind == PdfValueKind.Reference ? value : PdfValue.Reference(graph.Edit.Add(resolved)));
            }
        }
    }

    private static void RequireNoReferences(PdfGraphEdit graph, HashSet<PdfObjectId> removed)
    {
        var visited = new HashSet<PdfObjectId>();
        Visit(graph.Edit.Trailer);
        void Visit(PdfValue value)
        {
            graph.Context.Checkpoint();
            using var depth = graph.Context.EnterScope(PdfResourceKind.Depth);
            graph.Context.Consume(PdfResourceKind.Objects, 1);
            while (value.Kind == PdfValueKind.Reference)
            {
                if (removed.Contains(value.ReferenceId))
                    throw graph.Error("Forms.FieldReferenceProfile", "A surviving source relationship refers to a removed field/widget; an explicit owner rewrite is required.");
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, 48);
                if (!visited.Add(value.ReferenceId))
                    return;
                value = graph.Edit.Resolve(value.ReferenceId);
            }

            if (value.Kind == PdfValueKind.Array)
                foreach (var item in value.Items)
                    Visit(item);
            else if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
                foreach (var child in value.Entries.Values)
                    Visit(child);
        }
    }
}
