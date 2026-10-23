using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf.Intelligence;
/// <summary>Exact embedding identity, dimensions and caller-configured execution/pricing boundary.</summary>
public sealed class PdfEmbeddingModel
{
    /// <summary>Captures a pinned model and output dimension. Distinct versions or dimensions cannot share an index.</summary>
    public PdfEmbeddingModel(PdfIntelligenceModel model, int dimensions)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        if (dimensions < 1 || dimensions > 4096)
            throw new ArgumentOutOfRangeException(nameof(dimensions));
        Dimensions = dimensions;
    }

    /// <summary>Gets immutable exact provider/model/endpoint/pricing configuration.</summary>
    public PdfIntelligenceModel Model { get; }
    /// <summary>Gets exact output vector length.</summary>
    public int Dimensions { get; }

    internal bool Matches(PdfEmbeddingModel other) => Dimensions == other.Dimensions && Model.ProviderId == other.Model.ProviderId && Model.ExactModelVersion == other.Model.ExactModelVersion && Model.Location == other.Model.Location && Equals(Model.Endpoint, other.Model.Endpoint);
}

/// <summary>Immutable exact input batch and request binding. Preparation must remain offline.</summary>
public sealed class PdfEmbeddingRequest
{
    internal PdfEmbeddingRequest(string[] texts, string hash)
    {
        Texts = new ReadOnlyCollection<string>(texts);
        RequestSha256 = hash;
    }

    /// <summary>Gets bounded raw text inputs in required response order.</summary>
    public IReadOnlyList<string> Texts { get; }
    /// <summary>Gets the exact model/input/scope request hash.</summary>
    public string RequestSha256 { get; }
}

/// <summary>Explicit optional local/remote embedding adapter; implementations must honor shared budgets, cancellation and network authority.</summary>
public interface IPdfEmbeddingProvider
{
    /// <summary>Gets immutable pinned identity and dimensionality without network access.</summary>
    PdfEmbeddingModel Model { get; }

    /// <summary>Prepares one request offline with conservative complete-input token/cost reservation.</summary>
    PdfEmbeddingCall Prepare(PdfEmbeddingRequest request, PdfOperationContext context);
}

/// <summary>Prepared exact embedding dispatch, with no implicit retry or model selection.</summary>
public abstract class PdfEmbeddingCall
{
    /// <summary>Binds an offline call to exact model/request, positive input ceiling and configured cost.</summary>
    protected PdfEmbeddingCall(PdfEmbeddingModel model, string requestSha256, int inputTokenCeiling, decimal configuredCostCeiling)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        PdfRetrievalScope.Hash(requestSha256, nameof(requestSha256));
        if (inputTokenCeiling < 1 || inputTokenCeiling > 2000000 || configuredCostCeiling < 0)
            throw new ArgumentOutOfRangeException(nameof(inputTokenCeiling));
        RequestSha256 = requestSha256;
        InputTokenCeiling = inputTokenCeiling;
        ConfiguredCostCeiling = configuredCostCeiling;
    }

    /// <summary>Gets exact immutable provider model.</summary>
    public PdfEmbeddingModel Model { get; }
    /// <summary>Gets input/model/scope binding.</summary>
    public string RequestSha256 { get; }
    /// <summary>Gets reserved conservative token ceiling.</summary>
    public int InputTokenCeiling { get; }
    /// <summary>Gets reserved configured-rate cost ceiling.</summary>
    public decimal ConfiguredCostCeiling { get; }

    /// <summary>Executes once under the supplied active shared context. No implicit follow-up, retry or authority expansion is permitted.</summary>
    public abstract PdfEmbeddingResponse Execute(PdfOperationContext context);
}

/// <summary>Finite nonzero vector, privately copied under explicit resource accounting.</summary>
public sealed class PdfEmbeddingVector
{
    /// <summary>Copies 1..4096 finite values. Zero vectors reject because cosine similarity is undefined.</summary>
    public PdfEmbeddingVector(IEnumerable<double> values, PdfOperationContext context)
    {
        if (values == null)
            throw new ArgumentNullException(nameof(values));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        var list = new List<double>();
        double maximum = 0;
        foreach (double value in values)
        {
            context.Checkpoint();
            if (list.Count == 4096 || double.IsNaN(value) || double.IsInfinity(value))
                throw PdfSemanticChunks.Error(context, "Vector", "An embedding vector exceeds finite bounds.");
            context.Consume(PdfResourceKind.ExpandedBytes, 32);
            list.Add(value);
            maximum = Math.Max(maximum, Math.Abs(value));
        }

        if (maximum == 0)
            throw PdfSemanticChunks.Error(context, "Vector", "An embedding vector must be nonempty and nonzero.");
        context.Consume(PdfResourceKind.Objects, 1);
        Values = new ReadOnlyCollection<double>(list.ToArray());
    }

