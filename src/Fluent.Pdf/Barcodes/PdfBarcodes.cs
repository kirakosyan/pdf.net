using System.Threading;

namespace Fluent.Pdf;
/// <summary>Original bounded symbol encoding, mandatory independent decoding and explicit page/region integration.</summary>
public static class PdfBarcodes
{
    /// <summary>Generates independently decoded EPC002 UTF8 SCT at mandatory levelM, with at most version13. Payment data is validated before generation and never submitted or executed.</summary>
    public static PdfBarcodeSymbol GeneratePayment(PdfSepaCreditTransfer payment, IPdfBarcodeDecoder? independentDecoder = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (payment == null)
            throw new ArgumentNullException(nameof(payment));
        return Generate(payment.Payload, independentDecoder, new(), policy, cancellationToken);
    }

    /// <summary>Generates exact UTF8 QR or printable Code128B, independently decodes its canonical pixels, then creates a detached vector drawing. No symbol returns without exact payload/symbology/ECC agreement.</summary>
    public static PdfBarcodeSymbol Generate(string text, IPdfBarcodeDecoder? independentDecoder = null, PdfBarcodeOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (text.Length == 0 || text.Length > 32768)
            throw new ArgumentException("A nonempty bounded payload is required.", nameof(text));
        options ??= new();
        PdfBarcodeSymbol? result = null;
        var report = PdfFont.Run("GenerateBarcode", "managed.barcodes", policy, cancellationToken, c =>
        {
            string identity = Require(independentDecoder, c);
            bool supported;
            try
            {
                supported = independentDecoder!.Supports(options.Symbology);
                c.Checkpoint();
            }
            catch
            {
                c.Checkpoint();
                throw Error(c, "DecoderFailed", "The configured independent decoder prerequisite check failed.");
            }

            if (!supported)
                throw Error(c, "SymbologyUnavailable", "The explicitly configured independent decoder does not support the requested symbology.");
            c.Consume(PdfResourceKind.Objects, 1);
            var encoded = options.Symbology == PdfBarcodeSymbology.QrCode ? PdfQrEncoder.Encode(text, options, c) : Code128(text, options, c);
            int quiet = options.QuietZone, width = encoded.Width + 2 * quiet, height = encoded.Height + 2 * quiet;
            c.Consume(PdfResourceKind.ExpandedBytes, width * (long)height);
            var modules = new bool[width * height];
            for (int y = 0; y < encoded.Height; y++)
            {
                c.Checkpoint();
                Array.Copy(encoded.Bits, y * encoded.Width, modules, (y + quiet) * width + quiet, encoded.Width);
            }

            var grid = new PdfBarcodeGrid(modules, width, height, encoded.Version, encoded.Mask);
            var pixels = Raster(grid, options.Symbology == PdfBarcodeSymbology.QrCode ? 4 : 2, c);
            var verified = DecodeCore(pixels, independentDecoder!, c);
            if (verified == null || verified.Symbology != options.Symbology || verified.Text != text || verified.ErrorCorrection != options.ErrorCorrection)
                throw Error(c, "IndependentDecode", "Independent decoding did not reproduce the exact complete requested payload, symbology and error correction.");
            var drawing = Drawing(grid, c);
            result = new(text, options, grid, drawing, pixels, identity);
        }, PdfCapability.Barcodes);
        result!.OperationReport = report;
        result.Drawing.OperationReport = report;
        result.Drawing.Content.OperationReport = report;
        result.VerificationRaster.OperationReport = report;
        return result;
    }

    /// <summary>Independently detects at most one supported symbol in an explicit captured raster. Points use its top-left pixel frame; decoded Unicode is never executed or treated as original byte encoding.</summary>
    public static PdfBarcodeDetection Detect(PdfOcrRaster raster, IPdfBarcodeDecoder? independentDecoder = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (raster == null)
            throw new ArgumentNullException(nameof(raster));
        PdfBarcodeObservation? result = null;
        var report = PdfFont.Run("DetectBarcode", "managed.barcodes", policy, cancellationToken, c =>
        {
            Require(independentDecoder, c);
            result = DecodeCore(raster, independentDecoder!, c);
        }, PdfCapability.Barcodes);
        return new(result, null, report);
    }

