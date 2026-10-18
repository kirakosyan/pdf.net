using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>A bounded synchronous canvas builder. Use only on its callback thread; completed drawings and placements are immutable.</summary>
public sealed class PdfCanvas
{
    private readonly PdfOperationContext _context;
    private readonly int _thread = Thread.CurrentThread.ManagedThreadId;
    private bool _active = true;
    private PdfMatrix _transform = PdfMatrix.Identity;
    private readonly List<PdfCanvasPlacement> _placements = new();
    private readonly List<PdfCanvasClip> _clips = new();
    private readonly Stack<ScopeState> _scopes = new();
    private readonly List<PdfGraphicsResource> _createdResources = new();
    private readonly List<PdfDrawing> _createdDrawings = new();
    private readonly List<PdfFontResource> _createdFonts = new();
    private PdfCanvas(PdfPageGeometry geometry, PdfCanvasOptions options, PdfOperationContext context)
    {
        Geometry = geometry;
        Options = options;
        _context = context;
        var displayed = geometry.DisplayBounds(options.Box);
        double points = options.PointsPerUnit, width = displayed.Width / points - options.Margins.Left - options.Margins.Right, height = displayed.Height / points - options.Margins.Top - options.Margins.Bottom;
        if (width <= 0 || height <= 0)
            throw Error("Canvas.ContentFrame", "Canvas margins require a positive displayed content frame.");
        Bounds = new(0, 0, width, height);
        var origin = options.Origin == PdfCanvasOrigin.TopLeft ? new PdfMatrix(points, 0, 0, points, options.Margins.Left * points, options.Margins.Top * points) : new PdfMatrix(points, 0, 0, -points, options.Margins.Left * points, displayed.Height - options.Margins.Bottom * points);
        ToUser = geometry.DisplayToUser(options.Box).Multiply(origin);
        if (options.ClipToContent)
            AddClip(RectanglePath(Bounds), false, PdfMatrix.Identity);
    }

    /// <summary>Gets the captured immutable page geometry.</summary>
    public PdfPageGeometry Geometry { get; }
    /// <summary>Gets captured explicit coordinate settings.</summary>
    public PdfCanvasOptions Options { get; }
    /// <summary>Gets the margin content frame in canvas units.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets the base canvas-to-native-PDF transform, including viewing rotation, origin, margins and UserUnit.</summary>
    public PdfMatrix ToUser { get; }

    /// <summary>Creates a complete isolated canvas under one shared operation context; no page mutation occurs inside the callback.</summary>
    public static PdfCanvasResult Create(PdfPageGeometry geometry, Action<PdfCanvas> draw, PdfCanvasOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (geometry == null)
            throw new ArgumentNullException(nameof(geometry));
        if (draw == null)
            throw new ArgumentNullException(nameof(draw));
        options ??= new();
        PdfCanvasResult? result = null;
        Action<PdfOperationReport>? finish = null;
        var report = PdfFont.Run("CreateCanvas", "managed.canvas", policy, cancellationToken, context => result = CreateCore(geometry, draw, options, context, out finish), PdfCapability.WriteDocument);
        finish!(report);
        return result!;
    }

    internal static PdfCanvasResult CreateCore(PdfPageGeometry geometry, Action<PdfCanvas> draw, PdfCanvasOptions options, PdfOperationContext context, out Action<PdfOperationReport> finish)
    {
        var canvas = new PdfCanvas(geometry, options, context);
        PdfCanvasResult result;
        try
        {
            draw(canvas);
            canvas.Check();
            if (canvas._scopes.Count != 0)
                throw canvas.Error("Canvas.UnbalancedScope", "Every canvas scope must close before the callback returns.");
            result = canvas.Finish();
        }
        finally
        {
            canvas._active = false;
            while (canvas._scopes.Count > 0)
                canvas._scopes.Pop().Lease.Dispose();
        }

        finish = report =>
        {
            result.OperationReport = report;
            result.Drawing.OperationReport = report;
            result.Drawing.Content.OperationReport = report;
            foreach (var resource in canvas._createdResources)
                resource.OperationReport = report;
            foreach (var drawing in canvas._createdDrawings)
            {
                drawing.OperationReport = report;
                drawing.Content.OperationReport = report;
            }

            foreach (var font in canvas._createdFonts)
                font.OperationReport = report;
        };
        return result;
    }

