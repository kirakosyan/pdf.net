namespace Fluent.Pdf;
internal sealed partial class PdfPageImportSource
{
    private readonly HashSet<PdfObjectId> _fieldSeen = new();
    private PdfValue? _form;
    private void ImportFields()
    {
        if (!Catalog.Entries.TryGetValue("AcroForm", out var declared))
            return;
        _form = Resolve(declared);
        if (_form.Entries.ContainsKey("XFA"))
            throw Error("Organization.XfaProfile", "XFA-backed forms require an explicit form conversion profile before organization.");
        foreach (string key in _form.Entries.Keys)
            if (!new[]
            {
                "Fields",
                "DR",
                "DA",
                "Q",
                "NeedAppearances",
                "SigFlags",
                "CO"
            }.Contains(key))
                throw Error("Organization.FormProfile", "This form uses global settings requiring an additional explicit merge profile.");
        if (_form.Entries.TryGetValue("NeedAppearances", out var needs) && Resolve(needs).BooleanValue)
            Engine.NeedAppearances = true;
        if (_form.Entries.TryGetValue("SigFlags", out var flags))
            Engine.SignatureFlags |= checked((int)Resolve(flags).IntegerValue);
        if (_form.Entries.TryGetValue("DR", out var resources))
            foreach (var category in Resolve(resources).Entries)
            {
                var values = Engine.FormResources.TryGetValue(category.Key, out var previous) ? PdfWriterCore.CopyDictionary(previous.Entries, Context) : new Dictionary<string, PdfValue>(StringComparer.Ordinal);
                foreach (var item in Resolve(category.Value).Entries)
                {
                    Context.Consume(PdfResourceKind.ExpandedBytes, item.Key.Length * 8L + 128);
                    if (ContainsScript(Catalog, new HashSet<PdfObjectId>()))
                        throw Error("Organization.ScriptedFieldRename", "Renaming fields in a scripted document requires explicit script reconciliation.");
                    string renamed = "Source" + (_ordinal + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + item.Key;
                    _formResourceNames.Add((category.Key, item.Key), renamed);
                    values.Add(renamed, Clone(item.Value));
                }

                Engine.FormResources[category.Key] = PdfValue.OwnedDictionary(values);
            }

        if (!_form.Entries.TryGetValue("Fields", out var fields))
            throw Error("Organization.FormFields", "An AcroForm requires a root field array.");
        foreach (var field in Array(fields))
        {
            var imported = Field(field, null);
            if (imported == null)
                continue;
            var values = PdfWriterCore.CopyDictionary(Engine.Edit.Resolve(imported.ReferenceId).Entries, Context);
            if (values.TryGetValue("T", out var name))
            {
                string text = FieldText(Resolve(name));
                if (!Engine.FieldNames.Add(text))
                {
                    if (Engine.Options.FieldCollisions == PdfFieldCollisionPolicy.Reject)
                        throw Error("Organization.FieldCollision", "Merged documents contain colliding root field names.");
                    if (ContainsScript(Catalog, new HashSet<PdfObjectId>()))
                        throw Error("Organization.ScriptedFieldRename", "Renaming fields in a scripted document requires explicit script reconciliation.");
                    string renamed = "Source" + (_ordinal + 1).ToString(System.Globalization.CultureInfo.InvariantCulture) + "_" + text;
                    int suffix = 0;
                    string candidate = renamed;
                    while (!Engine.FieldNames.Add(candidate))
                        candidate = renamed + "_" + (++suffix).ToString(System.Globalization.CultureInfo.InvariantCulture);
                    values["T"] = PdfDeferredNavigation.Text(candidate, Context);
                    Context.Report(new("Organization.FieldRenamed", "The explicit collision policy renamed a colliding root partial field name.", severity: PdfDiagnosticSeverity.Information));
                }
            }

            foreach (string key in new[]
            {
                "DA",
                "Q"
            }

            )
                if (!values.ContainsKey(key) && _form.Entries.TryGetValue(key, out var global))
                    values[key] = key == "DA" ? DefaultAppearance(global) : Clone(global);
            Engine.Edit.Set(imported.ReferenceId, PdfValue.OwnedDictionary(values));
            Engine.Fields.Add(imported);
        }

        if (_form.Entries.TryGetValue("CO", out var order))
            foreach (var field in Array(order))
                if (field.Kind == PdfValueKind.Reference && Map.TryGetValue(field.ReferenceId, out var mapped) && !_prunedFields.Contains(field.ReferenceId))
                    Engine.CalculationOrder.Add(PdfValue.Reference(mapped));
    }

    private readonly HashSet<PdfObjectId> _prunedFields = new();
    private PdfValue? Field(PdfValue declared, PdfValue? parent)
    {
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        Context.Checkpoint();
        Context.Consume(PdfResourceKind.Objects, 1);
        Context.Consume(PdfResourceKind.ExpandedBytes, 192);
        if (declared.Kind != PdfValueKind.Reference || !_fieldSeen.Add(declared.ReferenceId))
            throw Error("Organization.FieldCycle", "Form fields require a finite tree of unique indirect nodes.");
        var original = Resolve(declared);
        bool widget = Name(original, "Subtype") == "Widget";
        bool excluded = _omittedWidgets.Contains(declared.ReferenceId) || widget && original.Entries.TryGetValue("P", out var page) && page.Kind == PdfValueKind.Reference && !Selected.ContainsKey(page.ReferenceId);
        if (excluded)
        {
            _prunedFields.Add(declared.ReferenceId);
            return null;
        }

        var id = Engine.Edit.Add(PdfValue.Null);
        Map[declared.ReferenceId] = id;
        var reference = PdfValue.Reference(id);
        var kids = new List<PdfValue>();
        bool hadKids = original.Entries.TryGetValue("Kids", out var children);
        if (hadKids)
            foreach (var child in Array(children!))
            {
                var value = Field(child, reference);
                if (value != null)
                    kids.Add(value);
            }

        if (hadKids && kids.Count == 0)
        {
            _prunedFields.Add(declared.ReferenceId);
            return null;
        }

        var entries = CloneEntries(original, "Parent", "Kids", "DA");
        if (parent != null)
            entries["Parent"] = parent;
        if (original.Entries.TryGetValue("DA", out var appearance))
            entries["DA"] = DefaultAppearance(appearance);
        if (hadKids)
            entries["Kids"] = PdfValue.OwnedArray(kids);
        Engine.Edit.Set(id, PdfValue.OwnedDictionary(entries));
        return reference;
    }

    private string FieldText(PdfValue value)
    {
        if (value.Kind != PdfValueKind.String || value.StringBytes.Count > 4096)
            throw Error("Organization.FieldName", "Field partial names must be bounded text strings.");
        Context.Consume(PdfResourceKind.ExpandedBytes, value.StringBytes.Count * 4L + 32);
        var bytes = value.StringBytes;
        if (bytes.Count >= 2 && bytes[0] == 254 && bytes[1] == 255)
        {
            if ((bytes.Count & 1) != 0)
                throw Error("Organization.FieldName", "A UTF-16 field partial name has a truncated code unit.");
            var chars = new char[(bytes.Count - 2) / 2];
            for (int i = 0; i < chars.Length; i++)
                chars[i] = (char)(bytes[i * 2 + 2] * 256 + bytes[i * 2 + 3]);
            return new string (chars);
        }

        if (bytes.Any(b => b >= 128))
            throw Error("Organization.FieldNameEncoding", "Non-ASCII PDFDocEncoding field names require the explicit text-string encoding profile.");
        return new string (bytes.Select(b => (char)b).ToArray());
    }

    private readonly Dictionary<(string Category, string Name), string> _formResourceNames = new();
    private PdfValue DefaultAppearance(PdfValue declared)
    {
        var value = Resolve(declared);
        if (value.Kind != PdfValueKind.String)
            throw Error("Organization.DefaultAppearance", "A field default appearance must be a PDF byte string.");
        Context.Consume(PdfResourceKind.ExpandedBytes, value.StringBytes.Count * 8L + 256);
        var builder = new PdfByteBuilder(Context);
        foreach (byte b in new byte[]
        {
            66,
            84,
            10
        }

        )
            builder.Add(b);
        foreach (byte b in value.StringBytes)
            builder.Add(b);
        foreach (byte b in new byte[]
        {
            10,
            69,
            84,
            10
        }

        )
            builder.Add(b);
        var program = PdfContentProgram.ParseCore(builder.Build(), PdfContentKind.Page, PdfUnknownOperatorPolicy.Reject, null, Context);
        var operations = new List<PdfContentOperation>();
        foreach (var operation in program.Operations)
        {
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.ExpandedBytes, operation.Operands.Count * 32L + 128);
            string? category = operation.Operation switch
            {
                PdfOperator.Font => "Font",
                PdfOperator.StrokeColorSpace or PdfOperator.FillColorSpace => "ColorSpace",
                PdfOperator.ExtendedState => "ExtGState",
                _ => null
            };
            int nameIndex = 0;
            if ((operation.Operation == PdfOperator.FillColorExtended || operation.Operation == PdfOperator.StrokeColorExtended) && operation.Operands.Count > 0 && operation.Operands[operation.Operands.Count - 1].Kind == PdfValueKind.Name)
            {
                category = "Pattern";
                nameIndex = operation.Operands.Count - 1;
            }

            if (category != null && operation.Operands.Count > nameIndex && operation.Operands[nameIndex].Kind == PdfValueKind.Name)
            {
                string name = operation.Operands[nameIndex].NameValue;
                if (_formResourceNames.TryGetValue((category, name), out var renamed))
                {
                    var operands = operation.Operands.ToArray();
                    operands[nameIndex] = PdfValue.Name(renamed);
                    operations.Add(new(operation.Operation, operands));
                    continue;
                }

                if (category != "ColorSpace" || !new[]
                {
                    "DeviceGray",
                    "DeviceRGB",
                    "DeviceCMYK",
                    "Pattern"
                }.Contains(name))
                    throw Error("Organization.AppearanceResource", "A default appearance references an absent global form resource.");
            }

            operations.Add(operation);
        }

        var stream = PdfContentProgram.CreateCore(operations, PdfContentKind.Page, Context).ToStream(Context);
        if (stream.RawStreamLength < 6 || stream.RawStreamLength > int.MaxValue)
            throw Error("Organization.DefaultAppearance", "The serialized default appearance has invalid framing.");
        int length = (int)stream.RawStreamLength - 6;
        Context.Consume(PdfResourceKind.ExpandedBytes, length * 2L + 32);
        var bytes = new byte[length];
        using var input = stream.OpenRawStream();
        for (int i = 0; i < 3; i++)
            if (input.ReadByte() < 0)
                throw Error("Organization.DefaultAppearance", "The appearance framing is truncated.");
        int offset = 0;
        while (offset < length)
        {
            Context.Checkpoint();
            int read = input.Read(bytes, offset, length - offset);
            if (read == 0)
                throw Error("Organization.DefaultAppearance", "The appearance body is truncated.");
            offset += read;
        }

        return PdfValue.String(bytes);
    }

    private bool ContainsScript(PdfValue value, HashSet<PdfObjectId> visited)
    {
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        Context.Checkpoint();
        Context.Consume(PdfResourceKind.Objects, 1);
        if (value.Kind == PdfValueKind.Reference)
        {
            Context.Consume(PdfResourceKind.ExpandedBytes, 48);
            if (!visited.Add(value.ReferenceId))
                return false;
            return ContainsScript(Graph.Resolve(value.ReferenceId), visited);
        }

        if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
        {
            if (Name(value, "S") == "JavaScript")
                return true;
            foreach (var item in value.Entries.Values)
                if (ContainsScript(item, visited))
                    return true;
        }

        if (value.Kind == PdfValueKind.Array)
            foreach (var item in value.Items)
                if (ContainsScript(item, visited))
                    return true;
        return false;
    }
}
