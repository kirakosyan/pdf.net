using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Managed passive page/Form text extraction with explicit font providers, mappings and geometric inferences.</summary>
public static class PdfTextExtraction
{
    /// <summary>Extracts one page under a shared parse/font/content budget. Returned quads are advance cells rather than glyph ink.</summary>
    public static PdfTextExtractionResult ReadPage(PdfDocumentSnapshot source, int pageIndex, PdfTextExtractionOptions? options = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        options ??= new();
        providers ??= PdfProviderRegistry.Empty;
        PdfTextPageData? data = null;
        var report = PdfFont.Run("ExtractPageText", "managed.extraction", policy ?? source.Options.OperationPolicy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(source, new(), context);
            data = new PdfTextExtractionEngine(graph, options, providers).Read(pageIndex);
        }, PdfCapability.ReadDocument);
        return data!.Result(report);
    }
}

internal sealed class PdfTextPageData
{
    internal readonly int Page;
    internal readonly PdfPageGeometry Geometry;
    internal PdfContentProgram Program = null!;
    internal readonly List<PdfExtractedCharacter> Characters = new();
    internal readonly List<PdfExtractedSpan> Spans = new();
    internal readonly List<(int First, int Count, string Uri)> SourceLinks = new();
    internal List<PdfExtractedWord> Words = new();
    internal List<PdfExtractedLine> Lines = new();
    internal List<PdfExtractedParagraph> Paragraphs = new();
    internal string Raw = "", Text = "", Layout = "";
    internal PdfTextPageData(int page, PdfPageGeometry geometry)
    {
        Page = page;
        Geometry = geometry;
    }

    internal PdfTextExtractionResult Result(PdfOperationReport report) => new(Page, Geometry, Characters, Spans, Words, Lines, Paragraphs, Raw, Text, Layout, report);
}

internal sealed class PdfTextExtractionEngine
{
    private readonly PdfGraphEdit _graph;
    private readonly PdfTextExtractionOptions _options;
    private readonly PdfProviderRegistry _providers;
    private readonly PdfLayerEvaluator _layers;
    private readonly bool _collectSourceLinks;
    private readonly Dictionary<PdfValue, PdfExtractionFont> _fonts = new();
    private readonly HashSet<PdfValue> _formPath = new();
    private PdfTextPageData _page = null!;
    private PdfMatrix _display;
    private int _operation, _totalCodes;
    private Actual? _actual;
    private PdfOperationContext Context => _graph.Context;

    private sealed class State
    {
        internal PdfMatrix Ctm = PdfMatrix.Identity;
        internal PdfExtractionFont? Font;
        internal double Size, CharacterSpacing, WordSpacing, Leading, Rise;
        internal double Horizontal = 1;
        internal int Mode;
        internal State Copy() => (State)MemberwiseClone();
    }

    private sealed class Actual
    {
        internal readonly string Text;
        internal readonly int First, Total;
        internal readonly bool InitiallySelected;
        internal int SelectedPainting, ExcludedPainting;
        internal Actual(string text, int first, int total, bool selected)
        {
            Text = text;
            First = first;
            Total = total;
            InitiallySelected = selected;
        }
    }

    internal PdfTextExtractionEngine(PdfGraphEdit graph, PdfTextExtractionOptions options, PdfProviderRegistry providers, bool collectSourceLinks = false)
    {
        _graph = graph;
        _collectSourceLinks = collectSourceLinks;
        _options = options;
        _providers = providers;
        _layers = new(graph, options.LayerConfiguration, options.LayerUsage);
    }

    internal PdfTextPageData Read(int index)
    {
        var page = _graph.Snapshot.Pages[index];
        _page = new(index, page.Geometry);
        _display = page.Geometry.UserToDisplay();
        var binding = page.Source!;
        var resources = _graph.Get(binding.EffectiveAttributes, "Resources");
        var program = PdfPageContent.Read(binding, PdfUnknownOperatorPolicy.Reject, null, _providers, Context);
        _page.Program = program;
        try
        {
            Execute(program, resources, new State(), true);
            new PdfTextGrouping(_page, _options, Context).Build();
        }
        catch (ArgumentException)
        {
            throw Error("GeometryRange", "The source transforms exceed finite extraction geometry.");
        }

        return _page;
    }

