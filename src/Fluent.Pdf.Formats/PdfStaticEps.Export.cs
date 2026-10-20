using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml.Linq;
namespace Fluent.Pdf.Formats;
public static partial class PdfStaticEps
{
    /// <summary>Exports one opaque Gray/RGB vector/Form page as literal level2 EPS data. Text, images, effects, annotations and unsupported color/hairline profiles reject; no raster fallback occurs.</summary>
    public static PdfOperationReport WritePage(PdfDocumentSnapshot source, int pageIndex, Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source)); if (pageIndex < 0 || pageIndex >= source.Pages.Count) throw new ArgumentOutOfRangeException(nameof(pageIndex)); if (output == null || !output.CanWrite) throw new ArgumentException("A writable borrowed EPS stream is required.", nameof(output));
        return PdfFont.Run("ExportStaticEps", "managed.eps-data", policy ?? source.Options.OperationPolicy, cancellationToken, c => FormatIo.Copy(BuildPage(source, pageIndex, c), output, c), PdfCapability.ReadDocument);
    }
    /// <summary>Stages complete EPS before atomic file replacement.</summary>
    public static PdfOperationReport WritePageFile(PdfDocumentSnapshot source, int pageIndex, string path, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null) throw new ArgumentNullException(nameof(source)); if (pageIndex < 0 || pageIndex >= source.Pages.Count) throw new ArgumentOutOfRangeException(nameof(pageIndex)); Guard.Text(path, nameof(path), 32767); return PdfFont.Run("ExportStaticEpsFile", "managed.eps-data", policy ?? source.Options.OperationPolicy, cancellationToken, c => { var bytes = BuildPage(source, pageIndex, c); PdfAtomicFile.Write(path, c, target => FormatIo.Copy(bytes, target, c)); }, PdfCapability.ReadDocument);
    }
    internal static PdfByteStore BuildPage(PdfDocumentSnapshot source, int page, PdfOperationContext c)
    {
        var svg = PdfSvg.BuildVectorPage(source, page, c, "Printer.StaticEps"); using var input = svg.Open(0, svg.Length); var root = FormatIo.Xml(input, c).Root!; double width = Size("width"), height = Size("height");
        using var buffer = new PdfCodecBufferStream(c, 67108864); using (var writer = new StreamWriter(buffer, new UTF8Encoding(false, true), 1024, leaveOpen: true))
        {
            writer.NewLine = "\n"; writer.WriteLine("%!PS-Adobe-3.0 EPSF-3.0"); writer.WriteLine("%%BoundingBox: 0 0 " + Math.Ceiling(width).ToString(CultureInfo.InvariantCulture) + " " + Math.Ceiling(height).ToString(CultureInfo.InvariantCulture)); writer.WriteLine("%%HiResBoundingBox: 0 0 " + FormatIo.N(width) + " " + FormatIo.N(height)); writer.WriteLine("%%LanguageLevel: 2\n%%Pages: 1\n%%EndComments\ngsave"); writer.WriteLine("false setstrokeadjust"); writer.WriteLine("0 " + FormatIo.N(height) + " translate 1 -1 scale"); XNamespace ns = "http://www.w3.org/2000/svg"; var clips = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var clip in root.Descendants(ns + "clipPath")) { c.Checkpoint(); string id = (string?)clip.Attribute("id") ?? ""; if (clips.ContainsKey(id)) throw Error(c, "ExportClip", "An unambiguous clip identity is required."); clips.Add(id, clip); }
            foreach (var child in root.Elements()) Node(child); writer.WriteLine("grestore\nshowpage\n%%EOF"); writer.Flush();
            void Node(XElement element)
            {
                using var depth = c.EnterScope(PdfResourceKind.Depth); c.Checkpoint(); if (element.Name == ns + "defs") return;
                if (element.Name == ns + "g") { string value = (string?)element.Attribute("clip-path") ?? ""; if (!value.StartsWith("url(#", StringComparison.Ordinal) || !value.EndsWith(")", StringComparison.Ordinal) || !clips.TryGetValue(value.Substring(5, value.Length - 6), out var clip)) throw Error(c, "ExportClip", "A closed local clip reference is required."); var paths = clip.Elements(ns + "path").Take(2).ToArray(); if (paths.Length != 1) throw Error(c, "ExportClip", "An explicit single compound clip path is required."); writer.WriteLine("gsave\nnewpath"); Path(paths[0], true); writer.WriteLine((string?)paths[0].Attribute("clip-rule") == "evenodd" ? "eoclip newpath" : "clip newpath"); foreach (var child in element.Elements()) Node(child); writer.WriteLine("grestore"); return; }
                if (element.Name != ns + "path") throw Error(c, "ExportProfile", "The vector bridge returned unsupported output data."); writer.WriteLine("gsave"); Matrix(FormatVectorData.Matrix((string?)element.Attribute("transform") ?? "", c)); string fill = (string?)element.Attribute("fill") ?? "none", stroke = (string?)element.Attribute("stroke") ?? "none";
                if (fill != "none") { Color(fill); writer.WriteLine("newpath"); Path(element, false); writer.WriteLine((string?)element.Attribute("fill-rule") == "evenodd" ? "eofill" : "fill"); }
                if (stroke != "none") { Color(stroke); writer.WriteLine(Number(element, "stroke-width") + " setlinewidth"); writer.WriteLine(Cap((string?)element.Attribute("stroke-linecap")) + " setlinecap " + Join((string?)element.Attribute("stroke-linejoin")) + " setlinejoin"); writer.WriteLine(Number(element, "stroke-miterlimit") + " setmiterlimit"); string dash = (string?)element.Attribute("stroke-dasharray") ?? ""; foreach (string component in dash.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) _ = Scalar(component, c); writer.WriteLine("[" + dash + "] " + FormatIo.N(Scalar((string?)element.Attribute("stroke-dashoffset") ?? "0", c)) + " setdash"); writer.WriteLine("newpath"); Path(element, false); writer.WriteLine("stroke"); }
                writer.WriteLine("grestore");
            }
            void Path(XElement element, bool transform)
            {
                var matrix = transform ? FormatVectorData.Matrix((string?)element.Attribute("transform") ?? "", c) : PdfMatrix.Identity;
                foreach (var segment in FormatVectorData.Path((string?)element.Attribute("d") ?? "", c)) { c.Checkpoint(); var values = segment.Coordinates; if (segment.Operation == PdfOperator.ClosePath) { writer.WriteLine("closepath"); continue; } for (int i = 0; i < values.Count; i += 2) { var point = matrix.Transform(new(values[i], values[i + 1])); writer.Write(Scalar(FormatIo.N(point.X), c).ToString("R", CultureInfo.InvariantCulture)); writer.Write(' '); writer.Write(Scalar(FormatIo.N(point.Y), c).ToString("R", CultureInfo.InvariantCulture)); writer.Write(' '); } writer.WriteLine(segment.Operation == PdfOperator.MoveTo ? "moveto" : segment.Operation == PdfOperator.LineTo ? "lineto" : "curveto"); }
            }
            void Matrix(PdfMatrix m) { var a = new[] { m.A, m.B, m.C, m.D, m.E, m.F }; foreach (double value in a) _ = Scalar(FormatIo.N(value), c); writer.WriteLine("[" + string.Join(" ", a.Select(FormatIo.N)) + "] concat"); }
            void Color(string value) { var components = FormatVectorData.Color(value, c); foreach (double component in components) _ = Scalar(FormatIo.N(component), c); writer.WriteLine(string.Join(" ", components.Select(FormatIo.N)) + " setrgbcolor"); }
            string Number(XElement element, string name) => FormatIo.N(Scalar((string?)element.Attribute(name) ?? "", c));
        }
        return buffer.Finish();
        double Size(string name) { string value = (string?)root.Attribute(name) ?? ""; if (!value.EndsWith("pt", StringComparison.Ordinal)) throw Error(c, "ExportGeometry", "Explicit point geometry is required."); double size = Scalar(value.Substring(0, value.Length - 2), c); if (size <= 0 || size > 14400) throw Error(c, "ExportGeometry", "EPS page dimensions must be at most14400 points."); return size; }
        string Cap(string? value) => value == "butt" ? "0" : value == "round" ? "1" : value == "square" ? "2" : throw Error(c, "ExportStyle", "Unsupported line cap.");
        string Join(string? value) => value == "miter" ? "0" : value == "round" ? "1" : value == "bevel" ? "2" : throw Error(c, "ExportStyle", "Unsupported line join.");
    }
}
