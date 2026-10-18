using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit offline source syntax. None performs resource discovery or executes document actions.</summary>
public enum PdfInputFormat
{
    /// <summary>Strict Unicode text, divided into paragraphs at blank lines.</summary>
    Text,
    /// <summary>The documented bounded Markdown source profile.</summary>
    Markdown,
    /// <summary>The version-one JSON document and scalar-binding schema.</summary>
    Json,
    /// <summary>The version-one XML document schema, with DTDs and resolvers prohibited.</summary>
    Xml
}

/// <summary>An explicitly supplied reusable drawing; names in source refer only to these captured resources.</summary>
public sealed class PdfInputDrawing
{
    /// <summary>Captures drawing geometry without opening any resource or selecting a provider.</summary>
    public PdfInputDrawing(PdfDrawing drawing, PdfRect sourceBounds, double width, double height)
    {
        Drawing = drawing ?? throw new ArgumentNullException(nameof(drawing));
        if (sourceBounds.Width <= 0 || sourceBounds.Height <= 0)
            throw new ArgumentException("Positive source bounds are required.", nameof(sourceBounds));
        PdfGraphicsNumbers.Finite(width);
        PdfGraphicsNumbers.Finite(height);
        if (width <= 0 || height <= 0)
            throw new ArgumentOutOfRangeException(nameof(width));
        SourceBounds = sourceBounds;
        Width = width;
        Height = height;
    }

    /// <summary>Gets the supplied drawing.</summary>
    public PdfDrawing Drawing { get; }
    /// <summary>Gets the source coordinate rectangle.</summary>
    public PdfRect SourceBounds { get; }
    /// <summary>Gets the requested physical width.</summary>
    public double Width { get; }
    /// <summary>Gets the requested physical height.</summary>
    public double Height { get; }
}

/// <summary>Immutable explicit generation, font/style and named-resource choices for source conversion.</summary>
public sealed class PdfInputConversionOptions
{
    /// <summary>Captures at most 4096 styles/resources. The body style is mandatory; other semantic styles are required when used. No default font or resource loader is installed.</summary>
    public PdfInputConversionOptions(PdfGenerationOptions generation, IReadOnlyDictionary<string, PdfFlowTextStyle> styles, IReadOnlyDictionary<string, PdfInputDrawing>? resources = null, int maximumInputBytes = 4194304, int maximumNodes = 100000)
    {
        Generation = generation ?? throw new ArgumentNullException(nameof(generation));
        if (styles == null)
            throw new ArgumentNullException(nameof(styles));
        if (styles.Count > 4096 || resources?.Count > 4096 || maximumInputBytes < 1 || maximumInputBytes > 67108864 || maximumNodes < 1 || maximumNodes > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumInputBytes));
        var copy = new Dictionary<string, PdfFlowTextStyle>(StringComparer.Ordinal);
        foreach (var item in styles)
            copy.Add(Guard.Text(item.Key, nameof(styles), 128), item.Value ?? throw new ArgumentException("Styles must be nonnull."));
        if (!copy.ContainsKey("body"))
            throw new ArgumentException("An explicit body style is required.", nameof(styles));
        Styles = new ReadOnlyDictionary<string, PdfFlowTextStyle>(copy);
        var assets = new Dictionary<string, PdfInputDrawing>(StringComparer.Ordinal);
        if (resources != null)
            foreach (var item in resources)
                assets.Add(Guard.Text(item.Key, nameof(resources), 128), item.Value ?? throw new ArgumentException("Resources must be nonnull."));
        Resources = new ReadOnlyDictionary<string, PdfInputDrawing>(assets);
        MaximumInputBytes = maximumInputBytes;
        MaximumNodes = maximumNodes;
    }

    /// <summary>Gets generation layout, culture and precommit hooks.</summary>
    public PdfGenerationOptions Generation { get; }
    /// <summary>Gets explicit styles by ordinal name.</summary>
    public IReadOnlyDictionary<string, PdfFlowTextStyle> Styles { get; }
    /// <summary>Gets captured drawings by ordinal name; source paths and URLs are never resolved.</summary>
    public IReadOnlyDictionary<string, PdfInputDrawing> Resources { get; }
    /// <summary>Gets the source-byte ceiling, in addition to operation limits.</summary>
    public int MaximumInputBytes { get; }
    /// <summary>Gets the total syntax/model node ceiling.</summary>
    public int MaximumNodes { get; }
}

