using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>A typed immutable path segment in PDF user coordinates.</summary>
public sealed class PdfPathSegment
{
    private PdfPathSegment(PdfOperator operation, params double[] values)
    {
        foreach (double value in values)
            PdfGraphicsNumbers.Finite(value);
        Operation = operation;
        Coordinates = new ReadOnlyCollection<double>(values);
    }

    /// <summary>Gets its path construction operator.</summary>
    public PdfOperator Operation { get; }
    /// <summary>Gets immutable finite coordinates.</summary>
    public IReadOnlyList<double> Coordinates { get; }

    /// <summary>Starts a subpath.</summary>
    public static PdfPathSegment Move(double x, double y) => new(PdfOperator.MoveTo, x, y);
    /// <summary>Adds a straight segment.</summary>
    public static PdfPathSegment Line(double x, double y) => new(PdfOperator.LineTo, x, y);
    /// <summary>Adds a cubic Bézier segment.</summary>
    public static PdfPathSegment Curve(double x1, double y1, double x2, double y2, double x3, double y3) => new(PdfOperator.CurveTo, x1, y1, x2, y2, x3, y3);
    /// <summary>Closes the current subpath.</summary>
    public static PdfPathSegment Close() => new(PdfOperator.ClosePath);
    /// <summary>Adds a closed rectangle.</summary>
    public static PdfPathSegment Rectangle(PdfRect rectangle) => new(PdfOperator.Rectangle, rectangle.X, rectangle.Y, rectangle.Width, rectangle.Height);
}

/// <summary>An immutable path that retains vector segments and can be edited without changing another drawing.</summary>
public sealed partial class PdfPath
{
    private PdfPath(List<PdfPathSegment> segments, List<PdfContentOperation> operations)
    {
        Segments = segments.AsReadOnly();
        Operations = operations.AsReadOnly();
    }

    /// <summary>Gets exact authored segments.</summary>
    public IReadOnlyList<PdfPathSegment> Segments { get; }
    /// <summary>Gets validated path construction operators.</summary>
    public IReadOnlyList<PdfContentOperation> Operations { get; }
    /// <summary>Gets the successful bounded construction report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;

    /// <summary>Creates a validated vector path under a fresh operation budget.</summary>
    public static PdfPath Create(IEnumerable<PdfPathSegment> segments, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (segments == null)
            throw new ArgumentNullException(nameof(segments));
        PdfPath? result = null;
        var report = PdfFont.Run("CreatePath", "managed.graphics", policy, cancellationToken, context =>
        {
            var items = new List<PdfPathSegment>();
            var operations = new List<PdfContentOperation>();
            bool current = false;
            foreach (var segment in segments)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 512);
                if (segment == null)
                    throw new ArgumentException("A path segment cannot be null.", nameof(segments));
                if (!current && segment.Operation != PdfOperator.MoveTo && segment.Operation != PdfOperator.Rectangle)
                    throw PdfWriterCore.Error(context, "Graphics.PathCurrentPoint", "A path segment requires a current point.");
                current = true;
                items.Add(segment);
                operations.Add(new PdfContentOperation(segment.Operation, segment.Coordinates.Select(PdfValues.Real).ToArray()));
            }

            result = new PdfPath(items, operations);
        }, PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Returns a separately validated path with a logical segment range replaced.</summary>
    public PdfPath Edit(int index, int removeCount, IEnumerable<PdfPathSegment> replacement, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (index < 0 || removeCount < 0 || index > Segments.Count - removeCount)
            throw new ArgumentOutOfRangeException(nameof(index));
        if (replacement == null)
            throw new ArgumentNullException(nameof(replacement));
        return Create(Segments.Take(index).Concat(replacement).Concat(Segments.Skip(index + removeCount)), policy, cancellationToken);
    }
}
