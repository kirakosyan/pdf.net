using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class FontRobustnessTests
{
    [Fact]
    public void DeterministicFontMutationsRejectSafelyOrYieldBoundedImmutableFaces()
    {
        var random = new Random(8041803);
        var sources = new[]
        {
            "original.ttf",
            "original.otf",
            "original.pfb",
            "original.pfa",
            "original.cff",
            "original.ttc",
            "licensed-cjk.otf"
        }.Select(name => File.ReadAllBytes(FontTests.Fixture(name))).ToArray();
        for (int iteration = 0; iteration < 1400; iteration++)
        {
            byte[] source = sources[iteration % sources.Length];
            int length = iteration % 4 == 0 ? random.Next(source.Length) : source.Length;
            var bytes = new byte[length];
            Array.Copy(source, bytes, length);
            for (int n = 0; n < 1 + iteration % 4 && bytes.Length > 0; n++)
                bytes[random.Next(bytes.Length)] = (byte)random.Next(256);
            using var input = new MemoryStream(bytes);
            try
            {
                var font = PdfFont.Read(input, FontTests.Providers, policy: new(limits: new(maximumInputBytes: 1048576, maximumExpandedBytes: 16777216, maximumObjects: 100000, maximumDepth: 32)));
                Assert.InRange(font.Info.GlyphCount, 1, 65536);
                Assert.True(font.OperationReport.Succeeded);
            }
            catch (PdfException)
            {
            }

            Assert.True(input.CanRead);
        }
    }

    [Fact]
    public void IndependentJobsCanShareAnImmutableFaceAndProviderWithoutLeakingFontMappings()
    {
        var font = FontTests.Read("original.ttf");
        Parallel.For(0, 16, index =>
        {
            for (int iteration = 0; iteration < 4; iteration++)
            {
                string text = index % 2 == 0 ? "A一" : "B😀";
                var resource = PdfFontResource.CreateText(font, text);
                using var document = Pdf.Create().AddPage();
                document.Pages[0].SetFont("F1", resource);
                document.Pages[0].Content = FontTests.Show(resource);
                using var output = new MemoryStream();
                document.Save(output);
                output.Position = 0;
                using var reopened = Pdf.Open(output);
                var binding = Assert.Single(reopened.Pages[0].ReadFonts());
                Assert.Equal(text, binding.DecodeText(resource.EncodedText).Text);
            }
        });
    }

    [Fact]
    public void FontOutputFailuresDoNotDisposeBorrowedStreamsOrPoisonTheNextSubset()
    {
        var font = FontTests.Read("original.ttf");
        int glyph = font.GetGlyph('A').GlyphIndex;
        using var expected = new MemoryStream();
        font.WriteProgram(expected, new[] { glyph });
        foreach (int limit in new[]
        {
            0,
            1,
            37,
            (int)expected.Length / 2,
            (int)expected.Length - 1
        }

        )
        {
            using var output = new FaultOutput(limit);
            Assert.Throws<IOException>(() => font.WriteProgram(output, new[] { glyph }));
            Assert.True(output.CanWrite);
        }

        using var next = new MemoryStream();
        font.WriteProgram(next, new[] { glyph });
        Assert.Equal(expected.ToArray(), next.ToArray());
    }

    [Fact]
    public void ExactVerticalCjkMetricsProduceExplicitVerticalCidEncoding()
    {
        var font = FontTests.Read("licensed-cjk.otf");
        var glyph = font.GetGlyph(0x4e2d);
        Assert.True(glyph.VerticalAdvance.HasValue);
        Assert.True(glyph.VerticalAdvance.Value > 0);
        var resource = PdfFontResource.CreateText(font, "中文", vertical: true);
        using var document = Pdf.Create().AddPage();
        document.Pages[0].SetFont("F1", resource);
        document.Pages[0].Content = FontTests.Show(resource);
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        var binding = Assert.Single(reopened.Pages[0].ReadFonts());
        Assert.Equal("Identity-V", binding.SourceDictionary!.Entries["Encoding"].NameValue);
        Assert.Equal("中文", binding.DecodeText(resource.EncodedText).Text);
    }

    private sealed class FaultOutput : Stream
    {
        private readonly int _limit;
        private int _written;
        internal FaultOutput(int limit) => _limit = limit;
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
                    throw new IOException("Original injected font output failure.");
                _written++;
            }
        }
    }
}
