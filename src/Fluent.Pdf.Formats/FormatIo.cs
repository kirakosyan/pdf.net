using System.Globalization;
using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Fluent.Pdf.Formats;
internal static class FormatIo
{
    internal static string Language(string language)
    {
        Guard.Text(language, nameof(language), 85); string[] parts = language.Split('-'); int at = 0;
        bool Alpha(string v) => v.Length > 0 && v.All(ch => ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z');
        bool Digits(string v) => v.Length > 0 && v.All(ch => ch >= '0' && ch <= '9');
        bool Alnum(string v) => v.Length > 0 && v.All(ch => ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9');
        void Invalid() => throw new ArgumentException("An explicit bounded modern BCP47 language tag is required.", nameof(language));
        if (parts[0].Equals("x", StringComparison.OrdinalIgnoreCase)) at = 1;
        else
        {
            if (parts[0].Length < 2 || parts[0].Length > 8 || !Alpha(parts[0])) Invalid(); int primary = parts[at++].Length;
            if (primary <= 3) for (int count = 0; count < 3 && at < parts.Length && parts[at].Length == 3 && Alpha(parts[at]); count++) at++;
            if (at < parts.Length && parts[at].Length == 4 && Alpha(parts[at])) at++;
            if (at < parts.Length && (parts[at].Length == 2 && Alpha(parts[at]) || parts[at].Length == 3 && Digits(parts[at]))) at++;
            var variants = new HashSet<string>(StringComparer.OrdinalIgnoreCase); while (at < parts.Length && (parts[at].Length >= 5 && parts[at].Length <= 8 && Alnum(parts[at]) || parts[at].Length == 4 && parts[at][0] >= '0' && parts[at][0] <= '9' && Alnum(parts[at]))) { if (!variants.Add(parts[at++])) Invalid(); }
            var extensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase); while (at < parts.Length && parts[at].Length == 1 && Alnum(parts[at]) && !parts[at].Equals("x", StringComparison.OrdinalIgnoreCase)) { if (!extensions.Add(parts[at++])) Invalid(); int start = at; while (at < parts.Length && parts[at].Length >= 2 && parts[at].Length <= 8 && Alnum(parts[at])) at++; if (at == start) Invalid(); }
            if (at == parts.Length) return language; if (!parts[at].Equals("x", StringComparison.OrdinalIgnoreCase)) Invalid(); at++;
        }
        int privateStart = at; for (; at < parts.Length; at++) if (parts[at].Length > 8 || !Alnum(parts[at])) Invalid(); if (privateStart == at) Invalid(); return language;
    }
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
    internal static PdfByteStore XmlBytes(XElement root, PdfOperationContext c) { using var output = new PdfCodecBufferStream(c, 16777216); using (var writer = Writer(output)) { root.WriteTo(writer); writer.Flush(); } return output.Finish(); }
    internal static byte[] Raw(PdfByteStore bytes, PdfOperationContext c) { c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length); var result = new byte[(int)bytes.Length]; using var input = bytes.Open(0, bytes.Length); Exact(input, result, c); return result; }
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
