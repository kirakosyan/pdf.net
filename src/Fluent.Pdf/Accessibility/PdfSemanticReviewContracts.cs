namespace Fluent.Pdf;

/// <summary>The explicit semantic change proposed for human review; none establishes accessibility conformance.</summary>
public enum PdfSemanticFixKind
{
    /// <summary>Change an existing element's explicit semantics or child order.</summary>
    Element,
    /// <summary>Map one custom PDF1 structure role directly to a standard role.</summary>
    RoleMapping,
    /// <summary>Apply a complete explicit heading/list/table/content structure plan.</summary>
    StructurePlan
}

/// <summary>An immutable caller-proposed semantic change. Reasons may describe inference; values are never accepted automatically.</summary>
public sealed class PdfSemanticSuggestion
{
    private PdfSemanticSuggestion(string id, string reason, PdfSemanticFixKind kind)
    {
        Id = Guard.Text(id, nameof(id), 128);
        Reason = Guard.Text(reason, nameof(reason), 4096);
        Kind = kind;
    }
    /// <summary>Proposes explicit element metadata or an exact child permutation; null fields retain source values.</summary>
    public static PdfSemanticSuggestion Element(string id, PdfObjectId elementId, string reason, string? role = null, string? language = null, string? alternativeText = null, string? actualText = null, int[]? childOrder = null)
    {
        if (role != null) PdfStructureRules.Role(role);
        PdfStructureRules.Language(language);
        PdfStructureRules.Text(alternativeText, 1000000, nameof(alternativeText));
        PdfStructureRules.Text(actualText, 1000000, nameof(actualText));
        if (childOrder != null && childOrder.Length > 100000) throw new ArgumentException("A bounded complete permutation is required.", nameof(childOrder));
        if (role == null && language == null && alternativeText == null && actualText == null && childOrder == null) throw new ArgumentException("At least one explicit semantic change is required.");
        return new(id, reason, PdfSemanticFixKind.Element) { ElementId = elementId, Role = role, Language = language, AlternativeText = alternativeText, ActualText = actualText, ChildOrder = childOrder == null ? null : Array.AsReadOnly((int[])childOrder.Clone()) };
    }
    /// <summary>Proposes a direct custom-to-standard role map for the PDF1 profile; standard roles cannot be remapped.</summary>
    public static PdfSemanticSuggestion RoleMapping(string id, string customRole, string standardRole, string reason)
    {
        Guard.Text(customRole, nameof(customRole), 128);
        PdfValue.Name(customRole);
        PdfStructureRules.Role(standardRole);
        if (PdfStructureRules.StandardRole(customRole)) throw new ArgumentException("A standard role cannot be remapped.", nameof(customRole));
        return new(id, reason, PdfSemanticFixKind.RoleMapping) { CustomRole = customRole, Role = standardRole };
    }
    /// <summary>Proposes a complete author-supplied structure plan. It must be the only suggestion because it replaces element identities.</summary>
    public static PdfSemanticSuggestion Structure(string id, PdfStructurePlan plan, string reason)
        => new(id, reason, PdfSemanticFixKind.StructurePlan) { StructurePlan = plan ?? throw new ArgumentNullException(nameof(plan)) };
    /// <summary>Gets the caller's unique bounded suggestion identity.</summary>
    public string Id { get; }
    /// <summary>Gets the explicit reason or inference explanation for review.</summary>
    public string Reason { get; }
    /// <summary>Gets the exact proposed change category.</summary>
    public PdfSemanticFixKind Kind { get; }
    /// <summary>Gets the source element identity when applicable.</summary>
    public PdfObjectId? ElementId { get; private set; }
    /// <summary>Gets the proposed standard role.</summary>
    public string? Role { get; private set; }
    /// <summary>Gets the custom role to map, when applicable.</summary>
    public string? CustomRole { get; private set; }
    /// <summary>Gets the proposed language.</summary>
    public string? Language { get; private set; }
    /// <summary>Gets proposed alternative text requiring explicit reviewer acceptance.</summary>
    public string? AlternativeText { get; private set; }
    /// <summary>Gets proposed logical replacement text requiring explicit reviewer acceptance.</summary>
    public string? ActualText { get; private set; }
    /// <summary>Gets the proposed exact permutation; null retains existing order.</summary>
    public IReadOnlyList<int>? ChildOrder { get; private set; }
    /// <summary>Gets the explicit complete structure plan when applicable.</summary>
    public PdfStructurePlan? StructurePlan { get; private set; }
}

