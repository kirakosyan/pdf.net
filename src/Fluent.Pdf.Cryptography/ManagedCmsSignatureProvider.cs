using System.Formats.Asn1;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Threading;

namespace Fluent.Pdf.Cryptography;
/// <summary>Original offline DER CMS SignedData using SHA256/RSA-PKCS1 and one explicit public certificate. No store/key discovery, network, trust inference or private-key export.</summary>
public sealed class ManagedCmsSignatureProvider : IPdfSignatureProvider, IDisposable
{
    private const string Data = "1.2.840.113549.1.7.1", SignedData = "1.2.840.113549.1.7.2", Sha256 = "2.16.840.1.101.3.4.2.1", Rsa = "1.2.840.113549.1.1.1";
    // The netstandard2.0 reference enum predates the named value; supported actual runtimes expose flag32.
    private const X509KeyStorageFlags EphemeralKeys = (X509KeyStorageFlags)32;
    private static readonly Asn1Tag Explicit = new(TagClass.ContextSpecific, 0, true);
    private readonly PdfCertificateCredential? _credential;
    private X509Certificate2? _ownedCertificate;
    private RSA? _ownedKey;
    private bool _disposed;
    /// <summary>Creates an offline integrity verifier without a signing key.</summary>
    public ManagedCmsSignatureProvider()
    {
    }

    /// <summary>Borrows an explicit public certificate/RSA credential. Certificates loaded by the caller from PKCS12, stores or hardware providers use the same boundary; the key remains caller-owned.</summary>
    public ManagedCmsSignatureProvider(PdfCertificateCredential credential)
    {
        _credential = credential ?? throw new ArgumentNullException(nameof(credential));
    }

    /// <summary>Gets explicit detached signature service metadata; this is not trust or production qualification.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("managed.cms-signatures", "1.0.0", PdfCapability.Signatures);
    /// <summary>Gets the bounded PKCS12 import report, or null for a borrowed credential/verifier.</summary>
    public PdfOperationReport? ImportReport { get; private set; }

