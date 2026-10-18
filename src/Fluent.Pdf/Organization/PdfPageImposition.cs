using System.Threading;

namespace Fluent.Pdf;
/// <summary>Managed vector N-up, booklet and tiled page imposition with explicit sheet geometry.</summary>
public static class PdfPageImposition
{
    /// <summary>Fits source pages into physical row-major sheet cells. Logical structure remains in source reading order.</summary>
    public static PdfDocument NUp(PdfDocumentSnapshot source, PdfPageSize sheet, int columns = 2, int rows = 1, PdfMargins? margins = null, double gap = 0, PdfCanvasFit fit = PdfCanvasFit.Contain, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Run(source, sheet, columns, rows, margins, gap, fit, false, false, 0, options, policy, cancellationToken);
    /// <summary>Pads to a multiple of four and places outer/inner front and back pairs for a left-bound booklet.</summary>
    public static PdfDocument Booklet(PdfDocumentSnapshot source, PdfPageSize sheet, PdfMargins? margins = null, double gap = 0, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Run(source, sheet, 2, 1, margins, gap, PdfCanvasFit.Contain, true, false, 0, options, policy, cancellationToken);
    /// <summary>Tiles displayed source pages at physical size with explicit overlap. Interactive or structured duplicates require an additional copy profile.</summary>
    public static PdfDocument Tile(PdfDocumentSnapshot source, PdfPageSize sheet, PdfMargins? margins = null, double overlap = 0, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Run(source, sheet, 1, 1, margins, 0, PdfCanvasFit.Contain, false, true, overlap, options, policy, cancellationToken);
    private static PdfDocument Run(PdfDocumentSnapshot source, PdfPageSize sheet, int columns, int rows, PdfMargins? margins, double gap, PdfCanvasFit fit, bool booklet, bool tile, double overlap, PdfOrganizationOptions? options, PdfOperationPolicy? policy, CancellationToken token)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        Guard.Size(sheet, nameof(sheet));
        if (columns < 1 || rows < 1 || columns > 16 || rows > 16)
            throw new ArgumentOutOfRangeException(nameof(columns));
        PdfGraphicsNumbers.Finite(gap);
        PdfGraphicsNumbers.Finite(overlap);
        if (gap < 0 || overlap < 0 || !Enum.IsDefined(typeof(PdfCanvasFit), fit))
            throw new ArgumentOutOfRangeException(nameof(gap));
        options ??= new();
        var padding = margins ?? PdfMargins.Uniform(0);
        PdfDocument? result = null;
        try
        {
            var report = PdfFont.Run("ImposePages", "managed.organization", policy, token, context =>
            {
                using var normalized = PdfPageOrganization.Normalize(source, options, context);
                var placements = new List<PdfImposedPage>();
                double w = (sheet.Width - padding.Left - padding.Right - gap * (columns - 1)) / columns;
                double h = (sheet.Height - padding.Top - padding.Bottom - gap * (rows - 1)) / rows;
                if (w <= 0 || h <= 0 || overlap >= w || overlap >= h)
                    throw PdfWriterCore.Error(context, "Organization.SheetFrame", "Sheet margins, gaps and overlap must leave positive finite cells.");
                void Add(int page, int output, PdfRect clip, PdfMatrix matrix)
                {
                    if (output >= options.MaximumPages)
                        throw PdfWriterCore.Error(context, "Organization.PageLimit", "Imposition exceeds the finite output page ceiling.");
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.Objects, 1);
                    context.Consume(PdfResourceKind.ExpandedBytes, 256);
                    placements.Add(new(page, output, clip, matrix));
                }

                if (tile)
                {
                    for (int p = 0; p < normalized.Pages.Count; p++)
                    {
                        var geometry = normalized.Pages[p].Geometry;
                        var bounds = geometry.DisplayBounds();
                        int across = Count(bounds.Width, w), down = Count(bounds.Height, h);
                        long needed = (long)across * down;
                        int first = placements.Count == 0 ? 0 : placements[placements.Count - 1].Sheet + 1;
                        if (needed > options.MaximumPages - first)
                            throw PdfWriterCore.Error(context, "Organization.PageLimit", "Tiling exceeds the finite output page ceiling.");
                        for (int y = 0; y < down; y++)
                            for (int x = 0; x < across; x++)
                                Add(p, first + y * across + x, new(padding.Left, sheet.Height - padding.Top - h, w, h), new PdfMatrix(1, 0, 0, -1, padding.Left - x * (w - overlap), sheet.Height - padding.Top + y * (h - overlap)).Multiply(geometry.UserToDisplay()));
                    }

                    int Count(double size, double cell)
                    {
                        double value = Math.Max(1, Math.Ceiling((size - overlap) / (cell - overlap)));
                        if (value > options.MaximumPages)
                            throw PdfWriterCore.Error(context, "Organization.PageLimit", "Tiling exceeds the finite output page ceiling.");
                        return (int)value;
                    }
                }
                else
                {
                    int count = booklet ? checked((normalized.Pages.Count + 3) / 4 * 4) : normalized.Pages.Count;
                    int perSheet = columns * rows;
                    for (int slot = 0; slot < count; slot++)
                    {
                        int p = booklet ? (slot % 4) switch
                        {
                            0 => count - 1 - slot / 4 * 2,
                            1 => slot / 4 * 2,
                            2 => slot / 4 * 2 + 1,
                            _ => count - 2 - slot / 4 * 2
                        } : slot;
                        int output = slot / perSheet, cell = slot % perSheet;
                        double x = padding.Left + (cell % columns) * (w + gap), y = padding.Top + (cell / columns) * (h + gap);
                        if (p >= normalized.Pages.Count)
                        {
                            Add(-1, output, new(x, sheet.Height - y - h, w, h), PdfMatrix.Identity);
                            continue;
                        }

                        var geometry = normalized.Pages[p].Geometry;
                        var b = geometry.DisplayBounds();
                        if (b.Width <= 0 || b.Height <= 0)
                            throw PdfWriterCore.Error(context, "Organization.EmptyCrop", "An imposed source page requires a positive displayed crop.");
                        double sx = w / b.Width, sy = h / b.Height;
                        if (fit != PdfCanvasFit.Stretch)
                            sx = sy = fit == PdfCanvasFit.Cover ? Math.Max(sx, sy) : Math.Min(sx, sy);
                        if (sx <= 0 || sy <= 0 || double.IsInfinity(sx) || double.IsInfinity(sy))
                            throw PdfWriterCore.Error(context, "Organization.TransformRange", "The requested fit cannot retain positive finite scale factors.");
                        double left = x + (w - b.Width * sx) / 2, top = y + (h - b.Height * sy) / 2;
                        Add(p, output, new(x, sheet.Height - y - h, w, h), new PdfMatrix(sx, 0, 0, -sy, left, sheet.Height - top).Multiply(geometry.UserToDisplay()));
                    }
                }

                result = PdfPageImposer.Write(normalized, sheet, placements, options, context);
            }, PdfCapability.WriteDocument);
            result!.SetCompositionReport(report);
            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }
}

internal readonly struct PdfImposedPage
{
    internal PdfImposedPage(int page, int sheet, PdfRect clip, PdfMatrix matrix)
    {
        Page = page;
        Sheet = sheet;
        Clip = clip;
        Matrix = matrix;
    }

    internal int Page { get; }
    internal int Sheet { get; }
    internal PdfRect Clip { get; }
    internal PdfMatrix Matrix { get; }
}
