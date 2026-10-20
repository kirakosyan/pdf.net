using System.Collections.ObjectModel;
using System.IO;

namespace Fluent.Pdf.Word;
/// <summary>An explicitly selected immutable TrueType face for the optional Word adapter.</summary>
public sealed class PdfWordnetFont
{
    /// <summary>Captures a bounded requested family, explicit immutable font and face style. No file or machine font is opened.</summary>
    public PdfWordnetFont(string family, PdfFont font, bool bold = false, bool italic = false)
    {
        Family = WordnetPdfConverter.Family(family);
        Font = font ?? throw new ArgumentNullException(nameof(font));
        if (font.Info.Format != PdfFontFormat.TrueType)
            throw new ArgumentException("This explicit source profile requires an actual TrueType program.", nameof(font));
        Bold = bold;
        Italic = italic;
    }

    /// <summary>Gets the explicit family requested by document runs.</summary>
    public string Family { get; }
    /// <summary>Gets the caller-selected immutable face.</summary>
    public PdfFont Font { get; }
    /// <summary>Gets the supplied bold face selector.</summary>
    public bool Bold { get; }
    /// <summary>Gets the supplied italic face selector.</summary>
    public bool Italic { get; }
}

/// <summary>Optional original bridge to the exact published wordnet dependency, plus bounded original RTF/WordprocessingML reconstruction. Full production qualification remains pending.</summary>
public sealed class WordnetPdfConverter : IPdfWordConverter
{
    /// <summary>Gets the exact adopted dependency package version.</summary>
    public const string UpstreamVersion = "1.1.0";
    /// <summary>Gets the actual published package's source revision, distinct from later source-only APIs.</summary>
    public const string SourceRevision = "bc6ada467dd4626dd23dfbee5f848b21e9f67dc5";
    /// <summary>Gets the adopted package SHA256. Locked dependency closure and final artifacts require their own review.</summary>
    public const string PackageSha256 = "073bc06d91367c340f6f031e8cf2066f8a18d735cd93968d37f346d77c7bd4e5";
    /// <summary>Constructs an explicit converter. The pinned dependency has bundled font availability that cannot be disabled; callers must explicitly adopt it. No OS fonts or OCR/model services are discovered.</summary>
    public WordnetPdfConverter(bool allowBundledFonts, string fallbackFamily = "Noto Sans", PdfWordnetFont[]? fonts = null)
    {
        if (!allowBundledFonts)
            throw new PdfException(new("Word.BundledFontPolicy", "The pinned dependency cannot disable its bundled font pack; this adapter requires explicit caller adoption."));
        FallbackFamily = Family(fallbackFamily);
        fonts ??= Array.Empty<PdfWordnetFont>();
        if (fonts.Length > 64 || fonts.Any(f => f == null))
            throw new ArgumentException("At most64nonnull explicit font faces are accepted.", nameof(fonts));
        if (FallbackFamily != "Noto Sans" && !fonts.Any(f => string.Equals(f.Family, FallbackFamily, StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("The explicit fallback must be an adopted bundled or supplied family.", nameof(fallbackFamily));
        Fonts = new ReadOnlyCollection<PdfWordnetFont>((PdfWordnetFont[])fonts.Clone());
    }

    /// <summary>Gets the exact explicit fallback family; dependency substitutions remain diagnostic.</summary>
    public string FallbackFamily { get; }
    /// <summary>Gets immutable explicitly selected faces; the adopted dependency font pack remains available.</summary>
    public IReadOnlyList<PdfWordnetFont> Fonts { get; }
    /// <summary>Gets explicit conversion capability without implying fidelity/production qualification.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("wordnet.managed-adapter", UpstreamVersion, PdfCapability.ConvertWord);

    /// <summary>Reports source-format direction availability. Feature restrictions and losses are evaluated against actual content.</summary>
    public bool Supports(PdfWordFormat format, bool import) => Enum.IsDefined(typeof(PdfWordFormat), format);
    /// <summary>Converts DOC/DOCX through the pinned package; RTF first uses the original bounded bridge. Input/output remain borrowed and every phase uses the supplied root context.</summary>
    public void Import(Stream source, PdfWordFormat format, Stream pdfOutput, PdfWordConversionOptions options, PdfOperationContext context)
    {
        if (!Supports(format, true))
            throw new ArgumentOutOfRangeException(nameof(format));
        if (source == null || pdfOutput == null || options == null || context == null)
            throw new ArgumentNullException(nameof(source));
        context.Checkpoint();
        if (format == PdfWordFormat.Rtf)
        {
            var bytes = PdfWordConversion.Capture(source, options.MaximumInputBytes, context);
            var model = WordRtf.Read(bytes, options, context);
            var docx = WordOpenXml.Write(model, options, context);
            using var input = docx.Open(0, docx.Length);
            Convert(input, pdfOutput, WordNet.DocumentFormat.Docx, WordNet.DocumentFormat.Pdf, options, context);
        }
        else
            Convert(source, pdfOutput, format == PdfWordFormat.Doc ? WordNet.DocumentFormat.Doc : WordNet.DocumentFormat.Docx, WordNet.DocumentFormat.Pdf, options, context);
    }

    /// <summary>Reconstructs explicit editable text or fixed raster pages; omissions and inferred structure are diagnostic. Source actions are never activated, and no OCR/renderer is installed implicitly.</summary>
    public void Export(PdfDocumentSnapshot source, PdfWordFormat format, Stream output, PdfWordConversionOptions options, PdfProviderRegistry providers, PdfOperationContext context)
    {
        if (!Supports(format, false))
            throw new ArgumentOutOfRangeException(nameof(format));
        if (source == null || output == null || options == null || providers == null || context == null)
            throw new ArgumentNullException(nameof(source));
        context.Checkpoint();
        IPdfPageRenderer? renderer = options.Reconstruction == PdfWordReconstruction.FixedLayout ? providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages) : null;
        if (renderer != null)
            WordErrors.Loss(context, "FixedLayoutRasterization", "The selected fixed-layout source profile preserves page appearance as explicit raster images; searchable text, vector editability and source semantic/interactive structure are omitted.");
        else
            WordErrors.Loss(context, "EditableTextReconstruction", "The selected editable source profile recovers Unicode text into page-level paragraphs; original text geometry, mixed styles, graphics, tables, annotations and source semantic/interactive structure are omitted.");
        using var graph = new PdfGraphEdit(source, new(allowSignatureInvalidation: true), context);
        WordErrors.Node(context);
        var model = new WordModel();
        int characters = 0;
        for (int p = 0; p < graph.Snapshot.Pages.Count; p++)
        {
            context.Checkpoint();
            WordErrors.Node(context);
            var page = graph.Snapshot.Pages[p];
            var bounds = page.Geometry.DisplayBounds();
            var section = new WordSection
            {
                Width = bounds.Width,
                Height = bounds.Height
            };
            model.Sections.Add(section);
            if (renderer != null)
            {
                var request = new PdfRasterRequest(p, page.Source!, new(options.RasterDpi, options.RasterDpi), context);
                context.ConsumePixels(request.Width, request.Height);
                using var buffer = new PdfCodecBufferStream(context, request.ByteLength);
                using (var lease = new BorrowedStream(buffer, false, context))
                    renderer.Render(request, lease, context);
                context.Checkpoint();
                if (buffer.BufferedLength != request.ByteLength)
                    throw WordErrors.Error(context, "RasterLength", "The explicit renderer did not return the exact requested BGRA extent.");
                var png = WordPng.Write(buffer.Finish(), request.Width, request.Height, context);
                section.Blocks.Add(new WordPicture { Bytes = png, Width = bounds.Width, Height = bounds.Height, Fixed = true });
            }
            else
            {
                var data = new PdfTextExtractionEngine(graph, new(), providers).Read(p);
                if (data.Text.Length > options.MaximumCharacters - characters)
                    throw WordErrors.Error(context, "CharacterLimit", "Recovered text exceeds its explicit character ceiling.");
                characters += data.Text.Length;
                bool paints = data.Program.Operations.Any(o => o.Operation == PdfOperator.DrawObject || o.Operation == PdfOperator.InlineImage || o.Operation == PdfOperator.Fill || o.Operation == PdfOperator.Stroke || o.Operation == PdfOperator.Shading);
                if (data.Characters.Count == 0 && paints)
                    throw WordErrors.Error(context, "NoRecoverableText", "A painted page has no recoverable Unicode text; choose explicit fixed layout or provide a separate qualified OCR workflow.");
                WordErrors.Node(context, data.Text.Length * 4L + 256);
                var paragraph = new WordParagraph
                {
                    Heading = 0
                };
                paragraph.Runs.Add(new WordRun { Text = data.Text, Family = FallbackFamily, Size = 12 });
                section.Blocks.Add(paragraph);
                context.Report(new("Word.RecoveredTextProvenance", "Unicode is recovered from source mappings; page-level paragraphs are reconstruction, not recovered authoring structure.", severity: PdfDiagnosticSeverity.Information, pageIndex: p));
            }
        }

        if (format == PdfWordFormat.Rtf)
            WordRtf.Write(model, output, FallbackFamily, context);
        else
        {
            var docx = WordOpenXml.Write(model, options, context);
            using var input = docx.Open(0, docx.Length);
            if (format == PdfWordFormat.Docx)
                PdfWordConversion.Copy(input, output, docx.Length, context);
            else
                Convert(input, output, WordNet.DocumentFormat.Docx, WordNet.DocumentFormat.Doc, options, context);
        }

        context.Checkpoint();
    }

    private void Convert(Stream source, Stream output, WordNet.DocumentFormat from, WordNet.DocumentFormat to, PdfWordConversionOptions options, PdfOperationContext c)
    {
        c.Checkpoint();
        PdfByteStore? staged = source.CanSeek ? null : PdfWordConversion.Capture(source, options.MaximumInputBytes, c);
        using Stream? captured = staged?.Open(0, staged.Length);
        Stream selected = captured ?? source;
        if (from == WordNet.DocumentFormat.Docx)
            WordPackagePreflight.Validate(selected, options, c);
        using var depth = c.EnterScope(PdfResourceKind.Depth);
        // The opaque dependency uses a closed finite phase envelope. Every entry is charged; no nested SDK operation resets the root budget.
        c.Consume(PdfResourceKind.ExpandedBytes, options.MaximumProviderExpandedBytes);
        c.Consume(PdfResourceKind.Objects, options.MaximumCharacters * 2L + options.MaximumArchiveEntries * 8L);
        long inputExtent = selected.CanSeek ? Math.Max(0, selected.Length - selected.Position) : options.MaximumInputBytes;
        int inputCeiling = (int)Math.Min(options.MaximumOutputBytes, Math.Max(1, inputExtent));
        int maxInput = Math.Min(inputCeiling, Math.Max(1, checked((int)Math.Min(int.MaxValue, c.Available(PdfResourceKind.InputBytes))))), maxDepth = Math.Min(options.MaximumDepth, (int)Math.Min(int.MaxValue, c.Available(PdfResourceKind.Depth)));
        if (maxDepth < 1)
            throw WordErrors.Error(c, "DependencyDepth", "No root nesting capacity remains for the explicit dependency phase.");
        var settings = new WordNet.ConversionOptions(WordNet.FidelityMode.BestEffort, maxInput, options.MaximumOutputBytes, options.MaximumProviderExpandedBytes, options.MaximumArchiveEntries, options.MaximumCharacters, maxDepth, options.MaximumPages)
        {
            Clock = () => new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero),
            Fonts = new WordNet.FontOptions
            {
                DefaultFallback = FallbackFamily
            }
        };
        foreach (var face in Fonts)
        {
            c.Checkpoint();
            face.Font.EmbeddingAllowed(false, c);
            using var program = new PdfCodecBufferStream(c, 16777216);
            using (var lease = new BorrowedStream(program, false, c))
                face.Font.Program.Write(lease, Array.Empty<int>(), false, c);
            var bytes = program.Finish();
            using var input = bytes.Open(0, bytes.Length);
            c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 3 + 8192);
            settings.Fonts.Sources.Add(new WordNet.FontSource(face.Family, input, face.Bold, face.Italic));
        }

        WordNet.ConversionResult result;
        try
        {
            using var input = new BorrowedStream(selected, true, c);
            using var destination = new BorrowedStream(output, false, c);
            result = new WordNet.WordConverter().Convert(input, destination, from, to, settings, c.CancellationToken);
        }
        catch (OperationCanceledException)
        {
            c.Checkpoint();
            throw;
        }
        catch (Exception ex)when (ex is InvalidDataException || ex is NotSupportedException || ex is FormatException || ex.GetType().Assembly == typeof(WordNet.WordConverter).Assembly)
        {
            c.Checkpoint();
            throw new PdfException(new("Word.DependencyFailure", "The pinned Word dependency rejected source syntax, requested fidelity or its finite resource envelope."), ex);
        }

        c.Checkpoint();
        foreach (var diagnostic in result.Diagnostics)
        {
            c.Checkpoint();
            string code = diagnostic.Code;
            if (string.IsNullOrEmpty(code) || code.Length > 100 || code.Any(ch => ch < 33 || ch > 126))
                code = "DependencyDiagnostic";
            if (!Enum.IsDefined(typeof(WordNet.DiagnosticSeverity), diagnostic.Severity) || diagnostic.Count < 1)
                throw WordErrors.Error(c, "DependencyDiagnostic", "The pinned dependency returned an invalid diagnostic contract.");
            bool info = diagnostic.Severity == WordNet.DiagnosticSeverity.Info;
            c.Report(new("Wordnet." + code, "The pinned Word dependency reported " + diagnostic.Count.ToString(System.Globalization.CultureInfo.InvariantCulture) + " source/content occurrences; payloads and external paths are not echoed.", severity: info ? PdfDiagnosticSeverity.Information : PdfDiagnosticSeverity.Warning, lossCategory: info ? PdfLossCategory.None : PdfLossCategory.FidelityReduction));
        }

        c.Checkpoint();
    }

    internal static string Family(string family)
    {
        if (string.IsNullOrWhiteSpace(family) || family.Length > 128 || family.Any(ch => char.IsControl(ch) || ch == ';'))
            throw new ArgumentException("An explicit bounded font family without control characters or delimiters is required.", nameof(family));
        return family;
    }
}
