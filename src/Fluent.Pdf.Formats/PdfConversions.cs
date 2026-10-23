using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf.Formats;
/// <summary>Exact export profiles available through this source orchestration API.</summary>
public enum PdfConversionTarget
{
    /// <summary>Opaque supported vector page SVG; no automatic raster fallback.</summary>
    Svg,
    /// <summary>Literal static EPS vector page.</summary>
    StaticEps,
    /// <summary>Explicit rendered PNG page.</summary>
    Png,
    /// <summary>Explicit opaque BMP page.</summary>
    Bmp,
    /// <summary>Explicit ordered raster TIFF pages.</summary>
    Tiff,
    /// <summary>Self-contained fixed raster HTML.</summary>
    RasterHtml,
    /// <summary>XPS2005 raster pages.</summary>
    RasterXps,
    /// <summary>OpenXPS1.0 raster pages.</summary>
    RasterOpenXps,
    /// <summary>OFD1.0 raster pages.</summary>
    RasterOfd
}

/// <summary>Content family observation, not complete validation or an inferred plain-text format.</summary>
public enum PdfDetectedFormat
{
    /// <summary>Unknown or caller-selected plain text.</summary>
    Unknown,
    /// <summary>PDF header.</summary>
    Pdf,
    /// <summary>PNG signature.</summary>
    Png,
    /// <summary>JPEG signature.</summary>
    Jpeg,
    /// <summary>Classic TIFF signature.</summary>
    Tiff,
    /// <summary>BMP signature.</summary>
    Bmp,
    /// <summary>Static SVG XML root.</summary>
    Svg,
    /// <summary>EPSF PostScript header.</summary>
    Eps,
    /// <summary>PostScript header.</summary>
    PostScript,
    /// <summary>Canonical XLSX OPC main identity.</summary>
    Xlsx,
    /// <summary>Canonical PPTX OPC main identity.</summary>
    Pptx,
    /// <summary>Canonical DOCX OPC main identity.</summary>
    Docx,
    /// <summary>EPUB MIME/container identity.</summary>
    Epub,
    /// <summary>ODS MIME identity.</summary>
    Ods,
    /// <summary>XPS2005 fixed representation.</summary>
    Xps,
    /// <summary>OpenXPS fixed representation.</summary>
    OpenXps,
    /// <summary>OFD1.0 root identity.</summary>
    Ofd,
    /// <summary>EMF header signature.</summary>
    Emf,
    /// <summary>XSL-FO root.</summary>
    FormattingObjects,
    /// <summary>Literal HTML5 document header; no scripts or resources are executed.</summary>
    Html
}

/// <summary>Immutable content identification and shared operation report.</summary>
public sealed class PdfFormatDetection
{
    internal PdfFormatDetection(PdfDetectedFormat format, PdfOperationReport report)
    {
        Format = format;
        OperationReport = report;
    }

    /// <summary>Gets the observed family. Consumers must still validate its exact supported content profile.</summary>
    public PdfDetectedFormat Format { get; }
    /// <summary>Gets detection diagnostics, including a literal extension mismatch when supplied.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Explicit per-export format options. Language/title/identity are required by the profiles that use them.</summary>
public sealed class PdfConversionOptions
{
    /// <summary>Captures an exact profile and its literal metadata/rendering requirements; does not install or discover providers.</summary>
    public PdfConversionOptions(PdfConversionTarget target, PdfRenderOptions? rendering = null, string? language = null, string? title = null, PdfOfdIdentity? ofdIdentity = null)
    {
        if (!Enum.IsDefined(typeof(PdfConversionTarget), target))
            throw new ArgumentOutOfRangeException(nameof(target));
        if (target == PdfConversionTarget.RasterHtml || target == PdfConversionTarget.RasterXps || target == PdfConversionTarget.RasterOpenXps)
        {
            if (language == null)
                throw new ArgumentNullException(nameof(language));
            FormatIo.Language(language);
        }

        if (target == PdfConversionTarget.RasterHtml)
        {
            Guard.Text(title!, nameof(title), 4096);
            System.Xml.XmlConvert.VerifyXmlChars(title!);
        }

        if (target == PdfConversionTarget.RasterOfd && ofdIdentity == null)
            throw new ArgumentNullException(nameof(ofdIdentity));
        Target = target;
        Rendering = rendering ?? new();
        Language = language;
        Title = title;
        OfdIdentity = ofdIdentity;
    }

