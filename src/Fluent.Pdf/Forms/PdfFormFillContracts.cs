using System.Collections.ObjectModel;

namespace Fluent.Pdf;
/// <summary>Explicit offline text appearance resources and native-unit geometry choices.</summary>
public sealed class PdfFormTextStyle
{
    /// <summary>Captures fonts/shaper, positive font size and padding. Password auto-appearance requires an explicit one-scalar mask.</summary>
    public PdfFormTextStyle(PdfFontFallback fonts, PdfProviderRegistry providers, double fontSize = 12, double padding = 2, PdfTextOptions? textOptions = null, string? passwordMask = null)
    {
        Fonts = fonts ?? throw new ArgumentNullException(nameof(fonts));
        Providers = providers ?? throw new ArgumentNullException(nameof(providers));
        PdfGraphicsNumbers.Finite(fontSize);
        PdfGraphicsNumbers.Finite(padding);
        if (fontSize <= 0 || padding < 0)
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        if (passwordMask != null && !(passwordMask.Length == 1 && !char.IsSurrogate(passwordMask[0]) || passwordMask.Length == 2 && char.IsSurrogatePair(passwordMask, 0)))
            throw new ArgumentException("A password mask must be one Unicode scalar.", nameof(passwordMask));
        FontSize = fontSize;
        Padding = padding;
        TextOptions = textOptions ?? new();
        PasswordMask = passwordMask;
    }

    /// <summary>Gets immutable explicit font preference order.</summary>
    public PdfFontFallback Fonts { get; }
    /// <summary>Gets the explicitly provisioned shaping provider registry.</summary>
    public PdfProviderRegistry Providers { get; }
    /// <summary>Gets font size in native appearance units.</summary>
    public double FontSize { get; }
    /// <summary>Gets positive/zero padding in native appearance units.</summary>
    public double Padding { get; }
    /// <summary>Gets explicit Unicode shaping choices.</summary>
    public PdfTextOptions TextOptions { get; }
    /// <summary>Gets the explicit password presentation scalar, or null; stored field values remain unencrypted.</summary>
    public string? PasswordMask { get; }
}

/// <summary>An immutable value update with explicit appearance generation or exact per-widget private Forms.</summary>
public sealed class PdfFormUpdate
{
    /// <summary>Captures an exact qualified name/current value. Null defaultValue leaves DV unchanged; PDF null clears it. Variable text requires a style or a Form for every widget.</summary>
    public PdfFormUpdate(string name, PdfValue value, PdfFormTextStyle? textStyle = null, IReadOnlyDictionary<PdfObjectId, PdfGraphicsResource>? appearances = null, PdfValue? defaultValue = null)
    {
        Name = Guard.Text(name, nameof(name), 8192);
        Value = value ?? throw new ArgumentNullException(nameof(value));
        if (appearances?.Count > 4096)
            throw new ArgumentException("At most 4096 explicit widget appearances are supported.", nameof(appearances));
        var captured = new Dictionary<PdfObjectId, PdfGraphicsResource>();
        if (appearances != null)
            foreach (var pair in appearances)
                captured.Add(pair.Key, pair.Value ?? throw new ArgumentException("Appearances cannot be null.", nameof(appearances)));
        Appearances = new ReadOnlyDictionary<PdfObjectId, PdfGraphicsResource>(captured);
        TextStyle = textStyle;
        DefaultValue = defaultValue;
    }

    /// <summary>Gets the exact field name.</summary>
    public string Name { get; }
    /// <summary>Gets requested current data; a standard interchange text value is converted to a button export name using the target field kind.</summary>
    public PdfValue Value { get; }
    /// <summary>Gets optional shared-context text generation settings.</summary>
    public PdfFormTextStyle? TextStyle { get; }
    /// <summary>Gets detached normal Forms keyed by exact source widget identity.</summary>
    public IReadOnlyDictionary<PdfObjectId, PdfGraphicsResource> Appearances { get; }
    /// <summary>Gets an optional reset value update, or null to retain the source reset value.</summary>
    public PdfValue? DefaultValue { get; }
}
