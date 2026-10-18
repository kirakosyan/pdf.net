namespace Fluent.Pdf;
internal readonly struct PdfTemplateLocator
{
    internal PdfTemplateLocator(string id, PdfRect bounds)
    {
        Id = id;
        Bounds = bounds;
    }

    internal string Id { get; }
    internal PdfRect Bounds { get; }
}
