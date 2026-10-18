using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable explicitly supplied browser resource. No path or URL is fetched by creating it.</summary>
public sealed class PdfHtmlResource
{
    private readonly PdfByteStore _bytes;
    internal PdfHtmlResource(Uri address, string mediaType, PdfByteStore bytes)
    {
        Address = address;
        MediaType = mediaType;
        _bytes = bytes;
    }

    /// <summary>Gets the exact absolute HTTP(S) address used only for resource matching.</summary>
    public Uri Address { get; }
    /// <summary>Gets the explicit response Content-Type.</summary>
    public string MediaType { get; }
    /// <summary>Gets the captured immutable byte length.</summary>
    public long Length => _bytes.Length;

    /// <summary>Opens an independent read-only content view; no caller input stream is retained.</summary>
    public Stream OpenContent() => _bytes.Open(0, _bytes.Length);
    /// <summary>Captures up to 16 MiB from a borrowed resource stream with explicit media type and virtual address.</summary>
    public static PdfHtmlResource Read(Uri address, string mediaType, Stream input, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfHtmlSource.AddressValue(address);
        if (string.IsNullOrEmpty(mediaType) || mediaType.Length > 256 || mediaType.Any(c => c < 32 || c > 126))
            throw new ArgumentException("A bounded ASCII Content-Type without control characters is required.", nameof(mediaType));
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable borrowed resource stream is required.", nameof(input));
        PdfHtmlResource? result = null;
        PdfFont.Run("CaptureHtmlResource", "managed.html", policy, cancellationToken, context => result = new(address, mediaType, ReadCore(input, 16777216, context)), PdfCapability.RenderHtml);
        return result!;
    }

    internal static PdfByteStore ReadCore(Stream input, long maximum, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.Objects, 2);
        using var lease = new BorrowedStream(input, true, context);
        context.Consume(PdfResourceKind.ExpandedBytes, 65536);
        var buffer = new byte[65536];
        var builder = new PdfByteBuilder(context);
        int count;
        while ((count = lease.Read(buffer, 0, buffer.Length)) != 0)
        {
            if (count > maximum - builder.Length)
                throw PdfWriterCore.Error(context, "Html.InputExtent", "The explicitly supplied HTML/resource exceeds its supported source extent.");
            for (int i = 0; i < count; i++)
                builder.Add(buffer[i]);
            context.Checkpoint();
        }

        return builder.Build();
    }
}

/// <summary>Captured markup or an explicitly addressed main resource and immutable local resource map. It authorizes no network or filesystem access.</summary>
public sealed class PdfHtmlSource
{
    private PdfHtmlSource(string? markup, Uri address, IReadOnlyList<PdfHtmlResource> resources)
    {
        Markup = markup;
        Address = address;
        Resources = resources;
    }

    /// <summary>Gets explicit logical HTML markup, or null when the main document requires a supplied resource or explicit renderer acquisition policy.</summary>
    public string? Markup { get; }
    /// <summary>Gets the absolute virtual main address and relative-resource base.</summary>
    public Uri Address { get; }
    /// <summary>Gets the explicit resource map; document creation never follows these URLs.</summary>
    public IReadOnlyList<PdfHtmlResource> Resources { get; }
    /// <summary>Gets the successful source preparation report.</summary>
    public PdfOperationReport OperationReport { get; private set; } = null!;

    /// <summary>Captures well-formed Unicode HTML and explicit local resources. The default base is a reserved virtual HTTPS origin.</summary>
    public static PdfHtmlSource FromHtml(string markup, Uri? address = null, IEnumerable<PdfHtmlResource>? resources = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (markup == null)
            throw new ArgumentNullException(nameof(markup));
        return Create(markup, address ?? new Uri("https://fluent-pdf.invalid/document"), resources, null, policy, cancellationToken);
    }

