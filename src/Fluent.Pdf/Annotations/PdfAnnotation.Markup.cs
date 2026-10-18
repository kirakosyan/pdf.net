using System.Threading;

namespace Fluent.Pdf;
public sealed partial class PdfAnnotation
{
    /// <summary>Creates original vector appearances for text icons, shapes, ink, carets and text markup. QuadPoints use upper-left, upper-right, lower-left, lower-right order; complex text, stamps and media use explicit appearances.</summary>
    public static PdfAnnotation CreateMarkup(PdfAnnotationKind kind, string identifier, PdfRect bounds, PdfColor color, PdfValue? properties = null, PdfAnnotationMetadata? metadata = null, double strokeWidth = 1, double opacity = 1, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (color == null)
            throw new ArgumentNullException(nameof(color));
        if (string.IsNullOrEmpty(identifier) || identifier.Length > 4096)
            throw new ArgumentException("A bounded identifier is required.", nameof(identifier));
        PdfGraphicsNumbers.Finite(strokeWidth);
        PdfGraphicsNumbers.Unit(opacity);
        if (strokeWidth <= 0 || bounds.Width <= 0 || bounds.Height <= 0 || strokeWidth > Math.Min(bounds.Width, bounds.Height))
            throw new ArgumentOutOfRangeException(nameof(strokeWidth));
        properties ??= PdfGraphicsNumbers.Dict();
        metadata ??= new();
        PdfAnnotation? result = null;
        var report = PdfFont.Run("CreateAnnotationMarkup", "managed.annotations", policy, cancellationToken, context =>
        {
            if (!Enum.IsDefined(typeof(PdfAnnotationKind), kind) || kind == PdfAnnotationKind.FreeText || kind == PdfAnnotationKind.Stamp || kind == PdfAnnotationKind.FileAttachment || kind == PdfAnnotationKind.RichMedia)
                throw PdfWriterCore.Error(context, "Annotation.ExplicitAppearanceRequired", "Text, stamps and media require their explicit shaped/custom/poster appearance contracts.");
            if (properties.Kind != PdfValueKind.Dictionary)
                throw PdfWriterCore.Error(context, "Annotation.Properties", "Annotation properties require a dictionary.");
            ValidateProperties(properties, context);
            string? geometryKey = kind == PdfAnnotationKind.Ink ? "InkList" : kind == PdfAnnotationKind.Line ? "L" : kind == PdfAnnotationKind.Polygon || kind == PdfAnnotationKind.PolyLine ? "Vertices" : kind == PdfAnnotationKind.Highlight || kind == PdfAnnotationKind.Underline || kind == PdfAnnotationKind.StrikeOut || kind == PdfAnnotationKind.Squiggly ? "QuadPoints" : null;
            foreach (var pair in properties.Entries)
                if (pair.Key != "F" && pair.Key != geometryKey)
                    throw PdfWriterCore.Error(context, "Annotation.GeneratedStyleProfile", "Additional icons, borders, fill, endings or styling require an explicit custom appearance.");
            ValidateGeometry(kind.ToString(), properties, bounds, v => v, context);
            PdfGraphImport.ValidateDetached(properties, context);
            if (color.ColorSpace.Definition.Kind != PdfValueKind.Name || color.ColorSpace.IsPattern)
                throw PdfWriterCore.Error(context, "Annotation.MarkupColor", "Generated markup currently requires explicit DeviceGray, DeviceRGB or DeviceCMYK; other spaces use a custom appearance.");
            var values = PdfWriterCore.CopyDictionary(properties.Entries, context);
            values["C"] = PdfGraphicsNumbers.Array(color.Components.ToArray());
            values["CA"] = PdfValues.Real(opacity);
            values["BS"] = PdfGraphicsNumbers.Dict(("W", PdfValues.Real(strokeWidth)), ("S", PdfValue.Name("S")));
            var state = PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.ExtGState, PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("ExtGState")), ("ca", PdfValues.Real(opacity)), ("CA", PdfValues.Real(opacity)), ("BM", PdfValue.Name(kind == PdfAnnotationKind.Highlight ? "Multiply" : "Normal"))), "1.4", PdfResourceSet.Empty, context);
            var operations = new List<PdfContentOperation>
            {
                new(PdfOperator.ExtendedState, PdfValue.Name("Alpha")),
                new(PdfOperator.LineWidth, PdfValues.Real(strokeWidth)),
                new(PdfOperator.LineCap, PdfValue.Integer(1)),
                new(PdfOperator.LineJoin, PdfValue.Integer(1))
            };
            operations.Add(new(color.Components.Count == 1 ? PdfOperator.FillGray : color.Components.Count == 3 ? PdfOperator.FillRgb : PdfOperator.FillCmyk, color.Components.Select(PdfValues.Real).ToArray()));
            operations.Add(new(color.Components.Count == 1 ? PdfOperator.StrokeGray : color.Components.Count == 3 ? PdfOperator.StrokeRgb : PdfOperator.StrokeCmyk, color.Components.Select(PdfValues.Real).ToArray()));
            void Op(PdfOperator op, params double[] numbers)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 128 + numbers.Length * 64L);
                operations.Add(new(op, numbers.Select(PdfValues.Real).ToArray()));
            }

            void Path(PdfValue points, bool close, bool fill)
            {
                for (int i = 0; i < points.Items.Count; i += 2)
                    Op(i == 0 ? PdfOperator.MoveTo : PdfOperator.LineTo, points.Items[i].NumberValue, points.Items[i + 1].NumberValue);
                if (close)
                    Op(PdfOperator.ClosePath);
                Op(fill ? PdfOperator.Fill : PdfOperator.Stroke);
            }

            double inset = strokeWidth / 2, left = bounds.X + inset, bottom = bounds.Y + inset, right = bounds.Right - inset, top = bounds.Top - inset;
            if (kind == PdfAnnotationKind.Square || kind == PdfAnnotationKind.Text)
            {
                Op(PdfOperator.Rectangle, left, bottom, right - left, top - bottom);
                Op(kind == PdfAnnotationKind.Text ? PdfOperator.FillStroke : PdfOperator.Stroke);
                if (kind == PdfAnnotationKind.Text)
                {
                    Op(PdfOperator.StrokeGray, 0);
                    for (int i = 1; i <= 3; i++)
                    {
                        double y = bottom + (top - bottom) * i / 4;
                        Op(PdfOperator.MoveTo, left + (right - left) / 5, y);
                        Op(PdfOperator.LineTo, right - (right - left) / 5, y);
                        Op(PdfOperator.Stroke);
                    }

                    values["Name"] = PdfValue.Name("Note");
                }
            }
            else if (kind == PdfAnnotationKind.Circle)
            {
                const double k = .5522847498307936;
                double cx = (left + right) / 2, cy = (bottom + top) / 2, rx = (right - left) / 2, ry = (top - bottom) / 2;
                Op(PdfOperator.MoveTo, right, cy);
                Op(PdfOperator.CurveTo, right, cy + k * ry, cx + k * rx, top, cx, top);
                Op(PdfOperator.CurveTo, cx - k * rx, top, left, cy + k * ry, left, cy);
                Op(PdfOperator.CurveTo, left, cy - k * ry, cx - k * rx, bottom, cx, bottom);
                Op(PdfOperator.CurveTo, cx + k * rx, bottom, right, cy - k * ry, right, cy);
                Op(PdfOperator.ClosePath);
                Op(PdfOperator.Stroke);
            }
            else if (kind == PdfAnnotationKind.Caret)
            {
                Op(PdfOperator.MoveTo, left, bottom);
                Op(PdfOperator.LineTo, (left + right) / 2, top);
                Op(PdfOperator.LineTo, right, bottom);
                Op(PdfOperator.Stroke);
            }
            else if (kind == PdfAnnotationKind.Line)
                Path(properties.Entries["L"], false, false);
            else if (kind == PdfAnnotationKind.Polygon || kind == PdfAnnotationKind.PolyLine)
                Path(properties.Entries["Vertices"], kind == PdfAnnotationKind.Polygon, false);
            else if (kind == PdfAnnotationKind.Ink)
                foreach (var path in properties.Entries["InkList"].Items)
                    Path(path, false, false);
            else
            {
                var quad = properties.Entries["QuadPoints"].Items;
                for (int i = 0; i < quad.Count; i += 8)
                {
                    context.Checkpoint();
                    double ulx = quad[i].NumberValue, uly = quad[i + 1].NumberValue, urx = quad[i + 2].NumberValue, ury = quad[i + 3].NumberValue, llx = quad[i + 4].NumberValue, lly = quad[i + 5].NumberValue, lrx = quad[i + 6].NumberValue, lry = quad[i + 7].NumberValue;
                    if (kind == PdfAnnotationKind.Highlight)
                    {
                        Op(PdfOperator.MoveTo, ulx, uly);
                        Op(PdfOperator.LineTo, urx, ury);
                        Op(PdfOperator.LineTo, lrx, lry);
                        Op(PdfOperator.LineTo, llx, lly);
                        Op(PdfOperator.ClosePath);
                        Op(PdfOperator.Fill);
                    }
                    else
                    {
                        double x = kind == PdfAnnotationKind.StrikeOut ? (ulx + llx) / 2 : llx, y = kind == PdfAnnotationKind.StrikeOut ? (uly + lly) / 2 : lly, ex = kind == PdfAnnotationKind.StrikeOut ? (urx + lrx) / 2 : lrx, ey = kind == PdfAnnotationKind.StrikeOut ? (ury + lry) / 2 : lry;
                        Op(PdfOperator.MoveTo, x, y);
                        if (kind == PdfAnnotationKind.Squiggly)
                        {
                            double dx = ex - x, dy = ey - y, length = Math.Sqrt(dx * dx + dy * dy);
                            if (length <= 0 || double.IsInfinity(length))
                                throw PdfWriterCore.Error(context, "Annotation.QuadGeometry", "A generated squiggle requires finite nondegenerate quad baselines.");
                            double count = Math.Ceiling(length / Math.Max(2, strokeWidth * 3));
                            if (count > 4096)
                                throw PdfWriterCore.Error(context, "Annotation.AppearanceWorkLimit", "A generated squiggle exceeds its explicit per-quad work ceiling.");
                            int steps = Math.Max(2, (int)count);
                            for (int j = 1; j <= steps; j++)
                            {
                                double t = (double)j / steps, wave = j == steps ? 0 : (j % 2 == 0 ? 0 : strokeWidth);
                                Op(PdfOperator.LineTo, x + dx * t - dy / length * wave, y + dy * t + dx / length * wave);
                            }
                        }
                        else
                            Op(PdfOperator.LineTo, ex, ey);
                        Op(PdfOperator.Stroke);
                    }
                }
            }

            var drawing = PdfDrawing.CreateCore(operations, new(new PdfResourceBinding("Alpha", state)), context);
            var appearance = PdfGraphicsObjects.FormCore(drawing, bounds, null, null, context);
            result = CreateCore(kind, identifier, bounds, appearance, PdfValue.OwnedDictionary(values), metadata, context);
        }, PdfCapability.WriteDocument);
        result!.OperationReport = report;
        result.NormalAppearance.OperationReport = report;
        return result;
    }
}
