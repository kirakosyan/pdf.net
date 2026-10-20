using System.IO;
using System.Threading;

namespace Fluent.Pdf;

/// <summary>Explicit certificate revocation observation, never inferred from offline success.</summary>
public enum PdfRevocationStatus
{
    /// <summary>No fresh authoritative status is available.</summary>
    Unknown,
    /// <summary>The explicitly configured authority reports a fresh good status.</summary>
    Good,
    /// <summary>The explicitly configured authority reports revocation.</summary>
    Revoked
}

/// <summary>A caller-selected trust instant. No machine clock or ambient trust store is consulted.</summary>
public sealed class PdfSignatureTrustOptions
{
    /// <summary>Captures an exact evaluation instant.</summary>
    public PdfSignatureTrustOptions(DateTimeOffset evaluationTime) => EvaluationTime = evaluationTime;
    /// <summary>Gets the evaluation instant; a PDF signing-time string does not supply this authority.</summary>
    public DateTimeOffset EvaluationTime
    {
        get;
    }
}

/// <summary>Detached identity observations separate from mathematical CMS integrity and document edit permissions.</summary>
public sealed class PdfSignatureTrustDecision
{
    /// <summary>Captures explicit identity pin/chain trust, validity interval and revocation status.</summary>
    public PdfSignatureTrustDecision(bool identityTrusted, bool timeValid, PdfRevocationStatus revocation, string certificateSha256)
    {
        if (!Enum.IsDefined(typeof(PdfRevocationStatus), revocation))
            throw new ArgumentOutOfRangeException(nameof(revocation));
        if (certificateSha256 == null || certificateSha256.Length != 64 || certificateSha256.Any(ch => !(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f')))
            throw new ArgumentException("An exact lowercase certificate SHA256 is required.", nameof(certificateSha256));
        IdentityTrusted = identityTrusted;
        TimeValid = timeValid;
        Revocation = revocation;
        CertificateSha256 = certificateSha256;
    }
    /// <summary>Gets explicit identity trust.</summary>
    public bool IdentityTrusted
    {
        get;
    }
    /// <summary>Gets whether the certificate is valid at the requested instant.</summary>
    public bool TimeValid
    {
        get;
    }
    /// <summary>Gets revocation status, including unknown/offline.</summary>
    public PdfRevocationStatus Revocation
    {
        get;
    }
    /// <summary>Gets the exact signer certificate fingerprint.</summary>
    public string CertificateSha256
    {
        get;
    }
    /// <summary>Gets whether all identity, time and fresh revocation checks pass.</summary>
    public bool Accepted => IdentityTrusted && TimeValid && Revocation == PdfRevocationStatus.Good;
}

/// <summary>Explicit optional trust service. It receives the exact CMS already selected by the range validator; it must not infer trust from mathematical validity.</summary>
public interface IPdfSignatureTrustProvider : IPdfProvider
{
    /// <summary>Evaluates the embedded signer under explicit trust/time/network authority and the shared budget.</summary>
    PdfSignatureTrustDecision Evaluate(Stream cms, PdfSignatureTrustOptions options, PdfOperationContext context);
}

/// <summary>Optional caller-configured certificate status authority; unknown must remain observable.</summary>
public interface IPdfCertificateStatusProvider
{
    /// <summary>Returns authoritative status for the exact borrowed DER certificate at the explicit instant. Implementations validate freshness and responses under the shared context.</summary>
    PdfRevocationStatus Check(Stream certificate, DateTimeOffset evaluationTime, PdfOperationContext context);
}

/// <summary>One signature's exact signed extent, integrity and separate identity decision.</summary>
public sealed class PdfSignatureVerification
{
    internal PdfSignatureVerification(PdfSignatureObservation signature, PdfSignatureTrustDecision? trust, long length)
    {
        Signature = signature;
        Trust = trust;
        LaterByteCount = signature.SignedRevisionLength == 0 ? 0 : length - signature.SignedRevisionLength;
    }
    /// <summary>Gets structural/range and mathematical observations.</summary>
    public PdfSignatureObservation Signature
    {
        get;
    }
    /// <summary>Gets explicit trust, or null when no supported CMS could be verified.</summary>
    public PdfSignatureTrustDecision? Trust
    {
        get;
    }
    /// <summary>Gets bytes after the signed revision. Their presence does not establish permission or tampering.</summary>
    public long LaterByteCount
    {
        get;
    }
    /// <summary>Gets complete integrity and identity acceptance; does not authorize later modifications.</summary>
    public bool Accepted => Signature.CryptographicallyValid == true && Trust?.Accepted == true;
}

public static partial class PdfSignatures
{
    /// <summary>Verifies active detached signatures with an explicitly registered trust service under one context. No revocation, chain, timestamp or later-edit success is invented.</summary>
    public static IReadOnlyList<PdfSignatureVerification> Verify(PdfSyntaxDocument source, PdfProviderRegistry providers, PdfSignatureTrustOptions options, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || providers == null || options == null)
            throw new ArgumentNullException(source == null ? nameof(source) : providers == null ? nameof(providers) : nameof(options));
        var integrity = providers.GetRequired<IPdfSignatureProvider>(PdfCapability.Signatures);
        var trust = providers.GetRequired<IPdfSignatureTrustProvider>(PdfCapability.SignatureTrust);
        var results = new List<PdfSignatureVerification>();
        PdfFont.Run("VerifySignatures", "managed.signature-verification", policy ?? source.ReadPolicy, cancellationToken, c =>
        {
            using var edit = new PdfTransaction(source, c.Policy);
            foreach (var id in edit.ActiveIds(c))
            {
                var value = edit.Resolve(id);
                if (value.Kind != PdfValueKind.Dictionary || !value.Entries.ContainsKey("ByteRange"))
                    continue;
                c.Consume(PdfResourceKind.Objects, 1);
                c.Consume(PdfResourceKind.ExpandedBytes, 512);
                try
                {
                    var validated = Ranges(source, id, value, c);
                    bool valid;
                    using (var bytes = c.BorrowExpandedInput(new RangeStream(source.Source, validated.Ranges)))
                    using (var cms = c.BorrowExpandedInput(new MemoryStream(validated.Cms, false)))
                        valid = integrity.Verify(bytes, cms, c);
                    c.Checkpoint();
                    PdfSignatureTrustDecision? decision = null;
                    string? diagnostic = null;
                    if (valid)
                    {
                        try
                        {
                            using var cms = c.BorrowExpandedInput(new MemoryStream(validated.Cms, false));
                            decision = trust.Evaluate(cms, options, c) ?? throw Error(c, "TrustResult", "The trust provider returned no identity decision.");
                        }
                        catch (PdfException failure) when (failure.Diagnostic.Code == "Signature.UnsupportedProfile")
                        {
                            c.Checkpoint();
                            diagnostic = failure.Diagnostic.Code;
                        }
                        c.Checkpoint();
                    }
                    results.Add(new(new(id, validated.Ranges, valid, validated.Ranges[2] + validated.Ranges[3], diagnostic), decision, source.Source.Length));
                }
                catch (PdfException failure) when (failure.Diagnostic.Code == "Signature.UnsupportedProfile" || failure.Diagnostic.Code == "Signature.ByteRangeProfile")
                {
                    c.Checkpoint();
                    results.Add(new(new(id, Array.Empty<long>(), null, 0, failure.Diagnostic.Code), null, source.Source.Length));
                }
            }
        }, PdfCapability.Signatures);
        return results.AsReadOnly();
    }
}
