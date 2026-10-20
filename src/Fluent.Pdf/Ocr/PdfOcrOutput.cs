using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace Fluent.Pdf;
/// <summary>Explicit original OCR interchange profiles, all UTF8 without BOM.</summary>
public enum PdfOcrOutputFormat
{
    /// <summary>Exact raw recognized Unicode, including engine whitespace.</summary>
    PlainText,
    /// <summary>Version-one JSON with raw Unicode, exact provenance, word geometry/confidence and engine IDs.</summary>
    Json,
    /// <summary>XHTML hOCR with top-left pixel boxes, line/word records and engine word confidence.</summary>
    HOcr,
    /// <summary>ALTO4 XML using pixel measurement units and engine word confidence converted to0–1.</summary>
    AltoXml
}

/// <summary>Bounded OCR interchange. Geometry is always top-left raster pixels, never implicitly PDF coordinates.</summary>
public static class PdfOcrOutput
{
    /// <summary>Writes an explicit finite interchange profile at the borrowed current position; the caller stream stays open. Streaming failure may leave a prefix.</summary>
    public static PdfOperationReport Write(PdfOcrResult result, int rasterWidth, int rasterHeight, PdfOcrOutputFormat format, Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        CheckFormat(format);
        return PdfFont.Run("ExportOcr", "managed.ocr-output", policy, cancellationToken, c =>
        {
            using var lease = new BorrowedStream(output, false, c);
            Serialize(result, rasterWidth, rasterHeight, format, lease, c);
        }, PdfCapability.RecognizeText);
    }

    /// <summary>Atomically replaces one explicit file only after complete serialization and cancellation checks. No partial interchange file is committed.</summary>
    public static PdfOperationReport WriteFile(PdfOcrResult result, int rasterWidth, int rasterHeight, PdfOcrOutputFormat format, string path, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        Guard.Text(path, nameof(path), 32767);
        CheckFormat(format);
        return PdfFont.Run("ExportOcrFile", "managed.ocr-output", policy, cancellationToken, c => PdfAtomicFile.Write(path, c, output =>
        {
            using var lease = new BorrowedStream(output, false, c);
            Serialize(result, rasterWidth, rasterHeight, format, lease, c);
        }), PdfCapability.RecognizeText);
    }

    private static void CheckFormat(PdfOcrOutputFormat format)
    {
        if (!Enum.IsDefined(typeof(PdfOcrOutputFormat), format))
            throw new ArgumentOutOfRangeException(nameof(format));
    }

