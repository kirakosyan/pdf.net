namespace Fluent.Pdf;
// Original generated matrix/shaper profiles; construction facts and exact hashes are retained beside the data.
internal static class PdfStandardImageProfiles
{
    internal static PdfColorSpace Srgb(bool gray, PdfOperationContext context)
    {
        using var input = typeof(PdfImages).Assembly.GetManifestResourceStream("Fluent.Pdf.Images.Data.standard-" + (gray ? "gray" : "rgb") + "-v2.icc") ?? throw new InvalidOperationException("The original standard image profile resource is missing.");
        using var lease = context.BorrowExpandedInput(input);
        return PdfColorSpace.IccCore(PdfByteStore.Read(lease, context), null, context);
    }
}
