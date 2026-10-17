using Xunit;
using System.IO;

namespace Fluent.Pdf.Contract.Tests;

public sealed class ExampleTests
{
    [Fact]
    public void TheCompiledCSharpExampleRunsAgainstTheRealContractAssembly() =>
        Assert.Equal(2, ContractExamples.CSharp.ContractExample.Run());

    [Fact]
    public void TheCompiledVisualBasicExampleRunsAgainstTheRealContractAssembly() =>
        Assert.Equal(2, ContractExamples.VisualBasic.ContractExample.Run());

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothLanguageExamplesReturnEveryPermissiveLoss(bool visualBasic)
    {
        var provider = new ContractProvider { OnWriteContext = (_, stream, context) =>
        {
            context.Report(PolicyTests.Loss(0, 42));
            stream.WriteByte(1);
        } };
        var providers = new PdfProviderRegistry(provider);
        using var output = new MemoryStream();
        var report = visualBasic
            ? ContractExamples.VisualBasic.ContractExample.SaveWithReport(providers, output)
            : ContractExamples.CSharp.ContractExample.SaveWithReport(providers, output);
        Assert.True(report.Succeeded);
        Assert.Equal(PdfDiagnosticSeverity.Warning, Assert.Single(report.Diagnostics).Severity);
        Assert.Equal(0, report.Diagnostics[0].PageIndex);
        Assert.Equal(42, report.Diagnostics[0].ObjectId);
        Assert.Equal(new byte[] { 1 }, output.ToArray());
        Assert.True(output.CanWrite);
    }
}