    /// <summary>Uses an exact HTTP(S) document address. Bytes require supplied Resources or an explicit renderer acquisition policy; this method performs no network request.</summary>
    public static PdfHtmlSource FromResource(Uri address, IEnumerable<PdfHtmlResource> resources, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Create(null, address, resources ?? throw new ArgumentNullException(nameof(resources)), null, policy, cancellationToken);
    /// <summary>Captures UTF-8 HTML from a borrowed file/document stream. A UTF-8 BOM is allowed; malformed Unicode rejects without guessing another encoding.</summary>
    public static PdfHtmlSource Read(Stream input, Uri? address = null, IEnumerable<PdfHtmlResource>? resources = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable borrowed HTML stream is required.", nameof(input));
        return Create(null, address ?? new Uri("https://fluent-pdf.invalid/document"), resources, input, policy, cancellationToken);
    }

    private static PdfHtmlSource Create(string? markup, Uri address, IEnumerable<PdfHtmlResource>? resources, Stream? input, PdfOperationPolicy? policy, CancellationToken token)
    {
        AddressValue(address);
        PdfHtmlSource? result = null;
        var report = PdfFont.Run("PrepareHtml", "managed.html", policy, token, context =>
        {
            context.Consume(PdfResourceKind.Objects, 3);
            if (input != null)
            {
                var bytes = PdfHtmlResource.ReadCore(input, 8388608, context);
                context.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 4L + 128);
                var copy = new byte[checked((int)bytes.Length)];
                using var raw = bytes.Open(0, bytes.Length);
                int at = 0, count;
                while ((count = raw.Read(copy, at, Math.Min(65536, copy.Length - at))) != 0)
                {
                    at += count;
                    context.Checkpoint();
                }

                int start = copy.Length >= 3 && copy[0] == 239 && copy[1] == 187 && copy[2] == 191 ? 3 : 0;
                try
                {
                    markup = new UTF8Encoding(false, true).GetString(copy, start, copy.Length - start);
                }
                catch (DecoderFallbackException)
                {
                    throw PdfWriterCore.Error(context, "Html.TextEncoding", "HTML file input must contain well-formed UTF-8.");
                }
            }

            if (markup != null)
            {
                if (markup.Length > 4194304)
                    throw PdfWriterCore.Error(context, "Html.InputExtent", "The HTML markup exceeds the supported source extent.");
                context.Consume(PdfResourceKind.ExpandedBytes, markup.Length * 2L + 256);
                for (int i = 0; i < markup.Length; i++)
                {
                    if ((i & 1023) == 0)
                        context.Checkpoint();
                    if (char.IsHighSurrogate(markup[i]))
                    {
                        if (++i >= markup.Length || !char.IsLowSurrogate(markup[i]))
                            throw PdfWriterCore.Error(context, "Html.TextEncoding", "HTML markup must contain well-formed Unicode.");
                    }
                    else if (char.IsLowSurrogate(markup[i]))
                        throw PdfWriterCore.Error(context, "Html.TextEncoding", "HTML markup must contain well-formed Unicode.");
                }
            }

            context.Consume(PdfResourceKind.ExpandedBytes, 512);
            var captured = new List<PdfHtmlResource>();
            var addresses = new HashSet<string>(StringComparer.Ordinal);
            long total = 0;
            if (resources != null)
            {
                using var enumerator = resources.GetEnumerator();
                while (true)
                {
                    context.Checkpoint();
                    if (!enumerator.MoveNext())
                        break;
                    context.Consume(PdfResourceKind.Objects, 1);
                    context.Consume(PdfResourceKind.ExpandedBytes, 256);
                    var resource = enumerator.Current;
                    if (resource == null || captured.Count == 128 || !addresses.Add(resource.Address.AbsoluteUri) || (total += resource.Length) > 67108864)
                        throw PdfWriterCore.Error(context, "Html.ResourceInventory", "Resources must be nonnull, uniquely addressed and within the 128-resource/64 MiB source inventory.");
                    if (markup != null && resource.Address == address)
                        throw PdfWriterCore.Error(context, "Html.ResourceInventory", "The main markup address cannot also name a supplied resource.");
                    captured.Add(resource);
                }
            }

            result = new(markup, address, new ReadOnlyCollection<PdfHtmlResource>(captured));
        }, PdfCapability.RenderHtml);
        result!.OperationReport = report;
        return result;
    }

    internal static void AddressValue(Uri address)
    {
        if (address == null || !address.IsAbsoluteUri || address.Scheme != "https" && address.Scheme != "http" || address.AbsoluteUri.Length > 4096 || address.UserInfo.Length != 0 || address.Fragment.Length != 0)
            throw new ArgumentException("A bounded absolute HTTP(S) virtual address without credentials or a fragment is required.", nameof(address));
    }
}
