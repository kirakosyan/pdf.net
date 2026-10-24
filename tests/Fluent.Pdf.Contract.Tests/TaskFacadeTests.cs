using System.IO;
using System.Threading.Tasks;
using Fluent.Pdf.Formats;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class TaskFacadeTests
{
    private static PdfProviderRegistry Providers() => new(new PdfSimpleTextShaper());
    private static PdfFlowOptions Layout() => new(new(new(new(0, 0, 120, 100))), PdfMargins.Uniform(5));
    private static PdfFlowBlock[] Blocks() => new PdfFlowBlock[] { new PdfFlowParagraph("office office", new(new(TextTests.Read("latin.ttf")), 12)) };
    private static byte[] Save(PdfDocument document) { using var output = new MemoryStream(); document.Save(output); return output.ToArray(); }
    private static string Artifact(string name) { string directory = Path.Combine(AppContext.BaseDirectory, "artifacts", "task-facades"); Directory.CreateDirectory(directory); return Path.Combine(directory, name); }

    [Fact]
    public void GenerationMatchesCoreBytesAndUsesOneOperationBudget()
    {
        var providers = Providers(); var tasks = new PdfTasks(providers);
        var flow = PdfFlow.Layout(Blocks(), Layout(), providers);
        using var direct = flow.CreateDocument(); byte[] expected = Save(direct);
        using var output = new MemoryStream(); var report = tasks.Generate(Blocks(), Layout(), output);
        Assert.Equal(expected, output.ToArray()); Assert.True(output.CanWrite);
        Assert.Equal("GenerateTaskPdf", report.Operation);
        long used = report.Resources.Single(r => r.Resource == PdfResourceKind.ExpandedBytes).Consumed;
        Assert.True(used > flow.OperationReport.Resources.Single(r => r.Resource == PdfResourceKind.ExpandedBytes).Consumed);
        using var rejected = new MemoryStream(); rejected.WriteByte(42);
        var constrained = new PdfTasks(providers, new(limits: new(maximumExpandedBytes: used - 1)));
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => constrained.Generate(Blocks(), Layout(), rejected)).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, rejected.ToArray());
        File.WriteAllBytes(Artifact("generated.pdf"), output.ToArray()); File.WriteAllBytes(Artifact("direct.pdf"), expected);
    }

    [Fact]
    public void ExtractionAndOptimizationPreserveCoreResultsAndDiagnostics()
    {
        using var source = ExtractionTests.Fixture("BT /F1 10 Tf 20 100 Td <41808182> Tj ET"); var snapshot = source.CreateSnapshot();
        var tasks = new PdfTasks();
        var direct = PdfTextExtraction.ReadPage(snapshot, 0, policy: tasks.Policy); var facade = tasks.Extract(snapshot, 0);
        Assert.Equal(direct.RawText, facade.RawText); Assert.Equal(direct.Text, facade.Text);
        Assert.Equal(direct.Characters.Select(c => c.Quad.Bounds), facade.Characters.Select(c => c.Quad.Bounds));
        Assert.Equal(direct.OperationReport.Diagnostics.Select(d => d.Code), facade.OperationReport.Diagnostics.Select(d => d.Code));
        using var left = new MemoryStream(); using var right = new MemoryStream();
        var a = PdfOptimization.Write(snapshot, left, providers: tasks.Providers, policy: tasks.Policy); var b = tasks.Optimize(snapshot, right);
        Assert.Equal(a.OutputSha256, b.OutputSha256); Assert.Equal(left.ToArray(), right.ToArray());
        Assert.Equal(a.OperationReport.Diagnostics.Select(d => d.Code), b.OperationReport.Diagnostics.Select(d => d.Code));
        Assert.True(right.CanWrite); Assert.Equal("AԱ😀fi", tasks.Extract(snapshot, 0).RawText);
        File.WriteAllBytes(Artifact("optimized.pdf"), right.ToArray());
    }

    [Fact]
    public void ConversionAndMissingProvidersUseExistingCoreBehavior()
    {
        using var document = Pdf.Create().AddPage(new PdfPageSize(20, 10)); var source = document.CreateSnapshot();
        var options = new PdfConversionOptions(PdfConversionTarget.Png, new(72, 72));
        using var left = new MemoryStream(); using var right = new MemoryStream();
        var a = PdfConversions.Export(source, new[] { 0 }, left, options, new(new RasterViewTests.PatternRenderer()));
        var b = new PdfTasks(new(new RasterViewTests.PatternRenderer())).Convert(source, new[] { 0 }, right, options);
        Assert.Equal(left.ToArray(), right.ToArray()); Assert.Equal(a.Diagnostics.Select(d => d.Code), b.Diagnostics.Select(d => d.Code));
        using var missing = new MemoryStream(); missing.WriteByte(42);
        Assert.Throws<PdfProviderUnavailableException>(() => new PdfTasks().Convert(source, new[] { 0 }, missing, options));
        Assert.Equal(new byte[] { 42 }, missing.ToArray());
        File.WriteAllBytes(Artifact("converted.png"), right.ToArray());
    }

    [Fact]
    public void FormFillingMatchesCoreAndPreservesSource()
    {
        using var source = FormWorkflowTests.Source(); var snapshot = source.CreateSnapshot();
        var data = PdfFormData.Create(new[] { new PdfFormDataField("Agree", PdfValue.Name("Yes Exact")) });
        using var direct = PdfForms.FillData(snapshot, data); using var facade = new PdfTasks().Fill(snapshot, data);
        Assert.Equal(Save(direct), Save(facade));
        Assert.Equal("Yes Exact", PdfForms.Inspect(facade.CreateSnapshot()).Fields.Single(f => f.Name == "Agree").Value.NameValue);
        Assert.Equal("Off", PdfForms.Inspect(snapshot).Fields.Single(f => f.Name == "Agree").Value.NameValue);
        var invalid = PdfFormData.Create(new[] { new PdfFormDataField("Missing", PdfValue.Null) });
        var a = Assert.Throws<PdfException>(() => PdfForms.FillData(snapshot, invalid)); var b = Assert.Throws<PdfException>(() => new PdfTasks().Fill(snapshot, invalid));
        Assert.Equal(a.Diagnostic.Code, b.Diagnostic.Code);
        File.WriteAllBytes(Artifact("filled.pdf"), Save(facade));
    }

    [Fact]
    public void CollectionPluginsAreInstanceOwnedAndRetainCoreFailures()
    {
        PdfCollectionAdapters Adapter(byte value) => new(new[] { new KeyValuePair<string, PdfCollectionAdapter>("owned", (_, input, output, c) => { c.Checkpoint(); output.WriteByte(value); }) });
        var first = new PdfTasks(adapters: Adapter(1)); var second = new PdfTasks(adapters: Adapter(2));
        using var source = new MemoryStream(new byte[] { 5 });
        var collection = first.Collect(new[] { new KeyValuePair<string, Stream>("source.pdf", source) });
        using var one = first.Process(collection, "owned").Outcomes.Single().Output!.Open();
        using var two = second.Process(collection, "owned").Outcomes.Single().Output!.Open();
        Assert.Equal(1, one.ReadByte()); Assert.Equal(2, two.ReadByte()); Assert.True(source.CanRead);
        Assert.Equal("Collection.AdapterUnavailable", Assert.Throws<PdfException>(() => new PdfTasks().Process(collection, "owned")).Diagnostic.Code);
    }

    [Fact]
    public void GenerationFailuresAndCancellationPreserveAtomicDestination()
    {
        string path = Artifact("preserved.pdf"); File.WriteAllText(path, "original");
        Assert.Throws<PdfProviderUnavailableException>(() => new PdfTasks().GenerateFile(Blocks(), Layout(), path));
        Assert.Equal("original", File.ReadAllText(path));
        Assert.ThrowsAny<OperationCanceledException>(() => new PdfTasks(Providers()).GenerateFile(Blocks(), Layout(), path, cancellationToken: new(true)));
        Assert.Equal("original", File.ReadAllText(path));
        new PdfTasks(Providers()).GenerateFile(Blocks(), Layout(), path);
        using var input = File.OpenRead(path); using var read = Pdf.Open(input); Assert.Single(read.Pages);
    }

    [Fact]
    public async Task IndependentConcurrentCallsDoNotShareBudgetsOrOutput()
    {
        var tasks = new PdfTasks(Providers()); var blocks = Blocks(); var options = Layout();
        var jobs = Enumerable.Range(0, 4).Select(_ => Task.Run(() => { using var output = new MemoryStream(); tasks.Generate(blocks, options, output); return output.ToArray(); }));
        var values = await Task.WhenAll(jobs); Assert.All(values, value => Assert.Equal(values[0], value));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LanguageExamplesAndPreflightMatchDirectCalls(bool visualBasic)
    {
        using var output = new MemoryStream(); var tasks = new PdfTasks(Providers());
        if (visualBasic) ContractExamples.VisualBasic.ContractExample.GenerateTaskPdf(tasks, Blocks(), Layout(), output);
        else ContractExamples.CSharp.ContractExample.GenerateTaskPdf(tasks, Blocks(), Layout(), output);
        byte[] bytes = output.ToArray(); using var left = new MemoryStream(bytes); using var right = new MemoryStream(bytes);
        Assert.Equal(PdfPreflight.Inspect(left).SourceSha256, tasks.Inspect(right).SourceSha256);
        Assert.True(left.CanRead); Assert.True(right.CanRead);
        File.WriteAllBytes(Artifact(visualBasic ? "visual-basic.pdf" : "csharp.pdf"), bytes);
    }
}
