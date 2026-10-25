using System.Collections.ObjectModel;
using System.IO;
using System.Net;

namespace Fluent.Pdf.Intelligence;
/// <summary>Explicit model tasks; source data is never interpreted as an SDK command.</summary>
public enum PdfIntelligenceTask
{
    /// <summary>Summarize selected excerpts.</summary>
    Summarize,
    /// <summary>Answer an explicit question from selected excerpts.</summary>
    QuestionAnswer,
    /// <summary>Translate selected excerpts to an explicit language tag.</summary>
    Translate,
    /// <summary>Describe explicitly selected rendered page regions.</summary>
    DescribeImage,
    /// <summary>Recognize explicitly selected rendered page regions.</summary>
    Ocr,
    /// <summary>Extract data matching an explicit closed schema.</summary>
    StructuredExtraction
}

/// <summary>Declared model execution boundary.</summary>
public enum PdfIntelligenceLocation
{
    /// <summary>Caller-owned in-process model or explicit literal-loopback service.</summary>
    Local,
    /// <summary>Explicit remote HTTPS service requiring per-operation origin consent.</summary>
    Remote
}

/// <summary>Immutable exact model, endpoint, context ceiling and caller-configured pricing. Pricing is configuration, not verified market/billing data.</summary>
public sealed class PdfIntelligenceModel
{
    /// <summary>Captures nonsecret ASCII identities and finite configured rates per million reported tokens. Remote endpoints require HTTPS; local endpoints require a literal loopback address. Credentials/query/fragment are forbidden in the endpoint.</summary>
    public PdfIntelligenceModel(string providerId, string exactModelVersion, PdfIntelligenceLocation location = PdfIntelligenceLocation.Local, Uri? endpoint = null, int maximumContextTokens = 128000, string currency = "USD", decimal inputPricePerMillion = 0, decimal outputPricePerMillion = 0, decimal requestCharge = 0)
    {
        Identity(providerId, nameof(providerId));
        Identity(exactModelVersion, nameof(exactModelVersion));
        if (!Enum.IsDefined(typeof(PdfIntelligenceLocation), location))
            throw new ArgumentOutOfRangeException(nameof(location));
        if (maximumContextTokens < 1 || maximumContextTokens > 2000000)
            throw new ArgumentOutOfRangeException(nameof(maximumContextTokens));
        if (currency == null || currency.Length != 3 || currency.Any(ch => ch < 'A' || ch > 'Z'))
            throw new ArgumentException("An explicit uppercase currency code is required.", nameof(currency));
        Rate(inputPricePerMillion, nameof(inputPricePerMillion));
        Rate(outputPricePerMillion, nameof(outputPricePerMillion));
        Rate(requestCharge, nameof(requestCharge));
        if (location == PdfIntelligenceLocation.Remote && endpoint == null)
            throw new ArgumentException("An explicit remote HTTPS endpoint is required.", nameof(endpoint));
        if (endpoint != null)
        {
            if (!endpoint.IsAbsoluteUri || endpoint.UserInfo.Length != 0 || endpoint.Query.Length != 0 || endpoint.Fragment.Length != 0)
                throw new ArgumentException("An absolute credential-free endpoint without query/fragment is required.", nameof(endpoint));
            if (location == PdfIntelligenceLocation.Remote ? endpoint.Scheme != "https" : !IPAddress.TryParse(endpoint.Host.Trim('[', ']'), out var address) || !IPAddress.IsLoopback(address) || endpoint.Scheme != "http" && endpoint.Scheme != "https")
                throw new ArgumentException("The endpoint does not match its explicit local/remote boundary.", nameof(endpoint));
        }

        ProviderId = providerId;
        ExactModelVersion = exactModelVersion;
        Location = location;
        Endpoint = endpoint;
        MaximumContextTokens = maximumContextTokens;
        Currency = currency;
        InputPricePerMillion = inputPricePerMillion;
        OutputPricePerMillion = outputPricePerMillion;
        RequestCharge = requestCharge;
    }

