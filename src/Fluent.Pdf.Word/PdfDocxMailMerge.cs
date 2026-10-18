using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Xml;
using System.Xml.Linq;

namespace Fluent.Pdf.Word;
/// <summary>Closed DOCX merge policy sharing the native data/culture evaluator and explicit Word export/provider limits.</summary>
public sealed class PdfDocxMergeOptions
{
    /// <summary>Captures explicit evaluation/culture and Word limits; no field, font, image or external resource is discovered.</summary>
    public PdfDocxMergeOptions(PdfMergeEvaluationOptions evaluation, PdfWordConversionOptions word)
    {
        Evaluation = evaluation ?? throw new ArgumentNullException(nameof(evaluation));
        Word = word ?? throw new ArgumentNullException(nameof(word));
    }

    /// <summary>Gets the shared closed evaluation policy.</summary>
    public PdfMergeEvaluationOptions Evaluation { get; }
    /// <summary>Gets source/output/archive/provider limits for the filled package and optional PDF conversion.</summary>
    public PdfWordConversionOptions Word { get; }
}

/// <summary>Bounded original DOCX content-control/scalar binding through the optional Word adapter; template expressions never execute arbitrary code.</summary>
public static class PdfDocxMailMerge
{
    /// <summary>Fills an owned private package and writes DOCX at the borrowed output position, keeping both streams open. Scalar data is escaped as text and never reevaluated.</summary>
    public static PdfOperationReport Write(Stream template, PdfMergeRecord data, PdfDocxMergeOptions options, Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        return Run(template, data, options, output, null, null, policy, cancellationToken, out _);
    }

    /// <summary>Fills and serializes privately before atomic DOCX destination replacement.</summary>
    public static PdfOperationReport WriteFile(Stream template, PdfMergeRecord data, PdfDocxMergeOptions options, string path, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        return Run(template, data, options, null, path, null, policy, cancellationToken, out _);
    }

    /// <summary>Fills a private DOCX package and imports it through the explicitly registered optional Word converter under the same operation; returns a caller-owned PDF model.</summary>
    public static PdfDocument GeneratePdf(Stream template, PdfMergeRecord data, PdfDocxMergeOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        providers.GetRequired<IPdfWordConverter>(PdfCapability.ConvertWord);
        Run(template, data, options, null, null, providers, policy, cancellationToken, out var document);
        return document!;
    }

    private static PdfOperationReport Run(Stream template, PdfMergeRecord data, PdfDocxMergeOptions options, Stream? output, string? path, PdfProviderRegistry? providers, PdfOperationPolicy? policy, CancellationToken token, out PdfDocument? result)
    {
        if (template == null || !template.CanRead)
            throw new ArgumentException("A readable borrowed template is required.", nameof(template));
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        PdfDocument? document = null;
        try
        {
            var report = PdfFont.Run("MergeDocx", "managed.word-merge", policy, token, c =>
            {
                void Prepare(Stream? target)
                {
                    var original = PdfWordConversion.Capture(template, options.Word.MaximumInputBytes, c);
                    var filled = new Engine(original, data, options, c).Fill();
                    c.Checkpoint();
                    if (providers != null)
                    {
                        using var input = filled.Open(0, filled.Length);
                        using var buffer = new PdfCodecBufferStream(c, options.Word.MaximumOutputBytes);
                        using (var source = new BorrowedStream(input, true, c))
                        using (var destination = new BorrowedStream(buffer, false, c))
                            providers.GetRequired<IPdfWordConverter>(PdfCapability.ConvertWord).Import(source, PdfWordFormat.Docx, destination, options.Word, c);
                        c.Checkpoint();
                        document = PdfPageOrganization.Read(buffer.Finish(), options.Word.MaximumPages, c);
                    }
                    else
                    {
                        using var input = filled.Open(0, filled.Length);
                        using var destination = new BorrowedStream(target!, false, c);
                        PdfWordConversion.Copy(input, destination, filled.Length, c);
                    }

                    c.EnsureComplete();
                }

                if (path != null)
                    PdfAtomicFile.Write(path, c, stream => Prepare(stream));
                else
                    Prepare(output);
            }, PdfCapability.ConvertWord);
            document?.SetCompositionReport(report);
            result = document;
            return report;
        }
        catch
        {
            document?.Dispose();
            throw;
        }
    }

