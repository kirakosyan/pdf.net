using System.Threading;

namespace Fluent.Pdf;
public static partial class PdfPageOrganization
{
    /// <summary>Changes selected immutable page geometry without changing native content coordinates. Rotate, crop and resize are explicit geometry operations.</summary>
    public static PdfDocument Transform(PdfDocumentSnapshot source, PdfPageSelection selection, Func<PdfPageGeometry, PdfPageGeometry> transform, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (selection == null)
            throw new ArgumentNullException(nameof(selection));
        if (transform == null)
            throw new ArgumentNullException(nameof(transform));
        options ??= new();
        PdfDocument? result = null;
        try
        {
            var report = PdfFont.Run("TransformPageGeometry", "managed.organization", policy, cancellationToken, context =>
            {
                ContextValidate(source, selection, options, context);
                context.Consume(PdfResourceKind.ExpandedBytes, source.Pages.Count * 256L + selection.Indices.Count * 32L + 256);
                var chosen = new HashSet<int>(selection.Indices);
                var pages = new PdfPageSnapshot[source.Pages.Count];
                for (int i = 0; i < pages.Length; i++)
                {
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.Objects, 1);
                    var page = source.Pages[i];
                    var geometry = chosen.Contains(i) ? transform(page.Geometry) ?? throw new ArgumentException("A geometry callback returned null.", nameof(transform)) : page.Geometry;
                    pages[i] = new(geometry.Size, geometry.Rotation, page.Source, page.Content, page.FontResources, page.GraphicsResources, geometry, page.CanvasLayers, page.StructureRoles, page.Links);
                }

                result = PdfPageOrganization.Normalize(new(source.Options, pages, source.SourceDocument, source.OriginalOrder, source.WriteOptions, source.PageNumbering), options, context);
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

    /// <summary>Scales all physical page coordinates and annotations uniformly by changing UserUnit while retaining native boxes, content, structure and rotation.</summary>
    public static PdfDocument Scale(PdfDocumentSnapshot source, PdfPageSelection selection, double factor, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfGraphicsNumbers.Finite(factor);
        if (factor <= 0)
            throw new ArgumentOutOfRangeException(nameof(factor));
        return Transform(source, selection, g => new(g.Boxes, g.Rotation, g.UserUnit * factor), options, policy, cancellationToken);
    }

    private static void ContextValidate(PdfDocumentSnapshot source, PdfPageSelection selection, PdfOrganizationOptions options, PdfOperationContext context)
    {
        if (source.Pages.Count > options.MaximumPages)
            throw PdfWriterCore.Error(context, "Organization.PageLimit", "The output exceeds its finite page ceiling.");
        if (selection.Indices.Any(i => i >= source.Pages.Count))
            throw PdfWriterCore.Error(context, "Organization.PageRange", "A selected page exceeds this source.");
    }
}
