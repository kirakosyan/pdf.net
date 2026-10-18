using System.IO;

namespace Fluent.Pdf;
/// <summary>The outline/container representation supplied by a font provider.</summary>
public enum PdfFontFormat
{
    /// <summary>A standard PDF font without an embedded program.</summary>
    Standard,
    /// <summary>TrueType quadratic outlines in an SFNT program.</summary>
    TrueType,
    /// <summary>Compact Font Format cubic outlines.</summary>
    Cff,
    /// <summary>An encrypted Type 1 PostScript program.</summary>
    Type1,
    /// <summary>Caller-authored PDF glyph procedures, represented by font resources rather than a binary font file.</summary>
    Type3
}

/// <summary>Embedding restrictions declared by the font's license metadata; zero means installable embedding.</summary>
[Flags]
public enum PdfFontEmbeddingRights
{
    /// <summary>No program-declared embedding restriction.</summary>
    Installable = 0,
    /// <summary>Restricted license embedding; embedding must fail.</summary>
    Restricted = 2,
    /// <summary>Preview and print embedding only.</summary>
    PreviewAndPrint = 4,
    /// <summary>Editable embedding.</summary>
    Editable = 8,
    /// <summary>Subsetting is forbidden.</summary>
    NoSubsetting = 256,
    /// <summary>Only bitmap embedding is allowed; outline embedding must fail.</summary>
    BitmapOnly = 512
}

/// <summary>Immutable font-unit metrics. Coordinates use the font's unscaled design space.</summary>
public readonly struct PdfFontMetrics
{
    /// <summary>Creates finite design-space metrics with positive units per em and an ordered bounding box.</summary>
    public PdfFontMetrics(int unitsPerEm, int ascender, int descender, int capHeight, int left, int bottom, int right, int top, bool fixedPitch = false, double italicAngle = 0)
    {
        if (unitsPerEm <= 0 || unitsPerEm > 16384)
            throw new ArgumentOutOfRangeException(nameof(unitsPerEm));
        if (right < left || top < bottom)
            throw new ArgumentException("The font bounding box must be ordered.");
        if (double.IsInfinity(italicAngle) || double.IsNaN(italicAngle))
            throw new ArgumentOutOfRangeException(nameof(italicAngle));
        UnitsPerEm = unitsPerEm;
        Ascender = ascender;
        Descender = descender;
        CapHeight = capHeight;
        Left = left;
        Bottom = bottom;
        Right = right;
        Top = top;
        FixedPitch = fixedPitch;
        ItalicAngle = italicAngle;
    }

    /// <summary>Gets design units per em.</summary>
    public int UnitsPerEm { get; }
    /// <summary>Gets the typographic ascender.</summary>
    public int Ascender { get; }
    /// <summary>Gets the typographic descender.</summary>
    public int Descender { get; }
    /// <summary>Gets the capital height.</summary>
    public int CapHeight { get; }
    /// <summary>Gets the font bounding-box left edge.</summary>
    public int Left { get; }
    /// <summary>Gets the font bounding-box bottom edge.</summary>
    public int Bottom { get; }
    /// <summary>Gets the font bounding-box right edge.</summary>
    public int Right { get; }
    /// <summary>Gets the font bounding-box top edge.</summary>
    public int Top { get; }
    /// <summary>Gets whether advances have fixed pitch.</summary>
    public bool FixedPitch { get; }
    /// <summary>Gets the italic angle in counterclockwise degrees from vertical.</summary>
    public double ItalicAngle { get; }
}

