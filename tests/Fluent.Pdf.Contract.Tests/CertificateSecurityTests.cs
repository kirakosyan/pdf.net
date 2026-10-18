using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Fluent.Pdf.Cryptography;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class CertificateSecurityTests
{
    internal sealed class Certificate : IDisposable
    {
        internal RSA Key { get; }
        internal X509Certificate2 Value { get; }

        internal Certificate(string name)
        {
#if NETFRAMEWORK
            Key = new RSACng(2048);
#else
            Key = RSA.Create();
            Key.KeySize = 2048;
#endif
            var request = new CertificateRequest("CN=" + name, Key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            request.CertificateExtensions.Add(new X509SubjectKeyIdentifierExtension(request.PublicKey, false));
            Value = request.CreateSelfSigned(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2035, 1, 1, 0, 0, 0, TimeSpan.Zero));
        }

        internal PdfCertificateCredential Credential => new(Value, Key);

        public void Dispose()
        {
            Value.Dispose();
            Key.Dispose();
        }
    }

    [Theory]
    [InlineData(PdfCmsKeyTransport.OaepSha256)]
    [InlineData(PdfCmsKeyTransport.LegacyPkcs1)]
    public void CmsEnvelopeUsesOnlyExplicitRecipientsAndPrivateHandle(PdfCmsKeyTransport transport)
    {
        using var first = new Certificate("Original First");
        using var second = new Certificate("Original Second");
        using var absent = new Certificate("Original Absent");
        var provider = new ManagedCmsEnvelopeProvider();
        var seed = Enumerable.Range(0, 24).Select(i => (byte)i).ToArray();
        using var encoded = new MemoryStream();
        PolicyTests.Run(context =>
        {
            using var input = new MemoryStream(seed);
            provider.Encrypt(new[] { new PdfCertificateRecipient(first.Value, keyTransport: transport), new PdfCertificateRecipient(second.Value, keyTransport: transport) }, input, encoded, context);
        });
        foreach (var certificate in new[]
        {
            first,
            second
        }

        )
        {
            encoded.Position = 0;
            using var decoded = new MemoryStream();
            PolicyTests.Run(context => Assert.True(provider.TryDecrypt(certificate.Credential, transport == PdfCmsKeyTransport.LegacyPkcs1, encoded, decoded, context)));
            Assert.Equal(seed, decoded.ToArray());
        }

        encoded.Position = 0;
        using var unmatched = new MemoryStream();
        PolicyTests.Run(context => Assert.False(provider.TryDecrypt(absent.Credential, false, encoded, unmatched, context)));
        Assert.Equal(0, unmatched.Length);
        if (transport == PdfCmsKeyTransport.LegacyPkcs1)
        {
            encoded.Position = 0;
            using var blocked = new MemoryStream();
            Assert.Equal("Security.LegacyReadBlocked", Assert.Throws<PdfException>(() => PolicyTests.Run(context => provider.TryDecrypt(first.Credential, false, encoded, blocked, context))).Diagnostic.Code);
            Assert.Equal(0, blocked.Length);
        }
    }

    [Theory]
    [InlineData(PdfPublicKeyCipher.Aes256, true)]
    [InlineData(PdfPublicKeyCipher.Aes256, false)]
    [InlineData(PdfPublicKeyCipher.Aes128, true)]
    [InlineData(PdfPublicKeyCipher.Aes128, false)]
    public void CertificateFileSecurityGroupsPermissionsAndPreservesDocumentWorkflows(PdfPublicKeyCipher cipher, bool metadata)
    {
        using var owner = new Certificate("Original Owner");
        using var first = new Certificate("Original User One");
        using var second = new Certificate("Original User Two");
        using var source = SecurityWorkflowTests.Source();
        using var encrypted = new MemoryStream();
        var providers = new PdfProviderRegistry(new ManagedCmsEnvelopeProvider());
        var permissions = PdfDocumentPermissions.Print | PdfDocumentPermissions.FillForms;
        var protection = new PdfCertificateProtection(new[] { new PdfCertificateRecipient(owner.Value, ownerAuthority: true), new PdfCertificateRecipient(first.Value, permissions), new PdfCertificateRecipient(second.Value, permissions) }, cipher, metadata);
        var created = PdfSecurity.EncryptWithCertificates(source.CreateSnapshot(), encrypted, protection, providers);
        Assert.Equal(PdfAuthenticationRole.CreatedProtection, created.Role);
        encrypted.Position = 0;
        var syntax = PdfSyntax.Read(encrypted);
        var dictionary = syntax.Resolve(syntax.Revisions[0].Trailer.Entries["Encrypt"].ReferenceId);
        var filters = dictionary.Entries["CF"].Entries["DefaultCryptFilter"];
        Assert.Equal(2, filters.Entries["Recipients"].Items.Count);
        Assert.Equal(cipher == PdfPublicKeyCipher.Aes256 ? 256 : 128, filters.Entries["Length"].IntegerValue);
        foreach (var certificate in new[]
        {
            owner,
            first,
            second
        }

        )
        {
            encrypted.Position = 0;
            using var output = new MemoryStream();
            var result = PdfSecurity.DecryptWithCertificate(encrypted, output, certificate.Credential, providers);
            Assert.Equal(ReferenceEquals(certificate, owner) ? PdfAuthenticationRole.Owner : PdfAuthenticationRole.Certificate, result.Role);
            Assert.DoesNotContain(result.Report.Diagnostics, diagnostic => diagnostic.Severity == PdfDiagnosticSeverity.Error);
            Assert.Equal(metadata, result.EncryptMetadata);
            Assert.Equal(ReferenceEquals(certificate, owner), (result.Permissions & PdfDocumentPermissions.Modify) != 0);
            output.Position = 0;
            using var reopened = Pdf.Open(output);
            Assert.Equal(4, PdfForms.Inspect(reopened.CreateSnapshot()).Fields.Count);
            Assert.Equal("Original Unicode – π", PdfMetadata.Inspect(reopened.CreateSnapshot()).Text["Title"]);
            Assert.Single(PdfAttachments.Inspect(reopened.CreateSnapshot()).Items);
        }
    }

    [Fact]
    public void MissingRecipientMismatchedKeyAndDuplicateRecipientsPublishNothing()
    {
        using var owner = new Certificate("Original Owner");
        using var absent = new Certificate("Original Absent");
        using var source = SecurityWorkflowTests.Source();
        var providers = new PdfProviderRegistry(new ManagedCmsEnvelopeProvider());
        using var encrypted = new MemoryStream();
        PdfSecurity.EncryptWithCertificates(source.CreateSnapshot(), encrypted, new(new[] { new PdfCertificateRecipient(owner.Value) }), providers);
        encrypted.Position = 0;
        using var output = new MemoryStream();
        Assert.Equal("Security.AuthenticationFailed", Assert.Throws<PdfException>(() => PdfSecurity.DecryptWithCertificate(encrypted, output, absent.Credential, providers)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        encrypted.Position = 0;
        Assert.Equal("Security.CertificateKeyMismatch", Assert.Throws<PdfException>(() => PdfSecurity.DecryptWithCertificate(encrypted, output, new(owner.Value, absent.Key), providers)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        Assert.Equal("Security.DuplicateRecipient", Assert.Throws<PdfException>(() => PdfSecurity.EncryptWithCertificates(source.CreateSnapshot(), output, new(new[] { new PdfCertificateRecipient(owner.Value), new PdfCertificateRecipient(owner.Value) }), providers)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        Assert.Throws<PdfProviderUnavailableException>(() => PdfSecurity.EncryptWithCertificates(source.CreateSnapshot(), output, new(new[] { new PdfCertificateRecipient(owner.Value) }), PdfProviderRegistry.Empty));
    }

    [Fact]
    public void CmsInputBoundsAndMalformedDerFailWithoutSeedOutput()
    {
        using var certificate = new Certificate("Original Bound");
        var provider = new ManagedCmsEnvelopeProvider();
        using var output = new MemoryStream();
        using var oversized = new MemoryStream(new byte[65537]);
        Assert.Equal("Security.CmsExtent", Assert.Throws<PdfException>(() => PolicyTests.Run(context => provider.TryDecrypt(certificate.Credential, false, oversized, output, context))).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        using var malformed = new MemoryStream(new byte[] { 0x30, 0x80, 0, 0 });
        Assert.Equal("Security.CmsMalformed", Assert.Throws<PdfException>(() => PolicyTests.Run(context => provider.TryDecrypt(certificate.Credential, false, malformed, output, context))).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void EffectivePermissionGroupsNormalizeOwnerAuthorityAndPdfTwoAccessibility()
    {
        using var owner = new Certificate("Original Restricted Owner");
        using var first = new Certificate("Original No Accessibility Bit");
        using var second = new Certificate("Original Accessibility Bit");
        using var source = SecurityWorkflowTests.Source();
        using var encrypted = new MemoryStream();
        var providers = new PdfProviderRegistry(new ManagedCmsEnvelopeProvider());
        PdfSecurity.EncryptWithCertificates(source.CreateSnapshot(), encrypted, new(new[] { new PdfCertificateRecipient(owner.Value, PdfDocumentPermissions.None, ownerAuthority: true), new PdfCertificateRecipient(first.Value, PdfDocumentPermissions.Print), new PdfCertificateRecipient(second.Value, PdfDocumentPermissions.Print | PdfDocumentPermissions.ExtractForAccessibility) }), providers);
        encrypted.Position = 0;
        var syntax = PdfSyntax.Read(encrypted);
        var dictionary = syntax.Resolve(syntax.Revisions[0].Trailer.Entries["Encrypt"].ReferenceId);
        Assert.Equal(2, dictionary.Entries["CF"].Entries["DefaultCryptFilter"].Entries["Recipients"].Items.Count);
        foreach (var certificate in new[]
        {
            owner,
            first,
            second
        }

        )
        {
            encrypted.Position = 0;
            using var output = new MemoryStream();
            var result = PdfSecurity.DecryptWithCertificate(encrypted, output, certificate.Credential, providers);
            Assert.Equal(ReferenceEquals(certificate, owner) ? PdfDocumentPermissions.All : PdfDocumentPermissions.Print | PdfDocumentPermissions.ExtractForAccessibility, result.Permissions);
        }
    }
}
