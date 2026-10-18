using System.Text;
using System.Threading;

namespace Fluent.Pdf;
public static partial class PdfForms
{
    /// <summary>Extracts current data-bearing standard fields. Push/signature fields are excluded; NoExport is honored unless explicitly included.</summary>
    public static PdfFormData ReadValues(PdfDocumentSnapshot source, bool includeNoExport = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        PdfFormData? result = null;
        var report = PdfFont.Run("ReadFormValues", "managed.forms", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            var reader = new PdfFormReader(graph);
            result = PdfFormData.Capture(Values(), context);
            IEnumerable<PdfFormDataField> Values()
            {
                foreach (var field in reader.Fields)
                {
                    context.Checkpoint();
                    if (field.Kind == PdfFormFieldKind.Signature || field.Kind == PdfFormFieldKind.PushButton || !includeNoExport && (field.Flags & 4) != 0)
                        continue;
                    if (field.Kind == PdfFormFieldKind.Unknown)
                        throw reader.Error("UnsupportedType", "An unknown field type cannot be exported as certain field-value data.");
                    if (string.IsNullOrWhiteSpace(field.Name) || field.Name.Length > 8192 || field.Name[0] == '.' || field.Name[field.Name.Length - 1] == '.' || field.Name.Contains(".."))
                        throw reader.Error("DataNameProfile", "Value interchange requires a bounded, nonempty qualified field name.");
                    var value = field.Value;
                    if (value.Kind == PdfValueKind.Array)
                    {
                        context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 32L + 64);
                        value = PdfValue.Array(value.Items.Select(graph.Resolve).ToArray());
                    }

                    yield return new(field.Name, value);
                }
            }
        }, PdfCapability.ReadDocument);
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Applies data using target field types and optional explicit text generation settings. Unknown names and unavailable appearances reject atomically.</summary>
    public static PdfDocument FillData(PdfDocumentSnapshot source, PdfFormData data, PdfFormTextStyle? textStyle = null, bool retainSourceRegenerationRequest = false, PdfWriteMode writeMode = PdfWriteMode.Full, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        return Fill(source, data.Fields.Select(f => new PdfFormUpdate(f.Name, f.Value, textStyle)), retainSourceRegenerationRequest, writeMode, options, policy, cancellationToken);
    }

    /// <summary>Stages distinct field value/reset updates and synchronizes all owned widgets under one context. Calculations/actions never run; explicit private Forms cover richer appearance profiles.</summary>
    public static PdfDocument Fill(PdfDocumentSnapshot source, IEnumerable<PdfFormUpdate> updates, bool retainSourceRegenerationRequest = false, PdfWriteMode writeMode = PdfWriteMode.Full, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (updates == null)
            throw new ArgumentNullException(nameof(updates));
        return PdfGraphEdit.Run("FillFormFields", source, options, policy, cancellationToken, graph =>
        {
            var reader = new PdfFormReader(graph);
            reader.Editable();
            Regeneration(reader, retainSourceRegenerationRequest);
            SignaturePermissions(graph, options);
            var names = new Dictionary<string, PdfFormField>(StringComparer.Ordinal);
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, reader.Fields.Count * 128L + 64);
            foreach (var field in reader.Fields)
                names.Add(field.Name, field);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var writer = new PdfResourceWriter(graph.Edit, graph.Context, new());
            using var enumerator = updates.GetEnumerator();
            while (true)
            {
                graph.Context.Checkpoint();
                graph.Context.Consume(PdfResourceKind.Objects, 1);
                if (!enumerator.MoveNext())
                    break;
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, 256);
                var update = enumerator.Current ?? throw new ArgumentException("Updates cannot be null.", nameof(updates));
                if (!seen.Add(update.Name))
                    throw reader.Error("DuplicateUpdate", "A fill batch must update each exact name at most once.");
                if (!names.TryGetValue(update.Name, out var field))
                    throw reader.Error("UnknownField", "The requested field name is absent from the source.");
                if (field.Kind == PdfFormFieldKind.Signature || field.Kind == PdfFormFieldKind.PushButton || field.Kind == PdfFormFieldKind.Unknown)
                    throw reader.Error("FillKind", "This field kind requires a dedicated value/interaction operation.");
                if (field.Representations.Count > 1 && (field.Kind == PdfFormFieldKind.ComboBox || field.Kind == PdfFormFieldKind.ListBox))
                    throw reader.Error("LegacyChoiceProfile", "Multiple choice representations require per-representation option/index synchronization.");
                var choices = Choices(field, graph);
                var states = States(field, graph);
                int? maximum = Maximum(field, graph);
                var value = Normalize(update.Value, field, graph);
                var bindings = Bindings(reader, field);
                PdfObjectId owner = reader.NameOwners[field.Name];
                var above = reader.Parents[owner];
                if (value.Kind == PdfValueKind.Null && HasInheritedValue(above, "V", graph))
                    throw reader.Error("InheritedClearProfile", "Clearing a value inherited from a differently named ancestor requires an explicit inheritance materialization profile.");
                PdfFormValues.Value(field.Kind, field.Flags, value, choices, maximum, states, graph);
                PdfValue? reset = update.DefaultValue == null ? null : Normalize(update.DefaultValue, field, graph);
                if (reset?.Kind == PdfValueKind.Null && HasInheritedValue(above, "DV", graph))
                    throw reader.Error("InheritedClearProfile", "Clearing an inherited reset value requires an explicit inheritance materialization profile.");
                if (reset != null)
                    PdfFormValues.Value(field.Kind, field.Flags, reset, choices, maximum, states, graph);
                bool button = field.Kind == PdfFormFieldKind.CheckBox || field.Kind == PdfFormFieldKind.RadioButton;
                if (button && update.Appearances.Count != 0)
                    throw reader.Error("ButtonAppearanceProfile", "Fill synchronizes authored button states; use widget editing/decoration to replace button appearances.");
                foreach (var key in update.Appearances.Keys)
                    if (!field.Widgets.Any(w => w.Id == key))
                        throw reader.Error("AppearanceOwnership", "Each supplied appearance must belong to a widget of the updated field.");
                if (field.Inherited.Entries.ContainsKey("RV") || field.Kind == PdfFormFieldKind.Text && (field.Flags & 33554432) != 0 || field.Representations.Any(id => graph.Get(graph.Edit.Resolve(id), "RV").Kind != PdfValueKind.Null))
                    throw reader.Error("RichTextFillProfile", "Rich text requires an explicit rich/plain synchronization profile before filling.");
                foreach (var id in bindings)
                {
                    var entries = graph.Copy(graph.Edit.Resolve(id));
                    entries["V"] = value; // Same-name owners are updated together; clears above that owner reject.
                    if (reset != null)
                        entries["DV"] = reset;
                    if (reader.ById.ContainsKey(id) && (field.Kind == PdfFormFieldKind.ComboBox || field.Kind == PdfFormFieldKind.ListBox))
                        PdfFormValues.Selection(entries, value, choices, field.Flags, graph);
                    graph.Edit.Set(id, PdfValue.OwnedDictionary(entries));
                }

                foreach (var widget in field.Widgets)
                {
                    graph.Context.Checkpoint();
                    var entries = graph.Copy(graph.Edit.Resolve(widget.Id));
                    if (button)
                    {
                        var normal = graph.Get(graph.Get(PdfValue.OwnedDictionary(entries), "AP"), "N");
                        string selected = value.Kind == PdfValueKind.Name ? value.NameValue : "Off";
                        string state = normal.Entries.ContainsKey(selected) ? selected : "Off";
                        RequireForm(graph.Resolve(normal.Entries[state]), graph);
                        entries["AS"] = PdfValue.Name(state);
                    }
                    else
                    {
                        PdfGraphicsResource appearance;
                        if (update.Appearances.TryGetValue(widget.Id, out var supplied))
                            appearance = supplied;
                        else
                        {
                            if (update.TextStyle == null)
                                throw reader.Error("AppearanceRequired", "Every variable-text widget requires an explicit Form or shared-context text generation style.");
                            if (field.Representations.Count > 1)
                                throw reader.Error("LegacyAppearanceProfile", "Legacy representations require explicit per-widget Forms to preserve their individual visual settings.");
                            if (field.Kind == PdfFormFieldKind.ListBox || (field.Flags & 16777216) != 0 || field.Inherited.Entries.TryGetValue("Q", out var alignment) && (!alignment.TryGetInteger(out long q) || q != 0))
                                throw reader.Error("TextAppearanceProfile", "List selection, comb and non-left alignment need explicit private appearances in this source profile.");
                            string display = value.Kind == PdfValueKind.Null ? "" : PdfTextStrings.Decode(value, graph.Context);
                            if (field.Kind == PdfFormFieldKind.ComboBox)
                                display = choices.FirstOrDefault(c => c.ExportValue == display)?.DisplayValue ?? display;
                            var style = update.TextStyle;
                            if ((field.Flags & 8192) != 0)
                            {
                                if (style.PasswordMask == null)
                                    throw reader.Error("PasswordMaskRequired", "Password appearance generation requires an explicit mask scalar.");
                                graph.Context.Consume(PdfResourceKind.ExpandedBytes, display.Length * (long)style.PasswordMask.Length * 4 + 64);
                                var masked = new StringBuilder();
                                for (int i = 0; i < display.Length; i++)
                                {
                                    graph.Context.Checkpoint();
                                    if (char.IsHighSurrogate(display[i]))
                                        i++;
                                    masked.Append(style.PasswordMask);
                                }

                                display = masked.ToString();
                            }

                            var shaper = style.Providers.GetRequired<IPdfTextShaper>(PdfCapability.ShapeText);
                            var shaped = PdfText.ShapeCore(display, style.Fonts, shaper, style.TextOptions, graph.Context);
                            appearance = TextAppearance(shaped, new(0, 0, widget.Bounds.Width, widget.Bounds.Height), style.FontSize, style.Padding, graph.Context);
                        }

                        PdfAnnotation.ValidateAppearance(appearance, graph.Context);
                        var ap = graph.Copy(graph.Get(PdfValue.OwnedDictionary(entries), "AP"));
                        if (ap.ContainsKey("R") || ap.ContainsKey("D"))
                            throw reader.Error("StateAppearanceProfile", "Rollover/down appearances require a value-synchronized state appearance profile.");
                        ap["N"] = writer.Embed(appearance);
                        entries["AP"] = PdfValue.OwnedDictionary(ap);
                    }

                    graph.Edit.Set(widget.Id, PdfValue.OwnedDictionary(entries));
                }
            }

            _ = new PdfFormReader(graph);
        }, "1.7", writeMode);
    }

    private static PdfValue Normalize(PdfValue value, PdfFormField field, PdfGraphEdit graph)
    {
        if ((field.Kind == PdfFormFieldKind.CheckBox || field.Kind == PdfFormFieldKind.RadioButton) && value.Kind == PdfValueKind.String)
        {
            string text = PdfTextStrings.Decode(value, graph.Context);
            try
            {
                return PdfValue.Name(text);
            }
            catch (ArgumentException)
            {
                throw graph.Error("Forms.ButtonExport", "The imported button value must match a byte-valued export name exactly.");
            }
        }

        return value;
    }

    private static int? Maximum(PdfFormField field, PdfGraphEdit graph)
    {
        if (!field.Inherited.Entries.TryGetValue("MaxLen", out var raw))
            return null;
        if (!raw.TryGetInteger(out long length) || length <= 0 || length > int.MaxValue)
            throw graph.Error("Forms.MaxLen", "Source MaxLen must be a supported positive integer.");
        return (int)length;
    }

    private static List<PdfFormChoice> Choices(PdfFormField field, PdfGraphEdit graph)
    {
        var choices = new List<PdfFormChoice>();
        if (field.Kind != PdfFormFieldKind.ComboBox && field.Kind != PdfFormFieldKind.ListBox)
            return choices;
        var raw = graph.Get(field.Inherited, "Opt");
        if (raw.Kind == PdfValueKind.Null)
            return choices;
        if (raw.Kind != PdfValueKind.Array)
            throw graph.Error("Forms.Options", "Choice Opt must be an array.");
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, raw.Items.Count * 192L + 64);
        var exports = new HashSet<string>(StringComparer.Ordinal);
        foreach (var item in raw.Items)
        {
            graph.Context.Checkpoint();
            var option = graph.Resolve(item);
            string export, display;
            if (option.Kind == PdfValueKind.Array && option.Items.Count == 2)
            {
                export = PdfTextStrings.Decode(graph.Resolve(option.Items[0]), graph.Context);
                display = PdfTextStrings.Decode(graph.Resolve(option.Items[1]), graph.Context);
            }
            else
            {
                export = PdfTextStrings.Decode(option, graph.Context);
                display = export;
            }

            if (!exports.Add(export))
                throw graph.Error("Forms.AmbiguousOption", "Duplicate source choice exports require an explicit index-selection profile.");
            choices.Add(new(export, display));
        }

        return choices;
    }

    private static List<string> States(PdfFormField field, PdfGraphEdit graph)
    {
        var states = new List<string>();
        if (field.Kind != PdfFormFieldKind.CheckBox && field.Kind != PdfFormFieldKind.RadioButton)
            return states;
        foreach (var widget in field.Widgets)
        {
            var normal = graph.Get(graph.Get(widget.Definition, "AP"), "N");
            if (normal.Kind != PdfValueKind.Dictionary || !normal.Entries.ContainsKey("Off"))
                throw graph.Error("Forms.ButtonAppearances", "Button filling requires an explicit Off/on appearance dictionary for every widget.");
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, normal.Entries.Count * 64L + 64);
            foreach (var pair in normal.Entries)
            {
                RequireForm(graph.Resolve(pair.Value), graph);
                if (pair.Key != "Off")
                    states.Add(pair.Key);
            }
        }

        return states;
    }

    private static void RequireForm(PdfValue value, PdfGraphEdit graph)
    {
        if (value.Kind != PdfValueKind.Stream || graph.Get(value, "Subtype").Kind != PdfValueKind.Name || graph.Get(value, "Subtype").NameValue != "Form")
            throw graph.Error("Forms.AppearanceRequired", "A valid explicit Form appearance is required.");
    }

    private static HashSet<PdfObjectId> Bindings(PdfFormReader reader, PdfFormField field)
    {
        var graph = reader.Graph;
        if (!reader.NameOwners.TryGetValue(field.Name, out var owner))
            throw reader.Error("ValueOwner", "A value update requires a named field owner.");
        bool container = !field.Representations.Contains(owner);
        if (container && reader.Fields.Any(f => f.Name.StartsWith(field.Name + ".", StringComparison.Ordinal)))
            throw reader.Error("SharedValueOwnerProfile", "A same-name representation owner also parents other logical fields; explicit inheritance materialization is required.");
        var bindings = new HashSet<PdfObjectId>();
        foreach (var terminal in field.Representations)
        {
            var id = terminal;
            var seen = new HashSet<PdfObjectId>();
            while (true)
            {
                graph.Context.Checkpoint();
                graph.Context.Consume(PdfResourceKind.Objects, 1);
                graph.Context.Consume(PdfResourceKind.ExpandedBytes, 96);
                if (!seen.Add(id) || seen.Count > graph.Context.Policy.Limits.MaximumDepth)
                    throw reader.Error("ValueOwner", "Field value-owner paths must be bounded and finite.");
                bindings.Add(id);
                if (id == owner)
                    break;
                if (!reader.Parents.TryGetValue(id, out var parent) || !parent.HasValue)
                    throw reader.Error("ValueOwner", "The terminal must descend from its named owner.");
                id = parent.Value;
            }
        }

        return bindings;
    }

    private static bool HasInheritedValue(PdfObjectId? parent, string key, PdfGraphEdit graph)
    {
        var seen = new HashSet<PdfObjectId>();
        while (parent.HasValue)
        {
            graph.Context.Checkpoint();
            graph.Context.Consume(PdfResourceKind.Objects, 1);
            graph.Context.Consume(PdfResourceKind.ExpandedBytes, 48);
            if (!seen.Add(parent.Value) || seen.Count > graph.Context.Policy.Limits.MaximumDepth)
                throw graph.Error("Forms.ValueOwner", "Inherited value paths must be bounded and finite.");
            var dictionary = graph.Resolve(PdfValue.Reference(parent.Value));
            if (graph.Get(dictionary, key).Kind != PdfValueKind.Null)
                return true;
            parent = dictionary.Entries.TryGetValue("Parent", out var next) && next.Kind == PdfValueKind.Reference ? next.ReferenceId : (PdfObjectId? )null;
        }

        return false;
    }

    private static void Regeneration(PdfFormReader reader, bool retain)
    {
        var need = reader.Graph.Get(reader.Dictionary, "NeedAppearances");
        if (need.Kind != PdfValueKind.Boolean || !need.BooleanValue)
            return;
        if (!retain)
            throw reader.Error("RegenerationProfile", "The source requests viewer regeneration; supply an explicit retention decision or a complete regeneration profile.");
        reader.Graph.Context.Report(new("Forms.RegenerationRequestRetained", "Caller retained the source viewer-regeneration request; viewers may replace private appearances.", severity: PdfDiagnosticSeverity.Warning));
    }

    private static void SignaturePermissions(PdfGraphEdit graph, PdfOrganizationOptions? options)
    {
        bool allow = options?.AllowSignatureInvalidation == true, found = false;
        var permissions = graph.Get(graph.Catalog, "Perms");
        if (permissions.Kind != PdfValueKind.Null)
        {
            if (permissions.Kind != PdfValueKind.Dictionary)
                throw graph.Error("Forms.SignaturePermissionsProfile", "A permissions dictionary requires a supported signing/transform profile.");
            foreach (var value in permissions.Entries.Values)
            {
                graph.Context.Checkpoint();
                if (graph.Resolve(value).Kind != PdfValueKind.Null)
                    found = true;
            }
        }

        foreach (var id in graph.Edit.ActiveIds(graph.Context))
            Check(graph.Edit.Resolve(id));
        if (found)
        {
            if (!allow)
                throw graph.Error("Forms.SignaturePermissionsProfile", "Certification/transform permissions require explicit verification before form changes.");
            graph.Context.Report(new("Forms.SignaturePermissionsNotValidated", "Caller allowed signature invalidation; certification/transform permissions were not verified.", severity: PdfDiagnosticSeverity.Information));
        }

        void Check(PdfValue value)
        {
            graph.Context.Checkpoint();
            using var depth = graph.Context.EnterScope(PdfResourceKind.Depth);
            graph.Context.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Array)
                foreach (var item in value.Items)
                    if (item.Kind != PdfValueKind.Reference)
                        Check(item);
            if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
            {
                var transform = graph.Get(value, "TransformMethod");
                if (transform.Kind == PdfValueKind.Name && (transform.NameValue == "DocMDP" || transform.NameValue == "FieldMDP"))
                    found = true;
                foreach (var child in value.Entries.Values)
                    if (child.Kind != PdfValueKind.Reference)
                        Check(child);
            }
        }
    }
}
