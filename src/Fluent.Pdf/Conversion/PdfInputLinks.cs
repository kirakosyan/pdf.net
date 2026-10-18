namespace Fluent.Pdf;
// SDK-private source markers are interpreted only during explicit conversion; they are not authenticated provenance.
internal static class PdfInputLinks
{
    internal static void ValidateUri(string uri, PdfOperationContext c)
    {
        if (uri.Length > 8192 || uri.Any(ch => char.IsControl(ch)) || !Uri.TryCreate(uri, UriKind.Absolute, out var parsed) || (parsed.Scheme != "https" && parsed.Scheme != "http" && parsed.Scheme != "mailto") || parsed.Scheme != "mailto" && parsed.UserInfo.Length != 0)
            throw PdfInputConversion.Error(c, "UnsafeUri", "Only passive absolute HTTP, HTTPS and mailto links without credentials are supported.");
    }

    internal static PdfValue Action(string uri, PdfOperationContext c)
    {
        ValidateUri(uri, c);
        c.Consume(PdfResourceKind.ExpandedBytes, uri.Length * 16L + 8192);
        try
        {
            var parsed = new Uri(uri, UriKind.Absolute);
            string encoded = parsed.Host.Length == 0 ? parsed.AbsoluteUri : new UriBuilder(parsed)
            {
                Host = parsed.IdnHost
            }.Uri.AbsoluteUri;
            if (encoded.Length > 32768 || encoded.Any(ch => ch > 127))
                throw PdfInputConversion.Error(c, "UriEncoding", "A URI cannot be represented within the ASCII action profile.");
            return PdfGraphicsNumbers.Dict(("S", PdfValue.Name("URI")), ("URI", PdfValue.String(System.Text.Encoding.ASCII.GetBytes(encoded))));
        }
        catch (ArgumentException)
        {
            throw PdfInputConversion.Error(c, "UriEncoding", "A URI cannot be represented within the ASCII action profile.");
        }
        catch (UriFormatException)
        {
            throw PdfInputConversion.Error(c, "UriEncoding", "A URI cannot be represented within the ASCII action profile.");
        }
    }

    internal static void Prepare(PdfDocument document, PdfOperationContext c, PdfProviderRegistry providers)
    {
        using var graph = new PdfGraphEdit(document.CreateSnapshot(), new(), c);
        for (int p = 0; p < graph.Snapshot.Pages.Count; p++)
        {
            c.Checkpoint();
            var data = new PdfTextExtractionEngine(graph, new(), providers, collectSourceLinks: true).Read(p);
            foreach (var span in data.SourceLinks)
            {
                c.Checkpoint();
                ValidateUri(span.Uri, c);
                PdfRect? bounds = null;
                for (int i = span.First; i < span.First + span.Count; i++)
                {
                    c.Checkpoint();
                    var b = data.Characters[i].UserQuad.Bounds;
                    if (bounds is not PdfRect a)
                        bounds = b;
                    else
                        bounds = new PdfRect(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.Right, b.Right) - Math.Min(a.X, b.X), Math.Max(a.Top, b.Top) - Math.Min(a.Y, b.Y));
                }

                if (bounds.HasValue)
                    document.Pages[p].AddUriLink(bounds.Value, span.Uri, c);
            }
        }
    }
}
