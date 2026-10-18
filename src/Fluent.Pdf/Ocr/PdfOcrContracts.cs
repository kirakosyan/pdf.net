namespace Fluent.Pdf;
/// <summary>Explicit local segmentation without implicit orientation/script-model discovery.</summary>
public enum PdfOcrSegmentation
{
    /// <summary>Automatic page segmentation without orientation detection.</summary>
    Automatic,
    /// <summary>A single uniform text block.</summary>
    SingleBlock,
    /// <summary>A single text line.</summary>
    SingleLine,
    /// <summary>Sparse text without an inferred document reading order.</summary>
    SparseText
}

/// <summary>Explicit recognition language and finite output ceilings. A provider must reject unsupported languages rather than substitute a model.</summary>
public sealed class PdfOcrOptions
{
    /// <summary>Captures a bounded explicit language token, segmentation and character/word ceilings. No native/model download is implicit.</summary>
    public PdfOcrOptions(string language, PdfOcrSegmentation segmentation = PdfOcrSegmentation.Automatic, int maximumCharacters = 100000, int maximumWords = 100000)
    {
        if (language == null || language.Length < 1 || language.Length > 256 || language.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || c == '_' || c == '+')) || language[0] == '+' || language[language.Length - 1] == '+' || language.Contains("++"))
            throw new ArgumentException("An explicit bounded language token is required.", nameof(language));
        if (!Enum.IsDefined(typeof(PdfOcrSegmentation), segmentation))
            throw new ArgumentOutOfRangeException(nameof(segmentation));
        if (maximumCharacters < 1 || maximumCharacters > 1000000 || maximumWords < 1 || maximumWords > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumCharacters));
        Language = language;
        Segmentation = segmentation;
        MaximumCharacters = maximumCharacters;
        MaximumWords = maximumWords;
    }

    /// <summary>Gets the explicitly requested language/model selection token.</summary>
    public string Language { get; }
    /// <summary>Gets explicit segmentation.</summary>
    public PdfOcrSegmentation Segmentation { get; }
    /// <summary>Gets the total returned UTF16 character ceiling across text and word records.</summary>
    public int MaximumCharacters { get; }
    /// <summary>Gets the finite word record ceiling.</summary>
    public int MaximumWords { get; }
}

/// <summary>An immutable engine-recognized word, with source-raster geometry and confidence distinct from correctness.</summary>
public sealed class PdfOcrWord
{
    /// <summary>Captures text, top-left raster-pixel bounds, confidence0–100 or null, and engine block/paragraph/line/word identities. No PDF-space transform or reading order is inferred.</summary>
    public PdfOcrWord(string text, PdfRect bounds, double? confidence, int block, int paragraph, int line, int word)
    {
        if (text == null || text.Length == 0 || text.Length > 1000000)
            throw new ArgumentException("Bounded nonempty recognized word text is required.", nameof(text));
        PdfOcrRules.Text(text);
        if (bounds.X < 0 || bounds.Y < 0 || bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(bounds));
        PdfOcrRules.Confidence(confidence);
        if (block < 0 || paragraph < 0 || line < 0 || word < 0)
            throw new ArgumentOutOfRangeException(nameof(block));
        Text = text;
        Bounds = bounds;
        Confidence = confidence;
        Block = block;
        Paragraph = paragraph;
        Line = line;
        Word = word;
    }

    /// <summary>Gets recognized Unicode without a correctness guarantee.</summary>
    public string Text { get; }
    /// <summary>Gets bounds in original raster pixels, with top-left origin.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets engine confidence0–100, or null when absent. It is not an accuracy measurement.</summary>
    public double? Confidence { get; }
    /// <summary>Gets the engine block identity.</summary>
    public int Block { get; }
    /// <summary>Gets the engine paragraph identity within its block.</summary>
    public int Paragraph { get; }
    /// <summary>Gets the engine line identity within its paragraph.</summary>
    public int Line { get; }
    /// <summary>Gets the engine word identity within its line.</summary>
    public int Word { get; }
}