    private void Execute(PdfContentProgram program, PdfValue resources, State initial, bool initialVisibility)
    {
        Context.Checkpoint();
        using var depth = Context.EnterScope(PdfResourceKind.Depth);
        resources = _graph.Resolve(resources);
        if (resources.Kind != PdfValueKind.Null && resources.Kind != PdfValueKind.Dictionary)
            throw Error("Resources", "Content resources must be a dictionary.");
        State state = initial;
        var graphics = new Stack<State>();
        var marked = new Stack<(bool Visible, Actual? Previous, bool Own, int First, string? Uri)>();
        PdfMatrix text = PdfMatrix.Identity, line = PdfMatrix.Identity;
        bool visible = initialVisibility;
        foreach (var operation in program.Operations)
        {
            Context.Checkpoint();
            Context.Consume(PdfResourceKind.Objects, 1);
            int ordinal = _operation++;
            var values = operation.Operands;
            double Number(int i) => PdfExtractionData.Number(values[i], _graph);
            switch (operation.Operation)
            {
                case PdfOperator.SaveState:
                    Context.Consume(PdfResourceKind.ExpandedBytes, 256);
                    graphics.Push(state.Copy());
                    break;
                case PdfOperator.RestoreState:
                    state = graphics.Pop();
                    break;
                case PdfOperator.Transform:
                    state.Ctm = state.Ctm.Multiply(Matrix(values));
                    break;
                case PdfOperator.BeginText:
                    text = line = PdfMatrix.Identity;
                    break;
                case PdfOperator.Font:
                    state.Font = Font(resources, values[0].NameValue);
                    state.Size = Number(1);
                    break;
                case PdfOperator.CharacterSpacing:
                    state.CharacterSpacing = Number(0);
                    break;
                case PdfOperator.WordSpacing:
                    state.WordSpacing = Number(0);
                    break;
                case PdfOperator.HorizontalScale:
                    state.Horizontal = Number(0) / 100;
                    break;
                case PdfOperator.Leading:
                    state.Leading = Number(0);
                    break;
                case PdfOperator.TextRise:
                    state.Rise = Number(0);
                    break;
                case PdfOperator.TextRenderingMode:
                    if (!values[0].TryGetInteger(out long mode) || mode < 0 || mode > 7)
                        throw Error("TextMode", "Text rendering mode must be in 0 through 7.");
                    state.Mode = (int)mode;
                    break;
                case PdfOperator.TextMatrix:
                    text = line = Matrix(values);
                    break;
                case PdfOperator.MoveText:
                    Move(Number(0), Number(1));
                    break;
                case PdfOperator.MoveTextLeading:
                    state.Leading = -Number(1);
                    Move(Number(0), Number(1));
                    break;
                case PdfOperator.NextTextLine:
                    Move(0, -state.Leading);
                    break;
                case PdfOperator.ShowText:
                    Show(values[0], state, visible, ordinal, ref text);
                    break;
                case PdfOperator.ShowPositionedText:
                    foreach (var item in values[0].Items)
                    {
                        Context.Checkpoint();
                        if (item.Kind == PdfValueKind.String)
                            Show(item, state, visible, ordinal, ref text);
                        else
                        {
                            if (state.Font == null)
                                throw Error("FontRequired", "A TJ displacement requires a selected font.");
                            double displacement = -PdfExtractionData.Number(item, _graph) / 1000 * state.Size;
                            text = text.Multiply(Translate(state.Font.Vertical ? 0 : displacement * state.Horizontal, state.Font.Vertical ? displacement : 0));
                        }
                    }

                    break;
                case PdfOperator.SpacedNextLineShowText:
                    state.WordSpacing = Number(0);
                    state.CharacterSpacing = Number(1);
                    Move(0, -state.Leading);
                    Show(values[2], state, visible, ordinal, ref text);
                    break;
                case PdfOperator.NextLineShowText:
                    Move(0, -state.Leading);
                    Show(values[0], state, visible, ordinal, ref text);
                    break;
                case PdfOperator.ExtendedState:
                    Extended(resources, values[0].NameValue, state);
                    break;
                case PdfOperator.BeginMarkedContent:
                case PdfOperator.BeginMarkedContentProperties:
                    var property = operation.Operation == PdfOperator.BeginMarkedContentProperties ? Property(resources, values[1]) : PdfValue.Null;
                    var resolved = _graph.Resolve(property);
                    bool old = visible;
                    if (values[0].NameValue == "OC" && Optional(resolved))
                        visible &= _layers.Visible(property);
                    bool owns = false;
                    var previous = _actual;
                    if (_actual == null && resolved.Kind == PdfValueKind.Dictionary && resolved.Entries.TryGetValue("ActualText", out var replacement))
                    {
                        Context.Consume(PdfResourceKind.ExpandedBytes, 96);
                        _actual = new(PdfTextStrings.Decode(_graph.Resolve(replacement), Context), _page.Characters.Count, _totalCodes, visible || _options.IncludeHiddenLayers);
                        owns = true;
                    }

                    Context.Consume(PdfResourceKind.ExpandedBytes, 64);
                    string? sourceUri = null;
                    if (_collectSourceLinks && values[0].NameValue == "Link" && resolved.Kind == PdfValueKind.Dictionary && resolved.Entries.TryGetValue("FPLinkURI", out var uriValue))
                        sourceUri = PdfTextStrings.Decode(_graph.Resolve(uriValue), Context);
                    marked.Push((old, previous, owns, _page.Characters.Count, sourceUri));
                    break;
                case PdfOperator.EndMarkedContent:
                    var frame = marked.Pop();
                    if (frame.Uri != null)
                    {
                        Context.Consume(PdfResourceKind.ExpandedBytes, 96);
                        Context.Consume(PdfResourceKind.Objects, 1);
                        _page.SourceLinks.Add((frame.First, _page.Characters.Count - frame.First, frame.Uri));
                    }

                    if (frame.Own)
                        CloseActual();
                    _actual = frame.Previous;
                    visible = frame.Visible;
                    break;
                case PdfOperator.DrawObject:
                    Draw(resources, values[0].NameValue, state, visible);
                    break;
            }

            if (operation.Operation == PdfOperator.InlineImage || operation.Operation == PdfOperator.Shading || operation.Operation == PdfOperator.Fill || operation.Operation == PdfOperator.LegacyFill || operation.Operation == PdfOperator.FillEvenOdd || operation.Operation == PdfOperator.Stroke || operation.Operation == PdfOperator.CloseStroke || operation.Operation == PdfOperator.FillStroke || operation.Operation == PdfOperator.FillStrokeEvenOdd || operation.Operation == PdfOperator.CloseFillStroke || operation.Operation == PdfOperator.CloseFillStrokeEvenOdd)
                NotePainting(visible || _options.IncludeHiddenLayers);
        }

        void Move(double x, double y)
        {
            text = line = line.Multiply(Translate(x, y));
        }
    }

