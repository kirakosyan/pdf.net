using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using Xunit;
using Xunit.Abstractions;

namespace Fluent.Pdf.Contract.Tests;
public sealed class UnicodeConformanceTests
{
    private readonly ITestOutputHelper _output;
    public UnicodeConformanceTests(ITestOutputHelper output) => _output = output;
    [Fact]
    public void OfficialExtendedGraphemeBreakVectorsPassWithoutRuntimeUnicodeDependencies()
    {
        int count = 0;
        using var reader = Read("GraphemeBreakTest.txt.gz");
        string? line;
        while ((line = reader.ReadLine()) != null)
        {
            string body = line.Split('#')[0].Trim();
            if (body.Length == 0)
                continue;
            var expected = new List<int>();
            var text = new StringBuilder();
            foreach (string token in Words(body))
            {
                if (token == "÷")
                    expected.Add(text.Length);
                else if (token != "×")
                    text.Append(char.ConvertFromUtf32(int.Parse(token, NumberStyles.HexNumber, CultureInfo.InvariantCulture)));
            }

            var result = PdfUnicode.Analyze(text.ToString());
            int[] actual = new[]
            {
                0
            }.Concat(result.Graphemes.Select(x => x.Utf16Offset + x.Utf16Length)).ToArray();
            Assert.True(expected.SequenceEqual(actual), "Grapheme vector " + count + ": " + body);
            count++;
        }

        Assert.Equal(853, count);
        _output.WriteLine("Unicode 18.0.0 extended grapheme vectors: " + count);
    }

    [Fact]
    public void OfficialBidiCharacterVectorsMatchLevelsParagraphDirectionAndVisualOrder()
    {
        WithContext(context =>
        {
            var data = PdfUnicodeData.For(context);
            int count = 0;
            using var reader = Read("BidiCharacterTest.txt.gz");
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Length == 0 || line[0] == '#')
                    continue;
                string[] fields = line.Split(';');
                if (fields.Length < 5)
                    continue;
                int[] points = Words(fields[0]).Select(x => int.Parse(x, NumberStyles.HexNumber, CultureInfo.InvariantCulture)).ToArray();
                var types = points.Select(x => data.Get(x).Bidi).ToArray();
                int direction = int.Parse(fields[1], CultureInfo.InvariantCulture);
                var result = PdfBidiAlgorithm.ResolveParagraph(points, types, direction == 0 ? PdfParagraphDirection.LeftToRight : direction == 1 ? PdfParagraphDirection.RightToLeft : PdfParagraphDirection.Auto, data, context);
                Compare(result, Levels(fields[3]), Ints(fields[4]), "character vector " + count + ": " + fields[0]);
                Assert.Equal(int.Parse(fields[2], CultureInfo.InvariantCulture), result.Paragraphs[0].EmbeddingLevel);
                count++;
            }

            Assert.Equal(91707, count);
            _output.WriteLine("Unicode 18.0.0 bidi character vectors: " + count);
        });
    }

    [Fact]
    public void OfficialBidiClassVectorsMatchEveryRequestedParagraphDirection()
    {
        WithContext(context =>
        {
            var data = PdfUnicodeData.For(context);
            int count = 0, executions = 0;
            int[]? expectedLevels = null, expectedOrder = null;
            using var reader = Read("BidiTest.txt.gz");
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("@Levels:", StringComparison.Ordinal))
                {
                    expectedLevels = Levels(line.Substring(8));
                    continue;
                }

                if (line.StartsWith("@Reorder:", StringComparison.Ordinal))
                {
                    expectedOrder = Ints(line.Substring(9));
                    continue;
                }

                if (line.Length == 0 || line[0] == '#' || line[0] == '@')
                    continue;
                var fields = line.Split(';');
                if (fields.Length < 2)
                    continue;
                var types = Words(fields[0]).Select(x => (PdfBidiClass)Enum.Parse(typeof(PdfBidiClass), x)).ToArray();
                int flags = int.Parse(fields[1].Trim(), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                foreach (var selected in new[]
                {
                    (Mask: 1, Direction: PdfParagraphDirection.Auto),
                    (Mask: 2, Direction: PdfParagraphDirection.LeftToRight),
                    (Mask: 4, Direction: PdfParagraphDirection.RightToLeft)
                }

                )
                {
                    if ((flags & selected.Mask) == 0)
                        continue;
                    var result = PdfBidiAlgorithm.ResolveParagraph(new int[types.Length], types, selected.Direction, data, context);
                    Compare(result, expectedLevels!, expectedOrder!, "class vector " + count + " " + selected.Direction + ": " + fields[0]);
                    executions++;
                }

                count++;
            }

            Assert.Equal(490846, count);
            Assert.True(executions >= count);
            _output.WriteLine("Unicode 18.0.0 bidi class vectors: " + count + "; direction executions: " + executions);
        });
    }

    private static void Compare(PdfBidiAlgorithm.Result actual, int[] levels, int[] order, string label)
    {
        Assert.True(levels.SequenceEqual(actual.Levels), label + " levels expected " + string.Join(",", levels) + " actual " + string.Join(",", actual.Levels));
        Assert.True(order.SequenceEqual(actual.Visual), label + " order expected " + string.Join(",", order) + " actual " + string.Join(",", actual.Visual));
    }

    private static string[] Words(string text) => text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
    private static int[] Levels(string text) => Words(text).Select(x => x == "x" ? -1 : int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
    private static int[] Ints(string text) => Words(text).Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray();
    private static StreamReader Read(string file) => new(new GZipStream(File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "unicode", file)), CompressionMode.Decompress), Encoding.UTF8);
    private static void WithContext(Action<PdfOperationContext> action)
    {
        var context = new PdfOperationContext("OfficialUnicodeVectors", "test", PdfCapability.Fonts, new(limits: new(maximumExpandedBytes: 1000000000000, maximumObjects: 1000000000, maximumElapsedTime: TimeSpan.FromMinutes(10))), default);
        bool success = false;
        try
        {
            action(context);
            context.EnsureComplete();
            success = true;
        }
        finally
        {
            context.Finish(success);
            context.DisposeSources();
        }
    }
}
