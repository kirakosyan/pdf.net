using System.IO;
using System.Text;
using System.Threading;
using Fluent.Pdf.Ocr;
using Xunit;
using Xunit.Abstractions;

namespace Fluent.Pdf.Contract.Tests;
public sealed class BarcodeTests
{
    private readonly ITestOutputHelper _output;
    public BarcodeTests(ITestOutputHelper output) => _output = output;
    private static readonly ZxingBarcodeDecoder Decoder = new();
    private static string FixtureRoot => Path.Combine(AppContext.BaseDirectory, "barcode-fixtures");

    private static void SavePixels(string name, PdfBarcodeSymbol symbol)
    {
        Directory.CreateDirectory(FixtureRoot);
        using var file = File.Create(Path.Combine(FixtureRoot, name + ".pgm"));
        var header = Encoding.ASCII.GetBytes("P5\n" + symbol.VerificationRaster.Width + " " + symbol.VerificationRaster.Height + "\n255\n");
        file.Write(header, 0, header.Length);
        using var pixels = symbol.OpenVerificationPixels();
        pixels.CopyTo(file);
        File.WriteAllText(Path.Combine(FixtureRoot, name + ".payload"), Convert.ToBase64String(Encoding.UTF8.GetBytes(symbol.Text)));
        File.WriteAllText(Path.Combine(FixtureRoot, name + ".symbol"), symbol.Options.Symbology + "\t" + symbol.Options.ErrorCorrection + "\t" + symbol.QrVersion + "\t" + symbol.Mask);
    }

    [Fact]
    public void EveryQrVersionAndCorrectionLevelIndependentlyDecodesOriginalByteModeSymbols()
    {
        foreach (var correction in new[]
        {
            PdfBarcodeErrorCorrection.Low,
            PdfBarcodeErrorCorrection.Medium,
            PdfBarcodeErrorCorrection.Quartile,
            PdfBarcodeErrorCorrection.High
        }

        )
            for (int version = 1; version <= 40; version++)
            {
                var symbol = PdfBarcodes.Generate("A", Decoder, new(errorCorrection: correction, qrVersion: version));
                Assert.Equal(version, symbol.QrVersion);
                Assert.Equal(version * 4 + 25, symbol.Width);
                Assert.Equal(symbol.Width, symbol.Height);
                Assert.InRange(symbol.Mask, 0, 7);
                Assert.True(symbol.OperationReport.Succeeded);
                SavePixels("qr-v" + version.ToString("00") + "-" + correction, symbol);
                _output.WriteLine("Independently decoded QR version{0} level{1} mask{2}", version, correction, symbol.Mask);
            }
    }

    [Theory]
    [InlineData("Original ASCII payload")]
    [InlineData("Grüße Ελληνικά Україна العربية 日本語 😀")]
    [InlineData("line1\nline2\u0000original")]
    public void Utf8EciRoundTripsWithoutPayloadOrEncodingSubstitution(string text)
    {
        var symbol = PdfBarcodes.Generate(text, Decoder);
        var observed = PdfBarcodes.Detect(symbol.VerificationRaster, Decoder).Observation!;
        Assert.Equal(text, observed.Text);
        Assert.Equal(PdfBarcodeSymbology.QrCode, observed.Symbology);
        Assert.Equal(PdfBarcodeErrorCorrection.Medium, observed.ErrorCorrection);
        Assert.True(observed.Points.Count >= 3);
        Assert.Equal("managed.zxing-barcodes/0.16.11", symbol.VerifierIdentity);
    }