    /// <summary>Gets immutable finite original values; the store normalizes only during cosine scoring.</summary>
    public IReadOnlyList<double> Values { get; }
}

/// <summary>One exact ordered model response and reported input usage.</summary>
public sealed class PdfEmbeddingResponse
{
    /// <summary>Captures a bounded immutable response; SDK dispatch independently verifies binding/count/dimensions/usage.</summary>
    public PdfEmbeddingResponse(string requestSha256, string exactModelVersion, IEnumerable<PdfEmbeddingVector> vectors, int inputTokens, PdfOperationContext context)
    {
        PdfRetrievalScope.Hash(requestSha256, nameof(requestSha256));
        PdfIntelligenceModel.Identity(exactModelVersion, nameof(exactModelVersion));
        if (vectors == null)
            throw new ArgumentNullException(nameof(vectors));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (inputTokens < 0 || inputTokens > 2000000)
            throw new ArgumentOutOfRangeException(nameof(inputTokens));
        var list = new List<PdfEmbeddingVector>();
        foreach (var vector in vectors)
        {
            context.Checkpoint();
            if (vector == null || list.Count == 4096)
                throw PdfSemanticChunks.Error(context, "Batch", "A bounded nonnull vector batch is required.");
            context.Consume(PdfResourceKind.ExpandedBytes, 32);
            list.Add(vector);
        }

        RequestSha256 = requestSha256;
        ExactModelVersion = exactModelVersion;
        Vectors = new ReadOnlyCollection<PdfEmbeddingVector>(list.ToArray());
        InputTokens = inputTokens;
    }

    /// <summary>Gets exact request binding.</summary>
    public string RequestSha256 { get; }
    /// <summary>Gets reported exact model version.</summary>
    public string ExactModelVersion { get; }
    /// <summary>Gets ordered immutable vectors.</summary>
    public IReadOnlyList<PdfEmbeddingVector> Vectors { get; }
    /// <summary>Gets bounded reported input usage, not verified backend billing.</summary>
    public int InputTokens { get; }
}

