using System.IO;
using System.Text;
using Fluent.Pdf.Compliance;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ArchivalReportTests
{
    // Original protocol fixtures for bridge validation; these do not replace real-engine conformance tests in ArchivalTests.
    private const string Complete = "<report><buildInformation><releaseDetails id='core' version='1.28.2'/><releaseDetails id='validation-model' version='1.28.2'/></buildInformation><jobs><job><validationReport jobEndStatus='normal' profileName='PDF/A-2B validation profile' isCompliant='true'><details passedRules='100' failedRules='0' passedChecks='200' failedChecks='0'/></validationReport></job></jobs><batchSummary totalJobs='1' failedToParse='0' encrypted='0' outOfMemory='0' veraExceptions='0'><validationReports compliant='1' nonCompliant='0' failedJobs='0'>1</validationReports></batchSummary></report>";
    private static VeraPdfReportValidator Provider(string xml) => new((_, input, report, context) =>
    {
        var bytes = Encoding.UTF8.GetBytes(xml);
        context.Consume(PdfResourceKind.ExpandedBytes, bytes.Length);
        report.Write(bytes, 0, bytes.Length);
    });
    [Theory]
    [InlineData("version", "Archival.ReportEngine")]
    [InlineData("profile", "Archival.ReportProfile")]
    [InlineData("count", "Archival.ReportCount")]
    [InlineData("contradictory", "Archival.ReportCompleteness")]
    [InlineData("duplicate", "Archival.ReportCompleteness")]
    [InlineData("root", "Archival.ReportSchema")]
    [InlineData("dtd", "Archival.ReportXml")]
    public void AmbiguousIncompleteOrProhibitedReportsFailClosed(string change, string code)
    {
        string xml = change switch
        {
            "version" => Complete.Replace("1.28.2", "0.0.0"),
            "profile" => Complete.Replace("PDF/A-2B", "PDF/A-3B"),
            "count" => Complete.Replace("passedRules='100'", "passedRules='-1'"),
            "contradictory" => Complete.Replace("failedChecks='0'", "failedChecks='1'"),
            "duplicate" => Complete.Replace("<jobs>", "<jobs><job/>"),
            "root" => Complete.Replace("<report>", "<other>").Replace("</report>", "</other>"),
            _ => "<!DOCTYPE report [<!ENTITY source SYSTEM 'file:///must-not-read'>]>" + Complete
        };
        using var input = new MemoryStream(new byte[] { 1, 2, 3 });
        Assert.Equal(code, Assert.Throws<PdfException>(() => PolicyTests.Run(context => Provider(xml).Validate(PdfArchivalProfile.A2B, input, context))).Diagnostic.Code);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void ReportScopeAndExpandedBudgetsLatchRatherThanReturningAResult()
    {
        using var input = new MemoryStream(new byte[] { 1 });
        Assert.Equal("Limit.Depth", Assert.Throws<PdfException>(() => PolicyTests.Run(context => Provider(Complete).Validate(PdfArchivalProfile.A2B, input, context), new(maximumDepth: 2))).Diagnostic.Code);
        input.Position = 0;
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PolicyTests.Run(context => Provider(Complete).Validate(PdfArchivalProfile.A2B, input, context), new(maximumExpandedBytes: 64))).Diagnostic.Code);
    }

    private sealed class WrongBinding : IPdfArchivalValidator
    {
        internal int DescriptorReads;
        public PdfProviderDescriptor Descriptor
        {
            get
            {
                DescriptorReads++;
                return new("original.wrong-binding", "1", PdfCapability.ValidateArchival);
            }
        }

        public PdfArchivalValidation Validate(PdfArchivalProfile profile, Stream input, PdfOperationContext context) => new(profile, new string ('0', 64), true, true, Array.Empty<PdfArchivalFinding>());
    }

    [Fact]
    public void AResultForOtherBytesCannotAuthorizeAnArchivalVersionDowngrade()
    {
        using var source = ArchivalTests.DecorativeDrawing(PdfArchivalProfile.A1B);
        using var output = new MemoryStream();
        var provider = new WrongBinding();
        var registry = new PdfProviderRegistry(provider);
        Assert.Equal("Archival.ValidatorBinding", Assert.Throws<PdfException>(() => PdfArchival.Convert(source.CreateSnapshot(), output, new(PdfArchivalProfile.A1B), registry)).Diagnostic.Code);
        Assert.Equal(1, provider.DescriptorReads);
        Assert.Equal(0, output.Length);
        using var input = new MemoryStream();
        source.Save(input);
        input.Position = 0;
        using var edit = new PdfTransaction(PdfSyntax.Read(input));
        Assert.Equal("Writer.VersionDowngrade", Assert.Throws<PdfException>(() => edit.Save(output, new(version: "1.4"))).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void DeclaredComplianceCannotHideIncompleteValidationOrViolations()
    {
        Assert.Throws<ArgumentException>(() => new PdfArchivalValidation(PdfArchivalProfile.A2B, new string ('0', 64), false, true, Array.Empty<PdfArchivalFinding>()));
        Assert.Throws<ArgumentException>(() => new PdfArchivalValidation(PdfArchivalProfile.A2B, new string ('0', 64), true, true, new[] { new PdfArchivalFinding("original", "original", "violation") }));
    }
}
