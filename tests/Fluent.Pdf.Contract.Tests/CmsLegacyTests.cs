using System.Formats.Asn1;
using System.IO;
using System.Security.Cryptography;
using Fluent.Pdf.Cryptography;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class CmsLegacyTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LegacyContentCiphersRequireAnExplicitReadPolicy(bool tripleDes)
    {
        using var certificate = new CertificateSecurityTests.Certificate("Original Legacy Envelope");
        byte[] key = Enumerable.Range(1, tripleDes ? 24 : 16).Select(i => (byte)i).ToArray();
        byte[] iv = new byte[8], seed = Enumerable.Range(0, 24).Select(i => (byte)i).ToArray();
        using SymmetricAlgorithm cipher = tripleDes ? TripleDES.Create() : RC2.Create();
        cipher.Key = key;
        cipher.IV = iv;
        cipher.Padding = PaddingMode.PKCS7;
        cipher.Mode = CipherMode.CBC;
        using var transform = cipher.CreateEncryptor();
        var encrypted = transform.TransformFinalBlock(seed, 0, seed.Length);
        var writer = new AsnWriter(AsnEncodingRules.DER);
        var content = new Asn1Tag(TagClass.ContextSpecific, 0, true);
        writer.PushSequence();
        writer.WriteObjectIdentifier("1.2.840.113549.1.7.3");
        writer.PushSequence(content);
        writer.PushSequence();
        writer.WriteInteger(0);
        writer.PushSetOf();
        writer.PushSequence();
        writer.WriteInteger(0);
        writer.PushSequence();
        writer.WriteEncodedValue(certificate.Value.IssuerName.RawData);
        var serial = certificate.Value.GetSerialNumber();
        Array.Reverse(serial);
        writer.WriteIntegerUnsigned(serial);
        writer.PopSequence();
        // Legacy RSA is explicit here; modern output is separately tested with OAEP-SHA256.
        writer.PushSequence();
        writer.WriteObjectIdentifier("1.2.840.113549.1.1.1");
        writer.WriteNull();
        writer.PopSequence();
        writer.WriteOctetString(certificate.Key.Encrypt(key, RSAEncryptionPadding.Pkcs1));
        writer.PopSequence();
        writer.PopSetOf();
        writer.PushSequence();
        writer.WriteObjectIdentifier("1.2.840.113549.1.7.1");
        writer.PushSequence();
        writer.WriteObjectIdentifier(tripleDes ? "1.2.840.113549.3.7" : "1.2.840.113549.3.2");
        if (!tripleDes)
        {
            writer.PushSequence();
            writer.WriteInteger(58);
        }

        writer.WriteOctetString(iv);
        if (!tripleDes)
            writer.PopSequence();
        writer.PopSequence();
        writer.WriteOctetString(encrypted, new Asn1Tag(TagClass.ContextSpecific, 0));
        writer.PopSequence();
        writer.PopSequence();
        writer.PopSequence(content);
        writer.PopSequence();
        var bytes = writer.Encode();
        var provider = new ManagedCmsEnvelopeProvider();
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        Assert.Equal("Security.LegacyReadBlocked", Assert.Throws<PdfException>(() => PolicyTests.Run(context => provider.TryDecrypt(certificate.Credential, false, input, output, context))).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        input.Position = 0;
        PolicyTests.Run(context => Assert.True(provider.TryDecrypt(certificate.Credential, true, input, output, context)));
        Assert.Equal(seed, output.ToArray());
    }
}
