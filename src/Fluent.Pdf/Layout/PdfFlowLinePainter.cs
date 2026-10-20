using System.Globalization;

namespace Fluent.Pdf;
internal static class PdfFlowLinePainter
{
    internal static void Paint(PdfCanvas canvas, PdfFlowLine line, PdfFlowParagraphData.Measure measured, PdfFlowParagraphStyle style, bool last, PdfFlowOptions options, PdfOperationContext context, List<Action<PdfOperationReport>> finish)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, line.Text.Length * 8L + measured.Shaped.Runs.Count * 256L + 2048);
        var actual = new byte[line.Text.Length * 2 + 2];
        actual[0] = 254;
        actual[1] = 255;
        for (int i = 0; i < line.Text.Length; i++)
        {
            actual[2 + i * 2] = (byte)(line.Text[i] >> 8);
            actual[3 + i * 2] = (byte)line.Text[i];
        }

        var operations = new List<PdfContentOperation>();
        var bindings = new List<PdfResourceBinding>();
        var fonts = new List<PdfFontResource>();
        operations.Add(new(PdfOperator.SaveState));
        operations.Add(new(PdfOperator.BeginMarkedContentProperties, PdfValue.Name("Span"), PdfGraphicsNumbers.Dict(("ActualText", PdfValue.String(actual)))));
        operations.Add(new(PdfOperator.BeginText));
        double x = PdfFlowEngine.AlignmentShift(style.Alignment, measured, line.Bounds.Width) + measured.LeftPadding, left = x, right = x, top = measured.Top, bottom = measured.Bottom;
        double originX = x;
        int lastVisible = line.Text.Length - 1;
        while (lastVisible >= 0 && char.IsWhiteSpace(line.Text[lastVisible]))
            lastVisible--;
        int spaces = 0;
        if (style.Alignment == PdfFlowAlignment.Justify && !last && measured.Entries.Any(e => e.Run == null))
            throw PdfWriterCore.Error(context, "Layout.JustifiedTabs", "Justification with fixed tab stops requires an explicitly implemented tab alignment profile.");
        if (style.Alignment == PdfFlowAlignment.Justify && !last && !line.IsClipped)
            for (int i = 0; i <= lastVisible; i++)
                if (line.Text[i] == ' ')
                    spaces++;
        double expansion = spaces == 0 ? 0 : Math.Max(0, line.Bounds.Width - measured.Width) / spaces;
        foreach (var entry in measured.Entries)
        {
            context.Checkpoint();
            if (entry.Run == null)
            {
                x = originX + options.NextTab(x - originX);
                continue;
            }

            var run = entry.Run;
            var textStyle = entry.Style!;
            if (line.IsClipped && textStyle.SourceUri != null)
                throw PdfInputConversion.Error(context, "LinkClip", "A clipped linked run requires an explicit visible-link geometry profile.");
            if (textStyle.SourceRole != null)
                operations.Add(new(PdfOperator.BeginMarkedContent, PdfValue.Name(textStyle.SourceRole)));
            if (textStyle.SourceUri != null)
                operations.Add(new(PdfOperator.BeginMarkedContentProperties, PdfValue.Name("Link"), PdfGraphicsNumbers.Dict(("FPLinkURI", PdfDeferredNavigation.Text(textStyle.SourceUri, context)))));
            var clusters = run.Glyphs.Select(g => g.Cluster).Distinct().OrderBy(c => c).ToArray();
            context.Consume(PdfResourceKind.ExpandedBytes, clusters.Length * 64L + run.Glyphs.Count * 384L + 512);
            var ends = new Dictionary<int, int>();
            for (int i = 0; i < clusters.Length; i++)
                ends[clusters[i]] = i + 1 < clusters.Length ? clusters[i + 1] : run.Request.Start + run.Request.Length;
            var mappings = new List<PdfGlyphMapping>();
            var seen = new HashSet<int>();
            foreach (var glyph in run.Glyphs)
            {
                int count = ends[glyph.Cluster] - glyph.Cluster;
                if (count > 512)
                    throw PdfWriterCore.Error(context, "Text.ClusterEncodingLimit", "A layout glyph cluster exceeds the font resource mapping limit.");
                mappings.Add(new(glyph.GlyphIndex, seen.Add(glyph.Cluster) ? line.Text.Substring(glyph.Cluster, count) : string.Empty));
            }

            var font = PdfFontResource.CreateCore(run.Request.Font, mappings, true, false, context);
            fonts.Add(font);
            string suffix = fonts.Count.ToString(CultureInfo.InvariantCulture), fontName = "F" + suffix, colorName = "C" + suffix;
            bindings.Add(new(fontName, PdfGraphicsResource.Font(font)));
            var color = textStyle.Color;
            bindings.Add(new(colorName, PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.ColorSpace, color.ColorSpace.Definition, color.ColorSpace.MinimumVersion, PdfResourceSet.Empty, context)));
            operations.Add(new(PdfOperator.FillColorSpace, PdfValue.Name(colorName)));
            operations.Add(new(PdfOperator.FillColor, color.Components.Select(PdfValues.Real).ToArray()));
            operations.Add(new(PdfOperator.Font, PdfValue.Name(fontName), PdfValues.Real(textStyle.FontSize)));
            var bytes = font.EncodedText.StringBytes;
            int codeWidth = bytes.Count / run.Glyphs.Count;
            var m = run.Request.Font.Info.Metrics;
            double scale = textStyle.FontSize / m.UnitsPerEm;
            for (int i = 0; i < run.Glyphs.Count; i++)
            {
                context.Checkpoint();
                var glyph = run.Glyphs[i];
                var code = new byte[codeWidth];
                for (int b = 0; b < codeWidth; b++)
                    code[b] = bytes[i * codeWidth + b];
                double gx = x + glyph.OffsetX * scale, gy = glyph.OffsetY * scale + textStyle.BaselineShift;
                operations.Add(new(PdfOperator.TextMatrix, PdfValue.Integer(1), PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(1), PdfValues.Real(gx), PdfValues.Real(gy)));
                operations.Add(new(PdfOperator.ShowText, PdfValue.String(code)));
                left = Math.Min(left, gx + m.Left * scale);
                right = Math.Max(right, gx + m.Right * scale);
                top = Math.Max(top, gy + m.Top * scale);
                bottom = Math.Min(bottom, gy + m.Bottom * scale);
                x += glyph.AdvanceX * scale;
                bool lastGlyph = i + 1 == run.Glyphs.Count || run.Glyphs[i + 1].Cluster != glyph.Cluster;
                if (lastGlyph && expansion > 0)
                    for (int c = glyph.Cluster; c < ends[glyph.Cluster] && c <= lastVisible; c++)
                        if (line.Text[c] == ' ')
                            x += expansion;
            }

            if (textStyle.SourceUri != null)
                operations.Add(new(PdfOperator.EndMarkedContent));
            if (textStyle.SourceRole != null)
                operations.Add(new(PdfOperator.EndMarkedContent));
        }

        operations.Add(new(PdfOperator.EndText));
        operations.Add(new(PdfOperator.EndMarkedContent));
        operations.Add(new(PdfOperator.RestoreState));
        var drawing = PdfDrawing.CreateCore(operations, new(bindings.ToArray()), context);
        var box = new PdfRect(left, bottom, Math.Max(.000001, right - left), Math.Max(.000001, top - bottom));
        using (canvas.Save())
        {
            if (line.IsClipped)
                canvas.Clip(PdfPath.RectangleCore(line.Bounds, context));
            canvas.DrawDrawing(drawing, box, new(line.Bounds.X + box.X, line.Baseline - box.Top, box.Width, box.Height), PdfCanvasFit.Stretch);
        }

        finish.Add(report =>
        {
            drawing.OperationReport = report;
            drawing.Content.OperationReport = report;
            foreach (var font in fonts)
                font.OperationReport = report;
            foreach (var binding in drawing.Resources.Bindings)
                binding.Resource.OperationReport = report;
        });
    }
}