    /// <summary>Gets a nonsecret pinned provider/configuration identity.</summary>
    public string ProviderId { get; }
    /// <summary>Gets the exact requested and returned model version.</summary>
    public string ExactModelVersion { get; }
    /// <summary>Gets the declared execution boundary.</summary>
    public PdfIntelligenceLocation Location { get; }
    /// <summary>Gets the explicit fixed endpoint or null for in-process execution.</summary>
    public Uri? Endpoint { get; }
    /// <summary>Gets the configured maximum input/output token context.</summary>
    public int MaximumContextTokens { get; }
    /// <summary>Gets configured cost currency.</summary>
    public string Currency { get; }
    /// <summary>Gets configured input cost per million tokens.</summary>
    public decimal InputPricePerMillion { get; }
    /// <summary>Gets configured output cost per million tokens.</summary>
    public decimal OutputPricePerMillion { get; }
    /// <summary>Gets configured fixed request charge.</summary>
    public decimal RequestCharge { get; }

    internal decimal Cost(int input, int output) => (input * InputPricePerMillion + output * OutputPricePerMillion) / 1000000m + RequestCharge;
    private static void Rate(decimal value, string name)
    {
        if (value < 0 || value > 1000000 || decimal.Round(value, 6) != value)
            throw new ArgumentOutOfRangeException(name, "A finite nonnegative configured rate with at most six fractional digits is required.");
    }

    internal static void Identity(string value, string name)
    {
        if (value == null || value.Length < 1 || value.Length > 128 || value.Any(ch => !(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || "._-+/:".IndexOf(ch) >= 0)))
            throw new ArgumentException("A bounded nonsecret ASCII identity is required.", name);
    }
}

/// <summary>One explicit zero-based page and optional displayed top-left physical-point region.</summary>
public sealed class PdfIntelligenceSelection
{
    /// <summary>Captures an explicit page and positive region; omitted region means its entire displayed CropBox.</summary>
    public PdfIntelligenceSelection(int pageIndex, PdfRect? region = null)
    {
        if (pageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (region.HasValue && (region.Value.X < 0 || region.Value.Y < 0 || region.Value.Width <= 0 || region.Value.Height <= 0))
            throw new ArgumentException("A positive displayed page region is required.", nameof(region));
        PageIndex = pageIndex;
        Region = region;
    }

    /// <summary>Gets the zero-based source page.</summary>
    public int PageIndex { get; }
    /// <summary>Gets explicit displayed capture region or null for the full crop.</summary>
    public PdfRect? Region { get; }
}

/// <summary>Immutable per-operation data/cost/token authority. Remote origin and image processing are disabled by default.</summary>
public sealed class PdfIntelligenceOptions
{
    /// <summary>Captures finite limits. A remote model requires the exact HTTPS origin here. Cost defaults to zero and uses only explicitly configured rates; it is not an account-wide billing limit. Images require separate explicit authority.</summary>
    public PdfIntelligenceOptions(Uri? allowedRemoteOrigin = null, bool allowImages = false, int maximumSourceCharacters = 65536, int maximumInputTokens = 16384, int maximumOutputTokens = 2048, decimal maximumConfiguredCost = 0, string costCurrency = "USD", int maximumOutputCharacters = 16384, int maximumCitations = 128)
    {
        if (allowedRemoteOrigin != null && (!allowedRemoteOrigin.IsAbsoluteUri || allowedRemoteOrigin.Scheme != "https" || allowedRemoteOrigin.UserInfo.Length != 0 || allowedRemoteOrigin.Query.Length != 0 || allowedRemoteOrigin.Fragment.Length != 0 || allowedRemoteOrigin.AbsolutePath != "/"))
            throw new ArgumentException("An exact credential-free HTTPS origin is required.", nameof(allowedRemoteOrigin));
        Limit(maximumSourceCharacters, 1000000, nameof(maximumSourceCharacters));
        Limit(maximumInputTokens, 1000000, nameof(maximumInputTokens));
        Limit(maximumOutputTokens, 1000000, nameof(maximumOutputTokens));
        Limit(maximumOutputCharacters, 1000000, nameof(maximumOutputCharacters));
        Limit(maximumCitations, 1024, nameof(maximumCitations));
        if (maximumConfiguredCost < 0 || maximumConfiguredCost > 1000000000)
            throw new ArgumentOutOfRangeException(nameof(maximumConfiguredCost));
        if (costCurrency == null || costCurrency.Length != 3 || costCurrency.Any(ch => ch < 'A' || ch > 'Z'))
            throw new ArgumentException("An explicit uppercase configured cost currency is required.", nameof(costCurrency));
        AllowedRemoteOrigin = allowedRemoteOrigin;
        AllowImages = allowImages;
        MaximumSourceCharacters = maximumSourceCharacters;
        MaximumInputTokens = maximumInputTokens;
        MaximumOutputTokens = maximumOutputTokens;
        MaximumConfiguredCost = maximumConfiguredCost;
        CostCurrency = costCurrency;
        MaximumOutputCharacters = maximumOutputCharacters;
        MaximumCitations = maximumCitations;
    }

