using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit predeclared per-language error thresholds, separate from engine confidence and SDK qualification.</summary>
public sealed class PdfOcrAccuracyThresholds
{
    /// <summary>Captures maximum character/word error ratios0–1. Measured ratios may exceed1 when insertions dominate.</summary>
    public PdfOcrAccuracyThresholds(double maximumCharacterErrorRate, double maximumWordErrorRate)
    {
        if (double.IsNaN(maximumCharacterErrorRate) || double.IsInfinity(maximumCharacterErrorRate) || maximumCharacterErrorRate < 0 || maximumCharacterErrorRate > 1 || double.IsNaN(maximumWordErrorRate) || double.IsInfinity(maximumWordErrorRate) || maximumWordErrorRate < 0 || maximumWordErrorRate > 1)
            throw new ArgumentOutOfRangeException(nameof(maximumCharacterErrorRate));
        MaximumCharacterErrorRate = maximumCharacterErrorRate;
        MaximumWordErrorRate = maximumWordErrorRate;
    }

    /// <summary>Gets the predeclared maximum character error ratio.</summary>
    public double MaximumCharacterErrorRate { get; }
    /// <summary>Gets the predeclared maximum word error ratio.</summary>
    public double MaximumWordErrorRate { get; }
}

/// <summary>An explicit labeled recognition observation. Reference text must come from a separately retained corpus, not generated implementation output.</summary>
public sealed class PdfOcrAccuracySample
{
    /// <summary>Captures case/language/model identity and exact valid Unicode reference/recognized strings, each bounded to one million UTF16 units.</summary>
    public PdfOcrAccuracySample(string identifier, string language, string modelIdentity, string referenceText, string recognizedText)
    {
        PdfOcrRules.Text(identifier ?? throw new ArgumentNullException(nameof(identifier)));
        PdfOcrRules.Text(language ?? throw new ArgumentNullException(nameof(language)));
        PdfOcrRules.Text(modelIdentity ?? throw new ArgumentNullException(nameof(modelIdentity)));
        Identifier = Guard.Text(identifier, nameof(identifier), 256);
        Language = Guard.Text(language, nameof(language), 256);
        ModelIdentity = Guard.Text(modelIdentity, nameof(modelIdentity), 4096);
        if (referenceText == null || recognizedText == null || referenceText.Length > 1000000 || recognizedText.Length > 1000000)
            throw new ArgumentException("Finite nonnull reference and recognized Unicode are required.", nameof(referenceText));
        PdfOcrRules.Text(referenceText);
        PdfOcrRules.Text(recognizedText);
        ReferenceText = referenceText;
        RecognizedText = recognizedText;
    }

    /// <summary>Gets the separately labeled corpus case identity.</summary>
    public string Identifier { get; }
    /// <summary>Gets the exact declared language or mixed-model alias selection.</summary>
    public string Language { get; }
    /// <summary>Gets the exact observed model selection identity; versions are never pooled implicitly.</summary>
    public string ModelIdentity { get; }
    /// <summary>Gets exact corpus reference Unicode.</summary>
    public string ReferenceText { get; }
    /// <summary>Gets exact recognized Unicode, without confidence-based alteration.</summary>
    public string RecognizedText { get; }
}

/// <summary>Explicit bounded accuracy evaluation with thresholds fixed by the caller before observations are measured.</summary>
public sealed class PdfOcrAccuracyOptions
{
    /// <summary>Captures1–256 exact language threshold keys, finite case/DP-work limits and per-case Unicode limits. No threshold is inferred from confidence or measured output.</summary>
    public PdfOcrAccuracyOptions(IReadOnlyDictionary<string, PdfOcrAccuracyThresholds> thresholds, int maximumSamples = 10000, long maximumComparisonCells = 16000000, int maximumCharactersPerSample = 1000000)
    {
        if (thresholds == null || thresholds.Count < 1 || thresholds.Count > 256)
            throw new ArgumentException("Explicit finite per-language thresholds are required.", nameof(thresholds));
        if (maximumSamples < 1 || maximumSamples > 100000 || maximumComparisonCells < 1 || maximumComparisonCells > 1000000000 || maximumCharactersPerSample < 1 || maximumCharactersPerSample > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumSamples));
        var copy = new Dictionary<string, PdfOcrAccuracyThresholds>(StringComparer.Ordinal);
        foreach (var item in thresholds)
        {
            if (copy.Count == 256 || item.Value == null)
                throw new ArgumentException("Finite nonnull thresholds are required.", nameof(thresholds));
            copy.Add(Guard.Text(item.Key, nameof(thresholds), 256), item.Value);
        }

