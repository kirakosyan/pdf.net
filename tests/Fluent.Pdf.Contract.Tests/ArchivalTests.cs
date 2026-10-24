using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ArchivalTests
{
    [Fact]
    public void ArchivalAssociationAllowanceCannotDowngradeUnrelatedPdf20Features()
    {
        using var source = NavigationTests.Fixture((edit, root, _) => NavigationTests.Put(edit, root, "Feature", NavigationTests.D(("Type", PdfValue.Name("ExtGState")), ("UseBlackPtComp", PdfValue.Name("ON")))), 1);
        using var output = new MemoryStream();
        var failure = Assert.Throws<PdfException>(() => PdfArchival.Convert(source.CreateSnapshot(), output, new(PdfArchivalProfile.A3B, ArchivalFixture.Icc()), ArchivalFixture.Providers()));
        Assert.Equal("Writer.VersionTooLow", failure.Diagnostic.Code);
        Assert.Empty(output.ToArray());
    }
    internal static PdfDocument DecorativeDrawing(PdfArchivalProfile profile)
    {
        using var original = Pdf.Create().AddPage();
        using var content = new MemoryStream(Encoding.ASCII.GetBytes("/Artifact BMC 0.2 0.4 0.8 rg 20 30 150 100 re f EMC"));
        original.Pages[0].Content = PdfContentProgram.Parse(content);
        using var bytes = new MemoryStream();
        original.Save(bytes);
        bytes.Position = 0;
        var syntax = PdfSyntax.Read(bytes);
        using var edit = new PdfTransaction(syntax);
        var rootId = edit.Trailer.Entries["Root"].ReferenceId;
        var catalog = syntax.Resolve(rootId).Entries.ToDictionary(p => p.Key, p => p.Value);
        // This original drawing is explicitly decorative. No converter invents semantic tags for arbitrary source content.
        if (profile == PdfArchivalProfile.A1A || profile == PdfArchivalProfile.A2A || profile == PdfArchivalProfile.A3A)
        {
            var parentTree = edit.Add(NavigationTests.D(("Nums", PdfValue.Array())));
            var structure = edit.Add(NavigationTests.D(("Type", PdfValue.Name("StructTreeRoot")), ("K", PdfValue.Array()), ("ParentTree", PdfValue.Reference(parentTree))));
            catalog["StructTreeRoot"] = PdfValue.Reference(structure);
            catalog["MarkInfo"] = NavigationTests.D(("Marked", PdfValue.Boolean(true)));
            catalog["Lang"] = PdfValue.String(Encoding.ASCII.GetBytes("en-US"));
            edit.Set(rootId, PdfValue.Dictionary(catalog));
        }

        using var output = new MemoryStream();
        edit.Save(output);
        output.Position = 0;
        var document = Pdf.Open(output);
        if (profile != PdfArchivalProfile.A4F)
            return document;
        using (document)
        using (var attachment = new MemoryStream(Encoding.UTF8.GetBytes("Original archival companion data")))
            return PdfAttachments.Add(document.CreateSnapshot(), attachment, new("original.txt", "text/plain", "Original companion data", modificationTime: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), relationship: "Data"));
    }

    public static IEnumerable<object[]> Profiles() => Enum.GetValues(typeof(PdfArchivalProfile)).Cast<PdfArchivalProfile>().Select(p => new object[] { p });
    [Theory]
    [MemberData(nameof(Profiles))]
    public void EverySelectedProfileIsIndependentlyValidatedOnAnOriginalDecorativeFixture(PdfArchivalProfile profile)
    {
        using var source = DecorativeDrawing(profile);
        using var output = new MemoryStream();
        var providers = ArchivalFixture.Providers();
        var result = PdfArchival.Convert(source.CreateSnapshot(), output, new(profile, ArchivalFixture.Icc()), providers);
        Assert.True(result.Validation.Completed);
        Assert.True(result.Validation.IsCompliant);
        Assert.Empty(result.Validation.Findings);
        Assert.True(output.CanWrite);
        output.Position = 0;
        var repeated = PdfArchival.Validate(output, profile, providers);
        Assert.True(repeated.IsCompliant);
        Assert.NotNull(repeated.OperationReport);
        Assert.Equal(result.Validation.SourceSha256, repeated.SourceSha256);
        Assert.True(output.CanRead);
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        Assert.Single(reopened.Pages);
    }

    [Theory]
    [InlineData(PdfArchivalProfile.A1B)]
    [InlineData(PdfArchivalProfile.A2U)]
    [InlineData(PdfArchivalProfile.A3U)]
    public void EmbeddedFontAndUnicodeSurviveIndependentProfileValidation(PdfArchivalProfile profile)
    {
        var font = FontTests.Read("original.ttf");
        var resource = PdfFontResource.CreateText(font, "AB一😀");
        using var source = Pdf.Create().AddPage();
        source.Pages[0].SetFont("F1", resource);
        source.Pages[0].Content = FontTests.Show(resource);
        using var output = new MemoryStream();
        var result = PdfArchival.Convert(source.CreateSnapshot(), output, new(profile, ArchivalFixture.Icc()), ArchivalFixture.Providers());
        Assert.True(result.Validation.IsCompliant);
        if (profile == PdfArchivalProfile.A1B)
        {
            Assert.Contains(result.OperationReport.Diagnostics, d => d.Code == "Archival.CidSetAdded");
            output.Position = 0;
            var syntax = PdfSyntax.Read(output);
            var cidFont = Assert.Single(syntax.Revisions[0].Entries.Where(e => e.Kind == PdfCrossReferenceKind.Offset).Select(e => syntax.Resolve(e.Id)), v => v.Kind == PdfValueKind.Dictionary && v.Entries.TryGetValue("Subtype", out var subtype) && subtype.Kind == PdfValueKind.Name && subtype.NameValue == "CIDFontType2");
            var descriptor = syntax.Resolve(cidFont.Entries["FontDescriptor"].ReferenceId);
            var set = syntax.Resolve(descriptor.Entries["CIDSet"].ReferenceId);
            using var bits = set.OpenRawStream();
            Assert.Equal(248, bits.ReadByte());
            Assert.Equal(-1, bits.ReadByte());
        }

        output.Position = 0;
        using var reopened = Pdf.Open(output, providers: FontTests.Providers);
        Assert.Equal("AB一😀", Assert.Single(reopened.Pages[0].ReadFonts()).DecodeText(resource.EncodedText).Text);
    }

    [Fact]
    public void MissingValidatorNeverTreatsMetadataSelectionAsCompliance()
    {
        using var source = DecorativeDrawing(PdfArchivalProfile.A2B);
        using var output = new MemoryStream();
        Assert.Throws<PdfProviderUnavailableException>(() => PdfArchival.Convert(source.CreateSnapshot(), output, new(PdfArchivalProfile.A2B), PdfProviderRegistry.Empty));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void StructuralPreflightReportsUnembeddedFontsAttachmentsAndUntaggedLevelA()
    {
        using var source = Pdf.Create().AddPage();
        Assert.Contains(PdfArchival.Inspect(source.CreateSnapshot(), PdfArchivalProfile.A1A).Findings, f => f.Rule == "Archival.StructureRequired");
        using var attachment = new MemoryStream(new byte[] { 1, 2, 3 });
        using var attached = PdfAttachments.Add(source.CreateSnapshot(), attachment, new("original.bin"));
        Assert.Contains(PdfArchival.Inspect(attached.CreateSnapshot(), PdfArchivalProfile.A2B).Findings, f => f.Rule == "Archival.AttachmentProfile");
        var font = PdfFont.Standard("Helvetica", FontTests.Providers);
        source.Pages[0].SetFont("F1", PdfFontResource.CreateText(font, "A"));
        Assert.Contains(PdfArchival.Inspect(source.CreateSnapshot(), PdfArchivalProfile.A2B).Findings, f => f.Rule == "Archival.FontNotEmbedded");
    }

    [Fact]
    public void MetadataRepairIsExplicitReportedAndStrictModeRejectsItsLoss()
    {
        using var source = DecorativeDrawing(PdfArchivalProfile.A2B);
        using var metadata = PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetXmp(MetadataTests.Packet));
        using var output = new MemoryStream();
        var providers = ArchivalFixture.Providers();
        var options = new PdfArchivalOptions(PdfArchivalProfile.A2B, ArchivalFixture.Icc(), replaceMetadata: true);
        Assert.Equal("Archival.MetadataReplaced", Assert.Throws<PdfException>(() => PdfArchival.Convert(metadata.CreateSnapshot(), output, options, providers)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        var result = PdfArchival.Convert(metadata.CreateSnapshot(), output, options, providers, new(PdfProcessingMode.Permissive));
        Assert.True(result.Validation.IsCompliant);
        Assert.Contains(result.OperationReport.Diagnostics, f => f.Code == "Archival.MetadataReplaced" && f.LossCategory == PdfLossCategory.OmittedContent);
    }

    [Fact]
    public void CancellationAndSharedBudgetsPublishNoCandidateBytes()
    {
        using var source = DecorativeDrawing(PdfArchivalProfile.A2B);
        using var output = new MemoryStream();
        var providers = ArchivalFixture.Providers();
        var options = new PdfArchivalOptions(PdfArchivalProfile.A2B);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfArchival.Convert(source.CreateSnapshot(), output, options, providers, cancellationToken: canceled.Token));
        Assert.Equal(0, output.Length);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfArchival.Convert(source.CreateSnapshot(), output, options, providers, new(limits: new(maximumExpandedBytes: 64)))).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void FailedIndependentValidationReturnsDetachedRuleFindingsWithoutOutput()
    {
        using var source = DecorativeDrawing(PdfArchivalProfile.A2B);
        using var output = new MemoryStream();
        var failure = Assert.Throws<PdfException>(() => PdfArchival.Convert(source.CreateSnapshot(), output, new(PdfArchivalProfile.A2B), ArchivalFixture.Providers()));
        Assert.Equal("Archival.IndependentValidation", failure.Diagnostic.Code);
        Assert.Equal(0, output.Length);
        Assert.True(failure.Data.Contains("Fluent.Pdf.ArchivalValidation"), "Missing archival details; operation=" + failure.Diagnostic.Operation + "; keys=" + string.Join(",", failure.Data.Keys.Cast<object>().Select(key => key.ToString())) + "; stack=" + failure.StackTrace);
        var observation = Assert.IsType<PdfArchivalValidation>(failure.Data["Fluent.Pdf.ArchivalValidation"]);
        Assert.True(observation.Completed);
        Assert.False(observation.IsCompliant);
        Assert.NotEmpty(observation.Findings);
    }
}
