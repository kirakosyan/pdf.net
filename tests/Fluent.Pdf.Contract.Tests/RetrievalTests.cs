using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fluent.Pdf.Intelligence;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class RetrievalTests
{
    private static PdfDocument Document(bool table = false, string? first = null)
    {
        using var f = new SyntaxFixtures();
        var entries = new List<(int, int, long, bool)>
        {
            (0, 65535, 0, true)
        };
        void Object(int id, string body) => entries.Add((id, 0, f.Object(id, body), false));
        void Stream(int id, string body) => entries.Add((id, 0, f.StreamObject(id, "", Encoding.ASCII.GetBytes(body)), false));
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, "<< /Type /Pages /Kids [3 0 R 8 0 R 10 0 R] /Count 3 >>");
        void Page(int id, int content) => Object(id, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 400 200] /Resources << /Font << /F1 4 0 R >> >> /Contents " + content + " 0 R >>");
        Page(3, 7);
        Page(8, 9);
        Page(10, 11);
        Object(4, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /FirstChar 1 /LastChar 126 /Widths [" + string.Join(" ", Enumerable.Repeat("500", 126)) + "] /ToUnicode 5 0 R /FontDescriptor 6 0 R >>");
        Stream(5, "begincmap 1 begincodespacerange <00> <ff> endcodespacerange 1 beginbfrange <20> <7e> <0020> endbfrange 4 beginbfchar <01> <D83DDE00> <02> <000D> <03> <000A> <04> <00660069> endbfchar endcmap");
        Object(6, "<< /Type /FontDescriptor /FontName /Helvetica /Flags 32 /FontBBox [0 -200 1000 800] /Ascent 800 /Descent -200 /CapHeight 700 /ItalicAngle 0 /StemV 80 >>");
        string Text(string value, int x, int y) => "BT /F1 10 Tf 1 0 0 1 " + x + " " + y + " Tm (" + value + ") Tj ET\n";
        Stream(7, table ? Text("Title", 10, 170) + "10 40 180 100 re S 10 90 m 190 90 l S 100 40 m 100 140 l S\n" + Text("apples ", 20, 115) + Text("green", 110, 115) + Text("orchards ", 20, 65) + Text("fresh", 110, 65) + Text("Footer", 10, 20) : Text(first ?? "apples orchards green", 10, 100));
        Stream(9, Text("ocean boats blue", 10, 100));
        Stream(11, Text("invoice amount payable", 10, 100));
        f.Classic(entries.ToArray(), "/Root 1 0 R", 12);
        using var input = new MemoryStream(f.Bytes.ToArray());
        return Pdf.Open(input);
    }

    private static readonly PdfRetrievalPermissions Permissions = new("host-policy-v1", "writer", new[] { "alice" });
    private static PdfRetrievalScope Scope(PdfSemanticChunkResult capture, string tenant = "tenant-a", string document = "document-a") => new(tenant, document, capture.DocumentSha256);
    private static PdfSemanticChunkResult Capture(PdfDocument document, int bytes = 64) => PdfSemanticChunks.Capture(document.CreateSnapshot(), new[] { 0, 1, 2 }, new(bytes));
    private static PdfEmbeddingModel Model(bool remote = false, int dimensions = 3, string version = "recorded-embedding-v1") => new(new("recorded-test", version, remote ? PdfIntelligenceLocation.Remote : PdfIntelligenceLocation.Local, new Uri(remote ? "https://models.example.test/v1/embeddings" : "http://127.0.0.1:8081/v1/embeddings")), dimensions);
    private sealed class ResponseStream : MemoryStream
    {
        internal bool Closed;
        internal ResponseStream(byte[] bytes) : base(bytes)
        {
        }

        protected override void Dispose(bool disposing)
        {
            Closed = true;
            base.Dispose(disposing);
        }
    }

    private sealed class RecordedTransport : IPdfIntelligenceTransport
    {
        internal int Calls;
        internal string Failure = "";
        internal Action? BeforeReturn;
        internal string LastRequest = "";
        internal Stream? RequestLease;
        internal ResponseStream? Response;
        internal readonly List<string> Responses = new();
        internal static double[] Feature(string text) => new[]
        {
            text.Contains("apples") || text.Contains("orchards") ? 1d : .1d,
            text.Contains("ocean") || text.Contains("boats") ? 1d : .1d,
            text.Contains("invoice") || text.Contains("payable") ? 1d : .1d
        };
        public Stream OpenResponse(Uri endpoint, Stream request, long requestLength, PdfOperationContext c)
        {
            Calls++;
            RequestLease = request;
            using (var reader = new StreamReader(request, new UTF8Encoding(false, true), false, 1024, true))
                LastRequest = reader.ReadToEnd();
            Assert.Equal(requestLength, Encoding.UTF8.GetByteCount(LastRequest));
            Assert.Equal("/v1/embeddings", endpoint.AbsolutePath);
            var root = IntelligenceJson.Object(IntelligenceJson.Parse(LastRequest, c), c);
            var texts = IntelligenceJson.Array(root["input"], c);
            var data = new List<object?>();
            for (int i = 0; i < texts.Count; i++)
            {
                var vector = Feature((string)texts[i]!);
                if (Failure == "zero")
                    vector = new double[3];
                var values = vector.Select(value => (object? )(decimal)value).ToList();
                if (Failure == "dimensions")
                    values.RemoveAt(0);
                data.Add(new Dictionary<string, object?> { ["object"] = "embedding", ["index"] = (decimal)(Failure == "duplicate" ? 0 : i), ["embedding"] = values });
            }

            if (Failure == "count")
                data.RemoveAt(data.Count - 1);
            var response = new Dictionary<string, object?>
            {
                ["object"] = "list",
                ["model"] = Failure == "model" ? "other-version" : root["model"],
                ["data"] = data,
                ["usage"] = new Dictionary<string, object?>
                {
                    ["prompt_tokens"] = 5m,
                    ["total_tokens"] = Failure == "usage" ? 6m : 5m
                }
            };
            if (Failure == "tools")
                response["tool_calls"] = new List<object?>();
            string json = IntelligenceJson.Text(response, c);
            if (Failure == "precision")
                json = json.Replace("0.1", "1e-100");
            if (Failure == "nan")
                json = json.Replace("0.1", "NaN");
            if (Failure == "secret")
                throw new IOException("PRIVATE CREDENTIAL TOKEN");
            Responses.Add(json);
            Response = new(Encoding.UTF8.GetBytes(json));
            BeforeReturn?.Invoke();
            return Response;
        }
    }

    [Fact]
    public void LayoutAndByteBoundariesPreserveExactRawSourceAndSeparateInferredTables()
    {
        using var document = Document(true);
        var capture = PdfSemanticChunks.Capture(document.CreateSnapshot(), new[] { 0, 1, 2 }, new(20, tables: new(PdfTableDetectionMode.Ruled)));
        foreach (int page in capture.PageIndices)
        {
            string raw = PdfTextExtraction.ReadPage(document.CreateSnapshot(), page, new(includeInvisibleText: false)).RawText;
            var chunks = capture.Chunks.Where(chunk => chunk.PageIndex == page).ToArray();
            Assert.Equal(raw, string.Concat(chunks.Select(chunk => chunk.Text)));
            int offset = 0;
            foreach (var chunk in chunks)
            {
                Assert.Equal(offset, chunk.TextOffset);
                Assert.Equal(chunk.Text, string.Concat(chunk.Characters.Select(character => character.Text)));
                Assert.InRange(chunk.Utf8Bytes, 0, 20);
                Assert.Equal(chunk.Utf8Bytes, Encoding.UTF8.GetByteCount(chunk.Text));
                offset += chunk.Text.Length;
                Assert.All(chunk.Characters, character => Assert.False(character.GeometryEstimated));
            }
        }

        var first = capture.Chunks.Where(chunk => chunk.PageIndex == 0).ToArray();
        Assert.Null(first[0].TableIndex);
        Assert.Contains(first, chunk => chunk.TableIndex == 0);
        Assert.Null(first[first.Length - 1].TableIndex);
        Assert.Equal(capture.CaptureSha256, PdfSemanticChunks.Capture(document.CreateSnapshot(), new[] { 0, 1, 2 }, new(20, tables: new(PdfTableDetectionMode.Ruled))).CaptureSha256);
        Save("tables", document, capture, null, null);
    }

    [Fact]
    public void ExactScopeAndRecordedFeaturesProduceHandLabeledTopOneRetrieval()
    {
        using var document = Document();
        var capture = Capture(document);
        var scope = Scope(capture);
        var store = new PdfMemoryVectorStore("admin");
        var transport = new RecordedTransport();
        var provider = new PdfJsonEmbeddingProvider(Model(), transport);
        var index = PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", provider, store);
        foreach (var item in new[]
        {
            ("apples", 0),
            ("ocean", 1),
            ("invoice", 2)
        }

        )
        {
            var result = PdfVectorRetrieval.Search(scope, "alice", item.Item1, provider, store, 3);
            Assert.Equal(item.Item2, result.Matches[0].Chunk.PageIndex);
            Assert.Equal(3, result.Matches.Count);
            Assert.True(result.Matches[0].Score > result.Matches[1].Score);
            Assert.All(result.Matches, match =>
            {
                Assert.Same(Permissions, match.Index.Permissions);
                Assert.Equal(scope.DocumentSha256, match.Index.Capture.DocumentSha256);
            });
            Save(item.Item1, document, capture, index, result);
        }

        Assert.True(transport.Response!.Closed);
        Assert.Throws<ObjectDisposedException>(() => transport.RequestLease!.ReadByte());
    }

    [Fact]
    public void TenantDocumentRevisionAndAclCannotBeEscalatedBeforeEmbedding()
    {
        using var document = Document();
        var capture = Capture(document);
        var scope = Scope(capture);
        var store = new PdfMemoryVectorStore("admin");
        var transport = new RecordedTransport();
        var provider = new PdfJsonEmbeddingProvider(Model(), transport);
        PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", provider, store);
        int calls = transport.Calls;
        foreach (var denied in new[]
        {
            Scope(capture, "tenant-b"),
            Scope(capture, document: "document-b"),
            new PdfRetrievalScope(scope.TenantId, scope.DocumentId, new string ('0', 64))
        }

        )
            Assert.Equal("Retrieval.ScopeDenied", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Search(denied, "alice", "apples", provider, store)).Diagnostic.Code);
        Assert.Equal("Retrieval.ScopeDenied", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Search(scope, "mallory", "apples", provider, store)).Diagnostic.Code);
        Assert.Equal("Retrieval.ScopeDenied", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Index(capture, scope, new("attacker", "mallory", new[] { "mallory" }), "mallory", provider, store)).Diagnostic.Code);
        Assert.Equal("Retrieval.PermissionMutation", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Index(capture, scope, new("attacker", "writer", new[] { "mallory" }), "writer", provider, store)).Diagnostic.Code);
        Assert.Equal(calls, transport.Calls);
        // Identical text in another document has a distinct scoped binding and requires another explicit embedding call.
        var other = PdfVectorRetrieval.Index(capture, Scope(capture, document: "document-b"), Permissions, "admin", provider, store);
        Assert.NotEqual(other.Index.Embeddings.RequestSha256, PdfVectorRetrieval.Index(capture, scope, Permissions, "writer", provider, store).Index.Embeddings.RequestSha256);
    }

    [Theory]
    [InlineData("zero")]
    [InlineData("dimensions")]
    [InlineData("duplicate")]
    [InlineData("count")]
    [InlineData("model")]
    [InlineData("usage")]
    [InlineData("tools")]
    [InlineData("precision")]
    [InlineData("nan")]
    [InlineData("secret")]
    public void MalformedEmbeddingResponsesNeverReplaceTheExistingIndex(string failure)
    {
        using var document = Document();
        var capture = Capture(document);
        var scope = Scope(capture);
        var store = new PdfMemoryVectorStore("admin");
        var transport = new RecordedTransport();
        var provider = new PdfJsonEmbeddingProvider(Model(), transport);
        var initial = PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", provider, store);
        transport.Failure = failure;
        var error = Assert.Throws<PdfException>(() => PdfVectorRetrieval.Index(capture, scope, Permissions, "writer", provider, store));
        Assert.DoesNotContain("PRIVATE", error.ToString());
        PdfFont.Run("verify", "test", null, default, c => Assert.Equal(initial.Version, store.Authorize(scope, "alice", false, c).Version));
        if (transport.Response != null)
            Assert.True(transport.Response.Closed);
    }

    [Fact]
    public void RemoteOriginModelAndTokenCostReservationsFailBeforeDispatch()
    {
        using var document = Document();
        var capture = Capture(document);
        var scope = Scope(capture);
        var store = new PdfMemoryVectorStore("admin");
        var transport = new RecordedTransport();
        var remote = new PdfJsonEmbeddingProvider(Model(true), transport);
        Assert.Equal("Retrieval.RemoteAuthority", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", remote, store)).Diagnostic.Code);
        var local = new PdfJsonEmbeddingProvider(Model(), transport);
        Assert.Equal("Retrieval.TokenLimit", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", local, store, new(maximumInputTokens: 1))).Diagnostic.Code);
        var priced = new PdfJsonEmbeddingProvider(new(new("priced", "v1", endpoint: new Uri("http://127.0.0.1:8081/v1/embeddings"), requestCharge: 1), 3), transport);
        Assert.Equal("Retrieval.CostLimit", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", priced, store)).Diagnostic.Code);
        Assert.Equal(0, transport.Calls);
        PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", local, store);
        Assert.Equal("Retrieval.Model", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Search(scope, "alice", "apples", new PdfJsonEmbeddingProvider(Model(version: "v2"), transport), store)).Diagnostic.Code);
        Assert.Equal(1, transport.Calls);
    }

    [Fact]
    public void CancellationAndFiniteStoreOrSourceLimitsLeaveExistingStateIntact()
    {
        using var document = Document();
        var capture = Capture(document);
        var scope = Scope(capture);
        var store = new PdfMemoryVectorStore("admin");
        var transport = new RecordedTransport();
        var provider = new PdfJsonEmbeddingProvider(Model(), transport);
        var initial = PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", provider, store);
        using var cancel = new CancellationTokenSource();
        transport.BeforeReturn = () => cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfVectorRetrieval.Index(capture, scope, Permissions, "writer", provider, store, cancellationToken: cancel.Token));
        Assert.True(transport.Response!.Closed);
        transport.BeforeReturn = null;
        PdfFont.Run("verify", "test", null, default, c => Assert.Equal(initial.Version, store.Authorize(scope, "alice", false, c).Version));
        Assert.Equal("Retrieval.StoreCapacity", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", provider, new PdfMemoryVectorStore("admin", maximumRecords: 1))).Diagnostic.Code);
        Assert.Equal("Retrieval.Chunks", Assert.Throws<PdfException>(() => PdfSemanticChunks.Capture(document.CreateSnapshot(), new[] { 0, 1 }, new(maximumChunks: 1))).Diagnostic.Code);
        Assert.Equal("Retrieval.Characters", Assert.Throws<PdfException>(() => PdfSemanticChunks.Capture(document.CreateSnapshot(), new[] { 0 }, new(maximumCharacters: 1))).Diagnostic.Code);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Search(scope, "alice", "apples", provider, store, policy: new(limits: new(maximumExpandedBytes: 100)))).Diagnostic.Code);
    }

    [Fact]
    public void ScaledCosineHandlesExtremeFiniteVectorsAndRejectsStaleCommits()
    {
        using var document = Document();
        var capture = Capture(document);
        var scope = Scope(capture);
        var store = new PdfMemoryVectorStore("admin");
        var provider = new PdfJsonEmbeddingProvider(Model(), new RecordedTransport());
        var initial = PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", provider, store);
        PdfFont.Run("numeric", "test", null, default, c =>
        {
            Assert.Equal(1, PdfMemoryVectorStore.Cosine(new[] { double.MaxValue, double.MaxValue }, new[] { double.MaxValue, double.MaxValue }, c), 14);
            Assert.Equal(-1, PdfMemoryVectorStore.Cosine(new[] { double.Epsilon, 0d }, new[] { -double.Epsilon, 0d }, c), 14);
            Assert.Equal(0, PdfMemoryVectorStore.Cosine(new[] { 1d, 0d }, new[] { 0d, 1d }, c));
        });
        Assert.Equal("Retrieval.Vector", Assert.Throws<PdfException>(() => PdfFont.Run("invalid", "test", null, default, c => new PdfEmbeddingVector(new[] { double.NaN }, c))).Diagnostic.Code);
        PdfVectorRetrieval.Index(capture, scope, Permissions, "writer", provider, store);
        Assert.Equal("Retrieval.Generation", Assert.Throws<PdfException>(() => PdfFont.Run("stale", "test", null, default, c => store.Replace(initial.Index, "writer", initial.Version, c))).Diagnostic.Code);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    [InlineData(8)]
    public void UnicodeClustersAndCrLfRemainIntactAtByteBoundaries(int bytes)
    {
        using var document = Document(first: "ab\\001\\002\\003\\004 cd");
        var capture = PdfSemanticChunks.Capture(document.CreateSnapshot(), new[] { 0 }, new(bytes));
        Assert.Equal("ab😀\r\nfi cd", string.Concat(capture.Chunks.Select(chunk => chunk.Text)));
        Assert.All(capture.Chunks, chunk =>
        {
            Assert.True(chunk.Utf8Bytes <= bytes);
            Assert.False(chunk.Text.EndsWith("\r", StringComparison.Ordinal));
            Assert.False(chunk.Text.StartsWith("\n", StringComparison.Ordinal));
            Assert.Equal(-1, chunk.Text.IndexOf("\ufffd", StringComparison.Ordinal));
        });
        Save("unicode-" + bytes, document, capture, null, null);
    }

    [Fact]
    public void RemovalRevokesReadersBeforeEmbeddingAndWritersCannotRecreateScope()
    {
        using var document = Document();
        var capture = Capture(document);
        var scope = Scope(capture);
        var store = new PdfMemoryVectorStore("admin");
        var transport = new RecordedTransport();
        var provider = new PdfJsonEmbeddingProvider(Model(), transport);
        var original = PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", provider, store);
        PdfVectorRetrieval.Remove(scope, "writer", store);
        Assert.Equal("Retrieval.ScopeDenied", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Search(scope, "alice", "apples", provider, store)).Diagnostic.Code);
        Assert.Equal("Retrieval.ScopeDenied", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Index(capture, scope, Permissions, "writer", provider, store)).Diagnostic.Code);
        Assert.Equal(1, transport.Calls);
        var replacement = PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", provider, store);
        Assert.True(replacement.Version > original.Version);
    }

    [Fact]
    public void CompiledCSharpAndVisualBasicConsumersCaptureTheSameSelectedPage()
    {
        using var document = Document();
        var cs = ContractExamples.CSharp.ContractExample.ChunkSelectedPage(document.CreateSnapshot(), 1, 12);
        var vb = ContractExamples.VisualBasic.ContractExample.ChunkSelectedPage(document.CreateSnapshot(), 1, 12);
        Assert.Equal(cs.CaptureSha256, vb.CaptureSha256);
        Assert.All(cs.Chunks, chunk => Assert.Equal(1, chunk.PageIndex));
        Assert.Equal("ocean boats blue", string.Concat(cs.Chunks.Select(chunk => chunk.Text)));
        Save("consumer-csharp", document, cs, null, null);
        Save("consumer-vb", document, vb, null, null);
    }

    private sealed class RacingStore : IPdfVectorStore
    {
        internal readonly PdfMemoryVectorStore Inner = new("admin");
        internal PdfVectorIndex? ReplaceBeforeSearch;
        public PdfVectorIndexInfo Authorize(PdfRetrievalScope scope, string principal, bool write, PdfOperationContext c) => Inner.Authorize(scope, principal, write, c);
        public long Replace(PdfVectorIndex index, string principal, long expectedVersion, PdfOperationContext c) => Inner.Replace(index, principal, expectedVersion, c);
        public void Remove(PdfRetrievalScope scope, string principal, long expectedVersion, PdfOperationContext c) => Inner.Remove(scope, principal, expectedVersion, c);
        public IReadOnlyList<PdfVectorMatch> Search(PdfRetrievalScope scope, string principal, long expectedVersion, PdfEmbeddingModel model, PdfEmbeddingVector query, int maximumResults, PdfOperationContext c)
        {
            if (ReplaceBeforeSearch != null)
                Inner.Replace(ReplaceBeforeSearch, "writer", expectedVersion, c);
            return Inner.Search(scope, principal, expectedVersion, model, query, maximumResults, c);
        }
    }

    [Fact]
    public void ConcurrentReplacementBetweenAuthorizationAndSearchRejectsStaleResults()
    {
        using var document = Document();
        var capture = Capture(document);
        var scope = Scope(capture);
        var store = new RacingStore();
        var transport = new RecordedTransport();
        var provider = new PdfJsonEmbeddingProvider(Model(), transport);
        var index = PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", provider, store);
        store.ReplaceBeforeSearch = index.Index;
        Assert.Equal("Retrieval.Generation", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Search(scope, "alice", "apples", provider, store)).Diagnostic.Code);
    }

    [Fact]
    public async Task IndependentConcurrentQueriesRetainTheirExactTenantAndDocument()
    {
        using var document = Document();
        var capture = Capture(document);
        var store = new PdfMemoryVectorStore("admin");
        var a = Scope(capture);
        var b = Scope(capture, "tenant-b", "document-b");
        foreach (var scope in new[]
        {
            a,
            b
        }

        )
            PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", new PdfJsonEmbeddingProvider(Model(), new RecordedTransport()), store);
        await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Task.Run(() =>
        {
            var scope = i % 2 == 0 ? a : b;
            var result = PdfVectorRetrieval.Search(scope, "alice", "ocean", new PdfJsonEmbeddingProvider(Model(), new RecordedTransport()), store);
            Assert.Equal(1, result.Matches[0].Chunk.PageIndex);
            Assert.All(result.Matches, match =>
            {
                Assert.Equal(scope.TenantId, match.Index.Scope.TenantId);
                Assert.Equal(scope.DocumentId, match.Index.Scope.DocumentId);
            });
        })));
    }

    [Fact]
    public async Task ConcurrentCompareAndSwapWritersCommitExactlyOneGeneration()
    {
        using var document = Document();
        var capture = Capture(document);
        var scope = Scope(capture);
        var store = new PdfMemoryVectorStore("admin");
        var initial = PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", new PdfJsonEmbeddingProvider(Model(), new RecordedTransport()), store);
        using var ready = new Barrier(2);
        var outcomes = await Task.WhenAll(Enumerable.Range(0, 2).Select(i => Task.Run(() =>
        {
            try
            {
                PdfFont.Run("concurrent-cas", "test", null, default, c =>
                {
                    Assert.True(ready.SignalAndWait(TimeSpan.FromSeconds(20)));
                    store.Replace(initial.Index, "writer", initial.Version, c);
                });
                return "committed";
            }
            catch (PdfException error)
            {
                return error.Diagnostic.Code;
            }
        })));
        Assert.Equal(1, outcomes.Count(value => value == "committed"));
        Assert.Equal(1, outcomes.Count(value => value == "Retrieval.Generation"));
    }

    [Fact]
    public void ExactCharacterChunkAndPreparedTokenCeilingsAreInclusive()
    {
        using var document = Document();
        var capture = Capture(document);
        int characters = capture.Chunks.Sum(chunk => chunk.Text.Length);
        Assert.Equal(capture.CaptureSha256, PdfSemanticChunks.Capture(document.CreateSnapshot(), new[] { 0, 1, 2 }, new(64, maximumCharacters: characters)).CaptureSha256);
        Assert.Equal("Retrieval.Characters", Assert.Throws<PdfException>(() => PdfSemanticChunks.Capture(document.CreateSnapshot(), new[] { 0, 1, 2 }, new(64, maximumCharacters: characters - 1))).Diagnostic.Code);
        Assert.Equal(3, PdfSemanticChunks.Capture(document.CreateSnapshot(), new[] { 0, 1, 2 }, new(64, maximumChunks: 3)).Chunks.Count);
        Assert.Equal("Retrieval.Chunks", Assert.Throws<PdfException>(() => PdfSemanticChunks.Capture(document.CreateSnapshot(), new[] { 0, 1, 2 }, new(64, maximumChunks: 2))).Diagnostic.Code);
        var store = new PdfMemoryVectorStore("admin");
        var scope = Scope(capture);
        var transport = new RecordedTransport();
        var provider = new PdfJsonEmbeddingProvider(Model(), transport);
        PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", provider, store);
        int exact = Encoding.UTF8.GetByteCount(transport.LastRequest);
        PdfVectorRetrieval.Index(capture, scope, Permissions, "writer", provider, store, new(maximumInputTokens: exact));
        Assert.Equal("Retrieval.TokenLimit", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Index(capture, scope, Permissions, "writer", provider, store, new(maximumInputTokens: exact - 1))).Diagnostic.Code);
        Assert.Equal(2, transport.Calls);
    }

    [Fact]
    public void RemoteDispatchRequiresTheExactOriginAndExplicitConfiguredCostCeiling()
    {
        using var document = Document();
        var capture = Capture(document);
        var scope = Scope(capture);
        var transport = new RecordedTransport();
        var provider = new PdfJsonEmbeddingProvider(Model(true), transport);
        Assert.Equal("Retrieval.RemoteAuthority", Assert.Throws<PdfException>(() => PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", provider, new PdfMemoryVectorStore("admin"), new(allowedRemoteOrigin: new Uri("https://other.example.test/")))).Diagnostic.Code);
        PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", provider, new PdfMemoryVectorStore("admin"), new(allowedRemoteOrigin: new Uri("https://models.example.test/")));
        Assert.Equal(1, transport.Calls);
        var priced = new PdfJsonEmbeddingProvider(new(new("priced", "v1", endpoint: new Uri("http://127.0.0.1:8081/v1/embeddings"), requestCharge: 1), 3), transport);
        PdfVectorRetrieval.Index(capture, scope, Permissions, "admin", priced, new PdfMemoryVectorStore("admin"), new(maximumConfiguredCost: 1));
        Assert.Equal(2, transport.Calls);
    }

    private static void Save(string name, PdfDocument document, PdfSemanticChunkResult capture, PdfVectorIndexResult? index, PdfVectorSearchResult? search)
    {
        string directory = Path.Combine(AppContext.BaseDirectory, "retrieval-fixtures");
        Directory.CreateDirectory(directory);
        using (var input = document.CreateSnapshot().SourceDocument!.OpenOriginalStream())
        using (var output = File.Create(Path.Combine(directory, name + ".pdf")))
            input.CopyTo(output);
        var value = new Dictionary<string, object?>
        {
            ["actualModelInferenceQualified"] = false,
            ["sourceSha256"] = capture.DocumentSha256,
            ["captureSha256"] = capture.CaptureSha256,
            ["model"] = index?.Index.Model.Model.ExactModelVersion,
            ["dimensions"] = index == null ? null : (object? )(decimal)index.Index.Model.Dimensions,
            ["chunks"] = capture.Chunks.Select(chunk => (object? )new Dictionary<string, object?> { ["index"] = (decimal)chunk.Index, ["page"] = (decimal)chunk.PageIndex, ["offset"] = (decimal)chunk.TextOffset, ["text"] = chunk.Text, ["sha256"] = chunk.Sha256, ["bytes"] = (decimal)chunk.Utf8Bytes, ["table"] = chunk.TableIndex.HasValue ? (object? )(decimal)chunk.TableIndex.Value : null }).ToList(),
            ["vectors"] = index?.Index.Embeddings.Vectors.Select(vector => (object? )vector.Values.Select(number => (object? )(decimal)number).ToList()).ToList(),
            ["matches"] = search?.Matches.Select(match => (object? )new Dictionary<string, object?> { ["index"] = (decimal)match.Chunk.Index, ["score"] = (decimal)match.Score }).ToList()
        };
        PdfFont.Run("fixture", "test", null, default, c => File.WriteAllText(Path.Combine(directory, name + ".json"), IntelligenceJson.Text(value, c), new UTF8Encoding(false)));
    }
}
