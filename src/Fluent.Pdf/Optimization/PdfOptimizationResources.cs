using System.IO;

namespace Fluent.Pdf;
// Global name unions deliberately retain false positives across scopes. An unused
// binding is removed only when no inspected standard content can name it.
internal static class PdfOptimizationResources
{
    private static readonly HashSet<string> Categories = new(new[] { "Font", "XObject", "ExtGState", "Pattern", "Shading", "Properties" }, StringComparer.Ordinal);
    internal static int Prune(PdfGraphEdit graph, PdfProviderRegistry providers)
    {
        var c = graph.Context;
        var used = new HashSet<(string Category, string Name)>();
        var streams = new HashSet<PdfValue>();
        var owners = new HashSet<PdfValue>();
        var ids = new List<PdfObjectId>();
        foreach (var id in graph.Edit.ActiveIds(c))
        {
            c.Consume(PdfResourceKind.ExpandedBytes, 32);
            ids.Add(id);
        }

        foreach (var page in graph.Snapshot.Pages)
        {
            c.Checkpoint();
            Collect(PdfPageContent.Read(page.Source, PdfUnknownOperatorPolicy.Reject, null, providers, c));
        }

        foreach (var id in ids)
            Walk(graph.Edit.Resolve(id));
        int removed = 0;
        foreach (var id in ids)
            graph.Edit.Set(id, Rewrite(graph.Edit.Resolve(id)));
        if (removed > 0)
            c.Report(new("Optimization.UnusedResourceBindingsRemoved", "Resource bindings absent from the conservative complete standard-content name inventory were removed; implicit color spaces and unknown categories remain retained.", severity: PdfDiagnosticSeverity.Information));
        return removed;
        string Name(PdfValue value, string key)
        {
            var v = graph.Get(value, key);
            return v.Kind == PdfValueKind.Name ? v.NameValue : "";
        }

        void Add(string category, string name)
        {
            c.Consume(PdfResourceKind.ExpandedBytes, name.Length * 2L + 96);
            used.Add((category, name));
        }

        void Collect(PdfContentProgram program)
        {
            foreach (var op in program.Operations)
            {
                c.Checkpoint();
                string? category = op.Operation switch
                {
                    PdfOperator.Font => "Font",
                    PdfOperator.DrawObject => "XObject",
                    PdfOperator.ExtendedState => "ExtGState",
                    PdfOperator.Shading => "Shading",
                    PdfOperator.MarkPointProperties or PdfOperator.BeginMarkedContentProperties => "Properties",
                    PdfOperator.FillColorExtended or PdfOperator.StrokeColorExtended => "Pattern",
                    _ => null
                };
                if (category == null)
                    continue;
                foreach (var operand in op.Operands)
                    if (operand.Kind == PdfValueKind.Name)
                        Add(category, operand.NameValue);
            }
        }

        void Read(PdfValue stream, PdfContentKind kind, PdfValue resources)
        {
            stream = graph.Resolve(stream);
            if (stream.Kind != PdfValueKind.Stream)
                throw PdfOptimizationEngine.Error(c, "ResourceUsage", "Complete content-stream ownership is required before pruning resource bindings.");
            c.Consume(PdfResourceKind.ExpandedBytes, 96);
            if (!streams.Add(stream))
                return;
            c.Consume(PdfResourceKind.InputBytes, stream.RawStreamLength);
            var resolved = PdfFilterDeclarations.Resolve(stream, reference => graph.Edit.Resolve(reference), c);
            using var decoded = PdfFilters.Decode(resolved, c, providers);
            var bytes = PdfByteStore.Read(decoded, c);
            Collect(PdfContentProgram.ParseCore(bytes, kind, PdfUnknownOperatorPolicy.Reject, null, c, color => PdfImageColorResolver.Components(color, resources, graph.Resolve, c)));
        }

        void Appearance(PdfValue value)
        {
            using var depth = c.EnterScope(PdfResourceKind.Depth);
            value = graph.Resolve(value);
            if (value.Kind == PdfValueKind.Stream)
                Read(value, PdfContentKind.Page, graph.Get(value, "Resources"));
            else if (value.Kind == PdfValueKind.Dictionary)
                foreach (var child in value.Entries.Values)
                    Appearance(child);
            else if (value.Kind != PdfValueKind.Null)
                throw PdfOptimizationEngine.Error(c, "ResourceUsage", "Unsupported appearance ownership prevents complete resource-name analysis.");
        }

        void Walk(PdfValue value)
        {
            using var depth = c.EnterScope(PdfResourceKind.Depth);
            c.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Array)
            {
                foreach (var child in value.Items)
                    Walk(child);
                return;
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return;
            if (Name(value, "S") == "JavaScript" || graph.Get(value, "XFA").Kind != PdfValueKind.Null)
                throw PdfOptimizationEngine.Error(c, "DynamicResourceUsage", "Dynamic scripts/XFA prevent a complete passive resource-usage proof; disable unused-resource pruning to retain them unchanged.");
            bool form = value.Kind == PdfValueKind.Stream && Name(value, "Subtype") == "Form";
            bool pattern = value.Kind == PdfValueKind.Stream && graph.Get(value, "PatternType").TryGetInteger(out long patternType) && patternType == 1;
            bool font = Name(value, "Subtype") == "Type3";
            bool page = Name(value, "Type") == "Page" || Name(value, "Type") == "Pages";
            if (Name(value, "Subtype") == "PS" || graph.Get(value, "Ref").Kind != PdfValueKind.Null && form)
                throw PdfOptimizationEngine.Error(c, "ResourceUsage", "Opaque printer/reference XObjects prevent complete passive resource-name analysis.");
            if (form || pattern)
                Read(value, PdfContentKind.Page, graph.Get(value, "Resources"));
            if (font)
            {
                var procedures = graph.Get(value, "CharProcs");
                if (procedures.Kind != PdfValueKind.Dictionary)
                    throw PdfOptimizationEngine.Error(c, "ResourceUsage", "A complete Type3 procedure inventory is required.");
                foreach (var proc in procedures.Entries.Values)
                    Read(proc, PdfContentKind.Type3Glyph, graph.Get(value, "Resources"));
            }

            if (form || pattern || font || page)
            {
                c.Consume(PdfResourceKind.ExpandedBytes, 64);
                owners.Add(value);
            }

            if (value.Entries.TryGetValue("AP", out var appearance))
                Appearance(appearance);
            var da = graph.Get(value, "DA");
            if (da.Kind == PdfValueKind.String)
            {
                c.Consume(PdfResourceKind.ExpandedBytes, da.StringBytes.Count + 64L);
                var bytes = new PdfByteBuilder(c);
                foreach (byte b in da.StringBytes)
                    bytes.Add(b);
                // DA fragments intentionally have an open text state, so collect their
                // explicit Tf operands with an isolated BT/ET wrapper.
                var wrapped = new PdfByteBuilder(c);
                foreach (byte b in new byte[]
                {
                    66,
                    84,
                    10
                }

                )
                    wrapped.Add(b);
                using (var input = bytes.Build().Open(0, bytes.Length))
                {
                    int b;
                    while ((b = input.ReadByte()) >= 0)
                        wrapped.Add((byte)b);
                }

                foreach (byte b in new byte[]
                {
                    10,
                    69,
                    84
                }

                )
                    wrapped.Add(b);
                Collect(PdfContentProgram.ParseCore(wrapped.Build(), PdfContentKind.Page, PdfUnknownOperatorPolicy.Reject, null, c));
            }

            foreach (var child in value.Entries.Values)
                Walk(child);
        }

        PdfValue Rewrite(PdfValue value)
        {
            using var depth = c.EnterScope(PdfResourceKind.Depth);
            c.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Array)
            {
                c.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 16L + 64);
                return PdfValue.OwnedArray(value.Items.Select(Rewrite).ToList());
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return value;
            var entries = graph.Copy(value);
            foreach (string key in entries.Keys.ToArray())
                entries[key] = Rewrite(entries[key]);
            if (owners.Contains(value) && value.Entries.TryGetValue("Resources", out var reference))
            {
                var resources = graph.Resolve(reference);
                if (resources.Kind != PdfValueKind.Dictionary)
                    throw PdfOptimizationEngine.Error(c, "ResourceUsage", "Resource owners require a dictionary before pruning.");
                var kept = graph.Copy(resources);
                foreach (string category in Categories)
                    if (kept.TryGetValue(category, out var declared))
                    {
                        var group = graph.Resolve(declared);
                        if (group.Kind != PdfValueKind.Dictionary)
                            throw PdfOptimizationEngine.Error(c, "ResourceUsage", "Resource categories require exact dictionary bindings.");
                        var selected = graph.Copy(group);
                        foreach (string name in selected.Keys.ToArray())
                            if (!used.Contains((category, name)))
                            {
                                selected.Remove(name);
                                removed++;
                            }

                        kept[category] = PdfValue.OwnedDictionary(selected);
                    }

                entries["Resources"] = PdfValue.OwnedDictionary(kept);
            }

            var dictionary = PdfValue.OwnedDictionary(entries);
            return value.Kind == PdfValueKind.Stream ? value.WithStreamDictionary(dictionary) : dictionary;
        }
    }
}