    /// <summary>Gets explicit remote origin authority or null.</summary>
    public Uri? AllowedRemoteOrigin { get; }
    /// <summary>Gets explicit selected-region image processing authority.</summary>
    public bool AllowImages { get; }
    /// <summary>Gets cumulative selected text character ceiling.</summary>
    public int MaximumSourceCharacters { get; }
    /// <summary>Gets the provider's conservative input token reservation ceiling.</summary>
    public int MaximumInputTokens { get; }
    /// <summary>Gets requested and reported completion token ceiling.</summary>
    public int MaximumOutputTokens { get; }
    /// <summary>Gets per-job configured-rate reservation ceiling.</summary>
    public decimal MaximumConfiguredCost { get; }
    /// <summary>Gets exact configured cost currency; no conversion occurs.</summary>
    public string CostCurrency { get; }
    /// <summary>Gets cumulative accepted structured text character ceiling.</summary>
    public int MaximumOutputCharacters { get; }
    /// <summary>Gets maximum accepted source citations.</summary>
    public int MaximumCitations { get; }

    private static void Limit(int value, int maximum, string name)
    {
        if (value < 1 || value > maximum)
            throw new ArgumentOutOfRangeException(name);
    }
}

/// <summary>One immutable explicitly captured excerpt/image with SDK-owned page/region provenance.</summary>
public sealed class PdfIntelligenceSource
{
    internal readonly PdfByteStore? Image;
    internal PdfIntelligenceSource(string id, int page, PdfRect region, string text, string sha, PdfByteStore? image)
    {
        Id = id;
        PageIndex = page;
        Region = region;
        Text = text;
        Sha256 = sha;
        Image = image;
    }

    /// <summary>Gets request-local source identity.</summary>
    public string Id { get; }
    /// <summary>Gets exact zero-based source page.</summary>
    public int PageIndex { get; }
    /// <summary>Gets the exact displayed physical-point capture frame, not an inferred quote ink box.</summary>
    public PdfRect Region { get; }
    /// <summary>Gets explicitly selected source-order text or empty for an image.</summary>
    public string Text { get; }
    /// <summary>Gets exact captured text/image SHA256.</summary>
    public string Sha256 { get; }
    /// <summary>Gets whether the source contains explicitly authorized PNG pixels.</summary>
    public bool IsImage => Image != null;

    /// <summary>Opens a borrowed PNG lease under the active provider context. Providers must dispose it before returning.</summary>
    public Stream OpenImage(PdfOperationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (Image == null)
            throw new InvalidOperationException("This source has no captured image.");
        return context.BorrowExpandedInput(Image.Open(0, Image.Length));
    }
}

/// <summary>Immutable validated request and selected sources; supplied source text is untrusted data.</summary>
public sealed class PdfIntelligenceJob
{
    internal PdfIntelligenceJob(PdfIntelligenceTask task, string instruction, string language, PdfIntelligenceSchema schema, PdfIntelligenceSource[] sources, PdfIntelligenceOptions options, string sha)
    {
        Task = task;
        Instruction = instruction;
        TargetLanguage = language;
        Schema = schema;
        Sources = new ReadOnlyCollection<PdfIntelligenceSource>(sources);
        Options = options;
        RequestSha256 = sha;
    }

