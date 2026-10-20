using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace Fluent.Pdf.Formats;
public static partial class PdfSvg
{
    /// <summary>Exports the documented opaque RGB/Gray vector page/Form profile as editable SVG. Text, images, annotations, layers, patterns and unsupported effects reject; no raster substitution occurs.</summary>
    public static PdfOperationReport WritePage(PdfDocumentSnapshot source, int pageIndex, Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed SVG output is required.", nameof(output));
        return Export(source, pageIndex, output, null, policy, cancellationToken);
    }

    /// <summary>Stages vector SVG before atomic file replacement.</summary>
    public static PdfOperationReport WritePageFile(PdfDocumentSnapshot source, int pageIndex, string path, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        return Export(source, pageIndex, null, path, policy, cancellationToken);
    }

    private static PdfOperationReport Export(PdfDocumentSnapshot source, int pageIndex, Stream? output, string? path, PdfOperationPolicy? policy, CancellationToken token)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        return PdfFont.Run("ExportVectorSvg", "managed.svg", policy ?? source.Options.OperationPolicy, token, c =>
        {
            var bytes = BuildVectorPage(source, pageIndex, c);
            if (path == null)
                FormatIo.Copy(bytes, output!, c);
            else
                PdfAtomicFile.Write(path, c, destination => FormatIo.Copy(bytes, destination, c));
        }, PdfCapability.ReadDocument);
    }

    internal static PdfByteStore BuildVectorPage(PdfDocumentSnapshot source, int pageIndex, PdfOperationContext c, string diagnosticPrefix = "Svg")
    {
        using var graph = new PdfGraphEdit(source, new(allowSignatureInvalidation: true), c);
        var binding = graph.Snapshot.Pages[pageIndex].Source!;
        var annotations = graph.Get(binding.Dictionary, "Annots");
        if (annotations.Kind != PdfValueKind.Null && (annotations.Kind != PdfValueKind.Array || annotations.Items.Count != 0))
            throw PdfWriterCore.Error(c, diagnosticPrefix + ".ExportAnnotations", "Annotation appearance/navigation export requires another profile.");
        var bounds = binding.Geometry.DisplayBounds();
        if (bounds.Width <= 0 || bounds.Height <= 0)
            throw PdfWriterCore.Error(c, diagnosticPrefix + ".ExportGeometry", "A nonempty displayed page is required.");
        using var buffer = new PdfCodecBufferStream(c, 67108864);
        using (var target = new BorrowedStream(buffer, false, c))
        using (var writer = FormatIo.Writer(target))
        {
            writer.WriteStartElement("svg", "http://www.w3.org/2000/svg");
            writer.WriteAttributeString("width", FormatIo.N(bounds.Width) + "pt");
            writer.WriteAttributeString("height", FormatIo.N(bounds.Height) + "pt");
            writer.WriteAttributeString("viewBox", "0 0 " + FormatIo.N(bounds.Width) + " " + FormatIo.N(bounds.Height));
            writer.WriteAttributeString("overflow", "hidden");
            var exporter = new SvgWriter(graph, writer, c, diagnosticPrefix);
            exporter.Program(PdfPageContent.Read(binding, PdfUnknownOperatorPolicy.Reject, null, PdfProviderRegistry.Default, c), graph.Get(binding.EffectiveAttributes, "Resources"), new VectorState { Matrix = binding.Geometry.UserToDisplay() });
            writer.WriteEndElement();
            writer.Flush();
        }

        return buffer.Finish();
    }

    private sealed class VectorState
    {
        internal PdfMatrix Matrix = PdfMatrix.Identity;
        internal string Fill = "rgb(0%,0%,0%)", Stroke = "rgb(0%,0%,0%)", Dash = "";
        internal int FillComponents = 1, StrokeComponents = 1, Cap, Join;
        internal double Width = 1, Miter = 10, Phase;
        internal VectorState Copy() => (VectorState)MemberwiseClone();
    }

    private sealed class SvgWriter
    {
        private readonly PdfGraphEdit _g;
        private readonly XmlWriter _w;
        private readonly PdfOperationContext _c;
        private readonly string _diagnosticPrefix;
        private int _clips;
        private readonly HashSet<PdfObjectId> _active = new();
        internal SvgWriter(PdfGraphEdit graph, XmlWriter writer, PdfOperationContext context, string diagnosticPrefix)
        {
            _g = graph;
            _w = writer;
            _c = context;
            _diagnosticPrefix = diagnosticPrefix;
        }

        private PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, _diagnosticPrefix + "." + code, message);
        private static string Matrix(PdfMatrix m) => "matrix(" + string.Join(" ", new[] { m.A, m.B, m.C, m.D, m.E, m.F }.Select(FormatIo.N)) + ")";
        private void Clip(string data, PdfMatrix matrix, bool evenOdd)
        {
            _c.Consume(PdfResourceKind.Objects, 1);
            string id = "clip" + (++_clips).ToString(System.Globalization.CultureInfo.InvariantCulture);
            _w.WriteStartElement("defs");
            _w.WriteStartElement("clipPath");
            _w.WriteAttributeString("id", id);
            _w.WriteAttributeString("clipPathUnits", "userSpaceOnUse");
            _w.WriteStartElement("path");
            _w.WriteAttributeString("d", data);
            _w.WriteAttributeString("transform", Matrix(matrix));
            _w.WriteAttributeString("clip-rule", evenOdd ? "evenodd" : "nonzero");
            _w.WriteEndElement();
            _w.WriteEndElement();
            _w.WriteEndElement();
            _w.WriteStartElement("g");
            _w.WriteAttributeString("clip-path", "url(#" + id + ")");
        }

        internal void Program(PdfContentProgram program, PdfValue resources, VectorState initial)
        {
            using var depth = _c.EnterScope(PdfResourceKind.Depth);
            _c.Consume(PdfResourceKind.ExpandedBytes, 1024);
            var state = initial.Copy();
            var stack = new Stack<(VectorState State, int Clips)>();
            var path = new StringBuilder();
            int openClips = 0;
            bool? clip = null;
            double x = 0, y = 0, startX = 0, startY = 0;
            foreach (var operation in program.Operations)
            {
                _c.Checkpoint();
                _c.Consume(PdfResourceKind.Objects, 1);
                _c.Consume(PdfResourceKind.ExpandedBytes, 2048);
                var a = operation.Operands;
                double N(int i) => a[i].NumberValue;
                void Segment(string command, params double[] values)
                {
                    path.Append(command);
                    foreach (double value in values)
                        path.Append(' ').Append(FormatIo.N(value));
                    path.Append(' ');
                }

                switch (operation.Operation)
                {
                    case PdfOperator.SaveState:
                        stack.Push((state.Copy(), openClips));
                        break;
                    case PdfOperator.RestoreState:
                        if (stack.Count == 0)
                            throw Error(_c, "ExportState", "Unbalanced vector graphics state.");
                        var saved = stack.Pop();
                        while (openClips > saved.Clips)
                        {
                            _w.WriteEndElement();
                            openClips--;
                        }

                        state = saved.State;
                        break;
                    case PdfOperator.Transform:
                        if (path.Length != 0)
                            throw Error(_c, "ExportPathTransform", "Transforms inside an unfinished path require another profile.");
                        state.Matrix = state.Matrix.Multiply(new(N(0), N(1), N(2), N(3), N(4), N(5)));
                        break;
                    case PdfOperator.MoveTo:
                        x = startX = N(0);
                        y = startY = N(1);
                        Segment("M", x, y);
                        break;
                    case PdfOperator.LineTo:
                        x = N(0);
                        y = N(1);
                        Segment("L", x, y);
                        break;
                    case PdfOperator.CurveTo:
                        x = N(4);
                        y = N(5);
                        Segment("C", N(0), N(1), N(2), N(3), x, y);
                        break;
                    case PdfOperator.CurveFromCurrent:
                        Segment("C", x, y, N(0), N(1), N(2), N(3));
                        x = N(2);
                        y = N(3);
                        break;
                    case PdfOperator.CurveToEndpoint:
                        x = N(2);
                        y = N(3);
                        Segment("C", N(0), N(1), x, y, x, y);
                        break;
                    case PdfOperator.ClosePath:
                        Segment("Z");
                        x = startX;
                        y = startY;
                        break;
                    case PdfOperator.Rectangle:
                        Segment("M", N(0), N(1));
                        Segment("L", N(0) + N(2), N(1));
                        Segment("L", N(0) + N(2), N(1) + N(3));
                        Segment("L", N(0), N(1) + N(3));
                        Segment("Z");
                        x = startX = N(0);
                        y = startY = N(1);
                        break;
                    case PdfOperator.Clip:
                        clip = false;
                        break;
                    case PdfOperator.ClipEvenOdd:
                        clip = true;
                        break;
                    case PdfOperator.Stroke:
                    case PdfOperator.CloseStroke:
                    case PdfOperator.Fill:
                    case PdfOperator.LegacyFill:
                    case PdfOperator.FillEvenOdd:
                    case PdfOperator.FillStroke:
                    case PdfOperator.FillStrokeEvenOdd:
                    case PdfOperator.CloseFillStroke:
                    case PdfOperator.CloseFillStrokeEvenOdd:
                    case PdfOperator.EndPath:
                        bool close = operation.Operation == PdfOperator.CloseStroke || operation.Operation == PdfOperator.CloseFillStroke || operation.Operation == PdfOperator.CloseFillStrokeEvenOdd;
                        if (close)
                            Segment("Z");
                        if (operation.Operation != PdfOperator.EndPath)
                        {
                            bool stroke = operation.Operation == PdfOperator.Stroke || operation.Operation == PdfOperator.CloseStroke || operation.Operation == PdfOperator.FillStroke || operation.Operation == PdfOperator.FillStrokeEvenOdd || operation.Operation == PdfOperator.CloseFillStroke || operation.Operation == PdfOperator.CloseFillStrokeEvenOdd;
                            bool fill = operation.Operation != PdfOperator.Stroke && operation.Operation != PdfOperator.CloseStroke;
                            bool evenOdd = operation.Operation == PdfOperator.FillEvenOdd || operation.Operation == PdfOperator.FillStrokeEvenOdd || operation.Operation == PdfOperator.CloseFillStrokeEvenOdd;
                            _w.WriteStartElement("path");
                            _w.WriteAttributeString("d", path.ToString());
                            _w.WriteAttributeString("transform", Matrix(state.Matrix));
                            _w.WriteAttributeString("fill", fill ? state.Fill : "none");
                            _w.WriteAttributeString("fill-rule", evenOdd ? "evenodd" : "nonzero");
                            _w.WriteAttributeString("stroke", stroke ? state.Stroke : "none");
                            if (stroke)
                            {
                                if (state.Width == 0)
                                    throw Error(_c, "ExportHairline", "Device-dependent PDF hairlines require another export profile.");
                                _w.WriteAttributeString("stroke-width", FormatIo.N(state.Width));
                                _w.WriteAttributeString("stroke-linecap", state.Cap == 0 ? "butt" : state.Cap == 1 ? "round" : "square");
                                _w.WriteAttributeString("stroke-linejoin", state.Join == 0 ? "miter" : state.Join == 1 ? "round" : "bevel");
                                _w.WriteAttributeString("stroke-miterlimit", FormatIo.N(state.Miter));
                                if (state.Dash.Length != 0)
                                {
                                    _w.WriteAttributeString("stroke-dasharray", state.Dash);
                                    _w.WriteAttributeString("stroke-dashoffset", FormatIo.N(state.Phase));
                                }
                            }

                            _w.WriteEndElement();
                        }

                        if (clip.HasValue)
                        {
                            Clip(path.ToString(), state.Matrix, clip.Value);
                            openClips++;
                            clip = null;
                        }

                        path.Clear();
                        break;
                    case PdfOperator.LineWidth:
                        state.Width = N(0);
                        break;
                    case PdfOperator.LineCap:
                        state.Cap = (int)N(0);
                        break;
                    case PdfOperator.LineJoin:
                        state.Join = (int)N(0);
                        break;
                    case PdfOperator.MiterLimit:
                        state.Miter = N(0);
                        break;
                    case PdfOperator.Dash:
                        state.Dash = string.Join(" ", a[0].Items.Select(v => FormatIo.N(v.NumberValue)));
                        state.Phase = N(1);
                        break;
                    case PdfOperator.FillGray:
                        state.Fill = Color(new[] { N(0) });
                        state.FillComponents = 1;
                        break;
                    case PdfOperator.StrokeGray:
                        state.Stroke = Color(new[] { N(0) });
                        state.StrokeComponents = 1;
                        break;
                    case PdfOperator.FillRgb:
                        state.Fill = Color(a.Select(v => v.NumberValue).ToArray());
                        state.FillComponents = 3;
                        break;
                    case PdfOperator.StrokeRgb:
                        state.Stroke = Color(a.Select(v => v.NumberValue).ToArray());
                        state.StrokeComponents = 3;
                        break;
                    case PdfOperator.FillColorSpace:
                        state.FillComponents = Components(a[0], resources);
                        state.Fill = "rgb(0%,0%,0%)";
                        break;
                    case PdfOperator.StrokeColorSpace:
                        state.StrokeComponents = Components(a[0], resources);
                        state.Stroke = "rgb(0%,0%,0%)";
                        break;
                    case PdfOperator.FillColor:
                    case PdfOperator.FillColorExtended:
                        if (a.Count != state.FillComponents || a.Any(v => v.Kind != PdfValueKind.Number))
                            throw Error(_c, "ExportColor", "Patterns or unsupported color components reject.");
                        state.Fill = Color(a.Select(v => v.NumberValue).ToArray());
                        break;
                    case PdfOperator.StrokeColor:
                    case PdfOperator.StrokeColorExtended:
                        if (a.Count != state.StrokeComponents || a.Any(v => v.Kind != PdfValueKind.Number))
                            throw Error(_c, "ExportColor", "Patterns or unsupported color components reject.");
                        state.Stroke = Color(a.Select(v => v.NumberValue).ToArray());
                        break;
                    case PdfOperator.ExtendedState:
                        var states = _g.Get(resources, "ExtGState");
                        if (states.Kind != PdfValueKind.Dictionary || !states.Entries.TryGetValue(a[0].NameValue, out var stateReference))
                            throw Error(_c, "ExportResource", "An invoked graphics-state resource is missing.");
                        var graphicsState = _g.Resolve(stateReference);
                        if (graphicsState.Kind != PdfValueKind.Dictionary || graphicsState.Entries.Keys.Any(key => key != "Type" && key != "SA") || (_g.Get(graphicsState, "SA").Kind != PdfValueKind.Boolean || _g.Get(graphicsState, "SA").BooleanValue))
                            throw Error(_c, "ExportEffect", "Only an explicit neutral stroke-adjustment-false state is supported; other effects require another profile.");
                        var stateType = _g.Get(graphicsState, "Type");
                        if (stateType.Kind != PdfValueKind.Null && (stateType.Kind != PdfValueKind.Name || stateType.NameValue != "ExtGState"))
                            throw Error(_c, "ExportEffect", "A standard graphics-state type is required.");
                        break;
                    case PdfOperator.DrawObject:
                        var map = _g.Get(resources, "XObject");
                        if (map.Kind != PdfValueKind.Dictionary || !map.Entries.TryGetValue(a[0].NameValue, out var reference))
                            throw Error(_c, "ExportResource", "An invoked Form resource is missing.");
                        bool identity = reference.Kind == PdfValueKind.Reference;
                        if (identity && !_active.Add(reference.ReferenceId))
                            throw Error(_c, "ExportCycle", "A cyclic Form resource was rejected.");
                        try
                        {
                            var form = _g.Resolve(reference);
                            var subtype = _g.Get(form, "Subtype");
                            if (form.Kind != PdfValueKind.Stream || subtype.Kind != PdfValueKind.Name || subtype.NameValue != "Form")
                                throw Error(_c, "ExportObject", "This export profile supports vector Forms, not images or unknown objects.");
                            if (_g.Get(form, "Group").Kind != PdfValueKind.Null || _g.Get(form, "OC").Kind != PdfValueKind.Null)
                                throw Error(_c, "ExportEffect", "Transparency/layer Form groups require another profile.");
                            var child = state.Copy();
                            child.Matrix = state.Matrix.Multiply(PdfPageGraphics.Matrix(_g.Get(form, "Matrix"), _g));
                            var box = _g.Get(form, "BBox");
                            if (box.Kind != PdfValueKind.Array || box.Items.Count != 4)
                                throw Error(_c, "ExportBounds", "A finite Form BBox is required.");
                            double left = box.Items[0].NumberValue, bottom = box.Items[1].NumberValue, right = box.Items[2].NumberValue, top = box.Items[3].NumberValue;
                            Clip("M " + FormatIo.N(left) + " " + FormatIo.N(bottom) + " L " + FormatIo.N(right) + " " + FormatIo.N(bottom) + " L " + FormatIo.N(right) + " " + FormatIo.N(top) + " L " + FormatIo.N(left) + " " + FormatIo.N(top) + " Z", child.Matrix, false);
                            var declared = _g.Get(form, "Resources");
                            if (declared.Kind == PdfValueKind.Null)
                                declared = resources;
                            using var decoded = PdfFilters.Decode(PdfFilterDeclarations.Resolve(form, id => _g.Resolve(PdfValue.Reference(id)), _c), _c);
                            var bytes = PdfByteStore.Read(decoded, _c);
                            Program(PdfContentProgram.ParseCore(bytes, PdfContentKind.Page, PdfUnknownOperatorPolicy.Reject, null, _c), declared, child);
                            _w.WriteEndElement();
                        }
                        finally
                        {
                            if (identity)
                                _active.Remove(reference.ReferenceId);
                        }

                        break;
                    default:
                        throw Error(_c, "ExportUnsupported", "The page contains operators outside the explicit opaque vector export profile.");
                }
            }

            if (stack.Count != 0 || path.Length != 0 || clip.HasValue)
                throw Error(_c, "ExportState", "Unfinished path/state cannot be silently omitted.");
            while (openClips-- > 0)
                _w.WriteEndElement();
        }

        private int Components(PdfValue name, PdfValue resources)
        {
            var color = name;
            if (name.NameValue != "DeviceRGB" && name.NameValue != "DeviceGray")
            {
                var map = _g.Get(resources, "ColorSpace");
                if (map.Kind != PdfValueKind.Dictionary || !map.Entries.TryGetValue(name.NameValue, out var declared))
                    throw Error(_c, "ExportColor", "An exact device color resource is required.");
                color = _g.Resolve(declared);
            }

            if (color.Kind != PdfValueKind.Name || color.NameValue != "DeviceRGB" && color.NameValue != "DeviceGray")
                throw Error(_c, "ExportColor", "ICC/CMYK/pattern conversion requires another explicit profile.");
            return color.NameValue == "DeviceRGB" ? 3 : 1;
        }

        private string Color(double[] values)
        {
            if ((values.Length != 1 && values.Length != 3) || values.Any(v => v < 0 || v > 1))
                throw Error(_c, "ExportColor", "Only finite unit-range device Gray/RGB paint is supported.");
            return "rgb(" + string.Join(",", (values.Length == 1 ? new[] { values[0], values[0], values[0] } : values).Select(v => FormatIo.N(v * 100) + "%")) + ")";
        }
    }
}
