namespace Fluent.Pdf;
/// <summary>Equivalent versioned business-observation serializations.</summary>
public enum PdfBusinessDataFormat
{
    /// <summary>UTF-8 JSON matching the version-one business schema.</summary>
    Json,
    /// <summary>UTF-8 XML in urn:fluent-pdf:business:1, with explicit scalar/array types.</summary>
    Xml
}

/// <summary>Explicit extraction scope and finite export limits; observations never establish business correctness.</summary>
public sealed class PdfBusinessDataOptions
{
    /// <summary>Captures schema1, digital text, optional table inference, passive fields/structure and optional separate page OCR. OCR requires both explicit rendering and recognition configuration. Table continuation/header aggregation requires a separate profile.</summary>
    public PdfBusinessDataOptions(int schemaVersion = 1, PdfTextExtractionOptions? text = null, PdfTableExtractionOptions? tables = null, bool includeFields = true, PdfAccessibilityProfile? structureProfile = null, PdfOcrOptions? recognition = null, PdfRenderOptions? rendering = null, int maximumRecords = 100000, long maximumOutputBytes = 16777216)
    {
        if (schemaVersion != 1) throw new ArgumentOutOfRangeException(nameof(schemaVersion), "Only exact schema version1 is supported.");
        if (maximumRecords < 1 || maximumRecords > 1000000 || maximumOutputBytes < 1 || maximumOutputBytes > 67108864) throw new ArgumentOutOfRangeException(nameof(maximumRecords));
        if (tables != null && (tables.JoinConsecutivePages || tables.HeaderRows != 0)) throw new ArgumentException("Business schema1 retains per-page tables without header/continuation aggregation.", nameof(tables));
        if (structureProfile.HasValue) PdfStructureRules.Profile(structureProfile.Value);
        if ((recognition == null) != (rendering == null)) throw new ArgumentException("Explicit OCR requires both recognition and rendering parameters.");
        if (rendering != null && (rendering.DpiX != rendering.DpiY || rendering.DpiX != Math.Floor(rendering.DpiX) || rendering.DpiX < 72 || rendering.DpiX > 600 || !rendering.BackgroundRgb.HasValue)) throw new ArgumentException("OCR requires opaque isotropic integer DPI72–600 rendering.", nameof(rendering));
        SchemaVersion = schemaVersion; Text = text ?? new(); Tables = tables; IncludeFields = includeFields; StructureProfile = structureProfile;
        Recognition = recognition; Rendering = rendering; MaximumRecords = maximumRecords; MaximumOutputBytes = maximumOutputBytes;
    }
    /// <summary>Gets exact schema version1; unknown versions reject.</summary>
    public int SchemaVersion { get; }
    /// <summary>Gets explicit digital text selection/grouping parameters.</summary>
    public PdfTextExtractionOptions Text { get; }
    /// <summary>Gets optional per-page table inference and explicit numeric culture.</summary>
    public PdfTableExtractionOptions? Tables { get; }
    /// <summary>Gets whether passive declared form fields and widget references are exported.</summary>
    public bool IncludeFields { get; }
    /// <summary>Gets optional passive structural inspection profile; it is not conformance validation.</summary>
    public PdfAccessibilityProfile? StructureProfile { get; }
    /// <summary>Gets optional explicitly configured OCR, exported separately from digital text.</summary>
    public PdfOcrOptions? Recognition { get; }
    /// <summary>Gets explicit OCR page-rendering choices.</summary>
    public PdfRenderOptions? Rendering { get; }
    /// <summary>Gets total retained observation-record ceiling.</summary>
    public int MaximumRecords { get; }
    /// <summary>Gets maximum staged serialized byte count.</summary>
    public long MaximumOutputBytes { get; }
}
