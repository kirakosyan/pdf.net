using System.IO;
using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class BootstrapFilterTests
{
    // Original input encoded independently with Python zlib 1.3.2, default level.
    // These constants cover fixed and dynamic canonical trees and overlapping copies.
    [Theory]
    [InlineData("789c4b4c4a4e444500417c06e5", false)]
    [InlineData("789cedc2310d00300800306dc054ccbf103e1cf090368d0000000000b6653d55555555555555553dff8f06e5832d57", true)]
    public void IndependentlyEncodedFlateStreamsDecodeExactly(string hex, bool dynamic)
    {
        byte[] expected = dynamic ? Enumerable.Repeat((byte)'A', 5000).Concat(Encoding.ASCII.GetBytes(string.Concat(Enumerable.Repeat("BCD", 3000)))).Concat(Enumerable.Repeat((byte)'Z', 10)).ToArray() : Encoding.ASCII.GetBytes("abcabcabcabcabcabc");
        PolicyTests.Run(context =>
        {
            using var source = new MemoryStream(Hex(hex));
            var output = PdfInflate.Decode(source, context);
            using var view = output.Open(0, output.Length);
            Assert.Equal(expected, Copy(view));
        });
    }

    [Fact]
    public void EveryTruncatedPrefixAndChecksumOrFinalBlockDamageIsRejected()
    {
        byte[] valid = Hex("789c4b4c4a4e444500417c06e5");
        for (int length = 0; length < valid.Length; length++)
        {
            byte[] prefix = valid.Take(length).ToArray();
            Assert.Throws<PdfException>(() => PolicyTests.Run(context =>
            {
                using var source = new MemoryStream(prefix);
                PdfInflate.Decode(source, context);
            }));
        }

        foreach (int index in new[]
        {
            2,
            valid.Length - 1
        }

        )
        {
            byte[] damaged = (byte[])valid.Clone();
            damaged[index] ^= 1;
            Assert.Throws<PdfException>(() => PolicyTests.Run(context =>
            {
                using var source = new MemoryStream(damaged);
                PdfInflate.Decode(source, context);
            }));
        }
    }

    [Fact]
    public void DecoderAllocationBudgetAndCancellationAreMandatory()
    {
        byte[] encoded = SyntaxFixtures.ZlibStored(new byte[4097]);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PolicyTests.Run(context =>
        {
            using var source = new MemoryStream(encoded);
            PdfInflate.Decode(source, context);
        }, new PdfResourceLimits(maximumExpandedBytes: 32768 + 4096))).Diagnostic.Code);
    }

    private static byte[] Hex(string text) => Enumerable.Range(0, text.Length / 2).Select(i => Convert.ToByte(text.Substring(i * 2, 2), 16)).ToArray();
    private static byte[] Copy(Stream source)
    {
        using var target = new MemoryStream();
        source.CopyTo(target);
        return target.ToArray();
    }
}