    private void Show(PdfValue encoded, State state, bool visible, int ordinal, ref PdfMatrix text)
    {
        var font = state.Font ?? throw Error("FontRequired", "Text showing requires a selected font.");
        if (encoded.Kind != PdfValueKind.String)
            throw Error("TextString", "A text-showing operand must be a byte string.");
        bool keep = (visible || _options.IncludeHiddenLayers) && (_options.IncludeInvisibleText || state.Mode != 3 && state.Mode != 7);
        Context.Consume(PdfResourceKind.InputBytes, encoded.StringBytes.Count);
        int at = 0;
        while (at < encoded.StringBytes.Count)
        {
            Context.Checkpoint();
            int byteOffset = at;
            var decoded = font.Decode(encoded.StringBytes, ref at, keep);
            var metrics = font.Metrics(decoded.Code);
            _totalCodes++;
            NotePainting(keep);
            double advance = metrics.Width / 1000 * state.Size;
            var origin = state.Ctm.Multiply(text);
            var transform = origin.Multiply(new PdfMatrix(state.Size * state.Horizontal / 1000, 0, 0, state.Size / 1000, 0, state.Rise));
            if (keep)
            {
                font.RequireGeometry();
                Context.Consume(PdfResourceKind.Objects, 1);
                Context.Consume(PdfResourceKind.ExpandedBytes, 512 + decoded.Text.Length * 4L);
                var rectangle = new PdfRect(Math.Min(0, metrics.Width) - metrics.OriginX, font.Descent - metrics.OriginY, Math.Abs(metrics.Width), font.Ascent - font.Descent);
                var user = PdfGeometryMath.Transform(rectangle, transform);
                var quad = PdfGeometryMath.Transform(rectangle, _display.Multiply(transform));
                var start = _display.Multiply(origin).Transform(new(0, state.Rise));
                var end = _display.Multiply(origin).Transform(new(font.Vertical ? 0 : advance * state.Horizontal, state.Rise + (font.Vertical ? metrics.AdvanceY / 1000 * state.Size : 0)));
                var emOrigin = _display.Multiply(origin).Transform(new(0, 0));
                var emTop = _display.Multiply(origin).Transform(new(0, state.Size));
                double size = Distance(emOrigin, emTop);
                _page.Characters.Add(new(_page.Characters.Count, ordinal, decoded.Code, decoded.Length, decoded.Text, decoded.Confidence, quad, user, start, end, font.Name, size, state.Mode, visible, font.Estimated || font.WidthWasEstimated) { EncodedString = encoded, ByteOffset = byteOffset, AdvanceTJ = (font.Vertical ? metrics.AdvanceY : metrics.Width) + 1000 * (state.CharacterSpacing + (decoded.Length == 1 && decoded.Code == 32 ? state.WordSpacing : 0)) / state.Size, Vertical = font.Vertical });
            }

            double spacing = state.CharacterSpacing + (decoded.Length == 1 && decoded.Code == 32 ? state.WordSpacing : 0);
            text = text.Multiply(font.Vertical ? Translate(0, metrics.AdvanceY / 1000 * state.Size + spacing) : Translate((advance + spacing) * state.Horizontal, 0));
        }
    }