    /// <summary>Gets the explicit requested task.</summary>
    public PdfIntelligenceTask Task { get; }
    /// <summary>Gets caller-authored trusted task instructions/question.</summary>
    public string Instruction { get; }
    /// <summary>Gets explicit translation language or empty.</summary>
    public string TargetLanguage { get; }
    /// <summary>Gets exact structured data schema.</summary>
    public PdfIntelligenceSchema Schema { get; }
    /// <summary>Gets only explicitly selected source data.</summary>
    public IReadOnlyList<PdfIntelligenceSource> Sources { get; }
    /// <summary>Gets immutable per-job authority/ceilings.</summary>
    public PdfIntelligenceOptions Options { get; }
    /// <summary>Gets the exact selected-source/task/schema/request binding.</summary>
    public string RequestSha256 { get; }
}

/// <summary>A bounded model-proposed citation. The SDK resolves identity, exact unique quote and capture frame before exposing provenance.</summary>
public sealed class PdfIntelligenceCitation
{
    /// <summary>Captures a request-local source ID, JSON pointer into returned data and literal quote. Image citations require an empty quote.</summary>
    public PdfIntelligenceCitation(string sourceId, string outputPath, string quote)
    {
        if (sourceId == null || sourceId.Length < 1 || sourceId.Length > 128)
            throw new ArgumentException("A bounded source identity is required.", nameof(sourceId));
        if (outputPath == null || outputPath.Length > 1024 || outputPath.Length != 0 && outputPath[0] != '/')
            throw new ArgumentException("A bounded JSON pointer is required.", nameof(outputPath));
        if (quote == null || quote.Length > 8192)
            throw new ArgumentException("A bounded literal quote is required.", nameof(quote));
        SourceId = sourceId;
        OutputPath = outputPath;
        Quote = quote;
    }

    /// <summary>Gets the request-local source ID.</summary>
    public string SourceId { get; }
    /// <summary>Gets JSON pointer into the accepted structured data.</summary>
    public string OutputPath { get; }
    /// <summary>Gets literal source quote or empty for an image frame.</summary>
    public string Quote { get; }
}

/// <summary>Detached reported token usage and cost calculated from explicit configured rates.</summary>
[Serializable]
public sealed class PdfIntelligenceUsage
{
    /// <summary>Captures finite reported counts and configured-rate cost; backend billing accuracy requires separate qualification.</summary>
    public PdfIntelligenceUsage(int inputTokens, int outputTokens, decimal configuredCost, string currency)
    {
        if (inputTokens < 0 || inputTokens > 2000000)
            throw new ArgumentOutOfRangeException(nameof(inputTokens));
        if (outputTokens < 0 || outputTokens > 1000000)
            throw new ArgumentOutOfRangeException(nameof(outputTokens));
        if (configuredCost < 0 || configuredCost > 1000000000)
            throw new ArgumentOutOfRangeException(nameof(configuredCost));
        if (currency == null || currency.Length != 3 || currency.Any(ch => ch < 'A' || ch > 'Z'))
            throw new ArgumentException("An explicit cost currency is required.", nameof(currency));
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        ConfiguredCost = configuredCost;
        Currency = currency;
    }

