using System.Globalization;
using System.IO;
using System.Threading;
namespace Fluent.Pdf.Formats;
/// <summary>Original non-executing static EPS drawing-data subset. Procedures, text/fonts/images, file/process operators and unknown commands reject.</summary>
public static partial class PdfStaticEps
{
    /// <summary>Imports one explicitly bounded DSC EPS page using literal numeric drawing/state operators only. No PostScript VM, host I/O, native interpreter or resource discovery executes.</summary>
    public static PdfDocument Import(Stream input, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead) throw new ArgumentException("A readable borrowed EPS input is required.", nameof(input)); PdfDocument? result = null; var finish = new List<Action<PdfOperationReport>>();
        try { var report = PdfFont.Run("ImportStaticEps", "managed.eps-data", policy, cancellationToken, c => {
            c.Consume(PdfResourceKind.Objects, 32); c.Consume(PdfResourceKind.ExpandedBytes, 16384); string text = FormatIo.Text(input, c); var box = Bounds(text, c); result = Pdf.Create(new(operationPolicy: c.Policy));
            try { FormatIo.Page(result, new(box.Width, box.Height), canvas => { canvas.Transform(new(1, 0, 0, -1, -box.X, box.Y + box.Height)); new EpsReader(text, c, finish).Draw(canvas); }, c, finish); }
            catch (Exception ex) when (ex is ArgumentException || ex is InvalidOperationException || ex is OverflowException) { c.Checkpoint(); throw Error(c, "Data", "Static EPS geometry/state exceeds the supported finite profile."); }
        }, PdfCapability.WriteDocument); foreach (var complete in finish) complete(report); result!.SetCompositionReport(report); return result; } catch { result?.Dispose(); throw; }
    }
    private static PdfRect Bounds(string text, PdfOperationContext c)
    {
        int at = 0, limit = Math.Min(text.Length, 65536); double[]? integer = null, precise = null; bool first = true, ended = false;
        while (at < limit)
        {
            c.Checkpoint(); int end = at; while (end < limit && text[end] != '\r' && text[end] != '\n') end++; int length = end - at; if (length > 2048) throw Error(c, "Header", "EPS headers require bounded literal DSC lines."); c.Consume(PdfResourceKind.Objects, 1); c.Consume(PdfResourceKind.ExpandedBytes, length * 16L + 256); string line = text.Substring(at, length); at = end; if (at < limit && text[at] == '\r') at++; if (at < limit && text[at] == '\n') at++;
            if (first) { if (line.TrimEnd() != "%!PS-Adobe-3.0 EPSF-3.0") throw Error(c, "Header", "An explicit DSC EPS header is required."); first = false; continue; }
            if (line.StartsWith("%%LanguageLevel:", StringComparison.Ordinal)) { string level = line.Substring("%%LanguageLevel:".Length).Trim(); if (level != "1" && level != "2") throw Error(c, "Header", "Only declared language levels1/2 are supported by this closed EPS data profile."); }
            if (line == "%%EndComments") { ended = true; break; } if (!line.StartsWith("%", StringComparison.Ordinal)) break;
            bool hires = line.StartsWith("%%HiResBoundingBox:", StringComparison.Ordinal); if (hires || line.StartsWith("%%BoundingBox:", StringComparison.Ordinal)) { if (hires ? precise != null : integer != null) throw Error(c, "Bounds", "Duplicate bounding-box declarations reject."); string raw = line.Substring(line.IndexOf(':') + 1); string[] values = raw.Split(new[] { ' ', '\t', '\0', '\f' }, StringSplitOptions.RemoveEmptyEntries); if (values.Length != 4) throw Error(c, "Bounds", "Four literal bounding coordinates are required; deferred boxes reject."); var box = values.Select(v => Scalar(v, c)).ToArray(); if (!hires && values.Any(v => !long.TryParse(v, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _))) throw Error(c, "Bounds", "The ordinary DSC bounding box requires integers."); if (hires) precise = box; else integer = box; }
        }
        if (!ended || integer == null) throw Error(c, "Bounds", "An initial integer bounding box and EndComments marker are required."); var b = precise ?? integer; if (b[2] <= b[0] || b[3] <= b[1] || b[2] - b[0] > 14400 || b[3] - b[1] > 14400 || b[0] < integer[0] || b[1] < integer[1] || b[2] > integer[2] || b[3] > integer[3]) throw Error(c, "Bounds", "A positive at-most14400-point precise box must fit its integer DSC box."); return new(b[0], b[1], b[2] - b[0], b[3] - b[1]);
    }
    private static double Scalar(string value, PdfOperationContext c) { if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || double.IsNaN(number) || double.IsInfinity(number) || Math.Abs(number) > 1000000000 || number != 0 && Math.Abs(number) < 1e-30) throw Error(c, "Number", "A finite literal numeric operand within the published range is required."); return number; }
    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Printer.StaticEps." + code, message);
    private sealed class Pen
    {
        internal PdfColor Color = new(PdfColorSpace.Gray, 0); internal double Width = 1, Miter = 10, Phase; internal PdfLineCap Cap; internal PdfLineJoin Join; internal double[] Dash = Array.Empty<double>(); internal bool StrokeAdjust = true; internal Pen Copy() => (Pen)MemberwiseClone();
        internal PdfCanvasStyle Style(bool fill, PdfOperationContext c, List<Action<PdfOperationReport>> finish, bool evenOdd = false)
        {
            PdfGraphicsState? state = !fill ? PdfGraphicsState.StrokeAdjustmentCore(StrokeAdjust, c) : null; if (state != null) finish.Add(report => state.Resource.OperationReport = report);
            return new(fill: fill ? PdfPaint.Solid(Color) : null, stroke: fill ? null : PdfPaint.Solid(Color), lineWidth: Width, lineCap: Cap, lineJoin: Join, miterLimit: Miter, dash: Dash, dashPhase: Phase, evenOdd: evenOdd, state: state);
        }
    }
    private sealed class EpsReader
    {
        private readonly string _text; private readonly PdfOperationContext _c; private readonly List<Action<PdfOperationReport>> _finish; private int _at; private readonly List<object> _operands = new();
        internal EpsReader(string text, PdfOperationContext c, List<Action<PdfOperationReport>> finish) { _text = text; _c = c; _finish = finish; }
        internal void Draw(PdfCanvas canvas)
        {
            var path = new List<PdfPathSegment>(); var saved = new Stack<(Pen Pen, IDisposable Scope)>(); var pen = new Pen(); double x = 0, y = 0, startX = 0, startY = 0; bool current = false, shown = false;
            void EmptyPath() { if (path.Count != 0) throw Error(_c, "PathState", "Transforms/state scopes inside an unfinished path require a broader interpreter."); }
            void ReserveSegment() { _c.Consume(PdfResourceKind.Objects, 1); _c.Consume(PdfResourceKind.ExpandedBytes, 1024); }
            void Add(PdfPathSegment segment) => path.Add(segment);
            void Clear() { path.Clear(); current = false; }
            double N() { if (_operands.Count == 0 || _operands[_operands.Count - 1] is not double number) throw Error(_c, "Operands", "The drawing operator requires literal numeric operands."); _operands.RemoveAt(_operands.Count - 1); return number; }
            double[] Array() { if (_operands.Count == 0 || _operands[_operands.Count - 1] is not double[] values) throw Error(_c, "Operands", "A literal numeric array is required."); _operands.RemoveAt(_operands.Count - 1); return values; }
            void Current() { if (!current) throw Error(_c, "CurrentPoint", "A path segment requires a current point."); }
            try
            {
                string? token;
                while ((token = Token()) != null)
                {
                    _c.Checkpoint(); if (shown && token != "grestore") throw Error(_c, "PageCount", "Static EPS accepts one final showpage only.");
                    if (token == "true" || token == "false") { Push(token == "true"); continue; }
                    if (token == "[") { var values = new List<double>(); string? item; while ((item = Token()) != "]") { if (item == null || item == "[" || values.Count == 64) throw Error(_c, "Array", "Only complete flat numeric arrays of at most64 items are supported."); _c.Consume(PdfResourceKind.ExpandedBytes, 64); values.Add(Scalar(item, _c)); } Push(values.ToArray()); continue; }
                    if (token.Length > 0 && (token[0] >= '0' && token[0] <= '9' || token[0] == '.' || token[0] == '+' || token[0] == '-')) { Push(Scalar(token, _c)); continue; }
                    switch (token)
                    {
                        case "newpath": Clear(); break;
                        case "moveto": case "rmoveto": { double yy = N(), xx = N(); if (token == "rmoveto") { Current(); xx += x; yy += y; } ReserveSegment(); Add(PdfPathSegment.Move(x = startX = xx, y = startY = yy)); current = true; break; }
                        case "lineto": case "rlineto": { Current(); double yy = N(), xx = N(); if (token == "rlineto") { xx += x; yy += y; } ReserveSegment(); Add(PdfPathSegment.Line(x = xx, y = yy)); break; }
                        case "curveto": case "rcurveto": { Current(); double y3 = N(), x3 = N(), y2 = N(), x2 = N(), y1 = N(), x1 = N(); if (token == "rcurveto") { x1 += x; x2 += x; x3 += x; y1 += y; y2 += y; y3 += y; } ReserveSegment(); Add(PdfPathSegment.Curve(x1, y1, x2, y2, x = x3, y = y3)); break; }
                        case "closepath": Current(); ReserveSegment(); Add(PdfPathSegment.Close()); x = startX; y = startY; break;
                        case "stroke": case "fill": case "eofill": if (path.Count > 0) canvas.DrawPath(PdfPath.CreateCore(path, _c), pen.Style(token != "stroke", _c, _finish, token == "eofill")); Clear(); break;
                        case "clip": case "eoclip": canvas.Clip(PdfPath.CreateCore(path, _c), token == "eoclip"); break;
                        case "gsave": EmptyPath(); saved.Push((pen.Copy(), canvas.Save())); break;
                        case "grestore": EmptyPath(); if (saved.Count == 0) throw Error(_c, "State", "Unbalanced graphics-state restoration rejects."); var old = saved.Pop(); old.Scope.Dispose(); pen = old.Pen; break;
                        case "translate": { EmptyPath(); double yy = N(), xx = N(); canvas.Translate(xx, yy); break; }
                        case "scale": { EmptyPath(); double yy = N(), xx = N(); if (xx == 0 || yy == 0) throw Error(_c, "Transform", "Singular EPS transforms require a broader profile."); canvas.Scale(xx, yy); break; }
                        case "rotate": EmptyPath(); canvas.Rotate(N()); break;
                        case "concat": { EmptyPath(); double[] a = Array(); if (a.Length != 6) throw Error(_c, "Transform", "A six-number affine array is required."); var matrix = new PdfMatrix(a[0], a[1], a[2], a[3], a[4], a[5]); if (matrix.A * matrix.D - matrix.B * matrix.C == 0) throw Error(_c, "Transform", "Singular EPS transforms require another profile."); canvas.Transform(matrix); break; }
                        case "setstrokeadjust": if (_operands.Count == 0 || _operands[_operands.Count - 1] is not bool adjust) throw Error(_c, "Operands", "Stroke adjustment requires a literal boolean."); _operands.RemoveAt(_operands.Count - 1); pen.StrokeAdjust = adjust; break;
                        case "setgray": { double value = N(); pen.Color = new(PdfColorSpace.Gray, value); break; }
                        case "setrgbcolor": { double b = N(), g = N(), r = N(); pen.Color = new(PdfColorSpace.Rgb, r, g, b); break; }
                        case "setcmykcolor": { double k = N(), yy = N(), m = N(), cc = N(); pen.Color = new(PdfColorSpace.Cmyk, cc, m, yy, k); break; }
                        case "setlinewidth": pen.Width = N(); if (pen.Width < 0) throw Error(_c, "LineStyle", "Negative line widths reject."); break;
                        case "setlinecap": { double cap = N(); if (cap < 0 || cap > 2 || cap != Math.Truncate(cap)) throw Error(_c, "LineStyle", "Line cap must be0,1 or2."); pen.Cap = (PdfLineCap)(int)cap; break; }
                        case "setlinejoin": { double join = N(); if (join < 0 || join > 2 || join != Math.Truncate(join)) throw Error(_c, "LineStyle", "Line join must be0,1 or2."); pen.Join = (PdfLineJoin)(int)join; break; }
                        case "setmiterlimit": pen.Miter = N(); if (pen.Miter < 1) throw Error(_c, "LineStyle", "Miter limits must be at least1."); break;
                        case "setdash": pen.Phase = N(); pen.Dash = Array(); if (pen.Dash.Any(value => value < 0) || pen.Dash.Length > 0 && pen.Dash.All(value => value == 0)) throw Error(_c, "LineStyle", "A dash pattern must be nonnegative and advance."); break;
                        case "showpage": EmptyPath(); if (_operands.Count != 0) throw Error(_c, "Operands", "Unconsumed operands reject at the page boundary."); shown = true; break;
                        default: throw Error(_c, "Operator", "The EPS program contains a command outside the closed non-executing drawing-data table.");
                    }
                }
                if (saved.Count != 0 || path.Count != 0 || _operands.Count != 0) throw Error(_c, "State", "Unfinished operands/path/graphics state cannot be silently omitted.");
            }
            finally { while (saved.Count > 0) saved.Pop().Scope.Dispose(); }
            void Push(object value) { if (_operands.Count == 1024) throw Error(_c, "Operands", "The numeric operand stack exceeded its finite profile."); _operands.Add(value); }
        }
        private static bool White(char value) => value == '\0' || value == '\t' || value == '\n' || value == '\f' || value == '\r' || value == ' ';
        private static readonly string[] ResourceDirectives = { "%%IncludeFile:", "%%IncludeResource:", "%%BeginDocument:", "%%BeginBinary:", "%%BeginData:" };
        private string? Token()
        {
            _c.Checkpoint(); while (_at < _text.Length)
            {
                if ((_at & 1023) == 0) _c.Checkpoint(); char ch = _text[_at]; if (White(ch)) { _at++; continue; }
                if (ch == '%') { int start = _at; while (_at < _text.Length && _text[_at] != '\n' && _text[_at] != '\r') { if ((_at & 1023) == 0) _c.Checkpoint(); _at++; } foreach (string directive in ResourceDirectives) if (_at - start >= directive.Length && string.CompareOrdinal(_text, start, directive, 0, directive.Length) == 0) throw Error(_c, "Resources", "External/embedded/binary DSC resources require another profile."); continue; } break;
            }
            if (_at == _text.Length) return null; int begin = _at; if (_text[_at] == '[' || _text[_at] == ']') _at++; else while (_at < _text.Length && !White(_text[_at]) && _text[_at] != '%' && _text[_at] != '[' && _text[_at] != ']') { if (++_at - begin > 128) throw Error(_c, "TokenExtent", "A literal EPS token exceeds128 characters."); }
            _c.Consume(PdfResourceKind.Objects, 1); _c.Consume(PdfResourceKind.ExpandedBytes, (_at - begin) * 4L + 128); return _text.Substring(begin, _at - begin);
        }
    }
}
