using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class SecurityWorkflowTests
{
    private const string User = "original-user-credential";
    private const string Owner = "original-owner-credential";
    private static readonly byte[] Payload = Encoding.ASCII.GetBytes("original private binary payload");
    internal static PdfDocument Source()
    {
        using var forms = FormWorkflowTests.Source();
        using var metadata = PdfMetadata.Edit(forms.CreateSnapshot(), editor => editor.SetInformation("Title", "Original Unicode – π").SetXmp(MetadataTests.Packet));
        using var payload = new MemoryStream(Payload);
        return PdfAttachments.Add(metadata.CreateSnapshot(), payload, new("binary.bin"));
    }

    [Theory]
    [InlineData(PdfPasswordCipher.Aes256, true)]
    [InlineData(PdfPasswordCipher.Aes256, false)]
    [InlineData(PdfPasswordCipher.Aes128, true)]
    [InlineData(PdfPasswordCipher.Aes128, false)]
    public void PasswordSecurityPreservesFormsUnicodeAttachmentsAndExplicitMetadataChoice(PdfPasswordCipher cipher, bool encryptMetadata)
    {
        using var source = Source();
        using var user = new PdfPassword(Encoding.ASCII.GetBytes(User));
        using var owner = new PdfPassword(Encoding.ASCII.GetBytes(Owner));
        using var encrypted = new MemoryStream();
        var protection = PdfSecurity.EncryptWithPassword(source.CreateSnapshot(), encrypted, new(user, owner, PdfDocumentPermissions.Print | PdfDocumentPermissions.FillForms, cipher, encryptMetadata));
        Assert.Equal(PdfAuthenticationRole.CreatedProtection, protection.Role);
        Assert.Equal(cipher == PdfPasswordCipher.Aes256 ? 6 : 4, protection.Revision);
        Assert.True(encrypted.CanWrite);
        var ascii = Encoding.ASCII.GetString(encrypted.ToArray());
        Assert.DoesNotContain(User, ascii);
        Assert.DoesNotContain(Owner, ascii);
        Assert.DoesNotContain(Encoding.ASCII.GetString(Payload), ascii);
        Assert.Equal(!encryptMetadata, ascii.Contains("private & structured"));
        encrypted.Position = 0;
        Assert.Equal("Reader.EncryptionProviderRequired", Assert.Throws<PdfException>(() => Pdf.Open(encrypted)).Diagnostic.Code);
        foreach (var credential in new[]
        {
            owner,
            user
        }

        )
        {
            encrypted.Position = 0;
            using var plain = new MemoryStream();
            var result = PdfSecurity.DecryptWithPassword(encrypted, plain, credential);
            Assert.Equal(ReferenceEquals(credential, owner) ? PdfAuthenticationRole.Owner : PdfAuthenticationRole.User, result.Role);
            Assert.True((result.Permissions & PdfDocumentPermissions.Print) != 0);
            Assert.True((result.Permissions & PdfDocumentPermissions.Extract) == 0);
            Assert.True(encrypted.CanRead);
            Assert.True(plain.CanWrite);
            plain.Position = 0;
            using var opened = Pdf.Open(plain);
            Assert.Equal(2, opened.Pages.Count);
            Assert.Equal(4, PdfForms.Inspect(opened.CreateSnapshot()).Fields.Count);
            Assert.Equal("Original Unicode – π", PdfMetadata.Inspect(opened.CreateSnapshot()).Text["Title"]);
            var attachment = Assert.Single(PdfAttachments.Inspect(opened.CreateSnapshot()).Items);
            using var output = new MemoryStream();
            PdfAttachments.Extract(attachment, output);
            Assert.Equal(Payload, output.ToArray());
        }
    }

    [Fact]
    public void WrongCredentialsDisposedCredentialsAndCancellationNeverPublishPlaintext()
    {
        using var source = Source();
        using var user = new PdfPassword(Encoding.ASCII.GetBytes(User));
        using var owner = new PdfPassword(Encoding.ASCII.GetBytes(Owner));
        using var encrypted = new MemoryStream();
        PdfSecurity.EncryptWithPassword(source.CreateSnapshot(), encrypted, new(user, owner));
        using var output = new MemoryStream();
        output.WriteByte(42);
        using var wrong = new PdfPassword(Encoding.ASCII.GetBytes("wrong"));
        encrypted.Position = 0;
        Assert.Equal("Security.AuthenticationFailed", Assert.Throws<PdfException>(() => PdfSecurity.DecryptWithPassword(encrypted, output, wrong)).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        wrong.Dispose();
        encrypted.Position = 0;
        Assert.Throws<ObjectDisposedException>(() => PdfSecurity.DecryptWithPassword(encrypted, output, wrong));
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        encrypted.Position = 0;
        Assert.ThrowsAny<OperationCanceledException>(() => PdfSecurity.DecryptWithPassword(encrypted, output, owner, cancellationToken: canceled.Token));
        Assert.Equal(new byte[] { 42 }, output.ToArray());
    }

    [Fact]
    public void PlainInputAndSameStreamTransformsFailWithTypedOrArgumentErrors()
    {
        using var source = Source();
        using var input = new MemoryStream();
        source.Save(input);
        input.Position = 0;
        using var output = new MemoryStream();
        using var password = new PdfPassword(Encoding.ASCII.GetBytes(Owner));
        Assert.Equal("Security.NotEncrypted", Assert.Throws<PdfException>(() => PdfSecurity.DecryptWithPassword(input, output, password)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        Assert.Throws<ArgumentException>(() => PdfSecurity.DecryptWithPassword(input, input, password));
    }

    [Fact]
    public void BothSecurityDirectionsRequireExplicitSignatureInvalidation()
    {
        using var original = Source();
        using var bytes = new MemoryStream();
        original.Save(bytes);
        bytes.Position = 0;
        using var edit = new PdfTransaction(PdfSyntax.Read(bytes));
        edit.Add(NavigationTests.D(("Type", PdfValue.Name("Sig")), ("ByteRange", PdfValue.Array(PdfValue.Integer(0), PdfValue.Integer(1), PdfValue.Integer(2), PdfValue.Integer(3))), ("Contents", PdfValue.String(1, 2, 3))));
        using var signedBytes = new MemoryStream();
        edit.Save(signedBytes, new(allowSignatureInvalidation: true));
        signedBytes.Position = 0;
        using var signed = Pdf.Open(signedBytes);
        using var output = new MemoryStream();
        using var user = new PdfPassword(Encoding.ASCII.GetBytes(User));
        using var owner = new PdfPassword(Encoding.ASCII.GetBytes(Owner));
        Assert.Equal("Writer.SignatureRewrite", Assert.Throws<PdfException>(() => PdfSecurity.EncryptWithPassword(signed.CreateSnapshot(), output, new(user, owner))).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        var encrypted = PdfSecurity.EncryptWithPassword(signed.CreateSnapshot(), output, new(user, owner), allowSignatureInvalidation: true);
        Assert.Contains(encrypted.Report.Diagnostics, d => d.Code == "Writer.SignatureRewriteAllowed");
        output.Position = 0;
        using var plaintext = new MemoryStream();
        Assert.Equal("Writer.SignatureRewrite", Assert.Throws<PdfException>(() => PdfSecurity.DecryptWithPassword(output, plaintext, owner)).Diagnostic.Code);
        Assert.Equal(0, plaintext.Length);
        output.Position = 0;
        Assert.Contains(PdfSecurity.DecryptWithPassword(output, plaintext, owner, allowSignatureInvalidation: true).Report.Diagnostics, d => d.Code == "Writer.SignatureRewriteAllowed");
    }
}
