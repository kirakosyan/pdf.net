using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit bounded OCR with shared provider contexts and no implicit rasterizer, native library or model installation.</summary>
public static class PdfOcr
{
    /// <summary>Recognizes an explicitly captured raster. Results retain engine-order geometry/confidence; accuracy and semantic order require separate qualification.</summary>
    public static PdfOcrResult Recognize(PdfOcrRaster raster, PdfOcrOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (raster == null)
            throw new ArgumentNullException(nameof(raster));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        var provider = providers.GetRequired<IPdfOcrProvider>(PdfCapability.RecognizeText);
        PdfOcrResult? result = null;
        var report = PdfFont.Run("RecognizeOcr", providers.GetIdentifier(PdfCapability.RecognizeText), policy, cancellationToken, c => result = RecognizeCore(raster, options, provider, c), PdfCapability.RecognizeText);
        result!.OperationReport = report;
        return result;
    }

    internal static PdfOcrResult RecognizeCore(PdfOcrRaster raster, PdfOcrOptions options, IPdfOcrProvider provider, PdfOperationContext c)
    {
        c.Checkpoint();
        c.ConsumePixels(raster.Width, raster.Height);
        c.Consume(PdfResourceKind.InputBytes, raster.ByteLength);
        c.Consume(PdfResourceKind.Objects, 1);
        var result = provider.Recognize(raster, options, c);
        c.Checkpoint();
        if (result == null || result.Language != options.Language)
            throw PdfWriterCore.Error(c, "Ocr.ProviderBinding", "The OCR provider returned a missing result or silently changed the requested language.");
        if (result.Words.Count > options.MaximumWords)
            throw PdfWriterCore.Error(c, "Ocr.WordLimit", "The recognized word inventory exceeds its explicit ceiling.");
        long characters = result.Text.Length;
        if (characters > options.MaximumCharacters)
            throw PdfWriterCore.Error(c, "Ocr.CharacterLimit", "Recognized Unicode exceeds its explicit total character ceiling.");
        c.Consume(PdfResourceKind.ExpandedBytes, result.Text.Length * 2L + result.Words.Count * 16L + 256);
        c.Consume(PdfResourceKind.Objects, result.Words.Count + 1L);
        foreach (var word in result.Words)
        {
            c.Checkpoint();
            characters += word.Text.Length;
            if (characters > options.MaximumCharacters)
                throw PdfWriterCore.Error(c, "Ocr.CharacterLimit", "The recognized Unicode/word inventory exceeds its explicit total character ceiling.");
            c.Consume(PdfResourceKind.ExpandedBytes, word.Text.Length * 2L + 128);
            if (word.Bounds.Right > raster.Width || word.Bounds.Top > raster.Height)
                throw PdfWriterCore.Error(c, "Ocr.WordGeometry", "Recognized word bounds exceed the original raster geometry.");
        }

        if (characters > options.MaximumCharacters)
            throw PdfWriterCore.Error(c, "Ocr.CharacterLimit", "Recognized Unicode exceeds its explicit total character ceiling.");
        c.Consume(PdfResourceKind.ExpandedBytes, (result.Language.Length + result.EngineVersion.Length + result.ModelIdentity.Length) * 2L + result.Words.Count * 16L + 256);
        return new(result.Text, result.Words.ToArray(), result.Language, result.EngineVersion, result.ModelIdentity, result.MeanConfidence);
    }
}
