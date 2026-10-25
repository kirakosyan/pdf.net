using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Fluent.Pdf.Intelligence;
/// <summary>SDK-resolved provenance. A unique text quote match or exact captured image frame does not establish semantic answer correctness.</summary>
public sealed class PdfIntelligenceEvidence
{
    internal PdfIntelligenceEvidence(PdfIntelligenceCitation citation, PdfIntelligenceSource source, int? offset)
    {
        OutputPath = citation.OutputPath;
        SourceId = source.Id;
        SourceSha256 = source.Sha256;
        PageIndex = source.PageIndex;
        CaptureRegion = source.Region;
        Quote = citation.Quote;
        TextOffset = offset;
    }

    /// <summary>Gets cited JSON pointer into accepted output.</summary>
    public string OutputPath { get; }
    /// <summary>Gets validated request-local source identity.</summary>
    public string SourceId { get; }
    /// <summary>Gets exact captured source SHA256.</summary>
    public string SourceSha256 { get; }
    /// <summary>Gets the zero-based source PDF page.</summary>
    public int PageIndex { get; }
    /// <summary>Gets the exact displayed physical-point capture region, not inferred quote ink geometry.</summary>
    public PdfRect CaptureRegion { get; }
    /// <summary>Gets an exact uniquely matched quote or empty for an image.</summary>
    public string Quote { get; }
    /// <summary>Gets quote UTF16 offset in captured text, or null for an image.</summary>
    public int? TextOffset { get; }
}

/// <summary>Detached schema/quota/binding/provenance-validated result. Model factual, linguistic and OCR quality require separate review.</summary>
public sealed class PdfIntelligenceResult
{
    internal PdfIntelligenceResult(string json, string request, PdfIntelligenceModel model, PdfIntelligenceUsage usage, PdfIntelligenceEvidence[] evidence, PdfOperationReport report)
    {
        DataJson = json;
        RequestSha256 = request;
        Model = model;
        Usage = usage;
        Evidence = new ReadOnlyCollection<PdfIntelligenceEvidence>(evidence);
        OperationReport = report;
    }