/// <summary>Immutable recognized Unicode and engine-order word geometry. It does not establish language accuracy, reading order or production qualification.</summary>
public sealed class PdfOcrResult
{
    /// <summary>Captures finite recognized text/words and exact provider/model provenance. Arrays are cloned and no caller raster is retained.</summary>
    public PdfOcrResult(string text, PdfOcrWord[] words, string language, string engineVersion, string modelIdentity, double? meanConfidence = null)
    {
        if (text == null || text.Length > 1000000)
            throw new ArgumentException("Bounded nonnull recognized text is required.", nameof(text));
        PdfOcrRules.Text(text);
        if (words == null || words.Length > 1000000 || words.Any(w => w == null))
            throw new ArgumentException("A bounded nonnull word inventory is required.", nameof(words));
        Language = Guard.Text(language, nameof(language), 256);
        EngineVersion = Guard.Text(engineVersion, nameof(engineVersion), 256);
        ModelIdentity = Guard.Text(modelIdentity, nameof(modelIdentity), 4096);
        PdfOcrRules.Confidence(meanConfidence);
        Text = text;
        Words = Array.AsReadOnly((PdfOcrWord[])words.Clone());
        MeanConfidence = meanConfidence;
    }

    /// <summary>Captures recognized data together with exact caller-selected model declarations. Model metadata never substitutes for accuracy/license qualification.</summary>
    internal PdfOcrResult(string text, PdfOcrWord[] words, string language, string engineVersion, string modelIdentity, double? meanConfidence, PdfOcrLanguageModel[] models) : this(text, words, language, engineVersion, modelIdentity, meanConfidence)
    {
        if (models == null || models.Length > 32 || models.Any(m => m == null))
            throw new ArgumentException("A bounded explicit model metadata inventory is required.", nameof(models));
        Models = Array.AsReadOnly((PdfOcrLanguageModel[])models.Clone());
    }

    /// <summary>Captures recognized data with explicit selected model declarations, preserving the legacy constructor signature.</summary>
    public static PdfOcrResult WithModels(string text, PdfOcrWord[] words, string language, string engineVersion, string modelIdentity, double? meanConfidence, PdfOcrLanguageModel[] models) => new PdfOcrResult(text, words, language, engineVersion, modelIdentity, meanConfidence, models);
    /// <summary>Gets exact selected model declarations; empty means the provider supplied only a legacy digest without complete model metadata.</summary>
    public IReadOnlyList<PdfOcrLanguageModel> Models { get; } = Array.Empty<PdfOcrLanguageModel>();
    /// <summary>Gets raw recognized Unicode in engine order.</summary>
    public string Text { get; }
    /// <summary>Gets words in engine-returned order, separately from later inferred reading order.</summary>
    public IReadOnlyList<PdfOcrWord> Words { get; }
    /// <summary>Gets the exact recognized language selection, without silent fallback.</summary>
    public string Language { get; }
    /// <summary>Gets the actually checked native engine version.</summary>
    public string EngineVersion { get; }
    /// <summary>Gets exact explicit model provenance, such as a pinned content digest.</summary>
    public string ModelIdentity { get; }
    /// <summary>Gets engine mean confidence0–100, or null; it does not qualify accuracy.</summary>
    public double? MeanConfidence { get; }
    /// <summary>Gets the root public operation report, or null for a provider-only result.</summary>
    public PdfOperationReport? OperationReport { get; internal set; }
}

/// <summary>An explicitly configured OCR provider. Native/model paths, provenance and deployment are provider choices with no default discovery.</summary>
public interface IPdfOcrProvider : IPdfProvider
{
    /// <summary>Recognizes an immutable raster under the mandatory root context. Charge before native/raster/text allocation and preserve cancellation/latched failures.</summary>
    PdfOcrResult Recognize(PdfOcrRaster raster, PdfOcrOptions options, PdfOperationContext context);
}

internal static class PdfOcrRules
{
    internal static void Confidence(double? n)
    {
        if (n.HasValue && (double.IsNaN(n.Value) || double.IsInfinity(n.Value) || n.Value < 0 || n.Value > 100))
            throw new ArgumentOutOfRangeException(nameof(n));
    }

    internal static void Text(string text)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '\0')
                throw new ArgumentException("Recognized Unicode must not contain NUL.", nameof(text));
            if (char.IsHighSurrogate(text[i]))
            {
                if (++i == text.Length || !char.IsLowSurrogate(text[i]))
                    throw new ArgumentException("Recognized Unicode must contain valid scalar sequences.", nameof(text));
            }
            else if (char.IsLowSurrogate(text[i]))
                throw new ArgumentException("Recognized Unicode must contain valid scalar sequences.", nameof(text));
        }
    }
}
