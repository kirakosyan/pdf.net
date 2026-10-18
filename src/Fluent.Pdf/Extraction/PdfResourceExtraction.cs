using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Passive source resource/path/artifact inventory without rendering, font discovery or document action execution.</summary>
public static class PdfResourceExtraction
{
    /// <summary>Captures declared resources separately from actual uses, literal paths and explicit artifacts under one shared context. Unknown resource roles remain raw; unknown operators require explicit preservation.</summary>
    public static PdfPageResourceManifest ReadPage(PdfDocumentSnapshot source, int pageIndex, PdfUnknownOperatorPolicy unknownOperators = PdfUnknownOperatorPolicy.Reject, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (!Enum.IsDefined(typeof(PdfUnknownOperatorPolicy), unknownOperators))
            throw new ArgumentOutOfRangeException(nameof(unknownOperators));
        Engine? engine = null;
        var report = PdfFont.Run("ExtractPageResources", "managed.resources", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            using var graph = new PdfGraphEdit(source, new(), c);
            c.Consume(PdfResourceKind.ExpandedBytes, 4096);
            c.Consume(PdfResourceKind.Objects, 16);
            engine = new(graph, pageIndex, unknownOperators, providers ?? PdfProviderRegistry.Empty);
            engine.Read();
        }, PdfCapability.ReadDocument);
        return engine!.Finish(report);
    }

    private sealed class Engine
    {
        private readonly PdfGraphEdit _graph;
        private readonly int _page;
        private readonly PdfUnknownOperatorPolicy _unknown;
        private readonly PdfProviderRegistry _providers;
        private readonly List<PdfExtractedResource> _resources = new();
        private readonly List<PdfResourceDeclaration> _declarations = new();
        private readonly List<PdfResourceOccurrence> _uses = new();
        private readonly List<PdfExtractedPath> _paths = new();
        private readonly List<PdfExtractedArtifact> _artifacts = new();
        private readonly List<PdfResourceContent> _content = new();
        private readonly Dictionary<PdfValue, int> _identities = new();
        private readonly HashSet<(PdfObjectId, PdfValue)> _scopes = new();
        private readonly HashSet<PdfValue> _fontScanned = new();
        private readonly HashSet<PdfValue> _activeForms = new();
        private readonly Dictionary<(PdfValue, PdfValue), PdfContentProgram> _formPrograms = new();
        private readonly List<PdfObjectId> _formPath = new();
        private readonly List<int> _formCalls = new();
        private readonly List<Artifact> _activeArtifacts = new();
        private IReadOnlyList<PdfStampInfo> _stamps = Array.Empty<PdfStampInfo>();
        private PdfOperationContext C => _graph.Context;

        private sealed class Artifact
        {
            internal readonly PdfResourceSource Source;
            internal readonly PdfValue Properties;
            internal readonly List<int> Uses = new(), Paths = new();
            internal Artifact(PdfResourceSource source, PdfValue properties)
            {
                Source = source;
                Properties = properties;
            }
        }

        internal Engine(PdfGraphEdit graph, int page, PdfUnknownOperatorPolicy unknown, PdfProviderRegistry providers)
        {
            _graph = graph;
            _page = page;
            _unknown = unknown;
            _providers = providers;
        }

        internal void Read()
        {
            var page = _graph.Snapshot.Pages[_page].Source!;
            var scope = _graph.Get(page.EffectiveAttributes, "Resources");
            RegisterScope(page.ObjectId, scope);
            var program = PdfPageContent.Read(page, _unknown, _graph.Graph.Version, _providers, C);
            Tick();
            _content.Add(new(page.ObjectId, program, scope));
            Walk(program, scope, page.ObjectId, PdfMatrix.Identity);
            var stamps = PdfStamps.InspectCore(_graph);
            C.Consume(PdfResourceKind.ExpandedBytes, stamps.Count * 16L + 64);
            _stamps = new ReadOnlyCollection<PdfStampInfo>(stamps.Where(x => x.PageIndex == _page).ToArray());
            C.Consume(PdfResourceKind.ExpandedBytes, 1024);
            C.Consume(PdfResourceKind.Objects, 16);
        }

        internal PdfPageResourceManifest Finish(PdfOperationReport report) => new(_graph.Snapshot, _page, _resources.AsReadOnly(), _declarations.AsReadOnly(), _uses.AsReadOnly(), _paths.AsReadOnly(), _artifacts.AsReadOnly(), _content.AsReadOnly(), _stamps, report);
        private void RegisterScope(PdfObjectId owner, PdfValue scope)
        {
            Tick();
            using var depth = C.EnterScope(PdfResourceKind.Depth);
            scope = _graph.Resolve(scope);
            if (scope.Kind == PdfValueKind.Null)
                return;
            if (scope.Kind != PdfValueKind.Dictionary)
                throw Error("Dictionary", "Effective resources must be a dictionary.");
            if (!_scopes.Add((owner, scope)))
                return;
            foreach (var category in scope.Entries)
            {
                Tick();
                var names = _graph.Resolve(category.Value);
                if (names.Kind != PdfValueKind.Dictionary)
                {
                    Info("UnknownCategory", "A resource category is not a name dictionary; its raw definition remains available in the captured scope.");
                    continue;
                }

                foreach (var binding in names.Entries)
                {
                    Tick();
                    var resource = Resource(binding.Value, category.Key);
                    _declarations.Add(new(resource.Index, category.Key, binding.Key, owner));
                    if (resource.Kind == PdfExtractedResourceKind.Font)
                        Fonts(resource);
                    if (resource.Kind == PdfExtractedResourceKind.GraphicsState)
                    {
                        var selectedFont = GraphicsFont(resource);
                        if (selectedFont != null)
                            Fonts(selectedFont);
                    }

                    if (resource.Kind == PdfExtractedResourceKind.Form)
                    {
                        var form = resource.Definition;
                        var child = form.Entries.ContainsKey("Resources") ? _graph.Get(form, "Resources") : scope;
                        RegisterScope(resource.ObjectId ?? owner, child);
                    }
                }
            }
        }

        private PdfExtractedResource Resource(PdfValue raw, string category, string? declared = null)
        {
            Tick();
            var definition = Resolve(raw, out var id);
            string subtype = definition.Kind == PdfValueKind.Dictionary || definition.Kind == PdfValueKind.Stream ? PdfPageImportSource.Name(definition, "Subtype") ?? "" : "";
            var kind = category switch
            {
                "XObject" => definition.Kind == PdfValueKind.Stream && subtype == "Image" ? PdfExtractedResourceKind.Image : definition.Kind == PdfValueKind.Stream && subtype == "Form" ? PdfExtractedResourceKind.Form : PdfExtractedResourceKind.Unknown,
                "InlineImage" => definition.Kind == PdfValueKind.Stream ? PdfExtractedResourceKind.Image : PdfExtractedResourceKind.Unknown,
                "Font" => definition.Kind == PdfValueKind.Dictionary ? PdfExtractedResourceKind.Font : PdfExtractedResourceKind.Unknown,
                "FontProgram" => definition.Kind == PdfValueKind.Stream ? PdfExtractedResourceKind.FontProgram : PdfExtractedResourceKind.Unknown,
                "Shading" => PdfExtractedResourceKind.Shading,
                "Pattern" => PdfExtractedResourceKind.Pattern,
                "ColorSpace" => PdfExtractedResourceKind.ColorSpace,
                "ExtGState" => PdfExtractedResourceKind.GraphicsState,
                "Properties" => PdfExtractedResourceKind.Properties,
                _ => PdfExtractedResourceKind.Unknown
            };
            if (_identities.TryGetValue(definition, out int existing))
            {
                var entry = _resources[existing];
                if (entry.Kind != kind)
                {
                    entry.Kind = PdfExtractedResourceKind.Unknown;
                    Info("RoleConflict", "One resource identity has incompatible declared roles; no role is inferred.");
                }

                return entry;
            }

            if (kind == PdfExtractedResourceKind.Image)
            {
                var width = _graph.Get(definition, "Width");
                var height = _graph.Get(definition, "Height");
                if (width.Kind == PdfValueKind.Null)
                    width = _graph.Get(definition, "W");
                if (height.Kind == PdfValueKind.Null)
                    height = _graph.Get(definition, "H");
                if (!width.TryGetInteger(out long w) || !height.TryGetInteger(out long h) || w < 1 || h < 1 || w > int.MaxValue || h > int.MaxValue)
                    throw Error("ImageGeometry", "An image requires positive bounded declared dimensions.");
                C.ConsumePixels((int)w, (int)h);
            }

            if (kind == PdfExtractedResourceKind.Unknown)
                Info("UnknownType", "An unknown or incompatible resource role remains in the raw inventory without execution or substitution.");
            var created = new PdfExtractedResource(_graph.Snapshot, _resources.Count, kind, id, definition, declared ?? subtype);
            _identities.Add(definition, created.Index);
            _resources.Add(created);
            return created;
        }

        private void Fonts(PdfExtractedResource font)
        {
            Tick();
            if (!_fontScanned.Add(font.Definition))
                return;
            var programs = new List<int>();
            var declared = font.Definition;
            var subtype = _graph.Get(declared, "Subtype");
            if (subtype.Kind == PdfValueKind.Name && subtype.NameValue == "Type0")
            {
                var descendants = _graph.Get(declared, "DescendantFonts");
                if (descendants.Kind != PdfValueKind.Array || descendants.Items.Count != 1)
                    throw Error("FontDescendants", "A composite font requires exactly one declared descendant font.");
                declared = _graph.Resolve(descendants.Items[0]);
            }

            var descriptor = _graph.Get(declared, "FontDescriptor");
            if (descriptor.Kind != PdfValueKind.Null && descriptor.Kind != PdfValueKind.Dictionary)
                throw Error("FontDescriptor", "A font descriptor must be a dictionary.");
            foreach (string key in new[]
            {
                "FontFile",
                "FontFile2",
                "FontFile3"
            }

            )
                if (descriptor.Kind == PdfValueKind.Dictionary && descriptor.Entries.TryGetValue(key, out var file))
                {
                    Tick();
                    var program = Resource(file, "FontProgram", key);
                    programs.Add(program.Index);
                }

            if (subtype.Kind == PdfValueKind.Name && subtype.NameValue == "Type3")
            {
                var glyphs = _graph.Get(declared, "CharProcs");
                if (glyphs.Kind != PdfValueKind.Dictionary)
                    throw Error("GlyphProcedures", "A Type3 font requires a CharProcs dictionary.");
                foreach (var glyph in glyphs.Entries)
                {
                    Tick();
                    programs.Add(Resource(glyph.Value, "FontProgram", "Type3Glyph").Index);
                }
            }

            C.Consume(PdfResourceKind.ExpandedBytes, programs.Count * 8L + 64);
            font.FontProgramIndices = new ReadOnlyCollection<int>(programs);
        }

        private void Walk(PdfContentProgram program, PdfValue scope, PdfObjectId container, PdfMatrix initial)
        {
            Tick();
            using var depth = C.EnterScope(PdfResourceKind.Depth);
            var transforms = new Stack<PdfMatrix>();
            var marked = new Stack<bool>();
            var path = new List<PdfExtractedPathElement>();
            PdfMatrix current = initial;
            bool clip = false, evenOdd = false;
            for (int index = 0; index < program.Operations.Count; index++)
            {
                Tick();
                var op = program.Operations[index];
                var kind = op.Operation;
                PdfResourceSource Location()
                {
                    C.Consume(PdfResourceKind.ExpandedBytes, _formPath.Count * 32L + 192);
                    return new(_page, container, index, new ReadOnlyCollection<PdfObjectId>(_formPath.ToArray()), new ReadOnlyCollection<int>(_formCalls.ToArray()));
                }

                if (kind == PdfOperator.SaveState)
                    transforms.Push(current);
                else if (kind == PdfOperator.RestoreState)
                    current = transforms.Pop();
                else if (kind == PdfOperator.Transform)
                    current = current.Multiply(new(op.Operands[0].NumberValue, op.Operands[1].NumberValue, op.Operands[2].NumberValue, op.Operands[3].NumberValue, op.Operands[4].NumberValue, op.Operands[5].NumberValue));
                else if (kind >= PdfOperator.MoveTo && kind <= PdfOperator.Rectangle)
                    path.Add(new(op, current, Location()));
                else if (kind == PdfOperator.Clip || kind == PdfOperator.ClipEvenOdd)
                {
                    clip = true;
                    evenOdd = kind == PdfOperator.ClipEvenOdd;
                }
                else if (kind >= PdfOperator.Stroke && kind <= PdfOperator.EndPath)
                {
                    if (path.Count > 0 || clip)
                    {
                        C.Consume(PdfResourceKind.ExpandedBytes, path.Count * 16L + 128);
                        foreach (var a in _activeArtifacts)
                        {
                            Tick();
                            a.Paths.Add(_paths.Count);
                        }

                        _paths.Add(new(new ReadOnlyCollection<PdfExtractedPathElement>(path.ToArray()), op, Location(), clip, evenOdd, _activeArtifacts.Count != 0));
                    }

                    path.Clear();
                    clip = false;
                    evenOdd = false;
                }
                else if (kind == PdfOperator.BeginMarkedContent || kind == PdfOperator.BeginMarkedContentProperties)
                {
                    bool artifact = op.Operands[0].NameValue == "Artifact";
                    marked.Push(artifact);
                    if (artifact)
                    {
                        PdfValue properties = PdfValue.Null;
                        if (op.Operands.Count == 2)
                            properties = op.Operands[1].Kind == PdfValueKind.Name ? Named(scope, "Properties", op.Operands[1].NameValue) : _graph.Resolve(op.Operands[1]);
                        if (properties.Kind != PdfValueKind.Null && properties.Kind != PdfValueKind.Dictionary)
                            throw Error("ArtifactProperties", "Artifact properties must resolve to a dictionary.");
                        _activeArtifacts.Add(new(Location(), properties));
                    }
                }
                else if (kind == PdfOperator.EndMarkedContent)
                {
                    if (marked.Pop())
                    {
                        var a = _activeArtifacts[_activeArtifacts.Count - 1];
                        _activeArtifacts.RemoveAt(_activeArtifacts.Count - 1);
                        C.Consume(PdfResourceKind.ExpandedBytes, (a.Uses.Count + a.Paths.Count) * 8L + 128);
                        _artifacts.Add(new(a.Source, index, a.Properties, new ReadOnlyCollection<int>(a.Uses), new ReadOnlyCollection<int>(a.Paths)));
                    }
                }
                else if (kind == PdfOperator.InlineImage)
                    Use(Resource(op.Image!, "InlineImage", "InlineImage"), "InlineImage", current);
                else if (kind == PdfOperator.DrawObject)
                {
                    var entry = Resource(NamedRaw(scope, "XObject", op.Operands[0].NameValue), "XObject");
                    var matrix = entry.Kind == PdfExtractedResourceKind.Form ? current.Multiply(PdfPageGraphics.Matrix(_graph.Get(entry.Definition, "Matrix"), _graph)) : current;
                    Use(entry, op.Token, matrix);
                    if (entry.Kind == PdfExtractedResourceKind.Form)
                    {
                        if (!_activeForms.Add(entry.Definition))
                            throw Error("FormCycle", "Invoked Forms contain a recursive reference cycle.");
                        if (!entry.ObjectId.HasValue)
                            throw Error("DirectFormProfile", "An invoked Form requires an indirect source identity in this profile.");
                        var childScope = entry.Definition.Entries.ContainsKey("Resources") ? _graph.Get(entry.Definition, "Resources") : scope;
                        var key = (entry.Definition, childScope);
                        if (!_formPrograms.TryGetValue(key, out var child))
                        {
                            var filters = PdfFilterDeclarations.Resolve(entry.Definition, _graph.Edit.Resolve, C);
                            C.Consume(PdfResourceKind.InputBytes, filters.RawStreamLength);
                            using var decoded = PdfFilters.Decode(filters, C, _providers);
                            var bytes = PdfByteStore.Read(decoded, C);
                            child = PdfContentProgram.ParseCore(bytes, PdfContentKind.Page, _unknown, _graph.Graph.Version, C, color => PdfImageColorResolver.Components(color, childScope, _graph.Resolve, C));
                            Tick();
                            _formPrograms.Add(key, child);
                            _content.Add(new(entry.ObjectId.Value, child, childScope));
                        }

                        _formPath.Add(entry.ObjectId.Value);
                        _formCalls.Add(index);
                        try
                        {
                            Walk(child, childScope, entry.ObjectId.Value, matrix);
                        }
                        finally
                        {
                            _formPath.RemoveAt(_formPath.Count - 1);
                            _formCalls.RemoveAt(_formCalls.Count - 1);
                            _activeForms.Remove(entry.Definition);
                        }
                    }
                }
                else if (kind == PdfOperator.Font)
                    Use(Resource(NamedRaw(scope, "Font", op.Operands[0].NameValue), "Font"), op.Token, current);
                else if (kind == PdfOperator.Shading)
                    Use(Resource(NamedRaw(scope, "Shading", op.Operands[0].NameValue), "Shading"), op.Token, current);
                else if (kind == PdfOperator.ExtendedState)
                {
                    var state = Resource(NamedRaw(scope, "ExtGState", op.Operands[0].NameValue), "ExtGState");
                    Use(state, op.Token, current);
                    var selectedFont = GraphicsFont(state);
                    if (selectedFont != null)
                        Use(selectedFont, "gs.Font", current);
                }
                else if ((kind == PdfOperator.FillColorExtended || kind == PdfOperator.StrokeColorExtended) && op.Operands.Count > 0 && op.Operands[op.Operands.Count - 1].Kind == PdfValueKind.Name)
                    Use(Resource(NamedRaw(scope, "Pattern", op.Operands[op.Operands.Count - 1].NameValue), "Pattern"), op.Token, current);
                else if (kind == PdfOperator.Unknown)
                    Info("UnknownOperator", "An explicitly preserved unknown operator remains in the container program without inferred semantics.");
                void Use(PdfExtractedResource resource, string usage, PdfMatrix matrix)
                {
                    Tick();
                    foreach (var a in _activeArtifacts)
                    {
                        Tick();
                        a.Uses.Add(_uses.Count);
                    }

                    _uses.Add(new(resource.Index, usage, Location(), matrix, _activeArtifacts.Count != 0));
                }
            }

            if (path.Count != 0)
                Info("UnpaintedPath", "An unterminated source path remains in the captured container program without a fabricated paint occurrence.");
        }

        private PdfValue NamedRaw(PdfValue scope, string category, string name)
        {
            var dictionary = _graph.Get(scope, category);
            if (dictionary.Kind != PdfValueKind.Dictionary || !dictionary.Entries.TryGetValue(name, out var value))
                throw Error("ResourceMissing", "An operator references an absent effective resource binding.");
            return value;
        }

        private PdfValue Named(PdfValue scope, string category, string name) => _graph.Resolve(NamedRaw(scope, category, name));
        private PdfExtractedResource? GraphicsFont(PdfExtractedResource state)
        {
            var font = _graph.Get(state.Definition, "Font");
            if (font.Kind == PdfValueKind.Null)
                return null;
            if (font.Kind != PdfValueKind.Array || font.Items.Count != 2 || _graph.Resolve(font.Items[1]).Kind != PdfValueKind.Number)
                throw Error("GraphicsFont", "An extended-state font selection requires a font/size pair.");
            return Resource(font.Items[0], "Font");
        }

        private PdfValue Resolve(PdfValue value, out PdfObjectId? id)
        {
            id = null;
            var seen = new HashSet<PdfObjectId>();
            while (value.Kind == PdfValueKind.Reference)
            {
                Tick();
                if (!seen.Add(value.ReferenceId))
                    throw Error("ReferenceCycle", "Resource references contain a cycle.");
                id = value.ReferenceId;
                value = _graph.Edit.Resolve(value.ReferenceId);
            }

            return value;
        }

        private void Tick()
        {
            C.Checkpoint();
            C.Consume(PdfResourceKind.Objects, 1);
            C.Consume(PdfResourceKind.ExpandedBytes, 256);
        }

        private void Info(string code, string message) => C.Report(new("Resources." + code, message, severity: PdfDiagnosticSeverity.Information, pageIndex: _page));
        private PdfException Error(string code, string message) => _graph.Error("Resources." + code, message);
    }
}
