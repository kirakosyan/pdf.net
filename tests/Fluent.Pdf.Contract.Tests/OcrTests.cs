using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class OcrTests
{
    internal static PdfOcrRaster Raster(byte first = 0, int width = 32, int height = 16)
    {
        var bytes = new byte[width * height];
        bytes[0] = first;
        using var input = new MemoryStream(bytes);
        return PdfOcrRaster.Read(input, width, height, width, PdfOcrPixelFormat.Gray8);
    }

    private static PdfOcrResult Result(string language = "eng", PdfRect? box = null) => new("AB\n", new[] { new PdfOcrWord("AB", box ?? new(1, 1, 2, 2), 95, 1, 1, 1, 1) }, language, "original-managed-fixture", "original-model", 95);
    private static string Error(Action action) => Assert.ThrowsAny<PdfException>(action).Diagnostic.Code;
    private sealed class Provider : IPdfOcrProvider
    {
        public PdfProviderDescriptor Descriptor { get; } = new("original.ocr-contract", "1", PdfCapability.RecognizeText);

        public Func<PdfOcrRaster, PdfOcrOptions, PdfOperationContext, PdfOcrResult>? Action;
        public PdfOcrResult Recognize(PdfOcrRaster raster, PdfOcrOptions options, PdfOperationContext c) => Action == null ? Result(options.Language) : Action(raster, options, c);
    }

    [Fact]
    public void ExactRasterCaptureUsesCurrentPositionAndKeepsBorrowedInputOpen()
    {
        using var input = new MemoryStream(new byte[] { 99, 1, 2, 3, 4 });
        input.Position = 1;
        var raster = PdfOcrRaster.Read(input, 2, 2, 2, PdfOcrPixelFormat.Gray8, maximumBytes: 4);
        Assert.True(input.CanRead);
        Assert.Equal(5, input.Position);
        Assert.Equal(4, raster.ByteLength);
        Assert.NotNull(raster.OperationReport);
        var provider = new Provider();
        PdfOperationContext? seen = null;
        Stream? retained = null;
        provider.Action = (image, options, c) =>
        {
            seen = c;
            using var pixels = image.OpenPixels(c);
            retained = pixels;
            Assert.Equal(new byte[] { 1, 2, 3, 4 }, Read(pixels));
            return Result(box: new(0, 0, 1, 1));
        };
        var result = PdfOcr.Recognize(raster, new("eng"), new(provider));
        Assert.NotNull(result.OperationReport);
        Assert.Equal("RecognizeOcr", seen!.Operation);
        Assert.Equal(PdfCapability.RecognizeText, seen.Capability);
        Assert.Throws<ObjectDisposedException>(() => retained!.ReadByte());
        Assert.Equal("AB\n", result.Text);
    }

    private static byte[] Read(Stream source)
    {
        using var target = new MemoryStream();
        source.CopyTo(target);
        return target.ToArray();
    }

    [Theory]
    [InlineData(3)]
    [InlineData(5)]
    public void TruncatedAndTrailingRastersHaveTypedLengthFailures(int length)
    {
        using var input = new MemoryStream(new byte[length]);
        Assert.Equal("Ocr.RasterLength", Error(() => PdfOcrRaster.Read(input, 2, 2, 2, PdfOcrPixelFormat.Gray8)));
        Assert.True(input.CanRead);
        Assert.Equal(Math.Min(length, 5), input.Position);
    }

    [Fact]
    public void ExplicitGeometryAndCancellationAreCheckedBeforeCapturingPixels()
    {
        using var input = new MemoryStream(new byte[4]);
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfOcrRaster.Read(input, 2, 2, 1, PdfOcrPixelFormat.Gray8));
        Assert.Throws<ArgumentOutOfRangeException>(() => PdfOcrRaster.Read(input, 2, 2, 2, PdfOcrPixelFormat.Gray8, maximumBytes: 3));
        using var cancel = new CancellationTokenSource();
        cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfOcrRaster.Read(input, 2, 2, 2, PdfOcrPixelFormat.Gray8, cancellationToken: cancel.Token));
        Assert.Equal(0, input.Position);
        Assert.StartsWith("Limit.", Error(() => PdfOcrRaster.Read(input, 2, 2, 2, PdfOcrPixelFormat.Gray8, policy: new(limits: new(maximumPixels: 3)))));
    }

    [Theory]
    [InlineData(4, false)]
    [InlineData(5, true)]
    [InlineData(6, true)]
    public void TotalRecognizedCharacterBoundaryIncludesRawAndWordUnicode(int maximum, bool succeeds)
    {
        var image = Raster();
        var options = new PdfOcrOptions("eng", maximumCharacters: maximum);
        if (succeeds)
            Assert.Equal("AB\n", PdfOcr.Recognize(image, options, new(new Provider())).Text);
        else
            Assert.Equal("Ocr.CharacterLimit", Error(() => PdfOcr.Recognize(image, options, new(new Provider()))));
    }

    [Fact]
    public void MissingProviderSilentLanguageFallbackAndBadGeometryCannotReturnSuccess()
    {
        var image = Raster();
        Assert.Throws<PdfProviderUnavailableException>(() => PdfOcr.Recognize(image, new("eng"), PdfProviderRegistry.Empty));
        var language = new Provider
        {
            Action = (_, _, _) => Result()
        };
        Assert.Equal("Ocr.ProviderBinding", Error(() => PdfOcr.Recognize(image, new("deu"), new(language))));
        var geometry = new Provider
        {
            Action = (_, _, _) => Result(box: new(31, 0, 2, 1))
        };
        Assert.Equal("Ocr.WordGeometry", Error(() => PdfOcr.Recognize(image, new("eng"), new(geometry))));
        Assert.Throws<ArgumentException>(() => new PdfOcrWord("\ud800", new(0, 0, 1, 1), null, 0, 0, 0, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfOcrWord("A", new(0, 0, 1, 1), double.NaN, 0, 0, 0, 0));
    }

    [Fact]
    public void AProviderCannotHideLatchedBudgetOrCancellationFailures()
    {
        var image = Raster();
        var provider = new Provider
        {
            Action = (_, _, c) =>
            {
                try
                {
                    c.Consume(PdfResourceKind.Objects, long.MaxValue);
                }
                catch (PdfException)
                {
                }

                return Result();
            }
        };
        Assert.StartsWith("Limit.", Error(() => PdfOcr.Recognize(image, new("eng"), new(provider))));
        using var cancel = new CancellationTokenSource();
        provider.Action = (_, _, c) =>
        {
            cancel.Cancel();
            try
            {
                c.Checkpoint();
            }
            catch (OperationCanceledException)
            {
            }

            return Result();
        };
        Assert.ThrowsAny<OperationCanceledException>(() => PdfOcr.Recognize(image, new("eng"), new(provider), cancellationToken: cancel.Token));
    }

    [Fact]
    public void ResultArraysRemainImmutableAndCallerJobsUseIndependentContexts()
    {
        var words = new[]
        {
            new PdfOcrWord("AB", new(1, 1, 2, 2), null, 1, 1, 1, 1)
        };
        var captured = new PdfOcrResult("AB", words, "eng", "original", "original");
        words[0] = new("CHANGED", new(0, 0, 1, 1), null, 0, 0, 0, 0);
        Assert.Equal("AB", captured.Words[0].Text);
        var contexts = new List<PdfOperationContext>();
        var provider = new Provider
        {
            Action = (_, _, c) =>
            {
                contexts.Add(c);
                return captured;
            }
        };
        var image = Raster();
        for (int i = 0; i < 3; i++)
            Assert.Equal("AB", PdfOcr.Recognize(image, new("eng"), new(provider)).Text);
        Assert.Equal(3, contexts.Distinct().Count());
    }

    [Fact]
    public void BothPublicLanguageConsumersRecognizeTheExplicitRaster()
    {
        var image = Raster();
        var providers = new PdfProviderRegistry(new Provider());
        Assert.Equal("AB\n", ContractExamples.CSharp.ContractExample.RecognizeOcr(image, new("eng"), providers).Text);
        Assert.Equal("AB\n", ContractExamples.VisualBasic.ContractExample.RecognizeOcr(image, new("eng"), providers).Text);
    }
}
