using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;

namespace Fluent.Pdf.Formats;
/// <summary>Explicit fonts/shaping and local embedded-image authority for the documented SVG profile. No font, URI or stylesheet discovery occurs.</summary>
public sealed class PdfSvgImportOptions
{
    /// <summary>Captures caller-selected fonts/text policy and optional bounded PNG/JPEG data-image authority. Font selection explicitly overrides source family names.</summary>
    public PdfSvgImportOptions(PdfFontFallback? fonts = null, PdfTextOptions? text = null, bool allowDataImages = false, PdfImageReadOptions? images = null)
    { Fonts = fonts; Text = text ?? new(); AllowDataImages = allowDataImages; Images = images ?? new(); }
    /// <summary>Gets explicit font bindings; text rejects if missing.</summary>
    public PdfFontFallback? Fonts { get; }
    /// <summary>Gets explicit text shaping policy.</summary>
    public PdfTextOptions Text { get; }
    /// <summary>Gets local data-image authority; network/file URLs always reject.</summary>
    public bool AllowDataImages { get; }
    /// <summary>Gets explicit PNG/JPEG color/orientation/lossless-conversion policy.</summary>
    public PdfImageReadOptions Images { get; }
}
/// <summary>Original bounded static SVG import and vector-page SVG export. Unsupported constructs reject without raster substitution.</summary>
public static partial class PdfSvg
{
    /// <summary>Imports opaque paths/rectangles/polylines, transforms/user-space clips, explicit black-fill shaped text and authorized PNG/JPEG data images. CSS/resources/scripts outside the documented matrix reject.</summary>
    public static PdfDocument Import(Stream input, PdfSvgImportOptions? options = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead) throw new ArgumentException("A readable borrowed SVG input is required.", nameof(input));
        options ??= new(); providers ??= PdfProviderRegistry.Default; PdfDocument? result = null; var finish = new List<Action<PdfOperationReport>>();
        try
        {
            var report = PdfFont.Run("ImportSvg", "managed.svg", policy, cancellationToken, c =>
            {
                try
                {
                    var xml = FormatIo.Xml(input, c); var root = xml.Root;
                    if (root == null || root.Name.LocalName != "svg" || root.Name.NamespaceName != "" && root.Name.NamespaceName != "http://www.w3.org/2000/svg") throw Error(c, "Root", "A static SVG root is required.");
                    var reader = new SvgReader(root, options, providers, c, finish);
                    double width = reader.Length(root.Attribute("width")?.Value ?? throw Error(c, "Size", "Explicit SVG width/height are required.")), height = reader.Length(root.Attribute("height")?.Value ?? throw Error(c, "Size", "Explicit SVG width/height are required."));
                    if (width <= 0 || height <= 0) throw Error(c, "Size", "Positive explicit SVG dimensions are required.");
                    var view = root.Attribute("viewBox") is XAttribute box ? reader.Numbers(box.Value) : new[] { 0d, 0d, width / .75, height / .75 };
                    if (view.Length != 4 || view[2] <= 0 || view[3] <= 0) throw Error(c, "ViewBox", "A positive finite four-number viewBox is required.");
                    string aspect = root.Attribute("preserveAspectRatio")?.Value ?? "xMidYMid meet";
                    if (aspect != "none" && aspect != "xMidYMid meet") throw Error(c, "Aspect", "Only explicit stretch or centered meet aspect is supported.");
                    double sx = width / view[2], sy = height / view[3]; if (aspect != "none") sx = sy = Math.Min(sx, sy);
                    var transform = new PdfMatrix(sx, 0, 0, sy, (width - view[2] * sx) / 2 - view[0] * sx, (height - view[3] * sy) / 2 - view[1] * sy);
                    result = Pdf.Create(new(operationPolicy: c.Policy));
                    FormatIo.Page(result, new(width, height), canvas => { canvas.Transform(transform); reader.Draw(canvas); }, c, finish);
                }
                catch (Exception failure) when (failure is ArgumentException || failure is InvalidOperationException || failure is OverflowException)
                {
                    c.Checkpoint(); throw Error(c, "Geometry", "SVG geometry/arithmetic exceeds the supported finite profile.");
                }
            }, PdfCapability.WriteDocument);
            foreach (var complete in finish) complete(report); result!.SetCompositionReport(report); return result;
        }
        catch { result?.Dispose(); throw; }
    }
    private static PdfException Error(PdfOperationContext c, string code, string text) => PdfWriterCore.Error(c, "Svg." + code, text);
    private sealed class Style
    {
        internal string Fill = "black", Stroke = "none", Rule = "nonzero", Cap = "butt", Join = "miter"; internal double Width = 1, Miter = 4, FontSize = 16, Phase; internal double[] Dash = Array.Empty<double>(); internal string Family = "";
        internal Style Copy() => (Style)MemberwiseClone();
    }
    private sealed class SvgReader
    {
        private readonly XElement _root; private readonly PdfSvgImportOptions _options; private readonly PdfProviderRegistry _providers; private readonly PdfOperationContext _c; private readonly List<Action<PdfOperationReport>> _finish;
        private readonly Dictionary<string, XElement> _clips = new(StringComparer.Ordinal);
        internal SvgReader(XElement root, PdfSvgImportOptions options, PdfProviderRegistry providers, PdfOperationContext c, List<Action<PdfOperationReport>> finish)
        {
            _root = root; _options = options; _providers = providers; _c = c; _finish = finish;
            foreach (var definition in root.Descendants().Where(e => e.Name.LocalName == "defs"))
            {
                Fields(definition, "id");
                foreach (var clip in definition.Elements())
                {
                    if (clip.Name.Namespace != root.Name.Namespace || clip.Name.LocalName != "clipPath") throw Error(c, "Definition", "Only local user-space clipPath definitions are supported.");
                    Fields(clip, "id", "clipPathUnits", "clip-rule");
                    if (clip.Attribute("clipPathUnits") is XAttribute units && units.Value != "userSpaceOnUse") throw Error(c, "ClipUnits", "Object-bounding-box clips require another profile.");
                    string id = clip.Attribute("id")?.Value ?? ""; c.Consume(PdfResourceKind.ExpandedBytes, id.Length * 2L + 128);
                    if (id.Length == 0 || id.Length > 128 || _clips.ContainsKey(id)) throw Error(c, "Identity", "Clip definitions require unique bounded IDs."); _clips.Add(id, clip);
                }
            }
        }
        internal double Length(string value)
        {
            string suffix = ""; int end = value.Length; while (end > 0 && char.IsLetter(value[end - 1])) end--; if (end < value.Length) suffix = value.Substring(end);
            double number = FormatIo.Number(value.Substring(0, end), _c);
            return suffix switch { "" or "px" => number * .75, "pt" => number, "in" => number * 72, "mm" => number * 72 / 25.4, "cm" => number * 72 / 2.54, _ => throw Error(_c, "Units", "Only explicit px/pt/in/mm/cm SVG sizes are supported.") };
        }
        internal double[] Numbers(string value) => new SvgNumbers(value, _c).All();
        internal void Draw(PdfCanvas canvas) => Node(_root, canvas, new Style(), true);
        private void Fields(XElement e, params string[] allowed)
        {
            foreach (var a in e.Attributes())
            {
                _c.Checkpoint(); if (a.IsNamespaceDeclaration) continue;
                bool link = a.Name.LocalName == "href" && (a.Name.NamespaceName == "" || a.Name.NamespaceName == "http://www.w3.org/1999/xlink");
                if ((!link && a.Name.NamespaceName != "") || !allowed.Contains(a.Name.LocalName, StringComparer.Ordinal)) throw Error(_c, a.Name.LocalName.StartsWith("on", StringComparison.OrdinalIgnoreCase) ? "ActiveContent" : "Attribute", "The SVG attribute requires an unsupported behavior/style/resource profile.");
            }
        }
        private void Node(XElement e, PdfCanvas canvas, Style inherited, bool root = false)
        {
            _c.Checkpoint(); using var depth = _c.EnterScope(PdfResourceKind.Depth);
            if (e.Name.Namespace != _root.Name.Namespace) throw Error(_c, "Namespace", "SVG namespace extensions are unsupported.");
            string name = e.Name.LocalName;
            if (name == "script" || name == "foreignObject" || name == "animate" || name == "set") throw Error(_c, "ActiveContent", "SVG scripts/animation/foreign content are never executed.");
            var common = new[] { "id", "transform", "clip-path", "fill", "stroke", "stroke-width", "fill-rule", "stroke-linecap", "stroke-linejoin", "stroke-miterlimit", "stroke-dasharray", "stroke-dashoffset", "font-size", "font-family", "opacity", "fill-opacity", "stroke-opacity", "style" };
            string[] specific = root ? new[] { "width", "height", "viewBox", "preserveAspectRatio", "version", "overflow" } : name switch { "g" or "defs" => Array.Empty<string>(), "path" => new[] { "d" }, "rect" => new[] { "x", "y", "width", "height" }, "line" => new[] { "x1", "y1", "x2", "y2" }, "polyline" or "polygon" => new[] { "points" }, "text" => new[] { "x", "y" }, "image" => new[] { "x", "y", "width", "height", "href", "preserveAspectRatio" }, _ => throw Error(_c, "Element", "This SVG element requires another explicit profile.") };
            Fields(e, common.Concat(specific).ToArray());
            if (root && e.Attribute("overflow") is XAttribute overflow && overflow.Value != "hidden") throw Error(_c, "Overflow", "Only viewport-clipped SVG output is supported.");
            if (name == "defs") return;
            var style = inherited.Copy(); var attributes = e.Attributes().Where(a => !a.IsNamespaceDeclaration && a.Name.NamespaceName == "").ToDictionary(a => a.Name.LocalName, a => a.Value, StringComparer.Ordinal);
            if (attributes.TryGetValue("style", out var css))
            {
                if (css.Length > 4096) throw Error(_c, "StyleExtent", "Inline style exceeds its finite profile.");
                foreach (string declaration in css.Split(';'))
                {
                    if (string.IsNullOrWhiteSpace(declaration)) continue; _c.Consume(PdfResourceKind.Objects, 1); int colon = declaration.IndexOf(':');
                    if (colon <= 0) throw Error(_c, "Style", "A closed inline SVG style declaration is required."); string key = declaration.Substring(0, colon).Trim();
                    if (!common.Contains(key, StringComparer.Ordinal) || key == "style" || key == "id" || key == "transform" || key == "clip-path") throw Error(_c, "Style", "CSS selectors, variables and unsupported properties reject."); attributes[key] = declaration.Substring(colon + 1).Trim();
                }
            }
            foreach (var pair in attributes)
            switch (pair.Key)
            {
                case "fill": style.Fill = pair.Value.Trim().ToLowerInvariant(); break; case "stroke": style.Stroke = pair.Value.Trim().ToLowerInvariant(); break; case "fill-rule": style.Rule = pair.Value; break; case "stroke-linecap": style.Cap = pair.Value; break; case "stroke-linejoin": style.Join = pair.Value; break;
                case "stroke-dasharray": style.Dash = pair.Value == "none" ? Array.Empty<double>() : Numbers(pair.Value); if (style.Dash.Length > 64 || style.Dash.Any(value => value < 0) || style.Dash.Length > 0 && style.Dash.All(value => value == 0)) throw Error(_c, "Stroke", "A finite nonnegative nonzero dash pattern is required."); break;
                case "stroke-dashoffset": style.Phase = Value(pair.Value); break;
                case "stroke-width": style.Width = Value(pair.Value); break; case "stroke-miterlimit": style.Miter = Value(pair.Value); break; case "font-size": style.FontSize = Value(pair.Value); break; case "font-family": style.Family = pair.Value; break;
                case "opacity": case "fill-opacity": case "stroke-opacity": if (Value(pair.Value) != 1) throw Error(_c, "Opacity", "This SVG source profile requires opaque paint."); break;
            }
            using var scope = canvas.Save();
            if (attributes.TryGetValue("transform", out var matrix)) Transform(canvas, matrix);
            if (attributes.TryGetValue("clip-path", out var reference))
            {
                if (!reference.StartsWith("url(#", StringComparison.Ordinal) || !reference.EndsWith(")", StringComparison.Ordinal) || !_clips.TryGetValue(reference.Substring(5, reference.Length - 6), out var clip)) throw Error(_c, "ClipReference", "Clips must refer to an exact local definition.");
                var segments = new List<PdfPathSegment>(); string? clipRule = null;
                foreach (var child in clip.Elements())
                {
                    string childRule = child.Attribute("clip-rule")?.Value ?? clip.Attribute("clip-rule")?.Value ?? "nonzero";
                    if (clipRule != null && childRule != clipRule) throw Error(_c, "ClipRule", "Mixed child clip rules require another profile.");
                    clipRule = childRule; segments.AddRange(Path(child, true).Segments);
                }
                canvas.Clip(PdfPath.CreateCore(segments, _c), Rule(clipRule ?? "nonzero"));
            }
            if (root || name == "g") { foreach (var child in e.Elements()) Node(child, canvas, style); return; }
            if (e.HasElements) throw Error(_c, "Children", "Nested text/images/path effects require another explicit profile.");
            if (name == "text")
            {
                if (style.Stroke != "none" || style.Fill != "black" && style.Fill != "#000" && style.Fill != "#000000" && style.Fill != "none") throw Error(_c, "TextPaint", "This text profile supports explicit black fill without stroke.");
                if (style.Fill == "none") return;
                if (style.FontSize <= 0) throw Error(_c, "FontSize", "This SVG text profile requires a positive font size.");
                if (_options.Fonts == null) throw Error(_c, "Fonts", "SVG text requires caller-selected font bindings; host fonts are never discovered.");
                var shaped = PdfText.ShapeCore(e.Value, _options.Fonts, _providers.GetRequired<IPdfTextShaper>(PdfCapability.ShapeText), _options.Text, _c);
                _finish.Add(r => { shaped.OperationReport = r; shaped.Unicode.OperationReport = r; }); canvas.DrawText(shaped, new(Get(e, "x"), Get(e, "y")), style.FontSize); return;
            }
            if (name == "image") { Image(e, canvas); return; }
            var fill = Paint(style.Fill); var stroke = Paint(style.Stroke); if (name == "line") fill = null;
            if (style.Width == 0) stroke = null;
            if (fill == null && stroke == null) return;
            var cap = style.Cap switch { "butt" => PdfLineCap.Butt, "round" => PdfLineCap.Round, "square" => PdfLineCap.Square, _ => throw Error(_c, "LineCap", "Unknown SVG line cap.") };
            var join = style.Join switch { "miter" => PdfLineJoin.Miter, "round" => PdfLineJoin.Round, "bevel" => PdfLineJoin.Bevel, _ => throw Error(_c, "LineJoin", "Unknown SVG line join.") };
            if (style.Width < 0 || style.Miter < 1) throw Error(_c, "Stroke", "SVG stroke geometry exceeds the supported profile.");
            canvas.DrawPath(Path(e), new(fill, stroke, style.Width, cap, join, style.Miter, dash: style.Dash, dashPhase: style.Phase, evenOdd: Rule(style.Rule)));
        }
        private bool Rule(string value) => value == "evenodd" ? true : value == "nonzero" ? false : throw Error(_c, "FillRule", "Unknown SVG fill rule.");
        private double Value(string text) { if (text.EndsWith("px", StringComparison.Ordinal)) text = text.Substring(0, text.Length - 2); return FormatIo.Number(text, _c); }
        private double Get(XElement e, string name, double fallback = 0) => e.Attribute(name) is XAttribute a ? Value(a.Value) : fallback;
        private PdfPaint? Paint(string value)
        {
            if (value == "none") return null;
            value = value switch { "black" => "#000000", "white" => "#ffffff", "red" => "#ff0000", "green" => "#008000", "blue" => "#0000ff", _ => value };
            if (value.StartsWith("rgb(", StringComparison.Ordinal) && value.EndsWith(")", StringComparison.Ordinal))
            {
                var parts = value.Substring(4, value.Length - 5).Split(',');
                if (parts.Length != 3) throw Error(_c, "Color", "Exactly three RGB components are required.");
                var components = parts.Select(part => { string token = part.Trim(); return token.EndsWith("%", StringComparison.Ordinal) ? FormatIo.Number(token.Substring(0, token.Length - 1), _c) / 100 : FormatIo.Number(token, _c) / 255; }).ToArray();
                if (components.Any(component => component < 0 || component > 1)) throw Error(_c, "Color", "RGB components must be in their finite unit range.");
                return PdfPaint.Solid(new(PdfColorSpace.Rgb, components));
            }
            if (value.Length == 4 && value[0] == '#') value = "#" + value[1] + value[1] + value[2] + value[2] + value[3] + value[3];
            if (value.Length != 7 || value[0] != '#') throw Error(_c, "Color", "Only explicit opaque RGB hex or the documented basic color names are supported.");
            try { return PdfPaint.Solid(new(PdfColorSpace.Rgb, Convert.ToByte(value.Substring(1, 2), 16) / 255d, Convert.ToByte(value.Substring(3, 2), 16) / 255d, Convert.ToByte(value.Substring(5, 2), 16) / 255d)); }
            catch (FormatException) { throw Error(_c, "Color", "RGB color syntax is invalid."); }
        }
        private void Image(XElement e, PdfCanvas canvas)
        {
            var links = e.Attributes().Where(a => a.Name.LocalName == "href").ToArray();
            if (links.Length != 1) throw Error(_c, "ImageResource", "One unambiguous local image reference is required.");
            string href = links[0].Value;
            if (!_options.AllowDataImages) throw Error(_c, "ImageAuthority", "Embedded data images require explicit caller authority.");
            int comma = href.IndexOf(','); if (comma < 0 || href.Substring(0, comma) != "data:image/png;base64" && href.Substring(0, comma) != "data:image/jpeg;base64") throw Error(_c, "ImageResource", "Only local PNG/JPEG data images are supported; external URLs and files reject.");
            _c.Consume(PdfResourceKind.ExpandedBytes, href.Length * 2L + 256); byte[] raw;
            try { raw = Convert.FromBase64String(href.Substring(comma + 1)); } catch (FormatException) { throw Error(_c, "ImageData", "Embedded image base64 is invalid."); }
            using var source = new MemoryStream(raw, false); var bytes = PdfByteStore.Read(source, _c); var image = PdfImages.ReadCore(bytes, _options.Images, _c); _finish.Add(r => { PdfImages.Complete(image, r); if (image.SoftMask != null) PdfImages.Complete(image.SoftMask, r); });
            double width = Get(e, "width"), height = Get(e, "height"); if (width <= 0 || height <= 0) throw Error(_c, "ImageSize", "SVG images require positive explicit dimensions.");
            string aspect = e.Attribute("preserveAspectRatio")?.Value ?? "xMidYMid meet"; if (aspect != "none" && aspect != "xMidYMid meet") throw Error(_c, "Aspect", "Unsupported SVG image aspect policy.");
            canvas.DrawImageAsset(image, new(Get(e, "x"), Get(e, "y"), width, height), aspect == "none" ? PdfCanvasFit.Stretch : PdfCanvasFit.Contain);
        }
        private void Transform(PdfCanvas canvas, string source) => Transforms(source, canvas.Transform);
        private void Transforms(string source, Action<PdfMatrix> apply)
        {
            int at = 0;
            while (at < source.Length)
            {
                _c.Checkpoint(); while (at < source.Length && (char.IsWhiteSpace(source[at]) || source[at] == ',')) at++; if (at == source.Length) break;
                int nameStart = at; while (at < source.Length && char.IsLetter(source[at])) at++; string name = source.Substring(nameStart, at - nameStart); while (at < source.Length && char.IsWhiteSpace(source[at])) at++;
                if (at == source.Length || source[at++] != '(') throw Error(_c, "Transform", "A finite SVG transform list is required."); int end = source.IndexOf(')', at); if (end < 0) throw Error(_c, "Transform", "SVG transform is truncated.");
                var values = Numbers(source.Substring(at, end - at)); at = end + 1;
                if (name == "matrix" && values.Length == 6) apply(new(values[0], values[1], values[2], values[3], values[4], values[5]));
                else if (name == "translate" && (values.Length == 1 || values.Length == 2)) apply(new(1, 0, 0, 1, values[0], values.Length == 1 ? 0 : values[1]));
                else if (name == "scale" && (values.Length == 1 || values.Length == 2)) apply(new(values[0], 0, 0, values.Length == 1 ? values[0] : values[1], 0, 0));
                else if (name == "rotate" && (values.Length == 1 || values.Length == 3)) { if (values.Length == 3) apply(new(1, 0, 0, 1, values[1], values[2])); double angle = values[0] * Math.PI / 180; apply(new(Math.Cos(angle), Math.Sin(angle), -Math.Sin(angle), Math.Cos(angle), 0, 0)); if (values.Length == 3) apply(new(1, 0, 0, 1, -values[1], -values[2])); }
                else throw Error(_c, "Transform", "Only finite matrix/translate/scale/rotate transforms are supported.");
            }
        }
        private PdfPath Path(XElement e, bool clip = false)
        {
            if (e.Name.Namespace != _root.Name.Namespace) throw Error(_c, "Namespace", "Clip/path namespace extensions reject.");
            string name = e.Name.LocalName;
            if (clip)
            {
                string[] fields = name switch { "path" => new[] { "d", "id", "transform", "clip-rule" }, "rect" => new[] { "x", "y", "width", "height", "id", "transform", "clip-rule" }, "line" => new[] { "x1", "y1", "x2", "y2", "id", "transform", "clip-rule" }, "polygon" or "polyline" => new[] { "points", "id", "transform", "clip-rule" }, _ => throw Error(_c, "ClipGeometry", "Unsupported clip geometry.") };
                Fields(e, fields);
            }
            var segments = new List<PdfPathSegment>();
            if (name == "rect")
            { double w = Get(e, "width"), h = Get(e, "height"); if (w <= 0 || h <= 0) throw Error(_c, "PathGeometry", "Positive rectangle dimensions are required."); segments.Add(PdfPathSegment.Rectangle(new(Get(e, "x"), Get(e, "y"), w, h))); }
            else if (name == "line") { segments.Add(PdfPathSegment.Move(Get(e, "x1"), Get(e, "y1"))); segments.Add(PdfPathSegment.Line(Get(e, "x2"), Get(e, "y2"))); }
            else if (name == "polygon" || name == "polyline")
            { var points = Numbers(e.Attribute("points")?.Value ?? ""); if (points.Length < 4 || points.Length % 2 != 0) throw Error(_c, "PathGeometry", "At least two finite point pairs are required."); for (int i = 0; i < points.Length; i += 2) segments.Add(i == 0 ? PdfPathSegment.Move(points[i], points[i + 1]) : PdfPathSegment.Line(points[i], points[i + 1])); if (name == "polygon") segments.Add(PdfPathSegment.Close()); }
            else if (name == "path") segments.AddRange(new SvgNumbers(e.Attribute("d")?.Value ?? "", _c).Path().Segments);
            else throw Error(_c, "ClipGeometry", "Only explicit path/rect/line/polygon/polyline clip geometry is supported.");
            if (clip && e.HasElements) throw Error(_c, "ClipGeometry", "Nested clip effects require another profile.");
            if (clip && e.Attribute("transform") is XAttribute declaration)
            {
                var matrix = PdfMatrix.Identity; Transforms(declaration.Value, next => matrix = matrix.Multiply(next)); var transformed = new List<PdfPathSegment>();
                foreach (var segment in segments)
                {
                    _c.Consume(PdfResourceKind.ExpandedBytes, 512); var v = segment.Coordinates; PdfPoint P(int index) => matrix.Transform(new(v[index], v[index + 1]));
                    if (segment.Operation == PdfOperator.ClosePath) transformed.Add(PdfPathSegment.Close());
                    else if (segment.Operation == PdfOperator.Rectangle)
                    {
                        var a = matrix.Transform(new(v[0], v[1])); var b = matrix.Transform(new(v[0] + v[2], v[1])); var d = matrix.Transform(new(v[0], v[1] + v[3])); var c = matrix.Transform(new(v[0] + v[2], v[1] + v[3]));
                        transformed.Add(PdfPathSegment.Move(a.X, a.Y)); transformed.Add(PdfPathSegment.Line(b.X, b.Y)); transformed.Add(PdfPathSegment.Line(c.X, c.Y)); transformed.Add(PdfPathSegment.Line(d.X, d.Y)); transformed.Add(PdfPathSegment.Close());
                    }
                    else if (segment.Operation == PdfOperator.CurveTo) { var a = P(0); var b = P(2); var c = P(4); transformed.Add(PdfPathSegment.Curve(a.X, a.Y, b.X, b.Y, c.X, c.Y)); }
                    else { var point = P(0); transformed.Add(segment.Operation == PdfOperator.MoveTo ? PdfPathSegment.Move(point.X, point.Y) : PdfPathSegment.Line(point.X, point.Y)); }
                }
                segments = transformed;
            }
            return PdfPath.CreateCore(segments, _c);
        }
    }
    private sealed class SvgNumbers
    {
        private readonly string _source; private readonly PdfOperationContext _c; private int _at;
        internal SvgNumbers(string source, PdfOperationContext c) { _source = source; _c = c; }
        private void Space() { while (_at < _source.Length && (char.IsWhiteSpace(_source[_at]) || _source[_at] == ',')) _at++; }
        private double Read()
        {
            Space(); _c.Checkpoint(); int start = _at; if (_at < _source.Length && (_source[_at] == '+' || _source[_at] == '-')) _at++;
            while (_at < _source.Length && char.IsDigit(_source[_at])) _at++; if (_at < _source.Length && _source[_at] == '.') { _at++; while (_at < _source.Length && char.IsDigit(_source[_at])) _at++; }
            if (_at < _source.Length && (_source[_at] == 'e' || _source[_at] == 'E')) { _at++; if (_at < _source.Length && (_source[_at] == '+' || _source[_at] == '-')) _at++; while (_at < _source.Length && char.IsDigit(_source[_at])) _at++; }
            if (_at == start) throw Error(_c, "PathNumber", "A finite SVG path number is required."); _c.Consume(PdfResourceKind.Objects, 1); _c.Consume(PdfResourceKind.ExpandedBytes, 64); return FormatIo.Number(_source.Substring(start, _at - start), _c);
        }
        internal double[] All() { var values = new List<double>(); Space(); while (_at < _source.Length) { values.Add(Read()); Space(); } return values.ToArray(); }
        internal PdfPath Path()
        {
            var result = new List<PdfPathSegment>(); char command = '\0'; double x = 0, y = 0, startX = 0, startY = 0; bool current = false;
            while (true)
            {
                Space(); if (_at == _source.Length) break; _c.Checkpoint();
                if (char.IsLetter(_source[_at])) command = _source[_at++]; else if (command == '\0') throw Error(_c, "PathCommand", "An explicit SVG path command is required.");
                bool relative = char.IsLower(command); char op = char.ToUpperInvariant(command);
                if (!current && op != 'M') throw Error(_c, "PathCurrent", "SVG paths begin with moveto.");
                double X() => Read() + (relative ? x : 0); double Y() => Read() + (relative ? y : 0);
                if (op == 'Z') { result.Add(PdfPathSegment.Close()); x = startX; y = startY; command = '\0'; continue; }
                if (op == 'M' || op == 'L') { double nx = X(), ny = Y(); result.Add(op == 'M' ? PdfPathSegment.Move(nx, ny) : PdfPathSegment.Line(nx, ny)); x = nx; y = ny; if (op == 'M') { startX = x; startY = y; current = true; command = relative ? 'l' : 'L'; } }
                else if (op == 'H') { x = X(); result.Add(PdfPathSegment.Line(x, y)); }
                else if (op == 'V') { y = Y(); result.Add(PdfPathSegment.Line(x, y)); }
                else if (op == 'C') { double x1 = X(), y1 = Y(), x2 = X(), y2 = Y(), nx = X(), ny = Y(); result.Add(PdfPathSegment.Curve(x1, y1, x2, y2, nx, ny)); x = nx; y = ny; }
                else throw Error(_c, "PathCommand", "Only M/L/H/V/C/Z path commands are supported; arcs/quadratic/smooth paths require another profile.");
            }
            if (result.Count == 0) throw Error(_c, "PathGeometry", "A nonempty SVG path is required."); return PdfPath.CreateCore(result, _c);
        }
    }
}
