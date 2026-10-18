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
    ImageCodecs = 5
}
