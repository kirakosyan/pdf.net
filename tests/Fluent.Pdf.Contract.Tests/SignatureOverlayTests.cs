using System.IO;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class SignatureOverlayTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplacingOrRemovingASignatureCannotBypassOriginalByteRangeProtection(bool remove)
    {
        using var input = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "writer", "signed-original.pdf"));
        var source = PdfSyntax.Read(input);
        using var edit = new PdfTransaction(source);
        if (remove)
            edit.Remove(new(8));
        else
            edit.Set(new(8), PdfValue.Null);
        using var output = new MemoryStream();
        Assert.Equal("Writer.SignatureRewrite", Assert.Throws<PdfException>(() => edit.Save(output)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }
}