/// <summary>A detached suggestion inventory bound to exact source bytes. Preparation is read-only.</summary>
public sealed class PdfSemanticReviewPlan
{
    internal PdfSemanticReviewPlan(string hash, PdfAccessibilityProfile profile, PdfSemanticSuggestion[] suggestions, PdfAccessibilityFinding[] findings, PdfOperationReport report)
    { SourceSha256 = hash; Profile = profile; Suggestions = Array.AsReadOnly(suggestions); Findings = Array.AsReadOnly(findings); OperationReport = report; }
    /// <summary>Gets the exact input digest required when applying decisions.</summary>
    public string SourceSha256 { get; }
    /// <summary>Gets the requested accessibility profile without a conformance claim.</summary>
    public PdfAccessibilityProfile Profile { get; }
    /// <summary>Gets immutable unaccepted suggestions in application order.</summary>
    public IReadOnlyList<PdfSemanticSuggestion> Suggestions { get; }
    /// <summary>Gets incomplete automated source findings.</summary>
    public IReadOnlyList<PdfAccessibilityFinding> Findings { get; }
    /// <summary>Gets true; suggestion preparation never verifies human semantics.</summary>
    public bool HumanReviewRequired => true;
    /// <summary>Gets preparation budgets and diagnostics.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>An explicit caller-asserted reviewer decision. This record does not authenticate a person or certify conformance.</summary>
public sealed class PdfSemanticReviewDecision
{
    /// <summary>Captures a nonempty reviewer, note, suggestion ID and accept/reject decision.</summary>
    public PdfSemanticReviewDecision(string suggestionId, bool accept, string reviewer, string note)
    { SuggestionId = Guard.Text(suggestionId, nameof(suggestionId), 128); Accept = accept; Reviewer = Guard.Text(reviewer, nameof(reviewer), 256); Note = Guard.Text(note, nameof(note), 4096); }
    /// <summary>Gets the selected suggestion identity.</summary>
    public string SuggestionId { get; }
    /// <summary>Gets whether this exact proposed change was accepted.</summary>
    public bool Accept { get; }
    /// <summary>Gets caller-supplied reviewer identity.</summary>
    public string Reviewer { get; }
    /// <summary>Gets the caller's review rationale.</summary>
    public string Note { get; }
}

/// <summary>A detached ordered ledger entry with immutable source/output binding and post-fix findings.</summary>
public sealed class PdfSemanticReviewEntry
{
    internal PdfSemanticReviewEntry(PdfSemanticSuggestion suggestion, PdfSemanticReviewDecision? decision, string before, string after, PdfObjectId[] affected, PdfAccessibilityFinding[] findings)
    { Suggestion = suggestion; Decision = decision; BeforeSha256 = before; AfterSha256 = after; AffectedObjects = Array.AsReadOnly(affected); Findings = Array.AsReadOnly(findings); }
    /// <summary>Gets the exact proposed semantics and inference explanation.</summary>
    public PdfSemanticSuggestion Suggestion { get; }
    /// <summary>Gets the reviewer decision, or null for an unreviewed suggestion.</summary>
    public PdfSemanticReviewDecision? Decision { get; }
    /// <summary>Gets true only after an accepted fix and its structural checks succeeded.</summary>
    public bool Applied => Decision?.Accept == true;
    /// <summary>Gets the exact bytes before this ledger step.</summary>
    public string BeforeSha256 { get; }
    /// <summary>Gets the exact bytes after this step; unchanged for rejected/pending suggestions.</summary>
    public string AfterSha256 { get; }
    /// <summary>Gets affected source or newly authored structure identities.</summary>
    public IReadOnlyList<PdfObjectId> AffectedObjects { get; }
    /// <summary>Gets incomplete automated findings after this step.</summary>
    public IReadOnlyList<PdfAccessibilityFinding> Findings { get; }
}

/// <summary>An ordered semantic review ledger. Applied fixes never imply complete accessibility or human review.</summary>
public sealed class PdfSemanticReviewAudit
{
    internal PdfSemanticReviewAudit(string before, string after, PdfAccessibilityProfile profile, PdfSemanticReviewEntry[] entries, PdfOperationReport report)
    { BeforeSha256 = before; AfterSha256 = after; Profile = profile; Entries = Array.AsReadOnly(entries); OperationReport = report; }
    /// <summary>Gets original input binding.</summary>
    public string BeforeSha256 { get; }
    /// <summary>Gets the exact delivered output binding.</summary>
    public string AfterSha256 { get; }
    /// <summary>Gets the selected profile, without compliance inference.</summary>
    public PdfAccessibilityProfile Profile { get; }
    /// <summary>Gets every suggestion's outcome in plan order, including pending and rejected ones.</summary>
    public IReadOnlyList<PdfSemanticReviewEntry> Entries { get; }
    /// <summary>Gets true; complete independent validation and human/screen-reader review remain separate.</summary>
    public bool HumanReviewRequired => true;
    /// <summary>Gets the shared operation report.</summary>
    public PdfOperationReport OperationReport { get; }
}