    /// <summary>Captures transforms and clips until its scope closes in reverse order.</summary>
    public IDisposable Save()
    {
        Check();
        _context.Consume(PdfResourceKind.ExpandedBytes, _clips.Count * 16L + 256);
        var state = new ScopeState(_transform, _clips.ToArray(), _context.EnterScope(PdfResourceKind.Depth));
        _scopes.Push(state);
        return new CanvasScope(this, state);
    }

    /// <summary>Concatenates a local transform on the right of the current transform.</summary>
    public void Transform(PdfMatrix matrix)
    {
        Check();
        _transform = _transform.Multiply(matrix);
    }

    /// <summary>Translates in current canvas units.</summary>
    public void Translate(double x, double y) => Transform(new(1, 0, 0, 1, x, y));
    /// <summary>Scales current local axes; singular transforms remain representable and have no invertible hit frame.</summary>
    public void Scale(double x, double y) => Transform(new(x, 0, 0, y, 0, 0));
    /// <summary>Rotates local axes; positive angles turn toward positive local y from positive local x.</summary>
    public void Rotate(double degrees)
    {
        PdfGraphicsNumbers.Finite(degrees);
        double angle = (degrees % 360 + 360) % 360;
        double cos = angle == 0 ? 1 : angle == 180 ? -1 : angle == 90 || angle == 270 ? 0 : Math.Cos(angle * Math.PI / 180), sin = angle == 90 ? 1 : angle == 270 ? -1 : angle == 0 || angle == 180 ? 0 : Math.Sin(angle * Math.PI / 180);
        Transform(new(cos, sin, -sin, cos, 0, 0));
    }

    /// <summary>Intersects subsequent paint layers with an implicitly closed path captured at the current transform.</summary>
    public void Clip(PdfPath path, bool evenOdd = false)
    {
        Check();
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        AddClip(path, evenOdd, _transform);
    }

    /// <summary>Paints an exact vector path using explicit fill/stroke data and records its conservative frame.</summary>
    public PdfCanvasPlacement DrawPath(PdfPath path, PdfCanvasStyle? style = null, int zIndex = 0)
    {
        Check();
        if (path == null)
            throw new ArgumentNullException(nameof(path));
        style ??= PdfCanvasStyle.Default;
        var operations = new List<PdfContentOperation>();
        var resources = new List<PdfResourceBinding>();
        _context.Consume(PdfResourceKind.ExpandedBytes, path.Operations.Count * 32L + 8192);
        Paint(style.Fill, false, operations, resources);
        Paint(style.Stroke, true, operations, resources);
        if (style.State != null)
        {
            resources.Add(new("GS", style.State.Resource));
            operations.Add(new(PdfOperator.ExtendedState, PdfValue.Name("GS")));
        }

        if (style.Stroke != null)
        {
            operations.Add(new(PdfOperator.LineWidth, PdfValues.Real(style.LineWidth)));
            operations.Add(new(PdfOperator.LineCap, PdfValue.Integer((int)style.LineCap)));
            operations.Add(new(PdfOperator.LineJoin, PdfValue.Integer((int)style.LineJoin)));
            operations.Add(new(PdfOperator.MiterLimit, PdfValues.Real(style.MiterLimit)));
            operations.Add(new(PdfOperator.Dash, PdfGraphicsNumbers.Array(style.Dash.ToArray()), PdfValues.Real(style.DashPhase)));
        }

        operations.AddRange(path.Operations);
        operations.Add(new(style.Fill != null ? style.Stroke != null ? style.EvenOdd ? PdfOperator.FillStrokeEvenOdd : PdfOperator.FillStroke : style.EvenOdd ? PdfOperator.FillEvenOdd : PdfOperator.Fill : PdfOperator.Stroke));
        var drawing = Drawing(operations, new(resources.ToArray()));
        var bounds = PdfPathGeometry.Bounds(path, _context);
        double pad = style.Stroke == null ? 0 : (style.LineWidth == 0 ? 1 / Options.PointsPerUnit : style.LineWidth / 2) * Math.Max(style.LineJoin == PdfLineJoin.Miter ? style.MiterLimit : 1, style.LineCap == PdfLineCap.Square ? Math.Sqrt(2) : 1);
        if (pad > 0)
            bounds = PdfGeometryMath.Expand(bounds, pad);
        var formBounds = SafeFormBounds(bounds);
        try
        {
            var page = PdfGeometryMath.Transform(Geometry.Boxes.Media, ToUser.Multiply(_transform).Invert()).Bounds;
            formBounds = Union(formBounds, page);
        }
        catch (InvalidOperationException)
        {
        }

        var form = Form(drawing, formBounds);
        return Add(form, bounds, _transform, PdfMatrix.Identity, zIndex, _clips);
    }