    /// <summary>Gets exact export profile.</summary>
    public PdfConversionTarget Target { get; }
    /// <summary>Gets explicit raster choices.</summary>
    public PdfRenderOptions Rendering { get; }
    /// <summary>Gets explicit language, when used.</summary>
    public string? Language { get; }
    /// <summary>Gets literal HTML title, when used.</summary>
    public string? Title { get; }
    /// <summary>Gets explicit OFD identity, when used.</summary>
    public PdfOfdIdentity? OfdIdentity { get; }
}

/// <summary>Precommit progress; callback failure/cancellation prevents caller output publication.</summary>
public enum PdfConversionPhase
{
    /// <summary>Options/provider capture is starting.</summary>
    Starting,
    /// <summary>Complete output is staged but has not been copied to the caller destination.</summary>
    OutputPrepared
}

/// <summary>Content discovery and explicit source-profile export orchestration. No advertised entry denotes complete standards or production qualification.</summary>
public static class PdfConversions
{
    /// <summary>Writes the exact machine-readable source export matrix. All entries use the managed library targets on Windows/Linux/macOS; renderer entries additionally need a qualified provider for the actual host.</summary>
    public static PdfOperationReport WriteMatrix(Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed matrix stream is required.", nameof(output));
        return PdfFont.Run("WriteConversionMatrix", "managed.conversions", policy, cancellationToken, c =>
        {
            using var staged = new PdfCodecBufferStream(c, 65536);
            c.Consume(PdfResourceKind.ExpandedBytes, 16384);
            using (var writer = new StreamWriter(staged, new UTF8Encoding(false), 1024, true))
            {
                writer.Write("{\"schema\":1,\"productionQualified\":false,\"loadOrchestration\":false,\"targets\":[");
                bool first = true;
                foreach (PdfConversionTarget target in Enum.GetValues(typeof(PdfConversionTarget)))
                {
                    c.Checkpoint();
                    c.Consume(PdfResourceKind.Objects, 1);
                    if (!first)
                        writer.Write(',');
                    first = false;
                    writer.Write("{\"profile\":\"" + target + "\",\"direction\":\"save\",\"platforms\":[\"Windows\",\"Linux\",\"macOS\"],\"frameworks\":[\"net472\",\"netstandard2.0\",\"net8.0\",\"net10.0\"],\"provider\":\"" + (Raster(target) ? "explicit-page-renderer" : "managed-supported-vector-profile") + "\",\"maximumPages\":" + (Single(target) ? "1" : "512") + ",\"fullFormatSupport\":false}");
                }

                writer.Write("]}");
                writer.Flush();
            }

            FormatIo.Copy(staged.Finish(), output, c);
        }, PdfCapability.ReadDocument);
    }

    /// <summary>Captures up to64 MiB content, checks family identities and rejects ambiguous packages. An extension never overrides bytes; unknown text requires caller selection. Detection is not a complete decoder/validator.</summary>
    public static PdfFormatDetection Detect(Stream input, string? extension = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable borrowed format stream is required.", nameof(input));
        if (extension != null && (extension.Length > 16 || extension.Any(ch => !(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch == '.'))))
            throw new ArgumentException("A bounded literal extension is required.", nameof(extension));
        PdfDetectedFormat result = PdfDetectedFormat.Unknown;
        var report = PdfFont.Run("DetectFormat", "managed.conversions", policy, cancellationToken, c =>
        {
            var b = FormatIo.Capture(input, c, 67108864);
            result = Identify(b, c);
            if (extension != null)
            {
                string supplied = extension.TrimStart('.').ToLowerInvariant();
                string expected = result switch
                {
                    PdfDetectedFormat.Pdf => "pdf",
                    PdfDetectedFormat.Png => "png",
                    PdfDetectedFormat.Jpeg => "jpg",
                    PdfDetectedFormat.Tiff => "tiff",
                    PdfDetectedFormat.Bmp => "bmp",
                    PdfDetectedFormat.Svg => "svg",
                    PdfDetectedFormat.Eps => "eps",
                    PdfDetectedFormat.PostScript => "ps",
                    PdfDetectedFormat.Xlsx => "xlsx",
                    PdfDetectedFormat.Pptx => "pptx",
                    PdfDetectedFormat.Docx => "docx",
                    PdfDetectedFormat.Epub => "epub",
                    PdfDetectedFormat.Ods => "ods",
                    PdfDetectedFormat.Xps => "xps",
                    PdfDetectedFormat.OpenXps => "oxps",
                    PdfDetectedFormat.Ofd => "ofd",
                    PdfDetectedFormat.Emf => "emf",
                    PdfDetectedFormat.FormattingObjects => "fo",
                    PdfDetectedFormat.Html => "html",
                    _ => ""
                };
                if (expected.Length != 0 && supplied != expected && !(result == PdfDetectedFormat.Jpeg && supplied == "jpeg") && !(result == PdfDetectedFormat.Tiff && supplied == "tif") && !(result == PdfDetectedFormat.Html && supplied == "htm"))
                    c.Report(new("Conversion.ExtensionMismatch", "The supplied extension disagrees with the observed content family; bytes remain authoritative.", severity: PdfDiagnosticSeverity.Warning));
            }
        }, PdfCapability.ReadDocument);
        return new(result, report);
    }

