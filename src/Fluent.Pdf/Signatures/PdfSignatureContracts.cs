using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit certification permissions; unsupported edit classes reject conservatively.</summary>
public enum PdfCertificationPermission
{
    /// <summary>An approval signature without document certification.</summary>
    None = 0,
    /// <summary>No later changes.</summary>
    NoChanges = 1,
    /// <summary>Form filling and signing; only separately validated edit profiles may apply.</summary>
    FormsAndSignatures = 2,
    /// <summary>Form filling, signing and annotations; only separately validated edit profiles may apply.</summary>
    FormsSignaturesAndAnnotations = 3
}

/// <summary>FieldMDP lock selection.</summary>
public enum PdfSignatureLockAction
{
    /// <summary>Lock every form field.</summary>
    All,
    /// <summary>Lock listed fields and their descendants.</summary>
    Include,
    /// <summary>Lock all except listed fields and their descendants.</summary>
    Exclude
}

/// <summary>Immutable explicit field-lock authority.</summary>
public sealed class PdfSignatureFieldLock
{
    /// <summary>Captures at most1024 ordinal field names. All requires an empty list; Include/Exclude require a nonempty list.</summary>
    public PdfSignatureFieldLock(PdfSignatureLockAction action, params string[] fields)
    {
        if (!Enum.IsDefined(typeof(PdfSignatureLockAction), action) || fields == null || fields.Length > 1024 || (action == PdfSignatureLockAction.All) != (fields.Length == 0))
            throw new ArgumentException("A finite explicit lock selection is required.", nameof(fields));
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in fields)
        {
            Guard.Text(field, nameof(fields), 1024);
            if (!seen.Add(field))
                throw new ArgumentException("Lock names must be unique.", nameof(fields));
        }

        Action = action;
        Fields = Array.AsReadOnly((string[])fields.Clone());
    }

    /// <summary>Gets lock selection.</summary>
    public PdfSignatureLockAction Action { get; }
    /// <summary>Gets immutable ordinal names.</summary>
    public IReadOnlyList<string> Fields { get; }
}

/// <summary>A pre-created flat signature field and optional explicit visible Form appearance.</summary>
public sealed class PdfSignatureFieldDefinition
{
    /// <summary>Captures an undotted field name, page and native unrotated PDF rectangle. A null appearance requires an empty rectangle; visible appearances must be detached Form XObjects.</summary>
    public PdfSignatureFieldDefinition(string name, int pageIndex = 0, PdfRect bounds = default, PdfGraphicsResource? appearance = null)
    {
        Guard.Text(name, nameof(name), 1024);
        if (name.IndexOf('.') >= 0 || pageIndex < 0 || (appearance == null ? bounds.Width != 0 || bounds.Height != 0 : bounds.Width <= 0 || bounds.Height <= 0))
            throw new ArgumentException("A flat name and explicit appearance/bounds are required.", nameof(name));
        Name = name;
        PageIndex = pageIndex;
        Bounds = bounds;
        Appearance = appearance;
    }

    /// <summary>Gets exact flat field name.</summary>
    public string Name { get; }
    /// <summary>Gets zero-based page.</summary>
    public int PageIndex { get; }
    /// <summary>Gets native unrotated PDF bounds.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets explicit visible appearance, or null for an invisible signature.</summary>
    public PdfGraphicsResource? Appearance { get; }
}

/// <summary>Explicit finite detached CMS signing choices. No wall-clock timestamp is invented.</summary>
public sealed class PdfSignatureOptions
{
    /// <summary>Captures a pre-created field, optional certification/locks/reason/time, and CMS reservation. A too-small reservation fails without retrying the signer.</summary>
    public PdfSignatureOptions(string fieldName, int reservedCmsBytes = 16384, PdfCertificationPermission certification = PdfCertificationPermission.None, PdfSignatureFieldLock? fieldLock = null, string? reason = null, DateTimeOffset? signingTime = null)
    {
        Guard.Text(fieldName, nameof(fieldName), 1024);
        if (reservedCmsBytes < 1024 || reservedCmsBytes > 1048576 || !Enum.IsDefined(typeof(PdfCertificationPermission), certification))
            throw new ArgumentOutOfRangeException(nameof(reservedCmsBytes));
        if (reason != null)
            Guard.Text(reason, nameof(reason), 4096);
        FieldName = fieldName;
        ReservedCmsBytes = reservedCmsBytes;
        Certification = certification;
        FieldLock = fieldLock;
        Reason = reason;
        SigningTime = signingTime;
    }