    /// <summary>Fits a reusable vector drawing into an explicit canvas destination, retaining its upright appearance.</summary>
    public PdfCanvasPlacement DrawDrawing(PdfDrawing drawing, PdfRect sourceBounds, PdfRect destination, PdfCanvasFit fit = PdfCanvasFit.Contain, PdfCanvasAnchor anchor = PdfCanvasAnchor.Center, int zIndex = 0)
    {
        Check();
        if (drawing == null)
            throw new ArgumentNullException(nameof(drawing));
        var form = Form(drawing, sourceBounds);
        return Place(form, sourceBounds, PdfMatrix.Identity, destination, sourceBounds.Width / sourceBounds.Height, fit, anchor, zIndex);
    }

    /// <summary>Fits an explicit Form XObject into a canvas destination, including its own form matrix.</summary>
    public PdfCanvasPlacement DrawForm(PdfGraphicsResource form, PdfRect destination, PdfCanvasFit fit = PdfCanvasFit.Contain, PdfCanvasAnchor anchor = PdfCanvasAnchor.Center, int zIndex = 0)
    {
        Check();
        RequireObject(form, "Form");
        var definition = form.Definition;
        var bounds = ReadRect(definition, "BBox");
        var matrix = definition.Entries.TryGetValue("Matrix", out var encoded) ? ReadMatrix(encoded) : PdfMatrix.Identity;
        var transformed = PdfGeometryMath.Transform(bounds, matrix).Bounds;
        if (transformed.Width <= 0 || transformed.Height <= 0)
            throw Error("Canvas.FormFrame", "A fitted form requires a nondegenerate transformed frame.");
        return Place(form, bounds, matrix, destination, transformed.Width / transformed.Height, fit, anchor, zIndex);
    }

    /// <summary>Fits an explicit image XObject using its declared pixel aspect. Image decoding/encoding belongs to the image service.</summary>
    public PdfCanvasPlacement DrawImage(PdfGraphicsResource image, PdfRect destination, PdfCanvasFit fit = PdfCanvasFit.Contain, PdfCanvasAnchor anchor = PdfCanvasAnchor.Center, int zIndex = 0)
    {
        Check();
        RequireObject(image, "Image");
        long width = Integer(image.Definition, "Width"), height = Integer(image.Definition, "Height");
        if (width < 1 || height < 1 || width > int.MaxValue || height > int.MaxValue)
            throw Error("Canvas.ImageGeometry", "Image placement requires positive bounded integer pixel dimensions.");
        return Place(image, new(0, 0, 1, 1), PdfMatrix.Identity, destination, (double)width / height, fit, anchor, zIndex);
    }

