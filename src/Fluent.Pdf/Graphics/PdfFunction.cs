using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable PDF interpolation/tint function, preserved as vector resource data without executing document code.</summary>
public sealed class PdfFunction
{
    private PdfFunction(PdfValue definition, int inputs, int outputs, double[] domain, double[] range)
    {
        Definition = definition;
        InputComponents = inputs;
        OutputComponents = outputs;
        Domain = new ReadOnlyCollection<double>(domain);
        Range = new ReadOnlyCollection<double>(range);
    }

    /// <summary>Gets immutable direct function data; nested sampled-function streams are hoisted by graphics resource embedding.</summary>
    public PdfValue Definition { get; }
    /// <summary>Gets its input channel count.</summary>
    public int InputComponents { get; }
    /// <summary>Gets its output channel count.</summary>
    public int OutputComponents { get; }
    /// <summary>Gets paired input bounds.</summary>
    public IReadOnlyList<double> Domain { get; }
    /// <summary>Gets paired output bounds, or an empty array when omitted for an exponential/stitching function.</summary>
    public IReadOnlyList<double> Range { get; }
    /// <summary>Gets its successful bounded construction report.</summary>
    public PdfOperationReport OperationReport { get; private set; } = null!;

    /// <summary>Creates a type-2 exponential interpolation function with exact endpoint vectors.</summary>
    public static PdfFunction Exponential(double[] start, double[] end, double exponent = 1, double domainMinimum = 0, double domainMaximum = 1, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (start == null || end == null || start.Length < 1 || start.Length > 32 || start.Length != end.Length)
            throw new ArgumentException("One through 32 matching endpoint channels are required.");
        PdfGraphicsNumbers.Finite(exponent);
        if (domainMaximum <= domainMinimum)
            throw new ArgumentOutOfRangeException(nameof(domainMaximum));
        PdfGraphicsNumbers.Finite(domainMinimum);
        PdfGraphicsNumbers.Finite(domainMaximum);
        if (exponent != Math.Truncate(exponent) && domainMinimum < 0 || exponent < 0 && domainMinimum <= 0 && domainMaximum >= 0)
            throw new ArgumentException("The function exponent is undefined over the requested domain.");
        PdfFunction? result = null;
        var report = PdfFont.Run("CreateExponentialFunction", "managed.graphics", policy, cancellationToken, context =>
        {
            context.Consume(PdfResourceKind.Objects, start.Length * 2L + 8);
            context.Consume(PdfResourceKind.ExpandedBytes, start.Length * 256L + 1024);
            foreach (double value in start.Concat(end))
                PdfGraphicsNumbers.Finite(value);
            var domain = new[]
            {
                domainMinimum,
                domainMaximum
            };
            result = new PdfFunction(PdfGraphicsNumbers.Dict(("FunctionType", PdfValue.Integer(2)), ("Domain", PdfGraphicsNumbers.Array(domain)), ("C0", PdfGraphicsNumbers.Array(start)), ("C1", PdfGraphicsNumbers.Array(end)), ("N", PdfValues.Real(exponent))), 1, start.Length, domain, System.Array.Empty<double>());
        }, PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Creates a type-0 sampled function with an exact packed sample plane. Samples are copied only after size and budget preflight.</summary>
    public static PdfFunction Sampled(double[] domain, double[] range, int[] size, int bitsPerSample, byte[] samples, int order = 1, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (domain == null || range == null || size == null || samples == null)
            throw new ArgumentNullException(nameof(domain));
        Bounds(domain, 32);
        Bounds(range, 32);
        if (size.Length != domain.Length / 2 || size.Any(x => x < 1) || order != 1 && order != 3 || !new[]
        {
            1,
            2,
            4,
            8,
            12,
            16,
            24,
            32
        }.Contains(bitsPerSample))
            throw new ArgumentException("Sample dimensions, interpolation order or bit depth are invalid.");
        PdfFunction? result = null;
        var report = PdfFont.Run("CreateSampledFunction", "managed.graphics", policy, cancellationToken, context =>
        {
            long count = range.Length / 2;
            foreach (int dimension in size)
            {
                context.Checkpoint();
                if (count > long.MaxValue / dimension)
                    throw PdfWriterCore.Error(context, "Graphics.FunctionSize", "The sampled function size overflows its bounded representation.");
                count *= dimension;
            }

            if (count > (long.MaxValue - 7) / bitsPerSample || ((count * bitsPerSample + 7) / 8) != samples.LongLength)
                throw PdfWriterCore.Error(context, "Graphics.FunctionSize", "Packed samples must exactly cover the declared function grid.");
            context.Consume(PdfResourceKind.Objects, size.Length + range.Length + 16L);
            context.Consume(PdfResourceKind.ExpandedBytes, samples.LongLength * 2 + size.Length * 256L + 4096);
            var bytes = (byte[])samples.Clone();
            var dictionary = PdfGraphicsNumbers.Dict(("FunctionType", PdfValue.Integer(0)), ("Domain", PdfGraphicsNumbers.Array(domain)), ("Range", PdfGraphicsNumbers.Array(range)), ("Size", PdfValue.Array(size.Select(x => PdfValue.Integer(x)).ToArray())), ("BitsPerSample", PdfValue.Integer(bitsPerSample)), ("Order", PdfValue.Integer(order)));
            var builder = new PdfByteBuilder(context);
            foreach (byte b in bytes)
                builder.Add(b);
            var store = builder.Build();
            result = new PdfFunction(PdfValue.StreamValue(dictionary, store, 0, store.Length), size.Length, range.Length / 2, (double[])domain.Clone(), (double[])range.Clone());
        }, PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Creates a type-3 piecewise function with explicit ordered bounds and per-piece input encoding.</summary>
    public static PdfFunction Stitch(PdfFunction[] functions, double[] bounds, double[] encode, double domainMinimum = 0, double domainMaximum = 1, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (functions == null || bounds == null || encode == null || functions.Length < 1 || functions.Length > 1024 || bounds.Length != functions.Length - 1 || encode.Length != functions.Length * 2 || functions.Any(x => x == null || x.InputComponents != 1 || x.OutputComponents != functions[0].OutputComponents))
            throw new ArgumentException("Matching one-input piecewise functions, bounds and encode pairs are required.");
        PdfGraphicsNumbers.Finite(domainMinimum);
        PdfGraphicsNumbers.Finite(domainMaximum);
        if (domainMaximum <= domainMinimum)
            throw new ArgumentOutOfRangeException(nameof(domainMaximum));
        double previous = domainMinimum;
        foreach (double bound in bounds)
        {
            PdfGraphicsNumbers.Finite(bound);
            if (bound <= previous || bound >= domainMaximum)
                throw new ArgumentException("Piecewise bounds must be strictly increasing inside the domain.");
            previous = bound;
        }

        foreach (double value in encode)
            PdfGraphicsNumbers.Finite(value);
        PdfFunction? result = null;
        var report = PdfFont.Run("CreateStitchingFunction", "managed.graphics", policy, cancellationToken, context =>
        {
            context.Consume(PdfResourceKind.Objects, functions.Length * 4L + 8);
            context.Consume(PdfResourceKind.ExpandedBytes, functions.Length * 512L + 1024);
            var domain = new[]
            {
                domainMinimum,
                domainMaximum
            };
            result = new PdfFunction(PdfGraphicsNumbers.Dict(("FunctionType", PdfValue.Integer(3)), ("Domain", PdfGraphicsNumbers.Array(domain)), ("Functions", PdfValue.Array(functions.Select(x => x.Definition).ToArray())), ("Bounds", PdfGraphicsNumbers.Array(bounds)), ("Encode", PdfGraphicsNumbers.Array(encode))), 1, functions[0].OutputComponents, domain, System.Array.Empty<double>());
        }, PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    internal static void Bounds(double[] values, int maximum)
    {
        if (values.Length < 2 || values.Length % 2 != 0 || values.Length > maximum * 2)
            throw new ArgumentException("Paired finite channel bounds are required.");
        for (int i = 0; i < values.Length; i += 2)
        {
            PdfGraphicsNumbers.Finite(values[i]);
            PdfGraphicsNumbers.Finite(values[i + 1]);
            if (values[i + 1] <= values[i])
                throw new ArgumentException("Channel bounds must be increasing.");
        }
    }
}
