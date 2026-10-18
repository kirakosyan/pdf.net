using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf.Html;
/// <summary>Explicit immutable file/data-URI snapshots mapped to virtual HTTP(S) addresses. Browser file access is never enabled.</summary>
public static class HtmlAssets
{
    /// <summary>Snapshots one relative file from a caller-protected root. The caller must prevent concurrent path/link mutation and unauthorized hard links throughout capture.</summary>
    public static PdfHtmlResource ReadFile(string protectedRoot, string relativePath, Uri address, string mediaType, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfHtmlResource? result = null;
        PdfFont.Run("CaptureHtmlFile", "managed.html.resources", policy, cancellationToken, context => result = CaptureFile(protectedRoot, relativePath, address, mediaType, context), PdfCapability.RenderHtml);
        return result!;
    }

    /// <summary>Captures a file under the existing job context. Rejects absolute/traversal paths, device paths and reparse/symbolic-link ancestors. A mutable untrusted directory is not an authorized root.</summary>
    public static PdfHtmlResource CaptureFile(string protectedRoot, string relativePath, Uri address, string mediaType, PdfOperationContext context)
    {
        Validate(address, mediaType, context);
        if (string.IsNullOrEmpty(protectedRoot) || protectedRoot.Length > 4096 || string.IsNullOrEmpty(relativePath) || relativePath.Length > 4096 || Path.IsPathRooted(relativePath) || relativePath.Any(c => c < 32 || c == ':' || c == '%') || relativePath.Split('/', '\\').Any(s => s.Length == 0 || s == "." || s == ".." || s.EndsWith(".", StringComparison.Ordinal) || s.EndsWith(" ", StringComparison.Ordinal)))
            throw HtmlResourceLoader.Error(context, "LocalPathDenied", "The explicit asset path is outside the protected relative-file profile.");
        context.Consume(PdfResourceKind.ExpandedBytes, 131072);
        context.Consume(PdfResourceKind.Objects, 4);
        foreach (string segment in relativePath.Split('/', '\\'))
        {
            string stem = segment.Split('.')[0].ToUpperInvariant();
            if (new[]
            {
                "CON",
                "PRN",
                "AUX",
                "NUL",
                "CLOCK$",
                "CONIN$",
                "CONOUT$"
            }.Contains(stem) || stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && stem[3] >= '0' && stem[3] <= '9')
                throw HtmlResourceLoader.Error(context, "LocalPathDenied", "Device names are outside the explicit regular-asset profile.");
        }

        string root = Path.GetFullPath(protectedRoot);
        if (!Path.IsPathRooted(protectedRoot) || root.StartsWith("\\\\", StringComparison.Ordinal) || root.StartsWith("//", StringComparison.Ordinal))
            throw HtmlResourceLoader.Error(context, "LocalPathDenied", "The asset root must be an explicit absolute local directory.");
        string path = Path.GetFullPath(Path.Combine(root, relativePath.Replace('\\', Path.DirectorySeparatorChar).Replace('/', Path.DirectorySeparatorChar)));
        var comparison = Path.DirectorySeparatorChar == '\\' ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        string prefix = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(prefix, comparison))
            throw HtmlResourceLoader.Error(context, "LocalPathDenied", "The explicit asset path escapes its protected root.");
        try
        {
            // Check the entire chain, including ancestors above the supplied root. Ownership protects the check/open interval.
            string? ancestor = path;
            while (ancestor != null)
            {
                context.Checkpoint();
                if ((File.GetAttributes(ancestor) & FileAttributes.ReparsePoint) != 0)
                    throw HtmlResourceLoader.Error(context, "LocalPathDenied", "A symbolic link or reparse point is outside the protected file profile.");
                ancestor = Path.GetDirectoryName(ancestor);
            }

            using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
            return new(address, mediaType, PdfHtmlResource.ReadCore(input, 16777216, context));
        }
        catch (IOException)
        {
            context.Checkpoint();
            throw HtmlResourceLoader.Error(context, "LocalFileIo", "An explicitly supplied protected asset could not be captured; its path is omitted.");
        }
        catch (UnauthorizedAccessException)
        {
            context.Checkpoint();
            throw HtmlResourceLoader.Error(context, "LocalFileIo", "Access to an explicit protected asset failed; its path is omitted.");
        }
    }

    /// <summary>Decodes one bounded data URI into an immutable virtual resource. This performs no network or filesystem work.</summary>
    public static PdfHtmlResource ReadDataUri(string dataUri, Uri address, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfHtmlResource? result = null;
        PdfFont.Run("CaptureHtmlDataUri", "managed.html.resources", policy, cancellationToken, context => result = CaptureDataUri(dataUri, address, context), PdfCapability.RenderHtml);
        return result!;
    }

    /// <summary>Strict percent/base64 decoding under an existing job budget, with 16 MiB encoded and decoded ceilings. No payload appears in diagnostics.</summary>
    public static PdfHtmlResource CaptureDataUri(string dataUri, Uri address, PdfOperationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        if (dataUri == null)
            throw new ArgumentNullException(nameof(dataUri));
        context.Checkpoint();
        if (dataUri.Length > 16777216 || !dataUri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            throw HtmlResourceLoader.Error(context, "DataUriExtent", "The data URI exceeds its explicit encoded extent or scheme.");
        int comma = dataUri.IndexOf(',');
        if (comma < 5 || comma > 261)
            throw HtmlResourceLoader.Error(context, "DataUriSyntax", "A bounded data URI media header and separator are required.");
        context.Consume(PdfResourceKind.ExpandedBytes, (comma + 1L) * 4 + 1024);
        string media = dataUri.Substring(5, comma - 5);
        bool base64 = media.EndsWith(";base64", StringComparison.OrdinalIgnoreCase);
        if (base64)
            media = media.Substring(0, media.Length - 7);
        if (media.Length == 0)
            media = "text/plain;charset=US-ASCII";
        else if (media[0] == ';')
            media = "text/plain" + media;
        Validate(address, media, context);
        var builder = new PdfByteBuilder(context);
        if (base64)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, (dataUri.Length - comma - 1L) * 4 + 256);
            string encoded = dataUri.Substring(comma + 1);
            if (encoded.Length % 4 != 0 || encoded.Any(c => !(c >= 'A' && c <= 'Z' || c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '+' || c == '/' || c == '=')))
                throw HtmlResourceLoader.Error(context, "DataUriSyntax", "The data URI has malformed base64; payload is omitted.");
            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(encoded);
            }
            catch (FormatException)
            {
                throw HtmlResourceLoader.Error(context, "DataUriSyntax", "The data URI has malformed base64; payload is omitted.");
            }

            for (int i = 0; i < decoded.Length; i++)
            {
                if ((i & 4095) == 0)
                    context.Checkpoint();
                builder.Add(decoded[i]);
            }
        }
        else
        {
            for (int i = comma + 1; i < dataUri.Length; i++)
            {
                if ((i & 4095) == 0)
                    context.Checkpoint();
                char value = dataUri[i];
                if (value == '%')
                {
                    if (i + 2 >= dataUri.Length || Hex(dataUri[i + 1]) < 0 || Hex(dataUri[i + 2]) < 0)
                        throw HtmlResourceLoader.Error(context, "DataUriSyntax", "The data URI has malformed percent encoding; payload is omitted.");
                    builder.Add((byte)(Hex(dataUri[i + 1]) * 16 + Hex(dataUri[i + 2])));
                    i += 2;
                }
                else if (value <= 32 || value >= 127 || value == '#')
                    throw HtmlResourceLoader.Error(context, "DataUriSyntax", "The data URI requires exact ASCII or percent-encoded octets; payload is omitted.");
                else
                    builder.Add((byte)value);
            }
        }

        return new(address, media, builder.Build());
    }

    private static int Hex(char c) => c >= '0' && c <= '9' ? c - '0' : c >= 'a' && c <= 'f' ? c - 'a' + 10 : c >= 'A' && c <= 'F' ? c - 'A' + 10 : -1;
    private static void Validate(Uri address, string mediaType, PdfOperationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));
        context.Checkpoint();
        PdfHtmlSource.AddressValue(address);
        if (string.IsNullOrEmpty(mediaType) || mediaType.Length > 256 || mediaType.Any(c => c < 32 || c > 126))
            throw HtmlResourceLoader.Error(context, "AssetMediaType", "An exact bounded ASCII asset media type is required.");
    }
}