    private sealed class Engine
    {
        private static readonly XNamespace W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main", WP = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
        private static readonly XNamespace Rel = "http://schemas.openxmlformats.org/package/2006/relationships", CT = "http://schemas.openxmlformats.org/package/2006/content-types";
        private readonly PdfByteStore _source;
        private readonly PdfDocxMergeOptions _options;
        private readonly PdfMergeEvaluation _evaluation;
        private readonly Dictionary<string, PdfByteStore> _parts = new(StringComparer.Ordinal);
        private readonly Dictionary<string, XDocument> _xml = new(StringComparer.Ordinal);
        private int _imageId = 1000;
        private PdfOperationContext C => _evaluation.Context;

        internal Engine(PdfByteStore source, PdfMergeRecord data, PdfDocxMergeOptions options, PdfOperationContext c)
        {
            _source = source;
            _options = options;
            _evaluation = new(data, options.Evaluation, c);
        }

        internal PdfByteStore Fill()
        {
            using (var input = _source.Open(0, _source.Length))
                WordPackagePreflight.Validate(input, _options.Word, C);
            using (var input = _source.Open(0, _source.Length))
            using (var zip = new ZipArchive(input, ZipArchiveMode.Read, true))
                foreach (var entry in zip.Entries)
                {
                    _evaluation.Visit(entry.Length * 2L + 256);
                    if (entry.FullName.EndsWith("/", StringComparison.Ordinal))
                        continue;
                    using var stream = entry.Open();
                    _parts.Add(entry.FullName, PdfWordConversion.Capture(stream, _options.Word.MaximumProviderExpandedBytes, C));
                }

            if (!_parts.ContainsKey("[Content_Types].xml") || !_parts.ContainsKey("_rels/.rels") || !_parts.ContainsKey("word/document.xml"))
                throw _evaluation.Error("TemplateParts", "This DOCX template profile requires explicit standard package/root/document parts.");
            foreach (var part in _parts.Where(p => p.Key.EndsWith(".xml", StringComparison.Ordinal) || p.Key.EndsWith(".rels", StringComparison.Ordinal)).ToArray())
            {
                _evaluation.Visit(part.Value.Length * 4L + 8192);
                using var input = part.Value.Open(0, part.Value.Length);
                using var reader = XmlReader.Create(input, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = _options.Word.MaximumProviderExpandedBytes, MaxCharactersFromEntities = 1 });
                _xml.Add(part.Key, XDocument.Load(reader, LoadOptions.PreserveWhitespace));
            }

            foreach (var part in _xml.Where(x => x.Key.StartsWith("word/", StringComparison.Ordinal) && x.Key.EndsWith(".xml", StringComparison.Ordinal)).ToArray())
            {
                if (part.Value.Root == null)
                    throw _evaluation.Error("TemplateXml", "A template XML part has no root.");
                Process(part.Value.Root, _evaluation.Root, -1, part.Key);
            }

            if (_parts.Count > _options.Word.MaximumArchiveEntries)
                throw _evaluation.Error("PartLimit", "Filled package parts exceed the explicit archive-entry ceiling.");
            using var output = new PdfCodecBufferStream(C, _options.Word.MaximumOutputBytes);
            C.Consume(PdfResourceKind.ExpandedBytes, 131072);
            using (var archive = new ZipArchive(output, ZipArchiveMode.Create, true))
                foreach (var part in _parts)
                {
                    _evaluation.Visit(65536);
                    var entry = archive.CreateEntry(part.Key, CompressionLevel.Optimal);
                    entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                    using var destination = entry.Open();
                    if (_xml.TryGetValue(part.Key, out var xml))
                    {
                        using var writer = XmlWriter.Create(destination, new XmlWriterSettings { Encoding = new UTF8Encoding(false, true), CloseOutput = false });
                        xml.Save(writer);
                        writer.Flush();
                        C.Checkpoint();
                    }
                    else
                    {
                        using var input = part.Value.Open(0, part.Value.Length);
                        PdfWordConversion.Copy(input, destination, part.Value.Length, C);
                    }
                }

            return output.Finish();
        }

        private void Process(XElement parent, PdfMergeRecord current, int index, string part)
        {
            using var depth = C.EnterScope(PdfResourceKind.Depth);
            _evaluation.Visit(parent.Nodes().Count() * 24L + 128);
            foreach (var element in parent.Elements().ToArray())
            {
                _evaluation.Visit();
                if (element.Name == W + "sdt")
                {
                    string? tag = (string? )element.Element(W + "sdtPr")?.Element(W + "tag")?.Attribute(W + "val");
                    if (tag != null && tag.StartsWith("merge:", StringComparison.Ordinal))
                    {
                        Control(element, tag, current, index, part);
                        continue;
                    }
                }

                if (element.Name == W + "p")
                    Paragraph(element, current, index);
                Process(element, current, index, part);
            }
        }

