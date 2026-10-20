using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Complete independent exact-input PDF/UA observations. It does not establish human semantic review or SDK production qualification.</summary>
public sealed class PdfAccessibilityValidation
{
    /// <summary>Captures exact profile/digest and complete independent findings. Compliance requires completed validation and zero findings.</summary>
    public PdfAccessibilityValidation(PdfAccessibilityProfile profile, string sourceSha256, bool completed, bool isCompliant, PdfAccessibilityFinding[] findings)
    {
        PdfStructureRules.Profile(profile);
        Digest(sourceSha256);
        if (findings == null || findings.Length > 10000 || findings.Any(f => f == null))
            throw new ArgumentException("A bounded complete finding inventory is required.", nameof(findings));
        if (isCompliant && (!completed || findings.Length != 0))
            throw new ArgumentException("Compliance requires complete validation without findings.", nameof(isCompliant));
        Profile = profile;
        SourceSha256 = sourceSha256;
        Completed = completed;
        IsCompliant = isCompliant;
        Findings = Array.AsReadOnly((PdfAccessibilityFinding[])findings.Clone());
    }

    /// <summary>Gets the exact validated profile.</summary>
    public PdfAccessibilityProfile Profile { get; }
    /// <summary>Gets the lowercase exact-input SHA256.</summary>
    public string SourceSha256 { get; }
    /// <summary>Gets whether complete independent validation finished.</summary>
    public bool Completed { get; }
    /// <summary>Gets the independent automated conformance observation; human review remains separate.</summary>
    public bool IsCompliant { get; }
    /// <summary>Gets the complete independent finding inventory.</summary>
    public IReadOnlyList<PdfAccessibilityFinding> Findings { get; }
    /// <summary>Gets separately supplied exact-input human review, or null; automated success never invents review.</summary>
    public PdfAccessibilityHumanReview? HumanReview { get; internal set; }
    /// <summary>Gets root operation report after public validation; null for a provider-only result.</summary>
    public PdfOperationReport? OperationReport { get; internal set; }

    internal static void Digest(string value)
    {
        if (value == null || value.Length != 64 || value.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f')))
            throw new ArgumentException("An exact lowercase SHA256 is required.", nameof(value));
    }
}

/// <summary>An explicit author/reviewer attestation bound to exact bytes, kept separate from all automated checks.</summary>
public sealed class PdfAccessibilityHumanReview
{
    /// <summary>Records explicit reviewer/report identity, profile/digest, reading-order review and semantic descriptions/table/form review. False values remain observations, never automatic approvals.</summary>
    public PdfAccessibilityHumanReview(PdfAccessibilityProfile profile, string sourceSha256, string reviewer, string reportId, bool readingOrderReviewed, bool semanticsReviewed)
    {
        PdfStructureRules.Profile(profile);
        PdfAccessibilityValidation.Digest(sourceSha256);
        Profile = profile;
        SourceSha256 = sourceSha256;
        Reviewer = Guard.Text(reviewer, nameof(reviewer), 256);
        ReportId = Guard.Text(reportId, nameof(reportId), 4096);
        ReadingOrderReviewed = readingOrderReviewed;
        SemanticsReviewed = semanticsReviewed;
    }

    /// <summary>Gets the explicitly reviewed profile.</summary>
    public PdfAccessibilityProfile Profile { get; }
    /// <summary>Gets the exact reviewed input digest.</summary>
    public string SourceSha256 { get; }
    /// <summary>Gets explicit reviewer identity.</summary>
    public string Reviewer { get; }
    /// <summary>Gets retained human report identity.</summary>
    public string ReportId { get; }
    /// <summary>Gets the author's explicit reading-order review observation.</summary>
    public bool ReadingOrderReviewed { get; }
    /// <summary>Gets the author's explicit description/table/form semantic review observation.</summary>
    public bool SemanticsReviewed { get; }
}

/// <summary>An explicitly supplied complete independent PDF/UA validator; metadata-only implementations may not declare compliance.</summary>
public interface IPdfAccessibilityValidator : IPdfProvider
{
    /// <summary>Validates the complete requested standard against borrowed exact input under the mandatory context. No engine/model/network discovery is implicit.</summary>
    PdfAccessibilityValidation Validate(PdfAccessibilityProfile profile, Stream input, PdfOperationContext context);
}

public static partial class PdfAccessibility
{
    /// <summary>Runs complete independent exact-input validation, optionally attaching separately supplied matching human review. Caller input stays open; provider success never bypasses latched failures.</summary>
    public static PdfAccessibilityValidation Validate(Stream input, PdfAccessibilityProfile profile, PdfProviderRegistry providers, PdfAccessibilityHumanReview? humanReview = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("Readable borrowed input is required.", nameof(input));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        PdfStructureRules.Profile(profile);
        var validator = providers.GetRequired<IPdfAccessibilityValidator>(PdfCapability.ValidateAccessibility);
        PdfAccessibilityValidation? result = null;
        var report = PdfFont.Run("ValidateAccessibility", providers.GetIdentifier(PdfCapability.ValidateAccessibility), policy, cancellationToken, c =>
        {
            using var lease = new BorrowedStream(input, true, c);
            var bytes = PdfByteStore.Read(lease, c);
            c.Consume(PdfResourceKind.ExpandedBytes, 65536 + 4096);
            var buffer = new byte[65536];
            string digest;
            using (var hash = SHA256.Create())
            using (var source = bytes.Open(0, bytes.Length))
            {
                int count;
                while ((count = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    c.Checkpoint();
                    hash.TransformBlock(buffer, 0, count, buffer, 0);
                }

                hash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
                digest = BitConverter.ToString(hash.Hash!).Replace("-", "").ToLowerInvariant();
            }

            if (humanReview != null && (humanReview.Profile != profile || humanReview.SourceSha256 != digest))
                throw PdfWriterCore.Error(c, "Accessibility.HumanReviewBinding", "Human review belongs to another profile or exact input.");
            using var raw = bytes.Open(0, bytes.Length);
            using var borrowed = c.BorrowExpandedInput(raw);
            var observation = validator.Validate(profile, borrowed, c);
            c.Checkpoint();
            if (observation == null || observation.Profile != profile || observation.SourceSha256 != digest)
                throw PdfWriterCore.Error(c, "Accessibility.ValidatorBinding", "The validator returned another profile or exact input digest.");
            c.Consume(PdfResourceKind.Objects, observation.Findings.Count + 1L);
            c.Consume(PdfResourceKind.ExpandedBytes, observation.Findings.Count * 16L + 256);
            foreach (var finding in observation.Findings)
                c.Consume(PdfResourceKind.ExpandedBytes, (finding.Rule.Length + finding.Location.Length + finding.Message.Length) * 2L + 128);
            result = new(profile, digest, observation.Completed, observation.IsCompliant, observation.Findings.ToArray())
            {
                HumanReview = humanReview
            };
        }, PdfCapability.ValidateAccessibility);
        result!.OperationReport = report;
        return result;
    }
}