/// <summary>An immutable font identity and license/metric declaration from an explicitly selected provider.</summary>
public sealed class PdfFontInfo
{
    /// <summary>Captures the PostScript name, program identity, geometry and license flags.</summary>
    public PdfFontInfo(string postScriptName, PdfFontFormat format, int glyphCount, PdfFontMetrics metrics, string sha256, PdfFontEmbeddingRights embeddingRights = PdfFontEmbeddingRights.Installable, bool symbolic = false)
    {
        if (string.IsNullOrEmpty(postScriptName) || postScriptName.Length > 127 || postScriptName.Any(c => c < 33 || c > 126 || "()<>[]{}/%#".IndexOf(c) >= 0))
            throw new ArgumentException("A regular ASCII PostScript name is required.", nameof(postScriptName));
        if (!Enum.IsDefined(typeof(PdfFontFormat), format))
            throw new ArgumentOutOfRangeException(nameof(format));
        if (glyphCount < 1 || glyphCount > 65536)
            throw new ArgumentOutOfRangeException(nameof(glyphCount));
        if (metrics.UnitsPerEm <= 0)
            throw new ArgumentException("Initialized metrics are required.", nameof(metrics));
        if (sha256 == null || sha256.Length != 64 || sha256.Any(c => !(c >= '0' && c <= '9' || c >= 'a' && c <= 'f' || c >= 'A' && c <= 'F')))
            throw new ArgumentException("An exact SHA-256 is required.", nameof(sha256));
        if (((int)embeddingRights & ~782) != 0)
            throw new ArgumentOutOfRangeException(nameof(embeddingRights));
        PostScriptName = postScriptName;
        Format = format;
        GlyphCount = glyphCount;
        Metrics = metrics;
        Sha256 = sha256.ToLowerInvariant();
        EmbeddingRights = embeddingRights;
        Symbolic = symbolic;
    }

    /// <summary>Gets the program's PostScript name.</summary>
    public string PostScriptName { get; }
    /// <summary>Gets its outline format.</summary>
    public PdfFontFormat Format { get; }
    /// <summary>Gets the number of glyph identities, including the missing glyph at zero.</summary>
    public int GlyphCount { get; }
    /// <summary>Gets design-space geometry.</summary>
    public PdfFontMetrics Metrics { get; }
    /// <summary>Gets the source program/face identity hash.</summary>
    public string Sha256 { get; }
    /// <summary>Gets declared embedding restrictions.</summary>
    public PdfFontEmbeddingRights EmbeddingRights { get; }
    /// <summary>Gets whether the font uses a symbolic encoding.</summary>
    public bool Symbolic { get; }
}

/// <summary>A caller-owned immutable font face. Methods must honor the supplied operation context and never search machine fonts.</summary>
public interface IPdfFontProgram
{
    /// <summary>Gets immutable identity, rights and metrics.</summary>
    PdfFontInfo Info { get; }

    /// <summary>Maps one Unicode scalar to a glyph identity; zero means unavailable.</summary>
    int MapGlyph(int unicodeScalar, PdfOperationContext context);
    /// <summary>Gets a glyph's horizontal advance in design units.</summary>
    double GetAdvance(int glyph, PdfOperationContext context);
    /// <summary>Gets an exact glyph name for simple Type 1/CFF encodings.</summary>
    string GetGlyphName(int glyph, PdfOperationContext context);
    /// <summary>Writes a complete embeddable program, including composite dependencies for a requested subset. Retained glyph identities must remain stable.</summary>
    PdfFontProgramWriteResult Write(Stream output, IReadOnlyList<int> glyphs, bool subset, PdfOperationContext context);
}

/// <summary>Exact optional vertical design-space metrics for an explicitly supported face.</summary>
public readonly struct PdfFontVerticalMetrics
{
    /// <summary>Captures a positive vertical advance and finite vertical-origin coordinates.</summary>
    public PdfFontVerticalMetrics(double advance, double originX, double originY)
    {
        if (advance <= 0 || double.IsInfinity(advance) || double.IsNaN(advance) || double.IsInfinity(originX) || double.IsNaN(originX) || double.IsInfinity(originY) || double.IsNaN(originY))
            throw new ArgumentException("Vertical font metrics must be finite with a positive advance.");
        Advance = advance;
        OriginX = originX;
        OriginY = originY;
    }

    /// <summary>Gets the positive vertical advance.</summary>
    public double Advance { get; }
    /// <summary>Gets the vertical origin's horizontal coordinate.</summary>
    public double OriginX { get; }
    /// <summary>Gets the vertical origin's vertical coordinate.</summary>
    public double OriginY { get; }
}

