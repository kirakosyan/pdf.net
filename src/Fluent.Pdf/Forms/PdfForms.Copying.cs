using System.Threading;

namespace Fluent.Pdf;
public static partial class PdfForms
{
    /// <summary>Copies one standard separate field/widget graph to an explicit new root partial name. Every widget/navigation page needs an explicit source-to-target page mapping; scripts remain passive.</summary>
    public static PdfDocument CopyField(PdfFormField field, PdfDocumentSnapshot target, string partialName, IReadOnlyDictionary<int, int> pageMap, bool retainOpaqueNameReferences = false, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (field == null)
            throw new ArgumentNullException(nameof(field));
        if (pageMap == null)
            throw new ArgumentNullException(nameof(pageMap));
        Guard.Text(partialName, nameof(partialName), 8192);
        if (partialName.Contains('.'))
            throw new ArgumentException("The copied root partial name cannot contain a period.", nameof(partialName));
        return PdfGraphEdit.Run("CopyFormField", target, options, policy, cancellationToken, graph =>
        {
            var targetReader = new PdfFormReader(graph);
            targetReader.Editable();
            Regeneration(targetReader, false);
            SignaturePermissions(graph, options);
            if (graph.Catalog.Entries.ContainsKey("StructTreeRoot"))
                throw targetReader.Error("StructureCreationProfile", "Copying widgets into a tagged target requires an explicit structure creation profile.");
            using var sourceGraph = new PdfGraphEdit(field.Source, new(), graph.Context);
            var reader = new PdfFormReader(sourceGraph);
            reader.Editable();
            Regeneration(reader, false);
            CheckNameReferences(sourceGraph, retainOpaqueNameReferences);
            if (field.Representations.Count != 1 || !reader.ById.TryGetValue(field.Representations[0], out var current))
                throw reader.Error("RepresentationProfile", "Copying multiple legacy representations requires an explicit consolidation profile.");
            if (current.Kind == PdfFormFieldKind.Unknown)
                throw reader.Error("UnsupportedType", "Copying an unknown field type requires an explicit semantic profile.");
            var original = sourceGraph.Edit.Resolve(current.Representations[0]);
            if (reader.Name(original, "Subtype") == "Widget")
                throw reader.Error("MergedWidgetProfile", "Merged field/widgets require an explicit action/ownership splitting profile before copying.");
            if (current.Kind == PdfFormFieldKind.Signature && current.Value.Kind != PdfValueKind.Null)
                throw reader.Error("SignedFieldCopy", "A cryptographic signature cannot be copied as a new valid field value.");
            if (original.Entries.ContainsKey("Lock"))
            {
                if (!retainOpaqueNameReferences)
                    throw reader.Error("OpaqueNameReferences", "A copied signature lock may name source fields; explicit retention is required.");
                graph.Context.Report(new("Forms.OpaqueNameReferencesRetained", "Caller retained passive signature-lock field-name references without rewriting them.", severity: PdfDiagnosticSeverity.Warning));
            }

            var roots = graph.Get(targetReader.Dictionary, "Fields");
            if (roots.Kind == PdfValueKind.Array)
                foreach (var root in roots.Items)
                {
                    var name = graph.Get(graph.Resolve(root), "T");
                    if (name.Kind != PdfValueKind.Null && PdfTextStrings.Decode(name, graph.Context) == partialName)
                        throw targetReader.Error("DuplicateName", "The copied root name already belongs to a target field/container.");
                }

            if (targetReader.Fields.Any(f => f.Name == partialName || f.Name.StartsWith(partialName + ".", StringComparison.Ordinal)))
                throw targetReader.Error("DuplicateName", "The copied name collides with the target hierarchy.");
            var importer = new PdfGraphImport(graph, sourceGraph.Snapshot);
            var routes = new Dictionary<int, int>();
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, pageMap.Count * 128L + current.Widgets.Count * 256L + 512);
            foreach (var pair in pageMap)
            {
                graph.Context.Checkpoint();
                if (pair.Key < 0 || pair.Key >= sourceGraph.Snapshot.Pages.Count || pair.Value < 0 || pair.Value >= graph.Snapshot.Pages.Count)
                    throw new ArgumentOutOfRangeException(nameof(pageMap));
                importer.Map.Add(sourceGraph.Snapshot.Pages[pair.Key].Source!.ObjectId, graph.Snapshot.Pages[pair.Value].Source!.ObjectId);
                routes.Add(pair.Key, pair.Value);
            }

            var fieldId = graph.Edit.Add(PdfValue.Null);
            importer.Map.Add(current.Representations[0], fieldId);
            var allowed = new HashSet<PdfObjectId>(current.Representations);
            var additions = new Dictionary<int, List<PdfValue>>();
            foreach (var widget in current.Widgets)
            {
                graph.Context.Checkpoint();
                if (!widget.PageIndex.HasValue || !routes.ContainsKey(widget.PageIndex.Value))
                    throw reader.Error("WidgetPageMap", "Every copied widget needs an explicit attached target page.");
                allowed.Add(widget.Id);
                importer.Map.Add(widget.Id, graph.Edit.Add(PdfValue.Null));
            }

            var entries = sourceGraph.Copy(original);
            entries.Remove("Parent");
            entries.Remove("Kids");
            entries.Remove("T");
            foreach (var property in current.Inherited.Entries)
                if (property.Key != "DR")
                    entries[property.Key] = property.Value;
            foreach (var value in entries.Values)
                CheckRelationships(value);
            var cloned = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, entries.Count * 96L + 64);
            foreach (var property in entries)
            {
                var imported = importer.Clone(property.Value);
                if (imported.Kind == PdfValueKind.Stream)
                    imported = PdfValue.Reference(graph.Edit.Add(imported));
                cloned.Add(property.Key, imported);
            }

            cloned["T"] = PdfTextStrings.Encode(partialName, graph.Context);
            var form = graph.Copy(targetReader.Dictionary);
            CopyDefaultAppearance(current, sourceGraph, graph, importer, cloned, form, CheckRelationships);
            var children = new List<PdfValue>();
            var widgetNames = new Dictionary<int, HashSet<string>>();
            foreach (var widget in current.Widgets)
            {
                int targetPage = routes[widget.PageIndex!.Value];
                CheckRelationships(widget.Definition);
                var dictionary = graph.Copy(importer.Clone(widget.Definition));
                dictionary["Parent"] = PdfValue.Reference(fieldId);
                dictionary["P"] = PdfValue.Reference(graph.Snapshot.Pages[targetPage].Source!.ObjectId);
                if (dictionary.TryGetValue("NM", out var rawName))
                {
                    string name = PdfTextStrings.Decode(graph.Resolve(rawName), graph.Context);
                    if (!widgetNames.TryGetValue(targetPage, out var used))
                    {
                        widgetNames.Add(targetPage, used = new(StringComparer.Ordinal));
                        var annotations = graph.Get(graph.Edit.Resolve(graph.Snapshot.Pages[targetPage].Source!.ObjectId), "Annots");
                        if (annotations.Kind == PdfValueKind.Array)
                            foreach (var reference in annotations.Items)
                            {
                                var existing = graph.Get(graph.Resolve(reference), "NM");
                                if (existing.Kind == PdfValueKind.Null)
                                    continue;
                                graph.Context.Consume(PdfResourceKind.ExpandedBytes, 64);
                                used.Add(PdfTextStrings.Decode(existing, graph.Context));
                            }
                    }

                    if (!used.Add(name))
                        throw targetReader.Error("WidgetIdentifierCollision", "A copied annotation NM identifier collides on its target page.");
                }

                var id = importer.Map[widget.Id];
                graph.Edit.Set(id, PdfValue.OwnedDictionary(dictionary));
                children.Add(PdfValue.Reference(id));
                if (!additions.TryGetValue(targetPage, out var added))
                    additions.Add(targetPage, added = new());
                added.Add(PdfValue.Reference(id));
            }

            if (children.Count != 0)
                cloned["Kids"] = PdfValue.OwnedArray(children);
            graph.Edit.Set(fieldId, PdfValue.OwnedDictionary(cloned));
            foreach (var pair in additions)
            {
                var page = graph.Snapshot.Pages[pair.Key].Source!;
                var dictionary = graph.Copy(graph.Edit.Resolve(page.ObjectId));
                var old = graph.Get(PdfValue.OwnedDictionary(dictionary), "Annots");
                if (old.Kind != PdfValueKind.Null && old.Kind != PdfValueKind.Array)
                    throw targetReader.Error("Annotations", "Target Annots must be an array.");
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, (old.Kind == PdfValueKind.Array ? old.Items.Count : 0) * 32L + pair.Value.Count * 32L + 64);
                var annotations = old.Kind == PdfValueKind.Array ? old.Items.ToList() : new List<PdfValue>();
                annotations.AddRange(pair.Value);
                dictionary["Annots"] = PdfValue.OwnedArray(annotations);
                graph.Edit.Set(page.ObjectId, PdfValue.OwnedDictionary(dictionary));
            }