    /// <summary>Stages exact selected pages under one shared context, then copies to a borrowed stream. Progress callbacks run only before publication. No empty document or format/provider fallback succeeds.</summary>
    public static PdfOperationReport Export(PdfDocumentSnapshot source, IEnumerable<int> pages, Stream output, PdfConversionOptions options, PdfProviderRegistry? providers = null, Action<PdfConversionPhase, PdfOperationContext>? progress = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Check(source, pages, options);
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed conversion stream is required.", nameof(output));
        providers ??= PdfProviderRegistry.Empty;
        return PdfFont.Run("ExportFormat", "managed.conversions", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            var bytes = Build(source, pages, options, providers, progress, c);
            FormatIo.Copy(bytes, output, c);
        }, PdfCapability.ReadDocument);
    }

    /// <summary>Stages output before atomic file replacement; callbacks share the same context and can cancel before replacement.</summary>
    public static PdfOperationReport ExportFile(PdfDocumentSnapshot source, IEnumerable<int> pages, string path, PdfConversionOptions options, PdfProviderRegistry? providers = null, Action<PdfConversionPhase, PdfOperationContext>? progress = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Check(source, pages, options);
        Guard.Text(path, nameof(path), 32767);
        providers ??= PdfProviderRegistry.Empty;
        return PdfFont.Run("ExportFormatFile", "managed.conversions", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            var bytes = Build(source, pages, options, providers, progress, c);
            PdfAtomicFile.Write(path, c, s => FormatIo.Copy(bytes, s, c));
        }, PdfCapability.ReadDocument);
    }

    private static PdfByteStore Build(PdfDocumentSnapshot source, IEnumerable<int> pages, PdfConversionOptions o, PdfProviderRegistry providers, Action<PdfConversionPhase, PdfOperationContext>? progress, PdfOperationContext c)
    {
        progress?.Invoke(PdfConversionPhase.Starting, c);
        c.Checkpoint();
        if (Raster(o.Target))
            _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
        var selected = new List<int>();
        foreach (int page in pages)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 16);
            if (selected.Count == 512 || page < 0 || page >= source.Pages.Count)
                throw Error(c, "PageSelection", "Select1–512 existing pages in exact order.");
            selected.Add(page);
        }

        if (selected.Count == 0 || Single(o.Target) && selected.Count != 1)
            throw Error(c, "PageSelection", "This profile requires its declared nonempty page selection.");
        PdfByteStore bytes = o.Target switch
        {
            PdfConversionTarget.Svg => PdfSvg.BuildVectorPage(source, selected[0], c),
            PdfConversionTarget.StaticEps => PdfStaticEps.BuildPage(source, selected[0], c),
            PdfConversionTarget.Png => PdfRasterExport.Build(source, selected, new(PdfRasterFormat.Png, o.Rendering), providers, c),
            PdfConversionTarget.Bmp => PdfRasterExport.Build(source, selected, new(PdfRasterFormat.Bmp, o.Rendering), providers, c),
            PdfConversionTarget.Tiff => PdfRasterExport.Build(source, selected, new(PdfRasterFormat.Tiff, o.Rendering), providers, c),
            PdfConversionTarget.RasterHtml => PdfHtmlExport.Build(source, selected, new(PdfHtmlExportMode.FixedRaster, o.Title!, o.Language!, o.Rendering), providers, c),
            PdfConversionTarget.RasterXps => PdfXps.Build(source, selected, PdfXpsFormat.Xps, o.Language!, o.Rendering, providers, c),
            PdfConversionTarget.RasterOpenXps => PdfXps.Build(source, selected, PdfXpsFormat.OpenXps, o.Language!, o.Rendering, providers, c),
            PdfConversionTarget.RasterOfd => PdfOfd.Build(source, selected, o.OfdIdentity!, o.Rendering, providers, c),
            _ => throw Error(c, "ProfileUnavailable", "This exact export profile is unavailable.")};
        if (bytes.Length == 0)
            throw Error(c, "EmptyOutput", "No empty/substituted conversion result is permitted.");
        progress?.Invoke(PdfConversionPhase.OutputPrepared, c);
        c.Checkpoint();
        return bytes;
    }

    private static PdfDetectedFormat Identify(PdfByteStore b, PdfOperationContext c)
    {
        bool Starts(params byte[] value)
        {
            if (b.Length < value.Length)
                return false;
            for (int i = 0; i < value.Length; i++)
                if (b.At(i) != value[i])
                    return false;
            return true;
        }

        if (Starts(37, 80, 68, 70, 45))
            return PdfDetectedFormat.Pdf;
        if (Starts(137, 80, 78, 71, 13, 10, 26, 10))
            return PdfDetectedFormat.Png;
        if (Starts(255, 216, 255))
            return PdfDetectedFormat.Jpeg;
        if (Starts(73, 73, 42, 0) || Starts(77, 77, 0, 42))
            return PdfDetectedFormat.Tiff;
        if (Starts(66, 77))
            return PdfDetectedFormat.Bmp;
        if (b.Length >= 88 && Starts(1, 0, 0, 0) && b.At(40) == 32 && b.At(41) == 69 && b.At(42) == 77 && b.At(43) == 70)
            return PdfDetectedFormat.Emf;
        if (Starts(37, 33, 80, 83, 45, 65, 100, 111, 98, 101, 45))
        {
            c.Consume(PdfResourceKind.ExpandedBytes, 1024);
            var header = new StringBuilder();
            for (long i = 0; i < Math.Min(256, b.Length); i++)
            {
                int ch = b.At(i);
                if (ch == 10 || ch == 13)
                    break;
                if (ch > 127)
                    throw Error(c, "Header", "A literal ASCII PostScript header is required.");
                header.Append((char)ch);
            }

            return header.ToString().IndexOf("EPSF-", StringComparison.Ordinal) >= 0 ? PdfDetectedFormat.Eps : PdfDetectedFormat.PostScript;
        }

        if (Starts(80, 75, 3, 4))
        {
            using var input = b.Open(0, b.Length);
            using var package = new FormatPackage(input, c);
            var candidates = new List<PdfDetectedFormat>();
            if (package.Has("OFD.xml"))
            {
                var root = package.Xml("OFD.xml").Root;
                if (root?.Name == System.Xml.Linq.XName.Get("OFD", "http://www.ofdspec.org/2016"))
                    candidates.Add(PdfDetectedFormat.Ofd);
            }

            if (package.Has("mimetype"))
            {
                var mimeBytes = package.Read("mimetype");
                using var mime = mimeBytes.Open(0, mimeBytes.Length);
                string value = FormatIo.Text(mime, c, 256);
                if (value == "application/epub+zip" && package.Has("META-INF/container.xml"))
                {
                    if (package.Xml("META-INF/container.xml").Root?.Name != System.Xml.Linq.XName.Get("container", "urn:oasis:names:tc:opendocument:xmlns:container"))
                        throw Error(c, "PackageIdentity", "An exact EPUB container root is required.");
                    candidates.Add(PdfDetectedFormat.Epub);
                }

                if (value == "application/vnd.oasis.opendocument.spreadsheet")
                    candidates.Add(PdfDetectedFormat.Ods);
            }

            if (package.Has("[Content_Types].xml"))
            {
                var types = package.Xml("[Content_Types].xml").Root;
                System.Xml.Linq.XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
                if (types?.Name != ns + "Types")
                    throw Error(c, "PackageIdentity", "An exact OPC content type namespace is required.");
                foreach (var item in types.Elements(ns + "Override"))
                {
                    c.Checkpoint();
                    string part = (string? )item.Attribute("PartName") ?? "", type = (string? )item.Attribute("ContentType") ?? "";
                    PdfDetectedFormat f = part == "/xl/workbook.xml" && type == "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml" ? PdfDetectedFormat.Xlsx : part == "/ppt/presentation.xml" && type == "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml" ? PdfDetectedFormat.Pptx : part == "/word/document.xml" && type == "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml" ? PdfDetectedFormat.Docx : part == "/FixedDocumentSequence.fdseq" && type == "application/vnd.ms-package.xps-fixeddocumentsequence+xml" ? PdfDetectedFormat.Xps : part == "/FixedDocumentSequence.fdseq" && type == "application/oxps-fixeddocumentsequence+xml" ? PdfDetectedFormat.OpenXps : PdfDetectedFormat.Unknown;
                    if (f != PdfDetectedFormat.Unknown)
                    {
                        if (!package.Has(part.Substring(1)))
                            throw Error(c, "PackageIdentity", "A declared format main part is missing.");
                        if (candidates.Contains(f))
                            throw Error(c, "Ambiguous", "Duplicate format identities are not inferred.");
                        candidates.Add(f);
                    }
                }
            }

            if (candidates.Count > 1)
                throw Error(c, "Ambiguous", "The package declares multiple format identities; explicit authority is required.");
            return candidates.Count == 0 ? PdfDetectedFormat.Unknown : candidates[0];
        }

        long at = 0;
        if (Starts(239, 187, 191))
            at = 3;
        while (at < b.Length && (b.At(at) == 32 || b.At(at) == 9 || b.At(at) == 10 || b.At(at) == 13))
        {
            c.Checkpoint();
            at++;
        }

        if (at < b.Length && b.At(at) == 60)
        {
            const string html = "<!DOCTYPE html";
            bool isHtml = b.Length - at >= html.Length;
            for (int n = 0; isHtml && n < html.Length; n++)
                if (char.ToLowerInvariant((char)b.At(at + n)) != char.ToLowerInvariant(html[n]))
                    isHtml = false;
            if (isHtml)
            {
                long end = at + html.Length;
                while (end < b.Length && (b.At(end) == 32 || b.At(end) == 9 || b.At(end) == 10 || b.At(end) == 13))
                {
                    c.Checkpoint();
                    end++;
                }

                if (end < b.Length && b.At(end) == 62)
                    return PdfDetectedFormat.Html;
            }

            using var input = b.Open(0, b.Length);
            var root = FormatIo.TryXml(input, c)?.Root;
            return root?.Name.LocalName == "html" && (root.Name.NamespaceName == "" || root.Name.NamespaceName == "http://www.w3.org/1999/xhtml") ? PdfDetectedFormat.Html : root?.Name.LocalName == "svg" && (root.Name.NamespaceName == "" || root.Name.NamespaceName == "http://www.w3.org/2000/svg") ? PdfDetectedFormat.Svg : root?.Name == System.Xml.Linq.XName.Get("root", "http://www.w3.org/1999/XSL/Format") ? PdfDetectedFormat.FormattingObjects : PdfDetectedFormat.Unknown;
        }

        return PdfDetectedFormat.Unknown;
    }

    private static bool Raster(PdfConversionTarget target) => target != PdfConversionTarget.Svg && target != PdfConversionTarget.StaticEps;
    private static bool Single(PdfConversionTarget target) => target == PdfConversionTarget.Svg || target == PdfConversionTarget.StaticEps || target == PdfConversionTarget.Png || target == PdfConversionTarget.Bmp;
    private static void Check(PdfDocumentSnapshot source, IEnumerable<int> pages, PdfConversionOptions options)
    {
        if (source == null || pages == null || options == null)
            throw new ArgumentNullException(source == null ? nameof(source) : pages == null ? nameof(pages) : nameof(options));
    }

    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Conversion." + code, message);
}