/// <summary>Original bounded offline Unicode/Markdown/JSON/XML conversion through shared-context generation.</summary>
public static class PdfInputConversion
{
    private static PdfGenerationTemplate<Stream> Template(PdfInputFormat format, PdfInputConversionOptions options)
    {
        if (!Enum.IsDefined(typeof(PdfInputFormat), format))
            throw new ArgumentOutOfRangeException(nameof(format));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        return new("input-conversion", (generation, input) =>
        {
            if (input == null || !input.CanRead)
                throw new ArgumentException("A readable borrowed input is required.", nameof(input));
            var parser = new PdfInputParser(generation, options);
            parser.Parse(Read(input, options.MaximumInputBytes, generation.OperationContext), format);
        });
    }

    /// <summary>Converts a borrowed source at its current position into a caller-owned document.</summary>
    public static PdfDocument Generate(Stream input, PdfInputFormat format, PdfInputConversionOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfDataGeneration.Generate(new[] { input }, Template(format, options), options.Generation, providers, policy, cancellationToken);
    /// <summary>Parses, lays out and serializes under one context, leaving both caller streams open. Stream output may contain a prefix on failure.</summary>
    public static PdfOperationReport Write(Stream input, PdfInputFormat format, PdfInputConversionOptions options, PdfProviderRegistry providers, Stream output, PdfWriteOptions? writeOptions = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfDataGeneration.Write(new[] { input }, Template(format, options), options.Generation, providers, output, writeOptions, policy, cancellationToken);
    /// <summary>Converts and serializes privately before atomic file replacement; failure preserves an existing destination.</summary>
    public static PdfOperationReport WriteFile(Stream input, PdfInputFormat format, PdfInputConversionOptions options, PdfProviderRegistry providers, string path, PdfWriteOptions? writeOptions = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => PdfDataGeneration.WriteFile(new[] { input }, Template(format, options), options.Generation, providers, path, writeOptions, policy, cancellationToken);
    private static string Read(Stream input, int maximum, PdfOperationContext c)
    {
        using var lease = new BorrowedStream(input, true, c);
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var chunk = new byte[8192];
        using var buffer = new PdfCodecBufferStream(c, maximum);
        while (true)
        {
            c.Checkpoint();
            int n = lease.Read(chunk, 0, (int)Math.Min(chunk.Length, maximum - buffer.BufferedLength + 1L));
            if (n == 0)
                break;
            if (buffer.BufferedLength + n > maximum)
                throw Error(c, "InputLimit", "The explicit source-byte ceiling was exceeded.");
            buffer.Write(chunk, 0, n);
        }

        var store = buffer.Finish();
        c.Consume(PdfResourceKind.ExpandedBytes, store.Length * 6 + 128);
        var raw = new byte[(int)store.Length];
        using (var captured = store.Open(0, store.Length))
        {
            int at = 0;
            while (at < raw.Length)
            {
                c.Checkpoint();
                int n = captured.Read(raw, at, raw.Length - at);
                if (n == 0)
                    throw Error(c, "InputLength", "Captured source ended unexpectedly.");
                at += n;
            }
        }

        Encoding encoding = new UTF8Encoding(false, true);
        int skip = 0;
        if (raw.Length >= 4 && raw[0] == 255 && raw[1] == 254 && raw[2] == 0 && raw[3] == 0)
        {
            encoding = new UTF32Encoding(false, false, true);
            skip = 4;
        }
        else if (raw.Length >= 4 && raw[0] == 0 && raw[1] == 0 && raw[2] == 254 && raw[3] == 255)
        {
            encoding = new UTF32Encoding(true, false, true);
            skip = 4;
        }
        else if (raw.Length >= 2 && raw[0] == 255 && raw[1] == 254)
        {
            encoding = new UnicodeEncoding(false, false, true);
            skip = 2;
        }
        else if (raw.Length >= 2 && raw[0] == 254 && raw[1] == 255)
        {
            encoding = new UnicodeEncoding(true, false, true);
            skip = 2;
        }
        else if (raw.Length >= 3 && raw[0] == 239 && raw[1] == 187 && raw[2] == 191)
            skip = 3;
        try
        {
            var text = encoding.GetString(raw, skip, raw.Length - skip);
            c.Checkpoint();
            return text;
        }
        catch (DecoderFallbackException)
        {
            throw Error(c, "Unicode", "The source encoding contains malformed Unicode.");
        }
    }

    internal static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Input." + code, message);
}
