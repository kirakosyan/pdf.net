namespace Fluent.Pdf;
internal static class PdfFormValues
{
    internal static uint Flags(PdfFormFieldDefinition definition, PdfGraphEdit graph)
    {
        uint flags = (uint)definition.Flags, common = 7, allowed;
        switch (definition.Kind)
        {
            case PdfFormFieldKind.Text:
                allowed = common | 4096 | 8192 | 1048576 | 4194304 | 8388608 | 16777216;
                break;
            case PdfFormFieldKind.RadioButton:
                allowed = common | 16384 | 33554432;
                flags |= 32768;
                break;
            case PdfFormFieldKind.PushButton:
                allowed = common;
                flags |= 65536;
                break;
            case PdfFormFieldKind.ComboBox:
                allowed = common | 262144 | 524288 | 4194304 | 67108864;
                flags |= 131072;
                break;
            case PdfFormFieldKind.ListBox:
                allowed = common | 524288 | 2097152 | 67108864;
                break;
            default:
                allowed = common;
                break;
        }

        if (((uint)definition.Flags & ~allowed) != 0)
            throw graph.Error("Forms.FlagsForKind", "The requested flags are unsupported for this field kind.");
        if ((flags & 16777216) != 0 && (!definition.MaximumLength.HasValue || (flags & (4096 | 8192 | 1048576)) != 0))
            throw graph.Error("Forms.CombFlags", "Comb requires MaxLen and excludes multiline, password and file-selection behavior.");
        if ((flags & 1048576) != 0 && (flags & (4096 | 8192)) != 0)
            throw graph.Error("Forms.FileSelectFlags", "File-selection text cannot be multiline or password text.");
        if (definition.MaximumLength.HasValue && definition.Kind != PdfFormFieldKind.Text)
            throw graph.Error("Forms.MaxLenKind", "MaxLen applies only to text fields.");
        if (definition.Choices.Count != 0 && definition.Kind != PdfFormFieldKind.ComboBox && definition.Kind != PdfFormFieldKind.ListBox)
            throw graph.Error("Forms.ChoiceKind", "Options apply only to choice fields.");
        return flags;
    }

    internal static void Value(PdfFormFieldKind kind, uint flags, PdfValue value, IReadOnlyList<PdfFormChoice> choices, int? maximum, IReadOnlyList<string> onNames, PdfGraphEdit graph)
    {
        if (value.Kind == PdfValueKind.Null)
            return;
        switch (kind)
        {
            case PdfFormFieldKind.Text:
                string text = PdfTextStrings.Decode(value, graph.Context);
                if (maximum.HasValue)
                {
                    int scalars = 0;
                    for (int i = 0; i < text.Length; i++)
                    {
                        graph.Context.Checkpoint();
                        if (char.IsHighSurrogate(text[i]))
                            i++;
                        scalars++;
                    }

                    if (scalars > maximum.Value)
                        throw graph.Error("Forms.MaxLen", "The text value exceeds the declared Unicode-scalar MaxLen.");
                }

                break;
            case PdfFormFieldKind.CheckBox:
            case PdfFormFieldKind.RadioButton:
                if (value.Kind != PdfValueKind.Name || value.NameValue != "Off" && !onNames.Contains(value.NameValue))
                    throw graph.Error("Forms.ButtonValue", "A button value must name Off or an authored on state.");
                break;
            case PdfFormFieldKind.ComboBox:
            case PdfFormFieldKind.ListBox:
                if (value.Kind == PdfValueKind.Array && (flags & 2097152) == 0)
                    throw graph.Error("Forms.MultiSelect", "An array value requires a multi-select list.");
                var values = value.Kind == PdfValueKind.Array ? value.Items : new[]
                {
                    value
                };
                var seen = new HashSet<string>(StringComparer.Ordinal);
                foreach (var item in values)
                {
                    var choice = PdfTextStrings.Decode(item, graph.Context);
                    graph.Context.Consume(PdfResourceKind.ExpandedBytes, 64);
                    if (!seen.Add(choice))
                        throw graph.Error("Forms.DuplicateSelection", "A choice value cannot select the same export twice.");
                    if ((kind != PdfFormFieldKind.ComboBox || (flags & 262144) == 0) && !choices.Any(x => x.ExportValue == choice))
                        throw graph.Error("Forms.ChoiceValue", "A selected export is absent from Opt.");
                }

                break;
            case PdfFormFieldKind.Signature:
                throw graph.Error("Forms.SignatureValue", "Author an unsigned signature field here; signature values require the signing operation.");
            case PdfFormFieldKind.PushButton:
                throw graph.Error("Forms.PushButtonValue", "Push buttons have no persistent value.");
            default:
                throw graph.Error("Forms.UnsupportedType", "The field type requires a supported value profile.");
        }
    }