        private void Control(XElement control, string tag, PdfMergeRecord current, int index, string part)
        {
            var content = control.Element(W + "sdtContent") ?? throw _evaluation.Error("TemplateControl", "A merge content control requires content.");
            int colon = tag.IndexOf(':', 6);
            if (colon < 0)
                throw _evaluation.Error("Instruction", "A merge instruction requires an explicit kind and field.");
            string kind = tag.Substring(6, colon - 6), path = tag.Substring(colon + 1);
            if (kind == "field")
            {
                int formatAt = path.IndexOf(':');
                string? format = formatAt < 0 ? null : path.Substring(formatAt + 1);
                if (formatAt >= 0)
                    path = path.Substring(0, formatAt);
                string value = Format(current, path, format, index);
                _evaluation.Visit(value.Length * 4L + 256);
                var texts = content.Descendants(W + "t").ToArray();
                if (texts.Length == 0)
                    throw _evaluation.Error("TemplateField", "A scalar control requires an explicit existing text run.");
                texts[0].Value = value;
                texts[0].SetAttributeValue(XNamespace.Xml + "space", "preserve");
                foreach (var text in texts.Skip(1))
                    text.Value = "";
                control.ReplaceWith(content.Nodes().ToArray());
                return;
            }

            if (kind == "if")
            {
                if (!_evaluation.Condition(current, path, index))
                {
                    control.Remove();
                    return;
                }

                if (!content.Descendants(W + "p").Any())
                    Paragraph(content, current, index, true);
                Process(content, current, index, part);
                control.ReplaceWith(content.Nodes().ToArray());
                return;
            }

            if (kind == "each")
            {
                var rows = _evaluation.Records(current, path, index);
                for (int i = 0; i < rows.Count; i++)
                {
                    _evaluation.Record();
                    _evaluation.Visit(content.DescendantNodes().LongCount() * 512L + 256);
                    var clone = new XElement(content);
                    if (clone.Descendants().Any(e => e.Name == W + "bookmarkStart" || e.Name == W + "bookmarkEnd" || e.Name == W + "commentRangeStart" || e.Name == W + "commentRangeEnd" || e.Name == WP + "docPr"))
                        throw _evaluation.Error("RepeatIdentity", "Repeating source bookmarks/comments/drawings requires an explicit identity-rebinding profile; use image controls for owned repeated images.");
                    if (!clone.Descendants(W + "p").Any())
                        Paragraph(clone, rows[i], i, true);
                    Process(clone, rows[i], i, part);
                    control.AddBeforeSelf(clone.Nodes().ToArray());
                }

                control.Remove();
                return;
            }

            if (kind == "image")
            {
                var image = _evaluation.Image(current, path, index);
                if (image == null)
                {
                    control.Remove();
                    return;
                }

                _ = PdfImagePng.Read(image.Bytes, new(), C);
                string directory = part.Substring(0, part.LastIndexOf('/') + 1), filename = part.Substring(part.LastIndexOf('/') + 1);
                string relationshipPart = directory + "_rels/" + filename + ".rels";
                if (!_xml.TryGetValue(relationshipPart, out var relationships))
                {
                    _evaluation.Visit(8192);
                    relationships = new(new XElement(Rel + "Relationships"));
                    _xml.Add(relationshipPart, relationships);
                    _parts.Add(relationshipPart, new PdfByteBuilder(C).Build());
                }

                string id, imagePart;
                do
                {
                    _evaluation.Visit();
                    if (_imageId++ == 1000000)
                        throw _evaluation.Error("ImageIdentity", "Image identity inventory was exhausted.");
                    id = "rImage" + _imageId.ToString(System.Globalization.CultureInfo.InvariantCulture);
                    imagePart = directory + "media/merge" + _imageId.ToString(System.Globalization.CultureInfo.InvariantCulture) + ".png";
                }
                while (_parts.ContainsKey(imagePart) || relationships.Descendants(Rel + "Relationship").Any(e => (string? )e.Attribute("Id") == id) || _xml.Values.Any(x => x.Descendants(WP + "docPr").Any(e => (string? )e.Attribute("id") == _imageId.ToString(System.Globalization.CultureInfo.InvariantCulture))));
                _parts.Add(imagePart, image.Bytes);
                relationships.Root!.Add(new XElement(Rel + "Relationship", new XAttribute("Id", id), new XAttribute("Type", "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image"), new XAttribute("Target", "media/" + imagePart.Substring(imagePart.LastIndexOf('/') + 1))));
                var types = _xml["[Content_Types].xml"].Root!;
                if (!types.Elements(CT + "Default").Any(e => (string? )e.Attribute("Extension") == "png"))
                    types.Add(new XElement(CT + "Default", new XAttribute("Extension", "png"), new XAttribute("ContentType", "image/png")));
                var picture = WordOpenXml.PictureFragment(new WordPicture { Bytes = image.Bytes, Width = image.Width, Height = image.Height }, _imageId, C);
                if (control.Parent?.Name == W + "p")
                    control.ReplaceWith(picture.Elements(W + "r").ToArray());
                else if (control.Parent?.Name == W + "body" || control.Parent?.Name == W + "tc")
                    control.ReplaceWith(picture);
                else
                    throw _evaluation.Error("ImagePlacement", "An image control requires an explicit paragraph/body/table-cell placement.");
                return;
            }

            throw _evaluation.Error("Instruction", "Unknown merge instructions are not executable.");
        }