    [Theory]
    [InlineData(1, 16, 13, 10, 6)]
    [InlineData(2, 31, 25, 19, 13)]
    [InlineData(5, 105, 83, 59, 43)]
    [InlineData(7, 153, 121, 85, 63)]
    [InlineData(10, 270, 212, 150, 118)]
    [InlineData(20, 857, 665, 481, 381)]
    [InlineData(27, 1464, 1124, 804, 624)]
    [InlineData(32, 1951, 1537, 1111, 841)]
    [InlineData(40, 2952, 2330, 1662, 1272)]
    public void NearCapacityPayloadsExerciseEveryBlockAcrossRepresentativeVersions(int version, int low, int medium, int quartile, int high)
    {
        var corrections = new[]
        {
            PdfBarcodeErrorCorrection.Low,
            PdfBarcodeErrorCorrection.Medium,
            PdfBarcodeErrorCorrection.Quartile,
            PdfBarcodeErrorCorrection.High
        };
        var capacities = new[]
        {
            low,
            medium,
            quartile,
            high
        };
        for (int level = 0; level < corrections.Length; level++)
        {
            string text = new(Enumerable.Range(0, capacities[level]).Select(n => (char)(32 + (n * 73 + version) % 95)).ToArray());
            var symbol = PdfBarcodes.Generate(text, Decoder, new(errorCorrection: corrections[level], qrVersion: version));
            Assert.Equal(text, symbol.Text);
            SavePixels("capacity-v" + version.ToString("00") + "-" + corrections[level], symbol);
            Assert.Equal("Barcode.Capacity", Assert.Throws<PdfException>(() => PdfBarcodes.Generate(text + "X", Decoder, new(errorCorrection: corrections[level], qrVersion: version))).Diagnostic.Code);
        }
    }

    [Theory]
    [InlineData("Original-128 / $ + %")]
    [InlineData("012345678901234567890123456789")]
    [InlineData(" ")]
    public void Code128BUsesExactPrintableAsciiAndMandatoryChecksum(string text)
    {
        var symbol = PdfBarcodes.Generate(text, Decoder, new(PdfBarcodeSymbology.Code128, PdfBarcodeErrorCorrection.None));
        Assert.Equal(text, PdfBarcodes.Detect(symbol.VerificationRaster, Decoder).Observation!.Text);
        Assert.Equal(0, symbol.QrVersion);
        Assert.Equal(-1, symbol.Mask);
        Assert.Equal(PdfBarcodeErrorCorrection.None, symbol.Options.ErrorCorrection);
    }

    private sealed class Verdict : IPdfBarcodeDecoder
    {
        internal string Text = "wrong";
        internal bool Available = true;
        public PdfProviderDescriptor Descriptor { get; } = new("contract.barcode-verdict", "1", PdfCapability.Barcodes);

        public bool Supports(PdfBarcodeSymbology symbology) => Available;
        public PdfBarcodeObservation Decode(PdfOcrRaster raster, PdfOperationContext context) => new(PdfBarcodeSymbology.QrCode, Text, PdfBarcodeErrorCorrection.Medium, new[] { new PdfPoint(1, 1) });
    }

