namespace Fluent.Pdf;
// Conservative shared font-bounding-box envelopes, including shaping offsets and paragraph positions.
internal static class PdfTextGeometry
{
    internal static PdfRect Bounds(PdfShapedText text, double size, double lineHeight, PdfOperationContext context)
    {
        bool any = false;
        PdfRect bounds = default;
        int paragraph = -1;
        double penX = 0, penY = 0;
        foreach (var run in text.Runs)
        {
            if (paragraph != run.ParagraphIndex)
            {
                paragraph = run.ParagraphIndex;
                penX = text.Options.Vertical ? -paragraph * size * lineHeight : 0;
                penY = text.Options.Vertical ? 0 : -paragraph * size * lineHeight;
            }

            var metrics = run.Request.Font.Info.Metrics;
            double scale = size / metrics.UnitsPerEm;
            foreach (var glyph in run.Glyphs)
            {
                context.Checkpoint();
                var box = new PdfRect(penX + (glyph.OffsetX + metrics.Left) * scale, penY + (glyph.OffsetY + metrics.Bottom) * scale, (metrics.Right - (double)metrics.Left) * scale, (metrics.Top - (double)metrics.Bottom) * scale);
                if (!any)
                    bounds = box;
                else
                {
                    double x = Math.Min(bounds.X, box.X), y = Math.Min(bounds.Y, box.Y);
                    bounds = new(x, y, Math.Max(bounds.Right, box.Right) - x, Math.Max(bounds.Top, box.Top) - y);
                }

                any = true;
                penX += glyph.AdvanceX * scale;
                penY += glyph.AdvanceY * scale;
            }
        }

        return bounds;
    }
}
