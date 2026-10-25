namespace Fluent.Pdf;

/// <summary>A named service advertised by an explicitly registered provider; this is not production qualification.</summary>
public enum PdfCapability
{
    /// <summary>Read a PDF into the managed document model.</summary>
    ReadDocument = 0,
    /// <summary>Write the managed document model as PDF.</summary>
    WriteDocument = 1,
    /// <summary>Render PDF pages; the rendering operation contract is defined by its owning requirement.</summary>
    RenderPages = 2,
    /// <summary>Render HTML; the operation contract is defined by its owning requirement.</summary>
    RenderHtml = 3,
    /// <summary>Recognize text; the operation contract is defined by its owning requirement.</summary>
    RecognizeText = 4,
    /// <summary>Explicit optional image stream codecs; algorithm/direction support is reported by their provider.</summary>
    ImageCodecs = 5,
    /// <summary>Explicit offline font parsing, embedding and standard-font metrics.</summary>
    Fonts = 6,
    /// <summary>Explicit Unicode-aware glyph shaping with logical cluster provenance.</summary>
    ShapeText = 7,
    /// <summary>Explicit offline CMS seed envelopes for certificate encryption.</summary>
    CmsEnvelopes = 8,
    /// <summary>Explicit complete independent PDF/A profile validation.</summary>
    ValidateArchival = 9,
    /// <summary>Explicit optional Word interchange with direction, reconstruction and fidelity diagnostics.</summary>
    ConvertWord = 10,
    /// <summary>Explicit complete independent PDF/UA validation, separate from human semantic review.</summary>
    ValidateAccessibility = 11,
    /// <summary>Explicit detached CMS signing and cryptographic integrity verification; trust is separate.</summary>
    Signatures = 12,
    /// <summary>Explicit signer identity, evaluation-time and revocation decisions.</summary>
    SignatureTrust = 13,
    /// <summary>Explicit incremental PAdES producer.</summary>
    Pades = 14,
    /// <summary>Complete independent exact-byte PAdES validation.</summary>
    ValidatePades = 15,
    /// <summary>Explicit ICC/overprint/spot-aware page rasterization.</summary>
    RenderColorManaged = 16,
    /// <summary>Explicit printer selection, one spool submission and job observations.</summary>
    PrintDocument = 17,
    /// <summary>Explicit optional multi-frame raster-file decoding.</summary>
    DecodeRasterDocuments = 18,
    /// <summary>Explicit independent barcode decoding and document symbol workflows.</summary>
    Barcodes = 19
}
