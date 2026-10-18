using System.IO;
using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class FilterTests
{
    [Theory]
    [InlineData("ASCII85Decode", "87cURD_*#TDfTZ)+T~>", "Hello, world!")]
    [InlineData("ASCIIHexDecode", "48656c6c6f2c20776f726c6421>", "Hello, world!")]
    public void KnownAnswerTextFilters(string name, string encoded, string expected) => Assert.Equal(Encoding.ASCII.GetBytes(expected), Decode(Encoding.ASCII.GetBytes(encoded), "/Filter /" + name));
    [Fact]
    public void KnownAnswerRunLengthAndLzwSpecialCases()
    {
        Assert.Equal(Encoding.ASCII.GetBytes("ABCZZZ"), Decode(new byte[] { 2, 65, 66, 67, 254, 90, 128 }, "/Filter /RunLengthDecode"));
        Assert.Equal(new byte[4], Decode(Encoding.ASCII.GetBytes("z~>"), "/Filter /ASCII85Decode"));
        Assert.Equal(Encoding.ASCII.GetBytes("ABABABA"), Decode(Pack9(256, 65, 66, 258, 260, 257), "/Filter /LZWDecode"));
        Assert.Equal(new byte[] { 0xA1, 0xF0 }, Decode(Encoding.ASCII.GetBytes("A1 F>"), "/Filter /ASCIIHexDecode"));
    }

    [Fact]
    public void IndependentTiffLzwFixtureExercisesWidthChangesAndDictionaryReuse()
    {
        byte[] expected = Enumerable.Range(0, 4096).Select(i => (byte)((i * 37 + i / 11) % 256)).ToArray();
        byte[] encoded = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "filters", "lzw-earlychange-1.bin"));
        Assert.Equal(expected, Decode(encoded, "/Filter /LZWDecode /DecodeParms << /EarlyChange 1 >>"));
        Assert.Equal(encoded, Encode(expected, new PdfFilter("LZWDecode", Dictionary(("EarlyChange", 1)))));
    }

    [Theory]
    [InlineData("FlateDecode")]
    [InlineData("LZWDecode")]
    [InlineData("ASCII85Decode")]
    [InlineData("ASCIIHexDecode")]
    [InlineData("RunLengthDecode")]
    public void EncodersHandleEmptyShortAndLargeInputsAndLeaveStreamsOpen(string name)
    {
        foreach (int length in new[]
        {
            0,
            1,
            2,
            3,
            4,
            127,
            128,
            129,
            4095,
            9000
        }

        )
        {
            var random = new Random(423 + length);
            byte[] original = new byte[length];
            random.NextBytes(original);
            foreach (int early in name == "LZWDecode" ? new[]
            {
                0,
                1
            }

            : new[]
            {
                1
            }

            )
            {
                var parameters = Dictionary(("EarlyChange", early));
                byte[] encoded = Encode(original, new PdfFilter(name, parameters));
                Assert.Equal(original, Decode(encoded, "/Filter /" + name + (name == "LZWDecode" ? " /DecodeParms << /EarlyChange " + early + " >>" : "")));
            }
        }
    }

    [Fact]
    public void EncodingMatchesIndependentAsciiAndLzwAnswersAndChainOrder()
    {
        byte[] hello = Encoding.ASCII.GetBytes("Hello, world!");
        Assert.Equal("87cURD_*#TDfTZ)+T~>", Encoding.ASCII.GetString(Encode(hello, new PdfFilter("ASCII85Decode"))));
        Assert.Equal(Pack9(256, 65, 66, 258, 260, 257), Encode(Encoding.ASCII.GetBytes("ABABABA"), new PdfFilter("LZWDecode")));
        byte[] input = Enumerable.Range(0, 8192).Select(i => (byte)(i % 223)).ToArray();
        var chain = new[]
        {
            new PdfFilter("A85"),
            new PdfFilter("Fl")
        };
        byte[] encoded = Encode(input, chain);
        Assert.Equal(input, Decode(encoded, "/Filter [/A85 /Fl] /DecodeParms [null null]"));
    }

    [Theory]
    [InlineData(2)]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void PredictorMatrixPreservesPackedPaddingAndSixteenBitSamples(int predictor)
    {
        foreach (int bits in new[]
        {
            1,
            2,
            4,
            8,
            16
        }

        )
            foreach (int colors in new[]
            {
                1,
                3
            }

            )
            {
                int columns = 7, row = (columns * colors * bits + 7) / 8;
                var random = new Random(bits * colors + predictor);
                byte[] original = new byte[row * 5];
                random.NextBytes(original);
                var parameters = Dictionary(("Predictor", predictor), ("Colors", colors), ("BitsPerComponent", bits), ("Columns", columns));
                byte[] encoded = Encode(original, new PdfFilter("FlateDecode", parameters));
                Assert.Equal(original, Decode(encoded, $"/Filter /FlateDecode /DecodeParms << /Predictor {predictor} /Colors {colors} /BitsPerComponent {bits} /Columns {columns} >>"));
            }
    }

    [Theory]
    [InlineData(10, new byte[] { 0, 10, 20, 30, 0, 11, 22, 33 })]
    [InlineData(11, new byte[] { 1, 10, 10, 10, 1, 11, 11, 11 })]
    [InlineData(12, new byte[] { 2, 10, 20, 30, 2, 1, 2, 3 })]
    [InlineData(13, new byte[] { 3, 10, 15, 20, 3, 6, 7, 7 })]
    [InlineData(14, new byte[] { 4, 10, 10, 10, 4, 1, 2, 3 })]
    public void PngPredictionHasIndependentKnownAnswers(int predictor, byte[] rows)
    {
        Assert.Equal(new byte[] { 10, 20, 30, 11, 22, 33 }, Decode(SyntaxFixtures.ZlibStored(rows), $"/Filter /FlateDecode /DecodeParms << /Predictor {predictor} /Columns 3 >>"));
    }

    [Fact]
    public void TiffPackedAndSixteenBitPredictionHaveIndependentAnswers()
    {
        Assert.Equal(new byte[] { 0x6C }, Decode(SyntaxFixtures.ZlibStored(new byte[] { 0x54 }), "/Filter /FlateDecode /DecodeParms << /Predictor 2 /Columns 3 /BitsPerComponent 2 >>"));
        Assert.Equal(new byte[] { 0x12, 0x34, 0x12, 0xFF }, Decode(SyntaxFixtures.ZlibStored(new byte[] { 0x12, 0x34, 0, 0xCB }), "/Filter /FlateDecode /DecodeParms << /Predictor 2 /Columns 2 /BitsPerComponent 16 >>"));
    }

    [Theory]
    [InlineData("ASCII85Decode", "!~>", "Filter.Ascii85Malformed")]
    [InlineData("ASCII85Decode", "!z~>", "Filter.Ascii85Malformed")]
    [InlineData("ASCII85Decode", "uuuuu~>", "Filter.Ascii85Malformed")]
    [InlineData("ASCII85Decode", "abcd", "Filter.Truncated")]
    [InlineData("ASCII85Decode", "~>X", "Filter.TrailingData")]
    [InlineData("ASCIIHexDecode", "Z>", "Filter.Malformed")]
    [InlineData("ASCIIHexDecode", "AA", "Filter.Truncated")]
    [InlineData("UnknownFilter", "", "Filter.Unsupported")]
    public void MalformedAndUnsupportedFiltersCannotReturnContent(string filter, string bytes, string code)
    {
        Assert.Equal(code, Assert.Throws<PdfException>(() => Decode(Encoding.ASCII.GetBytes(bytes), "/Filter /" + filter)).Diagnostic.Code);
    }

    [Fact]
    public void MalformedBinaryFiltersAndParameterChainsAreRejected()
    {
        foreach (byte[] bytes in new[]
        {
            new byte[]
            {
                0
            },
            new byte[]
            {
                129
            },
            new byte[]
            {
                0,
                9
            }
        }

        )
            Assert.Equal("Filter.Truncated", Assert.Throws<PdfException>(() => Decode(bytes, "/Filter /RunLengthDecode")).Diagnostic.Code);
        Assert.Equal("Filter.LzwMalformed", Assert.Throws<PdfException>(() => Decode(Pack9(256, 258, 257), "/Filter /LZWDecode")).Diagnostic.Code);
        Assert.Equal("Filter.Parameters", Assert.Throws<PdfException>(() => Decode(Pack9(256, 257), "/Filter /LZWDecode /DecodeParms << /EarlyChange 2 >>")).Diagnostic.Code);
        Assert.Equal("Filter.Parameters", Assert.Throws<PdfException>(() => Decode(new byte[0], "/Filter [/Fl /A85] /DecodeParms [null]")).Diagnostic.Code);
    }

    [Fact]
    public void ExpansionLimitsLatchAndDetachedResultsOutliveTheOperation()
    {
        byte[] bomb = Enumerable.Repeat(new byte[] { 129, 0 }, 64).SelectMany(x => x).Concat(new byte[] { 128 }).ToArray();
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => Decode(bomb, "/Filter /RunLengthDecode", new PdfResourceLimits(maximumExpandedBytes: 4096))).Diagnostic.Code);
        Stream? detached = null;
        var value = StreamValue(Encoding.ASCII.GetBytes("4142>"), "/Filter /ASCIIHexDecode");
        PolicyTests.Run(context => detached = PdfFilters.Decode(value, context));
        using (detached)
        {
            Assert.Equal(new byte[] { 65, 66 }, Copy(detached!));
            Assert.False(detached!.CanWrite);
        }
    }

    [Fact]
    public void DocumentDecodeResolvesIndirectDeclarationsAndReturnsItsOwnReport()
    {
        using var fixture = new SyntaxFixtures();
        long filter = fixture.Object(2, "[/ASCIIHexDecode]"), parameters = fixture.Object(3, "[null]");
        long stream = fixture.StreamObject(1, "/Filter 2 0 R /DecodeParms 3 0 R", Encoding.ASCII.GetBytes("4142>"));
        fixture.Classic(new[] { (1, 0, stream, false), (2, 0, filter, false), (3, 0, parameters, false) });
        fixture.Bytes.Position = 0;
        var syntax = PdfSyntax.Read(fixture.Bytes);
        var decoded = syntax.DecodeStream(new(1));
        Assert.Equal(2,ContractExamples.CSharp.ContractExample.DecodeStream(syntax,new(1)).Length);
        Assert.Equal(2,ContractExamples.VisualBasic.ContractExample.DecodeStream(syntax,new(1)).Length);
        Assert.Equal(2, decoded.Length);
        Assert.True(decoded.OperationReport.Succeeded);
        using var view = decoded.OpenRead();
        Assert.Equal(new byte[] { 65, 66 }, Copy(view));
        Assert.Equal("Filter.StreamRequired", Assert.Throws<PdfException>(() => syntax.DecodeStream(new(2))).Diagnostic.Code);
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => syntax.DecodeStream(new(1), policy: new PdfOperationPolicy(limits: new PdfResourceLimits(maximumInputBytes: 4)))).Diagnostic.Code);
    }

    [Fact]
    public void CyclicIndirectFilterDeclarationsAndMalformedPredictorParametersFailPrecisely()
    {
        using var fixture = new SyntaxFixtures();
        long cycle = fixture.Object(2, "2 0 R");
        long stream = fixture.StreamObject(1, "/Filter 2 0 R", new byte[0]);
        fixture.Classic(new[] { (1, 0, stream, false), (2, 0, cycle, false) });
        fixture.Bytes.Position = 0;
        var syntax = PdfSyntax.Read(fixture.Bytes);
        Assert.Equal("Filter.ParameterCycle", Assert.Throws<PdfException>(() => syntax.DecodeStream(new(1))).Diagnostic.Code);
        Assert.Equal("Filter.Parameters", Assert.Throws<PdfException>(() => Decode(SyntaxFixtures.ZlibStored(new byte[0]), "/Filter /FlateDecode /DecodeParms << /Predictor 9999999999999999999999999 >>")).Diagnostic.Code);
    }

    private static PdfValue Dictionary(params (string name, int value)[] pairs) => PdfValue.Dictionary(pairs.Select(p => new KeyValuePair<string, PdfValue>(p.name, PdfValue.Integer(p.value))));
    private static byte[] Encode(byte[] bytes, params PdfFilter[] filters)
    {
        byte[]? result = null;
        using var input = new MemoryStream(bytes);
        PolicyTests.Run(context =>
        {
            using var encoded = PdfFilters.Encode(input, filters, context);
            result = Copy(encoded);
        });
        Assert.True(input.CanRead);
        return result!;
    }

    private static byte[] Decode(byte[] bytes, string dictionary, PdfResourceLimits? limits = null)
    {
        var value = StreamValue(bytes, dictionary);
        byte[]? result = null;
        PolicyTests.Run(context =>
        {
            using var decoded = PdfFilters.Decode(value, context);
            result = Copy(decoded);
        }, limits);
        return result!;
    }

    private static PdfValue StreamValue(byte[] bytes, string dictionary)
    {
        using var fixture = new SyntaxFixtures();
        long offset = fixture.StreamObject(1, dictionary, bytes);
        fixture.Classic(new[] { (1, 0, offset, false) });
        fixture.Bytes.Position = 0;
        return PdfSyntax.Read(fixture.Bytes).Resolve(new(1));
    }

    private static byte[] Pack9(params int[] codes)
    {
        var bytes = new List<byte>();
        int bit = 0, current = 0;
        foreach (int code in codes)
            for (int i = 8; i >= 0; i--)
            {
                current = (current << 1) | ((code >> i) & 1);
                if (++bit == 8)
                {
                    bytes.Add((byte)current);
                    bit = 0;
                    current = 0;
                }
            }

        if (bit > 0)
            bytes.Add((byte)(current << (8 - bit)));
        return bytes.ToArray();
    }

    private static byte[] Copy(Stream source)
    {
        using var target = new MemoryStream();
        source.CopyTo(target);
        return target.ToArray();
    }
}
