using System.Text;
using Fluent.Pdf.Workers;

namespace Fluent.Pdf.Ocr;
/// <summary>Explicit local Tesseract5 C-API adapter using one fresh hash-pinned isolated worker per recognition. No native/model asset or language is discovered or downloaded.</summary>
public sealed class IsolatedTesseractOcr : IPdfOcrProvider
{
    /// <summary>Gets the original bounded OCR transport ABI, separate from the actual engine version.</summary>
    public const string BridgeAbi = "fluent-pdf-ocr-1";
    /// <summary>Gets the exact original IPC bridge version, separate from the caller's checked Tesseract version.</summary>
    public const string BridgeVersion = "1.0";
    private readonly NativeWorkerOptions _worker;
    private readonly PdfNativeAsset _model;
    private readonly string _version;
    private static readonly string[] Symbols =
    {
        "TessVersion",
        "TessBaseAPICreate",
        "TessBaseAPIDelete",
        "TessBaseAPIInit3",
        "TessBaseAPISetImage",
        "TessBaseAPISetSourceResolution",
        "TessBaseAPISetPageSegMode",
        "TessBaseAPIRecognize",
        "TessBaseAPIGetUTF8Text",
        "TessBaseAPIGetTsvText",
        "TessBaseAPIMeanTextConf",
        "TessDeleteText",
        "TessMonitorCreate",
        "TessMonitorDelete",
        "TessMonitorSetCancelFunc",
        "TessMonitorSetDeadlineMSecs"
    };
    /// <summary>Captures the exact bridge/deployment and expected actual Tesseract5 version. The manifest must explicitly pin its canonical engine binary and eng.traineddata; runtime identity, hashes, dependencies and sandbox remain mandatory.</summary>
    public IsolatedTesseractOcr(NativeWorkerOptions worker, string exactEngineVersion)
    {
        _worker = worker ?? throw new ArgumentNullException(nameof(worker));
        if (worker.Deployment.Abi != BridgeAbi || worker.Deployment.EngineVersion != BridgeVersion)
            throw new ArgumentException("The exact OCR bridge ABI/version is required.", nameof(worker));
        if (exactEngineVersion == null || exactEngineVersion.Length > 128 || !exactEngineVersion.StartsWith("5.", StringComparison.Ordinal) || exactEngineVersion.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '.' || c == '-' || c == '_' || c == '+')))
            throw new ArgumentException("An exact explicitly provisioned Tesseract5 version is required.", nameof(exactEngineVersion));
        _version = exactEngineVersion;
        string rid = worker.Deployment.RuntimeIdentifier;
        string engine = rid.StartsWith("win-", StringComparison.Ordinal) ? "tesseract.dll" : rid.StartsWith("osx-", StringComparison.Ordinal) ? "libtesseract.dylib" : rid.StartsWith("linux-", StringComparison.Ordinal) ? "libtesseract.so" : throw new ArgumentException("An explicit supported worker RID is required.", nameof(worker));
        if (!worker.Deployment.Assets.Any(a => a.FileName == engine && a.IsNativeBinary))
            throw new ArgumentException("The explicit canonical Tesseract binary must be pinned in the worker inventory.", nameof(worker));
        _model = worker.Deployment.Assets.SingleOrDefault(a => a.FileName == "eng.traineddata" && !a.IsNativeBinary) ?? throw new ArgumentException("The explicit English model must be pinned as eng.traineddata in the worker inventory.", nameof(worker));
        Descriptor = new("isolated.tesseract", exactEngineVersion, PdfCapability.RecognizeText);
    }

    /// <summary>Gets the exact requested native engine version and recognition service; capability declaration is not production qualification.</summary>
    public PdfProviderDescriptor Descriptor { get; }

    /// <summary>Recognizes through mandatory isolated execution with exact native-version/model binding, finite pixels/text/TSV and native deadline plus parent supervision.</summary>
    public PdfOcrResult Recognize(PdfOcrRaster raster, PdfOcrOptions options, PdfOperationContext context)
    {
        if (raster == null)
            throw new ArgumentNullException(nameof(raster));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        if (options.Language != "eng")
            throw Error("LanguageUnsupported", "This explicit source profile requires the pinned English model; no fallback is performed.");
        int versionLength = _version.Length, languageLength = options.Language.Length;
        long requestLength = 64L + versionLength + languageLength + raster.ByteLength;
        long utf8Limit = (long)options.MaximumCharacters * 4, tsvLimit = Math.Min(16777216L, (long)options.MaximumCharacters * 4 + options.MaximumWords * 128L + 4096);
        long responseLimit = 32L + versionLength + utf8Limit + tsvLimit;
        if (requestLength > _worker.MaximumInputBytes || requestLength > int.MaxValue)
            throw Error("RasterLimit", "The explicit raster exceeds the configured OCR worker input ceiling.");
        if (responseLimit > _worker.MaximumOutputBytes || responseLimit > int.MaxValue)
            throw Error("OutputLimit", "The requested Unicode/word ceilings exceed the configured OCR worker response envelope.");
        context.Consume(PdfResourceKind.ExpandedBytes, requestLength);
        var request = new byte[(int)requestLength];
        request[0] = 70;
        request[1] = 80;
        request[2] = 79;
        request[3] = 49;
        Put(request, 4, raster.Width);
        Put(request, 8, raster.Height);
        Put(request, 12, raster.Stride);
        Put(request, 16, raster.PixelFormat == PdfOcrPixelFormat.Gray8 ? 1 : raster.PixelFormat == PdfOcrPixelFormat.Rgb24 ? 3 : 4);
        Put(request, 20, raster.Dpi);
        Put(request, 24, options.Segmentation switch
        {
            PdfOcrSegmentation.Automatic => 3,
            PdfOcrSegmentation.SingleBlock => 6,
            PdfOcrSegmentation.SingleLine => 7,
            PdfOcrSegmentation.SparseText => 11,
            _ => throw new ArgumentOutOfRangeException(nameof(options))});
        Put(request, 28, options.MaximumCharacters);
        Put(request, 32, options.MaximumWords);
        Put(request, 36, (int)tsvLimit);
        Put(request, 40, versionLength);
        Put(request, 44, languageLength);
        Put(request, 48, (int)Math.Min(86400000, Math.Ceiling(_worker.MaximumElapsedTime.TotalMilliseconds)));
        Put(request, 52, (int)utf8Limit);
        for (int i = 0; i < versionLength; i++)
            request[64 + i] = (byte)_version[i];
        for (int i = 0; i < languageLength; i++)
            request[64 + versionLength + i] = (byte)options.Language[i];
        byte[] reply;
        try
        {
            using (var input = raster.OpenPixels(context))
            {
                int offset = 64 + versionLength + languageLength;
                while (offset < request.Length)
                {
                    context.Checkpoint();
                    int count = input.Read(request, offset, Math.Min(65536, request.Length - offset));
                    if (count == 0)
                        throw Error("RasterLength", "The captured raster ended unexpectedly.");
                    offset += count;
                }
            }

            if (raster.PixelFormat == PdfOcrPixelFormat.Bgra32)
                for (int y = 0; y < raster.Height; y++)
                {
                    context.Checkpoint();
                    int row = 64 + versionLength + languageLength + y * raster.Stride;
                    for (int x = 0; x < raster.Width; x++)
                        if (request[row + x * 4 + 3] != 255)
                            throw Error("RasterAlpha", "The local OCR source profile requires explicitly opaque BGRA pixels.");
                }

            reply = new NativeWorkerHost().Execute(_worker, request, context, (int)responseLimit);
        }
        finally
        {
            Array.Clear(request, 0, request.Length);
        }

        try
        {
            if (reply.Length < 32 || reply[0] != 70 || reply[1] != 80 || reply[2] != 79 || reply[3] != 79)
                throw Error("NativeResponse", "The OCR worker returned an invalid response envelope.");
            int status = Get(reply, 4), detail = Get(reply, 8);
            if (status != 0)
            {
                if (status == 2 && detail >= 0 && detail < Symbols.Length)
                    throw Error("MissingSymbol", "The explicit Tesseract binary lacks required C API export: " + Symbols[detail] + ".");
                throw Error(status switch
                {
                    1 => "NativeLibrary",
                    3 => "NativeVersion",
                    4 => "ModelInitialization",
                    5 => "NativeAllocation",
                    6 => "Recognition",
                    7 => "NativeTextLimit",
                    8 => "NativeDeadline",
                    _ => "NativeRejected"
                }, "The isolated OCR engine rejected its explicit native/model configuration, recognition or finite result extent.");
            }

            int confidence = Get(reply, 12), textLength = Get(reply, 16), tsvLength = Get(reply, 20), actualVersionLength = Get(reply, 24);
            if (Get(reply, 28) != 0 || confidence < 0 || confidence > 100 || textLength < 0 || textLength > utf8Limit || tsvLength < 0 || tsvLength > tsvLimit || actualVersionLength < 1 || actualVersionLength > 128 || 32L + actualVersionLength + textLength + tsvLength != reply.Length)
                throw Error("NativeResponse", "The OCR response lengths, confidence or reserved fields are invalid.");
            context.Consume(PdfResourceKind.ExpandedBytes, (actualVersionLength + (long)textLength + tsvLength) * 2 + 128);
            var utf8 = new UTF8Encoding(false, true);
            string actual, text, tsv;
            try
            {
                actual = utf8.GetString(reply, 32, actualVersionLength);
                text = utf8.GetString(reply, 32 + actualVersionLength, textLength);
                tsv = utf8.GetString(reply, 32 + actualVersionLength + textLength, tsvLength);
            }
            catch (DecoderFallbackException)
            {
                throw Error("NativeUtf8", "The OCR engine returned malformed UTF8.");
            }

            if (actual != _version)
                throw Error("NativeVersion", "The loaded Tesseract version differs from the explicitly pinned expected version.");
            var words = OcrTsv.Read(tsv, raster, options, text.Length, context);
            context.Consume(PdfResourceKind.ExpandedBytes, words.Count * 16L + 256);
            context.Report(new("Ocr.RecognitionProvenance", "Explicit local machine recognition supplied Unicode, geometry and confidence; linguistic accuracy and semantic order require independent review.", severity: PdfDiagnosticSeverity.Information));
            return new(text, words.ToArray(), options.Language, actual, _model.Sha256, confidence);
        }
        finally
        {
            Array.Clear(reply, 0, reply.Length);
        }

        PdfException Error(string code, string message) => PdfWriterCore.Error(context, "Ocr." + code, message);
    }

    private static int Get(byte[] bytes, int at) => unchecked((int)((uint)bytes[at] | (uint)bytes[at + 1] << 8 | (uint)bytes[at + 2] << 16 | (uint)bytes[at + 3] << 24));
    private static void Put(byte[] bytes, int at, int value)
    {
        for (int i = 0; i < 4; i++)
            bytes[at + i] = (byte)((uint)value >> (i * 8));
    }
}
