using System.Globalization;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Managed passive AcroForm creation, field/widget editing and calculation metadata.</summary>
public static partial class PdfForms
{
    /// <summary>Reads field inheritance, consistent duplicate representations and widget ownership without evaluating scripts/XFA.</summary>
    public static PdfFormSet Inspect(PdfDocumentSnapshot source, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        PdfFormReader? reader = null;
        var report = PdfFont.Run("InspectForms", "managed.forms", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            reader = new(graph);
        }, PdfCapability.ReadDocument);
        return new(reader!.Graph.Snapshot, reader.Dictionary, reader.Fields, report);
    }

    /// <summary>Adds a terminal field and owned widgets, creating missing hierarchical containers. Duplicate logical names reject; use AddWidget to share a value.</summary>
    public static PdfDocument Add(PdfDocumentSnapshot source, PdfFormFieldDefinition definition, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        return PdfGraphEdit.Run("AddFormField", source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfFormReader(graph);
            reader.Editable();
            graph.Context.Consume(PdfResourceKind.Objects, definition.Widgets.Count * 32L + 64);
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, definition.Widgets.Count * 128L + 4096);
            if (reader.Fields.Any(f => f.Name == definition.Name))
                throw reader.Error("DuplicateName", "A logical field with this name already exists.");
            if (graph.Catalog.Entries.ContainsKey("StructTreeRoot"))
                throw reader.Error("StructureCreationProfile", "Adding tagged widgets requires an explicit structure creation profile.");
            uint flags = PdfFormValues.Flags(definition, graph);
            var choices = PdfFormValues.Choices(definition, graph);
            var onNames = new List<string>();
            var resources = new PdfResourceWriter(graph.Edit, graph.Context, new());
            foreach (var widget in definition.Widgets)
                ValidateWidget(widget, definition.Kind, graph, onNames);
            if (definition.Kind == PdfFormFieldKind.RadioButton && (flags & 33554432) == 0 && onNames.Distinct(StringComparer.Ordinal).Count() != onNames.Count)
                throw reader.Error("RadioExports", "Radio exports must be distinct unless RadiosInUnison is explicit.");
            if (definition.Kind == PdfFormFieldKind.CheckBox && onNames.Distinct(StringComparer.Ordinal).Count() != 1)
                throw reader.Error("CheckboxExports", "Shared checkbox widgets must use the same on-state name.");
            PdfFormValues.Value(definition.Kind, flags, definition.Value, choices, definition.MaximumLength, onNames, graph);
            PdfFormValues.Value(definition.Kind, flags, definition.DefaultValue, choices, definition.MaximumLength, onNames, graph);
            var form = graph.Copy(reader.Dictionary);
            var roots = graph.Get(reader.Dictionary, "Fields");
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, (roots.Kind == PdfValueKind.Array ? roots.Items.Count : 0) * 32L + definition.Name.Length * 16L + 1024);
            var rootList = roots.Kind == PdfValueKind.Array ? roots.Items.ToList() : new List<PdfValue>();
            string[] segments = definition.Name.Split('.');
            if (segments.Length > graph.Context.Policy.Limits.MaximumDepth)
                graph.Context.RejectLimit(PdfResourceKind.Depth);
            PdfObjectId? parent = null;
            var siblings = rootList;
            for (int i = 0; i < segments.Length - 1; i++)
            {
                graph.Context.Checkpoint();
                PdfObjectId? found = null;
                foreach (var child in siblings)
                {
                    var value = graph.Resolve(child);
                    var partial = graph.Get(value, "T");
                    if (partial.Kind != PdfValueKind.Null && PdfTextStrings.Decode(partial, graph.Context) == segments[i])
                    {
                        if (found.HasValue)
                            throw reader.Error("DuplicateName", "The hierarchical container name is ambiguous.");
                        found = child.ReferenceId;
                    }
                }

                if (!found.HasValue)
                {
                    var entries = new Dictionary<string, PdfValue>
                    {
                        {
                            "T",
                            PdfTextStrings.Encode(segments[i], graph.Context)
                        },
                        {
                            "Kids",
                            PdfValue.Array()
                        }
                    };
                    if (parent.HasValue)
                        entries["Parent"] = PdfValue.Reference(parent.Value);
                    found = graph.Edit.Add(PdfValue.OwnedDictionary(entries));
                    siblings.Add(PdfValue.Reference(found.Value));
                    SaveSiblings(parent, siblings);
                }
                else if (reader.ById.ContainsKey(found.Value))
                    throw reader.Error("TerminalNamePrefix", "A terminal field cannot become a hierarchical container implicitly.");
                parent = found.Value;
                var children = graph.Get(graph.Edit.Resolve(parent.Value), "Kids");
                if (children.Kind != PdfValueKind.Array)
                    throw reader.Error("ContainerKids", "A field container requires Kids.");
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, children.Items.Count * 32L + 64);
                siblings = children.Items.ToList();
            }

            var field = new Dictionary<string, PdfValue>
            {
                {
                    "T",
                    PdfTextStrings.Encode(segments[segments.Length - 1], graph.Context)
                },
                {
                    "FT",
                    PdfValue.Name(PdfFormValues.Type(definition.Kind))
                },
                {
                    "Ff",
                    PdfValue.Integer(flags)
                }
            };
            if (parent.HasValue)
                field["Parent"] = PdfValue.Reference(parent.Value);
            if (definition.Value.Kind != PdfValueKind.Null)
                field["V"] = definition.Value;
            if (definition.DefaultValue.Kind != PdfValueKind.Null)
                field["DV"] = definition.DefaultValue;
            if (definition.MaximumLength.HasValue)
                field["MaxLen"] = PdfValue.Integer(definition.MaximumLength.Value);
            if (definition.AlternateName != null)
                field["TU"] = PdfTextStrings.Encode(definition.AlternateName, graph.Context);
            if (definition.MappingName != null)
                field["TM"] = PdfTextStrings.Encode(definition.MappingName, graph.Context);
            if (definition.Kind == PdfFormFieldKind.ComboBox || definition.Kind == PdfFormFieldKind.ListBox)
            {
                field["Opt"] = PdfFormValues.Options(choices, graph);
                PdfFormValues.Selection(field, definition.Value, choices, graph);
            }

            if (definition.EditingFont != null)
            {
                string name = "FF" + graph.Edit.ActiveIds(graph.Context).Count().ToString(CultureInfo.InvariantCulture);
                var defaults = graph.Copy(graph.Get(reader.Dictionary, "DR"));
                var fonts = graph.Copy(graph.Get(PdfValue.OwnedDictionary(defaults), "Font"));
                while (fonts.ContainsKey(name))
                {
                    graph.Context.Checkpoint();
                    if (name.Length >= 127)
                        throw reader.Error("EditingFontNames", "The generated editing-font namespace is exhausted.");
                    name += "F";
                }

                fonts[name] = resources.Embed(PdfGraphicsResource.Font(definition.EditingFont));
                defaults["Font"] = PdfValue.OwnedDictionary(fonts);
                form["DR"] = PdfValue.OwnedDictionary(defaults);
                field["DA"] = PdfValue.String(System.Text.Encoding.ASCII.GetBytes("/" + name + " " + PdfValues.Real(definition.FontSize).NumberText + " Tf 0 g"));
            }
            else if (definition.Kind == PdfFormFieldKind.Text || definition.Kind == PdfFormFieldKind.ComboBox || definition.Kind == PdfFormFieldKind.ListBox)
                throw reader.Error("EditingFontRequired", "Variable text fields require an explicit editing font in addition to their private appearances.");
            var id = graph.Edit.Add(PdfValue.Null);
            var widgets = new List<PdfValue>();
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, definition.Widgets.Count * 64L + 256);
            foreach (var widget in definition.Widgets)
                widgets.Add(PdfValue.Reference(Widget(graph, resources, id, widget, definition.Value)));
            field["Kids"] = PdfValue.OwnedArray(widgets);
            graph.Edit.Set(id, PdfValue.OwnedDictionary(field));
            siblings.Add(PdfValue.Reference(id));
            SaveSiblings(parent, siblings);
            form["Fields"] = PdfValue.OwnedArray(rootList);
            if (!form.ContainsKey("NeedAppearances"))
                form["NeedAppearances"] = PdfValue.Boolean(false);
            if (definition.Kind == PdfFormFieldKind.Signature)
            {
                var old = graph.Get(reader.Dictionary, "SigFlags");
                uint signatureFlags = reader.Flags(old);
                if ((signatureFlags & ~3u) != 0)
                    throw reader.Error("SignatureFlags", "Unknown signature flags require a supported editing profile.");
                form["SigFlags"] = PdfValue.Integer(signatureFlags | 1u);
            }

            PdfFormValues.Form(graph, form);
            _ = new PdfFormReader(graph);
            void SaveSiblings(PdfObjectId? owner, List<PdfValue> values)
            {
                if (!owner.HasValue)
                    return;
                var entries = graph.Copy(graph.Edit.Resolve(owner.Value));
                entries["Kids"] = PdfValue.OwnedArray(values);
                graph.Edit.Set(owner.Value, PdfValue.OwnedDictionary(entries));
            }
        }, "1.7");
    }

    /// <summary>Adds another widget representation of an existing logical field without creating a duplicate name.</summary>
    public static PdfDocument AddWidget(PdfFormField field, PdfFormWidgetDefinition widget, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (field == null)
            throw new ArgumentNullException(nameof(field));
        if (widget == null)
            throw new ArgumentNullException(nameof(widget));
        return PdfGraphEdit.Run("AddFormWidget", field.Source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfFormReader(graph);
            reader.Editable();
            if (graph.Catalog.Entries.ContainsKey("StructTreeRoot"))
                throw reader.Error("StructureCreationProfile", "Adding tagged widgets requires a supported structure creation profile.");
            if (field.Representations.Count != 1 || !reader.ById.TryGetValue(field.Representations[0], out var current))
                throw reader.Error("RepresentationProfile", "Adding widgets to multiple legacy terminal representations requires explicit consolidation.");
            var id = current.Representations[0];
            var entries = graph.Copy(graph.Edit.Resolve(id));
            if (reader.Name(PdfValue.OwnedDictionary(entries), "Subtype") == "Widget")
                throw reader.Error("MergedWidgetProfile", "Split a merged field/widget explicitly before adding another widget.");
            if (current.Kind == PdfFormFieldKind.Unknown)
                throw reader.Error("UnsupportedType", "Adding widgets requires a supported field kind.");
            ValidateWidget(widget, current.Kind, graph, new());
            var names = new List<string>();
            foreach (var existing in current.Widgets)
            {
                var normal = graph.Get(graph.Get(existing.Definition, "AP"), "N");
                if (normal.Kind == PdfValueKind.Dictionary)
                    foreach (var name in normal.Entries.Keys)
                        if (name != "Off")
                            names.Add(name);
            }

            if (current.Kind == PdfFormFieldKind.CheckBox && names.Any(x => x != widget.ExportValue))
                throw reader.Error("CheckboxExports", "Shared checkbox widgets must use the same on-state name.");
            if (current.Kind == PdfFormFieldKind.RadioButton && (current.Flags & 33554432) == 0 && names.Contains(widget.ExportValue!))
                throw reader.Error("RadioExports", "Radio exports must be distinct unless RadiosInUnison is explicit.");
            var resources = new PdfResourceWriter(graph.Edit, graph.Context, new());
            var created = Widget(graph, resources, id, widget, current.Value);
            var old = graph.Get(PdfValue.OwnedDictionary(entries), "Kids");
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, old.Kind == PdfValueKind.Array ? old.Items.Count * 32L + 64 : 64);
            var children = old.Kind == PdfValueKind.Array ? old.Items.ToList() : new List<PdfValue>();
            children.Add(PdfValue.Reference(created));
            entries["Kids"] = PdfValue.OwnedArray(children);
            graph.Edit.Set(id, PdfValue.OwnedDictionary(entries));
            _ = new PdfFormReader(graph);
        }, "1.7");
    }

    /// <summary>Renames the nearest named field ancestor, preserving identities, widgets, tags and calculation references. Descendant qualified names follow that ancestor.</summary>
    public static PdfDocument Rename(PdfFormField field, string partialName, bool retainOpaqueNameReferences = false, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (field == null)
            throw new ArgumentNullException(nameof(field));
        Guard.Text(partialName, nameof(partialName), 8192);
        if (partialName.Contains('.'))
            throw new ArgumentException("A partial name cannot contain a period.", nameof(partialName));
        return PdfGraphEdit.Run("RenameFormField", field.Source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfFormReader(graph);
            reader.Editable();
            CheckNameReferences(graph, retainOpaqueNameReferences);
            if (!reader.NameOwners.TryGetValue(field.Name, out var owner))
                throw reader.Error("NameOwner", "The field has no named ancestor to rename.");
            var entries = graph.Copy(graph.Edit.Resolve(owner));
            entries["T"] = PdfTextStrings.Encode(partialName, graph.Context);
            graph.Edit.Set(owner, PdfValue.OwnedDictionary(entries));
            _ = new PdfFormReader(graph);
        }, "1.7");
    }

    /// <summary>Sets the passive calculation order using exact terminal identities. No field action executes.</summary>
    public static PdfDocument SetCalculationOrder(PdfDocumentSnapshot source, IReadOnlyList<PdfObjectId> fields, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (fields == null)
            throw new ArgumentNullException(nameof(fields));
        return PdfGraphEdit.Run("SetFormCalculationOrder", source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfFormReader(graph);
            reader.Editable();
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, fields.Count * 96L + 64);
            var seen = new HashSet<PdfObjectId>();
            var order = new List<PdfValue>();
            foreach (var id in fields)
            {
                graph.Context.Checkpoint();
                if (!reader.ById.ContainsKey(id) || !seen.Add(id))
                    throw reader.Error("CalculationOrder", "Calculation order requires distinct known terminal fields.");
                order.Add(PdfValue.Reference(id));
            }

            var form = graph.Copy(reader.Dictionary);
            if (order.Count == 0)
                form.Remove("CO");
            else
                form["CO"] = PdfValue.OwnedArray(order);
            PdfFormValues.Form(graph, form);
            _ = new PdfFormReader(graph);
        }, "1.3");
    }

    private static void ValidateWidget(PdfFormWidgetDefinition widget, PdfFormFieldKind kind, PdfGraphEdit graph, List<string> names)
    {
        if (widget.PageIndex >= graph.Snapshot.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(widget));
        PdfAnnotation.ValidateAppearance(widget.NormalAppearance, graph.Context);
        bool button = kind == PdfFormFieldKind.CheckBox || kind == PdfFormFieldKind.RadioButton;
        if (button)
        {
            if (widget.ExportValue == null || widget.OffAppearance == null)
                throw graph.Error("Forms.ButtonAppearances", "A state button requires on/off Forms and an export name.");
            PdfAnnotation.ValidateAppearance(widget.OffAppearance, graph.Context);
            names.Add(widget.ExportValue);
        }
        else if (widget.ExportValue != null || widget.OffAppearance != null)
            throw graph.Error("Forms.AppearanceStateKind", "State appearances apply only to checkbox/radio widgets.");
    }

    private static PdfObjectId Widget(PdfGraphEdit graph, PdfResourceWriter resources, PdfObjectId parent, PdfFormWidgetDefinition widget, PdfValue value)
    {
        graph.Context.Consume(PdfResourceKind.Objects, 16);
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, 2048);
        var page = graph.Snapshot.Pages[widget.PageIndex].Source!;
        var normal = resources.Embed(widget.NormalAppearance);
        PdfValue appearance;
        string selected = value.Kind == PdfValueKind.Name ? value.NameValue : "Off";
        if (widget.ExportValue == null)
            appearance = normal;
        else
            appearance = PdfGraphicsNumbers.Dict(("Off", resources.Embed(widget.OffAppearance!)), (widget.ExportValue, normal));
        var entries = new Dictionary<string, PdfValue>
        {
            {
                "Type",
                PdfValue.Name("Annot")
            },
            {
                "Subtype",
                PdfValue.Name("Widget")
            },
            {
                "Parent",
                PdfValue.Reference(parent)
            },
            {
                "P",
                PdfValue.Reference(page.ObjectId)
            },
            {
                "Rect",
                widget.Bounds.Value
            },
            {
                "F",
                PdfValue.Integer(widget.AnnotationFlags)
            },
            {
                "AP",
                PdfGraphicsNumbers.Dict(("N", appearance))
            }
        };
        if (widget.ExportValue != null)
            entries["AS"] = PdfValue.Name(selected == widget.ExportValue ? selected : "Off");
        var id = graph.Edit.Add(PdfValue.OwnedDictionary(entries));
        var dictionary = graph.Copy(graph.Edit.Resolve(page.ObjectId));
        var old = graph.Get(PdfValue.OwnedDictionary(dictionary), "Annots");
        if (old.Kind != PdfValueKind.Null && old.Kind != PdfValueKind.Array)
            throw graph.Error("Forms.Annotations", "Page Annots must be an array.");
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, old.Kind == PdfValueKind.Array ? old.Items.Count * 32L + 64 : 64);
        var annotations = old.Kind == PdfValueKind.Array ? old.Items.ToList() : new List<PdfValue>();
        annotations.Add(PdfValue.Reference(id));
        dictionary["Annots"] = PdfValue.OwnedArray(annotations);
        graph.Edit.Set(page.ObjectId, PdfValue.OwnedDictionary(dictionary));
        return id;
    }
}
