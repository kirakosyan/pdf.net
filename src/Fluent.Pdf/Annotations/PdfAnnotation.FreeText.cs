using System.Globalization;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
public sealed partial class PdfAnnotation
{
    /// <summary>Creates a real embedded shaped-text appearance, using conservative font bounds to reject overflow. Geometry and font size use native page units; later arbitrary text regeneration is a separate editing profile.</summary>
    public static PdfAnnotation CreateFreeText(string identifier, PdfRect bounds, PdfShapedText text, double fontSize, PdfAnnotationMetadata? metadata = null, double padding = 2, bool allowClipping = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (string.IsNullOrEmpty(identifier) || identifier.Length > 4096)
            throw new ArgumentException("A bounded identifier is required.", nameof(identifier));
        PdfGraphicsNumbers.Finite(fontSize);
        PdfGraphicsNumbers.Finite(padding);
        if (fontSize <= 0 || padding < 0 || bounds.Width <= 0 || bounds.Height <= 0)
            throw new ArgumentOutOfRangeException(nameof(fontSize));
        PdfAnnotation? result = null;
        var report = PdfFont.Run("CreateFreeTextAnnotation", "managed.annotations", policy, cancellationToken, context =>
        {
            if (text.Runs.Count == 0 || text.Runs.All(r => r.Glyphs.Count == 0))
                throw PdfWriterCore.Error(context, "Annotation.EmptyText", "The shaped free-text factory requires at least one glyph.");
            var ink = PdfTextGeometry.Bounds(text, fontSize, 1.2, context);
            if (ink.Width + padding * 2 > bounds.Width || ink.Height + padding * 2 > bounds.Height)
            {
                if (!allowClipping)
                    throw PdfWriterCore.Error(context, "Annotation.TextOverflow", "The conservative shaped-text envelope exceeds the annotation's padded rectangle.");
                context.Report(new("Annotation.TextClipped", "The explicit free-text clipping policy truncates the appearance to its rectangle.", severity: PdfDiagnosticSeverity.Warning, lossCategory: PdfLossCategory.OmittedContent));
            }

            var positioned = PdfTextDrawing.CreateCore(text, bounds.X + padding - ink.X, bounds.Top - padding - ink.Top, fontSize, 1.2, "AnnText", true, context);
            context.Consume(PdfResourceKind.ExpandedBytes, positioned.Fonts.Count * 192L + 256);
            var resources = new PdfResourceSet(positioned.Fonts.Select(f => new PdfResourceBinding(f.Key, PdfGraphicsResource.Font(f.Value))).ToArray());
            var drawing = PdfDrawing.CreateCore(positioned.Content.Operations, resources, context);
            var appearance = PdfGraphicsObjects.FormCore(drawing, bounds, null, null, context);
            string da = "/" + positioned.Fonts.First().Key + " " + PdfValues.Real(fontSize).NumberText + " Tf 0 g";
            context.Consume(PdfResourceKind.ExpandedBytes, da.Length * 4L + 128);
            var properties = PdfGraphicsNumbers.Dict(("DA", PdfValue.String(Encoding.ASCII.GetBytes(da))), ("Q", PdfValue.Integer(0)));
            metadata ??= new();
            if (metadata.Contents.Length != 0 && metadata.Contents != text.Unicode.Text)
                throw PdfWriterCore.Error(context, "Annotation.TextContents", "Explicit free-text contents must match the shaped logical text.");
            var actual = new PdfAnnotationMetadata(text.Unicode.Text, metadata.Author, metadata.Subject, metadata.Created, metadata.Modified, metadata.State, metadata.StateModel, metadata.ReplyTo, metadata.GroupReply);
            result = CreateCore(PdfAnnotationKind.FreeText, identifier, bounds, appearance, properties, actual, context);
        }, PdfCapability.WriteDocument);
        result!.OperationReport = report;
        result.NormalAppearance.OperationReport = report;
        foreach (var binding in result.NormalAppearance.Dependencies.Bindings)
        {
            binding.Resource.OperationReport = report;
            if (binding.Resource.FontResource != null)
                binding.Resource.FontResource.OperationReport = report;
        }

        return result;
    }
}