    /// <summary>Imports a bounded explicit PKCS12 with an ephemeral RSA key. The password is never logged; this returned provider owns the imported certificate/key and must be disposed.</summary>
    public static ManagedCmsSignatureProvider FromPkcs12(Stream input, string password, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || password == null)
            throw new ArgumentNullException(input == null ? nameof(input) : nameof(password));
        ManagedCmsSignatureProvider? result = null;
        try
        {
            var report = PdfFont.Run("ImportSignaturePkcs12", "managed.cms-signatures", policy, cancellationToken, c =>
            {
                var bytes = Read(input, 1048576, c);
                X509Certificate2? certificate = null;
                RSA? key = null;
                try
                {
                    c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 8L + 1048576);
                    if (!Enum.IsDefined(typeof(X509KeyStorageFlags), EphemeralKeys)) throw Error(c, "Pkcs12RuntimeProfile", "This actual runtime cannot guarantee ephemeral PKCS12 key storage.");
#if NET9_0_OR_GREATER
                certificate=X509CertificateLoader.LoadPkcs12(bytes,password,EphemeralKeys);
#else
                    certificate = new X509Certificate2(bytes, password, EphemeralKeys);
#endif
                    c.Checkpoint();
                    key = certificate.GetRSAPrivateKey();
                    if (key == null || key.KeySize < 2048 || key.KeySize > 8192)
                        throw Error(c, "KeyProfile", "An explicit RSA2048–8192 private key is required.");
                    result = new(new PdfCertificateCredential(certificate, key))
                    {
                        _ownedCertificate = certificate,
                        _ownedKey = key
                    };
                    certificate = null;
                    key = null;
                }
                catch (CryptographicException)
                {
                    c.Checkpoint();
                    throw Error(c, "Pkcs12Import", "The explicit PKCS12 could not be imported.");
                }
                finally
                {
                    Array.Clear(bytes, 0, bytes.Length);
                    certificate?.Dispose();
                    key?.Dispose();
                }
            }, PdfCapability.Signatures);
            result!.ImportReport = report;
            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }

    /// <summary>Signs exact streamed bytes with a borrowed/imported RSA key and emits one detached DER CMS. No timestamp or signed attribute is synthesized.</summary>
    public void Sign(Stream signedBytes, Stream cmsOutput, PdfOperationContext context)
    {
        Alive();
        var c = context ?? throw new ArgumentNullException(nameof(context));
        c.Checkpoint();
        if (_credential == null)
            throw Error(c, "SigningKeyRequired", "An explicit signing credential is required.");
        using var publicBytes = _credential.OpenCertificate();
        var raw = Read(publicBytes, 65536, c);
        c.Consume(PdfResourceKind.ExpandedBytes, raw.Length * 16L + 262144);
        using var certificate = Certificate(raw);
        using var publicKey = certificate.GetRSAPublicKey();
        if (publicKey == null || publicKey.KeySize < 2048 || publicKey.KeySize > 8192 || _credential.PrivateKey.KeySize != publicKey.KeySize)
            throw Error(c, "KeyProfile", "Matching RSA2048–8192 keys are required.");
        byte[] digest = Hash(signedBytes, c), signature = Array.Empty<byte>();
        try
        {
            signature = _credential.PrivateKey.SignHash(digest, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            c.Checkpoint();
            if (!publicKey.VerifyHash(digest, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                throw Error(c, "KeyMismatch", "The explicit private key does not match the certificate.");
            var w = new AsnWriter(AsnEncodingRules.DER);
            w.PushSequence();
            w.WriteObjectIdentifier(SignedData);
            w.PushSequence(Explicit);
            w.PushSequence();
            w.WriteInteger(1);
            w.PushSetOf();
            Algorithm(w, Sha256);
            w.PopSetOf();
            w.PushSequence();
            w.WriteObjectIdentifier(Data);
            w.PopSequence();
            w.PushSetOf(Explicit);
            w.WriteEncodedValue(raw);
            w.PopSetOf(Explicit);
            w.PushSetOf();
            w.PushSequence();
            w.WriteInteger(1);
            w.PushSequence();
            w.WriteEncodedValue(certificate.IssuerName.RawData);
            var serial = certificate.GetSerialNumber();
            Array.Reverse(serial);
            w.WriteIntegerUnsigned(serial);
            w.PopSequence();
            Algorithm(w, Sha256);
            Algorithm(w, Rsa);
            w.WriteOctetString(signature);
            w.PopSequence();
            w.PopSetOf();
            w.PopSequence();
            w.PopSequence(Explicit);
            w.PopSequence();
            var encoded = w.Encode();
            using var output = new BorrowedStream(cmsOutput, false, c);
            output.Write(encoded, 0, encoded.Length);
            c.Checkpoint();
        }
        catch (CryptographicException)
        {
            c.Checkpoint();
            throw Error(c, "KeyOperation", "The explicit RSA operation failed.");
        }
        finally
        {
            Array.Clear(digest, 0, digest.Length);
            Array.Clear(signature, 0, signature.Length);
        }
    }

    /// <summary>Verifies one detached DER SHA256/RSA CMS with one embedded signer certificate and no signed attributes. Integrity is independent of certificate trust, expiry, revocation and permitted later edits.</summary>
    public bool Verify(Stream signedBytes, Stream cms, PdfOperationContext context)
    {
        Alive();
        var c = context ?? throw new ArgumentNullException(nameof(context));
        c.Checkpoint();
        var encoded = Read(cms, 1048576, c);
        c.Consume(PdfResourceKind.ExpandedBytes, encoded.Length * 8L + 262144);
        try
        {
            var input = new AsnReader(encoded, AsnEncodingRules.DER);
            var outer = input.ReadSequence();
            if (outer.ReadObjectIdentifier() != SignedData)
                return false;
            var wrapper = outer.ReadSequence(Explicit);
            var data = wrapper.ReadSequence();
            if (data.ReadInteger() != 1)
                return false;
            var algorithms = data.ReadSetOf();
            if (!Algorithm(algorithms, Sha256) || algorithms.HasData)
                return false;
            var content = data.ReadSequence();
            if (content.ReadObjectIdentifier() != Data || content.HasData)
                return false;
            var certificates = data.ReadSetOf(Explicit);
            var raw = certificates.ReadEncodedValue().ToArray();
            if (raw.Length > 65536 || certificates.HasData)
                return false;
            using var certificate = Certificate(raw);
            var signers = data.ReadSetOf();
            var signer = signers.ReadSequence();
            if (signer.ReadInteger() != 1)
                return false;
            var identity = signer.ReadSequence();
            var issuer = identity.ReadEncodedValue().ToArray();
            var serial = identity.ReadIntegerBytes().ToArray();
            identity.ThrowIfNotEmpty();
            var ownSerial = certificate.GetSerialNumber();
            Array.Reverse(ownSerial);
            int trim = 0, ownTrim = 0;
            while (trim < serial.Length - 1 && serial[trim] == 0)
                trim++;
            while (ownTrim < ownSerial.Length - 1 && ownSerial[ownTrim] == 0)
                ownTrim++;
            if (!issuer.SequenceEqual(certificate.IssuerName.RawData) || !serial.Skip(trim).SequenceEqual(ownSerial.Skip(ownTrim)))
                return false;
            if (!Algorithm(signer, Sha256) || !Algorithm(signer, Rsa))
                return false;
            var signature = signer.ReadOctetString();
            signer.ThrowIfNotEmpty();
            signers.ThrowIfNotEmpty();
            data.ThrowIfNotEmpty();
            wrapper.ThrowIfNotEmpty();
            outer.ThrowIfNotEmpty();
            input.ThrowIfNotEmpty();
            using var key = certificate.GetRSAPublicKey();
            if (key == null || key.KeySize < 2048 || key.KeySize > 8192 || signature.Length != key.KeySize / 8)
                return false;
            var digest = Hash(signedBytes, c);
            try
            {
                bool valid = key.VerifyHash(digest, signature, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
                c.Checkpoint();
                return valid;
            }
            finally
            {
                Array.Clear(digest, 0, digest.Length);
            }
        }
        catch (AsnContentException)
        {
            c.Checkpoint();
            return false;
        }
        catch (CryptographicException)
        {
            c.Checkpoint();
            return false;
        }
    }

    /// <summary>Releases only keys/certificates imported by FromPkcs12; borrowed caller credentials remain alive.</summary>
    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;
        _ownedKey?.Dispose();
        _ownedCertificate?.Dispose();
    }

    private void Alive()
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ManagedCmsSignatureProvider));
    }

    private static void Algorithm(AsnWriter w, string oid)
    {
        w.PushSequence();
        w.WriteObjectIdentifier(oid);
        w.WriteNull();
        w.PopSequence();
    }

    private static bool Algorithm(AsnReader r, string oid)
    {
        var a = r.ReadSequence();
        if (a.ReadObjectIdentifier() != oid)
            return false;
        if (a.HasData)
            a.ReadNull();
        a.ThrowIfNotEmpty();
        return true;
    }

    private static byte[] Hash(Stream input, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 65536);
        var buffer = new byte[65536];
        using var source = c.BorrowExpandedInput(input);
        using var hash = SHA256.Create();
        int n;
        while ((n = source.Read(buffer, 0, buffer.Length)) > 0)
        {
            c.Checkpoint();
            hash.TransformBlock(buffer, 0, n, buffer, 0);
        }

        hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        c.Checkpoint();
        return hash.Hash!;
    }

    private static byte[] Read(Stream input, int maximum, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, maximum * 2L + 2);
        var buffer = new byte[maximum + 1];
        int at = 0;
        using var source = c.BorrowExpandedInput(input);
        while (at < buffer.Length)
        {
            c.Checkpoint();
            int n = source.Read(buffer, at, Math.Min(4096, buffer.Length - at));
            if (n == 0)
                break;
            at += n;
        }

        if (at > maximum)
            throw Error(c, "CmsExtent", "The explicit cryptographic input exceeds its finite extent.");
        var result = new byte[at];
        Array.Copy(buffer, result, at);
        Array.Clear(buffer, 0, buffer.Length);
        return result;
    }

    private static X509Certificate2 Certificate(byte[] raw)
    {
#if NET9_0_OR_GREATER
        return X509CertificateLoader.LoadCertificate(raw);
#else
        return new X509Certificate2(raw);
#endif
    }

    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Signature." + code, message);
}
