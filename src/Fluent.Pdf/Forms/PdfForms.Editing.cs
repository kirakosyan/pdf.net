using System.Threading;

namespace Fluent.Pdf;
/// <summary>Standard page widget traversal order.</summary>
public enum PdfFormTabOrder
{
    /// <summary>Order by rows.</summary>
    Rows,
    /// <summary>Order by columns.</summary>
    Columns,
    /// <summary>Use the page structure order; this does not create structure associations.</summary>
    Structure
}

public static partial class PdfForms
{
    /// <summary>Sets/removes a passive calculation action and synchronizes CO for every terminal representation. Action authoring uses the explicit navigation policy; nothing executes.</summary>
    public static PdfDocument SetCalculationAction(PdfFormField field, PdfAction? action, PdfActionPolicy? actionPolicy = null, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (field == null)
            throw new ArgumentNullException(nameof(field));
        return PdfGraphEdit.Run("SetFormCalculationAction", field.Source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfFormReader(graph);
            reader.Editable();
            var value = action?.Materialize(new PdfNavigationReader(graph), actionPolicy ?? new());
            var form = graph.Copy(reader.Dictionary);
            var old = graph.Get(reader.Dictionary, "CO");
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, (old.Kind == PdfValueKind.Array ? old.Items.Count : 0) * 64L + field.Representations.Count * 128L + 128);
            var order = old.Kind == PdfValueKind.Array ? old.Items.ToList() : new List<PdfValue>();
            foreach (var id in field.Representations)
            {
                graph.Context.Checkpoint();
                if (!reader.ById.ContainsKey(id))
                    throw reader.Error("FieldIdentity", "The source field is unavailable.");
                var entries = graph.Copy(graph.Edit.Resolve(id));
                var actions = graph.Copy(graph.Get(PdfValue.OwnedDictionary(entries), "AA"));
                if (value == null)
                    actions.Remove("C");
                else
                    actions["C"] = value;
                if (actions.Count == 0)
                    entries.Remove("AA");
                else
                    entries["AA"] = PdfValue.OwnedDictionary(actions);
                graph.Edit.Set(id, PdfValue.OwnedDictionary(entries));
                if (value == null)
                    order.RemoveAll(v => v.ReferenceId == id);
                else if (!order.Any(v => v.ReferenceId == id))
                    order.Add(PdfValue.Reference(id));
            }

            if (order.Count == 0)
                form.Remove("CO");
            else
                form["CO"] = PdfValue.OwnedArray(order);
            PdfFormValues.Form(graph, form);
            _ = new PdfFormReader(graph);
        }, "1.3");
    }

    /// <summary>Creates an embedded shaped Unicode Form appearance with a conservative overflow check. Bounds/font size use native appearance units; no field value or viewer editing font is inferred.</summary>
    public static PdfGraphicsResource CreateTextAppearance(PdfShapedText text, PdfRect bounds, double fontSize, double padding = 2, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        PdfGraphicsNumbers.Finite(fontSize);
        PdfGraphicsNumbers.Finite(padding);
        if (fontSize <= 0 || padding < 0 || bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        PdfGraphicsResource? result = null;
        var report = PdfFont.Run("CreateFormTextAppearance", "managed.forms", policy, cancellationToken, context => result = TextAppearance(text, bounds, fontSize, padding, context), PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    private static PdfGraphicsResource TextAppearance(PdfShapedText text, PdfRect bounds, double fontSize, double padding, PdfOperationContext context)
    {
        var ink = PdfTextGeometry.Bounds(text, fontSize, 1.2, context);
        if (ink.Width + padding * 2 > bounds.Width || ink.Height + padding * 2 > bounds.Height)
            throw PdfWriterCore.Error(context, "Forms.TextOverflow", "The shaped-text envelope exceeds the padded appearance rectangle.");
        var positioned = PdfTextDrawing.CreateCore(text, bounds.X + padding - ink.X, bounds.Top - padding - ink.Top, fontSize, 1.2, "FormText", true, context);
        context.Consume(PdfResourceKind.ExpandedBytes, positioned.Fonts.Count * 192L + 256);
        var resources = new PdfResourceSet(positioned.Fonts.Select(f => new PdfResourceBinding(f.Key, PdfGraphicsResource.Font(f.Value))).ToArray());
        return PdfGraphicsObjects.FormCore(PdfDrawing.CreateCore(positioned.Content.Operations, resources, context), bounds, null, null, context);
    }

    /// <summary>Changes only ReadOnly, Required and NoExport, preserving every kind-specific source flag, widget identity and structure association.</summary>
    public static PdfDocument SetCommonFlags(PdfFormField field, PdfFormFieldFlags flags, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (field == null)
            throw new ArgumentNullException(nameof(field));
        if (((uint)flags & ~7u) != 0)
            throw new ArgumentOutOfRangeException(nameof(flags));
        return PdfGraphEdit.Run("SetFormCommonFlags", field.Source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfFormReader(graph);
            reader.Editable();
            foreach (var id in field.Representations)
            {
                var entries = graph.Copy(graph.Edit.Resolve(id));
                entries["Ff"] = PdfValue.Integer((field.Flags & ~7u) | (uint)flags);
                graph.Edit.Set(id, PdfValue.OwnedDictionary(entries));
            }

            _ = new PdfFormReader(graph);
        }, "1.7");
    }

    /// <summary>Replaces one widget's appearance, same-page bounds and annotation flags while preserving field identity, actions, tab position and structure links. Cross-page movement requires a structure relocation profile.</summary>
    public static PdfDocument EditWidget(PdfFormField field, PdfObjectId widgetId, PdfFormWidgetDefinition definition, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (field == null)
            throw new ArgumentNullException(nameof(field));
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        return PdfGraphEdit.Run("EditFormWidget", field.Source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfFormReader(graph);
            reader.Editable();
            var current = reader.ById[field.Representations[0]];
            if (current.Kind == PdfFormFieldKind.Unknown)
                throw reader.Error("UnsupportedType", "Appearance editing requires a supported field kind.");
            var widget = current.Widgets.SingleOrDefault(w => w.Id == widgetId);
            if (widget == null)
                throw reader.Error("WidgetIdentity", "The widget must belong to the selected logical field.");
            if (widget.PageIndex != definition.PageIndex)
                throw reader.Error("WidgetPageProfile", "Cross-page or unattached widget relocation requires an explicit ownership/structure profile.");
            ValidateWidget(definition, current.Kind, graph, new());
            var oldNormal = graph.Get(graph.Get(widget.Definition, "AP"), "N");
            if (definition.ExportValue != null && (oldNormal.Kind != PdfValueKind.Dictionary || oldNormal.Entries.Count != 2 || !oldNormal.Entries.ContainsKey("Off") || !oldNormal.Entries.ContainsKey(definition.ExportValue)))
                throw reader.Error("WidgetExportProfile", "Appearance editing preserves button export names; changing them requires a value synchronization profile.");
            var writer = new PdfResourceWriter(graph.Edit, graph.Context, new());
            var normal = writer.Embed(definition.NormalAppearance);
            var entries = graph.Copy(widget.Definition);
            var appearances = graph.Copy(graph.Get(widget.Definition, "AP"));
            appearances["N"] = definition.ExportValue == null ? normal : PdfGraphicsNumbers.Dict(("Off", writer.Embed(definition.OffAppearance!)), (definition.ExportValue, normal));
            entries["AP"] = PdfValue.OwnedDictionary(appearances);
            entries["Rect"] = definition.Bounds.Value;
            entries["F"] = PdfValue.Integer(definition.AnnotationFlags);
            if (definition.ExportValue != null)
                entries["AS"] = PdfValue.Name(current.Value.Kind == PdfValueKind.Name && current.Value.NameValue == definition.ExportValue ? definition.ExportValue : "Off");
            graph.Edit.Set(widgetId, PdfValue.OwnedDictionary(entries));
            _ = new PdfFormReader(graph);
        }, "1.7");
    }

    /// <summary>Sets the page's standard Tabs key without reordering annotations or rewriting existing structure relationships.</summary>
    public static PdfDocument SetTabOrder(PdfDocumentSnapshot source, int pageIndex, PdfFormTabOrder order, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (!Enum.IsDefined(typeof(PdfFormTabOrder), order))
            throw new ArgumentOutOfRangeException(nameof(order));
        return PdfGraphEdit.Run("SetFormTabOrder", source, options, policy, cancellationToken, graph =>
        {
            if (pageIndex < 0 || pageIndex >= graph.Snapshot.Pages.Count)
                throw new ArgumentOutOfRangeException(nameof(pageIndex));
            var reader = new PdfFormReader(graph);
            reader.Editable();
            var id = graph.Snapshot.Pages[pageIndex].Source!.ObjectId;
            var entries = graph.Copy(graph.Edit.Resolve(id));
            entries["Tabs"] = PdfValue.Name(order == PdfFormTabOrder.Rows ? "R" : order == PdfFormTabOrder.Columns ? "C" : "S");
            graph.Edit.Set(id, PdfValue.OwnedDictionary(entries));
        }, "1.5");
    }

    private static void CheckNameReferences(PdfGraphEdit graph, bool retain)
    {
        bool found = false;
        foreach (var id in graph.Edit.ActiveIds(graph.Context))
            Visit(graph.Edit.Resolve(id));
        if (!found)
            return;
        if (!retain)
            throw graph.Error("Forms.OpaqueNameReferences", "Renaming requires an explicit decision about retained scripts or form actions that may refer to field names.");
        graph.Context.Report(new("Forms.OpaqueNameReferencesRetained", "Caller retained opaque field-name references without rewriting or executing them.", severity: PdfDiagnosticSeverity.Warning));
        void Visit(PdfValue value)
        {
            graph.Context.Checkpoint();
            using var depth = graph.Context.EnterScope(PdfResourceKind.Depth);
            graph.Context.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Array)
            {
                foreach (var child in value.Items)
                    Visit(child);
            }
            else if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
            {
                var type = graph.Get(value, "S");
                if (value.Entries.ContainsKey("JS") || type.Kind == PdfValueKind.Name && (type.NameValue == "SubmitForm" || type.NameValue == "ResetForm" || type.NameValue == "ImportData"))
                    found = true;
                foreach (var child in value.Entries.Values)
                    if (child.Kind != PdfValueKind.Reference)
                        Visit(child);
            }
        }
    }
}
