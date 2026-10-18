using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable painted-layer frame. Hit testing measures its transformed bounding frame and captured clips, rather than glyph ink or image alpha.</summary>
public sealed class PdfCanvasPlacement
{
    internal PdfCanvasPlacement(int index, int z, PdfGraphicsResource resource, PdfRect bounds, PdfMatrix matrix, PdfMatrix own, PdfMatrix toUser, PdfPageGeometry geometry, PdfCanvasClip[] clips)
    {
        Index = index;
        ZIndex = z;
        Resource = resource;
        LocalBounds = bounds;
        Transform = matrix.Multiply(own);
        LocalEmission = matrix;
        OwnMatrix = own;
        ToUser = toUser;
        Geometry = geometry;
        Clips = clips;
        UserEmission = toUser.Multiply(matrix);
        Quad = PdfGeometryMath.Transform(bounds, Transform);
        Bounds = Quad.Bounds;
        UserBounds = PdfGeometryMath.Transform(bounds, toUser.Multiply(Transform)).Bounds;
        var visible = PdfGeometryMath.Intersect(Bounds, PdfGeometryMath.Transform(geometry.Boxes.Crop, toUser.Invert()).Bounds);
        foreach (var clip in clips)
            visible = PdfGeometryMath.Intersect(visible, clip.Bounds);
        VisibleBounds = visible;
    }

    /// <summary>Gets stable insertion order.</summary>
    public int Index { get; }
    /// <summary>Gets explicit z-order; equal values retain insertion order.</summary>
    public int ZIndex { get; }
    /// <summary>Gets the exact immutable painted resource.</summary>
    public PdfGraphicsResource Resource { get; }
    /// <summary>Gets the original source/metric frame.</summary>
    public PdfRect LocalBounds { get; }
    /// <summary>Gets the source-frame-to-canvas transform, including a Form's own matrix.</summary>
    public PdfMatrix Transform { get; }
    /// <summary>Gets transformed perimeter corners in canvas coordinates.</summary>
    public PdfQuad Quad { get; }
    /// <summary>Gets the transformed axis-aligned frame in canvas coordinates.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets the conservative intersection with captured clip bounds and effective CropBox.</summary>
    public PdfRect VisibleBounds { get; }
    /// <summary>Gets the transformed frame in native PDF user coordinates.</summary>
    public PdfRect UserBounds { get; }
    internal PdfMatrix LocalEmission { get; }
    internal PdfMatrix OwnMatrix { get; }
    internal PdfCanvasClip[] Clips { get; }
    internal PdfMatrix UserEmission { get; }
    internal PdfMatrix ToUser { get; }
    internal PdfPageGeometry Geometry { get; }

    /// <summary>Tests a point in base canvas coordinates against the actual convex frame and captured path clips, with absolute clip-local boundary tolerance.</summary>
    public bool HitTest(PdfPoint point, double tolerance = 0.000000001, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfGraphicsNumbers.Finite(tolerance);
        if (tolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        bool hit = false;
        PdfFont.Run("HitTestPlacement", "managed.canvas", policy, cancellationToken, c => hit = HitTestCore(point, tolerance, c), PdfCapability.WriteDocument);
        return hit;
    }

    internal bool HitTestCore(PdfPoint point, double tolerance, PdfOperationContext context)
    {
        context.Checkpoint();
        if (LocalBounds.Width == 0 || LocalBounds.Height == 0 || !Quad.Contains(point) || !PdfGeometryMath.Contains(Geometry.Boxes.Crop, ToUser.Transform(point)))
            return false;
        foreach (var clip in Clips)
            if (!clip.Contains(point, tolerance, context))
                return false;
        return true;
    }
}

/// <summary>A complete immutable canvas, independent of subsequent page edits and builder lifetime.</summary>
public sealed partial class PdfCanvasResult
{
    internal PdfCanvasResult(PdfPageGeometry geometry, PdfCanvasOptions options, PdfRect bounds, PdfMatrix toUser, PdfDrawing drawing, PdfCanvasPlacement[] placements)
    {
        Geometry = geometry;
        Options = options;
        Bounds = bounds;
        ToUser = toUser;
        Drawing = drawing;
        Placements = new ReadOnlyCollection<PdfCanvasPlacement>(placements);
    }

    /// <summary>Gets captured immutable geometry.</summary>
    public PdfPageGeometry Geometry { get; }
    /// <summary>Gets explicit canvas settings.</summary>
    public PdfCanvasOptions Options { get; }
    /// <summary>Gets the margin content frame in canvas units.</summary>
    public PdfRect Bounds { get; }
    /// <summary>Gets base canvas-to-native-user coordinate conversion.</summary>
    public PdfMatrix ToUser { get; }
    /// <summary>Gets the completed vector drawing in native PDF user coordinates.</summary>
    public PdfDrawing Drawing { get; }
    /// <summary>Gets paint frames in ascending z-order and stable insertion order.</summary>
    public IReadOnlyList<PdfCanvasPlacement> Placements { get; }
    /// <summary>Gets the successful shared-context canvas report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;

    /// <summary>Returns hit frames from front to back under one bounded operation.</summary>
    public IReadOnlyList<PdfCanvasPlacement> HitTest(PdfPoint point, bool topmostOnly = true, double tolerance = 0.000000001, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfGraphicsNumbers.Finite(tolerance);
        if (tolerance < 0)
            throw new ArgumentOutOfRangeException(nameof(tolerance));
        var hits = new List<PdfCanvasPlacement>();
        PdfFont.Run("HitTestCanvas", "managed.canvas", policy, cancellationToken, context =>
        {
            for (int i = Placements.Count - 1; i >= 0; i--)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                if (!Placements[i].HitTestCore(point, tolerance, context))
                    continue;
                context.Consume(PdfResourceKind.ExpandedBytes, 32);
                hits.Add(Placements[i]);
                if (topmostOnly)
                    break;
            }
        }, PdfCapability.WriteDocument);
        return hits.AsReadOnly();
    }
}

internal sealed class PdfCanvasClip
{
    internal PdfCanvasClip(PdfPath path, bool evenOdd, PdfMatrix matrix, PdfRect localBounds, IReadOnlyList<PdfContentOperation> operations)
    {
        Path = path;
        EvenOdd = evenOdd;
        Matrix = matrix;
        LocalBounds = localBounds;
        Bounds = PdfGeometryMath.Transform(localBounds, matrix).Bounds;
        RawOperations = operations;
    }

    internal PdfPath Path { get; }
    internal bool EvenOdd { get; }
    internal PdfMatrix Matrix { get; }
    internal PdfRect LocalBounds { get; }
    internal PdfRect Bounds { get; }
    internal IReadOnlyList<PdfContentOperation> RawOperations { get; }

    internal bool Contains(PdfPoint point, double tolerance, PdfOperationContext context)
    {
        context.Checkpoint();
        if (LocalBounds.Width == 0 || LocalBounds.Height == 0 || !PdfGeometryMath.Contains(Bounds, point))
            return false;
        PdfPoint local;
        try
        {
            local = Matrix.Invert().Transform(point);
        }
        catch (InvalidOperationException)
        {
            return false;
        }

        return PdfPathGeometry.Contains(Path, local, EvenOdd, tolerance, context);
    }
}