    /// <summary>Gets canonical accepted structured JSON data, never automatically executed/rendered as markup.</summary>
    public string DataJson { get; }
    /// <summary>Gets exact request/source/schema binding.</summary>
    public string RequestSha256 { get; }
    /// <summary>Gets the exact caller-selected model configuration.</summary>
    public PdfIntelligenceModel Model { get; }
    /// <summary>Gets reported bounded tokens and configured-rate cost.</summary>
    public PdfIntelligenceUsage Usage { get; }
    /// <summary>Gets resolved source evidence.</summary>
    public IReadOnlyList<PdfIntelligenceEvidence> Evidence { get; }
    /// <summary>Gets complete diagnostics and shared resource accounting.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Explicit selection, privacy authority, bounded provider execution and structured source validation under one root context.</summary>
public static class PdfDocumentIntelligence
{
    /// <summary>Analyzes explicitly selected mapped text or authorized image regions. Text excludes hidden layers/nonpainting modes but does not establish visible ink or remove concealed content. No provider or remote origin is selected by default.</summary>
    public static PdfIntelligenceResult Analyze(PdfDocumentSnapshot source, IEnumerable<PdfIntelligenceSelection> selections, PdfIntelligenceTask task, IPdfDocumentIntelligenceProvider? provider = null, string instruction = "", string targetLanguage = "", PdfIntelligenceSchema? schema = null, PdfIntelligenceOptions? options = null, PdfProviderRegistry? providers = null, PdfRenderOptions? render = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (selections == null)
            throw new ArgumentNullException(nameof(selections));
        if (!Enum.IsDefined(typeof(PdfIntelligenceTask), task))
            throw new ArgumentOutOfRangeException(nameof(task));
        if (instruction == null || instruction.Length > 8192 || task == PdfIntelligenceTask.QuestionAnswer && string.IsNullOrWhiteSpace(instruction))
            throw new ArgumentException("A bounded trusted task instruction/question is required.", nameof(instruction));
        if (targetLanguage == null || (task == PdfIntelligenceTask.Translate ? targetLanguage.Length < 1 || targetLanguage.Length > 35 || targetLanguage.Any(ch => !(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || ch == '-')) : targetLanguage.Length != 0))
            throw new ArgumentException("An explicit bounded language tag is required only for translation.", nameof(targetLanguage));
        if (task == PdfIntelligenceTask.StructuredExtraction && schema == null)
            throw new ArgumentException("Structured extraction requires an explicit closed schema.", nameof(schema));
        if (task != PdfIntelligenceTask.StructuredExtraction && schema != null)
            throw new ArgumentException("An explicit custom schema belongs to the structured-extraction task.", nameof(schema));
        var selected = new List<PdfIntelligenceSelection>();
        foreach (var item in selections)
        {
            if (item == null || selected.Count == 32 || item.PageIndex >= source.Pages.Count || selected.Any(prior => prior.PageIndex == item.PageIndex && Equals(prior.Region, item.Region)))
                throw new ArgumentException("At most32 valid distinct explicit page/region selections are required.", nameof(selections));
            selected.Add(item);
        }

        if (selected.Count == 0)
            throw new ArgumentException("At least one explicit source selection is required.", nameof(selections));
        options ??= new();
        providers ??= PdfProviderRegistry.Empty;
        bool images = task == PdfIntelligenceTask.DescribeImage || task == PdfIntelligenceTask.Ocr;
        schema ??= DefaultSchema(task, targetLanguage, options.MaximumOutputCharacters);
        if (schema.Kind != PdfIntelligenceValueKind.Object)
            throw new ArgumentException("A closed object result schema is required.", nameof(schema));
        render ??= new(96, 96, annotations: false);
        if (images && render.Region.HasValue)
            throw new ArgumentException("Image regions are explicit per-source selections; do not supply a second render region.", nameof(render));
        PdfIntelligenceModel? model = null;
        PdfIntelligenceResponse? response = null;
        string accepted = "", request = "";
        var evidence = new List<PdfIntelligenceEvidence>();
        var report = PdfFont.Run("AnalyzeDocument", "explicit.intelligence", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            if (provider == null)
                throw Error(c, "ProviderRequired", "An explicitly configured local or remote model provider is required.");
            try
            {
                model = provider.Model;
                c.Checkpoint();
            }
            catch
            {
                c.Checkpoint();
                throw Error(c, "ProviderFailed", "The configured model identity check failed.");
            }

            if (model == null)
                throw Error(c, "ProviderIdentity", "A complete immutable model identity is required.");
            if (model.Location == PdfIntelligenceLocation.Remote && (options.AllowedRemoteOrigin == null || Origin(model.Endpoint!) != Origin(options.AllowedRemoteOrigin)))
                throw Error(c, "RemoteConsent", "The exact remote processing origin must be explicitly authorized for this operation.");
            if (images && !options.AllowImages)
                throw Error(c, "ImageConsent", "Selected-region image processing requires separate explicit authority.");
            if (model.Currency != options.CostCurrency)
                throw Error(c, "CostCurrency", "Model pricing and explicit cost ceiling currencies must match without conversion.");
            bool supported;
            try
            {
                supported = provider.Supports(task, images);
                c.Checkpoint();
            }
            catch
            {
                c.Checkpoint();
                throw Error(c, "ProviderFailed", "The exact model prerequisite check failed.");
            }

            if (!supported)
                throw Error(c, "TaskUnavailable", "The exact task/image/model profile is unavailable; no fallback was selected.");
            c.Report(new("Intelligence.ModelIdentity", "Explicit provider/model: " + model.ProviderId + "/" + model.ExactModelVersion + ".", severity: PdfDiagnosticSeverity.Information));
            var declaration = schema.Declaration(c);
            var sources = Capture(source, selected, images, options, providers, render, c);
            var binding = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["version"] = 1m,
                ["task"] = task.ToString(),
                ["instruction"] = instruction,
                ["language"] = targetLanguage,
                ["provider"] = model.ProviderId,
                ["model"] = model.ExactModelVersion,
                ["schema"] = declaration,
                ["location"] = model.Location.ToString(),
                ["endpoint"] = model.Endpoint?.AbsoluteUri,
                ["inputPricePerMillion"] = model.InputPricePerMillion,
                ["outputPricePerMillion"] = model.OutputPricePerMillion,
                ["requestCharge"] = model.RequestCharge,
                ["maximumContextTokens"] = (decimal)model.MaximumContextTokens,
                ["allowImages"] = options.AllowImages,
                ["maximumSourceCharacters"] = (decimal)options.MaximumSourceCharacters,
                ["maximumOutputCharacters"] = (decimal)options.MaximumOutputCharacters,
                ["maximumCitations"] = (decimal)options.MaximumCitations,
                ["maximumInputTokens"] = (decimal)options.MaximumInputTokens,
                ["maximumOutputTokens"] = (decimal)options.MaximumOutputTokens,
                ["maximumCost"] = options.MaximumConfiguredCost,
                ["currency"] = options.CostCurrency,
                ["sources"] = sources.Select(item => (object? )SourceDescriptor(item)).ToList()
            };
            request = PdfArchival.Digest(IntelligenceJson.Bytes(binding, c), c);
            var job = new PdfIntelligenceJob(task, instruction, targetLanguage, schema, sources, options, request);
            PdfIntelligenceCall call;
            try
            {
                call = provider.Prepare(job, c);
                c.Checkpoint();
            }
            catch
            {
                c.Checkpoint();
                throw Error(c, "PrepareFailed", "The explicitly configured offline model preparation failed.");
            }

            if (call == null || !ReferenceEquals(call.Model, model) || call.RequestSha256 != request || call.MaximumOutputTokens != options.MaximumOutputTokens)
                throw Error(c, "CallBinding", "The prepared call changed the exact model, request or token ceiling.");
            if (call.InputTokenCeiling > options.MaximumInputTokens || (long)call.InputTokenCeiling + call.MaximumOutputTokens > model.MaximumContextTokens)
                throw Error(c, "TokenLimit", "Conservative prepared input/output tokens exceed the explicit job or model context ceiling.");
            if (call.ReservedConfiguredCost != model.Cost(call.InputTokenCeiling, call.MaximumOutputTokens) || call.ReservedConfiguredCost > options.MaximumConfiguredCost)
                throw Error(c, "CostLimit", "Conservative configured-rate cost exceeds its explicit ceiling or is not bound to the prepared token limits.");
            try
            {
                response = call.Execute(c);
                c.Checkpoint();
            }
            catch
            {
                c.Checkpoint();
                throw Error(c, "ExecutionFailed", "The explicitly configured model execution failed without exposing partial results.");
            }

            if (response == null || response.RequestSha256 != request)
                throw Error(c, "ResponseBinding", "The model result belongs to a different exact request/source/schema.");
            var usage = response.Usage;
            if (usage.InputTokens > call.InputTokenCeiling || usage.OutputTokens > call.MaximumOutputTokens || usage.Currency != model.Currency || usage.ConfiguredCost != model.Cost(usage.InputTokens, usage.OutputTokens) || usage.ConfiguredCost > call.ReservedConfiguredCost)
                throw Error(c, "UsageBinding", "Reported token/cost usage exceeds or disagrees with the explicit prepared reservation.");
            var data = IntelligenceJson.Parse(response.DataJson, c);
            schema.Validate(data, c);
            if (IntelligenceJson.Characters(data, c) > options.MaximumOutputCharacters)
                throw Error(c, "OutputCharacters", "Structured result text exceeds its explicit character ceiling.");
            if (response.Citations.Count == 0 || response.Citations.Count > options.MaximumCitations)
                throw Error(c, "CitationLimit", "A nonempty bounded source citation inventory is required.");
            foreach (var citation in response.Citations)
            {
                c.Checkpoint();
                c.Consume(PdfResourceKind.ExpandedBytes, citation.Quote.Length * 4L + citation.OutputPath.Length * 4L + 512);
                c.Consume(PdfResourceKind.Objects, 1);
                IntelligenceJson.Pointer(data, citation.OutputPath, c);
                var origin = sources.FirstOrDefault(item => item.Id == citation.SourceId) ?? throw Error(c, "CitationSource", "A proposed citation references an unselected source.");
                int? offset = null;
                if (origin.IsImage)
                {
                    if (citation.Quote.Length != 0)
                        throw Error(c, "ImageCitation", "Image provenance is its exact capture frame; unverified text quotes cannot be asserted.");
                }
                else
                {
                    int at = citation.Quote.Length == 0 ? -1 : origin.Text.IndexOf(citation.Quote, StringComparison.Ordinal);
                    if (at < 0 || origin.Text.IndexOf(citation.Quote, at + 1, StringComparison.Ordinal) >= 0)
                        throw Error(c, "CitationQuote", "A text citation must match one exact unique quote in its selected source.");
                    offset = at;
                }

                evidence.Add(new(citation, origin, offset));
            }

            RequireCoverage(data, "", evidence, c);
            accepted = IntelligenceJson.Text(data, c);
            c.Consume(PdfResourceKind.OutputBytes, new UTF8Encoding(false, true).GetByteCount(accepted));
        }, PdfCapability.ReadDocument);
        return new(accepted, request, model!, response!.Usage, evidence.ToArray(), report);
    }

    internal static Dictionary<string, object?> SourceDescriptor(PdfIntelligenceSource source) => new(StringComparer.Ordinal)
    {
        ["source_id"] = source.Id,
        ["page_index"] = (decimal)source.PageIndex,
        ["source_sha256"] = source.Sha256,
        ["kind"] = source.IsImage ? "image" : "text",
        ["capture_region"] = new List<object?>
        {
            source.Region.X.ToString("R", CultureInfo.InvariantCulture),
            source.Region.Y.ToString("R", CultureInfo.InvariantCulture),
            source.Region.Width.ToString("R", CultureInfo.InvariantCulture),
            source.Region.Height.ToString("R", CultureInfo.InvariantCulture)
        }
    };
    private static PdfIntelligenceSource[] Capture(PdfDocumentSnapshot snapshot, List<PdfIntelligenceSelection> selections, bool images, PdfIntelligenceOptions options, PdfProviderRegistry providers, PdfRenderOptions render, PdfOperationContext c)
    {
        if (images && !render.BackgroundRgb.HasValue)
            throw Error(c, "RenderAlpha", "Image model inputs require explicit opaque rendered pixels.");
        if (images)
            _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
        using var graph = new PdfGraphEdit(snapshot, new(allowSignatureInvalidation: true), c);
        var extractor = images ? null : new PdfTextExtractionEngine(graph, new(includeHiddenLayers: false, includeInvisibleText: false), providers);
        c.Consume(PdfResourceKind.ExpandedBytes, selections.Count * 512L);
        var result = new List<PdfIntelligenceSource>();
        long total = 0;
        foreach (var selection in selections)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            var geometry = graph.Snapshot.Pages[selection.PageIndex].Geometry;
            var region = selection.Region ?? geometry.DisplayBounds();
            if (!PdfPageBoxes.Contains(geometry.DisplayBounds(), region))
                throw Error(c, "SourceRegion", "A selected source frame lies outside the exact displayed CropBox.");
            string text = "", digest;
            PdfByteStore? png = null;
            if (images)
            {
                var choice = new PdfRenderOptions(render.DpiX, render.DpiY, region: region, color: render.Color, backgroundRgb: render.BackgroundRgb, annotations: render.Annotations, print: render.Print);
                var captured = PdfRendering.RenderCore(graph, selection.PageIndex, providers, choice, c);
                using var encoded = new PdfCodecBufferStream(c, 8388608);
                using (var output = new BorrowedStream(encoded, false, c))
                    PdfRasterExport.Png(output, captured.Request, captured.Samples, null, c);
                png = encoded.Finish();
                region = captured.Request.Region;
                digest = PdfArchival.Digest(png, c);
            }
            else
            {
                var page = extractor!.Read(selection.PageIndex);
                if (page.Characters.Any(character => character.GeometryEstimated))
                    throw Error(c, "SelectionGeometry", "Source-region privacy requires declared bounded character geometry; estimated text geometry rejects.");
                var selected = page.Characters.Where(character => character.Quad.Bounds.Width > 0 && character.Quad.Bounds.Height > 0 && PdfPageBoxes.Contains(region, character.Quad.Bounds)).OrderBy(character => character.Index);
                c.Consume(PdfResourceKind.ExpandedBytes, Math.Min(options.MaximumSourceCharacters, page.Raw.Length) * 4L + 256);
                var builder = new StringBuilder();
                int prior = -1;
                foreach (var character in selected)
                {
                    c.Checkpoint();
                    int separator = prior >= 0 && character.Index != prior + 1 ? 1 : 0;
                    if (total + builder.Length + separator + character.Text.Length > options.MaximumSourceCharacters)
                        throw Error(c, "SourceCharacters", "Selected source text exceeds its explicit cumulative character ceiling.");
                    if (separator != 0)
                        builder.Append(' ');
                    builder.Append(character.Text);
                    prior = character.Index;
                }

                text = builder.ToString();
                total += text.Length;
                if (text.Length == 0)
                    throw Error(c, "EmptySource", "The explicit text selection contains no supported mapped text; no OCR fallback is implicit.");
                digest = PdfArchival.Digest(IntelligenceJson.Utf8(text, c), c);
            }

            result.Add(new("S" + result.Count.ToString(CultureInfo.InvariantCulture), selection.PageIndex, region, text, digest, png));
        }

        return result.ToArray();
    }

    private static PdfIntelligenceSchema DefaultSchema(PdfIntelligenceTask task, string language, int maximum)
    {
        var fields = new List<PdfIntelligenceField>
        {
            new("text", PdfIntelligenceSchema.String(1, maximum))
        };
        if (task == PdfIntelligenceTask.Translate)
            fields.Add(new("language", PdfIntelligenceSchema.String(1, 35, new[] { language })));
        return PdfIntelligenceSchema.Object(fields);
    }

    private static string Origin(Uri uri) => uri.GetLeftPart(UriPartial.Authority).ToLowerInvariant();
    private static void RequireCoverage(object? value, string path, List<PdfIntelligenceEvidence> evidence, PdfOperationContext c)
    {
        c.Checkpoint();
        using var scope = c.EnterScope(PdfResourceKind.Depth);
        if (value == null || evidence.Any(item => item.OutputPath.Length == 0 || item.OutputPath == path || path.StartsWith(item.OutputPath + "/", StringComparison.Ordinal)))
            return;
        if (value is Dictionary<string, object?> members && members.Count != 0)
        {
            foreach (var pair in members)
                RequireCoverage(pair.Value, path + "/" + pair.Key.Replace("~", "~0").Replace("/", "~1"), evidence, c);
            return;
        }

        if (value is List<object?> items && items.Count != 0)
        {
            for (int n = 0; n < items.Count; n++)
                RequireCoverage(items[n], path + "/" + n.ToString(CultureInfo.InvariantCulture), evidence, c);
            return;
        }

        throw Error(c, "CitationCoverage", "Every substantive output value requires validated source provenance.");
    }

    internal static PdfException Error(PdfOperationContext c, string code, string message)
    {
        var diagnostic = new PdfDiagnostic("Intelligence." + code, message, PdfCapability.ReadDocument, "explicit.intelligence");
        c.Report(diagnostic);
        return new(diagnostic);
    }
}
