using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit paragraph base direction, or automatic first-strong resolution.</summary>
public enum PdfParagraphDirection
{
    /// <summary>Resolve from the first strong character outside isolates, defaulting to left-to-right.</summary>
    Auto,
    /// <summary>Use an even left-to-right paragraph level.</summary>
    LeftToRight,
    /// <summary>Use an odd right-to-left paragraph level.</summary>
    RightToLeft
}

/// <summary>Unicode bidirectional character classes, independent of the installed runtime's Unicode tables.</summary>
public enum PdfBidiClass
{
    /// <summary>Left-to-right.</summary>
    L,
    /// <summary>Right-to-left.</summary>
    R,
    /// <summary>Arabic letter.</summary>
    AL,
    /// <summary>European number.</summary>
    EN,
    /// <summary>European separator.</summary>
    ES,
    /// <summary>European terminator.</summary>
    ET,
    /// <summary>Arabic number.</summary>
    AN,
    /// <summary>Common separator.</summary>
    CS,
    /// <summary>Nonspacing mark.</summary>
    NSM,
    /// <summary>Boundary neutral.</summary>
    BN,
    /// <summary>Paragraph separator.</summary>
    B,
    /// <summary>Segment separator.</summary>
    S,
    /// <summary>Whitespace.</summary>
    WS,
    /// <summary>Other neutral.</summary>
    ON,
    /// <summary>Left-to-right embedding.</summary>
    LRE,
    /// <summary>Left-to-right override.</summary>
    LRO,
    /// <summary>Right-to-left embedding.</summary>
    RLE,
    /// <summary>Right-to-left override.</summary>
    RLO,
    /// <summary>Pop directional format.</summary>
    PDF,
    /// <summary>Left-to-right isolate.</summary>
    LRI,
    /// <summary>Right-to-left isolate.</summary>
    RLI,
    /// <summary>First-strong isolate.</summary>
    FSI,
    /// <summary>Pop directional isolate.</summary>
    PDI
}

/// <summary>One Unicode scalar and its UTF-16 provenance/resolved paragraph level.</summary>
public readonly struct PdfUnicodeScalar
{
    internal PdfUnicodeScalar(int value, int offset, int length, PdfBidiClass bidi, uint script, int level, bool ignorable)
    {
        Value = value;
        Utf16Offset = offset;
        Utf16Length = length;
        BidiClass = bidi;
        ScriptTag = script;
        EmbeddingLevel = level;
        DefaultIgnorable = ignorable;
    }

    /// <summary>Gets the Unicode scalar value.</summary>
    public int Value { get; }
    /// <summary>Gets its original UTF-16 offset.</summary>
    public int Utf16Offset { get; }
    /// <summary>Gets its UTF-16 extent.</summary>
    public int Utf16Length { get; }
    /// <summary>Gets the original Unicode bidi class.</summary>
    public PdfBidiClass BidiClass { get; }
    /// <summary>Gets its ISO 15924 script tag packed in big-endian byte order.</summary>
    public uint ScriptTag { get; }
    /// <summary>Gets the resolved level, or -1 for a format character removed by UAX #9 rule X9.</summary>
    public int EmbeddingLevel { get; }
    /// <summary>Gets its normative default-ignorable property.</summary>
    public bool DefaultIgnorable { get; }
}

/// <summary>An extended grapheme cluster with exact scalar and UTF-16 extents.</summary>
public readonly struct PdfGraphemeCluster
{
    internal PdfGraphemeCluster(int first, int count, int offset, int length)
    {
        FirstScalar = first;
        ScalarCount = count;
        Utf16Offset = offset;
        Utf16Length = length;
    }

    /// <summary>Gets the index of its first scalar.</summary>
    public int FirstScalar { get; }
    /// <summary>Gets the scalar count.</summary>
    public int ScalarCount { get; }
    /// <summary>Gets the original UTF-16 offset.</summary>
    public int Utf16Offset { get; }
    /// <summary>Gets the original UTF-16 extent.</summary>
    public int Utf16Length { get; }
}

/// <summary>A paragraph range and resolved base embedding level.</summary>
public readonly struct PdfBidiParagraph
{
    internal PdfBidiParagraph(int first, int count, int level)
    {
        FirstScalar = first;
        ScalarCount = count;
        EmbeddingLevel = level;
    }

    /// <summary>Gets its first scalar index.</summary>
    public int FirstScalar { get; }
    /// <summary>Gets its scalar count, including its terminating paragraph separator when present.</summary>
    public int ScalarCount { get; }
    /// <summary>Gets its resolved base level.</summary>
    public int EmbeddingLevel { get; }
}

