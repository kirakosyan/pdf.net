using System.IO;

namespace Fluent.Pdf;
/// <summary>Detached immutable top-down straight-alpha BGRA samples. Transparent pixels have zero RGB.</summary>
public sealed class PdfRaster
{
    private readonly PdfByteStore _samples;
    internal PdfRaster(PdfByteStore samples, PdfRasterRequest request, PdfOperationReport report)
    {
        _samples = samples;
        Request = request;
        Report = report;
    }

    /// <summary>Gets exact pixel layout, effective DPI and snapped region.</summary>
    public PdfRasterRequest Request { get; }
    /// <summary>Gets operation diagnostics and resource accounting.</summary>
    public PdfOperationReport Report { get; }

    /// <summary>Opens an independent read-only sample view without copying the raster.</summary>
    public Stream OpenSamples() => _samples.Open(0, _samples.Length);
}
