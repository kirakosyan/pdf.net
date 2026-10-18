using System.Globalization;
using System.Text;
using System.Threading;
using Fluent.Pdf.Fonts;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class InputConversionTests
{
    internal static PdfProviderRegistry Providers() => new(ManagedPdfProvider.Instance, new PdfSimpleTextShaper(), new ManagedFontProvider());
    internal static PdfInputConversionOptions Options(int bytes = 4194304, int nodes = 100000, string culture = "", Action<PdfGenerationEvent>? observer = null)
    {
        var style = new PdfFlowTextStyle(new(PdfFont.Standard("Courier", new(new ManagedFontProvider()))), 10);
        return new(new(new(new PdfPageGeometry(new PdfPageBoxes(new PdfRect(0, 0, 300, 400))), PdfMargins.Uniform(10)), culture, observer: observer), new Dictionary<string, PdfFlowTextStyle> { ["body"] = style, ["heading"] = new(style.Fonts, 14), ["code"] = style, ["strong"] = style, ["emphasis"] = style }, maximumInputBytes: bytes, maximumNodes: nodes);
    }

    private static MemoryStream Input(string value) => new(new UTF8Encoding(false, true).GetBytes(value));
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    private static string Text(PdfDocument document) => string.Join("\n", Enumerable.Range(0, document.Pages.Count).Select(p => PdfTextExtraction.ReadPage(document.CreateSnapshot(), p, providers: Providers()).Text));
    [Theory]
    [InlineData("utf8")]
    [InlineData("utf16le")]
    [InlineData("utf16be")]
    [InlineData("utf32le")]
    [InlineData("utf32be")]
    public void StrictBomUnicodeStartsAtBorrowedCurrentPositionAndPreservesParagraphs(string encoding)
    {
        Encoding enc = encoding == "utf8" ? new UTF8Encoding(true, true) : encoding == "utf16le" ? new UnicodeEncoding(false, true, true) : encoding == "utf16be" ? new UnicodeEncoding(true, true, true) : new UTF32Encoding(encoding == "utf32be", true, true);
        var raw = new byte[]
        {
            99
        }.Concat(enc.GetPreamble()).Concat(enc.GetBytes("AB\r\n\r\nBA")).ToArray();
        using var input = new MemoryStream(raw);
        input.Position = 1;
        using var doc = PdfInputConversion.Generate(input, PdfInputFormat.Text, Options(), Providers());
        Assert.Equal("ABBA", Text(doc));
        var extracted = PdfTextExtraction.ReadPage(doc.CreateSnapshot(), 0, providers: Providers());
        Assert.Equal(new[] { "AB", "BA" }, extracted.Lines.Select(line => string.Concat(line.Words.Select(word => word.Text))));
        Assert.True(extracted.Lines[1].Bounds.Y > extracted.Lines[0].Bounds.Y);
        Assert.True(input.CanRead);
        Assert.Equal(raw.Length, input.Position);
    }

    [Fact]
    public void MarkdownRetainsHeadingsListsTablesCodeAndPassiveNestedLinks()
    {
        const string source = "# AB\n\n**AB** *BA* `AB`\n\n- [AB](https://example.test/list)\n- BA\n\n| AB | BA |\n| --- | --- |\n| [BA](https://example.test/cell) | AB |\n\n```\nAB  BA\n```";
        using var input = Input(source);
        using var doc = PdfInputConversion.Generate(input, PdfInputFormat.Markdown, Options(), Providers());
        var links = doc.CreateSnapshot().Pages.SelectMany(p => p.Links).ToArray();
        Assert.Equal(2, links.Length);
        Assert.Equal(new[] { "https://example.test/list", "https://example.test/cell" }, links.Select(x => x.Uri));
        Assert.All(links, link =>
        {
            Assert.Equal(-1, link.TargetPageIndex);
            Assert.True(link.Bounds.Width > 0);
            Assert.True(link.Bounds.Height > 0);
            Assert.InRange(link.Bounds.X, 10, 290);
            Assert.InRange(link.Bounds.Y, 10, 390);
        });
        using var bytes = new MemoryStream();
        doc.Save(bytes);
        bytes.Position = 0;
        using var read = Pdf.Open(bytes);
        Assert.Equal(2, PdfNavigation.Inspect(read.CreateSnapshot()).Actions.Count(a => a.Subtype == "URI"));
        string extracted = Text(read);
        Assert.Contains("AB  BA", extracted);
        Assert.DoesNotContain("example.test", extracted);
        Assert.DoesNotContain("**", extracted);
        PdfFont.Run("InspectSource", "original.input.inspection", null, default, context =>
        {
            using var graph = new PdfGraphEdit(read.CreateSnapshot(), new(), context);
            Assert.True(CountRole(graph, "Code") > 0);
            Assert.True(CountRole(graph, "Strong") > 0);
            Assert.True(CountRole(graph, "Em") > 0);
        }, PdfCapability.ReadDocument);
    }

    private static int CountRole(PdfGraphEdit graph, string role)
    {
        int total = 0;
        foreach (var id in graph.Edit.ActiveIds(graph.Context))
        {
            var value = graph.Edit.Resolve(id);
            if (value.Kind != PdfValueKind.Stream || !value.Entries.TryGetValue("Subtype", out var subtype) || subtype.Kind != PdfValueKind.Name || subtype.NameValue != "Form")
                continue;
            var bytes = PdfExtractionData.Read(value, graph, Providers());
            var program = PdfContentProgram.ParseCore(bytes, PdfContentKind.Page, PdfUnknownOperatorPolicy.Reject, null, graph.Context);
            total += program.Operations.Count(o => o.Operation == PdfOperator.BeginMarkedContent && o.Operands[0].NameValue == role);
        }

        return total;
    }

    [Theory]
    [InlineData(PdfInputFormat.Json)]
    [InlineData(PdfInputFormat.Xml)]
    public void VersionedSchemasBindClosedScalarsWithExplicitCulture(PdfInputFormat format)
    {
        string source = format == PdfInputFormat.Json ? "{\"version\":1,\"data\":{\"price\":1234.5},\"blocks\":[{\"type\":\"paragraph\",\"text\":\"{{price:N2}}\"},{\"type\":\"list\",\"items\":[\"AB\",\"BA\"],\"ordered\":true},{\"type\":\"table\",\"rows\":[[\"AB\",\"BA\"],[\"BA\",\"AB\"]],\"headers\":1}]}" : "<document version=\"1\"><data><value name=\"price\" type=\"decimal\">1234.5</value></data><blocks><paragraph>{{price:N2}}</paragraph><list ordered=\"true\"><item>AB</item><item>BA</item></list><table headers=\"1\"><row><cell>AB</cell><cell>BA</cell></row><row><cell>BA</cell><cell>AB</cell></row></table></blocks></document>";
        using var input = Input(source);
        using var doc = PdfInputConversion.Generate(input, format, Options(culture: "de-DE"), Providers());
        Assert.Contains("1.234,50", Text(doc));
        Assert.DoesNotContain("{{", Text(doc));
    }

    [Theory]
    [InlineData(PdfInputFormat.Json, "{\"version\":1,\"extra\":42,\"blocks\":[{\"type\":\"paragraph\",\"text\":\"AB\"}]}")]
    [InlineData(PdfInputFormat.Xml, "<document version=\"1\" extra=\"42\"><blocks><paragraph>AB</paragraph></blocks></document>")]
    [InlineData(PdfInputFormat.Xml, "<document version=\"1\"><blocks><paragraph type=\"code\">AB</paragraph></blocks></document>")]
    [InlineData(PdfInputFormat.Xml, "<document version=\"1\"><blocks><paragraph><run text=\"ignored\">AB</run></paragraph></blocks></document>")]
    public void UnknownMembersAreStrictFailuresAndEveryPermissiveLossIsReported(PdfInputFormat format, string source)
    {
        using var input = Input(source);
        Assert.Equal("Input.UnknownField", Error(() => PdfInputConversion.Generate(input, format, Options(), Providers())));
        input.Position = 0;
        using var doc = PdfInputConversion.Generate(input, format, Options(), Providers(), new(PdfProcessingMode.Permissive));
        Assert.Contains(doc.LastOperationReport!.Diagnostics, d => d.Code == "Input.UnknownField" && d.Severity == PdfDiagnosticSeverity.Warning);
        Assert.Equal("AB", Text(doc));
    }

    [Theory]
    [InlineData("<!DOCTYPE document [<!ENTITY x SYSTEM 'file:///never/read'>]><document version=\"1\"><blocks><paragraph>&x;</paragraph></blocks></document>")]
    [InlineData("<!DOCTYPE document SYSTEM 'https://example.test/never'><document version=\"1\"><blocks/></document>")]
    [InlineData("<?xml-stylesheet href='file:///never/read'?><document version=\"1\"><blocks/></document>")]
    public void XmlDtdExternalEntitiesAndProcessingInstructionsNeverResolve(string source)
    {
        using var input = Input(source);
        Assert.StartsWith("Input.", Error(() => PdfInputConversion.Generate(input, PdfInputFormat.Xml, Options(), Providers(), new(PdfProcessingMode.Permissive))));
    }

    [Theory]
    [InlineData("file:///never/read")]
    [InlineData("javascript:never()")]
    [InlineData("data:text/plain,never")]
    [InlineData("https://user:password@example.test/")]
    public void UnsafeLinksFailEvenInPermissiveMode(string uri)
    {
        using var input = Input("[AB](" + uri + ")");
        Assert.Equal("Input.UnsafeUri", Error(() => PdfInputConversion.Generate(input, PdfInputFormat.Markdown, Options(), Providers(), new(PdfProcessingMode.Permissive))));
    }

    [Theory]
    [InlineData("{\"version\":1,\"version\":1,\"blocks\":[]}")]
    [InlineData("{\"version\":01,\"blocks\":[]}")]
    [InlineData("{\"version\":1,\"blocks\":[],}")]
    [InlineData("{\"version\":1,\"blocks\":[{\"type\":\"paragraph\",\"text\":\"\\ud800\"}]}")]
    [InlineData("{\"version\":1,\"data\":{\"x\":[1]},\"blocks\":[]}")]
    [InlineData("{\"version\":1,\"data\":{\"x\":1e-100},\"blocks\":[]}")]
    [InlineData("{\"version\":1,\"data\":{\"x\":0.12345678901234567890123456789},\"blocks\":[]}")]
    [InlineData("{\"version\":1,\"data\":{\"x\":1e99999999999999999},\"blocks\":[]}")]
    [InlineData("{\"version\":1,\"blocks\":[{\"type\":\"paragraph\",\"text\":\"{{missing}}\"}]}")]
    public void MalformedOrAmbiguousJsonAndBindingsHaveStableDiagnostics(string source)
    {
        using var input = Input(source);
        Assert.StartsWith("Input.", Error(() => PdfInputConversion.Generate(input, PdfInputFormat.Json, Options(), Providers())));
        Assert.True(input.CanRead);
    }

    [Fact]
    public void BoundariesCancellationMalformedUnicodeAndAtomicFailurePreserveOwnership()
    {
        using var input = Input("AB");
        Assert.Equal("Input.InputLimit", Error(() => PdfInputConversion.Generate(input, PdfInputFormat.Text, Options(bytes: 1), Providers())));
        using var nodes = Input("{\"version\":1,\"blocks\":[]}");
        Assert.Equal("Input.NodeLimit", Error(() => PdfInputConversion.Generate(nodes, PdfInputFormat.Json, Options(nodes: 1), Providers())));
        using var invalid = new MemoryStream(new byte[] { 0xff });
        Assert.Equal("Input.Unicode", Error(() => PdfInputConversion.Generate(invalid, PdfInputFormat.Text, Options(), Providers())));
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        input.Position = 0;
        Assert.ThrowsAny<OperationCanceledException>(() => PdfInputConversion.Generate(input, PdfInputFormat.Text, Options(), Providers(), cancellationToken: cancellation.Token));
        var dir = Path.Combine(Path.GetTempPath(), "pdf-input-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var file = Path.Combine(dir, "retained.bin");
        File.WriteAllText(file, "retained");
        try
        {
            input.Position = 0;
            Assert.Throws<InvalidOperationException>(() => PdfInputConversion.WriteFile(input, PdfInputFormat.Text, Options(observer: ev =>
            {
                if (ev.Kind == PdfGenerationEventKind.SavePrepared)
                    throw new InvalidOperationException("original-hook");
            }), Providers(), file));
            Assert.Equal("retained", File.ReadAllText(file));
            Assert.Single(Directory.GetFiles(dir));
            Assert.True(input.CanRead);
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(dir);
        }
    }

    [Fact]
    public void UnsupportedMarkdownIsDiagnosticAndMissingStylesOrResourcesReject()
    {
        using var input = Input("> AB");
        Assert.Equal("Input.MarkdownBlock", Error(() => PdfInputConversion.Generate(input, PdfInputFormat.Markdown, Options(), Providers())));
        input.Position = 0;
        using var doc = PdfInputConversion.Generate(input, PdfInputFormat.Markdown, Options(), Providers(), new(PdfProcessingMode.Permissive));
        Assert.Single(doc.LastOperationReport!.Diagnostics);
        using var drawing = Input("{\"version\":1,\"blocks\":[{\"type\":\"drawing\",\"resource\":\"file:///never/read\"}]}");
        Assert.Equal("Input.Resource", Error(() => PdfInputConversion.Generate(drawing, PdfInputFormat.Json, Options(), Providers())));
        using var code = Input("`AB`");
        var options = Options();
        var onlyBody = new PdfInputConversionOptions(options.Generation, new Dictionary<string, PdfFlowTextStyle> { ["body"] = options.Styles["body"] });
        Assert.Equal("Input.Style", Error(() => PdfInputConversion.Generate(code, PdfInputFormat.Markdown, onlyBody, Providers())));
    }

    [Fact]
    public void StreamWritingHooksAndProvidersShareOneContextAndBothConsumersExecute()
    {
        var contexts = new List<PdfOperationContext>();
        var options = Options(observer: ev => contexts.Add(ev.OperationContext));
        using var input = Input("[AB](https://example.test/)");
        using var output = new MemoryStream();
        var writer = new WitnessWriter(contexts);
        var providers = new PdfProviderRegistry(writer, new PdfSimpleTextShaper(), new ManagedFontProvider());
        var report = PdfInputConversion.Write(input, PdfInputFormat.Markdown, options, providers, output);
        Assert.True(report.Succeeded);
        Assert.All(contexts, c => Assert.Same(contexts[0], c));
        Assert.True(input.CanRead);
        Assert.True(output.CanWrite);
        using var cs = Input("AB");
        using var csOutput = new MemoryStream();
        Assert.True(ContractExamples.CSharp.ContractExample.ConvertInput(cs, PdfInputFormat.Text, options, Providers(), csOutput).Succeeded);
        using var vb = Input("BA");
        using var vbOutput = new MemoryStream();
        Assert.True(ContractExamples.VisualBasic.ContractExample.ConvertInput(vb, PdfInputFormat.Text, options, Providers(), vbOutput).Succeeded);
    }

    [Fact]
    public async System.Threading.Tasks.Task ExactInputCeilingLargeFiniteInputAndIndependentJobsRemainBounded()
    {
        using var exact = Input("AB");
        using var small = PdfInputConversion.Generate(exact, PdfInputFormat.Text, Options(bytes: 2), Providers());
        Assert.Equal("AB", Text(small));
        exact.Position = 0;
        Assert.Equal("Limit.ExpandedBytes", Error(() => PdfInputConversion.Generate(exact, PdfInputFormat.Text, Options(), Providers(), new(limits: new(maximumExpandedBytes: 1)))));
        using var large = Input(string.Join("\n\n", Enumerable.Repeat("AB", 200)));
        using var document = PdfInputConversion.Generate(large, PdfInputFormat.Text, Options(nodes: 200), Providers());
        Assert.True(document.Pages.Count > 1);
        Assert.Equal(400, Text(document).Count(ch => ch == 'A' || ch == 'B'));
        var options = Options();
        var results = await System.Threading.Tasks.Task.WhenAll(Enumerable.Range(0, 4).Select(_ => System.Threading.Tasks.Task.Run(() =>
        {
            using var source = Input("AB");
            using var job = PdfInputConversion.Generate(source, PdfInputFormat.Text, options, Providers());
            return Text(job);
        })));
        Assert.All(results, result => Assert.Equal("AB", result));
    }

    [Theory]
    [InlineData("https://bücher.example.test/path?q=é", "https://xn--bcher-kva.example.test/path?q=%C3%A9")]
    [InlineData("https://[::1]/path", "https://[::1]/path")]
    [InlineData("mailto:test@example.test", "mailto:test@example.test")]
    public void PassiveUrisUseAsciiActionsWithEscapedPathsAndInternationalHosts(string source, string expected)
    {
        using var input = Input("[AB](" + source + ")");
        using var output = new MemoryStream();
        PdfInputConversion.Write(input, PdfInputFormat.Markdown, Options(), Providers(), output);
        output.Position = 0;
        using var doc = Pdf.Open(output);
        var action = Assert.Single(PdfNavigation.Inspect(doc.CreateSnapshot()).Actions, a => a.Subtype == "URI");
        var bytes = action.Definition.Entries["URI"].StringBytes;
        Assert.All(bytes, value => Assert.InRange(value, (byte)0, (byte)127));
        Assert.Equal(expected, Encoding.ASCII.GetString(bytes.ToArray()));
    }

    [Fact]
    public void NamedDrawingResourcesAreCapturedExplicitlyAndDoNotOpenSourcePaths()
    {
        var drawing = PdfDrawing.Create(new[] { new PdfContentOperation(PdfOperator.Rectangle, PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(10), PdfValue.Integer(10)), new PdfContentOperation(PdfOperator.Fill) });
        var basis = Options();
        var options = new PdfInputConversionOptions(basis.Generation, basis.Styles, new Dictionary<string, PdfInputDrawing> { ["original"] = new(drawing, new(0, 0, 10, 10), 20, 20) });
        using var input = Input("{\"version\":1,\"blocks\":[{\"type\":\"drawing\",\"resource\":\"original\"},{\"type\":\"paragraph\",\"text\":\"AB\"}]}");
        using var output = new MemoryStream();
        Assert.True(PdfInputConversion.Write(input, PdfInputFormat.Json, options, Providers(), output).Succeeded);
        output.Position = 0;
        using var read = Pdf.Open(output);
        Assert.Equal("AB", Text(read));
    }

    private sealed class WitnessWriter : IPdfRevisionWriter
    {
        private readonly List<PdfOperationContext> _contexts;
        internal WitnessWriter(List<PdfOperationContext> contexts)
        {
            _contexts = contexts;
        }

        public PdfProviderDescriptor Descriptor { get; } = new("original.input.writer", "1.0", PdfCapability.WriteDocument);
        public bool RequiresSeekableStream => false;

        public bool Supports(PdfWriteOptions options) => ((IPdfRevisionWriter)ManagedPdfProvider.Instance).Supports(options);
        public void Write(PdfDocumentSnapshot document, Stream output, PdfOperationContext context)
        {
            _contexts.Add(context);
            ((IPdfDocumentWriter)ManagedPdfProvider.Instance).Write(document, output, context);
        }
    }
}