    /// <summary>Places immutable shaped text at a baseline in canvas units. Font size remains in typographic points.</summary>
    public PdfCanvasPlacement DrawText(PdfShapedText text, PdfPoint baseline, double fontSizePoints, double lineHeight = 1.2, bool subset = true, int zIndex = 0)
    {
        Check();
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        PdfGraphicsNumbers.Finite(fontSizePoints);
        PdfGraphicsNumbers.Finite(lineHeight);
        if (fontSizePoints <= 0 || lineHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(fontSizePoints));
        var textDrawing = PdfTextDrawing.CreateCore(text, 0, 0, fontSizePoints, lineHeight, "FT", subset, _context);
        _context.Consume(PdfResourceKind.ExpandedBytes, textDrawing.Fonts.Count * 256L + 128);
        var bindings = new List<PdfResourceBinding>();
        foreach (var pair in textDrawing.Fonts)
        {
            _createdFonts.Add(pair.Value);
            bindings.Add(new(pair.Key, PdfGraphicsResource.Font(pair.Value)));
        }

        var drawing = Drawing(textDrawing.Content.Operations, new(bindings.ToArray()));
        var bounds = TextBounds(text, fontSizePoints, lineHeight);
        var form = Form(drawing, SafeFormBounds(bounds));
        double scale = 1 / Options.PointsPerUnit;
        var local = new PdfMatrix(scale, 0, 0, Options.Origin == PdfCanvasOrigin.TopLeft ? -scale : scale, baseline.X, baseline.Y);
        return Add(form, bounds, _transform.Multiply(local), PdfMatrix.Identity, zIndex, _clips);
    }

    private PdfCanvasPlacement Place(PdfGraphicsResource resource, PdfRect localBounds, PdfMatrix ownMatrix, PdfRect destination, double aspect, PdfCanvasFit fit, PdfCanvasAnchor anchor, int zIndex)
    {
        if (!Enum.IsDefined(typeof(PdfCanvasFit), fit) || !Enum.IsDefined(typeof(PdfCanvasAnchor), anchor))
            throw new ArgumentOutOfRangeException(nameof(fit));
        var source = PdfGeometryMath.Transform(localBounds, ownMatrix).Bounds;
        if (source.Width <= 0 || source.Height <= 0)
            throw Error("Canvas.ObjectFrame", "A placed object requires positive source dimensions.");
        double width = destination.Width, height = destination.Height;
        if (fit != PdfCanvasFit.Stretch)
        {
            if (aspect <= 0 || double.IsInfinity(aspect) || double.IsNaN(aspect))
                throw Error("Canvas.ObjectFrame", "A source aspect ratio must be finite and positive.");
            if ((fit == PdfCanvasFit.Contain) == (width / Math.Max(height, double.Epsilon) > aspect))
                width = height * aspect;
            else
                height = width / aspect;
        }

        double ax = ((int)anchor % 3) / 2d, ay = ((int)anchor / 3) / 2d;
        if (Options.Origin == PdfCanvasOrigin.BottomLeft)
            ay = 1 - ay;
        var target = new PdfRect(destination.X + (destination.Width - width) * ax, destination.Y + (destination.Height - height) * ay, width, height);
        double sx = target.Width / source.Width, sy = target.Height / source.Height;
        var map = Options.Origin == PdfCanvasOrigin.TopLeft ? new PdfMatrix(sx, 0, 0, -sy, target.X - source.X * sx, target.Top + source.Y * sy) : new PdfMatrix(sx, 0, 0, sy, target.X - source.X * sx, target.Y - source.Y * sy);
        _context.Consume(PdfResourceKind.ExpandedBytes, (_clips.Count + 1) * 32L + 128);
        var clips = new List<PdfCanvasClip>(_clips);
        if (fit == PdfCanvasFit.Cover)
            clips.Add(CreateClip(RectanglePath(destination), false, _transform));
        return Add(resource, localBounds, _transform.Multiply(map), ownMatrix, zIndex, clips);
    }

