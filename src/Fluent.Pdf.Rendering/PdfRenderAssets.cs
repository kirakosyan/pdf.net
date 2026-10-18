using Fluent.Pdf.Workers;

namespace Fluent.Pdf.Rendering;
internal static class PdfRenderAssets
{
    // Exact unchanged candidate assets from the independently inventoried container, not an engine's PDF file-version API.
    internal static void Validate(NativeWorkerOptions worker)
    {
        string rid = worker.Deployment.RuntimeIdentifier;
        (long size, string hash) expected = rid switch
        {
            "win-x64" => (7260672, "fb898a1f5ace57805834f390407500bdb6ef93eff326a252ad334a8aae809d8e"),
            "win-arm64" => (6705152, "8644d3b7e3d8999cef35de74ba8172ac0aeac75b90e6556b62e4dfadac2ca815"),
            "linux-x64" => (7669256, "224f8ece41f7e35891f11c10073b7b7062d7a18e9ef870586162a85c46130f7d"),
            "linux-arm64" => (7867192, "95a4d8cde3500f57f486478d27795d411b531a14712df08379f4793538a24a88"),
            "osx-x64" => (7490088, "bbd84063a8d475c4b433056cce13451c65c2ef41e643cccbf22ba9dcc220a90d"),
            "osx-arm64" => (7191008, "33c98063af28c0b7cbf8227f4422bf5c15942df2455cf7f0a5dce3dc601d52b0"),
            _ => throw new ArgumentException("This candidate renderer requires an inventoried 64-bit OS worker.", nameof(worker))};
        string name = rid.StartsWith("win-", StringComparison.Ordinal) ? "pdfium.dll" : rid.StartsWith("osx-", StringComparison.Ordinal) ? "libpdfium.dylib" : "libpdfium.so";
        if (!worker.Deployment.Assets.Any(a => a.FileName == name && a.IsNativeBinary && a.Length == expected.size && a.Sha256 == expected.hash))
            throw new ArgumentException("The rendering deployment must pin the exact unchanged candidate engine asset, including its length and SHA-256.", nameof(worker));
    }
}
