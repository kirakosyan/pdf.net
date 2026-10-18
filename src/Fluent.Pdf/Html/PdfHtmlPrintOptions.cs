namespace Fluent.Pdf;
/// <summary>Immutable browser print policy. Geometry is requested in PDF points; actual engine page boxes are retained.</summary>
public sealed class PdfHtmlPrintOptions
{
    /// <summary>Captures paper/CSS sizing, orientation, explicit scale, point margins, bounded resource-free header/footer fragments and optional structural tagging.</summary>
    public PdfHtmlPrintOptions(bool preferCssPageSize = false, bool landscape = false, double scale = 1, double marginTop = 0, double marginBottom = 0, double marginLeft = 0, double marginRight = 0, string? headerHtml = null, string? footerHtml = null, bool tagged = false)
    {
        if (double.IsNaN(scale) || double.IsInfinity(scale) || scale < 0.1 || scale > 2)
            throw new ArgumentOutOfRangeException(nameof(scale));
        foreach (double value in new[]
        {
            marginTop,
            marginBottom,
            marginLeft,
            marginRight
        }

        )
            if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > 14400)
                throw new ArgumentOutOfRangeException(nameof(marginTop), "Print margins must be finite nonnegative point values within the source extent.");
        ValidateFragment(headerHtml, nameof(headerHtml));
        ValidateFragment(footerHtml, nameof(footerHtml));
        if (headerHtml != null && marginTop == 0 || footerHtml != null && marginBottom == 0)
            throw new ArgumentException("An explicit positive top/bottom margin is required for the corresponding print template.");
        PreferCssPageSize = preferCssPageSize;
        Landscape = landscape;
        Scale = scale;
        MarginTop = marginTop;
        MarginBottom = marginBottom;
        MarginLeft = marginLeft;
        MarginRight = marginRight;
        HeaderHtml = headerHtml;
        FooterHtml = footerHtml;
        Tagged = tagged;
    }

    /// <summary>Gets the shared immutable default: requested paper, zero margins, scale1, no templates or tagged-output claim.</summary>
    public static PdfHtmlPrintOptions Default { get; } = new();
    /// <summary>Gets whether CSS @page size takes precedence. False asks the engine to fit CSS layout to the requested paper.</summary>
    public bool PreferCssPageSize { get; }
    /// <summary>Gets whether the browser rotates the requested base paper to landscape.</summary>
    public bool Landscape { get; }
    /// <summary>Gets the explicit browser content scale in0.1–2.</summary>
    public double Scale { get; }
    /// <summary>Gets the top margin in PDF points.</summary>
    public double MarginTop { get; }
    /// <summary>Gets the bottom margin in PDF points.</summary>
    public double MarginBottom { get; }
    /// <summary>Gets the left margin in PDF points.</summary>
    public double MarginLeft { get; }
    /// <summary>Gets the right margin in PDF points.</summary>
    public double MarginRight { get; }
    /// <summary>Gets a bounded resource-free HTML/XHTML header fragment, or null. The renderer validates the explicit supported fragment profile under the operation context.</summary>
    public string? HeaderHtml { get; }
    /// <summary>Gets the corresponding footer fragment, or null. Native pageNumber/totalPages classes refer to final printed pages.</summary>
    public string? FooterHtml { get; }
    /// <summary>Gets whether to request and structurally validate tagged output. This does not request or establish PDF/UA conformance.</summary>
    public bool Tagged { get; }

    private static void ValidateFragment(string? text, string parameter)
    {
        if (text == null)
            return;
        if (text.Length == 0 || text.Length > 65536)
            throw new ArgumentException("A nonempty bounded print fragment is required.", parameter);
        for (int i = 0; i < text.Length; i++)
            if (char.IsHighSurrogate(text[i]))
            {
                if (++i == text.Length || !char.IsLowSurrogate(text[i]))
                    throw new ArgumentException("The print fragment must contain well-formed Unicode.", parameter);
            }
            else if (char.IsLowSurrogate(text[i]) || text[i] == '\0')
                throw new ArgumentException("The print fragment must contain well-formed Unicode.", parameter);
    }
}
