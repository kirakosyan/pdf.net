using System.IO;
using System.Security.Cryptography;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class SignedRevisionTests
{
    [Theory]
    [InlineData(PdfCrossReferenceFormat.Table)]
    [InlineData(PdfCrossReferenceFormat.Stream)]
    public void RealDetachedCmsSignatureRetainsEveryCoveredByteAfterIncrementalEditing(PdfCrossReferenceFormat format)
    {
        byte[] original = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "writer", "signed-original.pdf"));
        Assert.Equal("a5b7f85ce5fd2366b02d5a4c7222a0bde3f36c75892856b87c880f75d65908d1", Hash(original));
        using var input = new MemoryStream(original);
        using var document = Pdf.Open(input);
        var signature = document.SourceDocument!.Resolve(new(8));
        long[] ranges = signature.Entries["ByteRange"].Items.Select(x => x.IntegerValue).ToArray();
        Assert.Equal("e12813d9a3db81ec1a0075f100c9b627a65045b8adfb27204a3cb2e5558bf520", Hash(Covered(original, ranges)));
        document.Pages[0].Rotation = PdfPageRotation.Clockwise90;
        using var output = new MemoryStream();
        document.Save(output, writeOptions: new(PdfWriteMode.Incremental, format));
        byte[] updated = output.ToArray();
        Assert.Equal(original, updated.Take(original.Length));
        Assert.Equal(Covered(original, ranges), Covered(updated, ranges));
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        Assert.Equal(PdfPageRotation.Clockwise90, reopened.Pages[0].Rotation);
        Assert.Equal(signature.Entries["Contents"].StringBytes, reopened.SourceDocument!.Resolve(new(8)).Entries["Contents"].StringBytes);
        Assert.Equal(2, reopened.SourceDocument.Revisions.Count);
    }

    [Fact]
    public void SignedFullRewriteIsRejectedBeforeTouchingCallerOutput()
    {
        using var document = Pdf.OpenFile(Path.Combine(AppContext.BaseDirectory, "Fixtures", "writer", "signed-original.pdf"));
        using var output = new MemoryStream();
        Assert.Equal("Writer.SignatureRewrite", Assert.Throws<PdfException>(() => document.Save(output)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }

    private static byte[] Covered(byte[] input, long[] ranges)
    {
        using var output = new MemoryStream();
        for (int i = 0; i < ranges.Length; i += 2)
            output.Write(input, (int)ranges[i], (int)ranges[i + 1]);
        return output.ToArray();
    }

    private static string Hash(byte[] bytes)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
}
