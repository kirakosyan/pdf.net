using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class SyntaxTests
{
    [Theory]
    [InlineData("1.2")]
    [InlineData("1.3")]
    [InlineData("1.4")]
    [InlineData("1.5")]
    [InlineData("1.6")]
    [InlineData("1.7")]
    [InlineData("2.0")]
    public void ReadsSupportedHeadersAndRetainsExactValues(string version)
    {
        var syntax = Read(SyntaxFixtures.Simple("<< /Na#6de /A#2FB /V [null true false -000.50 9999999999999999999999999999 <A1F> (a\\n\\101\\\r\nb(c)d)] >>", version));
        var dictionary = syntax.Resolve(new(1));
        Assert.Equal(version, syntax.Version);
        Assert.Equal("A/B", dictionary.Entries["Name"].NameValue);
        var values = dictionary.Entries["V"].Items;
        Assert.Same(PdfValue.Null, values[0]);
        Assert.True(values[1].BooleanValue);
        Assert.False(values[2].BooleanValue);
        Assert.Equal("-000.50", values[3].NumberText);
        Assert.Equal(-0.5, values[3].NumberValue);
        Assert.Equal("9999999999999999999999999999", values[4].NumberText);
        Assert.Equal(new byte[] { 0xA1, 0xF0 }, values[5].StringBytes);
        Assert.Equal(Encoding.ASCII.GetBytes("a\nAb(c)d"), values[6].StringBytes);
        Assert.True(syntax.OperationReport!.Succeeded);
        Assert.Empty(syntax.OperationReport.Diagnostics);
    }

    [Fact]
    public void ResolvesGenerationsAndFreeEntriesWithoutResurrectingOlderObjects()
    {
        using var f = new SyntaxFixtures();
        long old = f.Object(1, "(old)"), first = f.Classic(new[] { (0, 65535, 0L, true), (1, 0, old, false) });
        long second = f.Classic(new[] { (1, 1, 0L, true) }, "/Prev " + first);
        long fresh = f.Object(1, "(new)", 1);
        f.Classic(new[] { (1, 1, fresh, false) }, "/Prev " + second);
        var syntax = Read(f.Bytes.ToArray());
        Assert.Equal(3, syntax.Revisions.Count);
        Assert.Equal(Encoding.ASCII.GetBytes("new"), syntax.Resolve(new(1, 1)).StringBytes);
        Assert.Same(PdfValue.Null, syntax.Resolve(new(1)));
        Assert.Same(PdfValue.Null, syntax.Resolve(new(1), 1));
        Assert.Equal(Encoding.ASCII.GetBytes("old"), syntax.Resolve(new(1), 2).StringBytes);
        Assert.Same(PdfValue.Null, syntax.Resolve(new(0)));
        Assert.Same(PdfValue.Null, syntax.Resolve(new(14)));
        Assert.Throws<ArgumentOutOfRangeException>(() => syntax.Resolve(new(1), 3));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void ReadsCrossReferenceStreamsWithFlateAndPngRows(bool flate, bool predictor)
    {
        var syntax = Read(SyntaxFixtures.CrossReferenceStream(flate, predictor));
        Assert.Equal("preserved", Encoding.ASCII.GetString(syntax.Resolve(new(1)).Entries["Unknown"].StringBytes.ToArray()));
        Assert.Equal(PdfValueKind.Stream, syntax.Revisions[0].Trailer.Kind);
        Assert.Equal(3, syntax.Revisions[0].Entries.Count);
    }

    [Fact]
    public void HybridStreamOverridesClassicEntries()
    {
        using var f = new SyntaxFixtures();
        long old = f.Object(1, "(old)"), fresh = f.Object(1, "(new)");
        long supplemental = f.StreamObject(2, "/Type /XRef /Size 3 /W [1 4 2] /Index [1 1]", SyntaxFixtures.Entry(1, fresh, 0));
        f.Classic(new[] { (0, 65535, 0L, true), (1, 0, old, false), (2, 0, supplemental, false) }, "/XRefStm " + supplemental, size: 3);
        var syntax = Read(f.Bytes.ToArray());
        Assert.True(syntax.Revisions[0].IsHybrid);
        Assert.Equal(Encoding.ASCII.GetBytes("new"), syntax.Resolve(new(1)).StringBytes);
    }

    [Fact]
    public void PreservesOpaqueStreamsAndIndirectLengthsWithoutExecutingActions()
    {
        using var f = new SyntaxFixtures();
        long length = f.Object(2, "4");
        long stream = f.Bytes.Position;
        f.Text("1 0 obj\n<< /Length 2 0 R /Filter /UnknownFilter /A << /S /JavaScript /JS (never run) >> >>\nstream\n");
        byte[] raw =
        {
            0,
            255,
            10,
            37
        };
        f.Bytes.Write(raw, 0, raw.Length);
        f.Text("\nendstream\nendobj\n");
        f.Classic(new[] { (0, 65535, 0L, true), (1, 0, stream, false), (2, 0, length, false) });
        byte[] original = f.Bytes.ToArray();
        var syntax = Read(original);
        var value = syntax.Resolve(new(1));
        Assert.Equal(4, value.RawStreamLength);
        using (var view = value.OpenRawStream())
            Assert.Equal(raw, Copy(view));
        Assert.Equal("UnknownFilter", value.Entries["Filter"].NameValue);
        Assert.Equal("JavaScript", value.Entries["A"].Entries["S"].NameValue);
        using var file = syntax.OpenOriginalStream();
        Assert.Equal(original, Copy(file));
        Assert.Throws<NotSupportedException>(() => file.WriteByte(1));
    }

    [Fact]
    public void RejectsOffsetCyclesCollisionsAndIntegerOverflow()
    {
        using var f = new SyntaxFixtures();
        long one = f.Object(1, "null");
        long xref = f.Bytes.Position;
        f.Classic(new[] { (1, 0, one, false) }, "/Prev " + xref);
        Assert.Equal("Syntax.XrefCycle", Failure(f.Bytes.ToArray()));
        using var collision = new SyntaxFixtures();
        long two = collision.Object(2, "null");
        collision.Classic(new[] { (1, 0, two, false) });
        Assert.Equal("Syntax.ObjectIdentity", Failure(collision.Bytes.ToArray()));
        byte[] overflow = Encoding.ASCII.GetBytes("%PDF-1.7\nstartxref\n9223372036854775808\n%%EOF\n");
        Assert.Equal("Syntax.IntegerRange", Failure(overflow));
        byte[] outside = Encoding.ASCII.GetBytes("%PDF-1.7\nstartxref\n9223372036854775807\n%%EOF\n");
        Assert.Equal("Syntax.OffsetRange", Failure(outside));
    }

    [Theory]
    [InlineData("<< /A 1 /#41 2 >>", "Syntax.DuplicateKey")]
    [InlineData("[1 65536 R]", "Syntax.ReferenceRange")]
    [InlineData("[2147483648 0 R]", "Syntax.ReferenceRange")]
    [InlineData("(unfinished", "Syntax.Truncated")]
    [InlineData("<0Z>", "Syntax.HexString")]
    [InlineData("/A#0G", "Syntax.NameEscape")]
    [InlineData("1e3", "Syntax.Value")]
    [InlineData("<< /Length 999999 >>\nstream\na", "Syntax.StreamLength")]
    public void RejectsMalformedValuesWithStableDiagnostics(string body, string code) => Assert.Equal(code, Failure(SyntaxFixtures.Simple(body)));
    [Fact]
    public void LengthCyclesCannotConsumeUnboundedStack()
    {
        using var f = new SyntaxFixtures();
        long one = f.Object(1, "<< /Length 2 0 R >>\nstream\n\nendstream"), two = f.Object(2, "<< /Length 1 0 R >>\nstream\n\nendstream");
        f.Classic(new[] { (1, 0, one, false), (2, 0, two, false) });
        Assert.Equal("Syntax.ObjectCycle", Failure(f.Bytes.ToArray()));
    }

    [Fact]
    public void EnforcesInputObjectExpansionDepthAndCancellationBoundaries()
    {
        byte[] bytes = SyntaxFixtures.Simple("[[[true]]] ");
        var baseline = Read(bytes);
        Assert.Equal(bytes.Length, baseline.OperationReport!.Resources.Single(x => x.Resource == PdfResourceKind.InputBytes).Consumed);
        Read(bytes, new PdfResourceLimits(maximumInputBytes: bytes.Length));
        Assert.Equal("Limit.InputBytes", Failure(bytes, new PdfResourceLimits(maximumInputBytes: bytes.Length - 1)));
        long objects = baseline.OperationReport.Resources.Single(x => x.Resource == PdfResourceKind.Objects).Consumed;
        Read(bytes, new PdfResourceLimits(maximumObjects: objects));
        Assert.Equal("Limit.Objects", Failure(bytes, new PdfResourceLimits(maximumObjects: objects - 1)));
        long expanded = baseline.OperationReport.Resources.Single(x => x.Resource == PdfResourceKind.ExpandedBytes).Consumed;
        Read(bytes, new PdfResourceLimits(maximumExpandedBytes: expanded));
        Assert.Equal("Limit.ExpandedBytes", Failure(bytes, new PdfResourceLimits(maximumExpandedBytes: expanded - 1)));
        int depth = (int)baseline.OperationReport.Resources.Single(x => x.Resource == PdfResourceKind.Depth).Peak;
        Read(bytes, new PdfResourceLimits(maximumDepth: depth));
        Assert.Equal("Limit.Depth", Failure(bytes, new PdfResourceLimits(maximumDepth: depth - 1)));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        using var input = new MemoryStream(bytes);
        Assert.ThrowsAny<OperationCanceledException>(() => PdfSyntax.Read(input, cancellationToken: cancellation.Token));
        Assert.True(input.CanRead);
    }

    [Fact]
    public void ReadsBorrowedNonseekableInputAndHonorsItsCurrentPosition()
    {
        byte[] bytes = SyntaxFixtures.Simple("42");
        using var input = new MemoryStream();
        input.Write(new byte[7], 0, 7);
        input.Write(bytes, 0, bytes.Length);
        input.Position = 7;
        var syntax = PdfSyntax.Read(input);
        Assert.True(input.CanRead);
        Assert.Equal(42, syntax.Resolve(new(1)).NumberValue);
        using var nonseekable = new NonSeekableStream(bytes);
        Assert.Equal(42, PdfSyntax.Read(nonseekable).Resolve(new(1)).NumberValue);
        Assert.True(nonseekable.CanRead);
    }

    [Fact]
    public void CSharpAndVisualBasicConsumersInspectActualPdfSyntax()
    {
        byte[] source = SyntaxFixtures.Simple("<< /Value 123 >>");
        using var csharp = new MemoryStream(source);
        using var vb = new MemoryStream(source);
        Assert.Equal(123, ContractExamples.CSharp.ContractExample.InspectSyntax(csharp).Resolve(new(1)).Entries["Value"].NumberValue);
        Assert.Equal(123, ContractExamples.VisualBasic.ContractExample.InspectSyntax(vb).Resolve(new(1)).Entries["Value"].NumberValue);
        Assert.True(csharp.CanRead);
        Assert.True(vb.CanRead);
    }

    [Fact]
    public void IndependentlyInspectedPageFixtureHasItsExpectedCatalogAndOpaqueContent()
    {
        using var source = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "syntax", "one-page-classic.pdf"));
        var syntax = PdfSyntax.Read(source);
        var root = syntax.Resolve(syntax.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        Assert.Equal("Catalog", root.Entries["Type"].NameValue);
        var pages = syntax.Resolve(root.Entries["Pages"].ReferenceId);
        Assert.Equal(1, pages.Entries["Count"].NumberValue);
        var page = syntax.Resolve(pages.Entries["Kids"].Items[0].ReferenceId);
        Assert.Equal(new double[] { 0, 0, 612, 792 }, page.Entries["MediaBox"].Items.Select(v => v.NumberValue));
        using var raw = syntax.Resolve(page.Entries["Contents"].ReferenceId).OpenRawStream();
        Assert.Equal("BT /F1 12 Tf 72 720 Td (Original syntax fixture) Tj ET\n", Encoding.ASCII.GetString(Copy(raw)));
    }

    [Fact]
    public void ImmutableFactoriesRejectInvalidValuesAndOwnTheirInputs()
    {
        byte[] data =
        {
            1,
            2
        };
        var value = PdfValue.String(data);
        data[0] = 9;
        Assert.Equal(1, value.StringBytes[0]);
        var items = new[]
        {
            value
        };
        var array = PdfValue.Array(items);
        items[0] = PdfValue.Null;
        Assert.Same(value, array.Items[0]);
        Assert.Throws<ArgumentException>(() => PdfValue.Name("\u0100"));
        Assert.Throws<ArgumentException>(() => PdfValue.Number("1E2"));
        Assert.Throws<InvalidOperationException>(() => value.NumberValue);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfObjectId(1, 65536));
    }

    [Fact]
    public void TruncatedSyntaxAndDeterministicMutationsFailSafelyOrRemainInspectable()
    {
        byte[] source = SyntaxFixtures.Simple("<< /A [1 2 3] /B (original) >>");
        int end = Encoding.ASCII.GetString(source).IndexOf("%%EOF", StringComparison.Ordinal) + 5;
        for (int length = 0; length < end; length++)
            Assert.Throws<PdfException>(() => Read(source.Take(length).ToArray()));
        var random = new Random(2187);
        for (int iteration = 0; iteration < 1000; iteration++)
        {
            byte[] mutation = (byte[])source.Clone();
            mutation[random.Next(mutation.Length)] = (byte)random.Next(256);
            try
            {
                Read(mutation);
            }
            catch (PdfException)
            {
            }
        }
    }

    private static PdfSyntaxDocument Read(byte[] bytes, PdfResourceLimits? limits = null)
    {
        using var input = new MemoryStream(bytes);
        return PdfSyntax.Read(input, new PdfOperationPolicy(limits: limits));
    }

    private static string Failure(byte[] bytes, PdfResourceLimits? limits = null) => Assert.Throws<PdfException>(() => Read(bytes, limits)).Diagnostic.Code;
    private static byte[] Copy(Stream input)
    {
        using var result = new MemoryStream();
        input.CopyTo(result);
        return result.ToArray();
    }
}
