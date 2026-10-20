using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class StampTests
{
    private static PdfStampContent Shape() => PdfStampContent.FromDrawing(GraphicsTests.Rectangle(new(PdfColorSpace.Rgb, 1, 0, 0)), new(0, 0, 100, 100));
    private static PdfDocument Source() => Pdf.Create().AddPage(new PdfPageSize(300, 400)).AddPage(new PdfPageSize(300, 400));
    private static readonly PdfStampOptions Options = new(new(20, 30, 100, 80), opacity: 0.5, rotation: 15, columns: 2, rows: 2, horizontalGap: 5, verticalGap: 10);
    [Fact]
    public void ExactPageSelectionPersistsOwnedPlacementAndUpdatesWithoutMutatingTheSource()
    {
        using var source = Source();
        var stamp = Shape();
        using var applied = PdfStamps.Apply(source.CreateSnapshot(), "original", stamp, new[] { 1 }, Options);
        var inventory = PdfStamps.Inspect(applied.CreateSnapshot());
        var info = Assert.Single(inventory.Stamps);
        Assert.Equal(1, info.PageIndex);
        Assert.Equal("original", info.Identifier);
        Assert.Equal(PdfStampContentKind.Drawing, info.ContentKind);
        Assert.Equal(Options.Destination, info.Options.Destination);
        Assert.Equal(2, info.Options.Columns);
        Assert.Equal(2, info.Options.Rows);
        Assert.Equal(0.5, info.Options.Opacity);
        Assert.Equal(15, info.Options.Rotation);
        Assert.True(info.Options.Artifact);
        Assert.Empty(PdfStamps.Inspect(source.CreateSnapshot()).Stamps);
        using var updated = PdfStamps.Update(applied.CreateSnapshot(), "original", stamp, new(new(10, 10, 20, 20), PdfStampLayer.Background, artifact: false));
        var changed = Assert.Single(PdfStamps.Inspect(updated.CreateSnapshot()).Stamps);
        Assert.Equal(PdfStampLayer.Background, changed.Options.Layer);
        Assert.False(changed.Options.Artifact);
        Assert.Equal(PdfStampLayer.Foreground, info.Options.Layer);
        using var output = new MemoryStream();
        updated.Save(output);
        Assert.True(output.CanWrite);
        output.Position = 0;
        using var reopened = Pdf.Open(output);
        Assert.Single(PdfStamps.Inspect(reopened.CreateSnapshot()).Stamps);
        using var removed = PdfStamps.Remove(reopened.CreateSnapshot(), "original");
        Assert.Empty(PdfStamps.Inspect(removed.CreateSnapshot()).Stamps);
    }

    [Fact]
    public void BothLanguageConsumersUseThePublicTransactionalStampContract()
    {
        using var source = Source();
        var stamp = Shape();
        using var csharp = ContractExamples.CSharp.ContractExample.Stamp(source.CreateSnapshot(), "cs", stamp, new[] { 0 }, Options);
        using var visualBasic = ContractExamples.VisualBasic.ContractExample.Stamp(source.CreateSnapshot(), "vb", stamp, new[] { 1 }, Options);
        Assert.Equal("cs", Assert.Single(PdfStamps.Inspect(csharp.CreateSnapshot()).Stamps).Identifier);
        Assert.Equal(1, Assert.Single(PdfStamps.Inspect(visualBasic.CreateSnapshot()).Stamps).PageIndex);
    }

    [Fact]
    public void InspectionReadsOwnedScopesUnderTheOriginalInputAndExpansionBudgets()
    {
        using var source = Source();
        using var applied = PdfStamps.Apply(source.CreateSnapshot(), "owned", Shape(), new[] { 0 }, Options);
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => PdfStamps.Inspect(applied.CreateSnapshot(), new(limits: new(maximumInputBytes: 1)))).Diagnostic.Code);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfStamps.Inspect(applied.CreateSnapshot(), new(limits: new(maximumExpandedBytes: 1)))).Diagnostic.Code);
    }

    [Fact]
    public void RepeatedApplyRemoveCyclesDoNotAccumulateIsolationScopesOrTouchUnrelatedStamps()
    {
        using var initial = Source();
        var stamp = Shape();
        PdfDocument current = PdfStamps.Apply(initial.CreateSnapshot(), "retained", stamp, new[] { 0 }, new(new(0, 0, 10, 10)));
        try
        {
            for (int i = 0; i < 20; i++)
            {
                using var applied = PdfStamps.Apply(current.CreateSnapshot(), "temporary", stamp, new[] { 0 }, new(new(20, 20, 10, 10), i % 2 == 0 ? PdfStampLayer.Background : PdfStampLayer.Foreground));
                var removed = PdfStamps.Remove(applied.CreateSnapshot(), "temporary");
                current.Dispose();
                current = removed;
                Assert.Equal("retained", Assert.Single(PdfStamps.Inspect(current.CreateSnapshot()).Stamps).Identifier);
                var page = current.CreateSnapshot().Pages[0].Source!;
                var contents = GraphicsTests.Resolve(page.Document, page.Dictionary.Entries["Contents"]);
                Assert.Equal(3, contents.Items.Count); // Exactly one owned guard pair and its invocation on an originally blank page.
            }
        }
        finally
        {
            current.Dispose();
        }
    }

    [Fact]
    public void RemovalKeepsAnUnrelatedInvocationUsingTheSamePrivateResourceName()
    {
        using var source = Source();
        source.Pages[0].Content = PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.Transform, new PdfMatrix(0.5, 0, 0, 0.5, 0, 0).Value.Items.ToArray()) });
        using var applied = PdfStamps.Apply(source.CreateSnapshot(), "owned", Shape(), new[] { 0 }, new(new(20, 20, 40, 40)));
        using var aliased = Edit(applied, graph =>
        {
            var page = graph.Snapshot.Pages[0].Source!;
            var entries = graph.Copy(page.Dictionary);
            var contents = graph.Resolve(entries["Contents"]).Items.ToList();
            var program = PdfContentProgram.CreateCore(new[] { new PdfContentOperation(PdfOperator.SaveState), new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("FStamp")), new PdfContentOperation(PdfOperator.RestoreState) }, PdfContentKind.Page, graph.Context);
            contents.Add(PdfValue.Reference(graph.Edit.Add(program.ToStream(graph.Context))));
            entries["Contents"] = PdfValue.OwnedArray(contents);
            graph.Edit.Set(page.ObjectId, PdfValue.OwnedDictionary(entries));
        });
        using var removed = PdfStamps.Remove(aliased.CreateSnapshot(), "owned");
        Assert.Empty(PdfStamps.Inspect(removed.CreateSnapshot()).Stamps);
        var surviving = Assert.Single(PdfPageGraphics.Inspect(removed.CreateSnapshot(), 0).Occurrences, o => o.Definition.Entries.ContainsKey("FluentPdfStampOwner"));
        Assert.Equal(1, surviving.Transform.A); Assert.Equal(1, surviving.Transform.D);
    }

    [Theory]
    [InlineData("program")]
    [InlineData("binding")]
    [InlineData("scope")]
    public void ChangedOwnershipProgramsBindingsAndScopePairsRejectHeuristicRemoval(string mode)
    {
        using var source = Source();
        using var applied = PdfStamps.Apply(source.CreateSnapshot(), "owned", Shape(), new[] { 0 }, new(new(20, 20, 40, 40)));
        using var changed = Edit(applied, graph =>
        {
            var page = graph.Snapshot.Pages[0].Source!;
            if (mode == "binding")
            {
                var entries = graph.Copy(page.Dictionary);
                var resources = graph.Copy(graph.Get(page.EffectiveAttributes, "Resources"));
                var objects = graph.Copy(graph.Get(PdfValue.OwnedDictionary(resources), "XObject"));
                objects["FStamp"] = PdfValue.Reference(page.ObjectId);
                resources["XObject"] = PdfValue.OwnedDictionary(objects);
                entries["Resources"] = PdfValue.OwnedDictionary(resources);
                graph.Edit.Set(page.ObjectId, PdfValue.OwnedDictionary(entries));
                return;
            }

            foreach (var reference in graph.Resolve(page.Dictionary.Entries["Contents"]).Items)
            {
                var stream = graph.Resolve(reference);
                if (!stream.Entries.ContainsKey(mode == "scope" ? "FluentPdfStampScope" : "FluentPdfStamp"))
                    continue;
                using var input = stream.OpenRawStream();
                using var output = new MemoryStream();
                input.CopyTo(output);
                var builder = new PdfByteBuilder(graph.Context);
                foreach (byte b in output.ToArray().Concat(Encoding.ASCII.GetBytes("0 0 m\n")))
                    builder.Add(b);
                var data = builder.Build();
                graph.Edit.Set(reference.ReferenceId, PdfValue.StreamValue(stream.DictionaryView(), data, 0, data.Length));
                break;
            }
        });
        Assert.Equal("Stamp.Ownership", Assert.Throws<PdfException>(() => PdfStamps.Remove(changed.CreateSnapshot(), "owned")).Diagnostic.Code);
    }

    [Fact]
    public void TaggedSourcesAllowDecorationAndRejectUnassociatedSemanticContent()
    {
        using var input = new MemoryStream(HtmlPrintTests.Fixture());
        using var source = Pdf.Open(input);
        using var applied = PdfStamps.Apply(source.CreateSnapshot(), "decoration", Shape(), new[] { 0 }, new(new(10, 10, 20, 20)));
        Assert.True(Assert.Single(PdfStamps.Inspect(applied.CreateSnapshot()).Stamps).Options.Artifact);
        Assert.Equal("Stamp.StructureRequired", Assert.Throws<PdfException>(() => PdfStamps.Apply(source.CreateSnapshot(), "semantic", Shape(), new[] { 0 }, new(new(10, 10, 20, 20), artifact: false))).Diagnostic.Code);
        using var removed = PdfStamps.Remove(applied.CreateSnapshot(), "decoration");
        PdfFont.Run("InspectStampStructure", "fixture", null, default, c => PdfHtmlTagStructure.Validate(removed.CreateSnapshot(), new(), c));
    }

    [Fact]
    public void ShapedTextImageAndPageSourcesAreDetachedAndRetainTheirExplicitKinds()
    {
        var shaped = PdfText.Shape("AB", new(FontTests.Read("original.ttf")), new(new PdfSimpleTextShaper()));
        var text = PdfStampContent.FromText(shaped, 20);
        Assert.Equal(PdfStampContentKind.Text, text.Kind);
        using var samples = new MemoryStream(new byte[] { 255, 0, 0, 0, 255, 0 });
        var image = PdfImages.FromSamples(samples, new(2, 1, 3, 8), PdfColorSpace.Rgb);
        var stamp = PdfStampContent.FromImage(image);
        Assert.True(samples.CanRead);
        Assert.Equal(PdfStampContentKind.Image, stamp.Kind);
        using var target = Source();
        using var page = PdfStamps.Apply(target.CreateSnapshot(), "text", text, new[] { 0 }, new(new(20, 20, 100, 40), artifact: false));
        Assert.Equal("AB", PdfTextExtraction.ReadPage(page.CreateSnapshot(), 0).Text);
        var captured = PdfStampContent.FromPage(page.CreateSnapshot(), 0);
        Assert.Equal(PdfStampContentKind.PdfPage, captured.Kind);
        page.Dispose();
        using var result = PdfStamps.Apply(target.CreateSnapshot(), "captured", captured, new[] { 1 }, new(new(0, 0, 300, 400), artifact: false));
        Assert.Equal("AB", PdfTextExtraction.ReadPage(result.CreateSnapshot(), 1).Text);
    }

    [Fact]
    public void InvalidSelectionsIdsAndOptionsRejectBeforeReturningOutput()
    {
        using var source = Source();
        var stamp = Shape();
        foreach (var pages in new[]
        {
            Array.Empty<int>(),
            new[]
            {
                0,
                0
            },
            new[]
            {
                -1
            },
            new[]
            {
                2
            }
        }

        )
            Assert.Equal("Stamp.PageSelection", Assert.Throws<PdfException>(() => PdfStamps.Apply(source.CreateSnapshot(), "owned", stamp, pages, Options)).Diagnostic.Code);
        Assert.Throws<ArgumentException>(() => PdfStamps.Apply(source.CreateSnapshot(), "bad id", stamp, new[] { 0 }, Options));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfStampOptions(new(0, 0, 10, 10), opacity: double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfStampOptions(new(0, 0, 10, 10), columns: 101));
        using var applied = PdfStamps.Apply(source.CreateSnapshot(), "owned", stamp, new[] { 0 }, Options);
        Assert.Equal("Stamp.IdentifierExists", Assert.Throws<PdfException>(() => PdfStamps.Apply(applied.CreateSnapshot(), "owned", stamp, new[] { 1 }, Options)).Diagnostic.Code);
        Assert.Equal("Stamp.NotFound", Assert.Throws<PdfException>(() => PdfStamps.Remove(source.CreateSnapshot(), "missing")).Diagnostic.Code);
    }

    [Fact]
    public void BudgetsAndCancellationStaySharedAndLeaveTheSourceUnchanged()
    {
        using var source = Source();
        var stamp = Shape();
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfStamps.Apply(source.CreateSnapshot(), "owned", stamp, new[] { 0 }, Options, policy: new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        using var cancelled = new CancellationTokenSource();
        IEnumerable<int> Pages()
        {
            yield return 0;
            cancelled.Cancel();
            yield return 1;
        }

        Assert.Throws<OperationCanceledException>(() => PdfStamps.Apply(source.CreateSnapshot(), "owned", stamp, Pages(), Options, cancellationToken: cancelled.Token));
        Assert.Empty(PdfStamps.Inspect(source.CreateSnapshot()).Stamps);
    }

    [Fact]
    public void HtmlCaptureUsesTheExplicitProviderAndSharedLatchAndReleasesItsBorrowedOutput()
    {
        Assert.Equal("Provider.Unavailable", Assert.Throws<PdfProviderUnavailableException>(() => PdfStampContent.FromHtml(PdfHtmlSource.FromHtml("<p>A</p>"), new())).Diagnostic.Code);
        var provider = new HtmlFixture(1);
        var captured = PdfStampContent.FromHtml(PdfHtmlSource.FromHtml("<p>A</p>"), new(provider));
        Assert.Equal(PdfStampContentKind.Html, captured.Kind);
        Assert.Equal("original.stamp.html", captured.OperationReport.ProviderId);
        Assert.Throws<ObjectDisposedException>(() => provider.Output!.WriteByte(0));
        using var target = Source();
        using var stamped = PdfStamps.Apply(target.CreateSnapshot(), "html", captured, new[] { 0 }, Options);
        Assert.Equal(PdfStampContentKind.Html, Assert.Single(PdfStamps.Inspect(stamped.CreateSnapshot()).Stamps).ContentKind);
        var multiple = new HtmlFixture(2);
        Assert.Equal("Stamp.HtmlPageSelection", Assert.Throws<PdfException>(() => PdfStampContent.FromHtml(PdfHtmlSource.FromHtml("<p>A</p>"), new(multiple))).Diagnostic.Code);
        Assert.Throws<ObjectDisposedException>(() => multiple.Output!.WriteByte(0));
        var latched = new HtmlFixture(1)
        {
            Swallow = true
        };
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfStampContent.FromHtml(PdfHtmlSource.FromHtml("<p>A</p>"), new(latched))).Diagnostic.Code);
    }

    [Fact]
    public void ImagePixelsAndOwnerDependentPageSourcesFailExplicitly()
    {
        using var samples = new MemoryStream(new byte[] { 255, 0, 0, 0, 255, 0 });
        var image = PdfImages.FromSamples(samples, new(2, 1, 3, 8), PdfColorSpace.Rgb);
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfStampContent.FromImage(image, new(limits: new(maximumPixels: 1)))).Diagnostic.Code);
        using var input = new MemoryStream(HtmlPrintTests.Fixture());
        using var tagged = Pdf.Open(input);
        Assert.Equal("Structure.SourceEditPolicy", Assert.Throws<PdfException>(() => PdfStampContent.FromPage(tagged.CreateSnapshot(), 0)).Diagnostic.Code);
        using var source = Source();
        using var layers = Edit(source, graph =>
        {
            var entries = graph.Copy(graph.Catalog);
            entries["OCProperties"] = PdfValue.Dictionary(Array.Empty<KeyValuePair<string, PdfValue>>());
            graph.SetCatalog(entries);
        });
        Assert.Equal("Stamp.OptionalContentProfile", Assert.Throws<PdfException>(() => PdfStampContent.FromPage(layers.CreateSnapshot(), 0)).Diagnostic.Code);
    }

    [Fact]
    public void StandalonePageGroupsBecomeIsolatedFormsWithoutChangingTheirDeclaredColorSpace()
    {
        using var original = Source();
        using var grouped = Edit(original, graph =>
        {
            var page = graph.Snapshot.Pages[0].Source!; var entries = graph.Copy(page.Dictionary);
            entries["Group"] = PdfGraphicsNumbers.Dict(("S", PdfValue.Name("Transparency")), ("I", PdfValue.Boolean(false)), ("CS", PdfValue.Name("DeviceRGB")));
            graph.Edit.Set(page.ObjectId, PdfValue.OwnedDictionary(entries));
        });
        var captured = PdfStampContent.FromPage(grouped.CreateSnapshot(), 0);
        var group = captured.Resource.Definition.Entries["Group"];
        Assert.True(group.Entries["I"].BooleanValue); Assert.Equal("DeviceRGB", group.Entries["CS"].NameValue);
        Assert.False(grouped.Pages[0].Source!.Dictionary.Entries["Group"].Entries["I"].BooleanValue);
    }

    private sealed class HtmlFixture : IPdfHtmlRenderer
    {
        private readonly byte[] _bytes;
        internal bool Swallow;
        internal Stream? Output;
        internal HtmlFixture(int pages)
        {
            using var document = Pdf.Create();
            for (int i = 0; i < pages; i++)
                document.AddPage();
            using var output = new MemoryStream();
            document.Save(output);
            _bytes = output.ToArray();
        }

        public PdfProviderDescriptor Descriptor { get; } = new("original.stamp.html", "1", PdfCapability.RenderHtml);

        public void Render(PdfHtmlSource source, PdfHtmlOptions options, Stream pdfOutput, PdfOperationContext context)
        {
            Output = pdfOutput;
            if (Swallow)
            {
                try
                {
                    context.Consume(PdfResourceKind.Objects, long.MaxValue);
                }
                catch (PdfException)
                {
                }

                return;
            }

            pdfOutput.Write(_bytes, 0, _bytes.Length);
        }
    }

    private static PdfDocument Edit(PdfDocument document, Action<PdfGraphEdit> action) => PdfGraphEdit.Run("OriginalStampMutationFixture", document.CreateSnapshot(), null, null, default, action);
}
