using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class ExampleTests
{
    [Fact]
    public void TheCompiledCSharpExampleRunsAgainstTheRealContractAssembly() =>
        Assert.Equal(2, ContractExamples.CSharp.ContractExample.Run());

    [Fact]
    public void TheCompiledVisualBasicExampleRunsAgainstTheRealContractAssembly() =>
        Assert.Equal(2, ContractExamples.VisualBasic.ContractExample.Run());
}