internal static class PdfEmbeddingExecution
{
    internal static PdfEmbeddingResponse Run(string[] texts, string scopeBinding, IPdfEmbeddingProvider provider, PdfEmbeddingModel model, PdfIntelligenceOptions options, PdfOperationContext c)
    {
        var config = model.Model;
        if (config.Location == PdfIntelligenceLocation.Remote && (options.AllowedRemoteOrigin == null || config.Endpoint!.GetLeftPart(UriPartial.Authority).ToLowerInvariant() != options.AllowedRemoteOrigin.GetLeftPart(UriPartial.Authority).ToLowerInvariant()))
            throw PdfSemanticChunks.Error(c, "RemoteAuthority", "The exact remote embedding origin requires explicit per-operation consent.");
        if (options.CostCurrency != config.Currency)
            throw PdfSemanticChunks.Error(c, "Currency", "Embedding cost currency must match the exact model configuration.");
        long total = 0;
        foreach (string text in texts)
        {
            c.Checkpoint();
            total += text.Length;
            if (text.Length == 0 || total > options.MaximumSourceCharacters)
                throw PdfSemanticChunks.Error(c, "Characters", "Embedding text is empty or exceeds its cumulative ceiling.");
        }

        c.Consume(PdfResourceKind.ExpandedBytes, total * 4 + texts.Length * 64L);
        var descriptor = new Dictionary<string, object?>
        {
            ["scope"] = scopeBinding,
            ["provider"] = config.ProviderId,
            ["model"] = config.ExactModelVersion,
            ["dimensions"] = (decimal)model.Dimensions,
            ["endpoint"] = config.Endpoint?.AbsoluteUri,
            ["texts"] = texts.Select(text => (object? )text).ToList()
        };
        string hash = PdfArchival.Digest(IntelligenceJson.Bytes(descriptor, c), c);
        PdfEmbeddingCall call;
        try
        {
            call = provider.Prepare(new(texts, hash), c);
            c.Checkpoint();
        }
        catch
        {
            c.Checkpoint();
            throw PdfSemanticChunks.Error(c, "PrepareFailed", "Explicit offline embedding preparation failed.");
        }

        if (call == null || !ReferenceEquals(call.Model, model) || call.RequestSha256 != hash)
            throw PdfSemanticChunks.Error(c, "CallBinding", "Prepared embedding identity or request binding disagrees.");
        if (call.InputTokenCeiling > options.MaximumInputTokens || call.InputTokenCeiling > config.MaximumContextTokens)
            throw PdfSemanticChunks.Error(c, "TokenLimit", "Prepared embedding input exceeds its reserved token ceiling.");
        if (call.ConfiguredCostCeiling != config.Cost(call.InputTokenCeiling, 0) || call.ConfiguredCostCeiling > options.MaximumConfiguredCost)
            throw PdfSemanticChunks.Error(c, "CostLimit", "Embedding configured-rate reservation exceeds its explicit ceiling.");
        PdfEmbeddingResponse response;
        try
        {
            response = call.Execute(c);
            c.Checkpoint();
        }
        catch
        {
            c.Checkpoint();
            throw PdfSemanticChunks.Error(c, "DispatchFailed", "The explicitly configured embedding dispatch failed; no automatic retry occurred.");
        }

        if (response == null || response.RequestSha256 != hash || response.ExactModelVersion != config.ExactModelVersion || response.Vectors.Count != texts.Length || response.InputTokens > call.InputTokenCeiling || response.Vectors.Any(vector => vector.Values.Count != model.Dimensions))
            throw PdfSemanticChunks.Error(c, "ResponseBinding", "Embedding response identity, count, dimensions or reported usage disagree.");
        return response;
    }
}

/// <summary>Original fixed-endpoint float-array JSON embedding bridge. No implicit transport, credential lookup, model assets or retries.</summary>
public sealed class PdfJsonEmbeddingProvider : IPdfEmbeddingProvider
{
    private readonly IPdfIntelligenceTransport _transport;
    private readonly int _tokensPerByte;
    /// <summary>Captures an exact endpoint/model and explicit caller transport. The 1..16 byte-token ceiling must be qualified for that backend.</summary>
    public PdfJsonEmbeddingProvider(PdfEmbeddingModel model, IPdfIntelligenceTransport transport, int maximumTokensPerUtf8Byte = 1)
    {
        Model = model ?? throw new ArgumentNullException(nameof(model));
        if (model.Model.Endpoint == null)
            throw new ArgumentException("An explicit embedding endpoint is required.", nameof(model));
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        if (maximumTokensPerUtf8Byte < 1 || maximumTokensPerUtf8Byte > 16)
            throw new ArgumentOutOfRangeException(nameof(maximumTokensPerUtf8Byte));
        _tokensPerByte = maximumTokensPerUtf8Byte;
    }

    /// <summary>Gets the pinned model/dimension boundary.</summary>
    public PdfEmbeddingModel Model { get; }

