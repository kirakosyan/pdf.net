using System.IO;

namespace Fluent.Pdf;
/// <summary>Optional immutable original OpenType source for shaping; copying does not grant embedding permission.</summary>
public interface IPdfFontShapingSource
{
    /// <summary>Gets the original collection face index.</summary>
    int ShapingFaceIndex { get; }

    /// <summary>Copies original bytes to the supplied bounded borrowed stream without modifying it or searching machine fonts.</summary>
    void CopyShapingSource(Stream output, PdfOperationContext context);
}

public sealed partial class PdfFont
{
    /// <summary>Copies original shaping bytes under the caller's shared operation context and returns the collection face index.</summary>
    public int CopyShapingSource(Stream output, PdfOperationContext context)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        if (Program is not IPdfFontShapingSource source)
            throw PdfWriterCore.Error(context, "Text.FontSourceUnavailable", "The selected face does not expose original OpenType shaping bytes.");
        using var lease = new BorrowedStream(output, false, context);
        source.CopyShapingSource(lease, context);
        context.Checkpoint();
        int face = source.ShapingFaceIndex;
        if (face < 0)
            throw PdfWriterCore.Error(context, "Text.FontSourceUnavailable", "The selected face returned an invalid collection index.");
        return face;
    }
}

/// <summary>Glyph progression for one fully itemized shaping run.</summary>
public enum PdfTextDirection
{
    /// <summary>Left to right.</summary>
    LeftToRight,
    /// <summary>Right to left.</summary>
    RightToLeft,
    /// <summary>Top to bottom using vertical OpenType substitutions and positioning.</summary>
    TopToBottom
}

/// <summary>One requested OpenType feature and its original UTF-16 range.</summary>
public readonly struct PdfTextFeature
{
    /// <summary>Creates a four-byte ASCII feature tag and a half-open logical UTF-16 range.</summary>
    public PdfTextFeature(string tag, uint value = 1, int start = 0, int end = int.MaxValue)
    {
        if (tag == null || tag.Length != 4 || tag.Any(c => c < 32 || c > 126))
            throw new ArgumentException("Four ASCII tag bytes are required.", nameof(tag));
        if (start < 0 || end < start)
            throw new ArgumentOutOfRangeException(nameof(start));
        Tag = tag;
        Value = value;
        Start = start;
        End = end;
    }

    /// <summary>Gets the OpenType tag.</summary>
    public string Tag { get; }
    /// <summary>Gets its value.</summary>
    public uint Value { get; }
    /// <summary>Gets its inclusive logical start.</summary>
    public int Start { get; }
    /// <summary>Gets its exclusive logical end.</summary>
    public int End { get; }
}

/// <summary>An immutable bounded shaping request with full paragraph context and an explicit selected face.</summary>
public sealed class PdfShapingRequest
{
    internal PdfShapingRequest(string text, int start, int length, PdfFont font, PdfTextDirection direction, uint script, string language, IReadOnlyList<PdfTextFeature> features)
    {
        Text = text;
        Start = start;
        Length = length;
        Font = font;
        Direction = direction;
        ScriptTag = script;
        Language = language;
        Features = features;
    }

    /// <summary>Gets unchanged full logical text, including context outside this run.</summary>
    public string Text { get; }
    /// <summary>Gets its inclusive UTF-16 run start.</summary>
    public int Start { get; }
    /// <summary>Gets its UTF-16 run length.</summary>
    public int Length { get; }
    /// <summary>Gets the exact selected immutable face.</summary>
    public PdfFont Font { get; }
    /// <summary>Gets glyph progression.</summary>
    public PdfTextDirection Direction { get; }
    /// <summary>Gets the ISO 15924 script tag in big-endian byte order.</summary>
    public uint ScriptTag { get; }
    /// <summary>Gets the explicit BCP 47 language, or und.</summary>
    public string Language { get; }
    /// <summary>Gets explicit feature overrides in caller order.</summary>
    public IReadOnlyList<PdfTextFeature> Features { get; }
}

/// <summary>One positioned glyph in font design units with unchanged logical UTF-16 cluster provenance.</summary>
public readonly struct PdfShapedGlyph
{
    /// <summary>Creates finite glyph geometry. The core validates glyph/cluster ranges against the request.</summary>
    public PdfShapedGlyph(int glyphIndex, int cluster, double advanceX, double advanceY, double offsetX = 0, double offsetY = 0)
    {
        if (glyphIndex < 0 || glyphIndex > 65535 || cluster < 0)
            throw new ArgumentOutOfRangeException(nameof(glyphIndex));
        if (double.IsNaN(advanceX) || double.IsInfinity(advanceX) || double.IsNaN(advanceY) || double.IsInfinity(advanceY) || double.IsNaN(offsetX) || double.IsInfinity(offsetX) || double.IsNaN(offsetY) || double.IsInfinity(offsetY))
            throw new ArgumentException("Finite glyph geometry is required.");
        GlyphIndex = glyphIndex;
        Cluster = cluster;
        AdvanceX = advanceX;
        AdvanceY = advanceY;
        OffsetX = offsetX;
        OffsetY = offsetY;
    }

    /// <summary>Gets the face's glyph identity.</summary>
    public int GlyphIndex { get; }
    /// <summary>Gets its original UTF-16 cluster offset.</summary>
    public int Cluster { get; }
    /// <summary>Gets horizontal progression.</summary>
    public double AdvanceX { get; }
    /// <summary>Gets vertical progression.</summary>
    public double AdvanceY { get; }
    /// <summary>Gets horizontal placement adjustment.</summary>
    public double OffsetX { get; }
    /// <summary>Gets vertical placement adjustment.</summary>
    public double OffsetY { get; }
}

/// <summary>An explicit optional shaper. Results use visual glyph order and monotone grapheme cluster offsets for each run.</summary>
public interface IPdfTextShaper : IPdfProvider
{
    /// <summary>Shapes the exact run; providers charge all allocations before native or managed processing and honor its shared context.</summary>
    IReadOnlyList<PdfShapedGlyph> Shape(PdfShapingRequest request, PdfOperationContext context);
}
