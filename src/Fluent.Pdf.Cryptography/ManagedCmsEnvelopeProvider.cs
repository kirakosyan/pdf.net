using System.Formats.Asn1;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Fluent.Pdf.Cryptography;
/// <summary>Offline RSA/AES PDF seed envelopes using platform cryptography and the pinned ASN.1 codec. No certificate stores, network trust retrieval or private-key export.</summary>
public sealed partial class ManagedCmsEnvelopeProvider : IPdfCmsEnvelopeProvider
{
    private const string DataOid = "1.2.840.113549.1.7.1", EnvelopedOid = "1.2.840.113549.1.7.3", Aes256Oid = "2.16.840.1.101.3.4.1.42", RsaOid = "1.2.840.113549.1.1.1", OaepOid = "1.2.840.113549.1.1.7", MgfOid = "1.2.840.113549.1.1.8", LabelOid = "1.2.840.113549.1.1.9", Sha256Oid = "2.16.840.1.101.3.4.2.1", Sha1Oid = "1.3.14.3.2.26";
    private static readonly Asn1Tag ExplicitContent = new(TagClass.ContextSpecific, 0, true);
    private static readonly Asn1Tag ImplicitOctets = new(TagClass.ContextSpecific, 0);
    /// <summary>Creates a stateless explicit software-envelope provider.</summary>
    public ManagedCmsEnvelopeProvider()
    {
    }

    /// <summary>Gets source capability metadata, independent of production qualification.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("managed.cms", "1.0.0", PdfCapability.CmsEnvelopes);