    private PdfCanvasPlacement Add(PdfGraphicsResource resource, PdfRect bounds, PdfMatrix matrix, PdfMatrix own, int z, IReadOnlyList<PdfCanvasClip> clips)
    {
        Check();
        _context.Consume(PdfResourceKind.Objects, 1);
        _context.Consume(PdfResourceKind.ExpandedBytes, clips.Count * 32L + 512);
        var placement = new PdfCanvasPlacement(_placements.Count, z, resource, bounds, matrix, own, ToUser, Geometry, clips.ToArray());
        _placements.Add(placement);
        return placement;
    }

    private void Paint(PdfPaint? paint, bool stroke, List<PdfContentOperation> operations, List<PdfResourceBinding> resources)
    {
        if (paint == null)
            return;
        string name = stroke ? "SC" : "FC";
        var space = paint.Pattern == null ? paint.Color!.ColorSpace : PdfColorSpace.Pattern(paint.Pattern.Colored ? null : paint.Color!.ColorSpace);
        var resource = PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.ColorSpace, space.Definition, space.MinimumVersion, PdfResourceSet.Empty, _context);
        Track(resource);
        resources.Add(new(name, resource));
        operations.Add(new(stroke ? PdfOperator.StrokeColorSpace : PdfOperator.FillColorSpace, PdfValue.Name(name)));
        var operands = paint.Color?.Components.Select(PdfValues.Real).ToList() ?? new List<PdfValue>();
        if (paint.Pattern != null)
        {
            string pattern = stroke ? "SP" : "FP";
            resources.Add(new(pattern, paint.Pattern.Resource));
            operands.Add(PdfValue.Name(pattern));
        }