        Thresholds = new ReadOnlyDictionary<string, PdfOcrAccuracyThresholds>(copy);
        MaximumSamples = maximumSamples;
        MaximumComparisonCells = maximumComparisonCells;
        MaximumCharactersPerSample = maximumCharactersPerSample;
    }

    /// <summary>Gets explicit exact-language thresholds.</summary>
    public IReadOnlyDictionary<string, PdfOcrAccuracyThresholds> Thresholds { get; }
    /// <summary>Gets the finite corpus case ceiling.</summary>
    public int MaximumSamples { get; }
    /// <summary>Gets the total bounded character-plus-word edit-distance matrix cell ceiling.</summary>
    public long MaximumComparisonCells { get; }
    /// <summary>Gets each reference/recognized string's UTF16 ceiling.</summary>
    public int MaximumCharactersPerSample { get; }
}

/// <summary>A per-language/model error observation; passing thresholds is not production or model qualification.</summary>
public sealed class PdfOcrAccuracyGroup
{
    internal PdfOcrAccuracyGroup(string language, string model, int samples, long chars, long words, long charErrors, long wordErrors, PdfOcrAccuracyThresholds thresholds)
    {
        Language = language;
        ModelIdentity = model;
        Samples = samples;
        ReferenceCharacters = chars;
        ReferenceWords = words;
        CharacterErrors = charErrors;
        WordErrors = wordErrors;
        Thresholds = thresholds;
    }

    /// <summary>Gets exact language/selection identity.</summary>
    public string Language { get; }
    /// <summary>Gets exact model/version selection identity.</summary>
    public string ModelIdentity { get; }
    /// <summary>Gets the observed case count.</summary>
    public int Samples { get; }
    /// <summary>Gets corpus reference Unicode scalar count, including whitespace.</summary>
    public long ReferenceCharacters { get; }
    /// <summary>Gets corpus reference whitespace-delimited word count.</summary>
    public long ReferenceWords { get; }
    /// <summary>Gets character substitutions/insertions/deletions.</summary>
    public long CharacterErrors { get; }
    /// <summary>Gets word substitutions/insertions/deletions.</summary>
    public long WordErrors { get; }
    /// <summary>Gets CER, or null when the corpus group has no reference characters.</summary>
    public double? CharacterErrorRate => ReferenceCharacters == 0 ? null : CharacterErrors / (double)ReferenceCharacters;
    /// <summary>Gets WER, or null when the corpus group has no reference words.</summary>
    public double? WordErrorRate => ReferenceWords == 0 ? null : WordErrors / (double)ReferenceWords;
    /// <summary>Gets the predeclared limits applied to this observation.</summary>
    public PdfOcrAccuracyThresholds Thresholds { get; }
    /// <summary>Gets whether nonempty reference character/word observations meet both thresholds. All-blank groups cannot pass.</summary>
    public bool ThresholdsMet => CharacterErrorRate.HasValue && WordErrorRate.HasValue && CharacterErrorRate.Value <= Thresholds.MaximumCharacterErrorRate && WordErrorRate.Value <= Thresholds.MaximumWordErrorRate;
}

/// <summary>An immutable corpus measurement bound to exact reference/recognized data digests, without a qualification claim.</summary>
public sealed class PdfOcrAccuracyReport
{
    internal PdfOcrAccuracyReport(string corpus, string version, string references, string actual, PdfOcrAccuracyGroup[] groups, long cells, PdfOperationReport report)
    {
        CorpusIdentifier = corpus;
        CorpusVersion = version;
        ReferenceSha256 = references;
        RecognitionSha256 = actual;
        Groups = Array.AsReadOnly(groups);
        ComparisonCells = cells;
        OperationReport = report;
    }

    /// <summary>Gets explicit retained corpus identity.</summary>
    public string CorpusIdentifier { get; }
    /// <summary>Gets explicit corpus version.</summary>
    public string CorpusVersion { get; }
    /// <summary>Gets exact ordered sample/identity/reference digest.</summary>
    public string ReferenceSha256 { get; }
    /// <summary>Gets exact ordered sample/identity/recognition digest.</summary>
    public string RecognitionSha256 { get; }
    /// <summary>Gets separate observations for each exact language/model pair.</summary>
    public IReadOnlyList<PdfOcrAccuracyGroup> Groups { get; }
    /// <summary>Gets bounded comparison work charged against the explicit evaluation ceiling.</summary>
    public long ComparisonCells { get; }
    /// <summary>Gets root diagnostics and budgets.</summary>
    public PdfOperationReport OperationReport { get; }
}

