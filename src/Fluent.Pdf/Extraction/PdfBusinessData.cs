using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Xml;

namespace Fluent.Pdf;
/// <summary>Passive versioned business observations with source/inference/OCR provenance and exact input binding.</summary>
public static class PdfBusinessData
{
    /// <summary>Captures at most64MiBof source PDF at the borrowed current position and stages complete JSON/XML before writing. Source facts, geometric inference and OCR remain separate; no business value is verified.</summary>
    public static PdfOperationReport Write(Stream input, Stream output, PdfBusinessDataFormat format, PdfBusinessDataOptions? options = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite || ReferenceEquals(input, output)) throw new ArgumentException("A distinct writable borrowed output is required.", nameof(output));
        return Run(input, output, null, format, options, providers, policy, cancellationToken);
    }
    /// <summary>Stages complete versioned observations before atomic file replacement.</summary>
    public static PdfOperationReport WriteFile(Stream input, string path, PdfBusinessDataFormat format, PdfBusinessDataOptions? options = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        return Run(input, null, path, format, options, providers, policy, cancellationToken);
    }
    private static PdfOperationReport Run(Stream input, Stream? output, string? path, PdfBusinessDataFormat format, PdfBusinessDataOptions? options, PdfProviderRegistry? providers, PdfOperationPolicy? policy, CancellationToken token)
    {
        if (!Enum.IsDefined(typeof(PdfBusinessDataFormat), format)) throw new ArgumentOutOfRangeException(nameof(format));
        options ??= new(); providers ??= PdfProviderRegistry.Default;
        if (options.Recognition != null)
        {
            _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
            _ = providers.GetRequired<IPdfOcrProvider>(PdfCapability.RecognizeText);
        }
        return PdfFont.Run("ExportBusinessData", "managed.business-data", policy, token, c =>
        {
            var source = PdfProfileIo.Capture(input, c); string digest = PdfArchival.Digest(source, c);
            using var document = PdfPageOrganization.Read(source, 10000, c);
            using var graph = new PdfGraphEdit(document.CreateSnapshot(), new(), c);
            var capture = new Capture(graph, options, providers); capture.Read();
            using var staged = new PdfCodecBufferStream(c, options.MaximumOutputBytes);
            if (format == PdfBusinessDataFormat.Json) capture.Json(staged, digest); else capture.Xml(staged, digest);
            var bytes = staged.Finish();
            if (path == null) PdfProfileIo.Copy(bytes, output!, c); else PdfAtomicFile.Write(path, c, stream => PdfProfileIo.Copy(bytes, stream, c));
        }, PdfCapability.ReadDocument);
    }

    private sealed class Record
    {
        internal readonly List<KeyValuePair<string, object?>> Values = new();
        internal readonly string Id;
        internal Record(string id) => Id = id;
    }
    private sealed class Capture
    {
        private readonly PdfGraphEdit _graph;
        private readonly PdfBusinessDataOptions _options;
        private readonly PdfProviderRegistry _providers;
        private readonly List<Record> _records = new();
        private PdfOperationContext C => _graph.Context;
        internal Capture(PdfGraphEdit graph, PdfBusinessDataOptions options, PdfProviderRegistry providers)
        { _graph = graph; _options = options; _providers = providers; }
        private Record Add(string kind, string provenance, int? page, PdfObjectId? owner, string? parent = null)
        {
            C.Checkpoint();
            if (_records.Count == _options.MaximumRecords) throw Error("RecordLimit", "The explicit business-observation record ceiling was reached.");
            C.Consume(PdfResourceKind.Objects, 1); C.Consume(PdfResourceKind.ExpandedBytes, 512);
            var r = new Record("r" + _records.Count.ToString(CultureInfo.InvariantCulture)); _records.Add(r);
            Set(r, "id", r.Id); Set(r, "kind", kind); Set(r, "provenance", provenance); Set(r, "pageIndex", page); Set(r, "objectId", owner.HasValue ? Identity(owner.Value) : null); Set(r, "parentId", parent); Set(r, "businessVerified", false);
            return r;
        }
        private void Set(Record record, string name, object? value)
        {
            C.Checkpoint(); C.Consume(PdfResourceKind.ExpandedBytes, 64 + (value is string text ? text.Length * 2L : 0));
            if (value is string s) try { XmlConvert.VerifyXmlChars(s); } catch (XmlException) { throw Error("Unicode", "Schema1 JSON/XML parity requires valid XML1.0 Unicode scalar text."); }
            record.Values.Add(new(name, value));
        }
        private void Strings(Record r, string name, IEnumerable<string> values)
        {
            C.Consume(PdfResourceKind.ExpandedBytes, 128); var list = new List<string>();
            foreach (string value in values)
            {
                C.Checkpoint();
                if (list.Count == 100000) throw Error("ReferenceLimit", "A record reference array exceeds100000 entries.");
                C.Consume(PdfResourceKind.ExpandedBytes, value.Length * 2L + 32);
                try { XmlConvert.VerifyXmlChars(value); } catch (XmlException) { throw Error("Unicode", "Schema1 JSON/XML parity requires valid XML1.0 Unicode scalar text."); }
                list.Add(value);
            }
            Set(r, name, list);
        }
        private void Bounds(Record r, PdfRect bounds)
        { Set(r, "x", bounds.X); Set(r, "y", bounds.Y); Set(r, "width", bounds.Width); Set(r, "height", bounds.Height); }
        private PdfException Error(string code, string message) => PdfWriterCore.Error(C, "Business." + code, message);
        private static PdfRect Transform(PdfMatrix matrix, PdfRect r) => new PdfQuad(matrix.Transform(new(r.X, r.Y)), matrix.Transform(new(r.Right, r.Y)), matrix.Transform(new(r.Right, r.Top)), matrix.Transform(new(r.X, r.Top))).Bounds;
        private static string Scalar(object value) => value is double d ? d.ToString("R", CultureInfo.InvariantCulture) : Convert.ToString(value, CultureInfo.InvariantCulture)!;
        private static string Identity(PdfObjectId id) => id.Number.ToString(CultureInfo.InvariantCulture) + ":" + id.Generation.ToString(CultureInfo.InvariantCulture);

        internal void Read()
        {
            for (int page = 0; page < _graph.Snapshot.Pages.Count; page++)
            {
                var owner = _graph.Snapshot.Pages[page].Source!.ObjectId;
                var data = new PdfTextExtractionEngine(_graph, _options.Text, _providers).Read(page);
                var pageRecord = Add("page", "mixed", page, owner);
                Set(pageRecord, "text", data.Raw); Set(pageRecord, "semanticText", data.Text); Set(pageRecord, "layoutText", data.Layout);
                Set(pageRecord, "layoutProvenance", "geometric-inference"); Bounds(pageRecord, data.Geometry.DisplayBounds());
                foreach (var ch in data.Characters)
                {
                    var r = Add("character", ch.Confidence == PdfTextMappingConfidence.Inferred || ch.Confidence == PdfTextMappingConfidence.Unmapped ? "inference" : "source", page, owner, pageRecord.Id);
                    Set(r, "text", ch.Text); Set(r, "sourceIndex", ch.Index); Set(r, "operationIndex", ch.OperationIndex); Set(r, "mapping", ch.Confidence.ToString()); Set(r, "sourceCode", (long)ch.Code); Set(r, "codeLength", ch.CodeLength);
                    Set(r, "invisible", ch.RenderingMode == 3 || ch.RenderingMode == 7); Set(r, "layerVisible", ch.LayerVisible); Set(r, "geometryEstimated", ch.GeometryEstimated); Bounds(r, ch.Quad.Bounds);
                }
                foreach (var span in data.Spans)
                {
                    var r = Add("replacement", "source", page, owner, pageRecord.Id); Set(r, "text", span.Text); Set(r, "sourceIndex", span.FirstCharacter); Set(r, "characterCount", span.CharacterCount); if (span.Bounds.HasValue) Bounds(r, span.Bounds.Value);
                }
                foreach (var paragraph in data.Paragraphs)
                {
                    var r = Add("paragraph", "inference", page, owner, pageRecord.Id); Bounds(r, paragraph.Bounds);
                    Strings(r, "sourceIndices", paragraph.Lines.SelectMany(l => l.Words).SelectMany(w => w.Characters).Select(ch => ch.Index.ToString(CultureInfo.InvariantCulture)));
                    Set(r, "confidenceKind", "unscored-geometric-grouping");
                }
                if (_options.Tables != null)
                {
                    var table = PdfTableExtraction.Detect(_graph, data, _options.Tables, _providers);
                    if (table != null)
                    {
                        var r = Add("table", "inference", page, owner, pageRecord.Id); Bounds(r, table.Bounds);
                        Set(r, "rows", table.RowCount); Set(r, "columns", table.ColumnCount); Set(r, "label", table.Mode.ToString()); Set(r, "confidence", table.Confidence); Set(r, "confidenceKind", "uncalibrated-table-heuristic"); Set(r, "numericCulture", _options.Tables.NumericCulture);
                        foreach (var cell in table.Cells)
                        {
                            var v = Add("cell", "inference", page, owner, r.Id); Bounds(v, cell.Bounds); Set(v, "text", cell.Text); Set(v, "number", cell.Number); Set(v, "row", cell.Row); Set(v, "column", cell.Column); Set(v, "rowSpan", cell.RowSpan); Set(v, "columnSpan", cell.ColumnSpan); Set(v, "confidence", cell.Confidence); Set(v, "confidenceKind", "uncalibrated-table-heuristic");
                            Strings(v, "sourceIndices", cell.Characters.Select(ch => ch.Index.ToString(CultureInfo.InvariantCulture)));
                            Strings(v, "ruleSources", cell.RuleSources.Select(s => Identity(s.ContainerObjectId) + "@" + s.OperationIndex.ToString(CultureInfo.InvariantCulture)));
                        }
                    }
                }
                if (_options.Recognition != null)
                {
                    var rendered = PdfRendering.RenderCore(_graph, page, _providers, _options.Rendering!, C);
                    using var pixels = rendered.Samples.Open(0, rendered.Samples.Length);
                    var raster = PdfOcrRaster.ReadCore(pixels, rendered.Request.Width, rendered.Request.Height, rendered.Request.Stride, PdfOcrPixelFormat.Bgra32, (int)_options.Rendering!.DpiX, 67108864, C);
                    var observed = PdfOcr.RecognizeCore(raster, _options.Recognition, _providers.GetRequired<IPdfOcrProvider>(PdfCapability.RecognizeText), C);
                    var r = Add("ocr", "ocr", page, owner, pageRecord.Id); Set(r, "text", observed.Text); Set(r, "language", observed.Language); Set(r, "engine", observed.EngineVersion); Set(r, "model", observed.ModelIdentity); Set(r, "confidence", observed.MeanConfidence); Set(r, "confidenceKind", "engine-score-0-100");
                    foreach (var word in observed.Words)
                    {
                        var v = Add("ocr-word", "ocr", page, owner, r.Id); Set(v, "text", word.Text); Set(v, "confidence", word.Confidence); Set(v, "confidenceKind", "engine-score-0-100");
                        var region = rendered.Request.Region; var b = word.Bounds;
                        Bounds(v, new(region.X + b.X * region.Width / raster.Width, region.Y + b.Y * region.Height / raster.Height, b.Width * region.Width / raster.Width, b.Height * region.Height / raster.Height));
                    }
                }
            }
            if (_options.IncludeFields)
                foreach (var field in new PdfFormReader(_graph, passive: true).Fields)
                {
                    var r = Add("field", "source", null, field.Representations.Count > 0 ? field.Representations[0] : null);
                    Set(r, "label", field.Name); Set(r, "fieldKind", field.Kind.ToString()); Set(r, "valueKind", field.Value.Kind.ToString());
                    Strings(r, "values", FieldValues(field.Value)); Strings(r, "sourceObjects", field.Representations.Select(Identity));
                    foreach (var widget in field.Widgets)
                    {
                        var w = Add("widget", "source", widget.PageIndex, widget.Id, r.Id);
                        if (widget.PageIndex.HasValue) Bounds(w, Transform(_graph.Snapshot.Pages[widget.PageIndex.Value].Geometry.UserToDisplay(), widget.Bounds));
                    }
                }
            if (_options.StructureProfile.HasValue)
            {
                var reader = new PdfStructureReader(_graph, _options.StructureProfile.Value, _providers);
                foreach (var element in reader.Elements)
                {
                    var r = Add("structure", "source", null, element.ObjectId); Set(r, "label", element.Role); Set(r, "text", element.ActualText); Set(r, "alternativeText", element.AlternativeText); Set(r, "language", element.Language);
                    Strings(r, "children", element.Children.Select(StructureChild));
                }
                foreach (var finding in reader.Findings)
                { var r = Add("structure-finding", "inference", null, null); Set(r, "label", finding.Rule); Set(r, "text", finding.Message); Set(r, "location", finding.Location); }
            }
        }
        private IEnumerable<string> FieldValues(PdfValue value)
        {
            switch (value.Kind)
            {
                case PdfValueKind.Null: yield break;
                case PdfValueKind.String: yield return PdfTextStrings.Decode(value, C); yield break;
                case PdfValueKind.Name: yield return value.NameValue; yield break;
                case PdfValueKind.Array:
                    foreach (var item in value.Items)
                    {
                        C.Checkpoint(); if (item.Kind != PdfValueKind.String) throw Error("FieldValue", "Schema1 multi-values require source text strings.");
                        yield return PdfTextStrings.Decode(item, C);
                    }
                    yield break;
                default: throw Error("FieldValue", "Schema1 exports null, names, text strings and text arrays; signatures and complex values require another profile.");
            }
        }
        private string StructureChild(PdfValue value)
        {
            C.Checkpoint();
            if (value.Kind == PdfValueKind.Reference) return "object:" + Identity(value.ReferenceId);
            if (value.Kind == PdfValueKind.Number) return "mcid:" + value.NumberValue.ToString("R", CultureInfo.InvariantCulture);
            if (value.Kind == PdfValueKind.Dictionary)
            {
                var type = _graph.Get(value, "Type");
                if (type.Kind == PdfValueKind.Name && (type.NameValue == "MCR" || type.NameValue == "OBJR"))
                {
                    string Reference(string key) => value.Entries.TryGetValue(key, out var raw) && raw.Kind == PdfValueKind.Reference ? Identity(raw.ReferenceId) : "inherited";
                    var mcid = _graph.Get(value, "MCID");
                    return type.NameValue + ":" + Reference("Pg") + ":" + Reference(type.NameValue == "MCR" ? "Stm" : "Obj") + ":" + (mcid.Kind == PdfValueKind.Number ? mcid.NumberValue.ToString("R", CultureInfo.InvariantCulture) : "none");
                }
            }
            throw Error("StructureChild", "Schema1 cannot represent this direct structural child.");
        }

        internal void Json(Stream output, string digest)
        {
            C.Consume(PdfResourceKind.ExpandedBytes, 8192);
            using var writer = new StreamWriter(output, new UTF8Encoding(false, true), 1024, true);
            writer.Write("{\"schemaVersion\":1,\"sourceSha256\":\""); writer.Write(digest); writer.Write("\",\"coordinateSpace\":\"display-points-top-left\",\"businessVerified\":false,\"records\":[");
            for (int i = 0; i < _records.Count; i++)
            {
                C.Checkpoint(); if (i != 0) writer.Write(','); writer.Write('{'); bool first = true;
                foreach (var field in _records[i].Values) { if (!first) writer.Write(','); first = false; Quote(writer, field.Key); writer.Write(':'); JsonValue(writer, field.Value); }
                writer.Write('}');
            }
            writer.Write("]}"); writer.Flush();
        }
        private void JsonValue(TextWriter writer, object? value)
        {
            C.Checkpoint();
            if (value == null) writer.Write("null");
            else if (value is string s) Quote(writer, s);
            else if (value is bool b) writer.Write(b ? "true" : "false");
            else if (value is List<string> list) { writer.Write('['); for (int i = 0; i < list.Count; i++) { if (i != 0) writer.Write(','); Quote(writer, list[i]); } writer.Write(']'); }
            else writer.Write(Scalar(value));
        }
        private void Quote(TextWriter writer, string value)
        {
            writer.Write('"');
            for (int i = 0; i < value.Length; i++)
            {
                if ((i & 1023) == 0) C.Checkpoint(); char ch = value[i];
                if (ch == '"' || ch == '\\') { writer.Write('\\'); writer.Write(ch); }
                else if (ch < 32 || char.IsSurrogate(ch)) { writer.Write("\\u"); writer.Write(((int)ch).ToString("x4", CultureInfo.InvariantCulture)); }
                else writer.Write(ch);
            }
            writer.Write('"');
        }
        internal void Xml(Stream output, string digest)
        {
            C.Consume(PdfResourceKind.ExpandedBytes, 65536);
            const string ns = "urn:fluent-pdf:business:1";
            using var writer = XmlWriter.Create(output, new XmlWriterSettings { Encoding = new UTF8Encoding(false, true), CloseOutput = false, NewLineHandling = NewLineHandling.Entitize });
            writer.WriteStartElement("businessData", ns); writer.WriteAttributeString("schemaVersion", "1"); writer.WriteAttributeString("sourceSha256", digest); writer.WriteAttributeString("coordinateSpace", "display-points-top-left"); writer.WriteAttributeString("businessVerified", "false");
            foreach (var record in _records)
            {
                C.Checkpoint(); writer.WriteStartElement("record", ns);
                foreach (var field in record.Values)
                {
                    writer.WriteStartElement(field.Key, ns); var value = field.Value;
                    writer.WriteAttributeString("type", value == null ? "null" : value is string ? "string" : value is bool ? "boolean" : value is List<string> ? "array" : "number");
                    if (value is List<string> list) foreach (string item in list) { C.Checkpoint(); writer.WriteElementString("value", ns, item); }
                    else if (value != null) writer.WriteString(value is bool b ? b ? "true" : "false" : Scalar(value));
                    writer.WriteEndElement();
                }
                writer.WriteEndElement();
            }
            writer.WriteEndElement(); writer.Flush();
        }
    }
}
