namespace Fluent.Pdf;
/// <summary>Stable passive finding with physical provenance and an explicitly bounded optional fix.</summary>
public sealed class PdfPreflightFinding
{
    internal PdfPreflightFinding(string code, string location, PdfDiagnosticSeverity severity, string? fix, string? loss)
    {
        Code = code;
        Location = location;
        Severity = severity;
        RemediationCode = fix;
        LossDescription = loss;
    }

    /// <summary>Gets stable finding code.</summary>
    public string Code { get; }
    /// <summary>Gets physical object/key provenance.</summary>
    public string Location { get; }
    /// <summary>Gets finding severity, not a conformance verdict.</summary>
    public PdfDiagnosticSeverity Severity { get; }
    /// <summary>Gets an available explicit fix code, or null when no fix is implemented.</summary>
    public string? RemediationCode { get; }
    /// <summary>Gets the fixed profile's declared loss, or null when no fix is implemented.</summary>
    public string? LossDescription { get; }
}

/// <summary>Immutable read-only bounded structural/security inventory. Absence of findings is not standards compliance.</summary>
public sealed class PdfPreflightReport
{
    internal PdfPreflightReport(string hash, List<PdfPreflightFinding> findings, PdfProviderRegistry providers, bool signatures, PdfOperationReport? report)
    {
        SourceSha256 = hash;
        Findings = Array.AsReadOnly(findings.ToArray());
        AvailableCapabilities = Array.AsReadOnly(providers.Capabilities.ToArray());
        HasSignatures = signatures;
        OperationReport = report!;
    }

    /// <summary>Gets the exact captured input SHA256.</summary>
    public string SourceSha256 { get; }
    /// <summary>Gets immutable stable findings.</summary>
    public IReadOnlyList<PdfPreflightFinding> Findings { get; }
    /// <summary>Gets only explicitly supplied provider capability declarations, not qualification or implicit discovery.</summary>
    public IReadOnlyList<PdfCapability> AvailableCapabilities { get; }
    /// <summary>Gets whether signature byte-range structures are present; validity is not inferred.</summary>
    public bool HasSignatures { get; }
    /// <summary>Gets false: this structural source profile does not replace a complete independent conformance validator.</summary>
    public bool ConformanceValidated => false;
    /// <summary>Gets the complete shared operation report.</summary>
    public PdfOperationReport OperationReport { get; internal set; }
}

/// <summary>Explicit immutable fix selection bound to exact inspected bytes. No automatic or font/renderer-based repairs occur.</summary>
public sealed class PdfRemediationPlan
{
    /// <summary>Selects the implemented RemoveBlockedActions fix only when present in the source report. It removes all actions/content blocked by the documented default action-retention policy; it is not secure erasure. Signature invalidation needs separate explicit authority and permissive loss policy.</summary>
    public PdfRemediationPlan(PdfPreflightReport source, IEnumerable<string> fixes, bool allowSignatureInvalidation = false)
    {
        if (source == null || fixes == null)
            throw new ArgumentNullException(nameof(source));
        var selected = new List<string>();
        foreach (string fix in fixes)
        {
            if (selected.Count == 1 || fix != "RemoveBlockedActions" || selected.Contains(fix) || !source.Findings.Any(f => f.RemediationCode == fix))
                throw new ArgumentException("Select each implemented applicable fix exactly once.", nameof(fixes));
            selected.Add(fix);
        }

        if (selected.Count == 0)
            throw new ArgumentException("An explicit nonempty applicable fix selection is required.", nameof(fixes));
        SourceSha256 = source.SourceSha256;
        Fixes = Array.AsReadOnly(selected.ToArray());
        AllowSignatureInvalidation = allowSignatureInvalidation;
    }

    /// <summary>Gets exact inspected source SHA256.</summary>
    public string SourceSha256 { get; }
    /// <summary>Gets explicit stable fix codes.</summary>
    public IReadOnlyList<string> Fixes { get; }
    /// <summary>Gets explicit authority for invalidating source signature bytes.</summary>
    public bool AllowSignatureInvalidation { get; }
}

/// <summary>Immutable source/output binding and before/after findings after verified explicit remediation.</summary>
public sealed class PdfRemediationAudit
{
    internal PdfRemediationAudit(PdfPreflightReport before, PdfPreflightReport after, PdfRemediationPlan plan, PdfOperationReport? report)
    {
        Before = before;
        After = after;
        Plan = plan;
        OperationReport = report!;
    }

    /// <summary>Gets exact source binding and findings.</summary>
    public PdfPreflightReport Before { get; }
    /// <summary>Gets exact output binding and post-fix findings; other unresolved findings remain visible.</summary>
    public PdfPreflightReport After { get; }
    /// <summary>Gets the exact explicit plan that was applied.</summary>
    public PdfRemediationPlan Plan { get; }
    /// <summary>Gets complete rewrite/post-validation diagnostics.</summary>
    public PdfOperationReport OperationReport { get; internal set; }
}