/// <summary>Original bounded Unicode-scalar CER and ordinal whitespace-word WER, independent of recognition confidence.</summary>
public static class PdfOcrAccuracy
{
    /// <summary>Gets the fixed comparison definition: exact Unicode scalars, case-sensitive words split by Unicode15.1 White_Space, with no hidden normalization or trimming.</summary>
    public const string ComparisonProfile = "unicode-scalars+whitespace15.1+ordinal-v1";
    /// <summary>Measures a finite labeled corpus against explicit prior thresholds. It never downloads models or declares production readiness.</summary>
    public static PdfOcrAccuracyReport Evaluate(IEnumerable<PdfOcrAccuracySample> samples, string corpusIdentifier, string corpusVersion, PdfOcrAccuracyOptions options, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (samples == null)
            throw new ArgumentNullException(nameof(samples));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        Guard.Text(corpusIdentifier, nameof(corpusIdentifier), 256);
        Guard.Text(corpusVersion, nameof(corpusVersion), 256);
        var groups = Array.Empty<PdfOcrAccuracyGroup>();
        long cells = 0;
        string referenceDigest = "", actualDigest = "";
        var report = PdfFont.Run("EvaluateOcrAccuracy", "managed.ocr-accuracy", policy, cancellationToken, c =>
        {
            var stats = new Dictionary<(string Language, string Model), Stats>();
            var identifiers = new HashSet<string>(StringComparer.Ordinal);
            int count = 0;
            using var referenceHash = SHA256.Create();
            using var actualHash = SHA256.Create();
            foreach (var sample in samples)
            {
                c.Checkpoint();
                if (count++ == options.MaximumSamples)
                    throw Error("SampleLimit", "The explicit OCR evaluation case ceiling was exceeded.");
                if (sample == null || !identifiers.Add(sample.Identifier))
                    throw Error("SampleIdentity", "Corpus case identities must be nonnull and unique.");
                if (sample.ReferenceText.Length > options.MaximumCharactersPerSample || sample.RecognizedText.Length > options.MaximumCharactersPerSample)
                    throw Error("CharacterLimit", "A corpus string exceeds its explicit UTF16 ceiling.");
                if (!options.Thresholds.TryGetValue(sample.Language, out var threshold))
                    throw Error("ThresholdMissing", "Every measured language requires predeclared thresholds.");
                c.Consume(PdfResourceKind.InputBytes, (sample.ReferenceText.Length + (long)sample.RecognizedText.Length) * 2);
                c.Consume(PdfResourceKind.Objects, 2);
                c.Consume(PdfResourceKind.ExpandedBytes, 512);
                Hash(referenceHash, sample.Identifier, c);
                Hash(actualHash, sample.Identifier, c);
                Hash(referenceHash, sample.Language, c);
                Hash(actualHash, sample.Language, c);
                Hash(referenceHash, sample.ModelIdentity, c);
                Hash(actualHash, sample.ModelIdentity, c);
                Hash(referenceHash, sample.ReferenceText, c);
                Hash(actualHash, sample.RecognizedText, c);
                var expected = Scalars(sample.ReferenceText, c);
                var recognized = Scalars(sample.RecognizedText, c);
                var expectedWords = Words(sample.ReferenceText, c);
                var recognizedWords = Words(sample.RecognizedText, c);
                long work = (expected.Length + 1L) * (recognized.Length + 1L) + (expectedWords.Length + 1L) * (recognizedWords.Length + 1L);
                if (work > options.MaximumComparisonCells - cells)
                    throw Error("ComparisonLimit", "The explicit total edit-distance work ceiling was exceeded.");
                cells += work;
                int charErrors = Distance(expected, recognized, EqualityComparer<int>.Default, c), wordErrors = Distance(expectedWords, recognizedWords, StringComparer.Ordinal, c);
                var key = (sample.Language, sample.ModelIdentity);
                if (!stats.TryGetValue(key, out var item))
                {
                    c.Consume(PdfResourceKind.ExpandedBytes, 256);
                    stats.Add(key, item = new(sample.Language, sample.ModelIdentity, threshold));
                }

                item.Samples++;
                item.Characters += expected.Length;
                item.Words += expectedWords.Length;
                item.CharacterErrors += charErrors;
                item.WordErrors += wordErrors;
            }

            if (count == 0)
                throw Error("EmptyCorpus", "Accuracy measurements require at least one explicitly labeled case.");
            referenceHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            actualHash.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
            referenceDigest = Hex(referenceHash.Hash!);
            actualDigest = Hex(actualHash.Hash!);
            c.Consume(PdfResourceKind.Objects, stats.Count);
            c.Consume(PdfResourceKind.ExpandedBytes, stats.Count * 256L + 256);
            groups = stats.Values.OrderBy(s => s.Language, StringComparer.Ordinal).ThenBy(s => s.Model, StringComparer.Ordinal).Select(s => new PdfOcrAccuracyGroup(s.Language, s.Model, s.Samples, s.Characters, s.Words, s.CharacterErrors, s.WordErrors, s.Thresholds)).ToArray();
            PdfException Error(string code, string message) => PdfWriterCore.Error(c, "Ocr.Accuracy" + code, message);
        }, PdfCapability.RecognizeText);
        return new(corpusIdentifier, corpusVersion, referenceDigest, actualDigest, groups, cells, report);
    }

