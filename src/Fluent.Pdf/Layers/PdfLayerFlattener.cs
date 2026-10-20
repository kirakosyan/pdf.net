namespace Fluent.Pdf;
// Original state-preserving flattening profile. Unsupported state dependencies reject before commit.
internal sealed class PdfLayerFlattener
{
    private readonly PdfLayerEvaluator _reader;
    private PdfGraphEdit Graph => _reader.Graph;
    private PdfOperationContext Context => Graph.Context;

    private readonly HashSet<PdfValue> _forms = new();
    private readonly Dictionary<(PdfValue, PdfValue), PdfValue> _cache = new();
    internal PdfLayerFlattener(PdfLayerEvaluator reader)
    {
        _reader = reader;
    }

    internal void Run()
    {
        Graph.RequireUnstructuredContent();
        if (Graph.Catalog.Entries.ContainsKey("AcroForm"))
            throw _reader.Error("FormFlattenProfile", "Layer flattening in interactive forms requires an explicit field/appearance profile.");
        Graph.PruneUnreachable();
        foreach (var id in Graph.Edit.ActiveIds(Context))
            Preflight(Graph.Edit.Resolve(id));
        foreach (var page in Graph.Snapshot.Pages)
        {
            Context.Checkpoint();
            var binding = page.Source!;
            var resources = Graph.Get(binding.EffectiveAttributes, "Resources");
            var program = PdfPageContent.Read(binding, PdfUnknownOperatorPolicy.Reject, null, PdfProviderRegistry.Empty, Context);
            var rewritten = Program(program, resources, 0, out var used);
            var entries = Graph.Copy(binding.Dictionary);
            entries["Contents"] = PdfValue.Reference(Graph.Edit.Add(rewritten.ToStream(Context)));
            entries["Resources"] = Resources(resources, used);
            if (binding.Dictionary.Entries.TryGetValue("Annots", out var rawAnnotations))
            {
                var annotations = Graph.Resolve(rawAnnotations);
                if (annotations.Kind != PdfValueKind.Array)
                    throw _reader.Error("Annotations", "Page Annots must be an array.");
                Context.Consume(PdfResourceKind.ExpandedBytes, annotations.Items.Count * 32L + 64);
                var kept = new List<PdfValue>();
                foreach (var raw in annotations.Items)
                {
                    var annotation = Graph.Resolve(raw);
                    if (annotation.Kind != PdfValueKind.Dictionary)
                        throw _reader.Error("Annotation", "An annotation must be a dictionary.");
                    if (!Visible(annotation))
                        continue;
                    var dictionary = Graph.Copy(annotation);
                    dictionary.Remove("OC");
                    if (dictionary.TryGetValue("AP", out var appearance))
                        dictionary["AP"] = Appearance(appearance, resources);
                    if (raw.Kind == PdfValueKind.Reference)
                    {
                        Graph.Edit.Set(raw.ReferenceId, PdfValue.OwnedDictionary(dictionary));
                        kept.Add(raw);
                    }
                    else
                        kept.Add(PdfValue.Reference(Graph.Edit.Add(PdfValue.OwnedDictionary(dictionary))));
                }

                entries["Annots"] = PdfValue.OwnedArray(kept);
            }

            Graph.Edit.Set(binding.ObjectId, PdfValue.OwnedDictionary(entries));
        }

        var catalog = Graph.Copy(Graph.Catalog);
        catalog.Remove("OCProperties");
        Graph.SetCatalog(catalog);
        Graph.PruneUnreachable();
        foreach (var id in Graph.Edit.ActiveIds(Context))
        {
            var value = Graph.Edit.Resolve(id);
            if (value.Kind == PdfValueKind.Dictionary && (_reader.Name(value, "Type") == "OCG" || _reader.Name(value, "Type") == "OCMD"))
                throw _reader.Error("UnclassifiedReference", "A remaining nonstandard relationship still references optional-content data.");
        }
    }