/// <summary>An optional exact vertical metric capability; callers must check support for the selected face.</summary>
public interface IPdfVerticalFontProgram
{
    /// <summary>Gets whether the selected face supplies all required vertical metrics.</summary>
    bool SupportsVerticalMetrics { get; }

    /// <summary>Gets exact vertical advance and origin under the mandatory context.</summary>
    PdfFontVerticalMetrics GetVerticalMetrics(int glyph, PdfOperationContext context);
}

/// <summary>Format details for an eagerly written embedded font program.</summary>
public sealed class PdfFontProgramWriteResult
{
    /// <summary>Captures the emitted PostScript name, PDF font-file subtype and Type 1 section extents.</summary>
    public PdfFontProgramWriteResult(string postScriptName, string? fontFileSubtype = null, long length1 = 0, long length2 = 0, long length3 = 0, bool cidKeyed = false)
    {
        if (string.IsNullOrEmpty(postScriptName) || postScriptName.Length > 127 || postScriptName.Any(c => c < 33 || c > 126 || "()<>[]{}/%#".IndexOf(c) >= 0))
            throw new ArgumentException("A regular ASCII PostScript font name is required.", nameof(postScriptName));
        if (fontFileSubtype != null && fontFileSubtype != "Type1C" && fontFileSubtype != "CIDFontType0C" && fontFileSubtype != "OpenType")
            throw new ArgumentException("An applicable font-file subtype is required.", nameof(fontFileSubtype));
        if (length1 < 0 || length2 < 0 || length3 < 0)
            throw new ArgumentOutOfRangeException(nameof(length1));
        PostScriptName = postScriptName;
        FontFileSubtype = fontFileSubtype;
        Length1 = length1;
        Length2 = length2;
        Length3 = length3;
        CidKeyed = cidKeyed;
    }

    /// <summary>Gets the emitted program's font name.</summary>
    public string PostScriptName { get; }
    /// <summary>Gets the FontFile3 subtype, or null for a FontFile/FontFile2 program.</summary>
    public string? FontFileSubtype { get; }
    /// <summary>Gets the clear Type 1 prefix extent, or TrueType program length.</summary>
    public long Length1 { get; }
    /// <summary>Gets the encrypted Type 1 section extent.</summary>
    public long Length2 { get; }
    /// <summary>Gets the Type 1 trailer extent.</summary>
    public long Length3 { get; }
    /// <summary>Gets whether emitted CFF charsets use CID identities.</summary>
    public bool CidKeyed { get; }
}

/// <summary>Explicit offline font parsing and deterministic standard-font selection.</summary>
public interface IPdfFontProvider : IPdfProvider
{
    /// <summary>Reads one explicitly selected collection face from a borrowed stream under the mandatory context.</summary>
    IPdfFontProgram Read(Stream input, int faceIndex, PdfOperationContext context);
    /// <summary>Returns one exact base-14 PostScript font name, or rejects unsupported names.</summary>
    IPdfFontProgram Standard(string postScriptName, PdfOperationContext context);
}

/// <summary>Optional deterministic PDF simple-font encoding and glyph-name mapping; no machine encoding fallback is permitted.</summary>
public interface IPdfFontEncodingProvider
{
    /// <summary>Decodes a direct simple-font encoding and immutable bytes under the mandatory context. Unknown mappings must be reported.</summary>
    string DecodeSimple(PdfValue encoding, PdfValue encodedText, string baseFont, PdfOperationContext context);
}

/// <summary>Optional deterministic source-code advances for standard simple fonts without a Widths array.</summary>
public interface IPdfFontEncodingMetricsProvider
{
    /// <summary>Returns 256 advances in 1000-em units, indexed by encoded byte. Null denotes an unsupported glyph name; no machine fonts may be used.</summary>
    IReadOnlyList<double?> GetSimpleAdvances(PdfValue encoding, string baseFont, PdfOperationContext context);
}

/// <summary>Optional original CID-to-glyph lookup for a CID-keyed embedded font program without a Unicode cmap.</summary>
public interface IPdfCidFontProgram
{
    /// <summary>Maps a declared CID to a glyph identity, or zero when absent.</summary>
    int MapCid(int cid, PdfOperationContext context);
}
