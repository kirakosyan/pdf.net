using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ProgressiveTests
{
    private readonly ITestOutputHelper _output;
    public ProgressiveTests(ITestOutputHelper output) => _output = output;
    private static byte[] Fixture(string name) => File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Progressive", name + ".pdf"));
    private static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private static byte[] LargeFixture()
    {
        using var f = new SyntaxFixtures();
        var entries = new List<(int, int, long, bool)>
        {
            (0, 65535, 0, true)
        };
        void Object(int id, string value) => entries.Add((id, 0, f.Object(id, value), false));
        void Stream(int id, byte[] bytes) => entries.Add((id, 0, f.StreamObject(id, "", bytes), false));
        Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        Object(2, "<< /Type /Pages /Kids [3 0 R 4 0 R] /Count 2 /MediaBox [0 0 100 200] /Rotate 90 /Resources << /XObject << /Original 7 0 R >> >> >>");
        Object(3, "<< /Type /Page /Parent 2 0 R /Contents 5 0 R >>");
        Object(4, "<< /Type /Page /Parent 2 0 R /Contents 6 0 R /Rotate 180 >>");
        Stream(5, Encoding.ASCII.GetBytes("q 1 0 0 rg 0 0 10 10 re f Q\n"));
        Stream(6, Enumerable.Repeat((byte)' ', 300000).ToArray());
        Stream(7, Enumerable.Repeat((byte)'A', 300000).ToArray());
        f.Classic(entries.ToArray(), "/Root 1 0 R", 8);
        return f.Bytes.ToArray();
    }

    private sealed class Source : IPdfRangeSource
    {
        internal readonly byte[] Bytes;
        internal readonly List<(long Offset, int Length)> Ranges = new();
        internal string Version = "original-revision-1", Failure = "";
        internal bool Authorized = true, ChangeOnRead;
        internal int Authorizations;
        internal PdfOperationContext? RetainedContext;
        internal Action? DuringRead;
        internal Source(byte[] bytes) => Bytes = bytes;
        public PdfRangeIdentity GetIdentity(PdfOperationContext context)
        {
            context.Checkpoint();
            Authorizations++;
            if (!Authorized)
                throw new IOException("secret credential must not escape");
            return new(Bytes.Length, Version);
        }

        public PdfRangeResponse OpenRange(long offset, int length, PdfRangeIdentity expectedIdentity, PdfOperationContext context)
        {
            context.Checkpoint();
            RetainedContext = context;
            DuringRead?.Invoke();
            Assert.Equal(Version, expectedIdentity.Version);
            Assert.True(Authorized);
            Ranges.Add((offset, length));
            if (Failure == "throw")
                throw new IOException("secret credential must not escape");
            var bytes = Bytes.Skip((int)offset).Take(length).ToArray();
            if (Failure == "short")
                bytes = bytes.Take(bytes.Length - 1).ToArray();
            if (Failure == "extra")
                bytes = bytes.Concat(new byte[] { 0 }).ToArray();
            var identity = new PdfRangeIdentity(Bytes.Length, Failure == "version" ? "another-revision" : Version);
            if (ChangeOnRead)
                Version = "changed-revision-2";
            return new(Failure == "offset" ? offset + 1 : offset, length, identity, new MemoryStream(bytes));
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void LazyResolutionSupportsClassicAndStreamIndexesWithDeclaredFilters(bool flate, bool predictor)
    {
        using var progressive = new PdfProgressiveDocument(new Source(SyntaxFixtures.CrossReferenceStream(flate, predictor)));
        var catalog = progressive.ReadObject(new(1));
        Assert.Equal("preserved", Encoding.ASCII.GetString(catalog.Entries["Unknown"].StringBytes.ToArray()));
    }

    [Fact]
    public void CompressedObjectsAndIncrementalGenerationShadowingRemainLazy()
    {
        using var f = new SyntaxFixtures();
        long container = f.StreamObject(1, "/Type /ObjStm /N 1 /First 4", Encoding.ASCII.GetBytes("3 0 << /Original /Compressed >>"));
        long xref = f.Bytes.Position;
        byte[] entries = SyntaxFixtures.Entry(0, 0, 65535).Concat(SyntaxFixtures.Entry(1, container, 0)).Concat(SyntaxFixtures.Entry(1, xref, 0)).Concat(SyntaxFixtures.Entry(2, 1, 0)).ToArray();
        f.StreamObject(2, "/Type /XRef /Size 4 /W [1 4 2]", entries);
        f.Text("startxref\n" + xref + "\n%%EOF\n");
        using (var compressed = new PdfProgressiveDocument(new Source(f.Bytes.ToArray())))
            Assert.Equal("Compressed", compressed.ReadObject(new(3)).Entries["Original"].NameValue);
        long fresh = f.Object(3, "(new generation)", 1);
        f.Classic(new[] { (3, 1, fresh, false) }, "/Prev " + xref, 4);
        using var changed = new PdfProgressiveDocument(new Source(f.Bytes.ToArray()));
        Assert.Same(PdfValue.Null, changed.ReadObject(new(3)));
        Assert.Equal("new generation", Encoding.ASCII.GetString(changed.ReadObject(new(3, 1)).StringBytes.ToArray()));
    }

    [Fact]
    public void EncryptedStreamIndexDoesNotBypassTheExplicitAuthenticationRequirement()
    {
        using var f = new SyntaxFixtures();
        long encryption = f.Object(1, "<< /Filter /Standard >>"), value = f.Object(2, "(opaque)"), xref = f.Bytes.Position;
        var entries = SyntaxFixtures.Entry(0, 0, 65535).Concat(SyntaxFixtures.Entry(1, encryption, 0)).Concat(SyntaxFixtures.Entry(1, value, 0)).Concat(SyntaxFixtures.Entry(1, xref, 0)).ToArray();
        f.StreamObject(3, "/Type /XRef /Size 4 /W [1 4 2] /Encrypt 1 0 R", entries);
        f.Text("startxref\n" + xref + "\n%%EOF\n");
        using var progressive = new PdfProgressiveDocument(new Source(f.Bytes.ToArray()));
        Assert.Equal("Progressive.EncryptionUnavailable", Assert.Throws<PdfException>(() => progressive.ReadObject(new(2))).Diagnostic.Code);
    }

    [Fact]
    public void PageAndResourceAccessIsLazyInheritedDetachedAndPrivatelyCached()
    {
        var source = new Source(LargeFixture());
        var policy = new PdfOperationPolicy(limits: new(maximumInputBytes: 16000));
        using var progressive = new PdfProgressiveDocument(source, new(1024, 8192), policy);
        var first = progressive.ReadPage(0);
        Assert.Equal(2, first.PageCount);
        Assert.Equal(new PdfObjectId(3), first.ObjectId);
        Assert.Equal(90, first.Dictionary.Entries["Rotate"].IntegerValue);
        Assert.Equal(4, first.Dictionary.Entries["MediaBox"].Items.Count);
        Assert.True(first.OperationReport.Succeeded);
        _output.WriteLine("Controlled first-page observation: sourceBytes={0}; fetchedBytes={1}; ranges={2}; elapsedMilliseconds={3}; productionLatencyQualified=false", source.Bytes.Length, source.Ranges.Sum(r => r.Length), source.Ranges.Count, first.OperationReport.ElapsedTime.TotalMilliseconds);
        Assert.Equal(first.ObjectId, ContractExamples.CSharp.ContractExample.ReadProgressivePage(new Source(source.Bytes), 0).ObjectId);
        Assert.Equal(first.ObjectId, ContractExamples.VisualBasic.ContractExample.ReadProgressivePage(new Source(source.Bytes), 0).ObjectId);
        Assert.True(source.Ranges.Sum(r => r.Length) < 16000);
        Assert.DoesNotContain(source.Ranges, r => r.Offset > 10000 && r.Offset < 590000);
        int requests = source.Ranges.Count;
        Assert.Equal(180, progressive.ReadPage(1).Dictionary.Entries["Rotate"].IntegerValue);
        Assert.Equal(requests, source.Ranges.Count);
        Assert.Equal("q 1 0 0 rg 0 0 10 10 re f Q\n", Encoding.ASCII.GetString(SignatureTests.Bytes(progressive.DecodeStream(new(5)).OpenRead())));
        var detached = progressive.ReadObject(new(5));
        progressive.Dispose();
        Assert.Equal("q 1 0 0 rg 0 0 10 10 re f Q\n", Encoding.ASCII.GetString(SignatureTests.Bytes(detached.OpenRawStream())));
        Assert.Throws<ObjectDisposedException>(() => progressive.ReadPage(0));
        Assert.Throws<ObjectDisposedException>(() => source.RetainedContext!.Checkpoint());
    }

    [Fact]
    public void CacheHitsReauthorizeAndRevisionChangesNeverMixBytes()
    {
        var source = new Source(LargeFixture());
        using var progressive = new PdfProgressiveDocument(source, new(1024, 8192));
        progressive.ReadPage(0);
        int requests = source.Ranges.Count;
        source.Authorized = false;
        var denied = Assert.Throws<PdfException>(() => progressive.ReadPage(0));
        Assert.DoesNotContain("secret", denied.Message);
        Assert.Equal(requests, source.Ranges.Count);
        source.Authorized = true;
        source.Version = "another-revision";
        Assert.Equal("Progressive.SourceChanged", Assert.Throws<PdfException>(() => progressive.ReadPage(0)).Diagnostic.Code);
        using var fresh = new PdfProgressiveDocument(source);
        Assert.Equal(2, fresh.ReadPage(0).PageCount);
    }

    [Theory]
    [InlineData("short", "Progressive.TruncatedResponse")]
    [InlineData("extra", "Progressive.ResponseLength")]
    [InlineData("version", "Progressive.ResponseBinding")]
    [InlineData("offset", "Progressive.ResponseBinding")]
    [InlineData("throw", "Progressive.ProviderFailed")]
    public void RangeResponsesMustMatchExactBoundsAndIdentity(string failure, string code)
    {
        using var progressive = new PdfProgressiveDocument(new Source(LargeFixture()) { Failure = failure }, new(1024, 8192));
        Assert.Equal(code, Assert.Throws<PdfException>(() => progressive.ReadPage(0)).Diagnostic.Code);
    }

    [Fact]
    public void SourceMutationCancellationInputAndCacheBudgetsAndConcurrencyAreEnforced()
    {
        using (var changed = new PdfProgressiveDocument(new Source(LargeFixture()) { ChangeOnRead = true }))
            Assert.Equal("Progressive.SourceChanged", Assert.Throws<PdfException>(() => changed.ReadPage(0)).Diagnostic.Code);
        var source = new Source(LargeFixture());
        using var progressive = new PdfProgressiveDocument(source, new(1024, 2048));
        source.DuringRead = () => Assert.Throws<InvalidOperationException>(() => progressive.ReadPage(0));
        progressive.ReadPage(0);
        progressive.ReadObject(new(7));
        int initial = source.Ranges.Count;
        progressive.ReadPage(0);
        Assert.True(source.Ranges.Count > initial); // Two-block ceiling forces eviction.
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => progressive.ReadPage(0, cancel.Token));
        using var limited = new PdfProgressiveDocument(new Source(LargeFixture()), new(1024, 8192), new(limits: new(maximumInputBytes: 16000)));
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => limited.ReadObject(new(7))).Diagnostic.Code);
        using var expanded = new PdfProgressiveDocument(new Source(LargeFixture()), new(1024, 8192), new(limits: new(maximumExpandedBytes: 1000)));
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => expanded.ReadPage(0)).Diagnostic.Code);
    }

    [Fact]
    public void ActualOriginalLinearizedFixtureHasBoundOffsetsAndSameLogicalPages()
    {
        var bytes = Fixture("linearized");
        Assert.Equal("5df8a76c6c48e8a4c9c2920a0e0e48718c07cf794707c6a4f61422e1fb899cf0", Hash(bytes));
        using var source = new MemoryStream(bytes);
        var observed = PdfLinearization.Inspect(source)!;
        Assert.Equal(bytes.Length, observed.FileLength);
        Assert.Equal(3, observed.PageCount);
        Assert.Equal(530, observed.HintOffset);
        Assert.Equal(121, observed.HintLength);
        using var progressive = new PdfProgressiveDocument(new Source(bytes), new(1024, 2048));
        Assert.Equal(observed.FirstPageEnd, progressive.InspectLinearization()!.FirstPageEnd);
        for (int n = 0; n < 3; n++)
            Assert.Equal(120 + n * 10, progressive.ReadPage(n).Dictionary.Entries["MediaBox"].Items[2].IntegerValue);
        using var ordinary = new MemoryStream(Fixture("original"));
        Assert.Null(PdfLinearization.Inspect(ordinary));
    }

    // These providers exercise the trust boundary with fixed original bytes. They are deliberately not independent conformance engines.
    private sealed class Producer : IPdfLinearizationProducer
    {
        public string EngineId => "contract.original-producer.1";

        public bool Supports(PdfLinearizationOptions options) => options.FirstPageIndex == 0;
        public void Linearize(Stream source, Stream candidate, PdfLinearizationOptions options, PdfOperationContext context)
        {
            context.Checkpoint();
            var bytes = Fixture("linearized");
            candidate.Write(bytes, 0, bytes.Length);
        }
    }

    private sealed class Validator : IPdfLinearizationValidator
    {
        internal bool Valid = true, Complete = true, Stale, WrongRequest;
        internal Stream? Retained;
        public string EngineId { get; set; } = "contract.verdict-fixture.1";

        public bool Supports(PdfLinearizationOptions options) => true;
        public PdfLinearizationValidation Validate(Stream candidate, PdfLinearizationOptions options, string requestSha256, PdfOperationContext context)
        {
            context.Checkpoint();
            Retained = candidate;
            var bytes = SignatureTests.Bytes(new NonDisposingStream(candidate));
            return new(Stale ? new string ('0', 64) : Hash(bytes), WrongRequest ? new string ('0', 64) : requestSha256, Complete, Valid && Complete, Valid ? Array.Empty<string>() : new[] { "Original.Contract.Failed" });
        }
    }

    private sealed class NonDisposingStream : Stream
    {
        private readonly Stream _inner;
        internal NonDisposingStream(Stream inner) => _inner = inner;
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);
        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }

    [Fact]
    public void LinearizedOutputRequiresCompleteExactByteIndependentVerdictAndExpiredLeases()
    {
        var original = Fixture("original");
        var validator = new Validator();
        using var input = new MemoryStream(original);
        using var output = new MemoryStream();
        var result = PdfLinearization.Write(input, output, producer: new Producer(), validator: validator);
        Assert.True(result.IsValid);
        Assert.True(result.OperationReport!.Succeeded);
        Assert.Equal(Fixture("linearized"), output.ToArray());
        Assert.Throws<ObjectDisposedException>(() => validator.Retained!.ReadByte());
        input.Position = 0;
        using var cs = new MemoryStream();
        ContractExamples.CSharp.ContractExample.WriteLinearized(input, cs, new Producer(), new Validator());
        input.Position = 0;
        using var vb = new MemoryStream();
        ContractExamples.VisualBasic.ContractExample.WriteLinearized(input, vb, new Producer(), new Validator());
        Assert.Equal(output.ToArray(), cs.ToArray());
        Assert.Equal(output.ToArray(), vb.ToArray());
        foreach (var failed in new[]
        {
            new Validator
            {
                Valid = false
            },
            new Validator
            {
                Complete = false
            },
            new Validator
            {
                Stale = true
            },
            new Validator
            {
                WrongRequest = true
            },
            new Validator
            {
                EngineId = new Producer().EngineId
            }
        }

        )
        {
            input.Position = 0;
            using var untouched = new MemoryStream();
            untouched.WriteByte(42);
            Assert.Throws<PdfException>(() => PdfLinearization.Write(input, untouched, producer: new Producer(), validator: failed));
            Assert.Equal(new byte[] { 42 }, untouched.ToArray());
        }

        input.Position = 0;
        Assert.Throws<PdfException>(() => PdfLinearization.Write(input, output));
        Assert.Equal(0, input.Position);
    }

    [Fact]
    public void MalformedDictionaryAndOrdinaryEditsCannotReuseStaleLinearization()
    {
        var bytes = Fixture("linearized");
        var malformed = (byte[])bytes.Clone();
        int at = Encoding.ASCII.GetString(bytes).IndexOf("/L 1522", StringComparison.Ordinal);
        malformed[at + 6] = (byte)'1';
        using (var bad = new MemoryStream(malformed))
            Assert.Equal("Linearization.DictionaryBounds", Assert.Throws<PdfException>(() => PdfLinearization.Inspect(bad)).Diagnostic.Code);
        using var source = new MemoryStream(bytes);
        using var document = Pdf.Open(source);
        document.Pages.Add(new PdfPageSize(50, 60));
        using var rewritten = new MemoryStream();
        document.Save(rewritten);
        rewritten.Position = 0;
        Assert.Null(PdfLinearization.Inspect(rewritten));
        string path = Path.Combine(Path.GetTempPath(), "fluent-linearization-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllBytes(path, new byte[] { 77 });
        try
        {
            using var input = new MemoryStream(Fixture("original"));
            Assert.Throws<PdfException>(() => PdfLinearization.WriteFile(input, path, producer: new Producer(), validator: new Validator { Stale = true }));
            Assert.Equal(new byte[] { 77 }, File.ReadAllBytes(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void SignedInputUnsupportedRequestsAndSharedBudgetsCannotExposeCandidateBytes()
    {
        var signed = SignatureVerificationTests.Signed();
        using var original = signed.OpenOriginalStream();
        using var output = new MemoryStream();
        output.WriteByte(99);
        Assert.Equal("Linearization.SignatureRewrite", Assert.Throws<PdfException>(() => PdfLinearization.Write(original, output, producer: new Producer(), validator: new Validator())).Diagnostic.Code);
        original.Position = 0;
        Assert.Equal("Linearization.PageRetention", Assert.Throws<PdfException>(() => PdfLinearization.Write(original, output, new(allowSignatureInvalidation: true), new Producer(), new Validator())).Diagnostic.Code);
        Assert.Equal(new byte[] { 99 }, output.ToArray());
        using var ordinary = new MemoryStream(Fixture("original"));
        Assert.Equal("Linearization.ProfileUnavailable", Assert.Throws<PdfException>(() => PdfLinearization.Write(ordinary, output, new(1), new Producer(), new Validator())).Diagnostic.Code);
        Assert.Equal(0, ordinary.Position);
        Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => PdfLinearization.Write(ordinary, output, producer: new Producer(), validator: new Validator(), policy: new(limits: new(maximumOutputBytes: 100)))).Diagnostic.Code);
        Assert.Equal(new byte[] { 99 }, output.ToArray());
        using var cancel = new CancellationTokenSource();
        var source = new Source(LargeFixture())
        {
            DuringRead = () => cancel.Cancel()
        };
        using var progressive = new PdfProgressiveDocument(source);
        Assert.ThrowsAny<OperationCanceledException>(() => progressive.ReadPage(0, cancel.Token));
    }
}
