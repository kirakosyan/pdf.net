using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class CanvasTests
{
    [Theory]
    [InlineData(PdfPageRotation.None, 0, 0, 320, 160)]
    [InlineData(PdfPageRotation.Clockwise90, 160, 0, 0, 320)]
    [InlineData(PdfPageRotation.Clockwise180, 320, 160, 0, 0)]
    [InlineData(PdfPageRotation.Clockwise270, 0, 320, 160, 0)]
    public void NonzeroOriginsUnitsAndAllRotationsHaveKnownCoordinateAnswers(PdfPageRotation rotation, double x0, double y0, double x1, double y1)
    {
        var geometry = Geometry(rotation);
        var matrix = geometry.UserToDisplay();
        var topLeft = matrix.Transform(new(-10, 100));
        var bottomRight = matrix.Transform(new(150, 20));
        Assert.Equal(x0, topLeft.X, 10);
        Assert.Equal(y0, topLeft.Y, 10);
        Assert.Equal(x1, bottomRight.X, 10);
        Assert.Equal(y1, bottomRight.Y, 10);
        var restored = geometry.DisplayToUser().Transform(topLeft);
        Assert.Equal(-10, restored.X, 10);
        Assert.Equal(100, restored.Y, 10);
    }

    [Fact]
    public void AllPageBoxesAndUserUnitRoundTripAndResizeWithoutBreakingContainment()
    {
        var geometry = Geometry(PdfPageRotation.Clockwise90);
        using var document = Pdf.Create().AddPage(geometry);
        var snapshot = document.CreateSnapshot();
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        using var loaded = Pdf.Open(output);
        Assert.True(geometry.Equals(loaded.Pages[0].Geometry));
        loaded.Pages[0].Size = new(800, 400);
        Assert.Equal(800, loaded.Pages[0].Geometry.Size.Width);
        Assert.True(PdfPageBoxes.Contains(loaded.Pages[0].Geometry.Boxes.Media, loaded.Pages[0].Geometry.Boxes.Art));
        Assert.True(snapshot.Pages[0].Geometry.Equals(geometry));
        Assert.Throws<ArgumentException>(() => new PdfPageBoxes(new(0, 0, 100, 100), new(0, 0, 101, 100)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfPageGeometry(geometry.Boxes, userUnit: double.NaN));
    }

    [Fact]
    public void UnitsMarginsAndBottomLeftOriginUseExplicitPhysicalConversions()
    {
        Assert.Equal(72, PdfUnits.Convert(25.4, PdfLengthUnit.Millimeter, PdfLengthUnit.Point), 10);
        Assert.Equal(1, PdfUnits.Convert(96, PdfLengthUnit.Pixel, PdfLengthUnit.Inch), 10);
        var geometry = new PdfPageGeometry(new(new(10, 20, 200, 100)), userUnit: 2);
        var top = PdfCanvas.Create(geometry, _ =>
        {
        }, new(PdfLengthUnit.Inch, margins: PdfMargins.Uniform(.25)));
        var point = top.ToUser.Transform(new(0, 0));
        Assert.Equal(19, point.X, 10);
        Assert.Equal(111, point.Y, 10);
        var bottom = PdfCanvas.Create(geometry, _ =>
        {
        }, new(PdfLengthUnit.Inch, PdfCanvasOrigin.BottomLeft, margins: PdfMargins.Uniform(.25)));
        point = bottom.ToUser.Transform(new(0, 0));
        Assert.Equal(19, point.X, 10);
        Assert.Equal(29, point.Y, 10);
        Assert.Throws<PdfException>(() => PdfCanvas.Create(geometry, _ =>
        {
        }, new(margins: PdfMargins.Uniform(1000))));
    }

    [Fact]
    public void TransformedFramesUseQuadHitTestsRatherThanOnlyTheirAxisAlignedBounds()
    {
        var geometry = SimpleGeometry();
        var canvas = PdfCanvas.Create(geometry, c =>
        {
            c.Translate(80, 20);
            c.Rotate(45);
            c.DrawPath(Rectangle(0, 0, 20, 20));
        });
        var placement = Assert.Single(canvas.Placements);
        Assert.True(placement.HitTest(placement.Transform.Transform(new(10, 10))));
        Assert.False(placement.HitTest(new(placement.Bounds.X + .001, placement.Bounds.Y + .001)));
        Assert.True(placement.Bounds.Width > 20);
        Assert.False(default(PdfQuad).Contains(new(0, 0)));
    }

    [Fact]
    public void PathsMeasureBezierExtremaAndRespectBothFillRules()
    {
        var curve = PdfPath.Create(new[] { PdfPathSegment.Move(0, 0), PdfPathSegment.Curve(0, 100, 100, 100, 100, 0), PdfPathSegment.Close() });
        var bounds = curve.GetBounds();
        Assert.Equal(100, bounds.Width, 9);
        Assert.Equal(75, bounds.Height, 9);
        Assert.True(curve.Contains(new(50, 50)));
        Assert.False(curve.Contains(new(50, 80)));
        var ring = PdfPath.Create(new[] { PdfPathSegment.Rectangle(new(0, 0, 100, 100)), PdfPathSegment.Rectangle(new(25, 25, 50, 50)) });
        Assert.True(ring.Contains(new(50, 50)));
        Assert.False(ring.Contains(new(50, 50), evenOdd: true));
        Assert.True(ring.Contains(new(10, 10), evenOdd: true));
    }

    [Fact]
    public void CapturedPathClipsTransformsAndZOrderRemainIndependentOfLaterState()
    {
        var canvas = PdfCanvas.Create(SimpleGeometry(), c =>
        {
            c.Clip(Rectangle(0, 0, 30, 30));
            c.DrawPath(Rectangle(0, 0, 50, 50), zIndex: 4);
            using (c.Save())
            {
                c.Translate(10, 0);
                c.Clip(Rectangle(0, 0, 10, 10));
                c.DrawPath(Rectangle(0, 0, 50, 50), zIndex: 2);
            }

            c.DrawPath(Rectangle(0, 0, 50, 50), zIndex: 3);
        });
        Assert.Equal(new[] { 2, 3, 4 }, canvas.Placements.Select(x => x.ZIndex));
        Assert.Equal(2, canvas.HitTest(new(25, 25), topmostOnly: false).Count);
        Assert.Empty(canvas.HitTest(new(40, 40), topmostOnly: false));
        Assert.Equal(4, Assert.Single(canvas.HitTest(new(5, 5))).ZIndex);
    }

    [Fact]
    public void ImageFittingPreservesDeclaredPixelAspectAndClipsCoverOverflow()
    {
        var image = Image();
        var canvas = PdfCanvas.Create(SimpleGeometry(), c =>
        {
            c.DrawImage(image, new(0, 0, 40, 40));
            c.DrawImage(image, new(60, 0, 40, 40), PdfCanvasFit.Cover);
        });
        Assert.Equal(40, canvas.Placements[0].Bounds.Width);
        Assert.Equal(20, canvas.Placements[0].Bounds.Height);
        Assert.Equal(10, canvas.Placements[0].Bounds.Y);
        Assert.True(canvas.Placements[1].Bounds.Width > canvas.Placements[1].VisibleBounds.Width);
        Assert.False(canvas.Placements[1].HitTest(new(50, 20)));
        Assert.True(canvas.Placements[1].HitTest(new(70, 20)));
    }

    [Fact]
    public void FormMatricesAndExplicitTextPointSizesContributeToPlacementFrames()
    {
        var drawing = GraphicsTests.Rectangle(new(PdfColorSpace.Rgb, 1, 0, 0));
        var form = PdfGraphicsObjects.Form(drawing, new(0, 0, 100, 80), new PdfMatrix(2, 0, 0, 3, 10, 20));
        var text = PdfText.Shape("office", new(TextTests.Read("latin.ttf")), new(new PdfSimpleTextShaper()));
        var canvas = PdfCanvas.Create(SimpleGeometry(), c =>
        {
            c.DrawForm(form, new(0, 0, 60, 60));
            c.DrawText(text, new(20, 30), 12);
        }, new(PdfLengthUnit.Millimeter));
        Assert.Equal(2, canvas.Placements.Count);
        Assert.True(canvas.Placements[1].Bounds.Width > 0);
        Assert.True(PdfVersionNumber.Parse(canvas.Drawing.MinimumVersion) >= 14);
    }

    [Fact]
    public void CanvasAppliesOverlayUnderlayAndReplacementWithoutRewritingOriginalStreamBytes()
    {
        var original = GraphicsTests.Save(GraphicsTests.Rectangle(new(PdfColorSpace.Rgb, .1, .2, .3)));
        using var document = Pdf.Open(new MemoryStream(original));
        var source = document.SourceDocument!;
        var content = source.Resolve(document.Pages[0].Source!.Dictionary.Entries["Contents"].ReferenceId);
        byte[] before = Bytes(content);
        var canvas = PdfCanvas.Create(document.Pages[0].Geometry, c => c.DrawPath(Rectangle(10, 10, 20, 20)));
        canvas.ApplyTo(document.Pages[0]);
        canvas.ApplyTo(document.Pages[0], PdfCanvasApplyMode.Underlay);
        Assert.Equal(2, document.Pages[0].CanvasLayers.Count);
        Assert.True(document.Pages[0].ReadContent().Operations.Count(x => x.Operation == PdfOperator.DrawObject) >= 2);
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        using var loaded = Pdf.Open(output);
        Assert.Equal(before, Bytes(loaded.SourceDocument!.Resolve(document.Pages[0].Source!.Dictionary.Entries["Contents"].ReferenceId)));
        Assert.True(loaded.Pages[0].ReadContent().Operations.Count > 0);
        canvas.ApplyTo(document.Pages[0], PdfCanvasApplyMode.Replace);
        Assert.Empty(document.Pages[0].CanvasLayers);
        Assert.Single(document.Pages[0].ReadContent().Operations, x => x.Operation == PdfOperator.DrawObject);
    }

    [Fact]
    public void ApplyingACompletedCanvasIsAtomicOnCancellationOrBudgetFailure()
    {
        var geometry = SimpleGeometry();
        var canvas = PdfCanvas.Create(geometry, c => c.DrawPath(Rectangle(0, 0, 20, 20)));
        using var document = Pdf.Create(new PdfOptions(operationPolicy: new(limits: new(maximumObjects: 1)))).AddPage(geometry);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => canvas.ApplyTo(document.Pages[0])).Diagnostic.Code);
        Assert.Empty(document.Pages[0].GraphicsResources.Bindings);
        Assert.Empty(document.Pages[0].CanvasLayers);
        Assert.Null(document.Pages[0].Content);
        Assert.Throws<OperationCanceledException>(() => canvas.ApplyTo(document.Pages[0], cancellationToken: new CancellationToken(true)));
    }

    [Fact]
    public void BuilderLifetimeThreadOwnershipScopesAndLatchedFailuresAreEnforced()
    {
        PdfCanvas? retained = null;
        var geometry = SimpleGeometry();
        PdfCanvas.Create(geometry, c => retained = c);
        Assert.Throws<InvalidOperationException>(() => retained!.Translate(1, 2));
        Assert.Equal("Canvas.UnbalancedScope", Assert.Throws<PdfException>(() => PdfCanvas.Create(geometry, c => c.Save())).Diagnostic.Code);
        Assert.Equal("Canvas.ScopeOrder", Assert.Throws<PdfException>(() => PdfCanvas.Create(geometry, c =>
        {
            var first = c.Save();
            var second = c.Save();
            first.Dispose();
            second.Dispose();
        })).Diagnostic.Code);
        Assert.Equal("Canvas.ThreadAccess", Assert.Throws<PdfException>(() => PdfCanvas.Create(geometry, c =>
        {
            var thread = new Thread(() =>
            {
                try
                {
                    c.Translate(1, 2);
                }
                catch (PdfException)
                {
                }
            });
            thread.Start();
            Assert.True(thread.Join(5000));
        })).Diagnostic.Code);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfCanvas.Create(geometry, c =>
        {
            try
            {
                for (int i = 0; i < 100; i++)
                    c.DrawPath(Rectangle(0, 0, 1, 1));
            }
            catch (PdfException)
            {
            }
        }, policy: new(limits: new(maximumObjects: 4)))).Diagnostic.Code);
    }

    [Fact]
    public void CompletedCanvasesCanBeSharedAcrossIndependentJobsAndSnapshots()
    {
        var geometry = SimpleGeometry();
        var canvas = PdfCanvas.Create(geometry, c => c.DrawPath(Rectangle(10, 10, 20, 20)));
        Parallel.For(0, 8, _ =>
        {
            using var document = Pdf.Create().AddPage(geometry);
            canvas.ApplyTo(document.Pages[0]);
            var snapshot = document.CreateSnapshot();
            document.Pages[0].Content = PdfContentProgram.Create(Array.Empty<PdfContentOperation>());
            Assert.Single(snapshot.Pages[0].CanvasLayers);
            using var output = new MemoryStream();
            PolicyTests.Run(context => ManagedPdfProvider.Instance.Write(snapshot, output, context));
            Assert.True(output.Length > 0);
        });
    }

    [Fact]
    public void ThousandsOfPlacementsUsePrivateResourceBatchesAndRetainEveryLayer()
    {
        var image = Image();
        var geometry = SimpleGeometry();
        var canvas = PdfCanvas.Create(geometry, c =>
        {
            for (int i = 0; i < 4097; i++)
                c.DrawImage(image, new(i % 190, i % 90, 1, 1));
        });
        Assert.Equal(4097, canvas.Placements.Count);
        using var document = Pdf.Create().AddPage(geometry);
        canvas.ApplyTo(document.Pages[0]);
        using var output = new MemoryStream();
        document.Save(output);
        output.Position = 0;
        using var loaded = Pdf.Open(output);
        Assert.Single(loaded.Pages);
    }

    [Fact]
    public void NonfiniteGeometrySingularTransformsAndMismatchedPagesFailBeforeCommit()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfPoint(double.NaN, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfRect(0, 0, double.PositiveInfinity, 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfCanvas.Create(SimpleGeometry(), c => c.Rotate(double.NaN)));
        var singular = PdfCanvas.Create(SimpleGeometry(), c => { c.Scale(0, 1); c.DrawImage(Image(), new(0, 0, 20, 20)); });
        Assert.False(singular.Placements[0].HitTest(new(0, 10)));
        var canvas = PdfCanvas.Create(SimpleGeometry(), _ =>
        {
        });
        using var document = Pdf.Create().AddPage();
        Assert.Throws<ArgumentException>(() => canvas.ApplyTo(document.Pages[0]));
        Assert.Empty(document.Pages[0].GraphicsResources.Bindings);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void BothLanguageExamplesEmitRotatedCanvasPages(bool visualBasic)
    {
        using var output = new MemoryStream();
        if (visualBasic)
            ContractExamples.VisualBasic.ContractExample.WriteCanvasPdf(output);
        else
            ContractExamples.CSharp.ContractExample.WriteCanvasPdf(output);
        output.Position = 0;
        using var document = Pdf.Open(output);
        Assert.Equal(PdfPageRotation.Clockwise90, document.Pages[0].Rotation);
        Assert.Equal(-20, document.Pages[0].Geometry.Boxes.Media.X);
        Assert.NotEmpty(document.Pages[0].ReadContent().Operations);
    }

    internal static PdfPageGeometry SimpleGeometry() => new(new(new(0, 0, 200, 100)));
    internal static PdfPageGeometry Geometry(PdfPageRotation rotation) => new(new(new(-20, 10, 200, 100), new(-10, 20, 160, 80), new(-15, 15, 180, 90), new(0, 25, 140, 70), new(5, 30, 130, 60)), rotation, 2);
    internal static PdfPath Rectangle(double x, double y, double width, double height) => PdfPath.Create(new[] { PdfPathSegment.Rectangle(new(x, y, width, height)) });
    internal static PdfGraphicsResource Image()
    {
        using var input = new MemoryStream(new byte[] { 255, 0, 0, 0, 0, 255 });
        var dictionary = PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("XObject")), ("Subtype", PdfValue.Name("Image")), ("Width", PdfValue.Integer(2)), ("Height", PdfValue.Integer(1)), ("BitsPerComponent", PdfValue.Integer(8)), ("ColorSpace", PdfValue.Name("DeviceRGB")));
        return PdfGraphicsResource.Create(PdfGraphicsResourceCategory.XObject, PdfValues.Stream(dictionary, input), "1.2");
    }

    private static byte[] Bytes(PdfValue value)
    {
        using var input = value.OpenRawStream();
        using var output = new MemoryStream();
        input.CopyTo(output);
        return output.ToArray();
    }
}