/// <summary>An immutable Unicode analysis with logical provenance, grapheme boundaries and bidi visual indices.</summary>
public sealed class PdfUnicodeAnalysis
{
    internal PdfUnicodeAnalysis(string text, PdfUnicodeScalar[] scalars, PdfGraphemeCluster[] clusters, PdfBidiParagraph[] paragraphs, int[] visual)
    {
        Text = text;
        Scalars = new ReadOnlyCollection<PdfUnicodeScalar>(scalars);
        Graphemes = new ReadOnlyCollection<PdfGraphemeCluster>(clusters);
        Paragraphs = new ReadOnlyCollection<PdfBidiParagraph>(paragraphs);
        VisualScalarOrder = new ReadOnlyCollection<int>(visual);
    }

    /// <summary>Gets unchanged logical Unicode text.</summary>
    public string Text { get; }
    /// <summary>Gets logical scalars and their resolved levels.</summary>
    public IReadOnlyList<PdfUnicodeScalar> Scalars { get; }
    /// <summary>Gets extended grapheme boundaries.</summary>
    public IReadOnlyList<PdfGraphemeCluster> Graphemes { get; }
    /// <summary>Gets paragraph ranges/base levels.</summary>
    public IReadOnlyList<PdfBidiParagraph> Paragraphs { get; }
    /// <summary>Gets UAX #9 visual scalar indices, with X9 format characters excluded.</summary>
    public IReadOnlyList<int> VisualScalarOrder { get; }
    /// <summary>Gets the successful bounded analysis report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;
}

/// <summary>Original managed Unicode analysis using explicitly pinned normative tables.</summary>
public static class PdfUnicode
{
    /// <summary>Gets the pinned Unicode data version.</summary>
    public static string Version => "18.0.0";

    /// <summary>Analyzes well-formed logical UTF-16 without normalization or text substitution.</summary>
    public static PdfUnicodeAnalysis Analyze(string text, PdfParagraphDirection direction = PdfParagraphDirection.Auto, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (!Enum.IsDefined(typeof(PdfParagraphDirection), direction))
            throw new ArgumentOutOfRangeException(nameof(direction));
        PdfUnicodeAnalysis? result = null;
        var report = PdfFont.Run("AnalyzeUnicode", "managed.unicode", policy, cancellationToken, context => result = AnalyzeCore(text, direction, context), PdfCapability.ShapeText);
        result!.OperationReport = report;
        return result;
    }

    internal static PdfUnicodeAnalysis AnalyzeCore(string text, PdfParagraphDirection direction, PdfOperationContext context)
    {
        var data = PdfUnicodeData.For(context);
        context.Consume(PdfResourceKind.InputBytes, text.Length * 2L);
        context.Consume(PdfResourceKind.Objects, text.Length);
        context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 160L + 512);
        var values = new List<int>(text.Length);
        var offsets = new List<int>(text.Length);
        var properties = new List<PdfUnicodeData.Property>(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            context.Checkpoint();
            int offset = i, scalar = text[i];
            if (char.IsSurrogate(text[i]))
            {
                if (!char.IsHighSurrogate(text[i]) || i + 1 >= text.Length || !char.IsLowSurrogate(text[i + 1]))
                    throw PdfWriterCore.Error(context, "Text.InvalidUnicode", "Logical text contains an unpaired UTF-16 surrogate.");
                scalar = char.ConvertToUtf32(text[i], text[++i]);
            }

            values.Add(scalar);
            offsets.Add(offset);
            properties.Add(data.Get(scalar));
        }

        int[] codepoints = values.ToArray();
        var types = properties.Select(x => x.Bidi).ToArray();
        var resolved = PdfBidiAlgorithm.Resolve(codepoints, types, direction, data, context);
        var scalars = new PdfUnicodeScalar[values.Count];
        for (int i = 0; i < scalars.Length; i++)
            scalars[i] = new PdfUnicodeScalar(values[i], offsets[i], values[i] > 65535 ? 2 : 1, properties[i].Bidi, properties[i].Script, resolved.Levels[i], properties[i].Ignorable);
        var clusters = PdfGraphemeBreaker.Break(scalars, properties, context);
        return new PdfUnicodeAnalysis(text, scalars, clusters, resolved.Paragraphs, resolved.Visual);
    }
}
