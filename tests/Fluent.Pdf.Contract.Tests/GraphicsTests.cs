using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class GraphicsTests
{
    internal static PdfDrawing Rectangle(PdfColor? color = null)
    {
        var operations = new List<PdfContentOperation>();
        var bindings = new List<PdfResourceBinding>();
        if (color != null)
        {
            bindings.Add(new("C1", color.ColorSpace.AsResource()));
            operations.Add(new(PdfOperator.FillColorSpace, PdfValue.Name("C1")));
            operations.Add(new(PdfOperator.FillColor, color.Components.Select(PdfValues.Real).ToArray()));
        }

        operations.Add(new(PdfOperator.Rectangle, PdfValue.Integer(10), PdfValue.Integer(10), PdfValue.Integer(80), PdfValue.Integer(60)));
        operations.Add(new(PdfOperator.Fill));
        return PdfDrawing.Create(operations, new(bindings.ToArray()));
    }

    internal static byte[] Save(PdfDrawing drawing, PdfWriteOptions? options = null)
    {
        using var document = Pdf.Create().AddPage(new PdfPageSize(120, 100));
        foreach (var binding in drawing.Resources.Bindings)
            document.Pages[0].SetResource(binding.Name, binding.Resource);
        document.Pages[0].Content = drawing.Content;
        using var output = new MemoryStream();
        document.Save(output, writeOptions: options);
        Assert.True(output.CanWrite);
        return output.ToArray();
    }

    internal static PdfValue Resources(PdfDocument document)
    {
        var source = document.SourceDocument!;
        var value = document.Pages[0].Source!.EffectiveAttributes.Entries["Resources"];
        return Resolve(source, value);
    }

    internal static PdfValue Resolve(PdfSyntaxDocument source, PdfValue value)
    {
        while (value.Kind == PdfValueKind.Reference)
            value = source.Resolve(value.ReferenceId);
        return value;
    }

    [Fact]
    public void VectorPathsEditWithoutMutatingOriginalSegments()
    {
        var path = PdfPath.Create(new[] { PdfPathSegment.Move(1, 2), PdfPathSegment.Curve(3, 4, 5, 6, 7, 8), PdfPathSegment.Close() });
        var edited = path.Edit(1, 1, new[] { PdfPathSegment.Line(9, 10) });
        Assert.Equal(PdfOperator.CurveTo, path.Operations[1].Operation);
        Assert.Equal(PdfOperator.LineTo, edited.Operations[1].Operation);
        Assert.Equal(3, path.Segments.Count);
        Assert.Equal("Graphics.PathCurrentPoint", Assert.Throws<PdfException>(() => PdfPath.Create(new[] { PdfPathSegment.Line(1, 2) })).Diagnostic.Code);
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfPathSegment.Move(double.NaN, 2));
    }

    [Fact]
    public void AffineGeometryComposesAndInvertsWithExplicitOrder()
    {
        var translate = new PdfMatrix(1, 0, 0, 1, 5, -7);
        var scale = new PdfMatrix(2, 0, 0, 3, 0, 0);
        var matrix = translate.Multiply(scale);
        var point = matrix.Transform(new(4, 5));
        Assert.Equal(13, point.X);
        Assert.Equal(8, point.Y);
        var original = matrix.Invert().Transform(point);
        Assert.Equal(4, original.X, 10);
        Assert.Equal(5, original.Y, 10);
        Assert.Throws<InvalidOperationException>(() => default(PdfMatrix).Invert());
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfRect(0, 0, -1, 2));
    }

    [Fact]
    public void ColorSpacesPreserveExplicitChannelModelsAndTintFunctions()
    {
        Assert.Equal(1, PdfColorSpace.Gray.Components);
        Assert.Equal(3, PdfColorSpace.Rgb.Components);
        Assert.Equal(4, PdfColorSpace.Cmyk.Components);
        var lab = PdfColorSpace.Lab(.9642, .8249);
        Assert.Equal("Lab", lab.Definition.Items[0].NameValue);
        _ = new PdfColor(lab, 50, 25, -20);
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfColor(PdfColorSpace.Rgb, 1, 0, 2));
        var tint = PdfFunction.Exponential(new[] { 1d, 1d, 1d }, new[] { .1, .2, .3 });
        var spot = PdfColorSpace.Separation("OriginalSpot", PdfColorSpace.Rgb, tint);
        Assert.Equal("Separation", spot.Definition.Items[0].NameValue);
        var samples = PdfFunction.Sampled(new[] { 0d, 1d, 0d, 1d }, new[] { 0d, 1d, 0d, 1d, 0d, 1d }, new[] { 2, 2 }, 8, new byte[12]);
        var deviceN = PdfColorSpace.DeviceN(new[] { "InkA", "InkB" }, PdfColorSpace.Rgb, samples);
        Assert.Equal(2, deviceN.Components);
        Assert.Equal("1.3", deviceN.MinimumVersion);
        Assert.Throws<ArgumentException>(() => PdfColorSpace.DeviceN(new[] { "A", "A" }, PdfColorSpace.Rgb, samples));
    }

    [Theory]
    [InlineData("original-srgb.icc", 3, "2.0")]
    [InlineData("original-gray.icc", 1, "1.5")]
    public void IndependentIccProfilesRemainExactIndirectStreams(string file, int channels, string version)
    {
        byte[] bytes = File.ReadAllBytes(Fixture(file));
        using var input = new MemoryStream(bytes);
        var color = PdfColorSpace.Icc(input);
        Assert.True(input.CanRead);
        Assert.Equal(channels, color.Components);
        Assert.Equal(version, color.MinimumVersion);
        byte[] pdf = Save(Rectangle(new PdfColor(color, Enumerable.Repeat(.5, channels).ToArray())), new(version: version));
        using var source = Pdf.Open(new MemoryStream(pdf));
        var resources = Resources(source);
        var colors = Resolve(source.SourceDocument!, resources.Entries["ColorSpace"]);
        var space = Resolve(source.SourceDocument!, colors.Entries["C1"]);
        Assert.Equal(PdfValueKind.Reference, space.Items[1].Kind);
        var profile = Resolve(source.SourceDocument!, space.Items[1]);
        using var encoded = profile.OpenRawStream();
        using var copy = new MemoryStream();
        encoded.CopyTo(copy);
        Assert.Equal(bytes, copy.ToArray());
        Assert.Equal(version, source.SourceDocument!.Version);
        if (channels == 3)
        {
            var group = PdfTransparencyGroup.Create(Rectangle(), new(0, 0, 100, 100), color);
            Assert.Equal(version, group.Resource.MinimumVersion);
            var tint = PdfFunction.Exponential(new[] { 1d, 1d, 1d }, new[] { 0d, 0d, 0d });
            Assert.Equal(version, PdfColorSpace.Separation("IccSpot", color, tint).MinimumVersion);
            Assert.Equal("Writer.VersionTooLow", Assert.Throws<PdfException>(() => Save(Rectangle(new PdfColor(color, .1, .2, .3)), new(version: "1.7"))).Diagnostic.Code);
        }
    }

    [Theory]
    [InlineData(0, "Graphics.IccHeader")]
    [InlineData(8, "Graphics.IccVersion")]
    [InlineData(16, "Graphics.IccProfile")]
    [InlineData(132, "Graphics.IccTags")]
    public void MalformedIccHeadersAndTablesRejectWithoutClosingInput(int offset, string diagnostic)
    {
        byte[] bytes = File.ReadAllBytes(Fixture("original-srgb.icc"));
        if (offset == 132)
            Array.Copy(bytes, 132, bytes, 144, 4);
        else
            bytes[offset] = 255;
        using var input = new MemoryStream(bytes);
        Assert.Equal(diagnostic, Assert.Throws<PdfException>(() => PdfColorSpace.Icc(input)).Diagnostic.Code);
        Assert.True(input.CanRead);
    }

    [Fact]
    public void IccInputAndExpansionBudgetsAndCancellationAreMandatory()
    {
        byte[] bytes = File.ReadAllBytes(Fixture("original-srgb.icc"));
        using var input = new MemoryStream(bytes);
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => PdfColorSpace.Icc(input, policy: new(limits: new(maximumInputBytes: 127)))).Diagnostic.Code);
        input.Position = 0;
        Assert.Throws<OperationCanceledException>(() => PdfColorSpace.Icc(input, cancellationToken: new CancellationToken(true)));
        Assert.True(input.CanRead);
    }

    [Fact]
    public void DrawingStateIsIsolatedAndMissingNamedResourcesFailExplicitly()
    {
        var drawing = Rectangle(new(PdfColorSpace.Cmyk, .1, .2, .3, .4));
        Assert.Equal(PdfOperator.SaveState, drawing.Content.Operations[0].Operation);
        Assert.Equal(PdfOperator.RestoreState, drawing.Content.Operations[drawing.Content.Operations.Count - 1].Operation);
        Assert.Equal("Graphics.ResourceMissing", Assert.Throws<PdfException>(() => PdfDrawing.Create(new[] { new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Absent")) })).Diagnostic.Code);
        Assert.Equal("Graphics.ForeignReference", Assert.Throws<PdfException>(() => PdfGraphicsResource.Create(PdfGraphicsResourceCategory.ExtGState, Dict(("SMask", PdfValue.Reference(new(1)))), "1.4")).Diagnostic.Code);
    }

    [Fact]
    public void UnknownBlendModesAreStrictFailuresAndPermissiveLossesAreRetained()
    {
        Assert.Equal("Graphics.UnsupportedBlendMode", Assert.Throws<PdfException>(() => PdfGraphicsState.Create("UnknownMode")).Diagnostic.Code);
        var state = PdfGraphicsState.Create("UnknownMode", policy: new(PdfProcessingMode.Permissive));
        Assert.Equal("Normal", state.BlendMode);
        var diagnostic = Assert.Single(state.Resource.OperationReport.Diagnostics);
        Assert.Equal(PdfLossCategory.SubstitutedContent, diagnostic.LossCategory);
        Assert.Equal(PdfDiagnosticSeverity.Warning, diagnostic.Severity);
    }

    [Fact]
    public void EveryStandardBlendModeRetainsItsExactStateAndVersion()
    {
        foreach (string name in PdfGraphicsState.SupportedBlendModes)
        {
            var state = PdfGraphicsState.Create(name, .4, .7, strokeOverprint: true, fillOverprint: true, overprintMode: 1);
            Assert.Equal(name, state.Resource.Definition.Entries["BM"].NameValue);
            Assert.Equal(.4, state.Resource.Definition.Entries["ca"].NumberValue);
            Assert.Equal("1.4", state.Resource.MinimumVersion);
        }
    }

    [Fact]
    public void SoftMasksRemainVectorTransparencyGroupsWithExactBackdrop()
    {
        var group = PdfTransparencyGroup.Create(Rectangle(new(PdfColorSpace.Gray, .5)), new(0, 0, 100, 100), PdfColorSpace.Gray, true, true);
        var mask = new PdfSoftMask(group, PdfSoftMaskKind.Luminosity, new(PdfColorSpace.Gray, .25));
        var state = PdfGraphicsState.Create("Multiply", .8, .9, mask);
        var drawing = PdfDrawing.Create(new[] { new PdfContentOperation(PdfOperator.ExtendedState, PdfValue.Name("G1")), new PdfContentOperation(PdfOperator.Rectangle, PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(100), PdfValue.Integer(100)), new PdfContentOperation(PdfOperator.Fill) }, new(new PdfResourceBinding("G1", state.Resource)));
        using var document = Pdf.Open(new MemoryStream(Save(drawing)));
        var source = document.SourceDocument!;
        var states = Resolve(source, Resources(document).Entries["ExtGState"]);
        var saved = Resolve(source, states.Entries["G1"]);
        var soft = Resolve(source, saved.Entries["SMask"]);
        Assert.Equal("Luminosity", soft.Entries["S"].NameValue);
        Assert.Equal(.25, soft.Entries["BC"].Items[0].NumberValue);
        Assert.Equal(PdfValueKind.Reference, soft.Entries["G"].Kind);
        var form = Resolve(source, soft.Entries["G"]);
        Assert.Equal("Form", form.Entries["Subtype"].NameValue);
        var transparency = form.Entries["Group"];
        Assert.True(transparency.Entries["I"].BooleanValue);
        Assert.True(transparency.Entries["K"].BooleanValue);
    }

    [Fact]
    public void TilingPatternsKeepPrivateScopesAndRejectColorSelectionInUncoloredCells()
    {
        var monochrome = Rectangle();
        var plain = PdfPattern.Tiling(monochrome, new(0, 0, 100, 80), 100, 80, colored: false);
        Assert.False(plain.Colored);
        Assert.Equal(2, plain.Resource.Definition.Entries["PaintType"].IntegerValue);
        Assert.Equal("Graphics.UncoloredPatternContent", Assert.Throws<PdfException>(() => PdfPattern.Tiling(Rectangle(new(PdfColorSpace.Rgb, 1, 0, 0)), new(0, 0, 100, 80), 100, 80, colored: false)).Diagnostic.Code);
        var colored = PdfPattern.Tiling(Rectangle(new(PdfColorSpace.Rgb, 1, 0, 0)), new(0, 0, 100, 80), 100, 80);
        Assert.True(colored.Colored);
    }

    [Fact]
    public void GradientAndPiecewiseFunctionsRemainStandardVectorResources()
    {
        var redBlue = PdfFunction.Exponential(new[] { 1d, 0d, 0d }, new[] { 0d, 0d, 1d });
        var first = PdfShading.Axial(PdfColorSpace.Rgb, new(0, 0), new(100, 0), redBlue, true, true);
        Assert.Equal(2, first.ShadingType);
        var radial = PdfShading.Radial(PdfColorSpace.Rgb, new(20, 20), 0, new(20, 20), 30, redBlue);
        Assert.Equal(3, radial.ShadingType);
        var stitch = PdfFunction.Stitch(new[] { redBlue, redBlue }, new[] { .5 }, new[] { 0d, 1d, 1d, 0d });
        Assert.Equal(3, stitch.Definition.Entries["FunctionType"].IntegerValue);
        var pattern = PdfPattern.Shading(first, state: PdfGraphicsState.Create("Screen"));
        Assert.Equal(2, pattern.Resource.Definition.Entries["PatternType"].IntegerValue);
        Assert.Equal("1.4", pattern.Resource.MinimumVersion);
        var grid = PdfFunction.Sampled(new[] { 0d, 1d, 0d, 1d }, new[] { 0d, 1d, 0d, 1d, 0d, 1d }, new[] { 2, 2 }, 8, new byte[12]);
        Assert.Equal(1, PdfShading.Functional(PdfColorSpace.Rgb, grid, new(0, 0, 1, 1)).ShadingType);
    }

    [Theory]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    [InlineData(7)]
    public void MeshShadingsValidateExactPackedPacketsAndPreserveTheirBytes(int type)
    {
        byte[] bytes = MeshBytes(type);
        using var input = new MemoryStream(bytes);
        var shading = PdfShading.Mesh(PdfColorSpace.Rgb, type, input, 8, 8, new[] { 0d, 100d, 0d, 100d, 0d, 1d, 0d, 1d, 0d, 1d }, bitsPerFlag: 8, verticesPerRow: 2);
        Assert.True(input.CanRead);
        Assert.Equal(type, shading.ShadingType);
        Assert.Equal("1.3", shading.Resource.MinimumVersion);
        using var data = shading.Resource.Definition.OpenRawStream();
        using var output = new MemoryStream();
        data.CopyTo(output);
        Assert.Equal(bytes, output.ToArray());
        using var truncated = new MemoryStream(bytes.Take(bytes.Length - 1).ToArray());
        Assert.Equal("Graphics.MeshFraming", Assert.Throws<PdfException>(() => PdfShading.Mesh(PdfColorSpace.Rgb, type, truncated, 8, 8, new[] { 0d, 100d, 0d, 100d, 0d, 1d, 0d, 1d, 0d, 1d }, bitsPerFlag: 8, verticesPerRow: 2)).Diagnostic.Code);
    }

    [Fact]
    public void InvalidFunctionGridsAndResourceBudgetsRejectBeforeExpansion()
    {
        Assert.Equal("Graphics.FunctionSize", Assert.Throws<PdfException>(() => PdfFunction.Sampled(new[] { 0d, 1d, 0d, 1d }, new[] { 0d, 1d }, new[] { int.MaxValue, int.MaxValue }, 32, Array.Empty<byte>())).Diagnostic.Code);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfPath.Create(Enumerable.Range(0, 100).Select(i => PdfPathSegment.Move(i, i)), new(limits: new(maximumObjects: 3)))).Diagnostic.Code);
        Assert.Throws<ArgumentException>(() => PdfFunction.Exponential(new[] { 0d }, new[] { 1d }, .5, -1, 1));
    }

    [Fact]
    public void SavedSourceResourcesMergeWithoutChangingOtherBindingsOrSnapshots()
    {
        using var first = Pdf.Open(new MemoryStream(Save(Rectangle(new(PdfColorSpace.Rgb, 1, 0, 0)))));
        var replacement = PdfColorSpace.Lab(.9642, .8249).AsResource();
        first.Pages[0].SetResource("Lab", replacement);
        var snapshot = first.CreateSnapshot();
        first.Pages[0].RemoveResource(PdfGraphicsResourceCategory.ColorSpace, "Lab");
        Assert.Single(snapshot.Pages[0].GraphicsResources.Bindings);
        first.Pages[0].SetResource("Lab", replacement);
        using var output = new MemoryStream();
        first.Save(output);
        output.Position = 0;
        using var saved = Pdf.Open(output);
        var colors = Resolve(saved.SourceDocument!, Resources(saved).Entries["ColorSpace"]);
        Assert.True(colors.Entries.ContainsKey("C1"));
        Assert.True(colors.Entries.ContainsKey("Lab"));
    }

    [Fact]
    public void IndependentGraphicsJobsRetainDeterministicResourceScopes()
    {
        var drawing = Rectangle(new(PdfColorSpace.Rgb, .2, .4, .6));
        byte[] expected = Save(drawing);
        Parallel.For(0, 32, _ => Assert.Equal(expected, Save(drawing)));
    }

    [Fact]
    public void ColorStateRestoresChannelCountsAndChecksPatternPaintTypes()
    {
        var wrong = new[]
        {
            new PdfContentOperation(PdfOperator.FillRgb, PdfValue.Integer(1), PdfValue.Integer(0), PdfValue.Integer(0)),
            new PdfContentOperation(PdfOperator.FillColor, PdfValue.Integer(1))
        };
        Assert.Equal("Graphics.ColorChannels", Assert.Throws<PdfException>(() => PdfDrawing.Create(wrong)).Diagnostic.Code);
        var restored = new[]
        {
            new PdfContentOperation(PdfOperator.SaveState),
            new PdfContentOperation(PdfOperator.FillRgb, PdfValue.Integer(1), PdfValue.Integer(0), PdfValue.Integer(0)),
            new PdfContentOperation(PdfOperator.RestoreState),
            new PdfContentOperation(PdfOperator.FillColor, PdfValue.Integer(1))
        };
        Assert.True(PdfDrawing.Create(restored).OperationReport.Succeeded);
        var uncolored = PdfPattern.Tiling(Rectangle(), new(0, 0, 100, 80), 100, 80, colored: false);
        var operations = new[]
        {
            new PdfContentOperation(PdfOperator.FillColorSpace, PdfValue.Name("Pattern")),
            new PdfContentOperation(PdfOperator.FillColorExtended, PdfValue.Name("P1"))
        };
        Assert.Equal("Graphics.PatternColorSpace", Assert.Throws<PdfException>(() => PdfDrawing.Create(operations, new(new PdfResourceBinding("P1", uncolored.Resource)))).Diagnostic.Code);
    }

    [Fact]
    public void ReusedFormsShareOneEagerFontProgramAcrossPrivateScopes()
    {
        var resource = PdfFontResource.CreateText(TextTests.Read("latin.ttf"), "office");
        var text = PdfDrawing.Create(FontTests.Show(resource).Operations, new(new PdfResourceBinding("F1", PdfGraphicsResource.Font(resource))));
        var form = PdfGraphicsObjects.Form(text, new(0, 0, 400, 120));
        var drawing = PdfDrawing.Create(new[] { new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("A")), new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("B")) }, new(new PdfResourceBinding("A", form), new PdfResourceBinding("B", form)));
        using var document = Pdf.Open(new MemoryStream(Save(drawing)));
        var source = document.SourceDocument!;
        var forms = Resolve(source, Resources(document).Entries["XObject"]);
        Assert.Equal(forms.Entries["A"].ReferenceId, forms.Entries["B"].ReferenceId);
        var saved = Resolve(source, forms.Entries["A"]);
        Assert.True(saved.Entries["Resources"].Entries.ContainsKey("Font"));
    }

    [Fact]
    public void UncoloredNestedVectorFormsCannotHideColorOperations()
    {
        var form = PdfGraphicsObjects.Form(Rectangle(new(PdfColorSpace.Rgb, 1, 0, 0)), new(0, 0, 100, 80));
        var drawing = PdfDrawing.Create(new[] { new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("G")) }, new(new PdfResourceBinding("G", form)));
        Assert.Equal("Graphics.UncoloredPatternContent", Assert.Throws<PdfException>(() => PdfPattern.Tiling(drawing, new(0, 0, 100, 80), 100, 80, colored: false)).Diagnostic.Code);
    }

    [Fact]
    public void AdvancedUnknownBlendBehaviorIsReportedAndDeviceNReservedNamesAreChecked()
    {
        var definition = Dict(("BM", PdfValue.Name("VendorBlend")));
        Assert.Equal("Graphics.UnsupportedBlendMode", Assert.Throws<PdfException>(() => PdfGraphicsResource.Create(PdfGraphicsResourceCategory.ExtGState, definition, "1.4")).Diagnostic.Code);
        var resource = PdfGraphicsResource.Create(PdfGraphicsResourceCategory.ExtGState, definition, "1.4", new(PdfProcessingMode.Permissive));
        Assert.Equal(PdfLossCategory.UnsupportedContent, Assert.Single(resource.OperationReport.Diagnostics).LossCategory);
        var tint = PdfFunction.Sampled(new[] { 0d, 1d, 0d, 1d }, new[] { 0d, 1d, 0d, 1d, 0d, 1d }, new[] { 2, 2 }, 8, new byte[12]);
        Assert.Equal(2, PdfColorSpace.DeviceN(new[] { "None", "None" }, PdfColorSpace.Rgb, tint).Components);
        Assert.Throws<ArgumentException>(() => PdfColorSpace.DeviceN(new[] { "All", "Ink" }, PdfColorSpace.Rgb, tint));
    }

    [Fact]
    public void CSharpAndVisualBasicConsumersWriteReusableVectorCurves()
    {
        foreach (var write in new Func<Stream, PdfOperationReport>[]
        {
            ContractExamples.CSharp.ContractExample.WriteVectorPdf,
            ContractExamples.VisualBasic.ContractExample.WriteVectorPdf
        }

        )
        {
            using var output = new MemoryStream();
            Assert.True(write(output).Succeeded);
            Assert.True(output.CanWrite);
            output.Position = 0;
            using var document = Pdf.Open(output);
            Assert.Single(document.Pages[0].ReadContent().Operations, x => x.Operation == PdfOperator.DrawObject);
            var source = document.SourceDocument!;
            var reference = Resources(document).Entries["XObject"].Entries["Art"];
            using var data = source.DecodeStream(reference.ReferenceId).OpenRead();
            var program = PdfContentProgram.Parse(data);
            Assert.Contains(program.Operations, x => x.Operation == PdfOperator.CurveTo);
        }
    }

    [Fact]
    public void GraphicsOutputBudgetFailureRetainsTheExistingAtomicFileAndRecovers()
    {
        string directory = Path.Combine(Path.GetTempPath(), "fluent-graphics-atomic-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "output.pdf");
        byte[] original = new byte[]
        {
            3,
            2,
            1
        };
        File.WriteAllBytes(path, original);
        try
        {
            using var document = Pdf.Create(new PdfOptions(operationPolicy: new(limits: new(maximumOutputBytes: 128)))).AddPage();
            var drawing = Rectangle(new(PdfColorSpace.Rgb, .2, .4, .6));
            foreach (var binding in drawing.Resources.Bindings)
                document.Pages[0].SetResource(binding.Name, binding.Resource);
            document.Pages[0].Content = drawing.Content;
            Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => document.SaveFile(path)).Diagnostic.Code);
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Single(Directory.GetFiles(directory));
            Assert.False(document.LastOperationReport!.Succeeded);
            Assert.NotEmpty(Save(drawing));
        }
        finally
        {
            File.Delete(path);
            Directory.Delete(directory);
        }
    }

    internal static byte[] MeshBytes(int type)
    {
        if (type == 4)
            return new byte[]
            {
                0,
                0,
                0,
                255,
                0,
                0,
                0,
                255,
                0,
                0,
                255,
                0,
                0,
                0,
                255,
                0,
                0,
                255
            };
        if (type == 5)
            return new byte[]
            {
                0,
                0,
                255,
                0,
                0,
                255,
                0,
                0,
                255,
                0,
                0,
                255,
                0,
                0,
                255,
                255,
                255,
                255,
                255,
                255
            };
        var points = type == 6 ? new byte[]
        {
            0,
            0,
            0,
            85,
            0,
            170,
            0,
            255,
            85,
            255,
            170,
            255,
            255,
            255,
            255,
            170,
            255,
            85,
            255,
            0,
            170,
            0,
            85,
            0
        }

        : new byte[]
        {
            0,
            0,
            0,
            85,
            0,
            170,
            0,
            255,
            85,
            255,
            170,
            255,
            255,
            255,
            255,
            170,
            255,
            85,
            255,
            0,
            170,
            0,
            85,
            0,
            85,
            85,
            85,
            170,
            170,
            170,
            170,
            85
        };
        return new byte[]
        {
            0
        }.Concat(points).Concat(new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255 }).ToArray();
    }

    internal static string Fixture(string file) => Path.Combine(AppContext.BaseDirectory, "Fixtures", "graphics", file);
    private static PdfValue Dict(params (string Key, PdfValue Value)[] values) => PdfValue.Dictionary(values.Select(x => new KeyValuePair<string, PdfValue>(x.Key, x.Value)));
}
