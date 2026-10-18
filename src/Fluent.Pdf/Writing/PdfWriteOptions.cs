namespace Fluent.Pdf;
/// <summary>The physical revision strategy for a save.</summary>
public enum PdfWriteMode
{
    /// <summary>Write a new complete file, preserving active object identities.</summary>
    Full,
    /// <summary>Copy the original bytes exactly and append changed objects and a cross-reference revision.</summary>
    Incremental
}

/// <summary>The representation of newly generated cross references.</summary>
public enum PdfCrossReferenceFormat
{
    /// <summary>Select a table within the configured offset/object ceilings, otherwise use a stream.</summary>
    Auto,
    /// <summary>Use classic fixed-width table entries.</summary>
    Table,
    /// <summary>Use a PDF 1.5-or-newer cross-reference stream with 64-bit offsets.</summary>
    Stream
}

/// <summary>Immutable save choices. Signature validation/signing are separate provider capabilities.</summary>
public sealed class PdfWriteOptions
{
    /// <summary>Captures revision/xref choices, an optional exact target version and explicit permission to invalidate signatures during a full rewrite.</summary>
    public PdfWriteOptions(PdfWriteMode mode = PdfWriteMode.Full, PdfCrossReferenceFormat crossReferenceFormat = PdfCrossReferenceFormat.Auto, string? version = null, bool allowSignatureInvalidation = false)
    {
        if (!Enum.IsDefined(typeof(PdfWriteMode), mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (!Enum.IsDefined(typeof(PdfCrossReferenceFormat), crossReferenceFormat))
            throw new ArgumentOutOfRangeException(nameof(crossReferenceFormat));
        if (version != null)
            PdfVersionNumber.Parse(version);
        Mode = mode;
        CrossReferenceFormat = crossReferenceFormat;
        Version = version;
        AllowSignatureInvalidation = allowSignatureInvalidation;
    }

    /// <summary>Gets shared full-rewrite defaults.</summary>
    public static PdfWriteOptions Default { get; } = new();
    /// <summary>Gets the physical revision strategy.</summary>
    public PdfWriteMode Mode { get; }
    /// <summary>Gets the representation of newly generated cross references.</summary>
    public PdfCrossReferenceFormat CrossReferenceFormat { get; }
    /// <summary>Gets an exact target version, or null to retain/negotiate the source version.</summary>
    public string? Version { get; }
    /// <summary>Gets explicit permission for a full rewrite of a document containing signature dictionaries.</summary>
    public bool AllowSignatureInvalidation { get; }
    internal bool IsDefault => Mode == PdfWriteMode.Full && CrossReferenceFormat == PdfCrossReferenceFormat.Auto && Version == null && !AllowSignatureInvalidation;
}

internal static class PdfVersionNumber
{
    internal static int Parse(string version) => version switch
    {
        "1.2" => 12,
        "1.3" => 13,
        "1.4" => 14,
        "1.5" => 15,
        "1.6" => 16,
        "1.7" => 17,
        "2.0" => 20,
        _ => throw new ArgumentException("A PDF version from 1.2 through 1.7, or 2.0, is required.", nameof(version))};
    internal static string Text(int version) => version == 20 ? "2.0" : "1." + (version - 10).ToString(System.Globalization.CultureInfo.InvariantCulture);
}
