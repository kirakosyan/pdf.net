namespace Fluent.Pdf;
/// <summary>Standard password encryption selected explicitly for new output.</summary>
public enum PdfPasswordCipher
{
    /// <summary>PDF 2.0 AES-256 with revision 6 password authentication.</summary>
    Aes256,
    /// <summary>PDF 1.6 AES-128 with revision 4 authentication, for explicit compatibility workflows.</summary>
    Aes128
}

/// <summary>Document permission bits carried by standard PDF security handlers.</summary>
[Flags]
public enum PdfDocumentPermissions
{
    /// <summary>No optional user permissions.</summary>
    None = 0,
    /// <summary>Permit printing at the quality allowed by the high-quality flag.</summary>
    Print = 4,
    /// <summary>Permit general content changes.</summary>
    Modify = 8,
    /// <summary>Permit ordinary content extraction.</summary>
    Extract = 16,
    /// <summary>Permit annotation changes and applicable form operations.</summary>
    Annotate = 32,
    /// <summary>Permit filling existing fields.</summary>
    FillForms = 256,
    /// <summary>Legacy accessibility extraction bit; PDF 2.0 readers ignore this restriction.</summary>
    ExtractForAccessibility = 512,
    /// <summary>Permit document assembly.</summary>
    Assemble = 1024,
    /// <summary>Permit high-quality printing when Print is also enabled.</summary>
    PrintHighQuality = 2048,
    /// <summary>All defined user permission bits.</summary>
    All = Print | Modify | Extract | Annotate | FillForms | ExtractForAccessibility | Assemble | PrintHighQuality
}

/// <summary>The successful authentication path; owner authority overrides user permission restrictions.</summary>
public enum PdfAuthenticationRole
{
    /// <summary>The user password matched.</summary>
    User,
    /// <summary>The owner password or certificate owner authority matched.</summary>
    Owner,
    /// <summary>A recipient certificate matched with its explicit permissions.</summary>
    Certificate,
    /// <summary>New protection was created; this does not authenticate possession of a recipient private key.</summary>
    CreatedProtection
}

/// <summary>An explicitly owned, disposable copy of standard-prepared password bytes.</summary>
/// <remarks>Revision 6 uses UTF-8 bytes after the standard's password preparation; legacy handlers use their specified eight-bit bytes. This byte API performs no implicit Unicode preparation. Dispose after all operations have acquired their private copies.</remarks>
public sealed class PdfPassword : IDisposable
{
    private readonly object _gate = new();
    private byte[]? _bytes;
    /// <summary>Captures at most 4096 prepared bytes. The caller retains ownership of the input array.</summary>
    public PdfPassword(byte[] preparedPasswordBytes)
    {
        if (preparedPasswordBytes == null)
            throw new ArgumentNullException(nameof(preparedPasswordBytes));
        if (preparedPasswordBytes.Length > 4096)
            throw new ArgumentException("The password exceeds the supported input extent.", nameof(preparedPasswordBytes));
        _bytes = (byte[])preparedPasswordBytes.Clone();
    }

    internal byte[] Copy(PdfOperationContext context)
    {
        lock (_gate)
        {
            if (_bytes == null)
                throw new ObjectDisposedException(nameof(PdfPassword));
            context.Consume(PdfResourceKind.ExpandedBytes, _bytes.Length);
            return (byte[])_bytes.Clone();
        }
    }

    /// <summary>Clears the owned bytes. Already acquired per-operation copies are independently cleared by their operations.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            PdfSecurityCryptography.Clear(_bytes);
            _bytes = null;
        }
    }
}

/// <summary>Immutable password protection choices. Credentials remain caller-owned and are never added to document snapshots or reports.</summary>
public sealed class PdfPasswordProtection
{
    /// <summary>Captures explicit user/owner credentials and permissions. AES-256 revision 6 is the default; an empty user password intentionally allows opening without a secret.</summary>
    public PdfPasswordProtection(PdfPassword userPassword, PdfPassword ownerPassword, PdfDocumentPermissions permissions = PdfDocumentPermissions.All, PdfPasswordCipher cipher = PdfPasswordCipher.Aes256, bool encryptMetadata = true)
    {
        UserPassword = userPassword ?? throw new ArgumentNullException(nameof(userPassword));
        OwnerPassword = ownerPassword ?? throw new ArgumentNullException(nameof(ownerPassword));
        if ((permissions & ~PdfDocumentPermissions.All) != 0)
            throw new ArgumentOutOfRangeException(nameof(permissions));
        if (!Enum.IsDefined(typeof(PdfPasswordCipher), cipher))
            throw new ArgumentOutOfRangeException(nameof(cipher));
        Permissions = permissions;
        Cipher = cipher;
        EncryptMetadata = encryptMetadata;
    }

    /// <summary>Gets the caller-owned prepared user credential.</summary>
    public PdfPassword UserPassword { get; }
    /// <summary>Gets the caller-owned prepared owner credential.</summary>
    public PdfPassword OwnerPassword { get; }
    /// <summary>Gets the requested user permission flags.</summary>
    public PdfDocumentPermissions Permissions { get; }
    /// <summary>Gets the explicit output cipher.</summary>
    public PdfPasswordCipher Cipher { get; }
    /// <summary>Gets whether metadata stream bytes are encrypted.</summary>
    public bool EncryptMetadata { get; }
}

/// <summary>Detached nonsecret authentication and encryption metadata, with the operation report.</summary>
public sealed class PdfSecurityResult
{
    internal PdfSecurityResult(PdfAuthenticationRole role, PdfDocumentPermissions permissions, string cipher, int revision, bool metadata, PdfOperationReport report)
    {
        Role = role;
        Permissions = permissions;
        Cipher = cipher;
        Revision = revision;
        EncryptMetadata = metadata;
        Report = report;
    }

    /// <summary>Gets the successful authentication path.</summary>
    public PdfAuthenticationRole Role { get; }
    /// <summary>Gets standard user or recipient permissions. Owner authority permits all document operations independently of these flags.</summary>
    public PdfDocumentPermissions Permissions { get; }
    /// <summary>Gets the selected cipher identity, without key material.</summary>
    public string Cipher { get; }
    /// <summary>Gets the standard handler revision, or zero for a public-key handler.</summary>
    public int Revision { get; }
    /// <summary>Gets the metadata stream encryption choice.</summary>
    public bool EncryptMetadata { get; }
    /// <summary>Gets resource accounting and nonsecret diagnostics.</summary>
    public PdfOperationReport Report { get; }
}
