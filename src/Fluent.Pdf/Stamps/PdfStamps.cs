using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Transactional owned page-content stamping. Removal affects recognized invocations, and is not secure redaction.</summary>
public static class PdfStamps
{
    private const string Marker = "FluentPdfStamp";
    private const string FormOwner = "FluentPdfStampOwner";
    private const string ScopeMarker = "FluentPdfStampScope";

    internal static IReadOnlyList<PdfStampInfo> InspectCore(PdfGraphEdit graph, int? pageIndex = null)
    {
        var owned = ReadOwned(graph, pageIndex);
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, owned.Count * 16L + 64);
        return new ReadOnlyCollection<PdfStampInfo>(owned.Select(x => x.Info).ToArray());
    }
    /// <summary>Passively validates and inventories owned streams without executing actions or decoding stamp images.</summary>
    public static PdfStampInspection Inspect(PdfDocumentSnapshot source, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        IReadOnlyList<PdfStampInfo>? items = null;
        var report = PdfFont.Run("InspectStamps", "managed.stamps", policy, cancellationToken, c =>
        {
            using var graph = new PdfGraphEdit(source, new(), c);
            var owned = ReadOwned(graph);
            c.Consume(PdfResourceKind.ExpandedBytes, owned.Count * 16L + 64);
            items = new ReadOnlyCollection<PdfStampInfo>(owned.Select(x => x.Info).ToArray());
        }, PdfCapability.ReadDocument);
        return new(items!, report);
    }

    /// <summary>Applies one uniquely identified stamp group to exact zero-based pages, once per selected page. Duplicate/empty page selections reject.</summary>
    public static PdfDocument Apply(PdfDocumentSnapshot source, string identifier, PdfStampContent content, IEnumerable<int> pageIndices, PdfStampOptions options, PdfOrganizationOptions? organization = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Validate(identifier, content, options);
        if (pageIndices == null)
            throw new ArgumentNullException(nameof(pageIndices));
        return Change(source, identifier, content, options, pageIndices, 0, organization, policy, cancellationToken);
    }

    /// <summary>Replaces every recognized invocation of the exact identifier, retaining its selected pages and applying an explicit new placement snapshot.</summary>
    public static PdfDocument Update(PdfDocumentSnapshot source, string identifier, PdfStampContent content, PdfStampOptions options, PdfOrganizationOptions? organization = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Validate(identifier, content, options);
        return Change(source, identifier, content, options, null, 1, organization, policy, cancellationToken);
    }

    /// <summary>Removes only canonical owned invocations of the exact identifier. Other content/resources and signed-edit safeguards remain intact; historical/unreachable bytes are not erased.</summary>
    public static PdfDocument Remove(PdfDocumentSnapshot source, string identifier, PdfOrganizationOptions? organization = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Identifier(identifier);
        return Change(source, identifier, null, null, null, 2, organization, policy, cancellationToken);
    }

    private static PdfDocument Change(PdfDocumentSnapshot source, string identifier, PdfStampContent? content, PdfStampOptions? options, IEnumerable<int>? selection, int mode, PdfOrganizationOptions? organization, PdfOperationPolicy? policy, CancellationToken token)
    {
        return PdfGraphEdit.Run(mode == 0 ? "ApplyStamp" : mode == 1 ? "UpdateStamp" : "RemoveStamp", source, organization, policy, token, graph =>
        {
            var c = graph.Context;
            var owned = ReadOwned(graph);
            c.Consume(PdfResourceKind.ExpandedBytes, owned.Count * 32L + 512);
            var selected = owned.Where(x => x.Info.Identifier == identifier).ToArray();
            if (mode == 0 && selected.Length != 0)
                throw graph.Error("Stamp.IdentifierExists", "The exact stamp identifier already exists; use explicit update or removal.");
            if (mode != 0 && selected.Length == 0)
                throw graph.Error("Stamp.NotFound", "No recognized owned stamp matches the requested identifier.");
            var pages = new HashSet<int>();
            if (mode == 0)
            {
                foreach (int index in selection!)
                {
                    Tick(c);
                    if (index < 0 || index >= graph.Snapshot.Pages.Count || !pages.Add(index))
                        throw graph.Error("Stamp.PageSelection", "Stamp pages must be unique valid zero-based indices.");
                }

                if (pages.Count == 0)
                    throw graph.Error("Stamp.PageSelection", "An explicit nonempty page selection is required.");
            }
            else
                foreach (var item in selected)
                    pages.Add(item.Info.PageIndex);
            if (mode != 2 && !options!.Artifact && graph.Catalog.Entries.ContainsKey("StructTreeRoot"))
                throw graph.Error("Stamp.StructureRequired", "A semantic stamp on tagged output requires an explicit structure-association authoring profile. Decoration may be marked as Artifact.");
            var writer = new PdfResourceWriter(graph.Edit, c, new());
            foreach (int index in pages.OrderBy(x => x))
            {
                Tick(c);
                var page = graph.Snapshot.Pages[index].Source!;
                var entries = graph.Copy(page.Dictionary);
                var resources = graph.Copy(graph.Get(page.EffectiveAttributes, "Resources"));
                var xobjects = resources.TryGetValue("XObject", out var xo) ? graph.Copy(graph.Resolve(xo)) : new Dictionary<string, PdfValue>(StringComparer.Ordinal);
                var contents = Contents(graph, page);
                var originalProgram = PdfPageContent.Read(page, PdfUnknownOperatorPolicy.Reject, graph.Graph.Version, new(), c);
                if (mode != 0)
                {
                    var counts = new Dictionary<string, int>(StringComparer.Ordinal);
                    foreach (var op in originalProgram.Operations)
                        if (op.Operation == PdfOperator.DrawObject)
                        {
                            Tick(c);
                            string name = op.Operands[0].NameValue;
                            counts.TryGetValue(name, out int count);
                            counts[name] = count == 0 ? 1 : 2;
                        }

                    foreach (var item in selected.Where(x => x.Info.PageIndex == index))
                    {
                        if (contents.RemoveAll(v => v.Kind == PdfValueKind.Reference && v.ReferenceId == item.Stream) != 1)
                            throw Ownership(graph);
                        int begin = contents.FindIndex(v => v.Kind == PdfValueKind.Reference && v.ReferenceId == item.Begin);
                        int end = contents.FindIndex(v => v.Kind == PdfValueKind.Reference && v.ReferenceId == item.End);
                        if (begin < 0 || end <= begin) throw Ownership(graph);
                        int pageOwnedCount = owned.Count(x => x.Info.PageIndex == index);
                        c.Consume(PdfResourceKind.ExpandedBytes, pageOwnedCount * 128L + 256);
                        var ownedStreams = new HashSet<PdfObjectId>();
                        foreach (var other in owned.Where(x => x.Info.PageIndex == index)) { ownedStreams.Add(other.Stream); ownedStreams.Add(other.Begin); ownedStreams.Add(other.End); }
                        bool externalStateUse = false;
                        for (int n = 0; n < contents.Count; n++)
                        {
                            Tick(c);
                            if (n > begin && n < end) continue;
                            var stream = contents[n];
                            if (stream.Kind != PdfValueKind.Reference || !ownedStreams.Contains(stream.ReferenceId)) externalStateUse = true;
                        }
                        if (externalStateUse)
                        {
                            // Later unrelated content can rely on the state restored by this pair.
                            // Retain those exact operators as ordinary content, relinquishing ownership.
                            foreach (var scopeId in new[] { item.Begin, item.End })
                            {
                                var scopeStream = graph.Resolve(PdfValue.Reference(scopeId));
                                var dictionary = graph.Copy(scopeStream); dictionary.Remove(ScopeMarker);
                                graph.Edit.Set(scopeId, scopeStream.WithStreamDictionary(PdfValue.OwnedDictionary(dictionary)));
                            }
                        }
                        else if (contents.RemoveAll(v => v.Kind == PdfValueKind.Reference && (v.ReferenceId == item.Begin || v.ReferenceId == item.End)) != 2) throw Ownership(graph);
                        if (counts.TryGetValue(item.Name, out int count) && count == 1)
                            xobjects.Remove(item.Name);
                    }
                }

                if (mode != 2)
                {
                    var canvas = PdfCanvas.CreateCore(page.Geometry, draw =>
                    {
                        for (int y = 0; y < options!.Rows; y++)
                            for (int x = 0; x < options.Columns; x++)
                            {
                                Tick(c);
                                var d = options.Destination;
                                d = new(d.X + x * (d.Width + options.HorizontalGap), d.Y + y * (d.Height + options.VerticalGap), d.Width, d.Height);
                                using var scope = draw.Save();
                                double angle = options.Rotation % 360 * Math.PI / 180, cos = Math.Cos(angle), sin = Math.Sin(angle), cx = d.X + d.Width / 2, cy = d.Y + d.Height / 2;
                                draw.Transform(new PdfMatrix(1, 0, 0, 1, cx, cy).Multiply(new(cos, sin, -sin, cos, 0, 0)).Multiply(new(1, 0, 0, 1, -cx, -cy)));
                                draw.DrawForm(content!.Resource, d, options.Fit);
                            }
                    }, new(), c, out _);
                    c.Consume(PdfResourceKind.ExpandedBytes, 4096);
                    c.Consume(PdfResourceKind.Objects, 24);
                    var group = PdfGraphicsNumbers.Dict(("S", PdfValue.Name("Transparency")), ("I", PdfValue.Boolean(true)), ("CS", PdfValue.Name("DeviceRGB")));
                    var baseForm = PdfGraphicsObjects.FormCore(canvas.Drawing, page.Geometry.Boxes.Media, null, group, c);
                    var state = PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.ExtGState, PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("ExtGState")), ("ca", PdfValues.Real(options!.Opacity)), ("CA", PdfValues.Real(options.Opacity)), ("BM", PdfValue.Name("Normal"))), "1.4", PdfResourceSet.Empty, c);
                    var drawing = PdfDrawing.CreateCore(new[] { new PdfContentOperation(PdfOperator.ExtendedState, PdfValue.Name("Alpha")), new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Content")) }, new(new PdfResourceBinding("Alpha", state), new PdfResourceBinding("Content", baseForm)), c);
                    var root = writer.Embed(PdfGraphicsObjects.FormCore(drawing, page.Geometry.Boxes.Media, null, null, c));
                    var rootValue = graph.Resolve(root);
                    var rootEntries = graph.Copy(rootValue);
                    rootEntries[FormOwner] = Ascii(identifier);
                    graph.Edit.Set(root.ReferenceId, rootValue.WithStreamDictionary(PdfValue.OwnedDictionary(rootEntries)));
                    string name = "FStamp";
                    int suffix = 0;
                    while (xobjects.ContainsKey(name))
                    {
                        Tick(c);
                        name = "FStamp" + (++suffix).ToString(CultureInfo.InvariantCulture);
                    }

                    xobjects[name] = root;
                    c.Consume(PdfResourceKind.ExpandedBytes, 8192);
                    c.Consume(PdfResourceKind.Objects, 48);
                    var program = Invocation(name, options.Artifact, c);
                    var mark = PdfGraphicsNumbers.Dict(("V", PdfValue.Integer(1)), ("Id", Ascii(identifier)), ("Kind", PdfValue.Integer((int)content!.Kind)), ("Name", PdfValue.Name(name)), ("Form", root), ("Options", Encode(options)));
                    var wrapper = graph.Copy(program);
                    wrapper[Marker] = mark;
                    var ownedStream = PdfValue.Reference(graph.Edit.Add(program.WithStreamDictionary(PdfValue.OwnedDictionary(wrapper))));
                    // Isolate the entire old program, whose balanced state may still leave CTM/color changes.
                    contents.Insert(0, State(true));
                    contents.Add(State(false));
                    if (options.Layer == PdfStampLayer.Background)
                        contents.Insert(0, ownedStream);
                    else
                        contents.Add(ownedStream);
                }

                resources["XObject"] = PdfValue.OwnedDictionary(xobjects);
                entries["Resources"] = PdfValue.OwnedDictionary(resources);
                entries["Contents"] = PdfValue.OwnedArray(contents);
                graph.Edit.Set(page.ObjectId, PdfValue.OwnedDictionary(entries));
            }

            PdfValue State(bool begin)
            {
                var bytes = new PdfByteBuilder(c);
                bytes.Add(begin ? (byte)'q' : (byte)'Q');
                bytes.Add(10);
                var store = bytes.Build();
                var dictionary = PdfGraphicsNumbers.Dict((ScopeMarker, PdfGraphicsNumbers.Dict(("V", PdfValue.Integer(1)), ("Id", Ascii(identifier)), ("Begin", PdfValue.Boolean(begin)))));
                return PdfValue.Reference(graph.Edit.Add(PdfValue.StreamValue(dictionary, store, 0, store.Length)));
            }
        }, content == null ? "1.4" : PdfVersionNumber.Text(Math.Max(14, PdfVersionNumber.Parse(content.Resource.MinimumVersion))));
    }

    private sealed class Owned
    {
        internal readonly PdfStampInfo Info;
        internal readonly PdfObjectId Stream;
        internal readonly string Name;
        internal PdfObjectId Begin, End;
        internal readonly int Position;
        internal Owned(PdfStampInfo info, PdfObjectId stream, string name, int position)
        {
            Info = info;
            Stream = stream;
            Name = name;
            Position = position;
        }
    }

    private static List<Owned> ReadOwned(PdfGraphEdit graph, int? pageIndex = null)
    {
        var c = graph.Context;
        c.Consume(PdfResourceKind.ExpandedBytes, 512);
        c.Consume(PdfResourceKind.Objects, 1);
        var result = new List<Owned>();
        for (int index = pageIndex ?? 0; index < (pageIndex.HasValue ? pageIndex.Value + 1 : graph.Snapshot.Pages.Count); index++)
        {
            Tick(c);
            var page = graph.Snapshot.Pages[index].Source!;
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var scopes = new Dictionary<string, List<(PdfObjectId Id, bool Begin, int Position)>>(StringComparer.Ordinal);
            var contents = Contents(graph, page);
            int pageStart = result.Count;
            for (int position = 0; position < contents.Count; position++)
            {
                var reference = contents[position];
                Tick(c);
                var stream = graph.Resolve(reference);
                if (stream.Entries.TryGetValue(ScopeMarker, out var rawScope))
                {
                    var scope = graph.Resolve(rawScope);
                    if (scope.Kind != PdfValueKind.Dictionary || Integer(graph.Get(scope, "V"), graph) != 1 || reference.Kind != PdfValueKind.Reference)
                        throw Ownership(graph);
                    string owner = ReadAscii(graph.Get(scope, "Id"), graph);
                    var begin = graph.Get(scope, "Begin");
                    if (begin.Kind != PdfValueKind.Boolean || stream.RawStreamLength != 2 || stream.Entries.ContainsKey("Filter"))
                        throw Ownership(graph);
                    c.Consume(PdfResourceKind.InputBytes, 2);
                    using var input = stream.OpenRawStream();
                    if (input.ReadByte() != (begin.BooleanValue ? 'q' : 'Q') || input.ReadByte() != 10 || input.ReadByte() != -1)
                        throw Ownership(graph);
                    if (!scopes.TryGetValue(owner, out var pair))
                    {
                        Tick(c);
                        scopes[owner] = pair = new();
                    }

                    pair.Add((reference.ReferenceId, begin.BooleanValue, position));
                    if (pair.Count > 2)
                        throw Ownership(graph);
                }

                if (!stream.Entries.TryGetValue(Marker, out var rawMarker))
                    continue;
                var mark = graph.Resolve(rawMarker);
                if (mark.Kind != PdfValueKind.Dictionary || Integer(graph.Get(mark, "V"), graph) != 1)
                    throw Ownership(graph);
                string id = ReadAscii(graph.Get(mark, "Id"), graph);
                if (!ids.Add(id) || reference.Kind != PdfValueKind.Reference)
                    throw Ownership(graph);
                int kind = Integer(graph.Get(mark, "Kind"), graph);
                if (!Enum.IsDefined(typeof(PdfStampContentKind), kind))
                    throw Ownership(graph);
                var nameValue = graph.Get(mark, "Name");
                if (nameValue.Kind != PdfValueKind.Name)
                    throw Ownership(graph);
                string name = nameValue.NameValue;
                if (name.Length == 0 || name.Length > 64 || name.Any(ch => !(ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9')))
                    throw Ownership(graph);
                var form = Raw(mark, "Form");
                var bound = Raw(graph.Get(graph.Get(page.EffectiveAttributes, "Resources"), "XObject"), name);
                if (form.Kind != PdfValueKind.Reference || bound.Kind != PdfValueKind.Reference || form.ReferenceId != bound.ReferenceId)
                    throw Ownership(graph);
                var definition = graph.Resolve(form);
                if (definition.Kind != PdfValueKind.Stream || ReadAscii(graph.Get(definition, FormOwner), graph) != id || PdfPageImportSource.Name(definition, "Subtype") != "Form")
                    throw Ownership(graph);
                c.Consume(PdfResourceKind.ExpandedBytes, 1024);
                c.Consume(PdfResourceKind.Objects, 8);
                var options = Decode(graph.Get(mark, "Options"), graph);
                var expected = Invocation(name, options.Artifact, c);
                c.Consume(PdfResourceKind.InputBytes, stream.RawStreamLength);
                using var decoded = PdfFilters.Decode(PdfFilterDeclarations.Resolve(stream, x => graph.Edit.Resolve(x), c), c, new());
                using var exact = expected.OpenRawStream();
                int a, b;
                do
                {
                    c.Checkpoint();
                    a = decoded.ReadByte();
                    b = exact.ReadByte();
                    if (a != b)
                        throw Ownership(graph);
                }
                while (a >= 0);
                c.Consume(PdfResourceKind.ExpandedBytes, 256);
                result.Add(new(new(id, index, (PdfStampContentKind)kind, options), reference.ReferenceId, name, position));
            }

            if (scopes.Count != ids.Count)
                throw Ownership(graph);
            for (int n = pageStart; n < result.Count; n++)
            {
                Tick(c);
                var owned = result[n];
                if (!scopes.TryGetValue(owned.Info.Identifier, out var pair) || pair.Count != 2 || !pair[0].Begin || pair[1].Begin || pair[0].Position >= pair[1].Position || (owned.Info.Options.Layer == PdfStampLayer.Background ? owned.Position >= pair[0].Position : owned.Position <= pair[1].Position))
                    throw Ownership(graph);
                owned.Begin = pair[0].Id;
                owned.End = pair[1].Id;
            }
        }

        return result;
    }

    private static List<PdfValue> Contents(PdfGraphEdit graph, PdfPageSource page)
    {
        Tick(graph.Context);
        var items = new List<PdfValue>();
        var path = new HashSet<PdfObjectId>();
        Append(Raw(page.Dictionary, "Contents"));
        return items;
        void Append(PdfValue raw)
        {
            Tick(graph.Context);
            using var depth = graph.Context.EnterScope(PdfResourceKind.Depth);
            if (raw.Kind == PdfValueKind.Reference && !path.Add(raw.ReferenceId))
                throw graph.Error("Stamp.ContentCycle", "Page content arrays contain a cycle.");
            try
            {
                var value = graph.Resolve(raw);
                if (value.Kind == PdfValueKind.Array)
                    foreach (var child in value.Items)
                        Append(child);
                else if (value.Kind == PdfValueKind.Stream)
                    items.Add(raw);
                else if (value.Kind != PdfValueKind.Null)
                    throw graph.Error("Stamp.ContentStream", "Page content must resolve to a stream or stream array.");
            }
            finally
            {
                if (raw.Kind == PdfValueKind.Reference)
                    path.Remove(raw.ReferenceId);
            }
        }
    }

    private static PdfValue Invocation(string name, bool artifact, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 1024);
        c.Consume(PdfResourceKind.Objects, 8);
        var operations = new List<PdfContentOperation>
        {
            new(PdfOperator.SaveState)
        };
        if (artifact)
            operations.Add(new(PdfOperator.BeginMarkedContent, PdfValue.Name("Artifact")));
        operations.Add(new(PdfOperator.DrawObject, PdfValue.Name(name)));
        if (artifact)
            operations.Add(new(PdfOperator.EndMarkedContent));
        operations.Add(new(PdfOperator.RestoreState));
        return PdfContentProgram.CreateCore(operations, PdfContentKind.Page, c).ToStream(c);
    }

    private static PdfValue Encode(PdfStampOptions o) => PdfGraphicsNumbers.Dict(("Destination", o.Destination.Value), ("Layer", PdfValue.Integer((int)o.Layer)), ("Opacity", PdfValues.Real(o.Opacity)), ("Rotation", PdfValues.Real(o.Rotation)), ("Artifact", PdfValue.Boolean(o.Artifact)), ("Columns", PdfValue.Integer(o.Columns)), ("Rows", PdfValue.Integer(o.Rows)), ("HorizontalGap", PdfValues.Real(o.HorizontalGap)), ("VerticalGap", PdfValues.Real(o.VerticalGap)), ("Fit", PdfValue.Integer((int)o.Fit)));
    private static PdfStampOptions Decode(PdfValue value, PdfGraphEdit graph)
    {
        try
        {
            if (value.Kind != PdfValueKind.Dictionary)
                throw Ownership(graph);
            var rect = graph.Get(value, "Destination");
            if (rect.Kind != PdfValueKind.Array || rect.Items.Count != 4)
                throw Ownership(graph);
            double x = Number(graph.Resolve(rect.Items[0])), y = Number(graph.Resolve(rect.Items[1])), right = Number(graph.Resolve(rect.Items[2])), top = Number(graph.Resolve(rect.Items[3]));
            var artifact = graph.Get(value, "Artifact");
            if (artifact.Kind != PdfValueKind.Boolean)
                throw Ownership(graph);
            return new(new(x, y, right - x, top - y), (PdfStampLayer)Integer(graph.Get(value, "Layer"), graph), Number(graph.Get(value, "Opacity")), Number(graph.Get(value, "Rotation")), artifact.BooleanValue, Integer(graph.Get(value, "Columns"), graph), Integer(graph.Get(value, "Rows"), graph), Number(graph.Get(value, "HorizontalGap")), Number(graph.Get(value, "VerticalGap")), (PdfCanvasFit)Integer(graph.Get(value, "Fit"), graph));
        }
        catch (ArgumentException)
        {
            throw Ownership(graph);
        }
        catch (InvalidOperationException)
        {
            throw Ownership(graph);
        }

        double Number(PdfValue number) => number.Kind == PdfValueKind.Number ? number.NumberValue : throw Ownership(graph);
    }

    private static PdfValue Raw(PdfValue dictionary, string key) => dictionary.Kind == PdfValueKind.Dictionary || dictionary.Kind == PdfValueKind.Stream ? dictionary.Entries.TryGetValue(key, out var v) ? v : PdfValue.Null : PdfValue.Null;
    private static int Integer(PdfValue value, PdfGraphEdit graph) => value.TryGetInteger(out long n) && n >= 0 && n <= int.MaxValue ? (int)n : throw Ownership(graph);
    private static PdfValue Ascii(string text) => PdfValue.String(Encoding.ASCII.GetBytes(text));
    private static string ReadAscii(PdfValue value, PdfGraphEdit graph)
    {
        if (value.Kind != PdfValueKind.String || value.StringBytes.Count < 1 || value.StringBytes.Count > 64 || value.StringBytes.Any(b => !(b >= 'A' && b <= 'Z' || b >= 'a' && b <= 'z' || b >= '0' && b <= '9' || b == '-' || b == '_')))
            throw Ownership(graph);
        graph.Context.Consume(PdfResourceKind.ExpandedBytes, 256);
        return Encoding.ASCII.GetString(value.StringBytes.ToArray());
    }

    private static void Identifier(string identifier)
    {
        if (string.IsNullOrEmpty(identifier) || identifier.Length > 64 || identifier.Any(ch => !(ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9' || ch == '-' || ch == '_')))
            throw new ArgumentException("An explicit1–64 character ASCII letter/digit/hyphen/underscore stamp identifier is required.", nameof(identifier));
    }

    private static void Validate(string id, PdfStampContent content, PdfStampOptions options)
    {
        Identifier(id);
        if (content == null)
            throw new ArgumentNullException(nameof(content));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
    }

    private static void Tick(PdfOperationContext c)
    {
        c.Checkpoint();
        c.Consume(PdfResourceKind.Objects, 1);
        c.Consume(PdfResourceKind.ExpandedBytes, 192);
    }

    private static PdfException Ownership(PdfGraphEdit graph) => graph.Error("Stamp.Ownership", "A purported owned stamp has malformed, duplicate or changed stream/resource/ownership metadata. No heuristic removal is performed.");
}
