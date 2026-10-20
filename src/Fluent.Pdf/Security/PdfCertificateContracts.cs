using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Fluent.Pdf;
/// <summary>Explicit file cipher for certificate security handlers.</summary>
public enum PdfPublicKeyCipher
{
    /// <summary>PDF 2.0 AES-256.</summary>
    Aes256,
    /// <summary>Explicit PDF 1.6 AES-128 compatibility output.</summary>
    Aes128
}

/// <summary>Explicit RSA key transport for a PDF recipient envelope.</summary>
public enum PdfCmsKeyTransport
{
    /// <summary>RSA-OAEP with SHA-256 and an empty label.</summary>
    OaepSha256,
    /// <summary>Explicit legacy RSA PKCS#1 v1.5 compatibility transport.</summary>
    LegacyPkcs1
}

/// <summary>An immutable public certificate snapshot and its requested PDF permissions.</summary>
public sealed class PdfCertificateRecipient
{
    private readonly byte[] _certificate;
    /// <summary>Captures only public certificate bytes; the caller retains certificate ownership. Certificate trust and validity policy remain explicit caller decisions.</summary>
    public PdfCertificateRecipient(X509Certificate2 certificate, PdfDocumentPermissions permissions = PdfDocumentPermissions.All, bool ownerAuthority = false, PdfCmsKeyTransport keyTransport = PdfCmsKeyTransport.OaepSha256)
    {
        _certificate = PdfCertificateBytes.Capture(certificate);
        if ((permissions & ~PdfDocumentPermissions.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(permissions));
        if (!Enum.IsDefined(typeof(PdfCmsKeyTransport), keyTransport))
            throw new ArgumentOutOfRangeException(nameof(keyTransport));
        Permissions = permissions;
        OwnerAuthority = ownerAuthority;
        KeyTransport = keyTransport;
    }

    /// <summary>Gets recipient permission flags.</summary>
    public PdfDocumentPermissions Permissions { get; }
    /// <summary>Gets whether this recipient may change protection and exercise owner authority.</summary>
    public bool OwnerAuthority { get; }
    /// <summary>Gets the explicit key transport.</summary>
    public PdfCmsKeyTransport KeyTransport { get; }

    /// <summary>Opens detached read-only public DER certificate bytes.</summary>
    public Stream OpenCertificate() => new MemoryStream(_certificate, false);
}

/// <summary>An explicit public certificate snapshot paired with a caller-owned private RSA handle.</summary>
/// <remarks>The SDK never searches certificate stores or exports the private key. Keep the key alive and serialize operations if its provider requires that. Hardware/PIN behavior belongs to the explicitly supplied key provider.</remarks>
public sealed class PdfCertificateCredential
{
    private readonly byte[] _certificate;
    /// <summary>Captures public bytes and borrows the explicitly supplied RSA handle without taking ownership.</summary>
    public PdfCertificateCredential(X509Certificate2 certificate, RSA privateKey)
    {
        _certificate = PdfCertificateBytes.Capture(certificate);
        PrivateKey = privateKey ?? throw new ArgumentNullException(nameof(privateKey));
    }

    /// <summary>Gets the caller-owned RSA key, which is never disposed by the SDK.</summary>
    public RSA PrivateKey { get; }

    /// <summary>Opens detached read-only public DER certificate bytes.</summary>
    public Stream OpenCertificate() => new MemoryStream(_certificate, false);
}

/// <summary>Immutable recipient and file-cipher choices. All recipients and certificate trust decisions are explicit.</summary>
public sealed class PdfCertificateProtection
{
    /// <summary>Captures one through 64 public recipient snapshots. AES-256 is the default; repeated certificate identities are rejected during authoring.</summary>
    public PdfCertificateProtection(PdfCertificateRecipient[] recipients, PdfPublicKeyCipher cipher = PdfPublicKeyCipher.Aes256, bool encryptMetadata = true)
    {
        if (recipients == null)
            throw new ArgumentNullException(nameof(recipients));
        if (recipients.Length < 1 || recipients.Length > 64 || recipients.Any(value => value == null))
            throw new ArgumentException("One through 64 nonnull recipients are required.", nameof(recipients));
        if (!Enum.IsDefined(typeof(PdfPublicKeyCipher), cipher))
            throw new ArgumentOutOfRangeException(nameof(cipher));
        Recipients = Array.AsReadOnly((PdfCertificateRecipient[])recipients.Clone());
        Cipher = cipher;
        EncryptMetadata = encryptMetadata;
    }

    /// <summary>Gets immutable public recipient snapshots.</summary>
    public IReadOnlyList<PdfCertificateRecipient> Recipients { get; }
    /// <summary>Gets the file cipher.</summary>
    public PdfPublicKeyCipher Cipher { get; }
    /// <summary>Gets whether metadata stream bytes are encrypted.</summary>
    public bool EncryptMetadata { get; }
}

/// <summary>An optional offline CMS recipient-envelope boundary using mandatory shared budgets and borrowed streams.</summary>
public interface IPdfCmsEnvelopeProvider : IPdfProvider
{
    /// <summary>Encrypts one bounded PDF seed/permission payload for a nonempty, equally authorized recipient group. Algorithms and private staging must be explicit; no key or network discovery.</summary>
    void Encrypt(IReadOnlyList<PdfCertificateRecipient> recipients, Stream plaintext, Stream envelope, PdfOperationContext context);
    /// <summary>Decrypts a matching recipient using only its explicit RSA handle, returning false without output when its certificate is absent. Legacy transport requires explicit permission. Caller streams and key remain borrowed.</summary>
    bool TryDecrypt(PdfCertificateCredential credential, bool allowLegacyKeyTransport, Stream envelope, Stream plaintext, PdfOperationContext context);
}

internal static class PdfCertificateBytes
{
    internal static byte[] Capture(X509Certificate2 certificate)
    {
        if (certificate == null)
            throw new ArgumentNullException(nameof(certificate));
        var bytes = certificate.RawData;
        if (bytes.Length == 0 || bytes.Length > 65536)
            throw new ArgumentException("A bounded public certificate is required.", nameof(certificate));
        return bytes;
    }
}