    internal static string Type(PdfFormFieldKind kind) => kind == PdfFormFieldKind.Text ? "Tx" : kind == PdfFormFieldKind.Signature ? "Sig" : kind == PdfFormFieldKind.ComboBox || kind == PdfFormFieldKind.ListBox ? "Ch" : "Btn";
    internal static List<PdfFormChoice> Choices(PdfFormFieldDefinition definition, PdfGraphEdit graph)
    {
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, definition.Choices.Count * 96L + 64);
        var choices = new List<PdfFormChoice>();
        var exports = new HashSet<string>(StringComparer.Ordinal);
        foreach (var choice in definition.Choices)
        {
            graph.Context.Checkpoint();
            PdfTextStrings.Encode(choice.ExportValue, graph.Context);
            PdfTextStrings.Encode(choice.DisplayValue, graph.Context);
            if (!exports.Add(choice.ExportValue))
                throw graph.Error("Forms.DuplicateOption", "Authored choice exports must be distinct.");
            choices.Add(choice);
        }

        if ((definition.Flags & PdfFormFieldFlags.Sort) != 0)
            choices.Sort((a, b) =>
            {
                graph.Context.Checkpoint();
                return string.CompareOrdinal(a.DisplayValue, b.DisplayValue);
            });
        return choices;
    }

    internal static PdfValue Options(IReadOnlyList<PdfFormChoice> choices, PdfGraphEdit graph)
    {
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, choices.Count * 96L + 64);
        var values = new List<PdfValue>();
        foreach (var choice in choices)
        {
            var export = PdfTextStrings.Encode(choice.ExportValue, graph.Context);
            values.Add(choice.ExportValue == choice.DisplayValue ? export : PdfValue.Array(export, PdfTextStrings.Encode(choice.DisplayValue, graph.Context)));
        }

        return PdfValue.OwnedArray(values);
    }

    internal static void Selection(Dictionary<string, PdfValue> field, PdfValue value, IReadOnlyList<PdfFormChoice> choices, PdfGraphEdit graph)
    {
        field.Remove("I");
        if (value.Kind == PdfValueKind.Null)
            return;
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, choices.Count * 64L + 128);
        var selected = new SortedDictionary<int, PdfValue>();
        var values = value.Kind == PdfValueKind.Array ? value.Items : new[]
        {
            value
        };
        foreach (var item in values)
        {
            string export = PdfTextStrings.Decode(item, graph.Context);
            for (int i = 0; i < choices.Count; i++)
            {
                graph.Context.Checkpoint();
                if (choices[i].ExportValue == export)
                {
                    selected.Add(i, item);
                    break;
                }
            }
        }

        if (selected.Count != values.Count)
            return; // Editable combo custom text has no option index.
        field["I"] = PdfValue.Array(selected.Keys.Select(i => PdfValue.Integer(i)).ToArray());
        if (value.Kind == PdfValueKind.Array)
            field["V"] = PdfValue.Array(selected.Values.ToArray());
    }

    internal static void Form(PdfGraphEdit graph, Dictionary<string, PdfValue> form)
    {
        var catalog = graph.Copy(graph.Catalog);
        catalog["AcroForm"] = PdfValue.OwnedDictionary(form);
        graph.SetCatalog(catalog);
    }
}
