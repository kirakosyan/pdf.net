namespace Fluent.Pdf;
/// <summary>Explicit bounded vertical-curvature dewarping. Control columns have fixed increasing source X; source Y increases down each column, preventing folds.</summary>
public sealed class PdfScanDewarpGrid
{
    /// <summary>Captures2–64 control columns/rows over a regular output grid. Source points are row-major edge coordinates within the exact original raster. No curve/model is inferred.</summary>
    public PdfScanDewarpGrid(int sourceWidth, int sourceHeight, int outputWidth, int outputHeight, int columns, int rows, PdfPoint[] sourcePoints)
    {
        if (sourceWidth < 1 || sourceHeight < 1 || outputWidth < 1 || outputHeight < 1 || sourceWidth > 100000 || sourceHeight > 100000 || outputWidth > 100000 || outputHeight > 100000 || columns < 2 || rows < 2 || columns > 64 || rows > 64)
            throw new ArgumentOutOfRangeException(nameof(columns));
        if (sourcePoints == null || sourcePoints.Length != columns * rows)
            throw new ArgumentException("An exact finite row-major control grid is required.", nameof(sourcePoints));
        for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                var point = sourcePoints[y * columns + x];
                if (point.X < 0 || point.Y < 0 || point.X > sourceWidth || point.Y > sourceHeight || x != 0 && point.X <= sourcePoints[y * columns + x - 1].X || y != 0 && (point.X != sourcePoints[x].X || point.Y <= sourcePoints[(y - 1) * columns + x].Y))
                    throw new ArgumentException("Dewarp columns must have fixed strictly increasing X and nonfolding strictly increasing Y within the original raster.", nameof(sourcePoints));
            }

        SourceWidth = sourceWidth;
        SourceHeight = sourceHeight;
        OutputWidth = outputWidth;
        OutputHeight = outputHeight;
        Columns = columns;
        Rows = rows;
        SourcePoints = Array.AsReadOnly((PdfPoint[])sourcePoints.Clone());
    }

    /// <summary>Gets exact original source width.</summary>
    public int SourceWidth { get; }
    /// <summary>Gets exact original source height.</summary>
    public int SourceHeight { get; }
    /// <summary>Gets corrected output width.</summary>
    public int OutputWidth { get; }
    /// <summary>Gets corrected output height.</summary>
    public int OutputHeight { get; }
    /// <summary>Gets control-column count.</summary>
    public int Columns { get; }
    /// <summary>Gets control-row count.</summary>
    public int Rows { get; }
    /// <summary>Gets immutable row-major original-raster edge coordinates.</summary>
    public IReadOnlyList<PdfPoint> SourcePoints { get; }

    /// <summary>Maps a point in the explicit output extent to original source coordinates by bilinear interpolation.</summary>
    public PdfPoint MapPoint(PdfPoint point)
    {
        if (point.X < 0 || point.Y < 0 || point.X > OutputWidth || point.Y > OutputHeight)
            throw new ArgumentOutOfRangeException(nameof(point));
        double x = point.X * (Columns - 1) / OutputWidth, y = point.Y * (Rows - 1) / OutputHeight;
        int col = Math.Min((int)x, Columns - 2), row = Math.Min((int)y, Rows - 2);
        x -= col;
        y -= row;
        var a = SourcePoints[row * Columns + col];
        var b = SourcePoints[row * Columns + col + 1];
        var d = SourcePoints[(row + 1) * Columns + col];
        var e = SourcePoints[(row + 1) * Columns + col + 1];
        return new((a.X * (1 - x) + b.X * x) * (1 - y) + (d.X * (1 - x) + e.X * x) * y, (a.Y * (1 - x) + b.Y * x) * (1 - y) + (d.Y * (1 - x) + e.Y * x) * y);
    }
}
