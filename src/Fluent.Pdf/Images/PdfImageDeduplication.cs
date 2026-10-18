using System.Collections.ObjectModel;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>An immutable per-operation image interning result; original assets and their reports remain unchanged.</summary>
public sealed class PdfImageDeduplicationResult
{
    internal PdfImageDeduplicationResult(PdfImageAsset[] images, int unique, PdfOperationReport report)
    {
        Images = new ReadOnlyCollection<PdfImageAsset>(images);
        UniqueCount = unique;
        OperationReport = report;
    }

    /// <summary>Gets input-order images with only identical safe assets sharing an instance.</summary>
    public IReadOnlyList<PdfImageAsset> Images { get; }
    /// <summary>Gets the number of distinct retained asset instances.</summary>
    public int UniqueCount { get; }
    /// <summary>Gets the completed shared-context comparison report.</summary>
    public PdfOperationReport OperationReport { get; }
}

public static partial class PdfImages
{
    /// <summary>Interns exact safe image graphs within one bounded batch, comparing bytes after hashing and retaining original-file/metadata identity.</summary>
    public static PdfImageDeduplicationResult Deduplicate(IEnumerable<PdfImageAsset> images, int maximumImages = 10000, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (images == null)
            throw new ArgumentNullException(nameof(images));
        if (maximumImages < 1 || maximumImages > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumImages));
        var output = new List<PdfImageAsset>();
        int unique = 0;
        var report = PdfFont.Run("DeduplicateImages", "managed.images", policy, cancellationToken, context =>
        {
            var buckets = new Dictionary<string, List<PdfImageAsset>>(StringComparer.Ordinal);
            var retained = new HashSet<PdfImageAsset>();
            using var enumerator = images.GetEnumerator();
            while (true)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                if (!enumerator.MoveNext())
                    break;
                if (output.Count == maximumImages)
                    throw Error(context, "Image.BatchLimit", "The image batch exceeds its explicit finite limit.");
                var image = enumerator.Current ?? throw new ArgumentException("Image batches cannot contain null.", nameof(images));
                context.Consume(PdfResourceKind.ExpandedBytes, 1024);
                if (!PdfImageIdentity.Safe(image.Definition, context))
                {
                    output.Add(image);
                    if (retained.Add(image))
                        unique++;
                    continue;
                }

                string digest = PdfImageIdentity.Hash(image, context);
                PdfImageAsset? match = null;
                if (buckets.TryGetValue(digest, out var candidates))
                {
                    foreach (var candidate in candidates)
                        if (PdfImageIdentity.Equal(image, candidate, context))
                        {
                            match = candidate;
                            break;
                        }
                }
                else
                    buckets.Add(digest, candidates = new());
                if (match == null)
                {
                    candidates!.Add(image);
                    match = image;
                }

                if (retained.Add(match))
                    unique++;
                output.Add(match);
            }
        }, PdfCapability.ImageCodecs);
        return new(output.ToArray(), unique, report);
    }
}

internal static class PdfImageIdentity
{
    private static readonly HashSet<string> SafeImageKeys = new(StringComparer.Ordinal)
    {
        "Type",
        "Subtype",
        "Width",
        "Height",
        "BitsPerComponent",
        "ColorSpace",
        "Decode",
        "Filter",
        "DecodeParms",
        "Interpolate",
        "Intent",
        "Mask",
        "SMask",
        "ImageMask",
        "Matte",
        "Length"
    };
    internal static bool Safe(PdfValue value, PdfOperationContext context)
    {
        using var depth = context.EnterScope(PdfResourceKind.Depth);
        context.Checkpoint();
        context.Consume(PdfResourceKind.Objects, 1);
        if (value.Kind == PdfValueKind.Reference)
            return false;
        if (value.Kind == PdfValueKind.Array)
        {
            foreach (var item in value.Items)
                if (!Safe(item, context))
                    return false;
        }

        if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
        {
            if (value.Entries.ContainsKey("OC") || value.Entries.ContainsKey("StructParent") || value.Entries.ContainsKey("StructParents") || value.Entries.ContainsKey("Metadata") || value.Entries.ContainsKey("Alternates"))
                return false;
            bool image = value.Entries.TryGetValue("Subtype", out var subtype) && subtype.Kind == PdfValueKind.Name && subtype.NameValue == "Image";
            foreach (var pair in value.Entries)
                if (image && !SafeImageKeys.Contains(pair.Key) || !Safe(pair.Value, context))
                    return false;
        }

        return true;
    }