    /// <summary>Encrypts a 20/24-byte PDF seed payload for up to 64 explicit recipients with RSA keys of 2048–8192 bits.</summary>
    public void Encrypt(IReadOnlyList<PdfCertificateRecipient> recipients, Stream plaintext, Stream envelope, PdfOperationContext context)
    {
        if (recipients == null)
            throw new ArgumentNullException(nameof(recipients));
        if (plaintext == null)
            throw new ArgumentNullException(nameof(plaintext));
        if (envelope == null)
            throw new ArgumentNullException(nameof(envelope));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (recipients.Count < 1 || recipients.Count > 64)
            throw new ArgumentException("One through 64 recipients are required.", nameof(recipients));
        for (int i = 0; i < recipients.Count; i++)
        {
            context.Checkpoint();
            if (recipients[i] == null || recipients[0] == null)
                throw new ArgumentException("Recipients cannot contain null.", nameof(recipients));
        }

        byte[] seed = Array.Empty<byte>(), key = Array.Empty<byte>(), iv = Array.Empty<byte>(), cipher = Array.Empty<byte>();
        try
        {
            seed = Read(plaintext, 24, context);
            if (seed.Length != 20 && seed.Length != 24)
                throw Error(context, "CmsSeed", "A PDF envelope payload must contain a seed with optional permissions.");
            key = PdfSecurityCryptography.Random(32, context);
            iv = PdfSecurityCryptography.Random(16, context);
            cipher = PdfSecurityCryptography.Aes(key, iv, seed, true, CipherMode.CBC, PaddingMode.PKCS7, context);
            context.Consume(PdfResourceKind.ExpandedBytes, recipients.Count * 32L + 128);
            var infos = new List<byte[]>(recipients.Count);
            long total = 512;
            foreach (var recipient in recipients)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                if (recipient == null)
                    throw new ArgumentException("Recipients cannot contain null.", nameof(recipients));
                using var certBytes = recipient.OpenCertificate();
                using var certificate = Certificate(certBytes, context);
                using var rsa = PublicKey(certificate, context);
                if (rsa.KeySize < 2048 || rsa.KeySize > 8192)
                    throw Error(context, "CertificateKeySize", "New recipient envelopes require RSA keys between 2048 and 8192 bits.");
                context.Consume(PdfResourceKind.ExpandedBytes, 65536L + rsa.KeySize / 8);
                var wrapped = rsa.Encrypt(key, recipient.KeyTransport == PdfCmsKeyTransport.OaepSha256 ? RSAEncryptionPadding.OaepSHA256 : RSAEncryptionPadding.Pkcs1);
                try
                {
                    var issuer = certificate.IssuerName.RawData;
                    var serial = certificate.GetSerialNumber();
                    Array.Reverse(serial);
                    context.Consume(PdfResourceKind.ExpandedBytes, (issuer.Length + serial.Length + wrapped.Length + 512L) * 4);
                    var writer = new AsnWriter(AsnEncodingRules.DER);
                    writer.PushSequence();
                    writer.WriteInteger(0);
                    writer.PushSequence();
                    writer.WriteEncodedValue(issuer);
                    writer.WriteIntegerUnsigned(serial);
                    writer.PopSequence();
                    Transport(writer, recipient.KeyTransport);
                    writer.WriteOctetString(wrapped);
                    writer.PopSequence();
                    total += writer.GetEncodedLength();
                    if (total > 65536)
                        throw Error(context, "CmsExtent", "The recipient envelope exceeds its supported extent.");
                    infos.Add(writer.Encode());
                }
                finally
                {
                    Clear(wrapped);
                }
            }

            context.Consume(PdfResourceKind.ExpandedBytes, total * 4);
            var output = new AsnWriter(AsnEncodingRules.DER);
            output.PushSequence();
            output.WriteObjectIdentifier(EnvelopedOid);
            output.PushSequence(ExplicitContent);
            output.PushSequence();
            output.WriteInteger(0);
            output.PushSetOf();
            foreach (var info in infos)
                output.WriteEncodedValue(info);
            output.PopSetOf();
            output.PushSequence();
            output.WriteObjectIdentifier(DataOid);
            output.PushSequence();
            output.WriteObjectIdentifier(Aes256Oid);
            output.WriteOctetString(iv);
            output.PopSequence();
            output.WriteOctetString(cipher, ImplicitOctets);
            output.PopSequence();
            output.PopSequence();
            output.PopSequence(ExplicitContent);
            output.PopSequence();
            var encoded = output.Encode();
            try
            {
                Write(envelope, encoded, context);
            }
            finally
            {
                Clear(encoded);
            }
        }
        catch (CryptographicException)
        {
            context.Checkpoint();
            throw Error(context, "CmsKeyOperation", "The explicit cryptographic key operation failed.");
        }
        catch (PlatformNotSupportedException)
        {
            context.Checkpoint();
            throw Error(context, "CmsPlatform", "This runtime or key provider does not support the requested cryptographic operation.");
        }
        finally
        {
            Clear(seed);
            Clear(key);
            Clear(iv);
            Clear(cipher);
        }
    }

    /// <summary>Decrypts only a matching explicit recipient and RSA handle. DER and payload extents are bounded; legacy RSA/SHA-1 transport needs explicit permission.</summary>
    public bool TryDecrypt(PdfCertificateCredential credential, bool allowLegacyKeyTransport, Stream envelope, Stream plaintext, PdfOperationContext context)
    {
        if (credential == null)
            throw new ArgumentNullException(nameof(credential));
        if (envelope == null)
            throw new ArgumentNullException(nameof(envelope));
        if (plaintext == null)
            throw new ArgumentNullException(nameof(plaintext));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        byte[] source = Array.Empty<byte>(), key = Array.Empty<byte>(), decoded = Array.Empty<byte>();
        try
        {
            source = Read(envelope, 65536, context);
            context.Consume(PdfResourceKind.ExpandedBytes, source.Length * 32L + 131072);
            using var certBytes = credential.OpenCertificate();
            using var certificate = Certificate(certBytes, context);
            using var publicKey = certificate.GetRSAPublicKey() ?? throw Error(context, "CertificateKeyType", "An RSA recipient certificate is required.");
            if (publicKey.KeySize < (allowLegacyKeyTransport ? 1024 : 2048) || publicKey.KeySize > 8192)
                throw Error(context, "CertificateKeySize", "The recipient key size is outside the selected profile.");
            CheckKey(publicKey, credential.PrivateKey, context);
            var selected = Parse(source, certificate, allowLegacyKeyTransport, context);
            if (selected == null)
                return false;
            context.Consume(PdfResourceKind.ExpandedBytes, 65536L + selected.EncryptedKey.Length);
            key = credential.PrivateKey.Decrypt(selected.EncryptedKey, selected.Padding);
            if (key.Length != selected.KeyBytes)
                throw Error(context, "CmsContent", "The envelope content key has an invalid extent.");
            decoded = DecryptContent(selected, key, context);
            if (decoded.Length != 20 && decoded.Length != 24)
                throw Error(context, "CmsSeed", "The decrypted envelope is outside the supported PDF seed profile.");
            Write(plaintext, decoded, context);
            return true;
        }
        catch (AsnContentException)
        {
            context.Checkpoint();
            throw Error(context, "CmsMalformed", "The CMS envelope is malformed or outside the definite DER profile.");
        }
        catch (CryptographicException)
        {
            context.Checkpoint();
            throw Error(context, "CmsKeyOperation", "The explicit cryptographic key operation failed or the envelope is malformed.");
        }
        catch (PlatformNotSupportedException)
        {
            context.Checkpoint();
            throw Error(context, "CmsPlatform", "This runtime or key provider does not support the requested cryptographic operation.");
        }
        catch (NotSupportedException)
        {
            context.Checkpoint();
            throw Error(context, "CmsKeyProfile", "The explicit RSA handle does not support the required key operations.");
        }
        finally
        {
            Clear(source);
            Clear(key);
            Clear(decoded);
        }
    }

    private static void Transport(AsnWriter writer, PdfCmsKeyTransport transport)
    {
        writer.PushSequence();
        if (transport == PdfCmsKeyTransport.LegacyPkcs1)
        {
            writer.WriteObjectIdentifier(RsaOid);
            writer.WriteNull();
        }
        else
        {
            writer.WriteObjectIdentifier(OaepOid);
            writer.PushSequence();
            writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            HashAlgorithm(writer);
            writer.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 0, true));
            writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 1, true));
            writer.PushSequence();
            writer.WriteObjectIdentifier(MgfOid);
            HashAlgorithm(writer);
            writer.PopSequence();
            writer.PopSequence(new Asn1Tag(TagClass.ContextSpecific, 1, true));
            writer.PopSequence();
        }

        writer.PopSequence();
        void HashAlgorithm(AsnWriter output)
        {
            output.PushSequence();
            output.WriteObjectIdentifier(Sha256Oid);
            output.WriteNull();
            output.PopSequence();
        }
    }

    private static RSA PublicKey(X509Certificate2 certificate, PdfOperationContext context)
    {
        using var source = certificate.GetRSAPublicKey() ?? throw Error(context, "CertificateKeyType", "An RSA recipient certificate is required.");
        context.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var parameters = source.ExportParameters(false);
        RSA result;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            result = new RSACng();
        else
            result = RSA.Create();
        try
        {
            result.ImportParameters(parameters);
            return result;
        }
        catch
        {
            result.Dispose();
            throw;
        }
    }

    private static void CheckKey(RSA certificateKey, RSA privateKey, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 16384);
        var expected = certificateKey.ExportParameters(false);
        var actual = privateKey.ExportParameters(false);
        if (expected.Modulus == null || expected.Exponent == null || actual.Modulus == null || actual.Exponent == null || !PdfSecurityCryptography.Equal(expected.Modulus, actual.Modulus) || !PdfSecurityCryptography.Equal(expected.Exponent, actual.Exponent))
            throw Error(context, "CertificateKeyMismatch", "The explicit RSA handle does not match the public recipient certificate.");
    }

    private static X509Certificate2 Certificate(Stream source, PdfOperationContext context)
    {
        var bytes = Read(source, 65536, context);
        context.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 8L + 65536);
        if (X509Certificate2.GetCertContentType(bytes) != X509ContentType.Cert)
            throw Error(context, "CertificateEncoding", "Only public DER certificate data is accepted.");
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(bytes);
#else
        return new X509Certificate2(bytes);
#endif
    }

    private static byte[] Read(Stream source, int maximum, PdfOperationContext context)
    {
        using var lease = context.BorrowExpandedInput(source);
        context.Consume(PdfResourceKind.ExpandedBytes, maximum + 1L);
        var part = new byte[maximum + 1];
        try
        {
            int count = 0;
            while (count <= maximum)
            {
                context.Checkpoint();
                int read = lease.Read(part, count, Math.Min(4096, part.Length - count));
                if (read == 0)
                    break;
                count += read;
                if (count > maximum)
                    throw Error(context, "CmsExtent", "The CMS input exceeds its declared extent.");
            }

            return PdfSecurityCryptography.Slice(part, 0, count, context);
        }
        finally
        {
            Clear(part);
        }
    }

    private static void Write(Stream target, byte[] bytes, PdfOperationContext context)
    {
        context.Checkpoint();
        using var lease = new BorrowedStream(target, false, context);
        lease.Write(bytes, 0, bytes.Length);
        context.Checkpoint();
    }

    private static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, "Security." + code, message);
    private static void Clear(byte[]? bytes) => PdfSecurityCryptography.Clear(bytes);
}