        operations.Add(new(stroke ? PdfOperator.StrokeColorExtended : PdfOperator.FillColorExtended, operands.ToArray()));
    }

    private PdfDrawing Drawing(IEnumerable<PdfContentOperation> operations, PdfResourceSet resources)
    {
        _context.Consume(PdfResourceKind.ExpandedBytes, 128);
        var drawing = PdfDrawing.CreateCore(operations, resources, _context);
        _createdDrawings.Add(drawing);
        return drawing;
    }

    private PdfGraphicsResource Form(PdfDrawing drawing, PdfRect bounds)
    {
        var resource = PdfGraphicsObjects.FormCore(drawing, bounds, null, null, _context);
        Track(resource);
        return resource;
    }

    private void Track(PdfGraphicsResource resource)
    {
        _context.Consume(PdfResourceKind.ExpandedBytes, 64);
        _createdResources.Add(resource);
    }

    private PdfPath RectanglePath(PdfRect bounds)
    {
        _context.Consume(PdfResourceKind.ExpandedBytes, 1024);
        return PdfPath.RectangleCore(bounds, _context);
    }

    private void AddClip(PdfPath path, bool evenOdd, PdfMatrix matrix)
    {
        _context.Consume(PdfResourceKind.ExpandedBytes, 128);
        _clips.Add(CreateClip(path, evenOdd, matrix));
    }

    private PdfCanvasClip CreateClip(PdfPath path, bool evenOdd, PdfMatrix matrix) => new(path, evenOdd, matrix, PdfPathGeometry.Bounds(path, _context), PdfPathGeometry.Transform(path, ToUser.Multiply(matrix), _context));
    private PdfCanvasResult Finish()
    {
        _context.Consume(PdfResourceKind.ExpandedBytes, _placements.Count * 128L + 512);
        var ordered = _placements.OrderBy(x => x.ZIndex).ThenBy(x => x.Index).ToArray();
        var drawings = new List<PdfGraphicsResource>();
        for (int offset = 0; offset < ordered.Length; offset += 4096)
        {
            int count = Math.Min(4096, ordered.Length - offset);
            _context.Consume(PdfResourceKind.ExpandedBytes, count * 512L + 256);
            var resources = new List<PdfResourceBinding>();
            var operations = new List<PdfContentOperation>();
            for (int i = 0; i < count; i++)
            {
                var p = ordered[offset + i];
                string name = "E" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
                resources.Add(new(name, p.Resource));
                operations.Add(new(PdfOperator.SaveState));
                foreach (var clip in p.Clips)
                {
                    operations.AddRange(clip.RawOperations);
                    operations.Add(new(clip.EvenOdd ? PdfOperator.ClipEvenOdd : PdfOperator.Clip));
                    operations.Add(new(PdfOperator.EndPath));
                }

                operations.Add(new(PdfOperator.Transform, p.UserEmission.Value.Items.ToArray()));
                operations.Add(new(PdfOperator.DrawObject, PdfValue.Name(name)));
                operations.Add(new(PdfOperator.RestoreState));
            }

            drawings.Add(Form(Drawing(operations, new(resources.ToArray())), Geometry.Boxes.Media));
        }

        PdfDrawing root;
        if (drawings.Count == 0)
            root = Drawing(System.Array.Empty<PdfContentOperation>(), PdfResourceSet.Empty);
        else
        {
            while (drawings.Count > 4096)
            {
                var next = new List<PdfGraphicsResource>();
                for (int offset = 0; offset < drawings.Count; offset += 4096)
                    next.Add(Form(Batch(drawings.Skip(offset).Take(4096).ToArray()), Geometry.Boxes.Media));
                drawings = next;
            }

            root = Batch(drawings.ToArray());
        }

        return new(Geometry, Options, Bounds, ToUser, root, ordered);
    }

    private PdfDrawing Batch(PdfGraphicsResource[] forms)
    {
        _context.Consume(PdfResourceKind.ExpandedBytes, forms.Length * 256L + 128);
        var resources = new List<PdfResourceBinding>();
        var operations = new List<PdfContentOperation>();
        for (int i = 0; i < forms.Length; i++)
        {
            string name = "B" + i.ToString(System.Globalization.CultureInfo.InvariantCulture);
            resources.Add(new(name, forms[i]));
            operations.Add(new(PdfOperator.DrawObject, PdfValue.Name(name)));
        }

        return Drawing(operations, new(resources.ToArray()));
    }

    private void Check()
    {
        if (!_active)
            throw new InvalidOperationException("The canvas builder is usable only during its creation callback.");
        if (Thread.CurrentThread.ManagedThreadId != _thread)
            throw Error("Canvas.ThreadAccess", "A canvas callback must serialize operations on its creating thread.");
        _context.Checkpoint();
    }

    private PdfException Error(string code, string message) => PdfWriterCore.Error(_context, code, message);
    private void RequireObject(PdfGraphicsResource resource, string subtype)
    {
        if (resource == null)
            throw new ArgumentNullException(nameof(resource));
        if (resource.Category != PdfGraphicsResourceCategory.XObject || resource.Definition.Kind != PdfValueKind.Stream || !resource.Definition.Entries.TryGetValue("Subtype", out var kind) || kind.Kind != PdfValueKind.Name || kind.NameValue != subtype)
            throw Error("Canvas.ObjectType", "The placement requires the exact declared XObject subtype.");
    }

    private long Integer(PdfValue value, string key)
    {
        if (!value.Entries.TryGetValue(key, out var number) || number.Kind != PdfValueKind.Number)
            throw Error("Canvas.ImageGeometry", "Image dimensions must be integer declarations.");
        try
        {
            return number.IntegerValue;
        }
        catch (System.IO.InvalidDataException)
        {
            throw Error("Canvas.ImageGeometry", "Image dimensions must be bounded integer declarations.");
        }
    }

    private PdfRect ReadRect(PdfValue value, string key)
    {
        if (!value.Entries.TryGetValue(key, out var box) || box.Kind != PdfValueKind.Array || box.Items.Count != 4)
            throw Error("Canvas.FormFrame", "A form requires four finite BBox bounds.");
        try
        {
            double x = box.Items[0].NumberValue, y = box.Items[1].NumberValue, right = box.Items[2].NumberValue, top = box.Items[3].NumberValue;
            return new(Math.Min(x, right), Math.Min(y, top), Math.Abs(right - x), Math.Abs(top - y));
        }
        catch (InvalidOperationException)
        {
            throw Error("Canvas.FormFrame", "Form bounds must be finite numbers.");
        }
    }

    private PdfMatrix ReadMatrix(PdfValue value)
    {
        if (value.Kind != PdfValueKind.Array || value.Items.Count != 6)
            throw Error("Canvas.FormFrame", "A form matrix requires six finite coefficients.");
        try
        {
            return new(value.Items[0].NumberValue, value.Items[1].NumberValue, value.Items[2].NumberValue, value.Items[3].NumberValue, value.Items[4].NumberValue, value.Items[5].NumberValue);
        }
        catch (InvalidOperationException)
        {
            throw Error("Canvas.FormFrame", "Form matrix coefficients must be finite numbers.");
        }
    }

    private static PdfRect SafeFormBounds(PdfRect bounds) => new(bounds.X, bounds.Y, Math.Max(bounds.Width, .000001), Math.Max(bounds.Height, .000001));
    private static PdfRect Union(PdfRect a, PdfRect b)
    {
        double x = Math.Min(a.X, b.X), y = Math.Min(a.Y, b.Y), right = Math.Max(a.Right, b.Right), top = Math.Max(a.Top, b.Top);
        return new(x, y, right - x, top - y);
    }

    private PdfRect TextBounds(PdfShapedText text, double size, double lineHeight)
    {
        bool any = false;
        PdfRect bounds = default;
        int paragraph = -1;
        double penX = 0, penY = 0;
        foreach (var run in text.Runs)
        {
            if (paragraph != run.ParagraphIndex)
            {
                paragraph = run.ParagraphIndex;
                penX = text.Options.Vertical ? -paragraph * size * lineHeight : 0;
                penY = text.Options.Vertical ? 0 : -paragraph * size * lineHeight;
            }

            var m = run.Request.Font.Info.Metrics;
            double scale = size / m.UnitsPerEm;
            foreach (var glyph in run.Glyphs)
            {
                _context.Checkpoint();
                var box = new PdfRect(penX + (glyph.OffsetX + m.Left) * scale, penY + (glyph.OffsetY + m.Bottom) * scale, (m.Right - (double)m.Left) * scale, (m.Top - (double)m.Bottom) * scale);
                bounds = any ? Union(bounds, box) : box;
                any = true;
                penX += glyph.AdvanceX * scale;
                penY += glyph.AdvanceY * scale;
            }
        }

        return bounds;
    }

    private sealed class ScopeState
    {
        internal ScopeState(PdfMatrix transform, PdfCanvasClip[] clips, IDisposable lease)
        {
            Transform = transform;
            Clips = clips;
            Lease = lease;
        }

        internal PdfMatrix Transform { get; }
        internal PdfCanvasClip[] Clips { get; }
        internal IDisposable Lease { get; }
    }

    private sealed class CanvasScope : IDisposable
    {
        private PdfCanvas? _canvas;
        private readonly ScopeState _state;
        internal CanvasScope(PdfCanvas canvas, ScopeState state)
        {
            _canvas = canvas;
            _state = state;
        }

        public void Dispose()
        {
            if (_canvas == null)
                return;
            var canvas = _canvas;
            if (!canvas._active)
            {
                _canvas = null;
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId != canvas._thread)
                throw canvas.Error("Canvas.ThreadAccess", "Canvas scopes must close on their callback thread.");
            if (canvas._scopes.Count == 0 || !ReferenceEquals(canvas._scopes.Peek(), _state))
                throw canvas.Error("Canvas.ScopeOrder", "Canvas scopes must close in reverse order.");
            canvas._scopes.Pop();
            canvas._transform = _state.Transform;
            canvas._clips.Clear();
            canvas._clips.AddRange(_state.Clips);
            _state.Lease.Dispose();
            _canvas = null;
            canvas._context.Checkpoint();
        }
    }
}
