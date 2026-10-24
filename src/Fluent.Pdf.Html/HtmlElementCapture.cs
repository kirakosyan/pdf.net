using System.IO;
using System.Text;

namespace Fluent.Pdf.Html;

/// <summary>Explicit unique CSS selector or ordinal literal text-node search in the main light DOM, with finite traversal, pixel and output bounds.</summary>
public sealed class HtmlElementRequest
{
    /// <summary>Requires exactly one selector or literal text. Markup is private, active source data, returned only when explicitly requested; it is not a sanitized or standalone document.</summary>
    public HtmlElementRequest(string? selector = null, string? literalText = null, int maximumNodes = 10000, long maximumPixels = 16777216, int maximumPngBytes = 8388608, int maximumMarkupCharacters = 0)
    {
        if ((selector == null) == (literalText == null)) throw new ArgumentException("Exactly one selector or literal text search is required.");
        string value = selector ?? literalText!;
        if (value.Length == 0 || value.Length > 2048 || value.IndexOf('\0') >= 0) throw new ArgumentException("A bounded nonempty selection is required.");
        try { _ = new UTF8Encoding(false, true).GetByteCount(value); } catch (EncoderFallbackException) { throw new ArgumentException("Selection text must contain valid Unicode."); }
        if (maximumNodes < 1 || maximumNodes > 100000) throw new ArgumentOutOfRangeException(nameof(maximumNodes));
        if (maximumPixels < 1 || maximumPixels > 67108864) throw new ArgumentOutOfRangeException(nameof(maximumPixels));
        if (maximumPngBytes < 64 || maximumPngBytes > 8388608) throw new ArgumentOutOfRangeException(nameof(maximumPngBytes));
        if (maximumMarkupCharacters < 0 || maximumMarkupCharacters > 1048576) throw new ArgumentOutOfRangeException(nameof(maximumMarkupCharacters));
        Selector = selector; LiteralText = literalText; MaximumNodes = maximumNodes; MaximumPixels = maximumPixels; MaximumPngBytes = maximumPngBytes; MaximumMarkupCharacters = maximumMarkupCharacters;
    }
    /// <summary>Gets the unique main-document CSS selector, or null for literal text search.</summary>
    public string? Selector { get; }
    /// <summary>Gets the ordinal substring matched within individual text nodes, selecting their unique parent element; scripts/styles are excluded.</summary>
    public string? LiteralText { get; }
    /// <summary>Gets the maximum visited light-DOM element/text nodes.</summary>
    public int MaximumNodes { get; }
    /// <summary>Gets the maximum rounded CSS capture area at fixed device scale one.</summary>
    public long MaximumPixels { get; }
    /// <summary>Gets the maximum accepted encoded PNG extent.</summary>
    public int MaximumPngBytes { get; }
    /// <summary>Gets the explicit outer-markup character ceiling; zero disables markup extraction.</summary>
    public int MaximumMarkupCharacters { get; }
}

/// <summary>A CSS rectangle with top-left origin and downward positive Y; values are CSS pixels, never PDF points.</summary>
public sealed class HtmlElementRectangle
{
    internal HtmlElementRectangle(double x, double y, double width, double height) { X = x; Y = y; Width = width; Height = height; }
    /// <summary>Gets its left position in the declared CSS coordinate space.</summary>
    public double X { get; }
    /// <summary>Gets its top position in the declared CSS coordinate space.</summary>
    public double Y { get; }
    /// <summary>Gets its nonnegative CSS width.</summary>
    public double Width { get; }
    /// <summary>Gets its nonnegative CSS height.</summary>
    public double Height { get; }
}

/// <summary>A detached painted-page bounding-box capture. Ancestor clipping and overlapping page content remain visible; this does not isolate or reflow an element.</summary>
public sealed class HtmlElementCapture
{
    private readonly byte[] _png;
    internal HtmlElementCapture(byte[] png, HtmlElementRectangle viewport, HtmlElementRectangle document, HtmlElementRectangle clip, string? markup, Uri address)
    { _png = png; ViewportBounds = viewport; DocumentBounds = document; CaptureBounds = clip; Markup = markup; BaseAddress = address; }
    /// <summary>Gets the transformed element border bounding rectangle relative to the unchanged CSS viewport.</summary>
    public HtmlElementRectangle ViewportBounds { get; }
    /// <summary>Gets the transformed element border bounding rectangle relative to the document origin.</summary>
    public HtmlElementRectangle DocumentBounds { get; }
    /// <summary>Gets the outward-integer-rounded rectangle clipped to the document surface; PNG dimensions equal this extent at device scale one.</summary>
    public HtmlElementRectangle CaptureBounds { get; }
    /// <summary>Gets optional original active outer markup. Treat it as private untrusted source; it is not a resource bundle or sanitized HTML.</summary>
    public string? Markup { get; }
    /// <summary>Gets the final private document address for relative resource interpretation; credentials are never embedded by the SDK.</summary>
    public Uri BaseAddress { get; }
    /// <summary>Gets the successful shared-budget operation report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;
    /// <summary>Opens immutable exact PNG bytes in a fresh read-only stream owned by the caller.</summary>
    public Stream OpenPng() => new MemoryStream(_png, false);
}
