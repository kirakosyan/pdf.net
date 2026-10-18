using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class WriterTests
{
    [Fact]
    public void ImportedXrefStreamMetadataConvertsToAClassicTrailerWithoutLeakingItsEncoding()
    {
        using var original = Create();
        using var first = new MemoryStream();
        original.Save(first, new(crossReferenceFormat: PdfCrossReferenceFormat.Stream));
        first.Position = 0;
        var source = PdfSyntax.Read(first);
        using var edit = new PdfTransaction(source);
        using var second = new MemoryStream();
        edit.Save(second, new(crossReferenceFormat: PdfCrossReferenceFormat.Table));
        second.Position = 0;
        var updated = PdfSyntax.Read(second);
        Assert.Equal(PdfValueKind.Dictionary, updated.Revisions[0].Trailer.Kind);
        Assert.False(updated.Revisions[0].Trailer.Entries.ContainsKey("W"));
        Assert.False(updated.Revisions[0].Trailer.Entries.ContainsKey("Type"));
        Assert.Equal(new byte[] { 0, 255 }, updated.Revisions[0].Trailer.Entries["ID"].Items[0].StringBytes);
    }

    [Theory]
    [InlineData(PdfCrossReferenceFormat.Table)]
    [InlineData(PdfCrossReferenceFormat.Stream)]
    public void FullFilesPreserveValuesOpaqueBytesAndTrailerIds(PdfCrossReferenceFormat format)
    {
        using var transaction = Create();
        using var output = new MemoryStream();
        var report = transaction.Save(output, new(crossReferenceFormat: format));
        Assert.True(report.Succeeded);
        Assert.Same(report, transaction.LastOperationReport);
        Assert.True(output.CanWrite);
        output.Position = 0;
        var syntax = PdfSyntax.Read(output);
        Assert.Equal(format == PdfCrossReferenceFormat.Stream, syntax.Revisions[0].Trailer.Kind == PdfValueKind.Stream);
        var catalog = syntax.Resolve(new(1));
        Assert.Equal("space #%\u00ff", catalog.Entries["Owned"].NameValue);
        Assert.Equal(new byte[] { 0, 255 }, syntax.Revisions[0].Trailer.Entries["ID"].Items[0].StringBytes);
        using var raw = syntax.Resolve(new(4)).OpenRawStream();
        Assert.Equal(Content, Copy(raw));
        Assert.Equal(output.Length, report.Resources.Single(x => x.Resource == PdfResourceKind.OutputBytes).Consumed);
        Assert.Equal(Content.Length, report.Resources.Single(x => x.Resource == PdfResourceKind.InputBytes).Consumed);
    }

    [Theory]
    [InlineData(PdfCrossReferenceFormat.Table)]
    [InlineData(PdfCrossReferenceFormat.Stream)]
    public void IncrementalChangesPreserveAllOriginalBytesAndHistoricalGenerations(PdfCrossReferenceFormat format)
    {
        using var source = new SyntaxFixtures();
        long one = source.Object(1, "<< /Type /Catalog /Owned 9 4 R >>"), nine = source.Object(9, "123", 4);
        source.Classic(new[] { (0, 65535, 0L, true), (1, 0, one, false), (9, 4, nine, false) }, "/Root 1 0 R /ID [<00FF> <0102>]", size: 50);
        byte[] original = source.Bytes.ToArray();
        source.Bytes.Position = 0;
        var syntax = PdfSyntax.Read(source.Bytes);
        using var transaction = new PdfTransaction(syntax);
        transaction.Set(new(9, 4), PdfValue.Integer(456));
        var added = transaction.Add(PdfValue.Name("new"));
        Assert.Equal(50, added.Number);
        Assert.Throws<ArgumentException>(() => transaction.Set(new(9, 5), PdfValue.Null));
        using var output = new MemoryStream();
        transaction.Save(output, new(PdfWriteMode.Incremental, format));
        Assert.Equal(original, output.ToArray().Take(original.Length));
        output.Position = 0;
        var updated = PdfSyntax.Read(output);
        Assert.Equal(2, updated.Revisions.Count);
        Assert.Equal("456", updated.Resolve(new(9, 4)).NumberText);
        Assert.Equal("123", updated.Resolve(new(9, 4), 1).NumberText);
        Assert.Equal("new", updated.Resolve(added).NameValue);
        Assert.Equal(syntax.Revisions[0].StartXref, updated.Revisions[0].Trailer.Entries["Prev"].IntegerValue);
        Assert.Equal("123", syntax.Resolve(new(9, 4)).NumberText);
    }

    [Theory]
    [InlineData(PdfCrossReferenceFormat.Table)]
    [InlineData(PdfCrossReferenceFormat.Stream)]
    public void FreedObjectsDoNotResurrectOlderRevisions(PdfCrossReferenceFormat format)
    {
        using var original = Create();
        byte[] bytes = Save(original);
        using var input = new MemoryStream(bytes);
        var syntax = PdfSyntax.Read(input);
        using var edit = new PdfTransaction(syntax);
        edit.Remove(new(4));
        Assert.Equal(PdfValueKind.Null, edit.Resolve(new(4)).Kind);
        using var output = new MemoryStream();
        edit.Save(output, new(PdfWriteMode.Incremental, format));
        output.Position = 0;
        var updated = PdfSyntax.Read(output);
        Assert.Equal(PdfValueKind.Null, updated.Resolve(new(4)).Kind);
        Assert.Equal(PdfValueKind.Stream, updated.Resolve(new(4), 1).Kind);
        var free = updated.Revisions[0].Entries.Single(x => x.Id.Number == 4);
        Assert.Equal(1, free.Id.Generation);
        Assert.Equal(PdfCrossReferenceKind.Free, free.Kind);
    }

    [Fact]
    public void CancellationDuringOriginalByteCopyExpiresTheLeaseAndNextSaveRecovers()
    {
        byte[] original = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "writer", "signed-original.pdf"));
        using var input = new MemoryStream(original);
        var source = PdfSyntax.Read(input);
        using var edit = new PdfTransaction(source);
        using var cancel = new CancellationTokenSource();
        using var output = new FaultOutput(int.MaxValue, () => cancel.Cancel());
        Assert.Throws<OperationCanceledException>(() => edit.Save(output, new(PdfWriteMode.Incremental), cancel.Token));
        Assert.InRange(output.Bytes.Length, 1, original.Length - 1);
        Assert.True(output.CanWrite);
        Assert.False(edit.LastOperationReport!.Succeeded);
        using var recovered = new MemoryStream();
        edit.Save(recovered, new(PdfWriteMode.Incremental));
        Assert.Equal(original, recovered.ToArray());
    }

    [Fact]
    public void UnchangedIncrementalSaveIsByteExactAndCallerStreamSegmentsStayOwned()
    {
        using var original = Create();
        byte[] bytes = Save(original);
        using var input = new MemoryStream(bytes);
        var syntax = PdfSyntax.Read(input);
        using var edit = new PdfTransaction(syntax);
        using var output = new MemoryStream();
        output.WriteByte(42);
        edit.Save(output, new(PdfWriteMode.Incremental));
        Assert.Equal(42, output.ToArray()[0]);
        Assert.Equal(bytes, output.ToArray().Skip(1));
        output.Position = 1;
        Assert.Equal(syntax.Version, PdfSyntax.Read(output).Version);
        Assert.True(output.CanWrite);
    }

    [Fact]
    public void ClassicTrailerExtensionsAreRetainedAndStreamConflictsRejectBeforeWriting()
    {
        using var edit = Create();
        var entries = edit.Trailer.Entries.ToDictionary(x => x.Key, x => x.Value);
        entries.Add("Type", PdfValue.Name("OwnedTrailerExtension"));
        edit.Trailer = PdfValue.Dictionary(entries);
        using var output = new MemoryStream();
        edit.Save(output, new(crossReferenceFormat: PdfCrossReferenceFormat.Table));
        output.Position = 0;
        Assert.Equal("OwnedTrailerExtension", PdfSyntax.Read(output).Revisions[0].Trailer.Entries["Type"].NameValue);
        using var rejected = new MemoryStream();
        Assert.Equal("Writer.TrailerKeyConflict", Assert.Throws<PdfException>(() => edit.Save(rejected, new(crossReferenceFormat: PdfCrossReferenceFormat.Stream))).Diagnostic.Code);
        Assert.Equal(0, rejected.Length);
    }

    [Fact]
    public void StreamAuthoringHonorsInputBoundsAndLeavesBorrowedInputOpen()
    {
        using var input = new MemoryStream(Content);
        var value = PdfValues.Stream(Dictionary(), input);
        Assert.True(input.CanRead);
        Assert.Equal(Content.Length, value.RawStreamLength);
        input.Position = 0;
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => PdfValues.Stream(Dictionary(), input, new(limits: new PdfResourceLimits(maximumInputBytes: Content.Length - 1)))).Diagnostic.Code);
    }

    [Fact]
    public void NumericConversionRetainsFiniteValuesWithoutExponentTokens()
    {
        foreach (double value in new[]
        {
            double.Epsilon,
            double.MaxValue,
            double.MinValue,
            1e-200,
            -1e200,
            1.2345678901234567,
            0.84551240822557006,
            0d
        }

        )
        {
            var number = PdfValues.Real(value);
            Assert.DoesNotContain("E", number.NumberText);
            Assert.DoesNotContain("e", number.NumberText);
            Assert.Equal(value, number.NumberValue);
        }

        Assert.Throws<ArgumentOutOfRangeException>(() => PdfValues.Real(double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfValues.Real(double.PositiveInfinity));
    }

    [Fact]
    public void EveryOutputByteFaultPropagatesAndTheNextSaveRecovers()
    {
        using var edit = Create();
        byte[] valid = Save(edit);
        for (int length = 0; length < valid.Length; length++)
        {
            using var output = new FaultOutput(length);
            Assert.Same(output.Failure, Assert.Throws<IOException>(() => edit.Save(output)));
            Assert.Equal(length, output.Bytes.Length);
            Assert.True(output.CanWrite);
            Assert.False(edit.LastOperationReport!.Succeeded);
        }

        Assert.Equal(valid, Save(edit));
    }

    [Fact]
    public void PolicyFailureAndCancellationKeepAtomicDestinationAndCleanOwnedStaging()
    {
        string root = Path.Combine(Path.GetTempPath(), "fluent-pdf-writer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string path = Path.Combine(root, "existing.pdf");
        File.WriteAllText(path, "original");
        try
        {
            using var original = Create();
            using var input = new MemoryStream(Save(original));
            var syntax = PdfSyntax.Read(input);
            foreach (var limits in new[]
            {
                new PdfResourceLimits(maximumOutputBytes: 1),
                new PdfResourceLimits(maximumObjects: 1),
                new PdfResourceLimits(maximumDepth: 1),
                new PdfResourceLimits(maximumExpandedBytes: 1)
            }

            )
            {
                using var edit = new PdfTransaction(syntax, new(limits: limits));
                Assert.Throws<PdfException>(() => edit.SaveFile(path));
                Assert.Equal("original", File.ReadAllText(path));
                Assert.Single(Directory.GetFiles(root));
            }

            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            using var transaction = new PdfTransaction(syntax);
            Assert.Throws<OperationCanceledException>(() => transaction.SaveFile(path, cancellationToken: cancel.Token));
            Assert.Equal("original", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(root);
        }
    }

    [Fact]
    public void VersionAndSignaturePoliciesRejectUnsupportedRewritesBeforeOutput()
    {
        using var edit = Create();
        var signature = edit.Add(Dictionary(("ByteRange", PdfValue.Array(PdfValue.Integer(0), PdfValue.Integer(1), PdfValue.Integer(2), PdfValue.Integer(3))), ("Contents", PdfValue.String(0))));
        using var output = new MemoryStream();
        Assert.Equal("Writer.SignatureRewrite", Assert.Throws<PdfException>(() => edit.Save(output)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        Assert.Contains(edit.Save(output, new(allowSignatureInvalidation: true)).Diagnostics, x => x.Code == "Writer.SignatureRewriteAllowed");
        edit.Remove(signature);
        using var rejected = new MemoryStream();
        Assert.Equal("Writer.VersionTooLow", Assert.Throws<PdfException>(() => edit.Save(rejected, new(crossReferenceFormat: PdfCrossReferenceFormat.Stream, version: "1.4"))).Diagnostic.Code);
        Assert.Equal(0, rejected.Length);
    }

    internal static readonly byte[] Content = Encoding.ASCII.GetBytes("q\n0 0 10 10 re f\nQ\n% owned endstream marker\n");
    internal static PdfTransaction Create()
    {
        var edit = new PdfTransaction();
        edit.Add(Dictionary(("Type", PdfValue.Name("Catalog")), ("Pages", PdfValue.Reference(new(2))), ("Owned", PdfValue.Name("space #%\u00ff"))));
        edit.Add(Dictionary(("Type", PdfValue.Name("Pages")), ("Kids", PdfValue.Array(PdfValue.Reference(new(3)))), ("Count", PdfValue.Integer(1))));
        edit.Add(Dictionary(("Type", PdfValue.Name("Page")), ("Parent", PdfValue.Reference(new(2))), ("MediaBox", PdfValue.Array(PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(100), PdfValue.Integer(100))), ("Contents", PdfValue.Reference(new(4)))));
        using var input = new MemoryStream(Content);
        edit.Add(PdfValues.Stream(Dictionary(), input));
        edit.Trailer = Dictionary(("Root", PdfValue.Reference(new(1))), ("ID", PdfValue.Array(PdfValue.String(0, 255), PdfValue.String(1, 2))));
        return edit;
    }

    internal static byte[] Save(PdfTransaction transaction)
    {
        using var output = new MemoryStream();
        transaction.Save(output);
        return output.ToArray();
    }

    internal static PdfValue Dictionary(params (string Key, PdfValue Value)[] pairs) => PdfValue.Dictionary(pairs.Select(x => new KeyValuePair<string, PdfValue>(x.Key, x.Value)));
    private static byte[] Copy(Stream input)
    {
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }

    private sealed class FaultOutput : Stream
    {
        private readonly MemoryStream _bytes = new();
        private readonly int _limit;
        internal IOException Failure { get; } = new("owned byte-boundary fault");

        private readonly Action? _onWrite;
        internal FaultOutput(int limit, Action? onWrite = null)
        {
            _limit = limit;
            _onWrite = onWrite;
        }

        internal byte[] Bytes => _bytes.ToArray();
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _bytes.Length;
        public override long Position { get => _bytes.Position; set => throw new NotSupportedException(); }

        public override void Write(byte[] buffer, int offset, int count)
        {
            int n = Math.Min(count, _limit - (int)_bytes.Length);
            if (n > 0)
                _bytes.Write(buffer, offset, n);
            _onWrite?.Invoke();
            if (n < count)
                throw Failure;
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        protected override void Dispose(bool disposing)
        {
            _bytes.Dispose();
            base.Dispose(disposing);
        }
    }
}