    private void Preflight(PdfValue value)
    {
        Context.Checkpoint();
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        Context.Consume(PdfResourceKind.Objects, 1);
        if (value.Kind == PdfValueKind.Array)
        {
            foreach (var child in value.Items)
                if (child.Kind != PdfValueKind.Reference)
                    Preflight(child);
            return;
        }

        if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
            return;
        if (Named("Subtype", "Type3"))
            throw _reader.Error("Type3FlattenProfile", "Type 3 glyph optional content requires a separately supported glyph-state profile.");
        if (value.Entries.TryGetValue("PatternType", out var pattern) && Graph.Resolve(pattern).TryGetInteger(out long type) && type == 1)
            throw _reader.Error("PatternFlattenProfile", "Tiling-pattern optional content requires a separately supported pattern-state profile.");
        if (Named("S", "SetOCGState"))
            throw _reader.Error("StateActionPolicy", "Remove layer-state actions explicitly before flattening optional content.");
        if (value.Entries.TryGetValue("SMask", out var mask) && Graph.Resolve(mask).Kind == PdfValueKind.Dictionary)
            throw _reader.Error("SoftMaskFlattenProfile", "Soft-mask optional content requires a separately supported mask-state profile.");
        foreach (var child in value.Entries.Values)
            if (child.Kind != PdfValueKind.Reference)
                Preflight(child);
        bool Named(string key, string expected)
        {
            if (!value.Entries.TryGetValue(key, out var raw))
                return false;
            var resolved = Graph.Resolve(raw);
            return resolved.Kind == PdfValueKind.Name && resolved.NameValue == expected;
        }
    }

    private bool Visible(PdfValue dictionary) => !dictionary.Entries.TryGetValue("OC", out var membership) || _reader.Visible(membership);
    private PdfValue Appearance(PdfValue value, PdfValue resources)
    {
        value = Graph.Resolve(value);
        Context.Checkpoint();
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        if (value.Kind == PdfValueKind.Stream)
            return Visible(value) ? Form(value, resources) : PdfValue.Null;
        if (value.Kind != PdfValueKind.Dictionary)
            throw _reader.Error("Appearance", "An annotation appearance must be a stream or dictionary.");
        var result = Graph.Copy(value);
        foreach (var pair in value.Entries)
        {
            var rewritten = Appearance(pair.Value, resources);
            if (rewritten.Kind == PdfValueKind.Null)
                result.Remove(pair.Key);
            else
                result[pair.Key] = rewritten;
        }

        return PdfValue.OwnedDictionary(result);
    }

    private PdfValue Resources(PdfValue original, HashSet<string> used)
    {
        original = Graph.Resolve(original);
        if (original.Kind != PdfValueKind.Null && original.Kind != PdfValueKind.Dictionary)
            throw _reader.Error("Resources", "Content Resources must be a dictionary.");
        var result = Graph.Copy(original);
        var objects = Graph.Get(original, "XObject");
        if (objects.Kind != PdfValueKind.Null && objects.Kind != PdfValueKind.Dictionary)
            throw _reader.Error("XObjects", "XObject resources must be a dictionary.");
        var kept = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
        Context.Consume(PdfResourceKind.ExpandedBytes, used.Count * 96L + 64);
        foreach (var name in used)
        {
            Context.Checkpoint();
            if (!objects.Entries.TryGetValue(name, out var raw))
                throw _reader.Error("XObjectMissing", "A retained Do operator names a missing XObject.");
            var stream = Graph.Resolve(raw);
            if (stream.Kind != PdfValueKind.Stream)
                throw _reader.Error("XObjectType", "A retained XObject must be a stream.");
            string subtype = _reader.Name(stream, "Subtype");
            if (subtype == "Form")
                kept[name] = Form(stream, original);
            else if (subtype == "Image")
            {
                var entries = Graph.Copy(stream);
                entries.Remove("OC");
                kept[name] = PdfValue.Reference(Graph.Edit.Add(stream.WithDictionary(PdfValue.OwnedDictionary(entries))));
            }
            else
                throw _reader.Error("XObjectFlattenProfile", "The XObject subtype requires a separately supported flattening profile.");
        }

        if (kept.Count == 0)
            result.Remove("XObject");
        else
            result["XObject"] = PdfValue.OwnedDictionary(kept);
        var properties = Graph.Get(original, "Properties");
        if (properties.Kind != PdfValueKind.Null)
        {
            if (properties.Kind != PdfValueKind.Dictionary)
                throw _reader.Error("Properties", "Named Properties resources must be a dictionary.");
            var entries = Graph.Copy(properties);
            foreach (var pair in properties.Entries)
            {
                var value = Graph.Resolve(pair.Value);
                if (value.Kind == PdfValueKind.Dictionary && (_reader.Name(value, "Type") == "OCG" || _reader.Name(value, "Type") == "OCMD"))
                    entries.Remove(pair.Key);
            }

            if (entries.Count == 0)
                result.Remove("Properties");
            else
                result["Properties"] = PdfValue.OwnedDictionary(entries);
        }

        return PdfValue.OwnedDictionary(result);
    }