        private string Format(PdfMergeRecord current, string path, string? format, int index)
        {
            string result = _evaluation.Format(current, path, format, index);
            try
            {
                XmlConvert.VerifyXmlChars(result);
            }
            catch (XmlException)
            {
                throw _evaluation.Error("XmlCharacter", "A scalar contains characters forbidden in DOCX XML text; field data is not echoed.");
            }

            return result;
        }

        private void Paragraph(XElement paragraph, PdfMergeRecord current, int index, bool inline = false)
        {
            var texts = paragraph.Descendants(W + "t").Where(t => (inline || t.Ancestors(W + "p").FirstOrDefault() == paragraph) && !t.Ancestors(W + "sdt").Any(s => s.Ancestors().Contains(paragraph))).ToArray();
            _evaluation.Visit(texts.Sum(t => (long)t.Value.Length * 8) + texts.Length * 64L + 128);
            string full = string.Concat(texts.Select(t => t.Value));
            var edits = new List<(int Start, int End, string Value)>();
            for (int at = 0; at < full.Length;)
            {
                C.Checkpoint();
                int start = full.IndexOf("{{", at, StringComparison.Ordinal);
                if (start < 0)
                    break;
                int end = full.IndexOf("}}", start + 2, StringComparison.Ordinal);
                if (end < 0 || end - start > 1280)
                    throw _evaluation.Error("Placeholder", "A scalar placeholder is malformed.");
                string expression = full.Substring(start + 2, end - start - 2);
                int formatAt = expression.IndexOf(':');
                string path = formatAt < 0 ? expression : expression.Substring(0, formatAt), value = Format(current, path, formatAt < 0 ? null : expression.Substring(formatAt + 1), index);
                _evaluation.Visit(value.Length * 4L + 128);
                edits.Add((start, end + 2, value));
                at = end + 2;
            }

            foreach (var edit in edits.AsEnumerable().Reverse())
            {
                int offset = 0, first = -1, last = -1, begin = 0, finish = 0;
                for (int i = 0; i < texts.Length; i++)
                {
                    int end = offset + texts[i].Value.Length;
                    if (first < 0 && edit.Start >= offset && edit.Start < end)
                    {
                        first = i;
                        begin = edit.Start - offset;
                    }

                    if (edit.End > offset && edit.End <= end)
                    {
                        last = i;
                        finish = edit.End - offset;
                        break;
                    }

                    offset = end;
                }

                if (first < 0 || last < 0)
                    throw _evaluation.Error("PlaceholderRange", "A scalar placeholder does not map to source text runs.");
                long replacementLength = begin + (long)edit.Value.Length + (first == last ? texts[last].Value.Length - finish : 0);
                if (replacementLength > 1000000)
                    throw _evaluation.Error("TextLimit", "A filled text run exceeds one million UTF16units.");
                C.Consume(PdfResourceKind.ExpandedBytes, (texts[first].Value.Length + (long)texts[last].Value.Length + replacementLength) * 4 + 128);
                string before = texts[first].Value.Substring(0, begin), after = texts[last].Value.Substring(finish);
                texts[first].Value = before + edit.Value + (first == last ? after : "");
                texts[first].SetAttributeValue(XNamespace.Xml + "space", "preserve");
                for (int i = first + 1; i < last; i++)
                    texts[i].Value = "";
                if (last != first)
                    texts[last].Value = after;
            }
        }
    }
}
