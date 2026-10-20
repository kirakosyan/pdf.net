using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Fluent.Pdf.Formats;
internal static class FormatIo
{
    internal static PdfByteStore Capture(Stream input, PdfOperationContext c, int maximum = 16777216)
    {
        if (input == null || !input.CanRead) throw new ArgumentException("A readable borrowed format stream is required.", nameof(input));
        c.Consume(PdfResourceKind.ExpandedBytes, 8192); var part = new byte[8192]; using var source = new BorrowedStream(input, true, c); using var output = new PdfCodecBufferStream(c, maximum);
        while (true) { c.Checkpoint(); int n = source.Read(part, 0, (int)Math.Min(part.Length, maximum - output.BufferedLength + 1)); if (n == 0) break; if (n > maximum - output.BufferedLength) throw Error(c, "InputExtent", "The explicit format input extent was exceeded."); output.Write(part, 0, n); }
        return output.Finish();
    }
    internal static string Text(Stream input, PdfOperationContext c, int maximum = 8388608)
    {
        var bytes = Capture(input, c, maximum); c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 4L + 128); var raw = new byte[(int)bytes.Length]; using var source = bytes.Open(0, bytes.Length); Exact(source, raw, c);
        int skip = raw.Length >= 3 && raw[0] == 239 && raw[1] == 187 && raw[2] == 191 ? 3 : 0;
        try { return new UTF8Encoding(false, true).GetString(raw, skip, raw.Length - skip); } catch (DecoderFallbackException) { throw Error(c, "Encoding", "The selected text profile requires well-formed UTF8."); }
    }
    internal static XDocument Xml(Stream input, PdfOperationContext c, int maximum = 8388608)
    {
        var bytes = Capture(input, c, maximum); c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 8L + 4096);
        var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = maximum, MaxCharactersFromEntities = 1024, CloseInput = false };
        try
        {
            using (var source = bytes.Open(0, bytes.Length))
            using (var reader = XmlReader.Create(source, settings))
            while (reader.Read())
            {
                c.Checkpoint(); if (reader.Depth >= c.Policy.Limits.MaximumDepth) c.RejectLimit(PdfResourceKind.Depth);
                c.Consume(PdfResourceKind.Objects, 1 + reader.AttributeCount); c.Consume(PdfResourceKind.ExpandedBytes, 256L * (1 + reader.AttributeCount) + reader.Value.Length * 2L);
                if (reader.NodeType == XmlNodeType.ProcessingInstruction) throw Error(c, "ActiveXml", "Stylesheet and other XML processing instructions are not executed.");
            }
            using var captured = bytes.Open(0, bytes.Length); using var parsed = XmlReader.Create(captured, settings); return XDocument.Load(parsed, LoadOptions.PreserveWhitespace);
        }
        catch (XmlException) { c.Checkpoint(); throw Error(c, "Xml", "Malformed XML, DTDs and external entities are rejected."); }
    }
    internal static XmlWriter Writer(Stream output) => XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false, true), CloseOutput = false, OmitXmlDeclaration = false, Indent = false });
    internal static double Number(string value, PdfOperationContext c)
    {
        if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) || double.IsNaN(number) || double.IsInfinity(number)) throw Error(c, "Number", "A finite invariant number is required."); return number;
    }
    internal static string N(double number) => number == 0 ? "0" : number.ToString("R", CultureInfo.InvariantCulture);
    internal static void Exact(Stream input, byte[] bytes, PdfOperationContext c) { int at = 0; while (at < bytes.Length) { c.Checkpoint(); int n = input.Read(bytes, at, bytes.Length - at); if (n == 0) throw Error(c, "Extent", "Captured bytes ended unexpectedly."); at += n; } }
    internal static void Copy(PdfByteStore bytes, Stream output, PdfOperationContext c) { c.Consume(PdfResourceKind.ExpandedBytes, 8192); var part = new byte[8192]; using var input = bytes.Open(0, bytes.Length); using var target = new BorrowedStream(output, false, c); int n; while ((n = input.Read(part, 0, part.Length)) > 0) { c.Checkpoint(); target.Write(part, 0, n); } }
    internal static void Page(PdfDocument document, PdfPageSize size, Action<PdfCanvas> draw, PdfOperationContext c, List<Action<PdfOperationReport>> finish)
    {
        if (!size.IsValid || size.Width > 14400 || size.Height > 14400) throw Error(c, "PageGeometry", "This fixed-layout profile requires positive page boxes at most14400 points.");
        var geometry = new PdfPageGeometry(new(new(0, 0, size.Width, size.Height)));
        var canvas = PdfCanvas.CreateCore(geometry, draw, new(), c, out var complete); finish.Add(complete);
        using var scope = document.UseModelContext(c); var page = document.Pages.Add(geometry); foreach (var binding in canvas.Drawing.Resources.Bindings) page.SetResource(binding.Name, binding.Resource); page.Content = canvas.Drawing.Content;
    }
    internal static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Format." + code, message);
}
