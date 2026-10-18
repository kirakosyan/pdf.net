using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class OrganizationTests
{
    [Fact]
    public void RangeSyntaxIsExplicitOrderedBoundedAndCancellable()
    {
        Assert.Equal(new[] { 3, 2, 1, 0, 3, 0, 2, 1, 3 }, PdfPageSelection.Parse("last-1,4,odd,even", 4).Indices);
        Assert.Equal("Organization.PageRange", Assert.Throws<PdfException>(() => PdfPageSelection.Parse("0", 3)).Diagnostic.Code);
        Assert.Equal("Organization.SelectionLimit", Assert.Throws<PdfException>(() => PdfPageSelection.Parse("all", 3, 2)).Diagnostic.Code);
        Assert.Throws<OperationCanceledException>(() => PdfPageSelection.Parse("all", 3, cancellationToken: new CancellationToken(true)));
    }

    private static PdfDocument Plain(int pages)
    {
        var document = Pdf.Create();
        for (int i = 0; i < pages; i++)
        {
            var page = document.Pages.Add(new PdfPageGeometry(new(new(-20, 10, 200 + i, 100)), (PdfPageRotation)((i % 4) * 90), 2));
            page.Content = PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.Rectangle, PdfValue.Integer(i + 1), PdfValue.Integer(20), PdfValue.Integer(10), PdfValue.Integer(10)), new PdfContentOperation(PdfOperator.Fill) });
        }

        return document;
    }

    [Fact]
    public void MixedSourceMergeExtractionAndDuplicateCopiesPreserveGeometryAndOwnership()
    {
        using var a = Plain(3);
        using var b = Plain(2);
        var sa = a.CreateSnapshot();
        var sb = b.CreateSnapshot();
        a.Dispose();
        b.Dispose();
        using var merged = PdfPageOrganization.Merge(new[] { new PdfPageInput(sa, PdfPageSelection.Parse("3,1", 3)), new PdfPageInput(sb) });
        Assert.Equal(4, merged.Pages.Count);
        Assert.Equal(sa.Pages[2].Geometry, merged.Pages[0].Geometry);
        Assert.Equal(sa.Pages[0].Geometry, merged.Pages[1].Geometry);
        using var extracted = PdfPageOrganization.Extract(merged.CreateSnapshot(), PdfPageSelection.Parse("4,2,2", 4));
        Assert.Equal(3, extracted.Pages.Count);
        Assert.Equal(extracted.Pages[1].Geometry, extracted.Pages[2].Geometry);
        Assert.Equal(new[] { 2d, 1d, 1d }, extracted.Pages.Select(p => p.ReadContent().Operations.First(o => o.Operation == PdfOperator.Rectangle).Operands[0].NumberValue));
        using var output = new MemoryStream();
        extracted.Save(output);
        Assert.True(output.CanWrite);
    }

    [Fact]
    public void ColumnStructureSurvivesMergingAndPartialSelection()
    {
        var style = new PdfFlowTextStyle(new(TextTests.Read("business.ttf")), 12);
        var flow = PdfFlow.Layout(new[] { new PdfFlowColumns(new[] { new PdfFlowParagraph(string.Join(" ", Enumerable.Repeat("office", 40)), style) }, height: 50) }, new(new(new(new(0, 0, 200, 100))), PdfMargins.Uniform(10)), new(new PdfSimpleTextShaper()));
        using var first = flow.CreateDocument();
        using var second = flow.CreateDocument();
        using var merged = PdfPageOrganization.Merge(new[] { new PdfPageInput(first.CreateSnapshot()), new PdfPageInput(second.CreateSnapshot(), PdfPageSelection.Parse("last", second.Pages.Count)) });
        Assert.Equal(first.Pages.Count + 1, merged.Pages.Count);
        var source = merged.SourceDocument!;
        var catalog = source.Resolve(source.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        var root = source.Resolve(catalog.Entries["StructTreeRoot"].ReferenceId);
        var parents = root.Entries["ParentTree"].Entries["Nums"].Items;
        Assert.Equal(merged.Pages.Count * 2, parents.Count);
        Assert.Equal(merged.Pages.Count, merged.Pages.Select(p => p.Source!.Dictionary.Entries["StructParents"].IntegerValue).Distinct().Count());
        using var saved = new MemoryStream();
        merged.Save(saved);
    }

    [Fact]
    public void OrganizationHonorsSharedLimitsAndPageCeilings()
    {
        using var source = Plain(2);
        Assert.Equal("Organization.PageLimit", Assert.Throws<PdfException>(() => PdfPageOrganization.Merge(new[] { new PdfPageInput(source.CreateSnapshot()) }, new(maximumPages: 1))).Diagnostic.Code);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfPageOrganization.Merge(new[] { new PdfPageInput(source.CreateSnapshot()) }, policy: new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        Assert.Throws<OperationCanceledException>(() => PdfPageOrganization.Merge(new[] { new PdfPageInput(source.CreateSnapshot()) }, cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public void NUpBookletAndTilesRetainVectorFormsAndKnownPageOrder()
    {
        using var source = Plain(4);
        using var nup = PdfPageImposition.NUp(source.CreateSnapshot(), new(600, 300), margins: PdfMargins.Uniform(10), gap: 10);
        Assert.Equal(2, nup.Pages.Count);
        Assert.All(nup.Pages, p => Assert.Equal(2, p.ReadContent().Operations.Count(o => o.Operation == PdfOperator.DrawObject)));
        using var booklet = PdfPageImposition.Booklet(source.CreateSnapshot(), new(600, 300));
        Assert.Equal(2, booklet.Pages.Count);
        Assert.Equal(new[] { new[] { "Page3", "Page0" }, new[] { "Page1", "Page2" } }, booklet.Pages.Select(p => p.ReadContent().Operations.Where(o => o.Operation == PdfOperator.DrawObject).Select(o => o.Operands[0].NameValue).ToArray()).ToArray());
        using var one = Plain(1);
        using var tiled = PdfPageImposition.Tile(one.CreateSnapshot(), new(200, 150), overlap: 10);
        Assert.Equal(6, tiled.Pages.Count);
        using var bytes = new MemoryStream();
        tiled.Save(bytes);
        bytes.Position = 0;
        using var read = Pdf.Open(bytes);
        Assert.Equal(6, read.Pages.Count);
    }

    [Fact]
    public void NUpMovesColumnMcidAssociationsIntoTheActualFormStreams()
    {
        var style = new PdfFlowTextStyle(new(TextTests.Read("business.ttf")), 12);
        var flow = PdfFlow.Layout(new[] { new PdfFlowColumns(new[] { new PdfFlowParagraph(string.Join(" ", Enumerable.Repeat("office", 40)), style) }, height: 50) }, new(new(new(new(0, 0, 200, 100))), PdfMargins.Uniform(10)), new(new PdfSimpleTextShaper()));
        using var original = flow.CreateDocument();
        using var imposed = PdfPageImposition.NUp(original.CreateSnapshot(), new(400, 100));
        var graph = imposed.SourceDocument!;
        var catalog = graph.Resolve(graph.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        var root = graph.Resolve(catalog.Entries["StructTreeRoot"].ReferenceId);
        Assert.Equal(original.Pages.Count, root.Entries["K"].Items.Count);
        foreach (var parentRef in root.Entries["K"].Items)
        {
            var parent = graph.Resolve(parentRef.ReferenceId);
            foreach (var childRef in parent.Entries["K"].Items)
            {
                var child = graph.Resolve(childRef.ReferenceId);
                var mcr = child.Entries["K"];
                Assert.Equal("MCR", mcr.Entries["Type"].NameValue);
                var form = graph.Resolve(mcr.Entries["Stm"].ReferenceId);
                Assert.True(form.Entries.ContainsKey("StructParents"));
                Assert.Contains(imposed.Pages, p => p.Source!.ObjectId == mcr.Entries["Pg"].ReferenceId);
            }
        }
    }

    [Fact]
    public void GeometryCallbacksAndPhysicalScalingRetainOriginalNativeContent()
    {
        using var source = Plain(2);
        var snapshot = source.CreateSnapshot();
        var selection = PdfPageSelection.Parse("2", 2);
        using var scaled = PdfPageOrganization.Scale(snapshot, selection, 2);
        Assert.Equal(2, scaled.Pages[0].Geometry.UserUnit);
        Assert.Equal(4, scaled.Pages[1].Geometry.UserUnit);
        Assert.Equal(snapshot.Pages[1].Geometry.Boxes, scaled.Pages[1].Geometry.Boxes);
        using var rotated = PdfPageOrganization.Transform(snapshot, selection, g => g.WithRotation(PdfPageRotation.Clockwise180));
        Assert.Equal(PdfPageRotation.Clockwise180, rotated.Pages[1].Rotation);
        Assert.Equal(2, rotated.Pages[1].ReadContent().Operations[0].Operands[0].IntegerValue);
    }

    private static PdfDocument Interactive(bool field = false, bool signed = false)
    {
        using var plain = Plain(2);
        using var bytes = new MemoryStream();
        plain.Save(bytes);
        bytes.Position = 0;
        using var read = Pdf.Open(bytes);
        var graph = read.SourceDocument!;
        using var edit = new PdfTransaction(graph);
        var catalogId = graph.Revisions[0].Trailer.Entries["Root"].ReferenceId;
        var catalog = graph.Resolve(catalogId).Entries.ToDictionary(p => p.Key, p => p.Value);
        var firstId = read.Pages[0].Source!.ObjectId;
        var lastId = read.Pages[1].Source!.ObjectId;
        var page = read.Pages[0].Source!.Dictionary.Entries.ToDictionary(p => p.Key, p => p.Value);
        var destination = PdfValue.Array(PdfValue.Reference(lastId), PdfValue.Name("XYZ"), PdfValue.Integer(-10), PdfValue.Integer(80), PdfValue.Null);
        var link = edit.Add(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Annot")), ("Subtype", PdfValue.Name("Link")), ("P", PdfValue.Reference(firstId)), ("Rect", PdfGraphicsNumbers.Array(0, 20, 10, 30)), ("Border", PdfGraphicsNumbers.Array(0, 0, 0)), ("Dest", destination)));
        var annots = new List<PdfValue>
        {
            PdfValue.Reference(link)
        };
        page["Resources"] = PdfGraphicsNumbers.Dict(("ColorSpace", PdfGraphicsNumbers.Dict(("Dest", PdfValue.Name("DeviceGray")))));
        var outlines = edit.Add(PdfValue.Null);
        var outline = edit.Add(PdfGraphicsNumbers.Dict(("Title", PdfValue.String(Encoding.ASCII.GetBytes("Last"))), ("Parent", PdfValue.Reference(outlines)), ("Dest", destination)));
        edit.Set(outlines, PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Outlines")), ("First", PdfValue.Reference(outline)), ("Last", PdfValue.Reference(outline)), ("Count", PdfValue.Integer(1))));
        catalog["Outlines"] = PdfValue.Reference(outlines);
        if (field)
        {
            var font = edit.Add(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Font")), ("Subtype", PdfValue.Name("Type1")), ("BaseFont", PdfValue.Name("Helvetica"))));
            var widget = edit.Add(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Annot")), ("Subtype", PdfValue.Name("Widget")), ("FT", PdfValue.Name("Tx")), ("T", PdfValue.String(Encoding.ASCII.GetBytes("amount"))), ("V", PdfValue.String(Encoding.ASCII.GetBytes("100"))), ("P", PdfValue.Reference(firstId)), ("Rect", PdfGraphicsNumbers.Array(0, 40, 50, 60))));
            annots.Add(PdfValue.Reference(widget));
            catalog["AcroForm"] = PdfGraphicsNumbers.Dict(("Fields", PdfValue.Array(PdfValue.Reference(widget))), ("NeedAppearances", PdfValue.Boolean(true)), ("DA", PdfValue.String(Encoding.ASCII.GetBytes("/F0 10 Tf 0 g"))), ("DR", PdfGraphicsNumbers.Dict(("Font", PdfGraphicsNumbers.Dict(("F0", PdfValue.Reference(font)))))));
        }

        if (signed)
            catalog["RetainedSignature"] = PdfValue.Reference(edit.Add(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Sig")), ("ByteRange", PdfGraphicsNumbers.Array(0, 1, 2, 3)), ("Contents", PdfValue.String(0)))));
        page["Annots"] = PdfValue.Array(annots.ToArray());
        edit.Set(firstId, PdfValue.OwnedDictionary(page));
        edit.Set(catalogId, PdfValue.OwnedDictionary(catalog));
        using var output = new MemoryStream();
        edit.Save(output, new(allowSignatureInvalidation: signed));
        output.Position = 0;
        return Pdf.Open(output);
    }

    [Fact]
    public void LinksOutlinesResourceNamesAndMissingDestinationsHaveExplicitBehavior()
    {
        using var source = Interactive();
        using var reordered = PdfPageOrganization.Extract(source.CreateSnapshot(), PdfPageSelection.Parse("2,1", 2));
        var graph = reordered.SourceDocument!;
        var page = reordered.Pages[1].Source!;
        var link = graph.Resolve(page.Dictionary.Entries["Annots"].Items[0].ReferenceId);
        Assert.Equal(reordered.Pages[0].Source!.ObjectId, link.Entries["Dest"].Items[0].ReferenceId);
        var resources = page.EffectiveAttributes.Entries["Resources"];
        if (resources.Kind == PdfValueKind.Reference)
            resources = graph.Resolve(resources.ReferenceId);
        Assert.True(resources.Entries["ColorSpace"].Entries.ContainsKey("Dest"));
        Assert.Equal("Organization.MissingDestination", Assert.Throws<PdfException>(() => PdfPageOrganization.Extract(source.CreateSnapshot(), PdfPageSelection.Parse("1", 2))).Diagnostic.Code);
        Assert.Equal("Organization.DestinationOmitted", Assert.Throws<PdfException>(() => PdfPageOrganization.Extract(source.CreateSnapshot(), PdfPageSelection.Parse("1", 2), new(missingDestinations: PdfMissingDestinationPolicy.OmitWithDiagnostic))).Diagnostic.Code);
        using var omitted = PdfPageOrganization.Extract(source.CreateSnapshot(), PdfPageSelection.Parse("1", 2), new(missingDestinations: PdfMissingDestinationPolicy.OmitWithDiagnostic), new(PdfProcessingMode.Permissive));
        Assert.Contains(omitted.LastOperationReport!.Diagnostics, d => d.Code == "Organization.DestinationOmitted");
    }

    [Fact]
    public void DuplicateFieldsRenameRootNamesAndNamespaceDefaultAppearanceResources()
    {
        using var first = Interactive(true);
        using var second = Interactive(true);
        var inputs = new[]
        {
            new PdfPageInput(first.CreateSnapshot()),
            new PdfPageInput(second.CreateSnapshot())
        };
        Assert.Equal("Organization.FieldCollision", Assert.Throws<PdfException>(() => PdfPageOrganization.Merge(inputs)).Diagnostic.Code);
        using var merged = PdfPageOrganization.Merge(inputs, new(fieldCollisions: PdfFieldCollisionPolicy.Rename));
        var graph = merged.SourceDocument!;
        var root = graph.Resolve(graph.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        var form = root.Entries["AcroForm"];
        Assert.Equal(2, form.Entries["Fields"].Items.Count);
        Assert.Equal(2, form.Entries["DR"].Entries["Font"].Entries.Count);
        Assert.True(form.Entries["NeedAppearances"].BooleanValue);
        foreach (var fieldRef in form.Entries["Fields"].Items)
        {
            var field = graph.Resolve(fieldRef.ReferenceId);
            string da = Encoding.ASCII.GetString(field.Entries["DA"].StringBytes.ToArray());
            Assert.Contains("/Source", da);
            Assert.Contains("_F0 10 Tf", da);
            Assert.Contains(merged.Pages, p => p.Source!.ObjectId == field.Entries["P"].ReferenceId);
        }

        Assert.Contains(merged.LastOperationReport!.Diagnostics, d => d.Code == "Organization.FieldRenamed");
    }

    [Fact]
    public void SignatureRewriteAndNUpDestinationsAreCheckedBeforeOutput()
    {
        using var signed = Interactive(signed: true);
        Assert.Equal("Writer.SignatureRewrite", Assert.Throws<PdfException>(() => PdfPageOrganization.Merge(new[] { new PdfPageInput(signed.CreateSnapshot()) })).Diagnostic.Code);
        using var allowed = PdfPageOrganization.Merge(new[] { new PdfPageInput(signed.CreateSnapshot()) }, new(allowSignatureInvalidation: true));
        Assert.Contains(allowed.LastOperationReport!.Diagnostics, d => d.Code == "Writer.SignatureRewriteAllowed");
        using var source = Interactive();
        using var imposed = PdfPageImposition.NUp(source.CreateSnapshot(), new(600, 300));
        var graph = imposed.SourceDocument!;
        var page = Assert.Single(imposed.Pages).Source!;
        var link = graph.Resolve(page.Dictionary.Entries["Annots"].Items[0].ReferenceId);
        Assert.Equal(page.ObjectId, link.Entries["P"].ReferenceId);
        Assert.Equal(page.ObjectId, link.Entries["Dest"].Items[0].ReferenceId);
        Assert.True(link.Entries["Rect"].Items[2].NumberValue > link.Entries["Rect"].Items[0].NumberValue);
    }

    [Fact]
    public void InsertSplitAndDeleteUseDetachedSourcesAndPreserveRequestedOrder()
    {
        using var host = Plain(3);
        using var source = Plain(2);
        using var inserted = PdfPageOrganization.Insert(host.CreateSnapshot(), 1, new(source.CreateSnapshot(), PdfPageSelection.Parse("2", 2)));
        Assert.Equal(new[] { 1d, 2d, 2d, 3d }, inserted.Pages.Select(p => p.ReadContent().Operations[0].Operands[0].NumberValue));
        using var deleted = PdfPageOrganization.Delete(inserted.CreateSnapshot(), PdfPageSelection.Parse("2-3", 4));
        Assert.Equal(new[] { 1d, 3d }, deleted.Pages.Select(p => p.ReadContent().Operations[0].Operands[0].NumberValue));
        var parts = PdfPageOrganization.Split(host.CreateSnapshot(), new[] { PdfPageSelection.Parse("3,1", 3), PdfPageSelection.Parse("2", 3) });
        try
        {
            Assert.Equal(new[] { 2, 1 }, parts.Select(p => p.Pages.Count));
            Assert.Same(parts[0].LastOperationReport, parts[1].LastOperationReport);
        }
        finally
        {
            foreach (var part in parts)
                part.Dispose();
        }

        Assert.Equal("Organization.EmptySelection", Assert.Throws<PdfException>(() => PdfPageOrganization.Delete(host.CreateSnapshot(), PdfPageSelection.Parse("all", 3))).Diagnostic.Code);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothLanguageExamplesRetainInputAndOutputOwnership(bool visualBasic)
    {
        using var source = Plain(2);
        using var original = new MemoryStream();
        source.Save(original);
        using var first = new MemoryStream(original.ToArray());
        using var second = new MemoryStream(original.ToArray());
        using var output = new MemoryStream();
        if (visualBasic)
            ContractExamples.VisualBasic.ContractExample.CombinePages(first, second, output);
        else
            ContractExamples.CSharp.ContractExample.CombinePages(first, second, output);
        Assert.True(first.CanRead);
        Assert.True(second.CanRead);
        Assert.True(output.CanWrite);
        output.Position = 0;
        using var combined = Pdf.Open(output);
        Assert.Equal(4, combined.Pages.Count);
    }

    [Fact]
    public void CancelledOrganizationDoesNotInvokeTheCallerIterator()
    {
        int moves = 0;
        IEnumerable<PdfPageInput> Inputs()
        {
            moves++;
            yield return null!;
        }

        Assert.Throws<OperationCanceledException>(() => PdfPageOrganization.Merge(Inputs(), cancellationToken: new CancellationToken(true)));
        Assert.Equal(0, moves);
    }
}