    /// <summary>Gets exact pre-created field name.</summary>
    public string FieldName { get; }
    /// <summary>Gets finite CMS byte reservation.</summary>
    public int ReservedCmsBytes { get; }
    /// <summary>Gets document certification authority.</summary>
    public PdfCertificationPermission Certification { get; }
    /// <summary>Gets optional field lock authority.</summary>
    public PdfSignatureFieldLock? FieldLock { get; }
    /// <summary>Gets optional caller reason.</summary>
    public string? Reason { get; }
    /// <summary>Gets optional caller timestamp; it is not a trusted timestamp token.</summary>
    public DateTimeOffset? SigningTime { get; }
}

/// <summary>Optional detached CMS signer/verifier with mandatory shared context and borrowed exact-byte streams. Trust/revocation/network services are never inferred.</summary>
public interface IPdfSignatureProvider : IPdfProvider
{
    /// <summary>Writes one bounded DER detached CMS over the exact concatenated byte ranges. Key/certificate selection belongs to the explicitly configured provider.</summary>
    void Sign(Stream signedBytes, Stream cmsOutput, PdfOperationContext context);
    /// <summary>Checks detached CMS cryptographic integrity only, without claiming certificate trust, revocation or modification permissions.</summary>
    bool Verify(Stream signedBytes, Stream cms, PdfOperationContext context);
}

/// <summary>One structural and cryptographic signature observation, separate from certificate trust and permitted-edit validation.</summary>
public sealed class PdfSignatureObservation
{
    internal PdfSignatureObservation(PdfObjectId id, long[] ranges, bool valid, long length)
    {
        ObjectId = id;
        ByteRange = Array.AsReadOnly(ranges);
        CryptographicallyValid = valid;
        SignedRevisionLength = length;
    }

    /// <summary>Gets signature object identity.</summary>
    public PdfObjectId ObjectId { get; }
    /// <summary>Gets exact two-pair byte range.</summary>
    public IReadOnlyList<long> ByteRange { get; }
    /// <summary>Gets detached CMS integrity; does not establish trust or permitted later edits.</summary>
    public bool CryptographicallyValid { get; }
    /// <summary>Gets covered revision extent.</summary>
    public long SignedRevisionLength { get; }
    /// <summary>Gets false: trust/revocation validation is a separate explicitly qualified service.</summary>
    public bool TrustEvaluated => false;
}

/// <summary>An immutable complete signed file. Exact signed bytes are preserved by every save.</summary>
public sealed class PdfSignedResult
{
    private readonly PdfByteStore _bytes;
    private readonly PdfOperationPolicy _policy;
    internal PdfSignedResult(PdfByteStore bytes, PdfOperationReport report, PdfOperationPolicy policy)
    {
        _bytes = bytes;
        OperationReport = report;
        _policy = policy;
    }

    /// <summary>Gets creation report.</summary>
    public PdfOperationReport OperationReport { get; }
    /// <summary>Gets exact signed extent.</summary>
    public long ByteLength => _bytes.Length;

    /// <summary>Opens a detached exact signed-file view.</summary>
    public Stream OpenPdfStream() => _bytes.Open(0, _bytes.Length);
    /// <summary>Copies exact bytes to an empty seekable borrowed sink. A copy failure may leave a partial signed file; file saves are atomic.</summary>
    public PdfOperationReport Save(Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfFont.Run("SaveSignedPdf", "managed.signatures", policy ?? _policy, cancellationToken, c => PdfSignatures.Copy(_bytes, output, c), PdfCapability.Signatures);
    /// <summary>Atomically replaces a destination only after exact signed-byte copying succeeds.</summary>
    public PdfOperationReport SaveFile(string path, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        return PdfFont.Run("SaveSignedPdfFile", "managed.signatures", policy ?? _policy, cancellationToken, c => PdfAtomicFile.Write(path, c, s => PdfSignatures.Copy(_bytes, s, c)), PdfCapability.Signatures);
    }
}