    private PdfExtractionFont Font(PdfValue resources, string name)
    {
        var fonts = _graph.Get(resources, "Font");
        if (fonts.Kind != PdfValueKind.Dictionary || !fonts.Entries.TryGetValue(name, out var raw))
            throw Error("FontMissing", "A selected font resource is missing.");
        var dictionary = _graph.Resolve(raw);
        if (_fonts.TryGetValue(dictionary, out var existing))
            return existing;
        Context.Consume(PdfResourceKind.ExpandedBytes, 192);
        var font = new PdfExtractionFont(dictionary, _graph, _providers);
        _fonts.Add(dictionary, font);
        return font;
    }

    private void Extended(PdfValue resources, string name, State state)
    {
        var all = _graph.Get(resources, "ExtGState");
        if (all.Kind != PdfValueKind.Dictionary || !all.Entries.TryGetValue(name, out var raw))
            throw Error("GraphicsStateMissing", "A selected extended graphics state is missing.");
        var dictionary = _graph.Resolve(raw);
        if (dictionary.Kind != PdfValueKind.Dictionary)
            throw Error("GraphicsState", "An extended graphics state must be a dictionary.");
        var value = _graph.Get(dictionary, "Font");
        if (value.Kind == PdfValueKind.Null)
            return;
        if (value.Kind != PdfValueKind.Array || value.Items.Count != 2)
            throw Error("GraphicsStateFont", "An extended Font requires a font dictionary and size.");
        var fontValue = _graph.Resolve(value.Items[0]);
        if (!_fonts.TryGetValue(fontValue, out var font))
        {
            Context.Consume(PdfResourceKind.ExpandedBytes, 192);
            font = new(fontValue, _graph, _providers);
            _fonts.Add(fontValue, font);
        }

        state.Font = font;
        state.Size = PdfExtractionData.Number(_graph.Resolve(value.Items[1]), _graph);
    }

