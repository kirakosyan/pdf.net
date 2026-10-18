using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ContentTests
{
    [Fact]
    public void CSharpAndVisualBasicConsumersEmitPageOperators()
    {
        foreach (var write in new Func<Stream, PdfOperationReport>[]
        {
            ContractExamples.CSharp.ContractExample.WriteRectangle,
            ContractExamples.VisualBasic.ContractExample.WriteRectangle
        }

        )
        {
            using var output = new MemoryStream();
            Assert.True(write(output).Succeeded);
            output.Position = 0;
            using var document = Pdf.Open(output);
            Assert.Contains(document.Pages[0].ReadContent().Operations, x => x.Operation == PdfOperator.Rectangle);
        }
    }

    private static PdfContentProgram Parse(string text, PdfOperationPolicy? policy = null) => PdfContentProgram.Parse(new MemoryStream(Encoding.ASCII.GetBytes(text)), policy: policy);
    [Fact]
    public void EveryStandardOperatorHasTypedGrammarAndRoundTripsWithoutChangingOperandBytes()
    {
        const string content = "q 1 0 0 1 7 9 cm 2 w 1 J 2 j 10 M [2 1] 0 d /RelativeColorimetric ri 50 i /GS gs " + "0 0 m 10 10 l 1 2 3 4 5 6 c 1 2 3 4 v 1 2 3 4 y h 0 0 2 2 re S s f F f* B B* b b* n W W* " + "/DeviceRGB CS /DeviceRGB cs 0 0 0 SC 0 0 0 /P SCN 0 0 0 sc /P scn 0 G 1 g 0 0 0 RG 1 0 0 rg 0 0 0 1 K 0 0 0 0 k /Shade sh /Object Do " + "/Tag MP /Tag << /MCID 1 >> DP /Tag BMC EMC /Tag /Props BDC EMC BX EX " + "BT 1 Tc 2 Tw 100 Tz 12 TL /Font 10 Tf 0 Tr 0 Ts 1 2 Td 0 -12 TD 1 0 0 1 0 0 Tm T* <00FF28> Tj [(abc) -12 <00FF>] TJ (line) ' 2 1 (next) \" ET Q";
        var program = Parse(content);
        Assert.Equal("1.3", program.MinimumVersion);
        foreach (PdfOperator value in Enum.GetValues(typeof(PdfOperator)))
            if (value != PdfOperator.Unknown && value != PdfOperator.InlineImage && value != PdfOperator.GlyphWidth && value != PdfOperator.GlyphBounds)
                Assert.Contains(program.Operations, x => x.Operation == value);
        using var output = new MemoryStream();
        Assert.True(program.Write(output).Succeeded);
        Assert.True(output.CanWrite);
        output.Position = 0;
        var reparsed = PdfContentProgram.Parse(output);
        Assert.Equal(program.Operations.Select(x => x.Operation), reparsed.Operations.Select(x => x.Operation));
        Assert.Equal(new byte[] { 0, 255, 40 }, reparsed.Operations.Single(x => x.Operation == PdfOperator.ShowText).Operands[0].StringBytes);
        var edited = program.Edit(0, 1, new[] { new PdfContentOperation(PdfOperator.SaveState) });
        Assert.NotSame(program, edited);
        Assert.Throws<PdfException>(() => program.Edit(0, 1, Array.Empty<PdfContentOperation>()));
    }

    [Theory]
    [InlineData("Q", "Content.GraphicsUnderflow")]
    [InlineData("q", "Content.UnbalancedState")]
    [InlineData("BT BT ET ET", "Content.NestedText")]
    [InlineData("ET", "Content.TextUnderflow")]
    [InlineData("(a) Tj", "Content.TextRequired")]
    [InlineData("BT 0 0 m ET", "Content.GraphicsInsideText")]
    [InlineData("BT q Q ET", "Content.GraphicsInsideText")]
    [InlineData("EMC", "Content.MarkedUnderflow")]
    [InlineData("/Tag BMC", "Content.UnbalancedState")]
    [InlineData("EX", "Content.CompatibilityUnderflow")]
    [InlineData("BX", "Content.UnbalancedState")]
    [InlineData("1 2", "Content.MissingOperator")]
    [InlineData("1 w 2 w 3 J", "Content.OperatorGrammar")]
    [InlineData("[0 0] 0 d", "Content.OperatorGrammar")]
    [InlineData("[1 (x)] 0 d", "Content.OperatorGrammar")]
    [InlineData("BT [(a) /Bad] TJ ET", "Content.OperatorGrammar")]
    [InlineData("/Tag << /Ref 1 0 R >> DP", "Content.OperatorGrammar")]
    [InlineData("500 0 d0", "Content.GlyphMetrics")]
    [InlineData("EI", "Content.UnexpectedInlineMarker")]
    [InlineData("ID", "Content.UnexpectedInlineMarker")]
    public void MalformedOrUnbalancedProgramsFailPrecisely(string content, string code)
    {
        Assert.Equal(code, Assert.Throws<PdfException>(() => Parse(content)).Diagnostic.Code);
    }

    [Fact]
    public void UnknownOperatorsRequireAnExplicitPolicyAndVersionFloorAndRetainTheirOperands()
    {
        byte[] bytes = Encoding.ASCII.GetBytes("BX 12 <00FF> vendor_op EX");
        Assert.Equal("Content.UnknownOperator", Assert.Throws<PdfException>(() => PdfContentProgram.Parse(new MemoryStream(bytes))).Diagnostic.Code);
        Assert.Throws<ArgumentException>(() => PdfContentProgram.Parse(new MemoryStream(bytes), unknownOperators: PdfUnknownOperatorPolicy.Preserve));
        var program = PdfContentProgram.Parse(new MemoryStream(bytes), unknownOperators: PdfUnknownOperatorPolicy.Preserve, unknownOperatorVersion: "2.0");
        Assert.Equal("2.0", program.MinimumVersion);
        Assert.Equal("vendor_op", program.Operations[1].Token);
        Assert.Equal(new byte[] { 0, 255 }, program.Operations[1].Operands[1].StringBytes);
        Assert.Throws<ArgumentException>(() => PdfContentOperation.Extension("123", "1.7"));
        Assert.Throws<ArgumentException>(() => PdfContentOperation.Extension("q", "1.7"));
    }

    [Fact]
    public void Type3MetricsAreRequiredExactlyOnceAtTheBeginning()
    {
        using var input = new MemoryStream(Encoding.ASCII.GetBytes("500 0 d0 0 0 10 10 re f"));
        Assert.Equal(PdfOperator.GlyphWidth, PdfContentProgram.Parse(input, PdfContentKind.Type3Glyph).Operations[0].Operation);
        foreach (string text in new[]
        {
            "q Q",
            "q Q 500 0 d0",
            "500 0 d0 500 0 d0"
        }

        )
            Assert.Equal("Content.GlyphMetrics", Assert.Throws<PdfException>(() => PdfContentProgram.Parse(new MemoryStream(Encoding.ASCII.GetBytes(text)), PdfContentKind.Type3Glyph)).Diagnostic.Code);
        var glyph = PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.GlyphBounds, PdfValue.Integer(500), PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(500), PdfValue.Integer(500)) }, PdfContentKind.Type3Glyph);
        using var document = Pdf.Create().AddPage();
        Assert.Throws<ArgumentException>(() => document.Pages[0].Content = glyph);
    }

    [Fact]
    public void ContentBudgetsCancellationAndBorrowedStreamsApplyToEachBoundary()
    {
        var policy = new PdfOperationPolicy(limits: new PdfResourceLimits(maximumObjects: 3));
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => Parse("q Q q Q", policy)).Diagnostic.Code);
        Assert.Equal("Limit.Depth", Assert.Throws<PdfException>(() => Parse("q q q Q Q Q", new(limits: new(maximumDepth: 2)))).Diagnostic.Code);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        using var input = new MemoryStream(Encoding.ASCII.GetBytes("q Q"));
        Assert.Throws<OperationCanceledException>(() => PdfContentProgram.Parse(input, cancellationToken: canceled.Token));
        Assert.True(input.CanRead);
        var content = Parse("q Q");
        using var output = new MemoryStream();
        Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => content.Write(output, new(limits: new(maximumOutputBytes: 2)))).Diagnostic.Code);
        Assert.True(output.CanWrite);
        Assert.True(content.Write(output).Succeeded);
    }

    [Fact]
    public void PageContentEditingIsDetachedAndSupportsIncrementalBytePreservation()
    {
        using var original = Pdf.Create().AddPage();
        original.Pages[0].Content = Parse("q 0.5 g 0 0 20 20 re f Q");
        var snapshot = original.CreateSnapshot();
        original.Pages[0].Content = Parse("q Q");
        Assert.Equal(5, snapshot.Pages[0].Content!.Operations.Count);
        using var first = new MemoryStream();
        original.Save(first);
        byte[] prefix = first.ToArray();
        first.Position = 0;
        using var document = Pdf.Open(first);
        Assert.Equal(2, document.Pages[0].ReadContent().Operations.Count);
        document.Pages[0].Content = Parse("q 1 0 0 rg 1 2 3 4 re f Q");
        using var updated = new MemoryStream();
        document.Save(updated, writeOptions: new(PdfWriteMode.Incremental));
        Assert.Equal(prefix, updated.ToArray().Take(prefix.Length));
        updated.Position = 0;
        using var reopened = Pdf.Open(updated);
        Assert.Contains(reopened.Pages[0].ReadContent().Operations, x => x.Operation == PdfOperator.FillRgb);
        document.Pages[0].Content = null;
        Assert.Equal(2, document.Pages[0].ReadContent().Operations.Count);
    }

    [Fact]
    public void OperatorsAndStreamsCanCrossContentsArrayBoundariesWithoutLosingState()
    {
        using var source = new SyntaxFixtures("1.2");
        long root = source.Object(1, "<< /Type /Catalog /Pages 2 0 R >>"), tree = source.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>"), page = source.Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] /Resources << >> /Contents [4 0 R 5 0 R] >>");
        long first = source.StreamObject(4, "", Encoding.ASCII.GetBytes("q BT /F 12 Tf")), second = source.StreamObject(5, "", Encoding.ASCII.GetBytes("(abc) Tj ET Q"));
        source.Classic(new[] { (0, 65535, 0L, true), (1, 0, root, false), (2, 0, tree, false), (3, 0, page, false), (4, 0, first, false), (5, 0, second, false) }, "/Root 1 0 R", size: 6);
        source.Bytes.Position = 0;
        using var document = Pdf.Open(source.Bytes);
        var content = document.Pages[0].ReadContent();
        Assert.Equal(6, content.Operations.Count);
        Assert.True(document.LastOperationReport!.Succeeded);
        document.Pages[0].Content = content;
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        Assert.Equal(content.Operations.Select(x => x.Token), reopened.Pages[0].ReadContent().Operations.Select(x => x.Token));
    }

    [Fact]
    public void EveryWriteFailureLeavesTheBorrowedOutputOpenAndTheProgramReusable()
    {
        var content = Parse("q 1 0 0 rg 0 0 10 10 re f Q");
        using var expected = new MemoryStream();
        content.Write(expected);
        for (int limit = 0; limit < expected.Length; limit++)
        {
            using var output = new FailingOutput(limit);
            Assert.Throws<IOException>(() => content.Write(output));
            Assert.True(output.CanWrite);
        }

        using var next = new MemoryStream();
        content.Write(next);
        Assert.Equal(expected.ToArray(), next.ToArray());
    }

    private sealed class FailingOutput : Stream
    {
        private readonly int _limit;
        private int _written;
        internal FailingOutput(int limit) => _limit = limit;
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => _written;
        public override long Position { get => _written; set => throw new NotSupportedException(); }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count)
        {
            for (int i = 0; i < count; i++)
            {
                if (_written == _limit)
                    throw new IOException("Original injected content write failure.");
                _written++;
            }
        }
    }
}
