using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class AnnotationTests
{
    internal static PdfValue Dict(params (string, PdfValue)[] values) => PdfGraphicsNumbers.Dict(values);
    internal static PdfValue Text(string value) => PdfValue.String(new byte[] { 254, 255 }.Concat(Encoding.BigEndianUnicode.GetBytes(value)).ToArray());
    internal static PdfGraphicsResource Appearance(PdfRect? bounds = null, PdfMatrix? matrix = null)
    {
        var box = bounds ?? new PdfRect(0, 0, 100, 60);
        return PdfGraphicsObjects.Form(PdfDrawing.Create(new[] { new PdfContentOperation(PdfOperator.FillRgb, PdfValue.Integer(1), PdfValue.Integer(0), PdfValue.Integer(0)), new PdfContentOperation(PdfOperator.Rectangle, PdfValues.Real(box.X), PdfValues.Real(box.Y), PdfValues.Real(box.Width), PdfValues.Real(box.Height)), new PdfContentOperation(PdfOperator.Fill) }), box, matrix);
    }

    internal static byte[] WaveBytes()
    {
        using var output = new MemoryStream();
        using var writer = new BinaryWriter(output, Encoding.ASCII, true);
        writer.Write(Encoding.ASCII.GetBytes("RIFF"));
        writer.Write(40);
        writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
        writer.Write(16);
        writer.Write((short)1);
        writer.Write((short)1);
        writer.Write(8000);
        writer.Write(16000);
        writer.Write((short)2);
        writer.Write((short)16);
        writer.Write(Encoding.ASCII.GetBytes("data"));
        writer.Write(4);
        writer.Write((short)258);
        writer.Write((short)772);
        writer.Flush();
        return output.ToArray();
    }

    internal static PdfValue FileSpecification()
    {
        using var stream = new MemoryStream(WaveBytes());
        var data = PdfValues.Stream(Dict(("Type", PdfValue.Name("EmbeddedFile")), ("Subtype", PdfValue.Name("audio/wav"))), stream);
        return Dict(("Type", PdfValue.Name("Filespec")), ("F", Text("original.wav")), ("UF", Text("original.wav")), ("EF", Dict(("F", data))));
    }

    internal static PdfValue Properties(PdfAnnotationKind kind)
    {
        var points = PdfGraphicsNumbers.Array(0, 60, 100, 60, 0, 0, 100, 0);
        switch (kind)
        {
            case PdfAnnotationKind.Highlight:
            case PdfAnnotationKind.Underline:
            case PdfAnnotationKind.StrikeOut:
            case PdfAnnotationKind.Squiggly:
                return Dict(("QuadPoints", points));
            case PdfAnnotationKind.Line:
                return Dict(("L", PdfGraphicsNumbers.Array(1, 1, 99, 59)));
            case PdfAnnotationKind.Polygon:
            case PdfAnnotationKind.PolyLine:
                return Dict(("Vertices", PdfGraphicsNumbers.Array(1, 1, 99, 1, 50, 59)));
            case PdfAnnotationKind.Ink:
                return Dict(("InkList", PdfValue.Array(PdfGraphicsNumbers.Array(1, 1, 50, 30, 99, 59))));
            case PdfAnnotationKind.FreeText:
                return Dict(("DA", PdfValue.String(Encoding.ASCII.GetBytes("/F0 12 Tf 0 g"))));
            case PdfAnnotationKind.FileAttachment:
                return Dict(("FS", FileSpecification()));
            case PdfAnnotationKind.RichMedia:
                var file = FileSpecification();
                var instance = Dict(("Subtype", PdfValue.Name("Sound")), ("Asset", file));
                var configuration = Dict(("Subtype", PdfValue.Name("Sound")), ("Instances", PdfValue.Array(instance)));
                return Dict(("RichMediaContent", Dict(("Assets", Dict(("Names", PdfValue.Array(Text("original.wav"), file)))), ("Configurations", PdfValue.Array(configuration)))), ("RichMediaSettings", Dict(("Activation", Dict(("Condition", PdfValue.Name("XA")), ("Configuration", configuration))))));
            default:
                return Dict();
        }
    }

    internal static PdfAnnotation Annotation(string name, PdfAnnotationMetadata? metadata = null) => PdfAnnotation.Create(PdfAnnotationKind.Square, name, new(0, 0, 100, 60), Appearance(), metadata: metadata);
    [Fact]
    public void EveryAuthoredSubtypeProducesAnActualAnnotationAndNormalAppearance()
    {
        using var blank = Pdf.Create().AddPage();
        var kinds = Enum.GetValues(typeof(PdfAnnotationKind)).Cast<PdfAnnotationKind>().ToArray();
        using var document = PdfAnnotations.Add(blank.CreateSnapshot(), 0, kinds.Select(k => PdfAnnotation.Create(k, k.ToString(), new(0, 0, 100, 60), Appearance(), Properties(k))));
        var set = PdfAnnotationSet.Inspect(document.CreateSnapshot());
        Assert.Equal(kinds.Length, set.Annotations.Count);
        var graph = set.Source.SourceDocument!;
        foreach (var record in set.Annotations)
        {
            Assert.Equal(record.Subtype, record.Identifier);
            Assert.NotNull(record.ObjectId);
            var appearance = GraphicsTests.Resolve(graph, record.Definition.Entries["AP"].Entries["N"]);
            Assert.Equal("Form", appearance.Entries["Subtype"].NameValue);
        }

        var media = set.Annotations.Single(a => a.Subtype == "RichMedia");
        var content = GraphicsTests.Resolve(graph, media.Definition.Entries["RichMediaContent"]);
        var config = GraphicsTests.Resolve(graph, content.Entries["Configurations"].Items[0]);
        var instance = GraphicsTests.Resolve(graph, config.Entries["Instances"].Items[0]);
        var file = content.Entries["Assets"].Entries["Names"].Items[1];
        Assert.Equal(file.ReferenceId, instance.Entries["Asset"].ReferenceId);
        Assert.Equal("2.0", graph.Version);
        var fileSpec = GraphicsTests.Resolve(graph, file);
        var embedded = GraphicsTests.Resolve(graph, fileSpec.Entries["EF"].Entries["F"]);
        using var raw = embedded.OpenRawStream();
        Assert.Equal(82, raw.ReadByte());
        Assert.Equal(48, embedded.RawStreamLength);
    }

    [Fact]
    public void UnicodeMetadataDatesForwardRepliesStatesAndPopupsSurviveEditsAndSourceDisposal()
    {
        var created = new DateTimeOffset(2026, 10, 18, 12, 30, 45, TimeSpan.FromHours(5.5));
        using var blank = Pdf.Create().AddPage();
        using var annotated = PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { Annotation("reply", new("ответ", "Անի", created: created, modified: created, state: "Accepted", stateModel: "Review", replyTo: "root")), Annotation("root", new("root", "author")) });
        var set = PdfAnnotationSet.Inspect(annotated.CreateSnapshot());
        using var popup = set.AddPopup(1, new(110, 20, 100, 80), true);
        var captured = PdfAnnotationSet.Inspect(popup.CreateSnapshot());
        annotated.Dispose();
        popup.Dispose();
        Assert.Equal("ответ", captured.Annotations[0].TextProperties["Contents"]);
        Assert.Equal("Անի", captured.Annotations[0].TextProperties["T"]);
        Assert.Equal("D:20261018123045+05'30'", captured.Annotations[0].TextProperties["CreationDate"]);
        Assert.Equal("Accepted", captured.Annotations[0].TextProperties["State"]);
        Assert.Equal("Popup", captured.Annotations[2].Subtype);
        using var edited = captured.Update(1, Dict(("T", Text("new author"))));
        var changed = PdfAnnotationSet.Inspect(edited.CreateSnapshot());
        Assert.Equal("new author", changed.Annotations[1].TextProperties["T"]);
        Assert.True(changed.Annotations[1].Definition.Entries.ContainsKey("Popup"));
        Assert.True(changed.Annotations[0].Definition.Entries.ContainsKey("IRT"));
        Assert.Equal("Annotation.ReplyCycle", Assert.Throws<PdfException>(() => captured.SetReply(1, 0)).Diagnostic.Code);
        Assert.Equal("Annotation.RelatedEntries", Assert.Throws<PdfException>(() => captured.Delete(new[] { 1 }, false)).Diagnostic.Code);
        using var removed = captured.Delete(new[] { 1 });
        Assert.Empty(PdfAnnotationSet.Inspect(removed.CreateSnapshot()).Annotations);
    }

    [Fact]
    public void ImportedAnnotationsPreserveIndirectPayloadAndRelationshipOwnership()
    {
        using var blank = Pdf.Create().AddPage();
        using var annotated = PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { Annotation("root"), Annotation("reply", new(replyTo: "root")) });
        var first = PdfAnnotationSet.Inspect(annotated.CreateSnapshot());
        using var pop = first.AddPopup(0, new(10, 10, 50, 50));
        var source = PdfAnnotationSet.Inspect(pop.CreateSnapshot());
        using var target = Pdf.Create().AddPage().AddPage();
        using var imported = source.ImportTo(target.CreateSnapshot(), 1, new[] { 0 });
        var result = PdfAnnotationSet.Inspect(imported.CreateSnapshot());
        Assert.Equal(3, result.Annotations.Count);
        Assert.All(result.Annotations, a => Assert.Equal(1, a.PageIndex));
        Assert.Equal("root", result.Annotations[0].Identifier);
        Assert.Equal("Annotation.ImportRelationship", Assert.Throws<PdfException>(() => source.ImportTo(target.CreateSnapshot(), 0, new[] { 1 })).Diagnostic.Code);
        Assert.Equal("Annotation.IdentifierCollision", Assert.Throws<PdfException>(() => source.ImportTo(imported.CreateSnapshot(), 0, new[] { 0 })).Diagnostic.Code);
    }

    [Fact]
    public void ImportRejectsDocumentOwnershipEvenWhenTypeNamesAreIndirect()
    {
        using var blank = Pdf.Create().AddPage();
        using var annotated = PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { Annotation("owner") });
        var set = PdfAnnotationSet.Inspect(annotated.CreateSnapshot());
        using var edit = new PdfTransaction(set.Source.SourceDocument);
        var root = edit.Trailer.Entries["Root"].ReferenceId;
        var pages = edit.Resolve(root).Entries["Pages"].ReferenceId;
        NavigationTests.Put(edit, root, "Type", PdfValue.Reference(edit.Add(PdfValue.Name("Catalog"))));
        NavigationTests.Put(edit, pages, "Type", PdfValue.Reference(edit.Add(PdfValue.Name("Pages"))));
        NavigationTests.Put(edit, set.Annotations[0].ObjectId!.Value, "Owner", PdfValue.Reference(root));
        using var bytes = new MemoryStream();
        edit.Save(bytes);
        bytes.Position = 0;
        using var reopened = Pdf.Open(bytes);
        var source = PdfAnnotationSet.Inspect(reopened.CreateSnapshot());
        using var target = Pdf.Create().AddPage();
        Assert.Equal("Graph.DocumentOwnershipImport", Assert.Throws<PdfException>(() => source.ImportTo(target.CreateSnapshot(), 0, new[] { 0 })).Diagnostic.Code);
    }

    [Fact]
    public void FlattenUsesTransformedAppearanceBoundsAndLeavesOriginalContentStateIsolated()
    {
        using var blank = Pdf.Create().AddPage(new PdfPageGeometry(new(new(-20, 10, 200, 100)), PdfPageRotation.Clockwise90, 2));
        blank.Pages[0].Content = PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.FillGray, PdfValues.Real(.5)) });
        var appearance = Appearance(new(10, 20, 30, 40), new(0, 1, -1, 0, 0, 0));
        var annotation = PdfAnnotation.Create(PdfAnnotationKind.Stamp, "rotated", new(-5, 10, 80, 60), appearance);
        using var document = PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { annotation });
        var set = PdfAnnotationSet.Inspect(document.CreateSnapshot());
        using var flattened = set.Flatten(new[] { 0 });
        Assert.Empty(PdfAnnotationSet.Inspect(flattened.CreateSnapshot()).Annotations);
        Assert.Equal(document.Pages[0].Geometry, flattened.Pages[0].Geometry);
        var program = flattened.Pages[0].ReadContent();
        var matrix = program.Operations.Single(o => o.Operation == PdfOperator.Transform);
        Assert.Equal(new double[] { 2, 0, 0, 2, 115, -10 }, matrix.Operands.Select(v => v.NumberValue));
        Assert.Single(program.Operations, o => o.Operation == PdfOperator.DrawObject);
        Assert.Equal(PdfOperator.SaveState, program.Operations[0].Operation);
        var graph = flattened.SourceDocument!;
        var contents = GraphicsTests.Resolve(graph, flattened.Pages[0].Source!.Dictionary.Entries["Contents"]);
        Assert.All(contents.Items, c => Assert.Equal(PdfValueKind.Stream, GraphicsTests.Resolve(graph, c).Kind));
    }

    [Fact]
    public void VisibilityAndDynamicFlagsUseExplicitFlatteningPolicies()
    {
        using var blank = Pdf.Create().AddPage();
        var flags = new[]
        {
            0,
            2,
            4,
            36
        };
        using var doc = PdfAnnotations.Add(blank.CreateSnapshot(), 0, flags.Select((f, i) => PdfAnnotation.Create(PdfAnnotationKind.Square, "a" + i, new(0, 0, 100, 60), Appearance(), Dict(("F", PdfValue.Integer(f))))));
        var set = PdfAnnotationSet.Inspect(doc.CreateSnapshot());
        var all = Enumerable.Range(0, flags.Length).ToArray();
        using var screen = set.Flatten(all);
        using var print = set.Flatten(all, PdfAnnotationView.Print);
        Assert.Equal(2, screen.Pages[0].ReadContent().Operations.Count(o => o.Operation == PdfOperator.DrawObject));
        Assert.Equal(2, print.Pages[0].ReadContent().Operations.Count(o => o.Operation == PdfOperator.DrawObject));
        using var dynamic = set.Update(0, Dict(("F", PdfValue.Integer(8))));
        Assert.Equal("Annotation.DynamicViewFlags", Assert.Throws<PdfException>(() => PdfAnnotationSet.Inspect(dynamic.CreateSnapshot()).Flatten(new[] { 0 })).Diagnostic.Code);
    }

    [Fact]
    public void RichMediaPostersRequireExplicitLossAndNeverExecutePayloads()
    {
        using var blank = Pdf.Create().AddPage();
        using var doc = PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { PdfAnnotation.Create(PdfAnnotationKind.RichMedia, "media", new(0, 0, 100, 60), Appearance(), Properties(PdfAnnotationKind.RichMedia)) });
        var set = PdfAnnotationSet.Inspect(doc.CreateSnapshot());
        Assert.Equal("Annotation.RichMediaPosterPolicy", Assert.Throws<PdfException>(() => set.Flatten(new[] { 0 })).Diagnostic.Code);
        Assert.Throws<PdfException>(() => set.Flatten(new[] { 0 }, allowRichMediaPosterOnly: true));
        using var poster = set.Flatten(new[] { 0 }, allowRichMediaPosterOnly: true, policy: new(PdfProcessingMode.Permissive));
        Assert.Empty(PdfAnnotationSet.Inspect(poster.CreateSnapshot()).Annotations);
        Assert.Contains(poster.LastOperationReport!.Diagnostics, d => d.Code == "Annotation.RichMediaPosterOnly");
    }

    [Fact]
    public void CreationAndBatchLimitsRejectInvalidGeometryReferencesAndCancelledEnumeration()
    {
        var form = Appearance();
        Assert.Equal("Annotation.SubtypeProperties", Assert.Throws<PdfException>(() => PdfAnnotation.Create(PdfAnnotationKind.Highlight, "bad", new(0, 0, 100, 60), form)).Diagnostic.Code);
        Assert.Equal("Annotation.PointBounds", Assert.Throws<PdfException>(() => PdfAnnotation.Create(PdfAnnotationKind.Line, "bad", new(0, 0, 100, 60), form, Dict(("L", PdfGraphicsNumbers.Array(0, 0, 101, 60))))).Diagnostic.Code);
        Assert.Equal("Graph.ForeignReference", Assert.Throws<PdfException>(() => PdfAnnotation.Create(PdfAnnotationKind.Square, "bad", new(0, 0, 100, 60), form, Dict(("Custom", PdfValue.Reference(new(123)))))).Diagnostic.Code);
        using var blank = Pdf.Create().AddPage();
        Assert.Equal("Annotation.IdentifierCollision", Assert.Throws<PdfException>(() => PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { Annotation("same"), Annotation("same") })).Diagnostic.Code);
        bool touched = false;
        IEnumerable<PdfAnnotation> Source()
        {
            touched = true;
            yield return Annotation("a");
        }

        Assert.Throws<OperationCanceledException>(() => PdfAnnotations.Add(blank.CreateSnapshot(), 0, Source(), cancellationToken: new CancellationToken(true)));
        Assert.False(touched);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfAnnotations.Add(blank.CreateSnapshot(), 0, Source(), policy: new(limits: new(maximumObjects: 1)))).Diagnostic.Code);
        Assert.False(touched);
    }

    [Fact]
    public void OwnedXfdfRoundTripsMetadataAppearancesPayloadsAndReplyPopupReferences()
    {
        using var blank = Pdf.Create().AddPage();
        var kinds = Enum.GetValues(typeof(PdfAnnotationKind)).Cast<PdfAnnotationKind>().ToArray();
        using var doc = PdfAnnotations.Add(blank.CreateSnapshot(), 0, kinds.Select(k => PdfAnnotation.Create(k, k.ToString(), new(0, 0, 100, 60), Appearance(), Properties(k), new(contents: "  line\r\nsecond\rthird  ", author: "Անի"))));
        var first = PdfAnnotationSet.Inspect(doc.CreateSnapshot());
        using var popup = first.AddPopup(0, new(100, 0, 80, 60));
        var set = PdfAnnotationSet.Inspect(popup.CreateSnapshot());
        using var output = new MemoryStream();
        set.WriteXfdf(output);
        Assert.True(output.CanWrite);
        output.Position = 0;
        var read = PdfAnnotationSet.ReadXfdf(output);
        Assert.True(output.CanRead);
        Assert.Equal(set.Annotations.Count, read.Annotations.Count);
        Assert.Equal("  line\r\nsecond\rthird  ", read.Annotations[0].TextProperties["Contents"]);
        Assert.Equal("Անի", read.Annotations[0].TextProperties["T"]);
        Assert.Equal("Popup", read.Annotations.Last().Subtype);
        using var target = Pdf.Create().AddPage();
        using var imported = read.ImportTo(target.CreateSnapshot(), 0, Enumerable.Range(0, read.Annotations.Count));
        Assert.Equal(set.Annotations.Count, PdfAnnotationSet.Inspect(imported.CreateSnapshot()).Annotations.Count);
        var sourceGraph = set.Source.SourceDocument!;
        var newGraph = read.Source.SourceDocument!;
        var sourceAp = GraphicsTests.Resolve(sourceGraph, set.Annotations[0].Definition.Entries["AP"].Entries["N"]);
        var newAp = GraphicsTests.Resolve(newGraph, read.Annotations[0].Definition.Entries["AP"].Entries["N"]);
        using var original = sourceAp.OpenRawStream();
        using var copied = newAp.OpenRawStream();
        Assert.Equal(ReadBytes(original), ReadBytes(copied));
        var media = read.Annotations.Single(a => a.Subtype == "RichMedia");
        var content = GraphicsTests.Resolve(newGraph, media.Definition.Entries["RichMediaContent"]);
        var file = GraphicsTests.Resolve(newGraph, content.Entries["Assets"].Entries["Names"].Items[1]);
        var stream = GraphicsTests.Resolve(newGraph, file.Entries["EF"].Entries["F"]);
        using var payload = stream.OpenRawStream();
        Assert.Equal(WaveBytes(), ReadBytes(payload));
    }

    [Fact]
    public void XfdfRejectsDtdMetadataMismatchMissingGraphsAndOutputLimitFailures()
    {
        using var hostile = new MemoryStream(Encoding.UTF8.GetBytes("<!DOCTYPE xfdf [<!ENTITY a SYSTEM 'file:///never-read'>]><xfdf>&a;</xfdf>"));
        Assert.Equal("Annotation.XfdfXml", Assert.Throws<PdfException>(() => PdfAnnotationSet.ReadXfdf(hostile)).Diagnostic.Code);
        using var external = new MemoryStream(Encoding.UTF8.GetBytes("<xfdf xmlns='http://ns.adobe.com/xfdf/'><annots/></xfdf>"));
        Assert.Equal("Annotation.XfdfAppearanceProviderRequired", Assert.Throws<PdfException>(() => PdfAnnotationSet.ReadXfdf(external)).Diagnostic.Code);
        using var blank = Pdf.Create().AddPage();
        using var doc = PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { Annotation("original") });
        var set = PdfAnnotationSet.Inspect(doc.CreateSnapshot());
        using var output = new MemoryStream();
        set.WriteXfdf(output);
        string xml = Encoding.UTF8.GetString(output.ToArray());
        using var mismatched = new MemoryStream(Encoding.UTF8.GetBytes(xml.Replace("name=\"original\"", "name=\"changed\"")));
        Assert.Equal("Annotation.XfdfMetadataMismatch", Assert.Throws<PdfException>(() => PdfAnnotationSet.ReadXfdf(mismatched)).Diagnostic.Code);
        using var small = new MemoryStream();
        Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => set.WriteXfdf(small, policy: new(limits: new(maximumOutputBytes: 1)))).Diagnostic.Code);
        Assert.True(small.CanWrite);
        using var cancelled = new MemoryStream(output.ToArray());
        Assert.Throws<OperationCanceledException>(() => PdfAnnotationSet.ReadXfdf(cancelled, cancellationToken: new CancellationToken(true)));
        Assert.Equal(0, cancelled.Position);
        var empty = PdfAnnotationSet.Inspect(blank.CreateSnapshot());
        using var xmlEmpty = new MemoryStream();
        empty.WriteXfdf(xmlEmpty);
        xmlEmpty.Position = 0;
        Assert.Empty(PdfAnnotationSet.ReadXfdf(xmlEmpty).Annotations);
    }

    private static byte[] ReadBytes(Stream stream)
    {
        using var output = new MemoryStream();
        stream.CopyTo(output);
        return output.ToArray();
    }

    [Fact]
    public void GeneratedMarkupUsesRealSubtypeGeometryAndRejectsAdditionalUnrenderedStyling()
    {
        var kinds = new[]
        {
            PdfAnnotationKind.Text,
            PdfAnnotationKind.Square,
            PdfAnnotationKind.Circle,
            PdfAnnotationKind.Line,
            PdfAnnotationKind.Polygon,
            PdfAnnotationKind.PolyLine,
            PdfAnnotationKind.Highlight,
            PdfAnnotationKind.Underline,
            PdfAnnotationKind.StrikeOut,
            PdfAnnotationKind.Squiggly,
            PdfAnnotationKind.Ink,
            PdfAnnotationKind.Caret
        };
        using var blank = Pdf.Create().AddPage();
        var color = new PdfColor(PdfColorSpace.Rgb, 1, .5, 0);
        using var document = PdfAnnotations.Add(blank.CreateSnapshot(), 0, kinds.Select(k => PdfAnnotation.CreateMarkup(k, k.ToString(), new(0, 0, 100, 60), color, Properties(k), opacity: .5)));
        var set = PdfAnnotationSet.Inspect(document.CreateSnapshot());
        using var flattened = set.Flatten(Enumerable.Range(0, set.Annotations.Count));
        Assert.Equal(kinds.Length, flattened.Pages[0].ReadContent().Operations.Count(o => o.Operation == PdfOperator.DrawObject));
        Assert.Empty(PdfAnnotationSet.Inspect(flattened.CreateSnapshot()).Annotations);
        Assert.Equal("Annotation.GeneratedStyleProfile", Assert.Throws<PdfException>(() => PdfAnnotation.CreateMarkup(PdfAnnotationKind.Square, "bad", new(0, 0, 100, 60), color, Dict(("BE", Dict())))).Diagnostic.Code);
        var polygon = PdfAnnotation.CreateMarkup(PdfAnnotationKind.Polygon, "p", new(0, 0, 100, 60), color, Properties(PdfAnnotationKind.Polygon));
        Assert.Equal("1.5", polygon.MinimumVersion);
    }

    [Fact]
    public void FreeTextEmbedsShapedFontsAndExactLogicalContentsWithExplicitOverflow()
    {
        var font = TextTests.Read("business.ttf");
        var text = PdfText.Shape("office", new(font), new(new PdfSimpleTextShaper()));
        var annotation = PdfAnnotation.CreateFreeText("text", new(0, 0, 180, 60), text, 12);
        using var blank = Pdf.Create().AddPage();
        using var document = PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { annotation });
        var set = PdfAnnotationSet.Inspect(document.CreateSnapshot());
        Assert.Equal("office", set.Annotations[0].TextProperties["Contents"]);
        var graph = document.SourceDocument!;
        var appearance = GraphicsTests.Resolve(graph, set.Annotations[0].Definition.Entries["AP"].Entries["N"]);
        Assert.True(appearance.Entries["Resources"].Entries["Font"].Entries.Count > 0);
        Assert.True(annotation.NormalAppearance.OperationReport.Succeeded);
        using var flattened = set.Flatten(new[] { 0 });
        Assert.Empty(PdfAnnotationSet.Inspect(flattened.CreateSnapshot()).Annotations);
        Assert.Equal("Annotation.TextOverflow", Assert.Throws<PdfException>(() => PdfAnnotation.CreateFreeText("tiny", new(0, 0, 1, 1), text, 12)).Diagnostic.Code);
        var clipped = PdfAnnotation.CreateFreeText("tiny", new(0, 0, 1, 1), text, 12, allowClipping: true, policy: new(PdfProcessingMode.Permissive));
        Assert.Contains(clipped.OperationReport.Diagnostics, d => d.Code == "Annotation.TextClipped");
    }

    [Fact]
    public void ReplyDepthAndXmlDepthAreBoundedAndMetadataExtensionsNeverDisappearSilently()
    {
        using var blank = Pdf.Create().AddPage();
        var batch = Enumerable.Range(0, 20).Select(i => Annotation("r" + i, new(replyTo: i == 19 ? null : "r" + (i + 1)))).ToArray();
        Assert.Equal("Limit.Depth", Assert.Throws<PdfException>(() => PdfAnnotations.Add(blank.CreateSnapshot(), 0, batch, policy: new(limits: new(maximumDepth: 16)))).Diagnostic.Code);
        using var document = PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { Annotation("one") });
        var set = PdfAnnotationSet.Inspect(document.CreateSnapshot());
        using var output = new MemoryStream();
        set.WriteXfdf(output);
        string xml = Encoding.UTF8.GetString(output.ToArray());
        using var modified = new MemoryStream(Encoding.UTF8.GetBytes(xml.Replace("page=\"0\"", "color=\"#FF0000\" page=\"0\"")));
        Assert.Equal("Annotation.XfdfProfile", Assert.Throws<PdfException>(() => PdfAnnotationSet.ReadXfdf(modified)).Diagnostic.Code);
        string nested = xml.Replace("<fp:dictionary>", "<fp:dictionary><fp:entry key=\"WA==\">" + string.Concat(Enumerable.Repeat("<fp:array>", 30)) + "<fp:null/>" + string.Concat(Enumerable.Repeat("</fp:array>", 30)) + "</fp:entry>");
        using var deep = new MemoryStream(Encoding.UTF8.GetBytes(nested));
        Assert.Equal("Limit.Depth", Assert.Throws<PdfException>(() => PdfAnnotationSet.ReadXfdf(deep, new(limits: new(maximumDepth: 16)))).Diagnostic.Code);
    }

    [Fact]
    public void BothLanguageConsumersEmitRealAnnotationAppearances()
    {
        foreach (var write in new Func<Stream, PdfOperationReport>[]
        {
            ContractExamples.CSharp.ContractExample.WriteAnnotationPdf,
            ContractExamples.VisualBasic.ContractExample.WriteAnnotationPdf
        }

        )
        {
            using var output = new MemoryStream();
            Assert.True(write(output).Succeeded);
            output.Position = 0;
            using var document = Pdf.Open(output);
            var set = PdfAnnotationSet.Inspect(document.CreateSnapshot());
            Assert.Equal(2, set.Annotations.Count);
            Assert.Equal("Reviewer", set.Annotations[0].TextProperties["T"]);
            using var flattened = set.Flatten(new[] { 0, 1 });
            Assert.Empty(PdfAnnotationSet.Inspect(flattened.CreateSnapshot()).Annotations);
        }
    }

    [Fact]
    public void InvisibleFlagDoesNotHideAStandardAnnotationAndPageGroupsRequireExplicitCompositing()
    {
        using var blank = Pdf.Create().AddPage();
        var annotation = PdfAnnotation.Create(PdfAnnotationKind.Square, "visible", new(0, 0, 100, 60), Appearance(), Dict(("F", PdfValue.Integer(1))));
        using var doc = PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { annotation });
        var set = PdfAnnotationSet.Inspect(doc.CreateSnapshot());
        using var flat = set.Flatten(new[] { 0 });
        Assert.Single(flat.Pages[0].ReadContent().Operations, o => o.Operation == PdfOperator.DrawObject);
        using var edit = new PdfTransaction(doc.SourceDocument);
        var page = doc.Pages[0].Source!;
        var entries = page.Dictionary.Entries.ToDictionary(p => p.Key, p => p.Value);
        entries["Group"] = Dict(("S", PdfValue.Name("Transparency")));
        edit.Set(page.ObjectId, PdfValue.Dictionary(entries));
        using var output = new MemoryStream();
        edit.Save(output);
        output.Position = 0;
        using var grouped = Pdf.Open(output);
        Assert.Equal("Annotation.PageGroupFlattening", Assert.Throws<PdfException>(() => PdfAnnotationSet.Inspect(grouped.CreateSnapshot()).Flatten(new[] { 0 })).Diagnostic.Code);
    }

    [Fact]
    public void WidgetMutationAndImportRequireTheFieldOwnershipProfile()
    {
        using var blank = Pdf.Create().AddPage();
        using var doc = PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { Annotation("field") });
        var set = PdfAnnotationSet.Inspect(doc.CreateSnapshot());
        using var edit = new PdfTransaction(doc.SourceDocument);
        var id = set.Annotations[0].ObjectId!.Value;
        var values = set.Annotations[0].Definition.Entries.ToDictionary(p => p.Key, p => p.Value);
        values["Subtype"] = PdfValue.Name("Widget");
        values["FT"] = PdfValue.Name("Tx");
        edit.Set(id, PdfValue.Dictionary(values));
        var root = edit.Trailer.Entries["Root"].ReferenceId;
        var catalog = edit.Resolve(root).Entries.ToDictionary(p => p.Key, p => p.Value);
        catalog["AcroForm"] = Dict(("Fields", PdfValue.Array(PdfValue.Reference(id))));
        edit.Set(root, PdfValue.Dictionary(catalog));
        using var output = new MemoryStream();
        edit.Save(output);
        output.Position = 0;
        using var source = Pdf.Open(output);
        var fields = PdfAnnotationSet.Inspect(source.CreateSnapshot());
        Assert.Equal("Annotation.WidgetEditing", Assert.Throws<PdfException>(() => fields.Update(0, Dict())).Diagnostic.Code);
        Assert.Equal("Annotation.WidgetDeletion", Assert.Throws<PdfException>(() => fields.Delete(new[] { 0 })).Diagnostic.Code);
        Assert.Equal("Annotation.WidgetImport", Assert.Throws<PdfException>(() => fields.ImportTo(blank.CreateSnapshot(), 0, new[] { 0 })).Diagnostic.Code);
    }

    [Fact]
    public void ExistingPageScopedNamesRemainValidWhileNamedRepliesRequireAnUnambiguousTarget()
    {
        using var blank = Pdf.Create().AddPage().AddPage();
        using var first = PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { Annotation("a") });
        using var second = PdfAnnotations.Add(first.CreateSnapshot(), 1, new[] { Annotation("b") });
        var entries = PdfAnnotationSet.Inspect(second.CreateSnapshot());
        using var edit = new PdfTransaction(second.SourceDocument);
        var id = entries.Annotations[1].ObjectId!.Value;
        var dictionary = entries.Annotations[1].Definition.Entries.ToDictionary(p => p.Key, p => p.Value);
        dictionary["NM"] = Text("a");
        edit.Set(id, PdfValue.Dictionary(dictionary));
        using var output = new MemoryStream();
        edit.Save(output);
        output.Position = 0;
        using var source = Pdf.Open(output);
        using var added = PdfAnnotations.Add(source.CreateSnapshot(), 0, new[] { Annotation("c") });
        Assert.Equal(2, PdfAnnotationSet.Inspect(added.CreateSnapshot()).Annotations.Count(a => a.Identifier == "a"));
        Assert.Equal("Annotation.ReplyTarget", Assert.Throws<PdfException>(() => PdfAnnotations.Add(source.CreateSnapshot(), 0, new[] { Annotation("reply", new(replyTo: "a")) })).Diagnostic.Code);
    }

    [Fact]
    public void StagedEditsRetainCallerPageCapacityAndDefaultGeometry()
    {
        using var source = Pdf.Create(new PdfOptions(PdfPageSize.Letter, maximumPages: 3)).AddPage();
        using var edited = PdfAnnotations.Add(source.CreateSnapshot(), 0, new[] { Annotation("one") });
        edited.Pages.Add();
        Assert.Equal(PdfPageSize.Letter, edited.Pages[1].Size);
        edited.Pages.Add();
        Assert.Throws<PdfException>(() => edited.Pages.Add());
        var snapshot = PdfAnnotationSet.Inspect(source.CreateSnapshot()).Source;
        Assert.Equal(3, snapshot.Options.MaximumPages);
        Assert.Equal(PdfPageSize.Letter, snapshot.Options.DefaultPageSize);
    }
}
