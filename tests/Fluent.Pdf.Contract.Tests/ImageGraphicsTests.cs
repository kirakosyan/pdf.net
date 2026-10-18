using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ImageGraphicsTests
{
    private static PdfImageAsset Pixel(byte red, PdfImageMetadata? metadata = null)
    {
        using var input = new MemoryStream(new byte[] { red, 0, 0 });
        return PdfImages.FromSamples(input, new(1, 1, 3, 8), PdfColorSpace.Rgb, metadata: metadata);
    }

    internal static PdfDocument Repeated(PdfImageAsset image)
    {
        var form = PdfGraphicsObjects.Form(PdfDrawing.Create(new[] { new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Pixel")) }, new(new PdfResourceBinding("Pixel", image.Resource))), new(0, 0, 1, 1), new PdfMatrix(2, 0, 0, 3, 0, 0));
        var document = Pdf.Create().AddPage(new PdfPageSize(100, 100));
        document.Pages[0].SetResource("Shared", form);
        document.Pages[0].Content = PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.SaveState), new PdfContentOperation(PdfOperator.Transform, PdfValue.Integer(10), PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(10), PdfValue.Integer(5), PdfValue.Integer(7)), new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Shared")), new PdfContentOperation(PdfOperator.RestoreState), new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Shared")) });
        return document;
    }

    [Fact]
    public void InvocationsRetainNestedTransformsBytesAndIndependentScopeAfterDisposal()
    {
        using var document = Repeated(Pixel(255));
        var manifest = PdfPageGraphics.Inspect(document.CreateSnapshot(), 0);
        document.Dispose();
        Assert.Equal(new[] { PdfGraphicsOccurrenceKind.Form, PdfGraphicsOccurrenceKind.Image, PdfGraphicsOccurrenceKind.Form, PdfGraphicsOccurrenceKind.Image }, manifest.Occurrences.Select(o => o.Kind));
        Assert.Equal(new PdfPoint(25, 37), manifest.Occurrences[1].Transform.Transform(new(1, 1)));
        Assert.Equal(new PdfPoint(2, 3), manifest.Occurrences[3].Transform.Transform(new(1, 1)));
        var image = manifest.ExtractImage(1);
        using var samples = image.OpenSamples();
        Assert.Equal(255, samples.ReadByte());
        var form = manifest.ExtractResource(0);
        using var imported = Pdf.Create().AddPage();
        imported.Pages[0].SetResource("Imported", form);
        imported.Pages[0].Content = PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Imported")) });
        Assert.Equal(2, PdfPageGraphics.Inspect(imported.CreateSnapshot(), 0).Occurrences.Count);
    }

    [Fact]
    public void ReplaceAndRemoveCloneOnlyTheSelectedSharedFormInvocation()
    {
        using var source = Repeated(Pixel(255));
        var manifest = PdfPageGraphics.Inspect(source.CreateSnapshot(), 0);
        using var replaced = manifest.ReplaceImage(1, Pixel(64));
        var changed = PdfPageGraphics.Inspect(replaced.CreateSnapshot(), 0);
        using var first = changed.ExtractImage(1).OpenSamples();
        using var second = changed.ExtractImage(3).OpenSamples();
        Assert.Equal(64, first.ReadByte());
        Assert.Equal(255, second.ReadByte());
        using var removed = manifest.Remove(1);
        var edited = PdfPageGraphics.Inspect(removed.CreateSnapshot(), 0);
        Assert.Single(edited.Occurrences, o => o.Kind == PdfGraphicsOccurrenceKind.Image);
        Assert.Equal(4, manifest.Occurrences.Count);
        using var original = manifest.ExtractImage(1).OpenSamples();
        Assert.Equal(255, original.ReadByte());
        using var removedForm = manifest.Remove(0);
        Assert.Equal(2, PdfPageGraphics.Inspect(removedForm.CreateSnapshot(), 0).Occurrences.Count);
    }

    [Fact]
    public void MasksProfilesAndOwnedDisplayMetadataSurvivePdfRoundTrip()
    {
        using var input = new MemoryStream(ImageAssetTests.Fixture("rgba.png"));
        var asset = PdfImages.Read(input, new(true));
        using var document = Repeated(asset);
        var manifest = PdfPageGraphics.Inspect(document.CreateSnapshot(), 0);
        var extracted = manifest.ExtractImage(1);
        using var mask = extracted.SoftMask!.OpenSamples();
        Assert.Equal(255, mask.ReadByte());
        Assert.Equal(128, mask.ReadByte());
        using var oriented = Repeated(Pixel(8, new(PdfImageOrientation.Rotate90, 144, 72)));
        var metadata = PdfPageGraphics.Inspect(oriented.CreateSnapshot(), 0).ExtractImage(1).Metadata;
        Assert.Equal(PdfImageOrientation.Rotate90, metadata.Orientation);
        Assert.Equal(144, metadata.HorizontalDpi);
        Assert.Equal(72, metadata.VerticalDpi);
    }

    [Fact]
    public void DeduplicationChecksWholeGraphAndOriginalDisplayIdentity()
    {
        var a = Pixel(1);
        var b = Pixel(1);
        var c = Pixel(2);
        var d = Pixel(1, new(PdfImageOrientation.Normal, 72, 72));
        var result = PdfImages.Deduplicate(new[] { a, b, c, d, d });
        Assert.Equal(3, result.UniqueCount);
        Assert.Same(a, result.Images[1]);
        Assert.Same(c, result.Images[2]);
        Assert.Same(d, result.Images[4]);
        var key = PdfImages.WithColorKey(b, new[] { 1, 1, 0, 0, 0, 0 });
        Assert.Equal(2, PdfImages.Deduplicate(new[] { a, key }).UniqueCount);
        Assert.Equal("Image.BatchLimit", Assert.Throws<PdfException>(() => PdfImages.Deduplicate(new[] { a, b }, 1)).Diagnostic.Code);
        bool touched = false;
        IEnumerable<PdfImageAsset> Source()
        {
            touched = true;
            yield return a;
        }

        Assert.Throws<OperationCanceledException>(() => PdfImages.Deduplicate(Source(), cancellationToken: new CancellationToken(true)));
        Assert.False(touched);
    }

    [Fact]
    public void InlinePixelLimitChargesOnceAndNamedColorSpacesResolveInPageScope()
    {
        using var standalone = new MemoryStream(Encoding.ASCII.GetBytes("BI /W 1 /H 1 /BPC 8 /CS /RGB ID abc EI"));
        Assert.Single(PdfContentProgram.Parse(standalone, policy: new(limits: new(maximumPixels: 1))).Operations);
        using var doc = Pdf.Create().AddPage();
        using var bytes = new MemoryStream();
        doc.Save(bytes);
        bytes.Position = 0;
        using var opened = Pdf.Open(bytes);
        using var edit = new PdfTransaction(opened.SourceDocument);
        var page = opened.Pages[0].Source!;
        var entries = page.Dictionary.Entries.ToDictionary(p => p.Key, p => p.Value);
        using var data = new MemoryStream(Encoding.ASCII.GetBytes("BI /W 1 /H 1 /BPC 8 /CS /Colors ID abc EI"));
        entries["Contents"] = PdfValue.Reference(edit.Add(PdfValues.Stream(PdfValue.Dictionary(Array.Empty<KeyValuePair<string, PdfValue>>()), data)));
        entries["Resources"] = PdfValue.Dictionary(new[] { new KeyValuePair<string, PdfValue>("ColorSpace", PdfValue.Dictionary(new[] { new KeyValuePair<string, PdfValue>("Colors", PdfValue.Name("DeviceRGB")) })) });
        edit.Set(page.ObjectId, PdfValue.Dictionary(entries));
        using var output = new MemoryStream();
        edit.Save(output);
        output.Position = 0;
        using var named = Pdf.Open(output);
        var manifest = PdfPageGraphics.Inspect(named.CreateSnapshot(), 0, new(limits: new(maximumPixels: 1)));
        Assert.Equal(PdfGraphicsOccurrenceKind.InlineImage, Assert.Single(manifest.Occurrences).Kind);
        var extracted = manifest.ExtractImage(0);
        using var samples = extracted.OpenSamples();
        Assert.Equal(97, samples.ReadByte());
        Assert.Equal("DeviceRGB", extracted.Definition.Entries["ColorSpace"].NameValue);
        using var removed = manifest.Remove(0);
        Assert.Empty(PdfPageGraphics.Inspect(removed.CreateSnapshot(), 0).Occurrences);
        using var replaced = manifest.ReplaceImage(0, Pixel(9));
        Assert.Equal(PdfGraphicsOccurrenceKind.Image, Assert.Single(PdfPageGraphics.Inspect(replaced.CreateSnapshot(), 0).Occurrences).Kind);
    }

    [Fact]
    public void InspectionAndEditingHonorLimitsAndCancellation()
    {
        using var document = Repeated(Pixel(1));
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfPageGraphics.Inspect(document.CreateSnapshot(), 0, new(limits: new(maximumPixels: 1)))).Diagnostic.Code);
        Assert.Throws<OperationCanceledException>(() => PdfPageGraphics.Inspect(document.CreateSnapshot(), 0, cancellationToken: new CancellationToken(true)));
        var manifest = PdfPageGraphics.Inspect(document.CreateSnapshot(), 0);
        Assert.Throws<OperationCanceledException>(() => manifest.Remove(1, cancellationToken: new CancellationToken(true)));
    }

    private static PdfDocument Flat(PdfGraphicsResource image)
    {
        var document = Pdf.Create().AddPage();
        document.Pages[0].SetResource("Image", image);
        document.Pages[0].Content = PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Image")) });
        using var bytes = new MemoryStream();
        document.Save(bytes);
        document.Dispose();
        bytes.Position = 0;
        return Pdf.Open(bytes);
    }

    private static PdfDocument CatalogMarker(PdfDocument document, bool signature)
    {
        using var edit = new PdfTransaction(document.SourceDocument);
        var root = edit.Trailer.Entries["Root"].ReferenceId;
        var catalog = edit.Resolve(root).Entries.ToDictionary(p => p.Key, p => p.Value);
        catalog[signature ? "RetainedSignature" : "StructTreeRoot"] = PdfValue.Reference(edit.Add(signature ? PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Sig")), ("ByteRange", PdfGraphicsNumbers.Array(0, 1, 2, 3)), ("Contents", PdfValue.String(0))) : PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("StructTreeRoot")))));
        edit.Set(root, PdfValue.Dictionary(catalog));
        using var output = new MemoryStream();
        edit.Save(output, new(allowSignatureInvalidation: signature));
        output.Position = 0;
        return Pdf.Open(output);
    }

    [Fact]
    public void SignedInspectionIsReadOnlyAndContentEditsRespectSignatureAndStructurePolicies()
    {
        using var plain = Flat(Pixel(3).Resource);
        using var signed = CatalogMarker(plain, true);
        var manifest = PdfPageGraphics.Inspect(signed.CreateSnapshot(), 0);
        Assert.Single(manifest.Occurrences);
        Assert.Equal("Writer.SignatureRewrite", Assert.Throws<PdfException>(() => manifest.Remove(0)).Diagnostic.Code);
        using var allowed = manifest.Remove(0, new(allowSignatureInvalidation: true));
        Assert.Contains(allowed.LastOperationReport!.Diagnostics, d => d.Code == "Writer.SignatureRewriteAllowed");
        using var tagged = CatalogMarker(plain, false);
        var structure = PdfPageGraphics.Inspect(tagged.CreateSnapshot(), 0);
        Assert.Equal("Structure.SourceEditPolicy", Assert.Throws<PdfException>(() => structure.Remove(0)).Diagnostic.Code);
    }

    [Fact]
    public void HostileMetadataAndOwnerDependentGraphsAreExplicitAndBounded()
    {
        var pixel = Pixel(4);
        var entries = pixel.Definition.Entries.ToDictionary(p => p.Key, p => p.Value);
        using var xml = new MemoryStream(Encoding.UTF8.GetBytes("<!DOCTYPE x [<!ENTITY a SYSTEM 'file:///never-read'>]><x>&a;</x>"));
        entries["Metadata"] = PdfValues.Stream(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Metadata")), ("Subtype", PdfValue.Name("XML"))), xml);
        using var raw = pixel.Definition.OpenRawStream();
        var resource = PdfGraphicsResource.Create(PdfGraphicsResourceCategory.XObject, PdfValues.Stream(PdfValue.Dictionary(entries), raw), "1.4");
        using var document = Flat(resource);
        var manifest = PdfPageGraphics.Inspect(document.CreateSnapshot(), 0);
        Assert.Equal("Image.MetadataXml", Assert.Throws<PdfException>(() => manifest.ExtractImage(0)).Diagnostic.Code);
        entries.Remove("Metadata");
        entries["OC"] = PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("OCG")));
        using var data = pixel.Definition.OpenRawStream();
        var owned = PdfGraphicsResource.Create(PdfGraphicsResourceCategory.XObject, PdfValues.Stream(PdfValue.Dictionary(entries), data), "1.5");
        using var scoped = Flat(owned);
        Assert.Equal("Image.OwnerDependentResource", Assert.Throws<PdfException>(() => PdfPageGraphics.Inspect(scoped.CreateSnapshot(), 0).ExtractImage(0)).Diagnostic.Code);
        entries.Remove("OC");
        entries["Width"] = PdfValue.Integer(int.MaxValue);
        using var largeData = pixel.Definition.OpenRawStream();
        var large = PdfGraphicsResource.Create(PdfGraphicsResourceCategory.XObject, PdfValues.Stream(PdfValue.Dictionary(entries), largeData), "1.2");
        using var largeDoc = Flat(large);
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfPageGraphics.Inspect(largeDoc.CreateSnapshot(), 0, new(limits: new(maximumPixels: 100)))).Diagnostic.Code);
    }

    [Fact]
    public void CSharpAndVisualBasicImageConsumersPreserveOriginalFilterBytesAndAlpha()
    {
        foreach (var write in new Func<Stream, Stream, bool, PdfOperationReport>[]
        {
            ContractExamples.CSharp.ContractExample.WriteImagePdf,
            ContractExamples.VisualBasic.ContractExample.WriteImagePdf
        }

        )
        {
            using var input = new MemoryStream(ImageAssetTests.Fixture("rgba.png"));
            using var output = new MemoryStream();
            Assert.True(write(input, output, true).Succeeded);
            Assert.True(input.CanRead);
            Assert.True(output.CanWrite);
            output.Position = 0;
            using var document = Pdf.Open(output);
            var manifest = PdfPageGraphics.Inspect(document.CreateSnapshot(), 0);
            int index = Enumerable.Range(0, manifest.Occurrences.Count).Single(i => manifest.Occurrences[i].Kind == PdfGraphicsOccurrenceKind.Image);
            var image = manifest.ExtractImage(index);
            Assert.NotNull(image.SoftMask);
            Assert.Equal(6, image.Info.Width * image.Info.Height);
        }
    }
}
