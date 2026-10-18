using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class AccessibilityTests
{
    internal static byte[] Original(string mode = "")
    {
        using var f = new SyntaxFixtures();
        var offsets = new Dictionary<int, long>();
        offsets[1] = f.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        offsets[2] = f.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        offsets[3] = f.Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /Contents 4 0 R /Resources << " + (mode.StartsWith("form", StringComparison.Ordinal) ? "/XObject << /X 5 0 R >>" : "") + " >> " + (mode == "link" ? "/Annots [5 0 R]" : "") + " >>");
        string content = mode.StartsWith("form", StringComparison.Ordinal) ? "/X Do" : mode == "unmarked" ? "0 0 10 10 re f" : "/P <</MCID 0>> BDC 0 0 10 10 re f EMC /P <</MCID 1>> BDC 20 0 10 10 re f EMC /Artifact BMC 30 0 10 10 re f EMC";
        if (mode == "formRepeated")
            content += " /X Do";
        if (mode == "formWhole")
            content = "/P <</MCID 0>> BDC /X Do EMC";
        if (mode == "duplicate")
            content += " /P <</MCID 0>> BDC 0 0 1 1 re f EMC";
        offsets[4] = f.StreamObject(4, "", Encoding.ASCII.GetBytes(content));
        if (mode.StartsWith("form", StringComparison.Ordinal))
            offsets[5] = f.StreamObject(5, "/Type /XObject /Subtype /Form /BBox [0 0 50 50] /Resources << >>", Encoding.ASCII.GetBytes("/Figure <</MCID 0>> BDC 0 0 10 10 re f EMC"));
        if (mode == "link")
            offsets[5] = f.Object(5, "<< /Type /Annot /Subtype /Link /Rect [0 0 10 10] /A << /S /URI /URI (https://example.invalid/) >> >>");
        var rows = new List<(int, int, long, bool)>
        {
            (0, 65535, 0, true)
        };
        rows.AddRange(offsets.Select(p => (p.Key, 0, p.Value, false)));
        f.Classic(rows.ToArray(), "/Root 1 0 R", offsets.Keys.Max() + 1);
        return f.Bytes.ToArray();
    }

    private static PdfDocument Open(string mode = "")
    {
        using var bytes = new MemoryStream(Original(mode));
        return Pdf.Open(bytes);
    }

    internal static PdfStructurePlan Plan(PdfAccessibilityProfile profile = PdfAccessibilityProfile.Ua1) => new("en-US", new("Document", new PdfStructureItem[] { new PdfStructureElement("H1", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0, 0) }), new PdfStructureElement("P", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0, 1) }) }), profile);
    private static string Error(Action action) => Assert.ThrowsAny<PdfException>(action).Diagnostic.Code;
    [Theory]
    [InlineData(PdfAccessibilityProfile.Ua1)]
    [InlineData(PdfAccessibilityProfile.Ua2)]
    public void ExplicitAuthorPlanCreatesRealStructureAndParentAssociationsWithoutClaimingConformance(PdfAccessibilityProfile profile)
    {
        using var original = Open();
        using var tagged = PdfAccessibility.Author(original.CreateSnapshot(), Plan(profile));
        var inspection = PdfAccessibility.Inspect(tagged.CreateSnapshot(), profile);
        Assert.Equal("en-US", inspection.Language);
        Assert.Equal(new[] { "Document", "H1", "P" }, inspection.Elements.Select(e => e.Role));
        Assert.Equal(new[] { 0, 1 }, inspection.Content.Select(c => c.Mcid));
        Assert.True(inspection.HumanReviewRequired);
        Assert.Single(inspection.Findings, f => f.Rule == "Accessibility.IndependentValidationRequired");
        Assert.DoesNotContain(inspection.Findings, f => f.Rule == "Accessibility.UnmarkedPainting");
        Assert.Empty(PdfAccessibility.Inspect(original.CreateSnapshot()).Elements);
        using var bytes = new MemoryStream();
        tagged.Save(bytes);
        bytes.Position = 0;
        using var roundtrip = Pdf.Open(bytes);
        Assert.Equal(3, PdfAccessibility.Inspect(roundtrip.CreateSnapshot(), profile).Elements.Count);
    }

    [Fact]
    public void RemediationPreservesIdentitiesAndParentLinksWhileChangingExplicitReadingOrderAndLanguage()
    {
        using var original = Open();
        using var tagged = PdfAccessibility.Author(original.CreateSnapshot(), Plan());
        var inspection = PdfAccessibility.Inspect(tagged.CreateSnapshot());
        var root = inspection.Elements[0];
        using var reordered = PdfAccessibility.Remediate(inspection, root.ObjectId, language: "de-DE", childOrder: new[] { 1, 0 });
        var next = PdfAccessibility.Inspect(reordered.CreateSnapshot());
        Assert.Equal(root.ObjectId, next.Elements[0].ObjectId);
        Assert.Equal("de-DE", next.Elements[0].Language);
        Assert.Equal(new[] { 1, 0 }, next.Content.Select(c => c.Mcid));
        Assert.Equal(inspection.Elements.Select(e => e.ObjectId).OrderBy(i => i.Number), next.Elements.Select(e => e.ObjectId).OrderBy(i => i.Number));
        Assert.Equal("Accessibility.ChildOrder", Error(() => PdfAccessibility.Remediate(inspection, root.ObjectId, childOrder: new[] { 0, 0 })));
        Assert.Equal(new[] { 0, 1 }, PdfAccessibility.Inspect(tagged.CreateSnapshot()).Content.Select(c => c.Mcid));
    }

    [Fact]
    public void AuthorDescriptionsAndTableHeaderIdentitiesAreExplicit()
    {
        Assert.Throws<ArgumentException>(() => new PdfStructureElement("Figure", Array.Empty<PdfStructureItem>()));
        Assert.Throws<ArgumentException>(() => new PdfStructureTableAttributes(headerScope: "Guessed"));
        using var original = Open();
        PdfStructurePlan Table(string target) => new("en", new("Document", new PdfStructureItem[] { new PdfStructureElement("Table", new PdfStructureItem[] { new PdfStructureElement("TR", new PdfStructureItem[] { new PdfStructureElement("TH", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0, 0) }, identifier: "heading", table: new(headerScope: "Column")), new PdfStructureElement("TD", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0, 1) }, table: new(headers: new[] { target })) }) }, table: new(summary: "Original author table")) }));
        using var tagged = PdfAccessibility.Author(original.CreateSnapshot(), Table("heading"));
        Assert.Equal(5, PdfAccessibility.Inspect(tagged.CreateSnapshot()).Elements.Count);
        Assert.Equal("Accessibility.HeaderIdentity", Error(() => PdfAccessibility.Author(original.CreateSnapshot(), Table("absent"))));
    }

    [Fact]
    public void FormStreamMcidsUseTheirOwnParentKeyAndAnExplicitMcrStream()
    {
        using var original = Open("form");
        var plan = new PdfStructurePlan("en", new("Document", new PdfStructureItem[] { new PdfStructureElement("Figure", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0, 0, new PdfObjectId(5, 0)) }, alternativeText: "Original filled rectangle") }));
        using var tagged = PdfAccessibility.Author(original.CreateSnapshot(), plan);
        var inspection = PdfAccessibility.Inspect(tagged.CreateSnapshot());
        Assert.Single(inspection.Content);
        Assert.Equal(new PdfObjectId(5, 0), inspection.Content[0].StreamId);
        Assert.Equal("Original filled rectangle", inspection.Elements[1].AlternativeText);
    }

    [Fact]
    public void PassiveLinkObjectReferencesUseUniqueAnnotationParentAssociations()
    {
        using var original = Open("link");
        var plan = new PdfStructurePlan("en", new("Document", new PdfStructureItem[] { new PdfStructureElement("Link", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0, 0), PdfStructureContent.Annotation(0, new(5, 0)) }), new PdfStructureElement("P", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0, 1) }) }));
        using var tagged = PdfAccessibility.Author(original.CreateSnapshot(), plan);
        var inspection = PdfAccessibility.Inspect(tagged.CreateSnapshot());
        Assert.Equal(3, inspection.Content.Count);
        Assert.Single(inspection.Content, c => c.AnnotationId != null);
        Assert.DoesNotContain(inspection.Findings, f => f.Rule == "Accessibility.AnnotationUnassociated");
    }

    [Fact]
    public void DuplicateOrUncoveredContentCannotBecomeAnApparentlyConsistentStructure()
    {
        using var duplicate = Open("duplicate");
        Assert.Equal("Accessibility.DuplicateMcid", Error(() => PdfAccessibility.Inspect(duplicate.CreateSnapshot())));
        using var original = Open();
        var shortPlan = new PdfStructurePlan("en", new("Document", new PdfStructureItem[] { new PdfStructureElement("P", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0, 0) }) }));
        Assert.Equal("Accessibility.PlanCoverage", Error(() => PdfAccessibility.Author(original.CreateSnapshot(), shortPlan)));
        using var unmarked = Open("unmarked");
        var observation = PdfAccessibility.Inspect(unmarked.CreateSnapshot());
        Assert.Contains(observation.Findings, f => f.Rule == "Accessibility.UnmarkedPainting");
        Assert.True(observation.HumanReviewRequired);
    }

    [Fact]
    public void ExistingSemanticsRequireExplicitReplacementAndStrictLossPolicy()
    {
        using var original = Open();
        using var tagged = PdfAccessibility.Author(original.CreateSnapshot(), Plan());
        Assert.Equal("Accessibility.ExistingStructure", Error(() => PdfAccessibility.Author(tagged.CreateSnapshot(), Plan())));
        var replacement = new PdfStructurePlan("en", Plan().Root, replaceExisting: true);
        Assert.Equal("Accessibility.ReplaceStructure", Error(() => PdfAccessibility.Author(tagged.CreateSnapshot(), replacement)));
        using var changed = PdfAccessibility.Author(tagged.CreateSnapshot(), replacement, policy: new(PdfProcessingMode.Permissive));
        Assert.Contains(changed.LastOperationReport!.Diagnostics, d => d.Code == "Accessibility.ReplaceStructure");
    }

    [Fact]
    public void CancellationAndSmallBudgetsFailWithoutChangingTheOriginal()
    {
        using var original = Open();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfAccessibility.Author(original.CreateSnapshot(), Plan(), cancellationToken: cancellation.Token));
        Assert.StartsWith("Limit.", Error(() => PdfAccessibility.Author(original.CreateSnapshot(), Plan(), policy: new(limits: new(maximumObjects: 1)))));
        Assert.Empty(PdfAccessibility.Inspect(original.CreateSnapshot()).Elements);
    }

    [Theory]
    [InlineData(PdfAccessibilityProfile.Ua1)]
    [InlineData(PdfAccessibilityProfile.Ua2)]
    public void ActualPinnedIndependentUaValidatorReportsMissingIdentificationInsteadOfInventingConformance(PdfAccessibilityProfile profile)
    {
        using var original = Open();
        using var tagged = PdfAccessibility.Author(original.CreateSnapshot(), Plan(profile));
        using var bytes = new MemoryStream();
        tagged.Save(bytes);
        bytes.Position = 0;
        var result = PdfAccessibility.Validate(bytes, profile, new(new Fluent.Pdf.Compliance.VeraPdfAccessibilityReportValidator(ArchivalFixture.RunAccessibility)));
        Assert.True(result.Completed);
        Assert.False(result.IsCompliant);
        Assert.NotEmpty(result.Findings);
        Assert.Null(result.HumanReview);
        Assert.True(bytes.CanRead);
    }

    [Fact]
    public void BothPublicLanguageConsumersAuthorTheSameExplicitStructure()
    {
        using var original = Open();
        using var cs = ContractExamples.CSharp.ContractExample.TagPdf(original.CreateSnapshot(), Plan());
        using var vb = ContractExamples.VisualBasic.ContractExample.TagPdf(original.CreateSnapshot(), Plan());
        Assert.Equal(new[] { 0, 1 }, PdfAccessibility.Inspect(cs.CreateSnapshot()).Content.Select(c => c.Mcid));
        Assert.Equal(new[] { 0, 1 }, PdfAccessibility.Inspect(vb.CreateSnapshot()).Content.Select(c => c.Mcid));
    }

    [Theory]
    [InlineData("<report/>", "Accessibility.ReportCompleteness")]
    [InlineData("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///private'>]><report>&e;</report>", "Accessibility.ReportXml")]
    [InlineData("<report><jobs><job><validationReport profileName='PDF/A-1B validation profile' isCompliant='true' jobEndStatus='normal'/></job></jobs></report>", "Accessibility.ReportProfile")]
    public void AccessibilityReportBridgeRejectsIncompleteExternalEntityAndWrongStandardReports(string xml, string expected)
    {
        var validator = new Fluent.Pdf.Compliance.VeraPdfAccessibilityReportValidator((profile, input, report, c) =>
        {
            var bytes = Encoding.UTF8.GetBytes(xml);
            report.Write(bytes, 0, bytes.Length);
        });
        using var input = new MemoryStream(Original());
        Assert.Equal(expected, Error(() => PdfAccessibility.Validate(input, PdfAccessibilityProfile.Ua1, new(validator))));
        Assert.True(input.CanRead);
    }

    [Theory]
    [InlineData("formRepeated", "Accessibility.DuplicateMcid")]
    [InlineData("formWhole", "Accessibility.MarkedNesting")]
    public void RepeatedOrDoublyAssociatedTaggedFormsReject(string mode, string expected)
    {
        using var original = Open(mode);
        Assert.Equal(expected, Error(() => PdfAccessibility.Inspect(original.CreateSnapshot())));
    }

    [Fact]
    public void HeaderCyclesRejectBeforeExposingANewModel()
    {
        using var original = Open();
        var root = new PdfStructureElement("Document", new PdfStructureItem[] { new PdfStructureElement("Table", new PdfStructureItem[] { new PdfStructureElement("TR", new PdfStructureItem[] { new PdfStructureElement("TH", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0, 0) }, identifier: "a", table: new(headers: new[] { "b" })), new PdfStructureElement("TH", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0, 1) }, identifier: "b", table: new(headers: new[] { "a" })) }) }) });
        Assert.Equal("Accessibility.HeaderCycle", Error(() => PdfAccessibility.Author(original.CreateSnapshot(), new("en", root))));
    }

    [Theory]
    [InlineData(PdfAccessibilityProfile.Ua1)]
    [InlineData(PdfAccessibilityProfile.Ua2)]
    public void ExplicitlyPreparedOriginalFiguresPassIndependentAutomatedUaChecksWithoutHumanReview(PdfAccessibilityProfile profile)
    {
        using var original = Open();
        var plan = new PdfStructurePlan("en-US", new("Document", new PdfStructureItem[] { new PdfStructureElement("Figure", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0, 0) }, alternativeText: "Original filled rectangle at left"), new PdfStructureElement("Figure", new PdfStructureItem[] { PdfStructureContent.MarkedContent(0, 1) }, alternativeText: "Original filled rectangle at right") }), profile);
        using var tagged = PdfAccessibility.Author(original.CreateSnapshot(), plan);
        string xml = "<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description rdf:about='' xmlns:pdfuaid='http://www.aiim.org/pdfua/ns/id/' xmlns:dc='http://purl.org/dc/elements/1.1/'><pdfuaid:part>" + (profile == PdfAccessibilityProfile.Ua1 ? "1" : "2") + "</pdfuaid:part>" + (profile == PdfAccessibilityProfile.Ua2 ? "<pdfuaid:rev>2024</pdfuaid:rev>" : "") + "<dc:title><rdf:Alt><rdf:li xml:lang='x-default'>Original accessibility shapes</rdf:li></rdf:Alt></dc:title></rdf:Description></rdf:RDF></x:xmpmeta>";
        using var metadata = PdfMetadata.Edit(tagged.CreateSnapshot(), e =>
        {
            e.SetInformation("Title", "Original accessibility shapes");
            e.SetXmp(xml);
        });
        using var prepared = PdfPresentation.SetViewerPreferences(metadata.CreateSnapshot(), PdfValue.Dictionary(new[] { new KeyValuePair<string, PdfValue>("DisplayDocTitle", PdfValue.Boolean(true)) }));
        using var bytes = new MemoryStream();
        prepared.Save(bytes);
        bytes.Position = 0;
        var result = PdfAccessibility.Validate(bytes, profile, new(new Fluent.Pdf.Compliance.VeraPdfAccessibilityReportValidator(ArchivalFixture.RunAccessibility)));
        Assert.True(result.Completed);
        Assert.True(result.IsCompliant);
        Assert.Empty(result.Findings);
        Assert.Null(result.HumanReview);
    }

    private sealed class Validator(string mode = "") : IPdfAccessibilityValidator
    {
        public PdfProviderDescriptor Descriptor { get; } = new("original.accessibility-boundary", "1", PdfCapability.ValidateAccessibility);

        public Stream? Borrowed;
        public PdfOperationContext? Context;
        public PdfAccessibilityValidation Validate(PdfAccessibilityProfile profile, Stream input, PdfOperationContext c)
        {
            Borrowed = input;
            Context = c;
            using var bytes = new MemoryStream();
            input.CopyTo(bytes);
            using var hash = SHA256.Create();
            string digest = BitConverter.ToString(hash.ComputeHash(bytes.ToArray())).Replace("-", "").ToLowerInvariant();
            if (mode == "swallow")
            {
                try
                {
                    c.Consume(PdfResourceKind.Objects, long.MaxValue);
                }
                catch (PdfException)
                {
                }
            }

            return new(profile, mode == "wrong" ? new string ('0', 64) : digest, true, mode != "finding", mode == "finding" ? new[] { new PdfAccessibilityFinding("original.rule", "document", "Original nonconformance fixture") } : Array.Empty<PdfAccessibilityFinding>());
        }
    }

    [Theory]
    [InlineData(PdfAccessibilityProfile.Ua1)]
    [InlineData(PdfAccessibilityProfile.Ua2)]
    public void IndependentValidationIsBoundToExactBytesAndDoesNotInventHumanReview(PdfAccessibilityProfile profile)
    {
        using var input = new MemoryStream(Original());
        var validator = new Validator();
        var result = PdfAccessibility.Validate(input, profile, new(validator));
        Assert.True(result.IsCompliant);
        Assert.Null(result.HumanReview);
        Assert.NotNull(result.OperationReport);
        Assert.True(input.CanRead);
        Assert.Equal("ValidateAccessibility", validator.Context!.Operation);
        Assert.Throws<ObjectDisposedException>(() => validator.Borrowed!.ReadByte());
        var review = new PdfAccessibilityHumanReview(profile, result.SourceSha256, "Original reviewer", "fixture-review", true, false);
        input.Position = 0;
        var repeated = PdfAccessibility.Validate(input, profile, new(validator), review);
        Assert.Same(review, repeated.HumanReview);
        Assert.False(repeated.HumanReview!.SemanticsReviewed);
        input.Position = 0;
        Assert.Equal("Accessibility.ValidatorBinding", Error(() => PdfAccessibility.Validate(input, profile, new(new Validator("wrong")))));
        input.Position = 0;
        Assert.StartsWith("Limit.", Error(() => PdfAccessibility.Validate(input, profile, new(new Validator("swallow")))));
        input.Position = 0;
        Assert.False(PdfAccessibility.Validate(input, profile, new(new Validator("finding"))).IsCompliant);
        input.Position = 0;
        Assert.Equal("Accessibility.HumanReviewBinding", Error(() => PdfAccessibility.Validate(input, profile, new(validator), new(profile, new string ('0', 64), "reviewer", "old-report", true, true))));
    }
}
