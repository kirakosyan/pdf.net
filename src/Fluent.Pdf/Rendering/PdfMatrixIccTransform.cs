using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>A detached explicit RGB ICC matrix/TRC color conversion and its bounded operation report.</summary>
public sealed class PdfColorTransformation
{
    internal PdfColorTransformation(PdfColor color, PdfOperationReport report)
    {
        Color = color;
        OperationReport = report;
    }
    /// <summary>Gets color components in the exact requested output profile.</summary>
    public PdfColor Color
    {
        get;
    }
    /// <summary>Gets operation diagnostics/accounting.</summary>
    public PdfOperationReport OperationReport
    {
        get;
    }
}
/// <summary>Original media-relative ICC RGB matrix/TRC conversion in D50 XYZ PCS. LUT, absolute/perceptual gamut mapping, black-point compensation and separations require a separately qualified provider.</summary>
public static class PdfMatrixIccTransform
{
    /// <summary>Transforms one normalized RGB color through explicit matrix/TRC profiles. Out-of-gamut results reject instead of silently clipping. No profile or engine is discovered.</summary>
    public static PdfColorTransformation Transform(PdfColor color, PdfColorSpace outputProfile, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (color == null || outputProfile == null)
            throw new ArgumentNullException(color == null ? nameof(color) : nameof(outputProfile));
        PdfColor? result = null;
        var report = PdfFont.Run("TransformMatrixIccColor", "managed.matrix-icc", policy, cancellationToken, c =>
        {
            c.Consume(PdfResourceKind.ExpandedBytes, 2048);
            var source = Read(color.ColorSpace, c);
            var target = Read(outputProfile, c);
            var rgb = color.Components;
            var linear = new[] { source.Curves[0](rgb[0]), source.Curves[1](rgb[1]), source.Curves[2](rgb[2]) };
            var xyz = Multiply(source.Matrix, linear);
            var mapped = Multiply(Inverse(target.Matrix, c), xyz);
            var converted = new double[3];
            for (int channel = 0; channel < 3; channel++)
            {
                c.Checkpoint();
                double value = mapped[channel], minimum = target.Curves[channel](0), maximum = target.Curves[channel](1);
                if (value < minimum - 1e-7 || value > maximum + 1e-7)
                    throw Error(c, "Gamut", "The requested color lies outside the explicit destination gamut; clipping is not authorized.");
                value = Math.Max(minimum, Math.Min(maximum, value));
                double lo = 0, hi = 1;
                for (int step = 0; step < 48; step++)
                {
                    c.Checkpoint();
                    double mid = (lo + hi) / 2;
                    if (target.Curves[channel](mid) < value)
                        lo = mid;
                    else
                        hi = mid;
                }
                converted[channel] = (lo + hi) / 2;
            }
            result = new(outputProfile, converted);
        }, PdfCapability.RenderColorManaged);
        return new(result!, report);
    }
    private static (double[] Matrix, Func<double, double>[] Curves) Read(PdfColorSpace profile, PdfOperationContext c)
    {
        if (profile.Components != 3 || profile.Definition.Kind != PdfValueKind.Array || profile.Definition.Items.Count != 2 || profile.Definition.Items[0].Kind != PdfValueKind.Name || profile.Definition.Items[0].NameValue != "ICCBased" || profile.Definition.Items[1].Kind != PdfValueKind.Stream)
            throw Error(c, "Profile", "Explicit complete RGB ICC profiles are required.");
        using var input = profile.Definition.Items[1].OpenRawStream();
        if (input.Length < 132 || input.Length > 16777216)
            throw Error(c, "Profile", "ICC extent is outside the matrix profile.");
        var bytes = PdfByteStore.Read(input, c);
        uint U32(long at)
        {
            if (at < 0 || at > bytes.Length - 4)
                throw Error(c, "Bounds", "An ICC matrix tag is truncated.");
            return (uint)bytes.At(at) << 24 | (uint)bytes.At(at + 1) << 16 | (uint)bytes.At(at + 2) << 8 | (uint)bytes.At(at + 3);
        }
        int U16(long at)
        {
            if (at < 0 || at > bytes.Length - 2)
                throw Error(c, "Bounds", "An ICC curve is truncated.");
            return bytes.At(at) << 8 | bytes.At(at + 1);
        }
        double Fixed(long at) => unchecked((int)U32(at)) / 65536d;
        string Tag(long at) => new(new[] { (char)bytes.At(at), (char)bytes.At(at + 1), (char)bytes.At(at + 2), (char)bytes.At(at + 3) });
        if (Tag(16) != "RGB " || Tag(20) != "XYZ ")
            throw Error(c, "Profile", "RGB input and XYZ PCS are required.");
        long count = U32(128);
        if (count > 4096 || count > (bytes.Length - 132) / 12)
            throw Error(c, "Tags", "A bounded ICC tag directory is required.");
        var tags = new Dictionary<string, (long Start, long Length)>(StringComparer.Ordinal);
        for (long i = 0; i < count; i++)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 128);
            long at = 132 + i * 12, start = U32(at + 4), length = U32(at + 8);
            string name = Tag(at);
            if (start < 132 || length < 8 || start > bytes.Length - length || tags.ContainsKey(name))
                throw Error(c, "Tags", "ICC tag extents and identities must be valid.");
            tags.Add(name, (start, length));
        }
        if (tags.Keys.Any(k => k == "A2B0" || k == "A2B1" || k == "A2B2" || k == "B2A0" || k == "B2A1" || k == "B2A2"))
            throw Error(c, "LutProfile", "LUT-based ICC profiles require a separate transform provider.");
        var matrix = new double[9];
        var curves = new Func<double, double>[3];
        string[] channels = { "r", "g", "b" };
        for (int channel = 0; channel < 3; channel++)
        {
            if (!tags.TryGetValue(channels[channel] + "XYZ", out var xyz) || xyz.Length < 20 || Tag(xyz.Start) != "XYZ " || !tags.TryGetValue(channels[channel] + "TRC", out var trc))
                throw Error(c, "MatrixTags", "Complete XYZ/TRC tags are required.");
            for (int row = 0; row < 3; row++)
                matrix[row * 3 + channel] = Fixed(xyz.Start + 8 + row * 4);
            string type = Tag(trc.Start);
            if (type == "curv")
            {
                if (trc.Length < 12)
                    throw Error(c, "Curve", "A complete curve header is required.");
                long n = U32(trc.Start + 8);
                if (n > 65536 || 12 + n * 2 > trc.Length)
                    throw Error(c, "Curve", "ICC curve extent exceeds the finite profile.");
                if (n == 0)
                    curves[channel] = x => x;
                else if (n == 1)
                {
                    double gamma = U16(trc.Start + 12) / 256d;
                    if (gamma <= 0 || gamma > 16)
                        throw Error(c, "Curve", "ICC gamma must be finite and positive.");
                    curves[channel] = x => Math.Pow(x, gamma);
                }
                else
                {
                    c.Consume(PdfResourceKind.ExpandedBytes, n * 8);
                    var samples = new double[(int)n];
                    for (int i = 0; i < n; i++)
                    {
                        c.Checkpoint();
                        samples[i] = U16(trc.Start + 12 + i * 2) / 65535d;
                        if (i > 0 && samples[i] < samples[i - 1])
                            throw Error(c, "Curve", "Sampled curves must be monotone.");
                    }
                    curves[channel] = x => { double at = x * (samples.Length - 1); int left = Math.Min(samples.Length - 2, (int)at); return samples[left] + (samples[left + 1] - samples[left]) * (at - left); };
                }
            }
            else if (type == "para")
            {
                if (trc.Length < 12)
                    throw Error(c, "Curve", "A complete parametric curve is required.");
                int function = U16(trc.Start + 8);
                int parameters = function == 0 ? 1 : function == 1 ? 3 : function == 2 ? 4 : function == 3 ? 5 : function == 4 ? 7 : 0;
                if (parameters == 0 || trc.Length < 12 + parameters * 4)
                    throw Error(c, "Curve", "Unknown or truncated parametric curve.");
                var p = new double[parameters];
                for (int i = 0; i < p.Length; i++)
                    p[i] = Fixed(trc.Start + 12 + i * 4);
                if (p[0] <= 0 || p[0] > 16 || function > 0 && p[1] <= 0)
                    throw Error(c, "Curve", "Parametric curve gamma/scaling is unsupported.");
                curves[channel] = x => function == 0 ? Math.Pow(x, p[0]) : function == 1 ? (x >= -p[2] / p[1] ? Math.Pow(p[1] * x + p[2], p[0]) : 0) : function == 2 ? (x >= -p[2] / p[1] ? Math.Pow(p[1] * x + p[2], p[0]) + p[3] : p[3]) : function == 3 ? (x >= p[4] ? Math.Pow(p[1] * x + p[2], p[0]) : p[3] * x) : (x >= p[4] ? Math.Pow(p[1] * x + p[2], p[0]) + p[5] : p[3] * x + p[6]);
            }
            else
                throw Error(c, "Curve", "Only curveType and parametricCurveType are supported.");
            double previous = double.MinValue;
            for (int step = 0; step <= 256; step++)
            {
                c.Checkpoint();
                double value = curves[channel](step / 256d);
                if (double.IsNaN(value) || double.IsInfinity(value) || value < previous)
                    throw Error(c, "Curve", "Curves must be finite and monotone on normalized RGB.");
                previous = value;
            }
        }
        _ = Inverse(matrix, c);
        return (matrix, curves);
    }
    private static double[] Multiply(double[] m, double[] v) => new[] { m[0] * v[0] + m[1] * v[1] + m[2] * v[2], m[3] * v[0] + m[4] * v[1] + m[5] * v[2], m[6] * v[0] + m[7] * v[1] + m[8] * v[2] };
    private static double[] Inverse(double[] m, PdfOperationContext c)
    {
        double d = m[0] * (m[4] * m[8] - m[5] * m[7]) - m[1] * (m[3] * m[8] - m[5] * m[6]) + m[2] * (m[3] * m[7] - m[4] * m[6]);
        if (Math.Abs(d) < 1e-12 || double.IsNaN(d) || double.IsInfinity(d))
            throw Error(c, "Matrix", "ICC primaries must form a finite invertible matrix.");
        return new[] { (m[4] * m[8] - m[5] * m[7]) / d, (m[2] * m[7] - m[1] * m[8]) / d, (m[1] * m[5] - m[2] * m[4]) / d, (m[5] * m[6] - m[3] * m[8]) / d, (m[0] * m[8] - m[2] * m[6]) / d, (m[2] * m[3] - m[0] * m[5]) / d, (m[3] * m[7] - m[4] * m[6]) / d, (m[1] * m[6] - m[0] * m[7]) / d, (m[0] * m[4] - m[1] * m[3]) / d };
    }
    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Color." + code, message);
}
