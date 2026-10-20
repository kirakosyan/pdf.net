namespace Fluent.Pdf;
/// <summary>The explicit policy for preserved source developer extensions.</summary>
public enum PdfUnknownExtensionPolicy
{
    /// <summary>Preserve opaque extension dictionaries and require a supported declared base version.</summary>
    Preserve,
    /// <summary>Reject developer extensions that the caller did not explicitly declare.</summary>
    Reject
}

/// <summary>A caller-declared developer extension requirement; this declares a format dependency without implementing its semantics.</summary>
public sealed class PdfExtensionRequirement
{
    /// <summary>Creates a vendor prefix, base PDF version and positive extension level.</summary>
    public PdfExtensionRequirement(string prefix, string baseVersion, int extensionLevel)
    {
        if (prefix == "Type" || string.IsNullOrEmpty(prefix) || prefix.Length > 127 || prefix.Any(c => c < 33 || c > 126 || "()<>[]{}/%#".IndexOf(c) >= 0))
            throw new ArgumentException("A nonempty regular ASCII developer prefix is required.", nameof(prefix));
        PdfVersionNumber.Parse(baseVersion);
        if (extensionLevel <= 0)
            throw new ArgumentOutOfRangeException(nameof(extensionLevel));
        Prefix = prefix;
        BaseVersion = baseVersion;
        ExtensionLevel = extensionLevel;
    }

    /// <summary>Creates a PDF 2.0 developer declaration with an explicit passive documentation URL and optional revision text. No URL is fetched.</summary>
    public PdfExtensionRequirement(string prefix, string baseVersion, int extensionLevel, string documentationUrl, string? revision = null) : this(prefix, baseVersion, extensionLevel)
    {
        if (string.IsNullOrWhiteSpace(documentationUrl) || documentationUrl.Length > 4096 || documentationUrl.Any(c => c < 33 || c > 126) || !Uri.TryCreate(documentationUrl, UriKind.Absolute, out _))
            throw new ArgumentException("A bounded absolute ASCII documentation URL is required; percent-encode non-ASCII characters.", nameof(documentationUrl));
        if (revision != null && revision.Length > 1024)
            throw new ArgumentException("A bounded revision string is required.", nameof(revision));
        DocumentationUrl = documentationUrl;
        Revision = revision;
    }

    /// <summary>Gets the explicit passive documentation URL; null on a legacy declaration.</summary>
    public string? DocumentationUrl { get; }
    /// <summary>Gets explicit extension revision text.</summary>
    public string? Revision { get; }
    /// <summary>Gets the developer prefix.</summary>
    public string Prefix { get; }
    /// <summary>Gets the required base PDF version.</summary>
    public string BaseVersion { get; }
    /// <summary>Gets the required developer extension level.</summary>
    public int ExtensionLevel { get; }
}