            graph.Context.Consume(PdfResourceKind.ExpandedBytes, (roots.Kind == PdfValueKind.Array ? roots.Items.Count : 0) * 32L + 64);
            var rootList = roots.Kind == PdfValueKind.Array ? roots.Items.ToList() : new List<PdfValue>();
            rootList.Add(PdfValue.Reference(fieldId));
            form["Fields"] = PdfValue.OwnedArray(rootList);
            if (!form.ContainsKey("NeedAppearances"))
                form["NeedAppearances"] = PdfValue.Boolean(false);
            if (graph.Get(graph.Get(PdfValue.OwnedDictionary(cloned), "AA"), "C").Kind != PdfValueKind.Null)
            {
                var old = graph.Get(targetReader.Dictionary, "CO");
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, (old.Kind == PdfValueKind.Array ? old.Items.Count : 0) * 32L + 64);
                var order = old.Kind == PdfValueKind.Array ? old.Items.ToList() : new List<PdfValue>();
                order.Add(PdfValue.Reference(fieldId));
                form["CO"] = PdfValue.OwnedArray(order);
            }

            if (current.Kind == PdfFormFieldKind.Signature)
            {
                uint flags = targetReader.Flags(graph.Get(targetReader.Dictionary, "SigFlags"));
                if ((flags & ~3u) != 0)
                    throw targetReader.Error("SignatureFlags", "Unknown signature flags require a supported editing profile.");
                form["SigFlags"] = PdfValue.Integer(flags | 1u);
            }

            PdfFormValues.Form(graph, form);
            _ = new PdfFormReader(graph);
            void CheckRelationships(PdfValue value)
            {
                var visited = new HashSet<PdfObjectId>();
                Visit(value);
                void Visit(PdfValue item)
                {
                    graph.Context.Checkpoint();
                    using var depth = graph.Context.EnterScope(PdfResourceKind.Depth);
                    graph.Context.Consume(PdfResourceKind.Objects, 1);
                    while (item.Kind == PdfValueKind.Reference)
                    {
                        var id = item.ReferenceId;
                        if (reader.IsFieldIdentity(id))
                        {
                            if (!allowed.Contains(id))
                                throw reader.Error("CrossFieldCopyReference", "The copied graph refers to an unselected source field/widget owner.");
                            return;
                        }

                        if (sourceGraph.Snapshot.Pages.Any(p => p.Source!.ObjectId == id))
                            return;
                        graph.Context.Consume(PdfResourceKind.ExpandedBytes, 48);
                        if (!visited.Add(id))
                            return;
                        item = sourceGraph.Edit.Resolve(id);
                    }

                    if (item.Kind == PdfValueKind.Array)
                        foreach (var child in item.Items)
                            Visit(child);
                    else if (item.Kind == PdfValueKind.Dictionary || item.Kind == PdfValueKind.Stream)
                        foreach (var child in item.Entries.Values)
                            Visit(child);
                }
            }
        }, "1.7");
    }

    /// <summary>Overlays a private Form on every normal/rollover/down appearance state, preserving values, state names, widget identity, actions and structure associations.</summary>
    public static PdfDocument DecorateWidget(PdfFormField field, PdfObjectId widgetId, PdfGraphicsResource decoration, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (field == null)
            throw new ArgumentNullException(nameof(field));
        if (decoration == null)
            throw new ArgumentNullException(nameof(decoration));
        return PdfGraphEdit.Run("DecorateFormWidget", field.Source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfFormReader(graph);
            reader.Editable();
            Regeneration(reader, false);
            SignaturePermissions(graph, options);
            var current = reader.ById[field.Representations[0]];
            var widget = current.Widgets.SingleOrDefault(w => w.Id == widgetId);
            if (widget == null)
                throw reader.Error("WidgetIdentity", "The widget must belong to the selected logical field.");
            if (widget.Bounds.Width <= 0 || widget.Bounds.Height <= 0)
                throw reader.Error("AppearanceBounds", "Decoration requires positive widget bounds.");
            PdfAnnotation.ValidateAppearance(decoration, graph.Context);
            var writer = new PdfResourceWriter(graph.Edit, graph.Context, new());
            var added = writer.Embed(decoration);
            var addedForm = graph.Resolve(added);
            var ap = graph.Copy(graph.Get(widget.Definition, "AP"));
            if (!ap.ContainsKey("N"))
                throw reader.Error("AppearanceRequired", "A normal appearance must exist before decoration.");
            foreach (string key in new[]
            {
                "N",
                "R",
                "D"
            }

            )
            {
                if (!ap.TryGetValue(key, out var raw))
                    continue;
                var value = graph.Resolve(raw);
                if (value.Kind == PdfValueKind.Stream)
                    ap[key] = Overlay(raw, value);
                else if (value.Kind == PdfValueKind.Dictionary)
                {
                    var states = graph.Copy(value);
                    foreach (string state in states.Keys.ToArray())
                    {
                        var reference = states[state];
                        states[state] = Overlay(reference, graph.Resolve(reference));
                    }

                    ap[key] = PdfValue.OwnedDictionary(states);
                }
                else
                    throw reader.Error("AppearanceState", "Appearance entries must be Forms or state dictionaries.");
            }

            var entries = graph.Copy(widget.Definition);
            entries["AP"] = PdfValue.OwnedDictionary(ap);
            graph.Edit.Set(widget.Id, PdfValue.OwnedDictionary(entries));
            _ = new PdfFormReader(graph);
            PdfValue Overlay(PdfValue reference, PdfValue original)
            {
                RequireForm(original, graph);
                var bounds = new PdfRect(0, 0, widget.Bounds.Width, widget.Bounds.Height);
                if (reference.Kind != PdfValueKind.Reference)
                    reference = PdfValue.Reference(graph.Edit.Add(original));
                var first = AppearanceMapping(original, bounds, graph);
                var second = AppearanceMapping(addedForm, bounds, graph);
                var operations = new[]
                {
                    new PdfContentOperation(PdfOperator.SaveState),
                    new PdfContentOperation(PdfOperator.Transform, first.Value.Items.ToArray()),
                    new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Original")),
                    new PdfContentOperation(PdfOperator.RestoreState),
                    new PdfContentOperation(PdfOperator.SaveState),
                    new PdfContentOperation(PdfOperator.Transform, second.Value.Items.ToArray()),
                    new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Decoration")),
                    new PdfContentOperation(PdfOperator.RestoreState)
                };
                var program = PdfContentProgram.CreateCore(operations, PdfContentKind.Page, graph.Context).ToStream(graph.Context);
                var definition = graph.Copy(program);
                definition["Type"] = PdfValue.Name("XObject");
                definition["Subtype"] = PdfValue.Name("Form");
                definition["BBox"] = bounds.Value;
                definition["Resources"] = PdfGraphicsNumbers.Dict(("XObject", PdfGraphicsNumbers.Dict(("Original", reference), ("Decoration", added))));
                return PdfValue.Reference(graph.Edit.Add(program.WithStreamDictionary(PdfValue.OwnedDictionary(definition))));
            }
        }, "1.7");
    }

    private static void CopyDefaultAppearance(PdfFormField field, PdfGraphEdit source, PdfGraphEdit target, PdfGraphImport importer, Dictionary<string, PdfValue> entries, Dictionary<string, PdfValue> form, Action<PdfValue> checkRelationships)
    {
        var da = source.Get(field.Inherited, "DA");
        bool variable = field.Kind == PdfFormFieldKind.Text || field.Kind == PdfFormFieldKind.ComboBox || field.Kind == PdfFormFieldKind.ListBox;
        if (da.Kind == PdfValueKind.Null)
        {
            if (variable)
                throw source.Error("Forms.EditingFontRequired", "Copied variable text requires a declared default appearance and editing font.");
            return;
        }

        if (da.Kind != PdfValueKind.String)
            throw source.Error("Forms.DefaultAppearanceProfile", "DA must be a byte content string.");
        var bytes = new PdfByteBuilder(target.Context);
        bytes.Add(66);
        bytes.Add(84);
        bytes.Add(10);
        foreach (byte b in da.StringBytes)
            bytes.Add(b);
        bytes.Add(10);
        bytes.Add(69);
        bytes.Add(84);
        bytes.Add(10);
        var parsed = PdfContentProgram.ParseCore(bytes.Build(), PdfContentKind.Page, PdfUnknownOperatorPolicy.Reject, null, target.Context);
        var defaults = target.Copy(target.Get(PdfValue.OwnedDictionary(form), "DR"));
        var fonts = target.Copy(target.Get(PdfValue.OwnedDictionary(defaults), "Font"));
        var sourceFonts = source.Get(source.Get(field.Inherited, "DR"), "Font");
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var operations = new List<PdfContentOperation>();
        bool hasFont = false;
        target.Context.Consume(PdfResourceKind.ExpandedBytes, parsed.Operations.Count * 256L + 128);
        foreach (var operation in parsed.Operations)
        {
            target.Context.Checkpoint();
            if (operation.Operation == PdfOperator.BeginText || operation.Operation == PdfOperator.EndText)
            {
                operations.Add(operation);
                continue;
            }

            if (operation.Operation == PdfOperator.Font)
            {
                hasFont = true;
                string name = operation.Operands[0].NameValue;
                if (!names.TryGetValue(name, out var renamed))
                {
                    if (sourceFonts.Kind != PdfValueKind.Dictionary || !sourceFonts.Entries.TryGetValue(name, out var font))
                        throw source.Error("Forms.EditingFontRequired", "A DA font is absent from the source default resources.");
                    checkRelationships(font);
                    renamed = ResourceName(fonts, "CopiedFont", target);
                    fonts.Add(renamed, importer.Clone(font));
                    names.Add(name, renamed);
                }

                operations.Add(new(PdfOperator.Font, PdfValue.Name(renamed), operation.Operands[1]));
            }
            else if (new[]
            {
                "Tc",
                "Tw",
                "Tz",
                "TL",
                "Tr",
                "Ts",
                "g",
                "rg",
                "k",
                "G",
                "RG",
                "K"
            }.Contains(operation.Token, StringComparer.Ordinal))
                operations.Add(operation);
            else
                throw source.Error("Forms.DefaultAppearanceProfile", "Additional resource-dependent DA operators require a default-resource rebinding profile.");
        }

        if (variable && !hasFont)
            throw source.Error("Forms.EditingFontRequired", "The copied variable-text default appearance requires an explicit font.");
        _ = PdfContentProgram.CreateCore(operations, PdfContentKind.Page, target.Context);
        using var buffer = new PdfCodecBufferStream(target.Context, target.Context.Available(PdfResourceKind.ExpandedBytes));
        var sink = new PdfWriterCore.Sink(buffer, target.Context);
        foreach (var operation in operations)
        {
            if (operation.Operation == PdfOperator.BeginText || operation.Operation == PdfOperator.EndText)
                continue;
            foreach (var operand in operation.Operands)
            {
                sink.Value(operand, false);
                sink.Text(" ");
            }

            sink.Text(operation.Token);
            sink.Text("\n");
        }

        sink.Flush();
        var data = buffer.Finish();
        if (data.Length > int.MaxValue)
            throw target.Error("Forms.DefaultAppearanceProfile", "The rewritten default appearance exceeds its supported extent.");
        target.Context.Consume(PdfResourceKind.ExpandedBytes, data.Length * 2L + 64);
        var encoded = new byte[(int)data.Length];
        using (var input = data.Open(0, data.Length))
        {
            int at = 0;
            while (at < encoded.Length)
            {
                target.Context.Checkpoint();
                int n = input.Read(encoded, at, encoded.Length - at);
                if (n == 0)
                    break;
                at += n;
            }
        }

        entries["DA"] = PdfValue.String(encoded);
        defaults["Font"] = PdfValue.OwnedDictionary(fonts);
        form["DR"] = PdfValue.OwnedDictionary(defaults);
    }
}