    private static int[] Scalars(string text, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, text.Length * 4L + 64);
        var values = new List<int>(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if ((i & 1023) == 0)
                c.Checkpoint();
            int value = text[i];
            if (char.IsHighSurrogate(text[i]))
                value = 0x10000 + ((text[i++] - 0xd800) << 10) + text[i] - 0xdc00;
            values.Add(value);
        }

        c.Consume(PdfResourceKind.ExpandedBytes, values.Count * 4L + 64);
        return values.ToArray();
    }

    private static string[] Words(string text, PdfOperationContext c)
    {
        var words = new List<string>();
        int start = -1;
        for (int i = 0; i <= text.Length; i++)
        {
            if ((i & 1023) == 0)
                c.Checkpoint();
            bool space = i == text.Length || WhiteSpace(text[i]);
            if (!space && start < 0)
                start = i;
            else if (space && start >= 0)
            {
                c.Consume(PdfResourceKind.Objects, 1);
                c.Consume(PdfResourceKind.ExpandedBytes, (i - start) * 2L + 80);
                words.Add(text.Substring(start, i - start));
                start = -1;
            }
        }

        c.Consume(PdfResourceKind.ExpandedBytes, words.Count * 16L + 128);
        return words.ToArray();
    }

    private static bool WhiteSpace(char value) => value >= 9 && value <= 13 || value == 32 || value == 133 || value == 160 || value == 5760 || value >= 8192 && value <= 8202 || value == 8232 || value == 8233 || value == 8239 || value == 8287 || value == 12288;
    private static int Distance<T>(IReadOnlyList<T> a, IReadOnlyList<T> b, IEqualityComparer<T> comparer, PdfOperationContext c)
    {
        if (a.Count < b.Count)
        {
            var swap = a;
            a = b;
            b = swap;
        }

        if (b.Count == 0)
            return a.Count;
        c.Consume(PdfResourceKind.ExpandedBytes, (b.Count + 1L) * 8 + 128);
        var previous = new int[b.Count + 1];
        var next = new int[b.Count + 1];
        for (int j = 0; j <= b.Count; j++)
            previous[j] = j;
        for (int i = 1; i <= a.Count; i++)
        {
            c.Checkpoint();
            next[0] = i;
            for (int j = 1; j <= b.Count; j++)
            {
                if ((j & 1023) == 0)
                    c.Checkpoint();
                next[j] = Math.Min(Math.Min(previous[j] + 1, next[j - 1] + 1), previous[j - 1] + (comparer.Equals(a[i - 1], b[j - 1]) ? 0 : 1));
            }

            var swap = previous;
            previous = next;
            next = swap;
        }

        return previous[b.Count];
    }

    private static void Hash(HashAlgorithm hash, string text, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, text.Length * 4L + 128);
        var bytes = Encoding.UTF8.GetBytes(text);
        var length = new byte[4];
        int n = bytes.Length;
        for (int i = 0; i < 4; i++)
            length[i] = (byte)((uint)n >> (i * 8));
        hash.TransformBlock(length, 0, 4, length, 0);
        hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
    }

    private static string Hex(byte[] bytes) => BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
    private sealed class Stats
    {
        internal readonly string Language, Model;
        internal readonly PdfOcrAccuracyThresholds Thresholds;
        internal int Samples;
        internal long Characters, Words, CharacterErrors, WordErrors;
        internal Stats(string language, string model, PdfOcrAccuracyThresholds threshold)
        {
            Language = language;
            Model = model;
            Thresholds = threshold;
        }
    }
}