    /// <summary>Gets reported input tokens.</summary>
    public int InputTokens { get; }
    /// <summary>Gets reported completion tokens.</summary>
    public int OutputTokens { get; }
    /// <summary>Gets reported tokens priced using the explicit configuration.</summary>
    public decimal ConfiguredCost { get; }
    /// <summary>Gets configured cost currency.</summary>
    public string Currency { get; }
}

/// <summary>Provider result requiring SDK schema, quota, binding and citation validation before public acceptance.</summary>
public sealed class PdfIntelligenceResponse
{
    /// <summary>Captures bounded response data and at most1024 citation proposals. It is not itself an accepted analysis result.</summary>
    public PdfIntelligenceResponse(string requestSha256, string dataJson, IEnumerable<PdfIntelligenceCitation> citations, PdfIntelligenceUsage usage)
    {
        if (requestSha256 == null || requestSha256.Length != 64 || requestSha256.Any(ch => !(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f')))
            throw new ArgumentException("An exact lowercase request SHA256 is required.", nameof(requestSha256));
        if (dataJson == null || dataJson.Length < 1 || dataJson.Length > 1048576)
            throw new ArgumentException("Bounded structured JSON is required.", nameof(dataJson));
        if (citations == null)
            throw new ArgumentNullException(nameof(citations));
        var copy = new List<PdfIntelligenceCitation>();
        foreach (var item in citations)
        {
            if (item == null || copy.Count == 1024)
                throw new ArgumentException("Bounded nonnull citations are required.", nameof(citations));
            copy.Add(item);
        }

        RequestSha256 = requestSha256;
        DataJson = dataJson;
        Citations = new ReadOnlyCollection<PdfIntelligenceCitation>(copy);
        Usage = usage ?? throw new ArgumentNullException(nameof(usage));
    }

    /// <summary>Gets provider-observed request binding.</summary>
    public string RequestSha256 { get; }
    /// <summary>Gets unaccepted structured JSON.</summary>
    public string DataJson { get; }
    /// <summary>Gets unaccepted source proposals.</summary>
    public IReadOnlyList<PdfIntelligenceCitation> Citations { get; }
    /// <summary>Gets reported usage.</summary>
    public PdfIntelligenceUsage Usage { get; }
}

/// <summary>One prepared bounded call. Preparation must be offline; execution receives the same mandatory context.</summary>
public abstract class PdfIntelligenceCall
{
    /// <summary>Captures exact immutable model/request identity and conservative input/output/cost reservations.</summary>
    protected PdfIntelligenceCall(PdfIntelligenceModel model, string requestSha256, int inputTokenCeiling, int maximumOutputTokens, decimal reservedConfiguredCost)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        if (requestSha256 == null || requestSha256.Length != 64)
            throw new ArgumentException("An exact request binding is required.", nameof(requestSha256));
        if (inputTokenCeiling < 1 || inputTokenCeiling > 2000000)
            throw new ArgumentOutOfRangeException(nameof(inputTokenCeiling));
        if (maximumOutputTokens < 1 || maximumOutputTokens > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumOutputTokens));
        if (reservedConfiguredCost < 0 || reservedConfiguredCost > 1000000000)
            throw new ArgumentOutOfRangeException(nameof(reservedConfiguredCost));
        RequestSha256 = requestSha256;
        InputTokenCeiling = inputTokenCeiling;
        MaximumOutputTokens = maximumOutputTokens;
        ReservedConfiguredCost = reservedConfiguredCost;
    }

    /// <summary>Gets the immutable captured provider model.</summary>
    public PdfIntelligenceModel Model { get; }
    /// <summary>Gets the exact prepared request binding.</summary>
    public string RequestSha256 { get; }
    /// <summary>Gets conservative prepared input token bound.</summary>
    public int InputTokenCeiling { get; }
    /// <summary>Gets the explicit completion token bound sent to the backend.</summary>
    public int MaximumOutputTokens { get; }
    /// <summary>Gets configured-rate cost reserved before execution.</summary>
    public decimal ReservedConfiguredCost { get; }

    /// <summary>Executes only this prepared fixed-destination call. Honor shared cancellation/deadline/budgets; no tools, follow-up URLs, document actions or implicit retries.</summary>
    public abstract PdfIntelligenceResponse Execute(PdfOperationContext context);
}

/// <summary>Explicit caller-owned local/remote model integration. Property/support/preparation methods must not perform I/O.</summary>
public interface IPdfDocumentIntelligenceProvider
{
    /// <summary>Gets immutable model identity/location/rates captured once per job.</summary>
    PdfIntelligenceModel Model { get; }

    /// <summary>Checks exact task/image prerequisites offline.</summary>
    bool Supports(PdfIntelligenceTask task, bool requiresImages);
    /// <summary>Prepares one request and conservative configured token/cost bounds offline using only supplied sources and the mandatory context.</summary>
    PdfIntelligenceCall Prepare(PdfIntelligenceJob job, PdfOperationContext context);
}