    [Fact]
    public void GenerationFailsWithoutIndependentMatchingDecodeAndFiniteCapacity()
    {
        Assert.Equal("Barcode.IndependentDecoderRequired", Assert.Throws<PdfException>(() => PdfBarcodes.Generate("A")).Diagnostic.Code);
        Assert.Equal("Barcode.IndependentDecode", Assert.Throws<PdfException>(() => PdfBarcodes.Generate("A", new Verdict())).Diagnostic.Code);
        Assert.Equal("Barcode.SymbologyUnavailable", Assert.Throws<PdfException>(() => PdfBarcodes.Generate("A", new Verdict { Available = false })).Diagnostic.Code);
        Assert.Equal("Barcode.Capacity", Assert.Throws<PdfException>(() => PdfBarcodes.Generate(new string ('A', 30), Decoder, new(qrVersion: 1))).Diagnostic.Code);
        Assert.Equal("Barcode.PayloadEncoding", Assert.Throws<PdfException>(() => PdfBarcodes.Generate("\ud800", Decoder)).Diagnostic.Code);
        Assert.Equal("Barcode.Code128Character", Assert.Throws<PdfException>(() => PdfBarcodes.Generate("é", Decoder, new(PdfBarcodeSymbology.Code128, PdfBarcodeErrorCorrection.None))).Diagnostic.Code);
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfBarcodes.Generate("A", Decoder, policy: new(limits: new(maximumPixels: 10)))).Diagnostic.Code);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfBarcodes.Generate("A", Decoder, policy: new(limits: new(maximumExpandedBytes: 100)))).Diagnostic.Code);
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfBarcodes.Generate("A", Decoder, cancellationToken: canceled.Token));
    }

    private static PdfOcrRaster Raster(byte[] samples, int width, int height, PdfOcrPixelFormat format = PdfOcrPixelFormat.Gray8)
    {
        using var input = new MemoryStream(samples);
        return PdfOcrRaster.Read(input, width, height, width * (format == PdfOcrPixelFormat.Bgra32 ? 4 : 1), format);
    }

    [Fact]
    public void RotatedAndDamagedSymbolsDecodeAndUnsupportedPixelsReject()
    {
        const string text = "Original damaged Ελληνικά 日本語";
        var symbol = PdfBarcodes.Generate(text, Decoder, new(errorCorrection: PdfBarcodeErrorCorrection.High, qrVersion: 6));
        int width = symbol.VerificationRaster.Width, height = symbol.VerificationRaster.Height;
        var samples = SignatureTests.Bytes(symbol.OpenVerificationPixels());
        for (int turn = 0; turn < 4; turn++)
        {
            Assert.Equal(text, PdfBarcodes.Detect(Raster(samples, width, height), Decoder).Observation!.Text);
            var next = new byte[samples.Length];
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    next[x * height + height - 1 - y] = samples[y * width + x];
            samples = next;
            (width, height) = (height, width);
        }

        for (int module = 15; module < 18; module++)
            for (int y = (module + 4) * 4; y < (module + 5) * 4; y++)
                for (int x = (module + 4) * 4; x < (module + 5) * 4; x++)
                    samples[y * width + x] = (byte)(255 - samples[y * width + x]);
        Assert.Equal(text, PdfBarcodes.Detect(Raster(samples, width, height), Decoder).Observation!.Text);
        Assert.Null(PdfBarcodes.Detect(Raster(Enumerable.Repeat((byte)255, 10000).ToArray(), 100, 100), Decoder).Observation);
        var transparent = new byte[100 * 100 * 4];
        Assert.Equal("Barcode.RasterAlpha", Assert.Throws<PdfException>(() => PdfBarcodes.Detect(Raster(transparent, 100, 100, PdfOcrPixelFormat.Bgra32), Decoder)).Diagnostic.Code);
        Assert.Equal("Barcode.DecoderExtent", Assert.Throws<PdfException>(() => PdfBarcodes.Generate("A", new ZxingBarcodeDecoder(10))).Diagnostic.Code);
    }

    [Fact]
    public void PaymentSchemaAndInvoiceAmountAreValidatedBeforeIndependentGeneration()
    {
        var invoice = new PdfInvoiceData(PdfInvoiceProfile.FacturX107Minimum, "Original-42", "Original Seller", "Original Buyer", new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero), "EUR", MidpointRounding.AwayFromZero, new[] { new PdfInvoiceLine("Original line", 2, 12.50m, 20) });
        var payment = PdfSepaCreditTransfer.FromInvoice(invoice, "DE89370400440532013000", "COBADEFFXXX");
        Assert.Equal(30m, payment.Amount);
        Assert.Equal(invoice.Identifier, payment.Remittance);
        Assert.Equal(payment.Payload, PdfSepaCreditTransfer.Parse(payment.Payload).Payload);
        var symbol = PdfBarcodes.GeneratePayment(payment, Decoder);
        Assert.InRange(symbol.QrVersion, 1, 13);
        Assert.Equal(PdfBarcodeErrorCorrection.Medium, symbol.Options.ErrorCorrection);
        Assert.Equal(payment.Payload, PdfBarcodes.Detect(symbol.VerificationRaster, Decoder).Observation!.Text);
        var structured = new PdfSepaCreditTransfer("Original Creditor", payment.Iban, payment.Bic, 123.45m, "RF18539007547034");
        Assert.Equal(structured.Payload, PdfSepaCreditTransfer.Parse(structured.Payload).Payload);
        Assert.Throws<ArgumentException>(() => new PdfSepaCreditTransfer("Original\nInjected", payment.Iban, payment.Bic, 1m));
        Assert.Throws<ArgumentException>(() => new PdfSepaCreditTransfer("Original", "DE89370400440532013001", payment.Bic, 1m));
        Assert.Throws<ArgumentException>(() => new PdfSepaCreditTransfer("Original", payment.Iban, payment.Bic, 1m, "RF18539007547035"));
        Assert.Throws<ArgumentException>(() => new PdfSepaCreditTransfer("Original", payment.Iban, payment.Bic, 1m, structured.CreditorReference, "both"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfSepaCreditTransfer("Original", payment.Iban, payment.Bic, 1.001m));
        Assert.Throws<ArgumentException>(() => PdfSepaCreditTransfer.Parse(payment.Payload.Replace("EUR30.00", "EUR30.0")));
        Assert.Throws<ArgumentException>(() => PdfSepaCreditTransfer.Parse(payment.Payload.Replace("BCD\n002", "BCD\n001")));
    }

    private sealed class FrameRenderer : IPdfPageRenderer
    {
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.barcode-frame", "1", PdfCapability.RenderPages);

        public void Render(PdfRasterRequest request, Stream bgra, PdfOperationContext context)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, request.Stride);
            var row = Enumerable.Repeat((byte)255, request.Stride).ToArray();
            for (int y = 0; y < request.Height; y++)
            {
                context.Checkpoint();
                bgra.Write(row, 0, row.Length);
            }
        }
    }

    private sealed class FrameDecoder : IPdfBarcodeDecoder
    {
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.barcode-frame-decoder", "1", PdfCapability.Barcodes);

        public bool Supports(PdfBarcodeSymbology symbology) => true;
        public PdfBarcodeObservation Decode(PdfOcrRaster raster, PdfOperationContext context) => new(PdfBarcodeSymbology.QrCode, "Original frame fixture", PdfBarcodeErrorCorrection.Medium, new[] { new PdfPoint(10, 20) });
    }

    [Fact]
    public void PageRegionLandmarksUseNativeCropRotationAndUserUnitCoordinates()
    {
        using var document = Pdf.Create();
        var geometry = new PdfPageGeometry(new(new PdfRect(10, 20, 50, 60), new PdfRect(15, 25, 29, 29)), PdfPageRotation.Clockwise90, 2);
        document.Pages.Add(geometry);
        var result = PdfBarcodes.DetectPage(document.CreateSnapshot(), 0, new(new FrameRenderer()), new FrameDecoder(), new(144, 144, region: new PdfRect(4, 6, 40, 40), annotations: false));
        // Raster(10,20) in an80x80 requested region maps to display(9,16), hence native(x=15+16/2,y=25+9/2).
        Assert.Equal(0, result.PageIndex);
        Assert.Equal(23, result.Observation!.Points[0].X);
        Assert.Equal(29.5, result.Observation.Points[0].Y);
        Assert.True(result.OperationReport.Succeeded);
    }

    private static void SavePdf(string name, PdfBarcodeSymbol symbol)
    {
        SavePixels(name, symbol);
        using var document = Pdf.Create();
        var geometry = new PdfPageGeometry(new(new PdfRect(0, 0, symbol.Width * 2, symbol.Height * 2)));
        var page = document.Pages.Add(geometry);
        var canvas = PdfCanvas.Create(geometry, draw => draw.DrawDrawing(symbol.Drawing, new(0, 0, symbol.Width, symbol.Height), new(0, 0, symbol.Width * 2, symbol.Height * 2)));
        canvas.ApplyTo(page);
        document.SaveFile(Path.Combine(FixtureRoot, name + ".pdf"));
    }

    [Fact]
    public void CompiledCSharpAndVisualBasicPlaceVerifiedVectorSymbolsIntoActualPdf()
    {
        const string text = "Original Ελληνικά 日本語 😀";
        var cs = ContractExamples.CSharp.ContractExample.GenerateBarcode(text, Decoder, new());
        var vb = ContractExamples.VisualBasic.ContractExample.GenerateBarcode(text, Decoder, new());
        Assert.Equal(cs.Text, vb.Text);
        Assert.Equal(cs.Mask, vb.Mask);
        SavePdf("unicode-csharp", cs);
        SavePdf("unicode-vb", vb);
        SavePdf("code128", PdfBarcodes.Generate("Original-Invoice-42", Decoder, new(PdfBarcodeSymbology.Code128, PdfBarcodeErrorCorrection.None)));
        SavePdf("payment", PdfBarcodes.GeneratePayment(new("Original Seller", "DE89370400440532013000", "COBADEFFXXX", 123.45m, remittance: "Original invoice42"), Decoder));
    }
}
