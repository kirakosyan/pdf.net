using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class SemanticReviewTests
{
    internal static byte[] Tagged(PdfAccessibilityProfile profile = PdfAccessibilityProfile.Ua1)
    {
        using var source = Pdf.Open(new MemoryStream(AccessibilityTests.Original()));
        using var tagged = PdfAccessibility.Author(source.CreateSnapshot(), AccessibilityTests.Plan(profile));
        using var output = new MemoryStream(); tagged.Save(output); return output.ToArray();
    }
    private static PdfAccessibilityInspection Inspect(byte[] bytes)
    { using var document = Pdf.Open(new MemoryStream(bytes)); return PdfAccessibility.Inspect(document.CreateSnapshot()); }
    private static string Hash(byte[] bytes)
    { using var hash = SHA256.Create(); return string.Concat(hash.ComputeHash(bytes).Select(b => b.ToString("x2", System.Globalization.CultureInfo.InvariantCulture))); }
    private static string Artifact(string name)
    { string path = Path.Combine(AppContext.BaseDirectory, "artifacts", "semantic-review"); Directory.CreateDirectory(path); return Path.Combine(path, name); }
    [Fact]
    public void PreparationIsPassiveBoundAndNeverConfirmsHumanSemantics()
    {
        byte[] bytes = Tagged(); var root = Inspect(bytes).Elements[0]; int[] order = { 1, 0 };
        var suggestion = PdfSemanticSuggestion.Element("order", root.ObjectId, "Inferred order needs review", childOrder: order);
        order[0] = 0;
        using var input = new MemoryStream(bytes); var plan = PdfAccessibility.PrepareReview(input, new[] { suggestion });
        Assert.True(input.CanRead); Assert.True(plan.HumanReviewRequired); Assert.Equal(Hash(bytes), plan.SourceSha256);
        Assert.Equal(new[] { 1, 0 }, Assert.Single(plan.Suggestions).ChildOrder);
        Assert.Equal(new[] { 0, 1 }, Inspect(bytes).Content.Select(c => c.Mcid));
    }
    [Fact]
    public void OrderedDecisionsRetainPendingRejectedAndRevalidatedAcceptedSteps()
    {
        byte[] bytes = Tagged(); var root = Inspect(bytes).Elements[0];
        var suggestions = new[] {
            PdfSemanticSuggestion.Element("order", root.ObjectId, "Inferred order", childOrder: new[] { 1, 0 }),
            PdfSemanticSuggestion.Element("language", root.ObjectId, "Explicit author language", language: "hy"),
            PdfSemanticSuggestion.Element("rejected", root.ObjectId, "Proposed replacement", actualText: "rejected text"),
            PdfSemanticSuggestion.Element("pending", root.ObjectId, "Unreviewed description", alternativeText: "pending text") };
        var plan = PdfAccessibility.PrepareReview(new MemoryStream(bytes), suggestions);
        using var output = new MemoryStream();
        var audit = PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, new[] { new PdfSemanticReviewDecision("language", true, "fixture-reviewer", "language decision"), new PdfSemanticReviewDecision("order", true, "fixture-reviewer", "order decision"), new PdfSemanticReviewDecision("rejected", false, "fixture-reviewer", "rejected decision") });
        Assert.Equal(suggestions.Select(s => s.Id), audit.Entries.Select(e => e.Suggestion.Id));
        Assert.Equal(new[] { true, true, false, false }, audit.Entries.Select(e => e.Applied)); Assert.Null(audit.Entries[3].Decision);
        Assert.Equal(audit.BeforeSha256, audit.Entries[0].BeforeSha256); Assert.Equal(audit.Entries[0].AfterSha256, audit.Entries[1].BeforeSha256);
        Assert.Equal(audit.AfterSha256, audit.Entries[2].AfterSha256); Assert.Equal(audit.AfterSha256, audit.Entries[3].AfterSha256);
        Assert.Equal(Hash(output.ToArray()), audit.AfterSha256); Assert.True(audit.HumanReviewRequired);
        var updated = Inspect(output.ToArray()); Assert.Equal(new[] { 1, 0 }, updated.Content.Select(c => c.Mcid)); Assert.Equal("hy", updated.Elements[0].Language);
        Assert.Null(updated.Elements[0].ActualText); Assert.Null(updated.Elements[0].AlternativeText);
        Assert.All(audit.Entries.Take(2), e => Assert.Contains(e.Findings, f => f.Rule == "Accessibility.IndependentValidationRequired"));
        using var ledger = new MemoryStream(); PdfAccessibility.WriteReview(audit, ledger);
        string json = Encoding.UTF8.GetString(ledger.ToArray()); Assert.Contains("\"conformanceVerified\":false", json); Assert.Contains("\"pending\"", json);
        File.WriteAllBytes(Artifact("original.pdf"), bytes); File.WriteAllBytes(Artifact("reviewed.pdf"), output.ToArray()); File.WriteAllBytes(Artifact("ledger.json"), ledger.ToArray());
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void UnacceptedSuggestionsPreserveExactBytes(bool rejected)
    {
        byte[] bytes = Tagged(); var root = Inspect(bytes).Elements[0]; var plan = PdfAccessibility.PrepareReview(new MemoryStream(bytes), new[] { PdfSemanticSuggestion.Element("order", root.ObjectId, "Unverified inference", childOrder: new[] { 1, 0 }) });
        using var output = new MemoryStream();
        var audit = PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, rejected ? new[] { new PdfSemanticReviewDecision("order", false, "fixture-reviewer", "Keep original order") } : Array.Empty<PdfSemanticReviewDecision>());
        Assert.Equal(bytes, output.ToArray()); Assert.Equal(audit.BeforeSha256, audit.AfterSha256); Assert.False(Assert.Single(audit.Entries).Applied);
    }
    [Fact]
    public void StaleDuplicateAndUnrelatedDecisionsRejectBeforeOutput()
    {
        byte[] bytes = Tagged(); var root = Inspect(bytes).Elements[0]; var plan = PdfAccessibility.PrepareReview(new MemoryStream(bytes), new[] { PdfSemanticSuggestion.Element("order", root.ObjectId, "Inference", childOrder: new[] { 1, 0 }) });
        using var output = new MemoryStream();
        Assert.Equal("Accessibility.Review.StalePlan", Assert.Throws<PdfException>(() => PdfAccessibility.ApplyReview(new MemoryStream(bytes.Concat(new byte[] { 10 }).ToArray()), output, plan, Array.Empty<PdfSemanticReviewDecision>())).Diagnostic.Code);
        var decision = new PdfSemanticReviewDecision("order", true, "fixture", "decision");
        Assert.Equal("Accessibility.Review.Decision", Assert.Throws<PdfException>(() => PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, new[] { decision, decision })).Diagnostic.Code);
        Assert.Equal("Accessibility.Review.Decision", Assert.Throws<PdfException>(() => PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, new[] { new PdfSemanticReviewDecision("unknown", true, "fixture", "decision") })).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }
    [Fact]
    public void InvalidLaterFixExposesNeitherEarlierOutputNorAtomicReplacement()
    {
        byte[] bytes = Tagged(); var root = Inspect(bytes).Elements[0]; var plan = PdfAccessibility.PrepareReview(new MemoryStream(bytes), new[] { PdfSemanticSuggestion.Element("valid", root.ObjectId, "Language", language: "fr"), PdfSemanticSuggestion.Element("invalid", root.ObjectId, "Invalid order", childOrder: new[] { 0, 0 }) });
        var decisions = plan.Suggestions.Select(s => new PdfSemanticReviewDecision(s.Id, true, "fixture", "decision")).ToArray();
        using var output = new MemoryStream(); Assert.Equal("Accessibility.ChildOrder", Assert.Throws<PdfException>(() => PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, decisions)).Diagnostic.Code); Assert.Equal(0, output.Length);
        string path = Artifact(Guid.NewGuid().ToString("N") + ".pdf"); File.WriteAllBytes(path, bytes);
        Assert.Throws<PdfException>(() => PdfAccessibility.ApplyReviewFile(new MemoryStream(bytes), path, plan, decisions)); Assert.Equal(bytes, File.ReadAllBytes(path));
    }
    private static byte[] CustomRole(string? mapping = null)
    {
        byte[] bytes = Tagged(); var inspection = Inspect(bytes); var id = inspection.Elements[1].ObjectId;
        using var edit = new PdfTransaction(PdfSyntax.Read(new MemoryStream(bytes)));
        edit.Set(id, PdfValue.Dictionary(edit.Resolve(id).Entries.Select(p => new KeyValuePair<string, PdfValue>(p.Key, p.Key == "S" ? PdfValue.Name("CustomHeading") : p.Value))));
        if (mapping != null)
        {
            var catalog = edit.Resolve(edit.Trailer.Entries["Root"].ReferenceId); var rootId = catalog.Entries["StructTreeRoot"].ReferenceId;
            var values = edit.Resolve(rootId).Entries.ToDictionary(p => p.Key, p => p.Value);
            values["RoleMap"] = WriterTests.Dictionary(("CustomHeading", PdfValue.Name(mapping))); edit.Set(rootId, PdfValue.Dictionary(values));
        }
        return WriterTests.Save(edit);
    }
    [Fact]
    public void AcceptedRoleMapPreservesRawRoleAndMakesStandardSemanticsObservable()
    {
        byte[] bytes = CustomRole(); Assert.Contains(Inspect(bytes).Findings, f => f.Rule == "Accessibility.RoleMapping");
        var plan = PdfAccessibility.PrepareReview(new MemoryStream(bytes), new[] { PdfSemanticSuggestion.RoleMapping("map", "CustomHeading", "H1", "Explicit heading semantics") });
        using var output = new MemoryStream(); var audit = PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, new[] { new PdfSemanticReviewDecision("map", true, "fixture", "heading reviewed") });
        var after = Inspect(output.ToArray()); Assert.Equal("CustomHeading", after.Elements[1].Role); Assert.DoesNotContain(after.Findings, f => f.Rule == "Accessibility.RoleMapping");
        Assert.Equal(2, Assert.Single(audit.Entries).AffectedObjects.Count);
        File.WriteAllBytes(Artifact("role-map.pdf"), output.ToArray());
    }
    [Fact]
    public void MappedAlternativeTextRequirementsAndRoleMapCyclesCannotBeHidden()
    {
        byte[] bytes = CustomRole("Figure"); var inspection = Inspect(bytes); Assert.Contains(inspection.Findings, f => f.Rule == "Accessibility.AlternativeText");
        Assert.Equal("Accessibility.AlternativeText", Assert.Throws<PdfException>(() => PdfAccessibility.Remediate(inspection, inspection.Elements[1].ObjectId, alternativeText: " ")).Diagnostic.Code);
        Assert.Equal("Accessibility.RoleMapCycle", Assert.Throws<PdfException>(() => Inspect(CustomRole("CustomHeading"))).Diagnostic.Code);
        Assert.Throws<ArgumentException>(() => PdfSemanticSuggestion.RoleMapping("map", "P", "H1", "reason"));
        Assert.Equal("Accessibility.Review.RoleMapProfile", Assert.Throws<PdfException>(() => PdfAccessibility.PrepareReview(new MemoryStream(bytes), new[] { PdfSemanticSuggestion.RoleMapping("map", "CustomHeading", "H1", "reason") }, PdfAccessibilityProfile.Ua2)).Diagnostic.Code);
    }
    [Fact]
    public void RoleMappingLedgerIncludesTransitivelyAffectedElements()
    {
        byte[] bytes = CustomRole("Alias"); var id = Inspect(bytes).Elements[1].ObjectId;
        var plan = PdfAccessibility.PrepareReview(new MemoryStream(bytes), new[] { PdfSemanticSuggestion.RoleMapping("map", "Alias", "H1", "Explicit role semantics") });
        using var output = new MemoryStream(); var audit = PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, new[] { new PdfSemanticReviewDecision("map", true, "fixture", "decision") });
        Assert.Contains(id, Assert.Single(audit.Entries).AffectedObjects);
        Assert.DoesNotContain(Inspect(output.ToArray()).Findings, f => f.Rule == "Accessibility.RoleMapping");
    }
    [Theory] [InlineData(PdfAccessibilityProfile.Ua1)] [InlineData(PdfAccessibilityProfile.Ua2)]
    public void ExplicitHeadingListAndTableAuthoringIsReviewedAndRevalidated(PdfAccessibilityProfile profile)
    {
        byte[] bytes = AccessibilityTests.Original();
        var tree = new PdfStructurePlan("en-US", new("Document", new PdfStructureItem[] {
            new PdfStructureElement("Sect", new PdfStructureItem[] { new PdfStructureElement("H1", Array.Empty<PdfStructureItem>(), actualText: "Explicit heading"), new PdfStructureElement("L", new PdfStructureItem[] { new PdfStructureElement("LI", new PdfStructureItem[] { new PdfStructureElement("Lbl", Array.Empty<PdfStructureItem>(), actualText: "1"), new PdfStructureElement("LBody", Array.Empty<PdfStructureItem>(), actualText: "Explicit item") }) }) }),
            new PdfStructureElement("Table", new PdfStructureItem[] { new PdfStructureElement("TR", new PdfStructureItem[] { new PdfStructureElement("TH", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0,0) }, identifier: "header", table: new(headerScope:"Column")), new PdfStructureElement("TD", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0,1) }, table: new(headers:new[]{"header"})) }) }) }), profile);
        var plan = PdfAccessibility.PrepareReview(new MemoryStream(bytes), new[] { PdfSemanticSuggestion.Structure("author", tree, "Original fixture's explicit author semantics") }, profile);
        using var output = new MemoryStream(); var audit = PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, new[] { new PdfSemanticReviewDecision("author", true, "fixture", "Synthetic contract decision, not a human qualification") });
        Assert.True(Assert.Single(audit.Entries).Applied); Assert.True(audit.HumanReviewRequired);
        using var ledger = new MemoryStream(); PdfAccessibility.WriteReview(audit, ledger); string json = Encoding.UTF8.GetString(ledger.ToArray()); Assert.Contains("\"headers\":[\"header\"]", json);
        var after = Inspect(output.ToArray()); Assert.Contains(after.Elements, e => e.Role == "H1"); Assert.Contains(after.Elements, e => e.Role == "LBody"); Assert.Contains(after.Elements, e => e.Role == "TH");
        File.WriteAllBytes(Artifact("author-" + profile + ".pdf"), output.ToArray()); File.WriteAllBytes(Artifact("author-" + profile + ".json"), ledger.ToArray());
    }
    [Fact]
    public void CancellationAndSharedLimitsAbortBeforeDelivery()
    {
        byte[] bytes = Tagged(); var root = Inspect(bytes).Elements[0]; var suggestions = new[] { PdfSemanticSuggestion.Element("order", root.ObjectId, "reason", childOrder: new[] { 1,0 }) };
        Assert.ThrowsAny<OperationCanceledException>(() => PdfAccessibility.PrepareReview(new MemoryStream(bytes), suggestions, cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => PdfAccessibility.PrepareReview(new MemoryStream(bytes), suggestions, policy: new(limits:new(maximumInputBytes:1)))).Diagnostic.Code);
        var plan = PdfAccessibility.PrepareReview(new MemoryStream(bytes), suggestions); var decisions = new[] { new PdfSemanticReviewDecision("order", true, "fixture", "decision") };
        using var output = new MemoryStream(); Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, decisions, policy: new(limits:new(maximumExpandedBytes:1)))).Diagnostic.Code); Assert.Equal(0, output.Length);
        Assert.ThrowsAny<OperationCanceledException>(() => PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, decisions, cancellationToken:new CancellationToken(true)));
        var audit = PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, decisions);
        using var ledger = new MemoryStream(); Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => PdfAccessibility.WriteReview(audit, ledger, new(limits:new(maximumOutputBytes:1)))).Diagnostic.Code);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void CompiledLanguagesRequireAnExplicitCallerDecision(bool visualBasic)
    {
        byte[] bytes = Tagged(); var id = Inspect(bytes).Elements[0].ObjectId;
        var decision = new PdfSemanticReviewDecision("order", true, "synthetic-fixture-reviewer", "Contract-only observation, not human qualification");
        using var output = new MemoryStream(); using var ledger = new MemoryStream();
        if (visualBasic) ContractExamples.VisualBasic.ContractExample.ReviewReadingOrder(bytes, id, new[] { 1,0 }, decision, output, ledger);
        else ContractExamples.CSharp.ContractExample.ReviewReadingOrder(bytes, id, new[] { 1,0 }, decision, output, ledger);
        Assert.Equal(new[] { 1,0 }, Inspect(output.ToArray()).Content.Select(c => c.Mcid)); Assert.Contains("\"conformanceVerified\":false", Encoding.UTF8.GetString(ledger.ToArray()));
        string stem = visualBasic ? "visual-basic" : "csharp";
        File.WriteAllBytes(Artifact(stem + ".pdf"), output.ToArray()); File.WriteAllBytes(Artifact(stem + ".json"), ledger.ToArray());
    }
    [Theory] [InlineData(PdfAccessibilityProfile.Ua1)] [InlineData(PdfAccessibilityProfile.Ua2)]
    public void AcceptedDescriptionAndOrderRetainIndependentAutomatedUaResultsWithoutClaimingHumanReview(PdfAccessibilityProfile profile)
    {
        byte[] bytes = AccessibilityTests.PreparedFigure(profile); var inspection = Inspect(bytes);
        var plan = PdfAccessibility.PrepareReview(new MemoryStream(bytes), new[] {
            PdfSemanticSuggestion.Element("description", inspection.Elements[1].ObjectId, "Explicit fixture author description", alternativeText:"Original black rectangle on the left"),
            PdfSemanticSuggestion.Element("order", inspection.Elements[0].ObjectId, "Fixture permutation for structural validation", childOrder:new[]{1,0}) }, profile);
        using var output = new MemoryStream(); var audit = PdfAccessibility.ApplyReview(new MemoryStream(bytes), output, plan, plan.Suggestions.Select(s=>new PdfSemanticReviewDecision(s.Id,true,"synthetic-fixture-reviewer","Source contract decision, not human qualification")));
        output.Position = 0;
        var validation = PdfAccessibility.Validate(output, profile, new(new Fluent.Pdf.Compliance.VeraPdfAccessibilityReportValidator(ArchivalFixture.RunAccessibility)));
        Assert.True(validation.Completed); Assert.True(validation.IsCompliant); Assert.Empty(validation.Findings); Assert.Null(validation.HumanReview); Assert.True(audit.HumanReviewRequired);
        using var ledger = new MemoryStream(); PdfAccessibility.WriteReview(audit, ledger);
        File.WriteAllBytes(Artifact("validator-" + profile + ".pdf"), output.ToArray()); File.WriteAllBytes(Artifact("validator-" + profile + ".json"), ledger.ToArray());
    }
}