    private PdfValue Form(PdfValue stream, PdfValue inherited)
    {
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        Context.Checkpoint();
        var resources = stream.Entries.TryGetValue("Resources", out var own) ? Graph.Resolve(own) : inherited;
        var key = (stream, resources);
        if (_cache.TryGetValue(key, out var previous))
            return previous;
        Context.Consume(PdfResourceKind.ExpandedBytes, 128);
        if (!_forms.Add(stream))
            throw _reader.Error("FormCycle", "Recursive Form content requires a finite expansion profile.");
        try
        {
            Context.Consume(PdfResourceKind.InputBytes, stream.RawStreamLength);
            using var decoded = PdfFilters.Decode(PdfFilterDeclarations.Resolve(stream, id => Graph.Resolve(PdfValue.Reference(id)), Context), Context);
            var bytes = PdfByteStore.Read(decoded, Context);
            var program = PdfContentProgram.ParseCore(bytes, PdfContentKind.Page, PdfUnknownOperatorPolicy.Reject, null, Context, color => PdfImageColorResolver.Components(color, resources, Graph.Resolve, Context));
            var rewritten = Program(program, resources, null, out var used);
            var dictionary = Graph.Copy(stream);
            foreach (var name in new[]
            {
                "OC",
                "Filter",
                "DecodeParms",
                "Length",
                "DL"
            }

            )
                dictionary.Remove(name);
            dictionary["Resources"] = Resources(resources, used);
            var result = PdfValue.Reference(Graph.Edit.Add(rewritten.ToStream(Context).WithDictionary(PdfValue.OwnedDictionary(dictionary))));
            _cache.Add(key, result);
            return result;
        }
        finally
        {
            _forms.Remove(stream);
        }
    }

