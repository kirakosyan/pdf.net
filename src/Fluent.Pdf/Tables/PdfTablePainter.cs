namespace Fluent.Pdf;
internal static class PdfTablePainter
{
    internal static void Paint(PdfCanvas canvas, PdfTableFragment fragment, PdfOperationContext context)
    {
        var plan = fragment.Plan;
        PaintRange(0, plan.HeaderHeight, fragment.Bounds.Y);
        if (fragment.NestedHeaderBandHeight > 0)
        {
            double top = fragment.Bounds.Y + plan.HeaderHeight;
            using (canvas.Save())
            {
                canvas.Clip(PdfPath.RectangleCore(new(fragment.Bounds.X, top, plan.Width, fragment.NestedHeaderBandHeight), context));
                foreach (var cell in plan.Cells)
                {
                    if (cell.Bounds.Y > fragment.BodyStart || cell.Bounds.Top <= fragment.BodyStart)
                        continue;
                    var style = cell.Grid.Cell.Style;
                    var band = new PdfRect(fragment.Bounds.X + cell.Bounds.X, top, cell.Bounds.Width, fragment.NestedHeaderBandHeight);
                    if (style.Background != null)
                        Fill(band, style.Background);
                    Edge(new(band.X, band.Y, Math.Min(style.Left.Width, band.Width), band.Height), style.Left);
                    Edge(new(band.Right - Math.Min(style.Right.Width, band.Width), band.Y, Math.Min(style.Right.Width, band.Width), band.Height), style.Right);
                    if (cell.Bounds.Y == fragment.BodyStart)
                        Edge(new(band.X, band.Y, band.Width, Math.Min(style.Top.Width, band.Height)), style.Top);
                }

                foreach (var repeat in fragment.NestedHeaders)
                {
                    foreach (var source in repeat.Source.Sources)
                        canvas.CopyPlacement(source, new(1, 0, 0, 1, repeat.Bounds.X - repeat.Source.SourceBounds.X, repeat.Bounds.Y - repeat.Source.SourceBounds.Y));
                }
            }
        }

        PaintRange(fragment.BodyStart, fragment.BodyEnd, fragment.Bounds.Y + plan.HeaderHeight + fragment.NestedHeaderBandHeight);
        void PaintRange(double from, double to, double targetY)
        {
            if (to <= from)
                return;
            using (canvas.Save())
            {
                canvas.Clip(PdfPath.RectangleCore(new(fragment.Bounds.X, targetY, plan.Width, to - from), context));
                foreach (var prepared in plan.Cells)
                {
                    context.Checkpoint();
                    var box = prepared.Bounds;
                    if (box.Top <= from || box.Y >= to)
                        continue;
                    var style = prepared.Grid.Cell.Style;
                    double cellTop = targetY + box.Y - from;
                    var destination = new PdfRect(fragment.Bounds.X + box.X, cellTop, box.Width, box.Height);
                    if (style.Background != null)
                        Fill(destination, style.Background);
                    using (canvas.Save())
                    {
                        double insetX = style.Left.Width + style.Padding.Left;
                        double insetRight = style.Right.Width + style.Padding.Right;
                        double insetTop = style.Top.Width + style.Padding.Top;
                        double insetBottom = style.Bottom.Width + style.Padding.Bottom;
                        if (destination.Width > insetX + insetRight && destination.Height > insetTop + insetBottom)
                        {
                            canvas.Clip(PdfPath.RectangleCore(new(destination.X + insetX, destination.Y + insetTop, destination.Width - insetX - insetRight, destination.Height - insetTop - insetBottom), context));
                            foreach (var segment in prepared.Content.Segments)
                            {
                                var visible = segment.Placement.VisibleBounds;
                                double sheetY = prepared.ContentY + segment.Y + visible.Y;
                                if (visible.Height <= 0 || sheetY + visible.Height <= from || sheetY >= to)
                                    continue;
                                canvas.CopyPlacement(segment.Placement, new(1, 0, 0, 1, destination.X + insetX, targetY + prepared.ContentY + segment.Y - from));
                            }
                        }
                    }

                    // Separate cell edges lie entirely inside their own rectangles. Continuation clipping suppresses false page-edge borders.
                    Edge(new(destination.X, destination.Y, Math.Min(style.Left.Width, destination.Width), destination.Height), style.Left);
                    Edge(new(destination.Right - Math.Min(style.Right.Width, destination.Width), destination.Y, Math.Min(style.Right.Width, destination.Width), destination.Height), style.Right);
                    if (fragment.NestedHeaderBandHeight == 0 || from != fragment.BodyStart || box.Y != from)
                        Edge(new(destination.X, destination.Y, destination.Width, Math.Min(style.Top.Width, destination.Height)), style.Top);
                    Edge(new(destination.X, destination.Top - Math.Min(style.Bottom.Width, destination.Height), destination.Width, Math.Min(style.Bottom.Width, destination.Height)), style.Bottom);
                }
            }
        }

        void Edge(PdfRect box, PdfTableBorder border)
        {
            if (border.Width > 0)
                Fill(box, border.Color);
        }

        void Fill(PdfRect box, PdfColor color)
        {
            if (box.Width > 0 && box.Height > 0)
                canvas.DrawPath(PdfPath.RectangleCore(box, context), new PdfCanvasStyle(PdfPaint.Solid(color)));
        }
    }
}