    private void Draw(PdfValue resources, string name, State state, bool visible)
    {
        var objects = _graph.Get(resources, "XObject");
        if (objects.Kind != PdfValueKind.Dictionary || !objects.Entries.TryGetValue(name, out var raw))
            throw Error("XObjectMissing", "A Do operator names a missing XObject.");
        var stream = _graph.Resolve(raw);
        if (stream.Kind != PdfValueKind.Stream)
            throw Error("XObject", "A Do operator requires a stream.");
        if (stream.Entries.TryGetValue("OC", out var membership))
            visible &= _layers.Visible(membership);
        string subtype = _layers.Name(stream, "Subtype");
        if (subtype == "Image")
        {
            NotePainting(visible || _options.IncludeHiddenLayers);
            return;
        }

        if (subtype != "Form")
            throw Error("XObjectProfile", "The XObject subtype requires an explicit text extraction profile.");
        Context.Consume(PdfResourceKind.ExpandedBytes, 384);
        if (!_formPath.Add(stream))
            throw Error("FormCycle", "Recursive Forms require a finite content expansion profile.");
        try
        {
            using var depth = Context.EnterScope(PdfResourceKind.Depth);
            var own = stream.Entries.TryGetValue("Resources", out var declared) ? _graph.Resolve(declared) : resources;
            var program = PdfContentProgram.ParseCore(PdfExtractionData.Read(stream, _graph, _providers), PdfContentKind.Page, PdfUnknownOperatorPolicy.Reject, null, Context, color => PdfImageColorResolver.Components(color, own, _graph.Resolve, Context));
            var nested = state.Copy();
            var matrix = _graph.Get(stream, "Matrix");
            if (matrix.Kind != PdfValueKind.Null)
            {
                if (matrix.Kind != PdfValueKind.Array || matrix.Items.Count != 6)
                    throw Error("FormMatrix", "A Form Matrix must contain six numbers.");
                nested.Ctm = nested.Ctm.Multiply(Matrix(matrix.Items));
            }

            Execute(program, own, nested, visible);
        }
        finally
        {
            _formPath.Remove(stream);
        }
    }

    private void CloseActual()
    {
        var actual = _actual!;
        int count = _page.Characters.Count - actual.First, total = _totalCodes - actual.Total;
        if (count != total || actual.ExcludedPainting > 0 || !actual.InitiallySelected)
        {
            if (count != 0 || actual.SelectedPainting > 0)
                Context.Report(new("Extraction.ActualTextPartialVisibility", "An ActualText replacement covers selected and excluded content; a faithful partial replacement is unavailable.", lossCategory: PdfLossCategory.FidelityReduction));
            return;
        }

        Context.Consume(PdfResourceKind.ExpandedBytes, 96);
        PdfRect? bounds = null;
        for (int i = actual.First; i < _page.Characters.Count; i++)
            bounds = bounds.HasValue ? Union(bounds.Value, _page.Characters[i].Quad.Bounds) : _page.Characters[i].Quad.Bounds;
        _page.Spans.Add(new(actual.First, count, actual.Text, bounds));
    }

    private void NotePainting(bool selected)
    {
        if (_actual == null)
            return;
        if (selected)
            _actual.SelectedPainting++;
        else
            _actual.ExcludedPainting++;
    }

    private PdfValue Property(PdfValue resources, PdfValue value)
    {
        if (value.Kind != PdfValueKind.Name)
            return value;
        var properties = _graph.Get(resources, "Properties");
        if (properties.Kind != PdfValueKind.Dictionary || !properties.Entries.TryGetValue(value.NameValue, out var property))
            throw Error("PropertyMissing", "A marked-content property resource is missing.");
        return property;
    }

    private bool Optional(PdfValue value) => value.Kind == PdfValueKind.Dictionary && (_layers.Name(value, "Type") == "OCG" || _layers.Name(value, "Type") == "OCMD");
    private PdfMatrix Matrix(IReadOnlyList<PdfValue> values) => new(PdfExtractionData.Number(_graph.Resolve(values[0]), _graph), PdfExtractionData.Number(_graph.Resolve(values[1]), _graph), PdfExtractionData.Number(_graph.Resolve(values[2]), _graph), PdfExtractionData.Number(_graph.Resolve(values[3]), _graph), PdfExtractionData.Number(_graph.Resolve(values[4]), _graph), PdfExtractionData.Number(_graph.Resolve(values[5]), _graph));
    private static PdfMatrix Translate(double x, double y) => new(1, 0, 0, 1, x, y);
    internal static double Distance(PdfPoint a, PdfPoint b)
    {
        double x = a.X - b.X, y = a.Y - b.Y;
        double scale = Math.Max(Math.Abs(x), Math.Abs(y));
        double result = scale == 0 ? 0 : scale * Math.Sqrt((x / scale) * (x / scale) + (y / scale) * (y / scale));
        PdfGraphicsNumbers.Finite(result);
        return result;
    }

    internal static PdfRect Union(PdfRect a, PdfRect b)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y);
        return new(x, y, Math.Max(a.Right, b.Right) - x, Math.Max(a.Top, b.Top) - y);
    }

    private PdfException Error(string code, string message) => _graph.Error("Extraction." + code, message);
}