    internal static void Validate(PdfOcrResult result, int width, int height, PdfOperationContext c)
    {
        if (width < 1 || height < 1 || width > 100000 || height > 100000)
            throw new ArgumentOutOfRangeException(nameof(width));
        c.Consume(PdfResourceKind.Objects, result.Words.Count + 1L);
        c.Consume(PdfResourceKind.InputBytes, result.Text.Length * 2L);
        c.Consume(PdfResourceKind.InputBytes, (result.Language.Length + result.EngineVersion.Length + result.ModelIdentity.Length) * 2L);
        foreach (var model in result.Models)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.InputBytes, (model.Identifier.Length + model.Version.Length + model.Sha256.Length + model.License.Length + model.SourceId.Length + model.Scripts.Sum(s => (long)s.Length) + model.Languages.Sum(s => (long)s.Length)) * 2L);
        }

        foreach (var word in result.Words)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.ExpandedBytes, word.Text.Length * 4L + 2048);
            c.Consume(PdfResourceKind.InputBytes, word.Text.Length * 2L);
            if (word.Bounds.Right > width || word.Bounds.Top > height)
                throw PdfWriterCore.Error(c, "Ocr.WordGeometry", "A recognized word lies outside the explicit source raster.");
        }
    }

    private static void Serialize(PdfOcrResult result, int width, int height, PdfOcrOutputFormat format, Stream output, PdfOperationContext c)
    {
        Validate(result, width, height, c);
        c.Consume(PdfResourceKind.ExpandedBytes, 16384);
        if (format == PdfOcrOutputFormat.PlainText || format == PdfOcrOutputFormat.Json)
        {
            using var writer = new StreamWriter(output, new UTF8Encoding(false, true), 1024, true);
            if (format == PdfOcrOutputFormat.PlainText)
                writer.Write(result.Text);
            else
                Json(writer);
            writer.Flush();
            return;
        }

        // Validate before writing any XML; diagnostics must not expose recognized payload.
        foreach (var word in result.Words)
            XmlText(word.Text);
        XmlText(result.EngineVersion);
        XmlText(result.Language);
        using var xml = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false, true), CloseOutput = false, Indent = false, CheckCharacters = true });
        if (format == PdfOcrOutputFormat.HOcr)
            HOcr(xml);
        else
            Alto(xml);
        xml.Flush();
        void XmlText(string value)
        {
            c.Checkpoint();
            try
            {
                XmlConvert.VerifyXmlChars(value);
            }
            catch (XmlException)
            {
                throw PdfWriterCore.Error(c, "Ocr.ExportXmlCharacter", "Recognized data contains a character forbidden by XML1.0.");
            }
        }

        void Json(StreamWriter w)
        {
            w.Write("{\"version\":1,\"coordinateFrame\":\"top-left-pixels\",\"width\":");
            Int(w, width);
            w.Write(",\"height\":");
            Int(w, height);
            w.Write(",\"text\":");
            String(w, result.Text);
            w.Write(",\"language\":");
            String(w, result.Language);
            w.Write(",\"engineVersion\":");
            String(w, result.EngineVersion);
            w.Write(",\"modelIdentity\":");
            String(w, result.ModelIdentity);
            w.Write(",\"meanEngineConfidence\":");
            Confidence(w, result.MeanConfidence);
            w.Write(",\"models\":[");
            bool first = true;
            foreach (var model in result.Models)
            {
                c.Checkpoint();
                if (!first)
                    w.Write(',');
                first = false;
                w.Write("{\"identifier\":");
                String(w, model.Identifier);
                w.Write(",\"version\":");
                String(w, model.Version);
                w.Write(",\"sha256\":");
                String(w, model.Sha256);
                w.Write(",\"byteLength\":");
                w.Write(model.ByteLength.ToString(CultureInfo.InvariantCulture));
                w.Write(",\"license\":");
                String(w, model.License);
                w.Write(",\"sourceId\":");
                String(w, model.SourceId);
                w.Write(",\"scripts\":");
                Strings(w, model.Scripts);
                w.Write(",\"languages\":");
                Strings(w, model.Languages);
                w.Write('}');
            }

            w.Write("],\"words\":[");
            first = true;
            foreach (var word in result.Words)
            {
                c.Checkpoint();
                if (!first)
                    w.Write(',');
                first = false;
                w.Write("{\"text\":");
                String(w, word.Text);
                w.Write(",\"box\":[");
                Number(w, word.Bounds.X);
                w.Write(',');
                Number(w, word.Bounds.Y);
                w.Write(',');
                Number(w, word.Bounds.Width);
                w.Write(',');
                Number(w, word.Bounds.Height);
                w.Write("],\"engineConfidence\":");
                Confidence(w, word.Confidence);
                w.Write(",\"block\":");
                Int(w, word.Block);
                w.Write(",\"paragraph\":");
                Int(w, word.Paragraph);
                w.Write(",\"line\":");
                Int(w, word.Line);
                w.Write(",\"word\":");
                Int(w, word.Word);
                w.Write('}');
            }

            w.Write("]}");
        }

        void String(StreamWriter w, string value)
        {
            w.Write('"');
            foreach (char ch in value)
            {
                c.Checkpoint();
                if (ch == '"')
                    w.Write("\\\"");
                else if (ch == '\\')
                    w.Write("\\\\");
                else if (ch < 32)
                {
                    w.Write("\\u");
                    w.Write(((int)ch).ToString("x4", CultureInfo.InvariantCulture));
                }
                else
                    w.Write(ch);
            }

            w.Write('"');
        }

        void Strings(StreamWriter w, IReadOnlyList<string> values)
        {
            w.Write('[');
            for (int i = 0; i < values.Count; i++)
            {
                if (i != 0)
                    w.Write(',');
                String(w, values[i]);
            }

            w.Write(']');
        }

        void Number(StreamWriter w, double n) => w.Write(n.ToString("R", CultureInfo.InvariantCulture));
        void Int(StreamWriter w, int n) => w.Write(n.ToString(CultureInfo.InvariantCulture));
        void Confidence(StreamWriter w, double? n)
        {
            if (n.HasValue)
                Number(w, n.Value);
            else
                w.Write("null");
        }

        IEnumerable<(int Start, int End, PdfRect Bounds)> Lines()
        {
            int at = 0;
            while (at < result.Words.Count)
            {
                c.Checkpoint();
                int start = at;
                var first = result.Words[at++];
                var box = first.Bounds;
                while (at < result.Words.Count && result.Words[at].Block == first.Block && result.Words[at].Paragraph == first.Paragraph && result.Words[at].Line == first.Line)
                {
                    c.Checkpoint();
                    var b = result.Words[at++].Bounds;
                    double x = Math.Min(box.X, b.X), y = Math.Min(box.Y, b.Y);
                    box = new(x, y, Math.Max(box.Right, b.Right) - x, Math.Max(box.Top, b.Top) - y);
                }

                yield return (start, at, box);
            }
        }

        string Bbox(PdfRect b) => "bbox " + Floor(b.X) + " " + Floor(b.Y) + " " + Ceiling(b.Right) + " " + Ceiling(b.Top);
        string Floor(double n) => Math.Floor(n).ToString(CultureInfo.InvariantCulture);
        string Ceiling(double n) => Math.Ceiling(n).ToString(CultureInfo.InvariantCulture);
        void HOcr(XmlWriter w)
        {
            const string ns = "http://www.w3.org/1999/xhtml";
            w.WriteStartElement("html", ns);
            w.WriteStartElement("head", ns);
            w.WriteStartElement("meta", ns);
            w.WriteAttributeString("name", "ocr-system");
            w.WriteAttributeString("content", result.EngineVersion);
            w.WriteEndElement();
            w.WriteStartElement("meta", ns);
            w.WriteAttributeString("name", "ocr-capabilities");
            w.WriteAttributeString("content", "ocr_page ocr_carea ocr_line ocrx_word");
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteStartElement("body", ns);
            w.WriteStartElement("div", ns);
            w.WriteAttributeString("class", "ocr_page");
            w.WriteAttributeString("id", "page_1");
            w.WriteAttributeString("title", Bbox(new(0, 0, width, height)) + "; ppageno 0");
            foreach (var line in Lines())
            {
                string id = (line.Start + 1).ToString(CultureInfo.InvariantCulture);
                w.WriteStartElement("div", ns);
                w.WriteAttributeString("class", "ocr_carea");
                w.WriteAttributeString("id", "block_" + id);
                w.WriteAttributeString("title", Bbox(line.Bounds));
                w.WriteStartElement("span", ns);
                w.WriteAttributeString("class", "ocr_line");
                w.WriteAttributeString("id", "line_" + id);
                w.WriteAttributeString("title", Bbox(line.Bounds));
                for (int i = line.Start; i < line.End; i++)
                {
                    c.Checkpoint();
                    var word = result.Words[i];
                    if (i != line.Start)
                        w.WriteString(" ");
                    w.WriteStartElement("span", ns);
                    w.WriteAttributeString("class", "ocrx_word");
                    w.WriteAttributeString("id", "word_" + (i + 1).ToString(CultureInfo.InvariantCulture));
                    string title = Bbox(word.Bounds);
                    if (word.Confidence.HasValue)
                        title += "; x_wconf " + Math.Round(word.Confidence.Value, MidpointRounding.AwayFromZero).ToString(CultureInfo.InvariantCulture);
                    w.WriteAttributeString("title", title);
                    w.WriteString(word.Text);
                    w.WriteEndElement();
                }

                w.WriteEndElement();
                w.WriteEndElement();
            }

            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        }

        void Alto(XmlWriter w)
        {
            const string ns = "http://www.loc.gov/standards/alto/ns-v4#";
            w.WriteStartElement("alto", ns);
            w.WriteStartElement("Description", ns);
            w.WriteElementString("MeasurementUnit", ns, "pixel");
            w.WriteStartElement("sourceImageInformation", ns);
            w.WriteElementString("fileName", ns, "caller-provided-raster");
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteStartElement("Layout", ns);
            w.WriteStartElement("Page", ns);
            w.WriteAttributeString("ID", "page_1");
            w.WriteAttributeString("PHYSICAL_IMG_NR", "1");
            w.WriteAttributeString("WIDTH", width.ToString(CultureInfo.InvariantCulture));
            w.WriteAttributeString("HEIGHT", height.ToString(CultureInfo.InvariantCulture));
            w.WriteStartElement("PrintSpace", ns);
            Box(w, new(0, 0, width, height));
            foreach (var line in Lines())
            {
                string id = (line.Start + 1).ToString(CultureInfo.InvariantCulture);
                w.WriteStartElement("TextBlock", ns);
                w.WriteAttributeString("ID", "block_" + id);
                Box(w, line.Bounds);
                w.WriteStartElement("TextLine", ns);
                w.WriteAttributeString("ID", "line_" + id);
                Box(w, line.Bounds);
                for (int i = line.Start; i < line.End; i++)
                {
                    c.Checkpoint();
                    var word = result.Words[i];
                    w.WriteStartElement("String", ns);
                    w.WriteAttributeString("ID", "word_" + (i + 1).ToString(CultureInfo.InvariantCulture));
                    Box(w, word.Bounds);
                    w.WriteAttributeString("CONTENT", word.Text);
                    if (word.Confidence.HasValue)
                        w.WriteAttributeString("WC", (word.Confidence.Value / 100).ToString("R", CultureInfo.InvariantCulture));
                    w.WriteEndElement();
                }

                w.WriteEndElement();
                w.WriteEndElement();
            }

            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteEndElement();
        }

        void Box(XmlWriter w, PdfRect b)
        {
            w.WriteAttributeString("HPOS", Floor(b.X));
            w.WriteAttributeString("VPOS", Floor(b.Y));
            w.WriteAttributeString("WIDTH", Ceiling(b.Right - Math.Floor(b.X)));
            w.WriteAttributeString("HEIGHT", Ceiling(b.Top - Math.Floor(b.Y)));
        }
    }
}
