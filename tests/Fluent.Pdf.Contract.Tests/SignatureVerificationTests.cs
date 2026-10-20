using System.IO;
using Fluent.Pdf.Cryptography;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class SignatureVerificationTests
{
    internal static readonly DateTimeOffset Instant = new(2026, 10, 20, 0, 0, 0, TimeSpan.Zero);
    internal static PdfSyntaxDocument Signed()
    {
        using var certificate = new CertificateSecurityTests.Certificate("Verification fixture");
        using var signer = new ManagedCmsSignatureProvider(certificate.Credential);
        var result = PdfSignatures.Sign(SignatureTests.Prepared(), new("First"), new(signer));
        using var input = result.OpenPdfStream();
        return PdfSyntax.Read(input);
    }
    [Theory]
    [InlineData(PdfRevocationStatus.Unknown, false)]
    [InlineData(PdfRevocationStatus.Good, true)]
    [InlineData(PdfRevocationStatus.Revoked, false)]
    public void IntegrityIdentityValidityAndRevocationRemainSeparate(PdfRevocationStatus status, bool accepted)
    {
        using var certificate = new CertificateSecurityTests.Certificate("Explicit pin");
        using var signer = new ManagedCmsSignatureProvider(certificate.Credential);
        var signed = PdfSignatures.Sign(SignatureTests.Prepared(), new("First"), new(signer));
        using var input = signed.OpenPdfStream();
        var source = PdfSyntax.Read(input);
        var authority = new Status(status);
        var trust = new PinnedSignatureTrustProvider(new[] { certificate.Value.RawData }, authority);
        var result = Assert.Single(PdfSignatures.Verify(source, new(signer, trust), new(Instant)));
        Assert.True(result.Signature.CryptographicallyValid);
        Assert.True(result.Trust!.IdentityTrusted);
        Assert.Equal(status, result.Trust.Revocation);
        Assert.Equal(accepted, result.Accepted);
        Assert.Equal(1, authority.Calls);
        Assert.Equal(0, result.LaterByteCount);
        Assert.Throws<ObjectDisposedException>(() => authority.Context!.Checkpoint());
        var expired = Assert.Single(PdfSignatures.Verify(source, new(signer, trust), new(new DateTimeOffset(2040, 1, 1, 0, 0, 0, TimeSpan.Zero))));
        Assert.False(expired.Trust!.TimeValid);
        Assert.False(expired.Accepted);
        var untrusted = Assert.Single(PdfSignatures.Verify(source, new(signer, new PinnedSignatureTrustProvider(Array.Empty<byte[]>())), new(Instant)));
        Assert.True(untrusted.Signature.CryptographicallyValid);
        Assert.False(untrusted.Accepted);
        Assert.False(untrusted.Trust!.IdentityTrusted);
        Assert.Equal(PdfRevocationStatus.Unknown, untrusted.Trust.Revocation);
    }
    [Fact]
    public void TrustNeverRunsForTamperedBytesAndBudgetsAreShared()
    {
        using var certificate = new CertificateSecurityTests.Certificate("Tampered pin");
        using var signer = new ManagedCmsSignatureProvider(certificate.Credential);
        var result = PdfSignatures.Sign(SignatureTests.Prepared(), new("First", reason: "TamperTarget"), new(signer));
        var bytes = SignatureTests.Bytes(result.OpenPdfStream());
        int at = System.Text.Encoding.ASCII.GetString(bytes).IndexOf("00540061006D007000650072005400610072006700650074", StringComparison.Ordinal);
        // The writer uses a hex PDF string. Change one signed reason nibble, retaining valid PDF syntax.
        Assert.True(at >= 0);
        bytes[at] = (byte)'6';
        using var input = new MemoryStream(bytes);
        var source = PdfSyntax.Read(input);
        var status = new Status(PdfRevocationStatus.Good);
        var providers = new PdfProviderRegistry(signer, new PinnedSignatureTrustProvider(new[] { certificate.Value.RawData }, status));
        var verification = Assert.Single(PdfSignatures.Verify(source, providers, new(Instant)));
        Assert.False(verification.Signature.CryptographicallyValid);
        Assert.Null(verification.Trust);
        Assert.Equal(0, status.Calls);
        Assert.Throws<PdfException>(() => PdfSignatures.Verify(source, providers, new(Instant), new(limits: new(maximumExpandedBytes: 100))));
    }
    internal sealed class Status : IPdfCertificateStatusProvider
    {
        private readonly PdfRevocationStatus _status;
        internal int Calls;
        internal PdfOperationContext? Context;
        internal Status(PdfRevocationStatus status) => _status = status;
        public PdfRevocationStatus Check(Stream certificate, DateTimeOffset instant, PdfOperationContext context)
        {
            Context = context;
            Calls++;
            Assert.True(certificate.ReadByte() >= 0);
            return _status;
        }
    }
}
