using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Immutable paragraph direction, writing direction, language and feature choices.</summary>
public sealed class PdfTextOptions
{
    /// <summary>Captures explicit shaping settings. No language, font or feature discovery occurs.</summary>
    public PdfTextOptions(PdfParagraphDirection paragraphDirection = PdfParagraphDirection.Auto, bool vertical = false, string language = "und", params PdfTextFeature[] features)
    {
        if (!Enum.IsDefined(typeof(PdfParagraphDirection), paragraphDirection))
            throw new ArgumentOutOfRangeException(nameof(paragraphDirection));
        if (string.IsNullOrEmpty(language) || language.Length > 63 || language.Any(c => !(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-')))
            throw new ArgumentException("A bounded ASCII language tag is required.", nameof(language));
        if (features == null || features.Length > 64 || features.Any(x => x.Tag == null))
            throw new ArgumentException("At most 64 initialized features are allowed.", nameof(features));
        ParagraphDirection = paragraphDirection;
        Vertical = vertical;
        Language = language;
        Features = new ReadOnlyCollection<PdfTextFeature>((PdfTextFeature[])features.Clone());
    }

    /// <summary>Gets the base-direction policy.</summary>
    public PdfParagraphDirection ParagraphDirection { get; }
    /// <summary>Gets whether vertical top-to-bottom shaping was requested.</summary>
    public bool Vertical { get; }
    /// <summary>Gets the explicit language.</summary>
    public string Language { get; }
    /// <summary>Gets immutable feature overrides.</summary>
    public IReadOnlyList<PdfTextFeature> Features { get; }
}

/// <summary>A visually ordered immutable run with exact font identity and logical provenance.</summary>
public sealed class PdfShapedRun
{
    internal PdfShapedRun(PdfShapingRequest request, PdfShapedGlyph[] glyphs, int level, int paragraph)
    {
        Request = request;
        Glyphs = new ReadOnlyCollection<PdfShapedGlyph>(glyphs);
        EmbeddingLevel = level;
        ParagraphIndex = paragraph;
    }

    /// <summary>Gets the exact itemization request.</summary>
    public PdfShapingRequest Request { get; }
    /// <summary>Gets positioned glyphs in visual order.</summary>
    public IReadOnlyList<PdfShapedGlyph> Glyphs { get; }
    /// <summary>Gets the run's resolved bidi level.</summary>
    public int EmbeddingLevel { get; }
    /// <summary>Gets its logical paragraph index.</summary>
    public int ParagraphIndex { get; }
}

/// <summary>Unicode analysis and immutable positioned runs. Wrapping and pagination use the layout service.</summary>
public sealed class PdfShapedText
{
    internal PdfShapedText(PdfUnicodeAnalysis unicode, List<PdfShapedRun> runs, PdfTextOptions options)
    {
        Unicode = unicode;
        Runs = runs.AsReadOnly();
        Options = options;
    }

    /// <summary>Gets unchanged logical Unicode and grapheme/bidi analysis.</summary>
    public PdfUnicodeAnalysis Unicode { get; }
    /// <summary>Gets paragraph runs in visual order.</summary>
    public IReadOnlyList<PdfShapedRun> Runs { get; }
    /// <summary>Gets captured text settings.</summary>
    public PdfTextOptions Options { get; }
    /// <summary>Gets the successful shaping report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;
}

/// <summary>Explicit offline Unicode itemization, cluster-preserving font fallback and shaping.</summary>
public static class PdfText
{
    /// <summary>Shapes using the first supplied face covering each whole grapheme. Missing coverage and unsupported shaping fail explicitly.</summary>
    public static PdfShapedText Shape(string text, PdfFontFallback fonts, PdfProviderRegistry providers, PdfTextOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (fonts == null)
            throw new ArgumentNullException(nameof(fonts));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        options ??= new PdfTextOptions();
        var shaper = providers.GetRequired<IPdfTextShaper>(PdfCapability.ShapeText);
        PdfShapedText? result = null;
        var report = PdfFont.Run("ShapeText", providers.GetIdentifier(PdfCapability.ShapeText), policy, cancellationToken, context => result = ShapeCore(text, fonts, shaper, options, context), PdfCapability.ShapeText);
        result!.OperationReport = report;
        result.Unicode.OperationReport = report;
        return result;
    }

    internal static PdfShapedText ShapeCore(string text, PdfFontFallback fonts, IPdfTextShaper shaper, PdfTextOptions options, PdfOperationContext context)
    {
        var unicode = PdfUnicode.AnalyzeCore(text, options.ParagraphDirection, context);
        var scalars = unicode.Scalars;
        context.Consume(PdfResourceKind.ExpandedBytes, scalars.Count * 48L + unicode.Graphemes.Count * 192L + 512);
        var visual = new int[scalars.Count];
        for (int i = 0; i < unicode.VisualScalarOrder.Count; i++)
            visual[unicode.VisualScalarOrder[i]] = i;
        var resolvedScripts = new uint[scalars.Count];
        uint following = 0;
        for (int i = scalars.Count - 1; i >= 0; i--)
        {
            if (Concrete(scalars[i].ScriptTag))
                following = scalars[i].ScriptTag;
            resolvedScripts[i] = following;
        }

        var items = new List<(int Start, int End, int Level, int Paragraph, int Visual, uint Script, PdfFont Font)>();
        int paragraph = 0;
        uint preceding = 0;
        foreach (var cluster in unicode.Graphemes)
        {
            context.Checkpoint();
            int first = cluster.FirstScalar;
            while (paragraph + 1 < unicode.Paragraphs.Count && first >= unicode.Paragraphs[paragraph].FirstScalar + unicode.Paragraphs[paragraph].ScalarCount)
            {
                paragraph++;
                preceding = 0;
            }

            bool visible = false;
            int level = 0;
            uint script = 0;
            int position = int.MaxValue;
            for (int i = first; i < first + cluster.ScalarCount; i++)
            {
                var scalar = scalars[i];
                if (scalar.BidiClass == PdfBidiClass.S)
                    throw PdfWriterCore.Error(context, "Text.TabLayoutRequired", "Segment separators require an explicit layout/tab-stop policy.");
                if (scalar.BidiClass == PdfBidiClass.B)
                    continue;
                if (scalar.EmbeddingLevel < 0 || scalar.DefaultIgnorable)
                    continue;
                if (!visible)
                    level = scalar.EmbeddingLevel;
                visible = true;
                position = Math.Min(position, visual[i]);
                if (Concrete(scalar.ScriptTag))
                    script = scalar.ScriptTag;
            }

            if (!visible)
                continue;
            if (script == 0)
                script = preceding != 0 ? preceding : resolvedScripts[first] != 0 ? resolvedScripts[first] : 0x5a797979;
            else
                preceding = script;
            PdfFont? selected = null;
            foreach (var font in fonts.Fonts)
            {
                bool covers = true;
                for (int i = first; i < first + cluster.ScalarCount; i++)
                    if (!scalars[i].DefaultIgnorable && scalars[i].EmbeddingLevel >= 0 && font.MapCore(scalars[i].Value, context) == 0)
                    {
                        covers = false;
                        break;
                    }

                if (covers)
                {
                    selected = font;
                    break;
                }
            }

            if (selected == null)
                throw PdfWriterCore.Error(context, "Text.MissingClusterFont", "No explicitly supplied font covers the complete Unicode grapheme cluster.");
            int end = cluster.Utf16Offset + cluster.Utf16Length;
            if (items.Count > 0)
            {
                var previous = items[items.Count - 1];
                if (previous.End == cluster.Utf16Offset && previous.Level == level && previous.Paragraph == paragraph && previous.Script == script && ReferenceEquals(previous.Font, selected))
                {
                    items[items.Count - 1] = (previous.Start, end, level, paragraph, Math.Min(previous.Visual, position), script, selected);
                    continue;
                }
            }

            items.Add((cluster.Utf16Offset, end, level, paragraph, position, script, selected));
        }

        var runs = new List<PdfShapedRun>();
        foreach (var item in items.OrderBy(x => x.Paragraph).ThenBy(x => x.Visual))
        {
            context.Checkpoint();
            var direction = options.Vertical ? PdfTextDirection.TopToBottom : (item.Level & 1) == 0 ? PdfTextDirection.LeftToRight : PdfTextDirection.RightToLeft;
            var request = new PdfShapingRequest(text, item.Start, item.End - item.Start, item.Font, direction, item.Script, options.Language, options.Features);
            var supplied = shaper.Shape(request, context) ?? throw PdfWriterCore.Error(context, "Text.ProviderResult", "The shaper returned no glyph sequence.");
            context.Consume(PdfResourceKind.Objects, supplied.Count + 1L);
            context.Consume(PdfResourceKind.ExpandedBytes, supplied.Count * 56L + 128);
            var glyphs = new PdfShapedGlyph[supplied.Count];
            int last = direction == PdfTextDirection.RightToLeft ? int.MaxValue : -1;
            for (int i = 0; i < glyphs.Length; i++)
            {
                context.Checkpoint();
                var glyph = supplied[i];
                int c = glyph.Cluster;
                if (glyph.GlyphIndex == 0 || glyph.GlyphIndex >= item.Font.Info.GlyphCount || c < item.Start || c >= item.End || char.IsLowSurrogate(text[c]) || (direction == PdfTextDirection.RightToLeft ? c > last : c < last))
                    throw PdfWriterCore.Error(context, "Text.ProviderResult", "The shaper returned an unavailable glyph or an invalid logical cluster.");
                last = c;
                glyphs[i] = glyph;
            }

            if (glyphs.Length != 0 && glyphs.Min(g => g.Cluster) != item.Start)
                throw PdfWriterCore.Error(context, "Text.ProviderResult", "The shaper lost the first logical cluster of a visible run.");
            if (glyphs.Length == 0)
                throw PdfWriterCore.Error(context, "Text.ProviderResult", "The shaper lost a visible run.");
            runs.Add(new PdfShapedRun(request, glyphs, item.Level, item.Paragraph));
        }

        return new PdfShapedText(unicode, runs, options);
    }

    private static bool Concrete(uint script) => script != 0x5a797979 && script != 0x5a696e68 && script != 0x5a7a7a7a;
}
