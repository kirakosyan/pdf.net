using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>A cross-reference change declaration. It does not infer semantic or certification permission.</summary>
public sealed class PdfRevisionChange
{
    internal PdfRevisionChange(PdfObjectId id, PdfCrossReferenceKind kind, bool existed)
    {
        ObjectId = id;
        Kind = kind;
        PreviouslyDefined = existed;
    }
    /// <summary>Gets exact declared object identity.</summary>
    public PdfObjectId ObjectId
    {
        get;
    }
    /// <summary>Gets free, direct or compressed declaration.</summary>
    public PdfCrossReferenceKind Kind
    {
        get;
    }
    /// <summary>Gets whether an older revision declared this object number.</summary>
    public bool PreviouslyDefined
    {
        get;
    }
}
/// <summary>One immutable revision inventory with structural changes and signatures physically introduced in it.</summary>
public sealed class PdfRevisionAuditRecord
{
    internal PdfRevisionAuditRecord(int index, long offset, List<PdfRevisionChange> changes, List<PdfSignatureObservation> signatures)
    {
        RevisionIndex = index;
        StartXref = offset;
        Changes = changes.AsReadOnly();
        Signatures = signatures.AsReadOnly();
    }
    /// <summary>Gets source revision index, newest first.</summary>
    public int RevisionIndex
    {
        get;
    }
    /// <summary>Gets linked cross-reference offset.</summary>
    public long StartXref
    {
        get;
    }
    /// <summary>Gets declared object changes, without an inferred semantic classification.</summary>
    public IReadOnlyList<PdfRevisionChange> Changes
    {
        get;
    }
    /// <summary>Gets original signatures, including those hidden or deleted by later revisions. Integrity never implies later-edit permission.</summary>
    public IReadOnlyList<PdfSignatureObservation> Signatures
    {
        get;
    }
}
/// <summary>A conservative preflight decision for the existing writer/signing profiles.</summary>
public sealed class PdfSignedEditPlan
{
    internal PdfSignedEditPlan(bool allowed, bool prefix, string reason)
    {
        Allowed = allowed;
        PreservesOriginalPrefix = prefix;
        Reason = reason;
    }
    /// <summary>Gets whether this exact requested profile passes the source-policy barrier.</summary>
    public bool Allowed
    {
        get;
    }
    /// <summary>Gets whether an allowed operation retains every original byte. This alone is not cryptographic, trust or permitted-edit validation.</summary>
    public bool PreservesOriginalPrefix
    {
        get;
    }
    /// <summary>Gets the stable policy reason.</summary>
    public string Reason
    {
        get;
    }
}
public static partial class PdfSignatures
{
    /// <summary>Audits every linked revision, including signatures removed from the active graph. Object changes are structural declarations; semantic modification validation remains separate.</summary>
    public static IReadOnlyList<PdfRevisionAuditRecord> AuditRevisions(PdfSyntaxDocument source, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || providers == null)
            throw new ArgumentNullException(source == null ? nameof(source) : nameof(providers));
        var verifier = providers.GetRequired<IPdfSignatureProvider>(PdfCapability.Signatures);
        var records = new List<PdfRevisionAuditRecord>();
        PdfFont.Run("AuditSignedRevisions", "managed.revision-audit", policy ?? source.ReadPolicy, cancellationToken, c =>
        {
            var older = new HashSet<int>();
            for (int revision = source.Revisions.Count - 1; revision >= 0; revision--)
            {
                var changes = new List<PdfRevisionChange>();
                var signatures = new List<PdfSignatureObservation>();
                c.Consume(PdfResourceKind.ExpandedBytes, 256);
                foreach (var entry in source.Revisions[revision].Entries)
                {
                    c.Checkpoint();
                    c.Consume(PdfResourceKind.Objects, 1);
                    c.Consume(PdfResourceKind.ExpandedBytes, 256);
                    changes.Add(new(entry.Id, entry.Kind, older.Contains(entry.Id.Number)));
                    older.Add(entry.Id.Number);
                    if (entry.Kind == PdfCrossReferenceKind.Free)
                        continue;
                    var value = source.Resolve(entry.Id, revision);
                    if (value.Kind != PdfValueKind.Dictionary || !value.Entries.ContainsKey("ByteRange"))
                        continue;
                    try
                    {
                        var validated = Ranges(source, entry.Id, value, c, revision);
                        using var bytes = c.BorrowExpandedInput(new RangeStream(source.Source, validated.Ranges));
                        using var cms = c.BorrowExpandedInput(new MemoryStream(validated.Cms, false));
                        var valid = verifier.Verify(bytes, cms, c);
                        c.Checkpoint();
                        signatures.Add(new(entry.Id, validated.Ranges, valid, validated.Ranges[2] + validated.Ranges[3]));
                    }
                    catch (PdfException failure) when (failure.Diagnostic.Code == "Signature.UnsupportedProfile" || failure.Diagnostic.Code == "Signature.ByteRangeProfile")
                    {
                        c.Checkpoint();
                        signatures.Add(new(entry.Id, Array.Empty<long>(), null, 0, failure.Diagnostic.Code));
                    }
                }
                records.Add(new(revision, source.Revisions[revision].StartXref, changes, signatures));
            }
        }, PdfCapability.Signatures);
        records.Reverse();
        return records.AsReadOnly();
    }
    /// <summary>Preflights an ordinary full/incremental write or an exact pre-created-field signing request. It does not replace final self-verification or validate existing trust.</summary>
    public static PdfSignedEditPlan PreflightEdit(PdfSyntaxDocument source, PdfWriteOptions writeOptions, PdfSignatureOptions? signing = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || writeOptions == null)
            throw new ArgumentNullException(source == null ? nameof(source) : nameof(writeOptions));
        PdfSignedEditPlan? plan = null;
        PdfFont.Run("PreflightSignedEdit", "managed.revision-audit", policy ?? source.ReadPolicy, cancellationToken, c =>
        {
            var protection = PdfSignaturePolicy.Protection(source, signing?.FieldName, c);
            if (signing != null)
            {
                string? rejection = protection.NoChanges || protection.Locked ? "Signature.CertificationPermission" : signing.Certification != PdfCertificationPermission.None && (source.HasSignatureByteRanges || protection.Protected) ? "Signature.CertificationOrder" : null;
                plan = new(rejection == null, rejection == null, rejection ?? "Signature.SigningPolicyAllowed");
            }
            else
            {
                bool prefix = writeOptions.Mode == PdfWriteMode.Incremental;
                if (protection.Protected && !writeOptions.AllowSignatureInvalidation)
                    plan = new(false, false, "Signature.CertifiedEditProfile");
                else if (source.HasSignatureByteRanges && !prefix && !writeOptions.AllowSignatureInvalidation)
                    plan = new(false, false, "Signature.FullRewriteInvalidates");
                else
                    plan = new(true, prefix, prefix ? "Signature.LaterEditsNotValidated" : "Signature.FullRewriteInvalidates");
            }
        }, PdfCapability.Signatures);
        return plan!;
    }
}
