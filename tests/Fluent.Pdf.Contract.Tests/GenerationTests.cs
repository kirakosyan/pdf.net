using System.Text;
using System.Threading;
using Fluent.Pdf.Fonts;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class GenerationTests
{
    internal static PdfFlowTextStyle Style() => new(new(FontTests.Read("original.ttf")), 10);
    internal static PdfFlowOptions Layout() => new(new PdfPageGeometry(new PdfPageBoxes(new PdfRect(0, 0, 200, 100))), PdfMargins.Uniform(10));
    private static PdfProviderRegistry Providers() => new(ManagedPdfProvider.Instance, new PdfSimpleTextShaper());
    private static PdfGenerationTemplate<string> Template(PdfFlowTextStyle style) => new("original-row", (context, value) => context.Paragraph(value, style));
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    [Fact]
    public void EventOrderAndEveryShapingWritingHookUseOneContext()
    {
        var contexts = new List<PdfOperationContext>();
        var events = new List<PdfGenerationEvent>();
        var shaper = new WitnessShaper(contexts);
        var writer = new WitnessWriter(contexts);
        var style = Style();
        var options = new PdfGenerationOptions(Layout(), "", observer: ev =>
        {
            contexts.Add(ev.OperationContext);
            events.Add(ev);
        });
        using var output = new MemoryStream();
        var report = PdfDataGeneration.Write(new[] { "AB" }, Template(style), options, new(shaper, writer), output);
        Assert.True(report.Succeeded);
        Assert.True(output.CanWrite);
        Assert.Equal(new[] { PdfGenerationEventKind.Starting, PdfGenerationEventKind.RecordStarting, PdfGenerationEventKind.TemplateStarting, PdfGenerationEventKind.TemplatePrepared, PdfGenerationEventKind.RecordPrepared, PdfGenerationEventKind.LayoutStarting, PdfGenerationEventKind.LayoutPrepared, PdfGenerationEventKind.PagePrepared, PdfGenerationEventKind.SaveStarting, PdfGenerationEventKind.SavePrepared, PdfGenerationEventKind.Prepared }, events.Select(ev => ev.Kind));
        Assert.All(contexts, c => Assert.Same(contexts[0], c));
        Assert.Equal(Enumerable.Range(0, events.Count), events.Select(ev => ev.Ordinal));
        var page = Assert.Single(events, ev => ev.PageIndex >= 0);
        Assert.NotNull(page.Page);
        Assert.Equal(1, page.Page!.LineCount);
        Assert.Equal(1, page.PageCount);
        output.Position = 0;
        using var doc = Pdf.Open(output);
        Assert.Equal("AB", PdfTextExtraction.ReadPage(doc.CreateSnapshot(), 0).Text);
    }

    [Fact]
    public void FinalPageHooksAgreeWithActualPaginationAndContextsExpire()
    {
        var style = Style();
        PdfGenerationContext? captured = null;
        var pageIndices = new List<int>();
        var counts = new List<int>();
        var template = new PdfGenerationTemplate<string>("pages", (c, row) =>
        {
            captured = c;
            c.Paragraph(row, style);
        });
        var options = new PdfGenerationOptions(Layout(), "", observer: ev =>
        {
            if (ev.Kind == PdfGenerationEventKind.PagePrepared)
            {
                pageIndices.Add(ev.PageIndex);
                counts.Add(ev.PageCount);
            }
        });
        using var doc = PdfDataGeneration.Generate(Enumerable.Repeat("AB", 12), template, options, Providers());
        Assert.True(doc.Pages.Count > 1);
        Assert.Equal(Enumerable.Range(0, doc.Pages.Count), pageIndices);
        Assert.All(counts, count => Assert.Equal(doc.Pages.Count, count));
        Assert.True(doc.LastOperationReport!.Succeeded);
        Assert.Equal("Generation.ContextExpired", Error(() => captured!.Paragraph("BA", style)));
        Assert.Equal(12, captured!.RecordsPrepared);
    }

    [Fact]
    public void CapturedContextMutationInAHookLatchesEvenWhenTheHookCatchesIt()
    {
        var style = Style();
        PdfGenerationContext? captured = null;
        string? caught = null;
        var template = new PdfGenerationTemplate<string>("guard", (c, row) =>
        {
            captured = c;
            c.Paragraph(row, style);
        });
        var options = new PdfGenerationOptions(Layout(), "", observer: ev =>
        {
            if (ev.Kind == PdfGenerationEventKind.TemplatePrepared)
                caught = Error(() => captured!.Paragraph("BA", style));
        });
        using var output = new MemoryStream();
        Assert.Equal("Generation.ReentrantMutation", Error(() => PdfDataGeneration.Write(new[] { "AB" }, template, options, Providers(), output)));
        Assert.Equal("Generation.ReentrantMutation", caught);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void CrossThreadBuilderUseLatchesAStableError()
    {
        Exception? threadFailure = null;
        var style = Style();
        var template = new PdfGenerationTemplate<string>("thread", (c, row) =>
        {
            var thread = new Thread(() =>
            {
                try
                {
                    c.Paragraph(row, style);
                }
                catch (Exception failure)
                {
                    threadFailure = failure;
                }
            });
            thread.Start();
            thread.Join();
        });
        var failure = Assert.Throws<PdfException>(() => PdfDataGeneration.Generate(new[] { "AB" }, template, new(Layout(), ""), Providers()));
        Assert.Equal("Generation.CallbackThread", failure.Diagnostic.Code);
        Assert.Same(threadFailure, failure);
    }

    [Fact]
    public void HookExceptionsPropagateAndLatePreparedFailurePreservesAtomicDestination()
    {
        var marker = new InvalidOperationException("original generation hook");
        var style = Style();
        string path = Path.Combine(Path.GetTempPath(), "fluent-generation-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllText(path, "retained");
        try
        {
            var options = new PdfGenerationOptions(Layout(), "", observer: ev =>
            {
                if (ev.Kind == PdfGenerationEventKind.SavePrepared)
                    throw marker;
            });
            var failure = Assert.Throws<InvalidOperationException>(() => PdfDataGeneration.WriteFile(new[] { "AB" }, Template(style), options, Providers(), path));
            Assert.Same(marker, failure);
            Assert.Equal("retained", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void InfiniteSourceHasOneLookaheadAndIsDisposedOnRecordLimit()
    {
        int pulls = 0, renders = 0;
        bool disposed = false;
        IEnumerable<string> Rows()
        {
            try
            {
                while (true)
                {
                    pulls++;
                    yield return "AB";
                }
            }
            finally
            {
                disposed = true;
            }
        }

        var template = new PdfGenerationTemplate<string>("bounded", (c, row) => renders++);
        Assert.Equal("Generation.RecordLimit", Error(() => PdfDataGeneration.Generate(Rows(), template, new(Layout(), "", maximumRecords: 2), Providers())));
        Assert.Equal(3, pulls);
        Assert.Equal(2, renders);
        Assert.True(disposed);
    }

    private sealed class Node
    {
        internal readonly string Text;
        internal Node? Next;
        internal Node(string text)
        {
            Text = text;
        }
    }

    [Fact]
    public void FiniteReferenceAndValueRecursionWorkAndReferenceCyclesReject()
    {
        var style = Style();
        PdfGenerationTemplate<Node>? tree = null;
        tree = new("tree", (c, node) =>
        {
            c.Paragraph(node.Text, style);
            if (node.Next != null)
                c.Apply(tree!, node.Next);
        });
        var first = new Node("AB")
        {
            Next = new Node("BA")
        };
        using var doc = PdfDataGeneration.Generate(new[] { first }, tree, new(Layout(), ""), Providers());
        Assert.Equal("ABBA", PdfTextExtraction.ReadPage(doc.CreateSnapshot(), 0).RawText.Replace("\n", ""));
        first.Next = first;
        Assert.Equal("Generation.TemplateCycle", Error(() => PdfDataGeneration.Generate(new[] { first }, tree, new(Layout(), ""), Providers())));
        PdfGenerationTemplate<int>? countdown = null;
        countdown = new("count", (c, value) =>
        {
            if (value > 0)
                c.Apply(countdown!, value - 1);
            else
                c.Paragraph("AB", style);
        });
        using var count = PdfDataGeneration.Generate(new[] { 3 }, countdown, new(Layout(), ""), Providers());
        Assert.Equal("AB", PdfTextExtraction.ReadPage(count.CreateSnapshot(), 0).Text);
    }

    [Fact]
    public void DataSelfCyclesAndBlockEventAndOperationCeilingsAreExplicit()
    {
        IEnumerable<object>? cycle = null;
        IEnumerable<object> Rows()
        {
            yield return cycle!;
        }

        cycle = Rows();
        Assert.Equal("Generation.DataCycle", Error(() => PdfDataGeneration.Generate(cycle, new PdfGenerationTemplate<object>("cycle", (c, row) =>
        {
        }), new(Layout(), ""), Providers())));
        var style = Style();
        var twice = new PdfGenerationTemplate<string>("two", (c, row) =>
        {
            c.Paragraph(row, style);
            c.Paragraph(row, style);
        });
        Assert.Equal("Generation.BlockLimit", Error(() => PdfDataGeneration.Generate(new[] { "AB" }, twice, new(Layout(), "", maximumBlocks: 1), Providers())));
        int events = 0;
        Assert.Equal("Generation.EventLimit", Error(() => PdfDataGeneration.Generate(new[] { "AB" }, Template(style), new(Layout(), "", observer: ev => events++, maximumEvents: 2), Providers())));
        Assert.Equal(2, events);
        Assert.Equal("Limit.ExpandedBytes", Error(() => PdfDataGeneration.Generate(new[] { "AB" }, Template(style), new(Layout(), ""), Providers(), new(limits: new(maximumExpandedBytes: 1)))));
        Assert.Equal("Limit.Objects", Error(() => PdfDataGeneration.Generate(new[] { "AB" }, Template(style), new(Layout(), ""), Providers(), new(limits: new(maximumObjects: 1)))));
        Assert.Equal("Limit.InputBytes", Error(() => PdfDataGeneration.Generate(new[] { "AB" }, Template(style), new(Layout(), ""), Providers(), new(limits: new(maximumInputBytes: 1)))));
    }

    private sealed class Arbitrary
    {
        public override string ToString() => throw new InvalidOperationException("Arbitrary formatting must not execute.");
    }

    [Fact]
    public void ExplicitCultureAndClosedScalarFormattingDoNotUseAmbientConversion()
    {
        var style = new PdfFlowTextStyle(new(PdfFont.Standard("Courier", new(ManagedFontProvider.Instance))), 10);
        string? formatted = null;
        var template = new PdfGenerationTemplate<decimal>("format", (c, value) =>
        {
            formatted = c.Format(value, "N2");
            c.Paragraph(formatted, style);
        });
        using var doc = PdfDataGeneration.Generate(new[] { 1234.5m }, template, new(Layout(), "de-DE"), Providers());
        Assert.Equal("1.234,50", formatted);
        Assert.Equal(formatted, PdfTextExtraction.ReadPage(doc.CreateSnapshot(), 0, providers: new(ManagedFontProvider.Instance)).Text);
        Assert.Equal("Table.ValueType", Error(() => PdfDataGeneration.Generate(new[] { new Arbitrary() }, new PdfGenerationTemplate<Arbitrary>("closed", (c, value) => c.Format(value)), new(Layout(), ""), Providers())));
    }

    [Fact]
    public void TypedTableBindingAndPageDecorationsShareTheRootContext()
    {
        var style = Style();
        var contexts = new List<PdfOperationContext>();
        var rows = new[]
        {
            "AB",
            "BA"
        };
        var columns = new[]
        {
            new PdfTableDataColumn<string>(PdfTableColumn.Auto(), row => row, style)
        };
        var template = new PdfGenerationTemplate<int>("table", (c, row) => c.AddTable(rows, columns));
        var pageTemplate = new PdfPageTemplate(footerHeight: 15, footer: page =>
        {
            contexts.Add(page.OperationContext);
            return PdfPageDecoration.Flow(new[] { new PdfFlowParagraph("AB", style) });
        });
        using var output = new MemoryStream();
        PdfDataGeneration.Write(new[] { 0 }, template, new(Layout(), "", pageTemplate: pageTemplate, observer: ev => contexts.Add(ev.OperationContext)), Providers(), output);
        Assert.NotEmpty(contexts);
        Assert.All(contexts, c => Assert.Same(contexts[0], c));
        output.Position = 0;
        using var doc = Pdf.Open(output);
        Assert.Contains("BA", PdfTextExtraction.ReadPage(doc.CreateSnapshot(), 0).Text);
    }

    [Fact]
    public void CancellationAndOutputFailureKeepTheExistingFileAndBorrowedStreamOpen()
    {
        var style = Style();
        Assert.Throws<OperationCanceledException>(() => PdfDataGeneration.Generate(new[] { "AB" }, Template(style), new(Layout(), ""), Providers(), cancellationToken: new CancellationToken(true)));
        using var output = new MemoryStream();
        Assert.Equal("Limit.OutputBytes", Error(() => PdfDataGeneration.Write(new[] { "AB" }, Template(style), new(Layout(), ""), Providers(), output, policy: new(limits: new(maximumOutputBytes: 1)))));
        Assert.True(output.CanWrite);
        string path = Path.Combine(Path.GetTempPath(), "fluent-generation-limit-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllText(path, "retained");
        try
        {
            Assert.Equal("Limit.OutputBytes", Error(() => PdfDataGeneration.WriteFile(new[] { "AB" }, Template(style), new(Layout(), ""), Providers(), path, policy: new(limits: new(maximumOutputBytes: 1)))));
            Assert.Equal("retained", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void ManyRecordsAreEnumeratedWithinTheDeclaredFiniteSource()
    {
        int rendered = 0, prepared = 0;
        var template = new PdfGenerationTemplate<int>("many", (c, value) => rendered++);
        using var document = PdfDataGeneration.Generate(Enumerable.Range(0, 1000), template, new(Layout(), "", observer: ev =>
        {
            if (ev.Kind == PdfGenerationEventKind.RecordPrepared)
                prepared = ev.RecordsPrepared;
        }, maximumRecords: 1000), Providers());
        Assert.Equal(1000, rendered);
        Assert.Equal(1000, prepared);
        Assert.True(document.LastOperationReport!.Succeeded);
    }

    [Fact]
    public void UnfinishedHookWorkerCannotPublishAnAtomicFile()
    {
        IDisposable? worker = null;
        var style = Style();
        string path = Path.Combine(Path.GetTempPath(), "fluent-generation-worker-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllText(path, "retained");
        try
        {
            var options = new PdfGenerationOptions(Layout(), "", observer: ev =>
            {
                if (ev.Kind == PdfGenerationEventKind.SavePrepared)
                    worker = ev.OperationContext.EnterScope(PdfResourceKind.Workers);
            });
            Assert.Equal("Operation.ScopeLeaked", Error(() => PdfDataGeneration.WriteFile(new[] { "AB" }, Template(style), options, Providers(), path)));
            Assert.Equal("retained", File.ReadAllText(path));
        }
        finally
        {
            worker?.Dispose();
            File.Delete(path);
        }
    }

    [Fact]
    public void BothLanguageConsumersUseThePublicTypedGenerationContract()
    {
        var style = Style();
        using var cs = ContractExamples.CSharp.ContractExample.GenerateData(new[] { "AB", "BA" }, Layout(), style, Providers());
        using var vb = ContractExamples.VisualBasic.ContractExample.GenerateData(new[] { "AB", "BA" }, Layout(), style, Providers());
        Assert.Equal("ABBA", PdfTextExtraction.ReadPage(cs.CreateSnapshot(), 0).RawText);
        Assert.Equal("ABBA", PdfTextExtraction.ReadPage(vb.CreateSnapshot(), 0).RawText);
    }

    private sealed class WitnessShaper : IPdfTextShaper
    {
        private readonly List<PdfOperationContext> _contexts;
        internal WitnessShaper(List<PdfOperationContext> contexts)
        {
            _contexts = contexts;
        }

        public PdfProviderDescriptor Descriptor { get; } = new("test.generation-shaper", "1.0", PdfCapability.ShapeText);

        public IReadOnlyList<PdfShapedGlyph> Shape(PdfShapingRequest request, PdfOperationContext context)
        {
            _contexts.Add(context);
            return new PdfSimpleTextShaper().Shape(request, context);
        }
    }

    private sealed class WitnessWriter : IPdfRevisionWriter
    {
        private readonly List<PdfOperationContext> _contexts;
        internal WitnessWriter(List<PdfOperationContext> contexts)
        {
            _contexts = contexts;
        }

        public PdfProviderDescriptor Descriptor { get; } = new("test.generation-writer", "1.0", PdfCapability.WriteDocument);
        public bool RequiresSeekableStream => false;

        public bool Supports(PdfWriteOptions options) => ((IPdfRevisionWriter)ManagedPdfProvider.Instance).Supports(options);
        public void Write(PdfDocumentSnapshot document, Stream output, PdfOperationContext context)
        {
            _contexts.Add(context);
            ((IPdfDocumentWriter)ManagedPdfProvider.Instance).Write(document, output, context);
        }
    }
}