    private PdfContentProgram Program(PdfContentProgram source, PdfValue resources, int? initialMode, out HashSet<string> used)
    {
        resources = Graph.Resolve(resources);
        if (resources.Kind != PdfValueKind.Null && resources.Kind != PdfValueKind.Dictionary)
            throw _reader.Error("Resources", "Content Resources must be a dictionary.");
        var result = new List<PdfContentOperation>();
        used = new(StringComparer.Ordinal);
        var marked = new Stack<(bool Visible, bool Keep, int Semantics)>();
        var states = new Stack<int?>();
        bool visible = true, pendingAdvance = false;
        int semantics = 0;
        int? mode = initialMode;
        foreach (var operation in source.Operations)
        {
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.ExpandedBytes, 64);
            var code = operation.Operation;
            if (code == PdfOperator.BeginMarkedContent || code == PdfOperator.BeginMarkedContentProperties)
            {
                PdfValue property = PdfValue.Null;
                if (code == PdfOperator.BeginMarkedContentProperties)
                {
                    var operand = operation.Operands[1];
                    if (operand.Kind == PdfValueKind.Name)
                    {
                        var props = Graph.Get(resources, "Properties");
                        if (props.Kind != PdfValueKind.Dictionary || !props.Entries.TryGetValue(operand.NameValue, out property!))
                            throw _reader.Error("PropertyMissing", "A marked-content property resource is missing.");
                    }
                    else
                        property = operand;
                }

                var resolved = Graph.Resolve(property);
                bool optional = resolved.Kind == PdfValueKind.Dictionary && (_reader.Name(resolved, "Type") == "OCG" || _reader.Name(resolved, "Type") == "OCMD");
                if (optional && operation.Operands[0].NameValue != "OC")
                    throw _reader.Error("NonOptionalProperty", "A non-OC marked-content application refers to a group dictionary and needs explicit preservation.");
                bool keep = visible && !optional;
                marked.Push((visible, keep, semantics));
                if (optional)
                    visible &= _reader.Visible(property);
                if (resolved.Kind == PdfValueKind.Dictionary && new[]
                {
                    "ActualText",
                    "Alt",
                    "E"
                }.Any(resolved.Entries.ContainsKey))
                    semantics++;
                if (keep)
                    result.Add(operation);
                continue;
            }

            if (code == PdfOperator.EndMarkedContent)
            {
                var outer = marked.Pop();
                if (outer.Keep)
                    result.Add(operation);
                visible = outer.Visible;
                semantics = outer.Semantics;
                continue;
            }

            if (code == PdfOperator.MarkPointProperties || code == PdfOperator.MarkPoint)
            {
                bool optional = false;
                if (code == PdfOperator.MarkPointProperties && operation.Operands[0].NameValue == "OC")
                {
                    var property = operation.Operands[1];
                    if (property.Kind == PdfValueKind.Name)
                    {
                        var properties = Graph.Get(resources, "Properties");
                        if (properties.Kind != PdfValueKind.Dictionary || !properties.Entries.TryGetValue(property.NameValue, out property!))
                            throw _reader.Error("PropertyMissing", "A marked-point property resource is missing.");
                    }

                    var resolved = Graph.Resolve(property);
                    optional = resolved.Kind == PdfValueKind.Dictionary && (_reader.Name(resolved, "Type") == "OCG" || _reader.Name(resolved, "Type") == "OCMD");
                }

                if (visible && !optional)
                    result.Add(operation);
                continue;
            }

            if (code == PdfOperator.SaveState)
                states.Push(mode);
            if (code == PdfOperator.RestoreState)
                mode = states.Pop();
            if (code == PdfOperator.TextRenderingMode)
            {
                if (!operation.Operands[0].TryGetInteger(out long declared) || declared < 0 || declared > 7)
                    throw _reader.Error("TextMode", "Text rendering mode must be an integer in 0 through 7.");
                mode = (int)declared;
            }

            if (code == PdfOperator.BeginText || code == PdfOperator.EndText || code == PdfOperator.TextMatrix || code == PdfOperator.MoveText || code == PdfOperator.MoveTextLeading || code == PdfOperator.NextTextLine || code == PdfOperator.NextLineShowText || code == PdfOperator.SpacedNextLineShowText)
                pendingAdvance = false;
            bool text = code == PdfOperator.ShowText || code == PdfOperator.ShowPositionedText || code == PdfOperator.NextLineShowText || code == PdfOperator.SpacedNextLineShowText;
            bool paint = text || code == PdfOperator.DrawObject || code == PdfOperator.InlineImage || code == PdfOperator.Shading || PaintPath(code);
            bool draw = visible;
            if (code == PdfOperator.DrawObject)
            {
                var objects = Graph.Get(resources, "XObject");
                if (objects.Kind != PdfValueKind.Dictionary || !objects.Entries.TryGetValue(operation.Operands[0].NameValue, out var raw))
                    throw _reader.Error("XObjectMissing", "A Do operator names a missing XObject.");
                var stream = Graph.Resolve(raw);
                if (stream.Kind != PdfValueKind.Stream)
                    throw _reader.Error("XObjectType", "A Do operator must name a stream.");
                draw &= Visible(stream);
            }

            if (!draw && paint)
            {
                if (semantics > 0)
                    throw _reader.Error("SemanticMarkedContent", "Hidden painting inside replacement/alternate text needs an explicit semantic rewrite profile.");
                if (text)
                {
                    if (!mode.HasValue || mode.Value >= 4)
                        throw _reader.Error("TextClippingProfile", "Hidden text with inherited/clip rendering mode needs a supported text-state rewrite profile.");
                    if (code == PdfOperator.SpacedNextLineShowText)
                    {
                        result.Add(new(PdfOperator.WordSpacing, operation.Operands[0]));
                        result.Add(new(PdfOperator.CharacterSpacing, operation.Operands[1]));
                    }

                    if (code == PdfOperator.NextLineShowText || code == PdfOperator.SpacedNextLineShowText)
                        result.Add(new(PdfOperator.NextTextLine));
                    pendingAdvance = true;
                }
                else if (PaintPath(code))
                    result.Add(new(PdfOperator.EndPath));
                continue;
            }

            if (text && pendingAdvance)
                throw _reader.Error("TextAdvanceProfile", "Visible text depends on a removed hidden text advance; explicitly reset its text position before flattening.");
            if (code == PdfOperator.DrawObject)
            {
                Context.Consume(PdfResourceKind.ExpandedBytes, 48);
                used.Add(operation.Operands[0].NameValue);
            }

            result.Add(operation);
        }

        return PdfContentProgram.CreateCore(result, source.Kind, Context, true);
    }

    private static bool PaintPath(PdfOperator code) => code == PdfOperator.Stroke || code == PdfOperator.CloseStroke || code == PdfOperator.Fill || code == PdfOperator.LegacyFill || code == PdfOperator.FillEvenOdd || code == PdfOperator.FillStroke || code == PdfOperator.FillStrokeEvenOdd || code == PdfOperator.CloseFillStroke || code == PdfOperator.CloseFillStrokeEvenOdd;
}
