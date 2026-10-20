using System.IO;

namespace Fluent.Pdf;
/// <summary>An explicitly selected Word interchange format; availability is provider-specific.</summary>
public enum PdfWordFormat
{
    /// <summary>Compound binary Word document.</summary>
    Doc,
    /// <summary>Open XML WordprocessingML package.</summary>
    Docx,
    /// <summary>Rich Text Format.</summary>
    Rtf
}

/// <summary>Explicit PDF reconstruction intent, independent of whether original authoring structure can be recovered.</summary>
public enum PdfWordReconstruction
{
    /// <summary>Preserve page geometry using an explicitly supplied renderer; rasterization and resulting semantic losses must be reported.</summary>
    FixedLayout,
    /// <summary>Reconstruct editable content with explicit recovered-text versus inferred-structure diagnostics.</summary>
    Editable
}

/// <summary>Immutable direction-independent limits and PDF reconstruction intent. Provider asset/font choices remain explicit provider configuration.</summary>
public sealed class PdfWordConversionOptions
{
    /// <summary>Captures bounded source/output/expansion/character/archive/page/depth limits in addition to the root policy. No resource, font or model is discovered.</summary>
    public PdfWordConversionOptions(PdfWordReconstruction reconstruction, int maximumInputBytes = 4194304, int maximumOutputBytes = 16777216, int maximumProviderExpandedBytes = 33554432, int maximumCharacters = 131072, int maximumArchiveEntries = 1024, int maximumPages = 1000, int maximumDepth = 32, double rasterDpi = 144)
    {
        if (!Enum.IsDefined(typeof(PdfWordReconstruction), reconstruction))
            throw new ArgumentOutOfRangeException(nameof(reconstruction));
        foreach (int value in new[]
        {
            maximumInputBytes,
            maximumOutputBytes,
            maximumProviderExpandedBytes,
            maximumCharacters,
            maximumArchiveEntries,
            maximumPages,
            maximumDepth
        }

        )
            if (value < 1)
                throw new ArgumentOutOfRangeException(nameof(maximumInputBytes));
        if (maximumInputBytes > 67108864 || maximumOutputBytes > 67108864 || maximumProviderExpandedBytes > 268435456 || maximumCharacters > 1000000 || maximumArchiveEntries > 100000 || maximumPages > 10000 || maximumDepth > 128)
            throw new ArgumentOutOfRangeException(nameof(maximumInputBytes));
        PdfGraphicsNumbers.Finite(rasterDpi);
        if (rasterDpi < 72 || rasterDpi > 600)
            throw new ArgumentOutOfRangeException(nameof(rasterDpi));
        Reconstruction = reconstruction;
        MaximumInputBytes = maximumInputBytes;
        MaximumOutputBytes = maximumOutputBytes;
        MaximumProviderExpandedBytes = maximumProviderExpandedBytes;
        MaximumCharacters = maximumCharacters;
        MaximumArchiveEntries = maximumArchiveEntries;
        MaximumPages = maximumPages;
        MaximumDepth = maximumDepth;
        RasterDpi = rasterDpi;
    }

    /// <summary>Gets the explicit PDF reconstruction intent.</summary>
    public PdfWordReconstruction Reconstruction { get; }
    /// <summary>Gets the source-byte ceiling.</summary>
    public int MaximumInputBytes { get; }
    /// <summary>Gets the converted-file ceiling.</summary>
    public int MaximumOutputBytes { get; }
    /// <summary>Gets one opaque provider phase's expansion envelope; each phase must charge it before entering the dependency.</summary>
    public int MaximumProviderExpandedBytes { get; }
    /// <summary>Gets the reconstructed/imported character ceiling.</summary>
    public int MaximumCharacters { get; }
    /// <summary>Gets the archive-entry ceiling.</summary>
    public int MaximumArchiveEntries { get; }
    /// <summary>Gets the physical page ceiling.</summary>
    public int MaximumPages { get; }
    /// <summary>Gets the dependency nesting ceiling, additionally bounded by root depth.</summary>
    public int MaximumDepth { get; }
    /// <summary>Gets explicitly selected fixed-layout raster DPI.</summary>
    public double RasterDpi { get; }
}

/// <summary>An explicit optional Word converter. Implementations retain borrowed stream ownership and use the mandatory context across every SDK/provider phase.</summary>
public interface IPdfWordConverter : IPdfProvider
{
    /// <summary>Reports exact direction/format availability without opening input or loading assets.</summary>
    bool Supports(PdfWordFormat format, bool import);
    /// <summary>Converts borrowed Word source to PDF. Do not execute source actions or discover resources/fonts/models. Report all losses through the supplied context.</summary>
    void Import(Stream source, PdfWordFormat format, Stream pdfOutput, PdfWordConversionOptions options, PdfOperationContext context);
    /// <summary>Converts a PDF snapshot to Word with explicit reconstruction, renderer/font/provider choices and loss/provenance diagnostics.</summary>
    void Export(PdfDocumentSnapshot source, PdfWordFormat format, Stream output, PdfWordConversionOptions options, PdfProviderRegistry providers, PdfOperationContext context);
}
