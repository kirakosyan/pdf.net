using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable standard vector shading, retaining explicit color-space and interpolation data.</summary>
public sealed class PdfShading
{
    private PdfShading(PdfGraphicsResource resource, PdfColorSpace colorSpace, int type)
    {
        Resource = resource;
        ColorSpace = colorSpace;
        ShadingType = type;
    }

    /// <summary>Gets its reusable Shading resource.</summary>
    public PdfGraphicsResource Resource { get; }
    /// <summary>Gets its exact color space.</summary>
    public PdfColorSpace ColorSpace { get; }
    /// <summary>Gets its standard shading type, one through seven.</summary>
    public int ShadingType { get; }

    /// <summary>Creates a type-2 axial gradient with an exact one-input color function.</summary>
    public static PdfShading Axial(PdfColorSpace colorSpace, PdfPoint start, PdfPoint end, PdfFunction function, bool extendStart = false, bool extendEnd = false, PdfRect? bounds = null, bool antiAlias = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (start.X == end.X && start.Y == end.Y)
            throw new ArgumentException("An axial gradient requires a nonzero axis.");
        return Gradient(colorSpace, function, 2, PdfGraphicsNumbers.Array(start.X, start.Y, end.X, end.Y), extendStart, extendEnd, bounds, antiAlias, policy, cancellationToken);
    }

    /// <summary>Creates a type-3 radial gradient with explicit nonnegative circle radii.</summary>
    public static PdfShading Radial(PdfColorSpace colorSpace, PdfPoint start, double startRadius, PdfPoint end, double endRadius, PdfFunction function, bool extendStart = false, bool extendEnd = false, PdfRect? bounds = null, bool antiAlias = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfGraphicsNumbers.Finite(startRadius);
        PdfGraphicsNumbers.Finite(endRadius);
        if (startRadius < 0 || endRadius < 0 || startRadius == 0 && endRadius == 0)
            throw new ArgumentOutOfRangeException(nameof(startRadius));
        return Gradient(colorSpace, function, 3, PdfGraphicsNumbers.Array(start.X, start.Y, startRadius, end.X, end.Y, endRadius), extendStart, extendEnd, bounds, antiAlias, policy, cancellationToken);
    }

    /// <summary>Creates a type-1 two-input function shading with an explicit domain and transform.</summary>
    public static PdfShading Functional(PdfColorSpace colorSpace, PdfFunction function, PdfRect domain, PdfMatrix? matrix = null, PdfRect? bounds = null, bool antiAlias = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Check(colorSpace, function, 2);
        if (domain.Width <= 0 || domain.Height <= 0)
            throw new ArgumentException("A function shading requires a positive two-dimensional domain.", nameof(domain));
        var entries = Base(colorSpace, 1, bounds, antiAlias);
        entries["Domain"] = PdfGraphicsNumbers.Array(domain.X, domain.Right, domain.Y, domain.Top);
        entries["Matrix"] = (matrix ?? PdfMatrix.Identity).Value;
        entries["Function"] = function.Definition;
        return Capture(colorSpace, 1, PdfValue.OwnedDictionary(entries), policy, cancellationToken);
    }