    internal static string Hash(PdfImageAsset image, PdfOperationContext context)
    {
        using var hash = SHA256.Create();
        context.Consume(PdfResourceKind.ExpandedBytes, 4096);
        var buffer = new byte[4096];
        void Text(string text)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 4L + 32);
            var bytes = Encoding.UTF8.GetBytes(text);
            hash.TransformBlock(bytes, 0, bytes.Length, bytes, 0);
        }

        void Bytes(Stream source)
        {
            Text("bytes:" + source.Length.ToString(System.Globalization.CultureInfo.InvariantCulture) + ":");
            using var lease = new BorrowedStream(source, true, context);
            int read;
            while ((read = lease.Read(buffer, 0, buffer.Length)) > 0)
            {
                context.Checkpoint();
                hash.TransformBlock(buffer, 0, read, buffer, 0);
            }
        }

        void Value(PdfValue value)
        {
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            Text("kind:" + (int)value.Kind + ":");
            if (value.Kind == PdfValueKind.Number)
                Text(value.NumberText.Length + ":" + value.NumberText);
            if (value.Kind == PdfValueKind.Name)
                Text(value.NameValue.Length + ":" + value.NameValue);
            if (value.Kind == PdfValueKind.Boolean)
                Text(value.BooleanValue ? "true" : "false");
            if (value.Kind == PdfValueKind.String)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, value.StringBytes.Count + 32);
                using var s = new MemoryStream(value.StringBytes.ToArray(), false);
                Bytes(s);
            }

            if (value.Kind == PdfValueKind.Array)
            {
                Text("count:" + value.Items.Count + ":");
                foreach (var item in value.Items)
                    Value(item);
            }

            if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, value.Entries.Count * 32L + 32);
                Text("entries:" + value.Entries.Count + ":");
                foreach (var pair in value.Entries.OrderBy(p => p.Key, StringComparer.Ordinal))
                {
                    Text(pair.Key.Length + ":" + pair.Key);
                    Value(pair.Value);
                }

                if (value.Kind == PdfValueKind.Stream)
                {
                    using var s = value.OpenRawStream();
                    Bytes(s);
                }
            }
        }

        Value(image.Definition);
        Text(image.Metadata.Orientation + ":" + image.Metadata.HorizontalDpi.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + image.Metadata.VerticalDpi.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ":" + image.Metadata.PixelAspectRatio.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        Text(image.MediaType ?? "none");
        Text(image.HasOriginalFile ? "file" : "no-file");
        if (image.OriginalFile != null)
        {
            using var s = image.OriginalFile.Open(0, image.OriginalFile.Length);
            Bytes(s);
        }

        hash.TransformFinalBlock(System.Array.Empty<byte>(), 0, 0);
        return Convert.ToBase64String(hash.Hash!);
    }

    internal static bool Equal(PdfImageAsset a, PdfImageAsset b, PdfOperationContext context)
    {
        if (ReferenceEquals(a, b))
            return true;
        if (a.Metadata.Orientation != b.Metadata.Orientation || a.Metadata.HorizontalDpi != b.Metadata.HorizontalDpi || a.Metadata.VerticalDpi != b.Metadata.VerticalDpi || a.Metadata.PixelAspectRatio != b.Metadata.PixelAspectRatio || a.MediaType != b.MediaType || a.HasOriginalFile != b.HasOriginalFile)
            return false;
        context.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var x = new byte[4096];
        var y = new byte[4096];
        bool Bytes(Stream first, Stream second)
        {
            if (first.Length != second.Length)
                return false;
            using var sx = new BorrowedStream(first, true, context);
            using var sy = new BorrowedStream(second, true, context);
            long remaining = first.Length;
            while (remaining > 0)
            {
                context.Checkpoint();
                int requested = (int)Math.Min(4096, remaining), nx = 0, ny = 0;
                while (nx < requested)
                {
                    int n = sx.Read(x, nx, requested - nx);
                    if (n == 0)
                        return false;
                    nx += n;
                }

                while (ny < requested)
                {
                    int n = sy.Read(y, ny, requested - ny);
                    if (n == 0)
                        return false;
                    ny += n;
                }

                for (int i = 0; i < requested; i++)
                    if (x[i] != y[i])
                        return false;
                remaining -= requested;
            }

            return true;
        }

        bool Value(PdfValue first, PdfValue second)
        {
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            if (ReferenceEquals(first, second))
                return true;
            if (first.Kind != second.Kind)
                return false;
            switch (first.Kind)
            {
                case PdfValueKind.Null:
                    return true;
                case PdfValueKind.Boolean:
                    return first.BooleanValue == second.BooleanValue;
                case PdfValueKind.Number:
                    return first.NumberText == second.NumberText;
                case PdfValueKind.Name:
                    return first.NameValue == second.NameValue;
                case PdfValueKind.String:
                    return first.StringBytes.SequenceEqual(second.StringBytes);
                case PdfValueKind.Array:
                    if (first.Items.Count != second.Items.Count)
                        return false;
                    for (int i = 0; i < first.Items.Count; i++)
                        if (!Value(first.Items[i], second.Items[i]))
                            return false;
                    return true;
                case PdfValueKind.Dictionary:
                case PdfValueKind.Stream:
                    if (first.Entries.Count != second.Entries.Count)
                        return false;
                    foreach (var pair in first.Entries)
                        if (!second.Entries.TryGetValue(pair.Key, out var v) || !Value(pair.Value, v))
                            return false;
                    if (first.Kind == PdfValueKind.Dictionary)
                        return true;
                    using (var fs = first.OpenRawStream())
                    using (var ss = second.OpenRawStream())
                        return Bytes(fs, ss);
                default:
                    return false;
            }
        }

        if (!Value(a.Definition, b.Definition))
            return false;
        if (a.OriginalFile == null)
            return true;
        using var one = a.OriginalFile.Open(0, a.OriginalFile.Length);
        using var two = b.OriginalFile!.Open(0, b.OriginalFile.Length);
        return Bytes(one, two);
    }
}