    /// <summary>Serializes one offline bounded model/input/dimensions request and reserves its entire wire length conservatively.</summary>
    public PdfEmbeddingCall Prepare(PdfEmbeddingRequest request, PdfOperationContext context)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        var wire = IntelligenceJson.Bytes(new Dictionary<string, object?> { ["model"] = Model.Model.ExactModelVersion, ["input"] = request.Texts.Select(text => (object? )text).ToList(), ["dimensions"] = (decimal)Model.Dimensions, ["encoding_format"] = "float" }, context);
        long bound = wire.Length * _tokensPerByte;
        if (bound > 2000000)
            throw PdfSemanticChunks.Error(context, "TokenLimit", "The complete prepared embedding wire exceeds its token ceiling.");
        return new Call(Model, request.RequestSha256, checked((int)Math.Max(1, bound)), wire, _transport);
    }

    private sealed class Call : PdfEmbeddingCall
    {
        private readonly PdfByteStore _wire;
        private readonly IPdfIntelligenceTransport _transport;
        internal Call(PdfEmbeddingModel model, string hash, int tokens, PdfByteStore wire, IPdfIntelligenceTransport transport) : base(model, hash, tokens, model.Model.Cost(tokens, 0))
        {
            _wire = wire;
            _transport = transport;
        }

        private static int BoundedInteger(object? value, int maximum, PdfOperationContext c)
        {
            int number = IntelligenceJson.Integer(value, c);
            if (number > maximum)
                throw PdfSemanticChunks.Error(c, "IntegerRange", "A response integer exceeds its declared bound.");
            return number;
        }

        public override PdfEmbeddingResponse Execute(PdfOperationContext c)
        {
            if (c == null)
                throw new ArgumentNullException(nameof(c));
            c.Checkpoint();
            Stream? response = null;
            try
            {
                c.Consume(PdfResourceKind.OutputBytes, _wire.Length);
                using (var raw = _wire.Open(0, _wire.Length))
                using (var request = new BorrowedStream(raw, true, c, inputResource: null))
                    response = _transport.OpenResponse(Model.Model.Endpoint!, request, _wire.Length, c);
                c.Checkpoint();
                if (response == null || !response.CanRead)
                    throw PdfSemanticChunks.Error(c, "Response", "The transport must return an SDK-owned readable response.");
                var bytes = PdfProfileIo.Capture(response, c, 2097152);
                string json;
                c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 4 + 256);
                try
                {
                    using var input = bytes.Open(0, bytes.Length);
                    using var reader = new StreamReader(input, new UTF8Encoding(false, true), false);
                    json = reader.ReadToEnd();
                }
                catch (DecoderFallbackException)
                {
                    throw PdfSemanticChunks.Error(c, "Unicode", "The embedding response must be strict UTF8.");
                }

                var root = IntelligenceJson.Object(IntelligenceJson.Parse(json, c), c);
                IntelligenceJson.Keys(root, c, "object", "model", "data", "usage");
                if (IntelligenceJson.String(IntelligenceJson.Get(root, "model", c), c) != Model.Model.ExactModelVersion)
                    throw PdfSemanticChunks.Error(c, "Model", "The embedding response model version disagrees.");
                var data = IntelligenceJson.Array(IntelligenceJson.Get(root, "data", c), c);
                if (data.Count < 1 || data.Count > 4096)
                    throw PdfSemanticChunks.Error(c, "Batch", "The response batch is outside finite bounds.");
                c.Consume(PdfResourceKind.ExpandedBytes, data.Count * 16L);
                var vectors = new PdfEmbeddingVector[data.Count];
                foreach (var value in data)
                {
                    c.Checkpoint();
                    var item = IntelligenceJson.Object(value, c);
                    IntelligenceJson.Keys(item, c, "object", "index", "embedding");
                    int index = BoundedInteger(IntelligenceJson.Get(item, "index", c), data.Count - 1, c);
                    if (vectors[index] != null)
                        throw PdfSemanticChunks.Error(c, "Index", "Duplicate embedding response indices reject.");
                    var numbers = IntelligenceJson.Array(IntelligenceJson.Get(item, "embedding", c), c);
                    if (numbers.Count != Model.Dimensions)
                        throw PdfSemanticChunks.Error(c, "Dimensions", "Returned embedding dimensions disagree.");
                    vectors[index] = new(numbers.Select(number => number is decimal exact ? (double)exact : throw PdfSemanticChunks.Error(c, "Vector", "Embedding values must be finite JSON numbers.")), c);
                }

                var usage = IntelligenceJson.Object(IntelligenceJson.Get(root, "usage", c), c);
                IntelligenceJson.Keys(usage, c, "prompt_tokens", "total_tokens");
                int tokens = BoundedInteger(IntelligenceJson.Get(usage, "prompt_tokens", c), InputTokenCeiling, c);
                if (BoundedInteger(IntelligenceJson.Get(usage, "total_tokens", c), InputTokenCeiling, c) != tokens)
                    throw PdfSemanticChunks.Error(c, "Usage", "Embedding token totals disagree.");
                return new(RequestSha256, Model.Model.ExactModelVersion, vectors, tokens, c);
            }
            finally
            {
                response?.Dispose();
                c.Checkpoint();
            }
        }
    }
}