    /// <summary>Captures and validates decoded type-4/5/6/7 mesh packets without quantizing coordinates or colors.</summary>
    public static PdfShading Mesh(PdfColorSpace colorSpace, int shadingType, Stream packedData, int bitsPerCoordinate, int bitsPerComponent, double[] decode, int bitsPerFlag = 2, int verticesPerRow = 0, PdfFunction? function = null, PdfRect? bounds = null, bool antiAlias = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (colorSpace == null || packedData == null || decode == null)
            throw new ArgumentNullException(nameof(colorSpace));
        if (!packedData.CanRead)
            throw new ArgumentException("A readable mesh stream is required.", nameof(packedData));
        if (colorSpace.IsPattern || shadingType < 4 || shadingType > 7 || !new[]
        {
            1,
            2,
            4,
            8,
            12,
            16,
            24,
            32
        }.Contains(bitsPerCoordinate) || !new[]
        {
            1,
            2,
            4,
            8,
            12,
            16
        }.Contains(bitsPerComponent) || !new[]
        {
            2,
            4,
            8
        }.Contains(bitsPerFlag))
            throw new ArgumentException("Mesh type, color space or packed precision is invalid.");
        if (shadingType == 5 && verticesPerRow < 2)
            throw new ArgumentOutOfRangeException(nameof(verticesPerRow));
        int channels = function == null ? colorSpace.Components : 1;
        if (function != null)
            Check(colorSpace, function, 1);
        if (decode.Length != 4 + channels * 2)
            throw new ArgumentException("Mesh decode pairs must cover coordinates and every stored color/function channel.", nameof(decode));
        PdfFunction.Bounds(decode, 34);
        PdfShading? result = null;
        var report = PdfFont.Run("CreateMeshShading", "managed.graphics", policy, cancellationToken, context =>
        {
            using var lease = new BorrowedStream(packedData, true, context);
            var bytes = PdfByteStore.Read(lease, context);
            PdfMeshValidator.Validate(bytes, shadingType, bitsPerCoordinate, bitsPerComponent, bitsPerFlag, channels, verticesPerRow, context);
            var entries = Base(colorSpace, shadingType, bounds, antiAlias);
            entries["BitsPerCoordinate"] = PdfValue.Integer(bitsPerCoordinate);
            entries["BitsPerComponent"] = PdfValue.Integer(bitsPerComponent);
            entries["Decode"] = PdfGraphicsNumbers.Array(decode);
            if (shadingType == 5)
                entries["VerticesPerRow"] = PdfValue.Integer(verticesPerRow);
            else
                entries["BitsPerFlag"] = PdfValue.Integer(bitsPerFlag);
            if (function != null)
                entries["Function"] = function.Definition;
            var definition = PdfValue.StreamValue(PdfValue.OwnedDictionary(entries), bytes, 0, bytes.Length);
            result = new PdfShading(PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.Shading, definition, PdfVersionNumber.Text(Math.Max(13, PdfVersionNumber.Parse(colorSpace.MinimumVersion))), PdfResourceSet.Empty, context), colorSpace, shadingType);
        }, PdfCapability.WriteDocument);
        result!.Resource.OperationReport = report;
        return result;
    }

    private static PdfShading Gradient(PdfColorSpace space, PdfFunction function, int type, PdfValue coordinates, bool extendStart, bool extendEnd, PdfRect? bounds, bool antiAlias, PdfOperationPolicy? policy, CancellationToken token)
    {
        Check(space, function, 1);
        var entries = Base(space, type, bounds, antiAlias);
        entries["Coords"] = coordinates;
        entries["Function"] = function.Definition;
        entries["Domain"] = PdfGraphicsNumbers.Array(function.Domain.ToArray());
        entries["Extend"] = PdfValue.Array(PdfValue.Boolean(extendStart), PdfValue.Boolean(extendEnd));
        return Capture(space, type, PdfValue.OwnedDictionary(entries), policy, token);
    }

    private static PdfShading Capture(PdfColorSpace space, int type, PdfValue definition, PdfOperationPolicy? policy, CancellationToken token) => new(PdfGraphicsResource.Create(PdfGraphicsResourceCategory.Shading, definition, PdfVersionNumber.Text(Math.Max(13, PdfVersionNumber.Parse(space.MinimumVersion))), policy, token), space, type);
    private static Dictionary<string, PdfValue> Base(PdfColorSpace space, int type, PdfRect? bounds, bool antiAlias)
    {
        var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
        {
            {
                "ShadingType",
                PdfValue.Integer(type)
            },
            {
                "ColorSpace",
                space.Definition
            },
            {
                "AntiAlias",
                PdfValue.Boolean(antiAlias)
            }
        };
        if (bounds.HasValue)
            entries["BBox"] = bounds.Value.Value;
        return entries;
    }

    private static void Check(PdfColorSpace space, PdfFunction function, int inputs)
    {
        if (space == null || function == null)
            throw new ArgumentNullException(nameof(space));
        if (space.IsPattern || function.InputComponents != inputs || function.OutputComponents != space.Components)
            throw new ArgumentException("A shading function must match its input dimension and non-pattern color channels.");
    }
}

// Original mesh framing validator. It never rasterizes or runs arbitrary functions.
internal static class PdfMeshValidator
{
    internal static void Validate(PdfByteStore bytes, int type, int coordinates, int components, int flags, int channels, int columns, PdfOperationContext context)
    {
        long bit = 0, total = checked(bytes.Length * 8), vertices = 0;
        bool previous = false;
        if (type == 4 || type == 5)
        {
            long bits = (type == 4 ? flags : 0) + coordinates * 2L + components * (long)channels, record = ((bits + 7) / 8) * 8;
            while (bit < total)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                long begin = bit;
                if (total - bit < record)
                    throw Error();
                int flag = type == 4 ? Read(flags) : 0;
                if (type == 4)
                {
                    if (!previous && flag != 0 || flag > 2)
                        throw Error();
                    int count = flag == 0 ? 3 : 1;
                    if (total - begin < count * record)
                        throw Error();
                    bit = begin + count * record;
                    vertices += count;
                    previous = true;
                }
                else
                {
                    bit = begin + record;
                    vertices++;
                }
            }

            if (type == 4 && vertices < 3 || type == 5 && (vertices < columns * 2L || vertices % columns != 0))
                throw Error();
            return;
        }

        while (bit < total)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            int flag = Read(flags);
            if (flag > 3 || !previous && flag != 0)
                throw Error();
            int points = type == 6 ? (flag == 0 ? 12 : 8) : (flag == 0 ? 16 : 12), colors = flag == 0 ? 4 : 2;
            long remaining = points * 2L * coordinates + colors * (long)channels * components;
            if (remaining > total - bit)
                throw Error();
            bit += remaining;
            bit = ((bit + 7) / 8) * 8;
            if (bit > total)
                throw Error();
            previous = true;
        }

        if (!previous)
            throw Error();
        int Read(int count)
        {
            if (total - bit < count)
                throw Error();
            int value = 0;
            for (int i = 0; i < count; i++, bit++)
                value = (value << 1) | ((bytes.At(bit / 8) >> (7 - (int)(bit % 8))) & 1);
            return value;
        }

        PdfException Error() => PdfWriterCore.Error(context, "Graphics.MeshFraming", "A mesh stream is truncated or has invalid vertex/patch continuity.");
    }
}
