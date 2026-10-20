using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Cms;
using Org.BouncyCastle.Crypto.Operators;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Tsp;
using Org.BouncyCastle.X509;
using Fluent.Pdf.Cryptography;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class TimestampTests
{
    [Fact]
    public void Rfc3161VerifiesTheExactPinnedSignerImprintNonceAndAuthenticatedGenerationTime()
    {
        using var rsa = RSA.Create();
        rsa.KeySize = 2048;
        var request = new CertificateRequest("CN=Original offline TSA", rsa, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.8") }, true));
        using var certificate = request.CreateSelfSigned(new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2035, 1, 1, 0, 0, 0, TimeSpan.Zero));
        var bc = new X509CertificateParser().ReadCertificate(certificate.RawData);
        var key = DotNetUtilities.GetRsaKeyPair(rsa).Private;
        var signer = new SignerInfoGeneratorBuilder().Build(new Asn1SignatureFactory("SHA256WITHRSA", key), bc);
        var generator = new TimeStampTokenGenerator(signer, new Asn1DigestFactory(new Org.BouncyCastle.Crypto.Digests.Sha256Digest(), new DerObjectIdentifier(TspAlgorithms.Sha256)), new DerObjectIdentifier("1.3.6.1.4.1.55555.1"), true);
        var imprint = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var nonce = new byte[] { 1, 2, 3 };
        var timestampRequest = new TimeStampRequestGenerator().Generate(TspAlgorithms.Sha256, imprint, new BigInteger(1, nonce));
        var instant = new DateTime(2026, 10, 20, 12, 0, 0, DateTimeKind.Utc);
        var token = generator.Generate(timestampRequest, BigInteger.One, instant).GetEncoded("DER");
        using var input = new MemoryStream(token);
        Assert.Equal(new DateTimeOffset(instant), Rfc3161TimestampVerifier.Verify(input, certificate.RawData, imprint, nonce));
        Assert.True(input.CanRead);
        using var wrongImprint = new MemoryStream(token);
        Assert.Equal("Signature.TimestampBinding", Assert.Throws<PdfException>(() => Rfc3161TimestampVerifier.Verify(wrongImprint, certificate.RawData, new byte[32], nonce)).Diagnostic.Code);
        using var wrongNonce = new MemoryStream(token);
        Assert.Equal("Signature.TimestampBinding", Assert.Throws<PdfException>(() => Rfc3161TimestampVerifier.Verify(wrongNonce, certificate.RawData, imprint, new byte[] { 9 })).Diagnostic.Code);
        using var trailing = new MemoryStream(token.Concat(new byte[] { 0 }).ToArray());
        Assert.Equal("Signature.DerProfile", Assert.Throws<PdfException>(() => Rfc3161TimestampVerifier.Verify(trailing, certificate.RawData, imprint, nonce)).Diagnostic.Code);
        var directory = Path.Combine(AppContext.BaseDirectory, "signature-fixtures");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "timestamp.der"), token);
        File.WriteAllBytes(Path.Combine(directory, "tsa.der"), certificate.RawData);
        File.WriteAllBytes(Path.Combine(directory, "imprint.bin"), imprint);
    }
    [Fact]
    public void TimestampDerDepthIsChargedBeforeOptionalCmsParsing()
    {
        var writer = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        for (int i = 0; i < 40; i++) writer.PushSequence();
        for (int i = 0; i < 40; i++) writer.PopSequence();
        using var input = new MemoryStream(writer.Encode());
        using var certificate = new CertificateSecurityTests.Certificate("DER depth fixture");
        Assert.Equal("Limit.Depth", Assert.Throws<PdfException>(() => Rfc3161TimestampVerifier.Verify(input, certificate.Value.RawData, new byte[32], new byte[] { 1 }, new(limits: new(maximumDepth: 8)))).Diagnostic.Code);
    }
    [Fact]
    public void EncapsulatedTimestampDerIsDepthCheckedBeforeBouncyCastle()
    {
        var inner = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        for (int i = 0; i < 2000; i++) inner.PushSequence();
        for (int i = 0; i < 2000; i++) inner.PopSequence();
        var w = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        var tag = new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 0, true);
        w.PushSequence(); w.WriteObjectIdentifier("1.2.840.113549.1.7.2"); w.PushSequence(tag); w.PushSequence();
        w.WriteInteger(3); w.PushSetOf(); w.PopSetOf(); w.PushSequence();
        w.WriteObjectIdentifier("1.2.840.113549.1.9.16.1.4"); w.PushSequence(tag); w.WriteOctetString(inner.Encode()); w.PopSequence(tag); w.PopSequence();
        w.PushSetOf(); w.PopSetOf(); w.PopSequence(); w.PopSequence(tag); w.PopSequence();
        using var input = new MemoryStream(w.Encode());
        using var certificate = new CertificateSecurityTests.Certificate("Encapsulated DER depth fixture");
        Assert.Equal("Limit.Depth", Assert.Throws<PdfException>(() => Rfc3161TimestampVerifier.Verify(input, certificate.Value.RawData, new byte[32], new byte[] { 1 }, new(limits: new(maximumDepth: 16)))).Diagnostic.Code);
    }

    [Fact]
    public void EncapsulatedEcdsaSignatureDerIsDepthCheckedBeforeVerification()
    {
        var inner = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        for (int i = 0; i < 2000; i++) inner.PushSequence();
        for (int i = 0; i < 2000; i++) inner.PopSequence();
        var w = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        var tag = new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 0, true);
        w.PushSequence(); w.WriteObjectIdentifier("1.2.840.113549.1.7.2"); w.PushSequence(tag); w.PushSequence();
        w.WriteInteger(3); w.PushSetOf(); w.PopSetOf(); w.PushSequence();
        w.WriteObjectIdentifier("1.2.840.113549.1.9.16.1.4"); w.PushSequence(tag); w.WriteOctetString(new byte[] { 0x30, 0 }); w.PopSequence(tag); w.PopSequence();
        w.PushSetOf(); w.PushSequence(); w.WriteInteger(1); w.PushSequence(); w.PopSequence();
        w.PushSequence(); w.WriteObjectIdentifier("2.16.840.1.101.3.4.2.1"); w.PopSequence();
        w.PushSequence(); w.WriteObjectIdentifier("1.2.840.10045.4.3.2"); w.PopSequence(); w.WriteOctetString(inner.Encode());
        w.PopSequence(); w.PopSetOf(); w.PopSequence(); w.PopSequence(tag); w.PopSequence();
        using var input = new MemoryStream(w.Encode());
        using var certificate = new CertificateSecurityTests.Certificate("Signature DER depth fixture");
        Assert.Equal("Limit.Depth", Assert.Throws<PdfException>(() => Rfc3161TimestampVerifier.Verify(input, certificate.Value.RawData, new byte[32], new byte[] { 1 }, new(limits: new(maximumDepth: 16)))).Diagnostic.Code);
    }

}