    /// <summary>Renders an explicitly selected page/region and detects one symbol under one shared context. Returned detector landmarks map to native PDF user coordinates, including crop, rotation and UserUnit. Use separate explicit regions for multiple symbols.</summary>
    public static PdfBarcodeDetection DetectPage(PdfDocumentSnapshot source, int pageIndex, PdfProviderRegistry providers, IPdfBarcodeDecoder? independentDecoder = null, PdfRenderOptions? render = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        if (pageIndex < 0 || pageIndex >= source.Pages.Count)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        render ??= new(96, 96, annotations: false);
        if (!render.BackgroundRgb.HasValue || render.DpiX != render.DpiY || render.DpiX != Math.Floor(render.DpiX) || render.DpiX < 72 || render.DpiX > 600)
            throw new ArgumentException("Barcode detection requires an explicit opaque isotropic integer DPI72–600 render profile.", nameof(render));
        _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
        PdfBarcodeObservation? result = null;
        var report = PdfFont.Run("DetectPageBarcode", "managed.barcodes", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            Require(independentDecoder, c);
            using var graph = new PdfGraphEdit(source, new(allowSignatureInvalidation: true), c);
            var captured = PdfRendering.RenderCore(graph, pageIndex, providers, render, c);
            var raster = new PdfOcrRaster(captured.Samples, captured.Request.Width, captured.Request.Height, captured.Request.Stride, PdfOcrPixelFormat.Bgra32, (int)render.DpiX);
            var observed = DecodeCore(raster, independentDecoder!, c);
            if (observed == null)
                return;
            c.Consume(PdfResourceKind.ExpandedBytes, observed.Points.Count * 32L + 512);
            var frame = new PdfOcrPageFrame(graph.Snapshot.Pages[pageIndex].Geometry, raster.Width, raster.Height, captured.Request.Region);
            result = new(observed.Symbology, observed.Text, observed.ErrorCorrection, observed.Points.Select(point => frame.PixelToUser.Transform(point)));
        }, PdfCapability.Barcodes);
        return new(result, pageIndex, report);
    }

    private static PdfBarcodeObservation? DecodeCore(PdfOcrRaster raster, IPdfBarcodeDecoder decoder, PdfOperationContext c)
    {
        c.Checkpoint();
        c.ConsumePixels(raster.Width, raster.Height);
        c.Consume(PdfResourceKind.InputBytes, raster.ByteLength);
        PdfBarcodeObservation? observed;
        try
        {
            observed = decoder.Decode(raster, c);
            c.Checkpoint();
        }
        catch
        {
            c.Checkpoint();
            throw Error(c, "DecoderFailed", "The explicitly configured independent barcode decoder failed.");
        }

        if (observed == null)
            return null;
        if (observed.Symbology == PdfBarcodeSymbology.Code128 && observed.ErrorCorrection != PdfBarcodeErrorCorrection.None || observed.Symbology == PdfBarcodeSymbology.QrCode && observed.ErrorCorrection == PdfBarcodeErrorCorrection.None)
            throw Error(c, "DecoderBinding", "The observed symbology and error correction are inconsistent.");
        c.Consume(PdfResourceKind.Objects, observed.Points.Count + 1L);
        c.Consume(PdfResourceKind.ExpandedBytes, observed.Text.Length * 4L + observed.Points.Count * 32L + 512);
        try
        {
            new System.Text.UTF8Encoding(false, true).GetByteCount(observed.Text);
        }
        catch (System.Text.EncoderFallbackException)
        {
            throw Error(c, "PayloadEncoding", "The independent decoder returned invalid Unicode scalars.");
        }

        foreach (var point in observed.Points)
            if (point.X < 0 || point.Y < 0 || point.X > raster.Width || point.Y > raster.Height)
                throw Error(c, "DetectorGeometry", "Independent detector landmarks exceed the source pixel frame.");
        return new(observed.Symbology, observed.Text, observed.ErrorCorrection, observed.Points);
    }

    private static string Require(IPdfBarcodeDecoder? decoder, PdfOperationContext c)
    {
        if (decoder == null)
            throw Error(c, "IndependentDecoderRequired", "An explicitly configured independently implemented barcode decoder is required.");
        PdfProviderDescriptor descriptor;
        try
        {
            descriptor = decoder.Descriptor;
            c.Checkpoint();
        }
        catch
        {
            c.Checkpoint();
            throw Error(c, "DecoderFailed", "The configured independent decoder identity check failed.");
        }

        if (descriptor == null || !descriptor.Capabilities.Contains(PdfCapability.Barcodes) || !Safe(descriptor.Id) || !Safe(descriptor.Version) || descriptor.Id == "managed.barcodes")
            throw Error(c, "DecoderIdentity", "A pinned nonsecret independently implemented barcode decoder identity is required.");
        string value = descriptor.Id + "/" + descriptor.Version;
        c.Report(new("Barcode.DecoderIdentity", "Explicit independent decoder: " + value + ".", PdfCapability.Barcodes, "managed.barcodes", PdfDiagnosticSeverity.Information));
        return value;
        bool Safe(string text) => text.Length <= 128 && text.All(ch => ch >= 'A' && ch <= 'Z' || ch >= 'a' && ch <= 'z' || ch >= '0' && ch <= '9' || "._-+".IndexOf(ch) >= 0);
    }

    private static PdfBarcodeGrid Code128(string text, PdfBarcodeOptions options, PdfOperationContext c)
    {
        if (text.Length > 256)
            throw Error(c, "Capacity", "The finite Code128B profile supports at most256 characters.");
        foreach (char value in text)
            if (value < 32 || value > 126)
                throw Error(c, "Code128Character", "Code128B accepts printable ASCII32–126 only; no character substitution occurs.");
        int width = (text.Length + 2) * 11 + 13;
        c.Consume(PdfResourceKind.ExpandedBytes, width * (long)options.BarHeight + text.Length * 16L + 256);
        var modules = new bool[width * options.BarHeight];
        int at = 0, checksum = 104;
        void Code(int code)
        {
            c.Checkpoint();
            foreach (char bit in PdfBarcodeTables.Code128[code])
            {
                if (bit == '1')
                    for (int y = 0; y < options.BarHeight; y++)
                        modules[y * width + at] = true;
                at++;
            }
        }

        Code(104);
        for (int n = 0; n < text.Length; n++)
        {
            int value = text[n] - 32;
            Code(value);
            checksum += value * (n + 1);
        }

        Code(checksum % 103);
        Code(106);
        return new(modules, width, options.BarHeight);
    }

    private static PdfOcrRaster Raster(PdfBarcodeGrid grid, int scale, PdfOperationContext c)
    {
        int width = grid.Width * scale, height = grid.Height * scale;
        c.ConsumePixels(width, height);
        var samples = new PdfByteBuilder(c);
        for (int y = 0; y < height; y++)
        {
            c.Checkpoint();
            for (int x = 0; x < width; x++)
                samples.Add(grid.Bits[y / scale * grid.Width + x / scale] ? (byte)0 : (byte)255);
        }

        return new(samples.Build(), width, height, width, PdfOcrPixelFormat.Gray8, scale * 72);
    }

    private static PdfDrawing Drawing(PdfBarcodeGrid grid, PdfOperationContext c)
    {
        IEnumerable<PdfContentOperation> Operations()
        {
            yield return new(PdfOperator.FillGray, PdfValue.Integer(1));
            yield return new(PdfOperator.Rectangle, PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(grid.Width), PdfValue.Integer(grid.Height));
            yield return new(PdfOperator.Fill);
            yield return new(PdfOperator.FillGray, PdfValue.Integer(0));
            for (int y = 0; y < grid.Height;)
            {
                c.Checkpoint();
                int rows = 1;
                while (y + rows < grid.Height)
                {
                    bool equal = true;
                    for (int x = 0; x < grid.Width; x++)
                        if (grid.Bits[y * grid.Width + x] != grid.Bits[(y + rows) * grid.Width + x])
                        {
                            equal = false;
                            break;
                        }

                    if (!equal)
                        break;
                    rows++;
                }

                for (int x = 0; x < grid.Width;)
                {
                    if (!grid.Bits[y * grid.Width + x])
                    {
                        x++;
                        continue;
                    }

                    int start = x++;
                    while (x < grid.Width && grid.Bits[y * grid.Width + x])
                        x++;
                    yield return new(PdfOperator.Rectangle, PdfValue.Integer(start), PdfValue.Integer(grid.Height - y - rows), PdfValue.Integer(x - start), PdfValue.Integer(rows));
                }

                y += rows;
            }

            yield return new(PdfOperator.Fill);
        }

        return PdfDrawing.CreateCore(Operations(), PdfResourceSet.Empty, c);
    }

    internal static PdfException Error(PdfOperationContext c, string code, string message)
    {
        var diagnostic = new PdfDiagnostic("Barcode." + code, message, PdfCapability.Barcodes, "managed.barcodes");
        c.Report(diagnostic);
        return new(diagnostic);
    }
}
