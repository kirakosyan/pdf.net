using System.IO;

namespace Fluent.Pdf;
/// <summary>An explicit optional headless renderer. Implementations remain responsible for declaring their supported content profile.</summary>
public interface IPdfPageRenderer : IPdfProvider
{
    /// <summary>Paints exactly request.ByteLength top-down, tightly packed, straight-alpha BGRA bytes to a borrowed output. Use the mandatory context for all nested work and isolated native execution.</summary>
    void Render(PdfRasterRequest request, Stream bgraOutput, PdfOperationContext context);
}
