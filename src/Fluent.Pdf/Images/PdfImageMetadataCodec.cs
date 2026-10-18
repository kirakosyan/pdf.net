using System.Globalization;
using System.Text;
using System.Xml;

namespace Fluent.Pdf;
internal static class PdfImageMetadataCodec
{
    private const string Namespace = "urn:fluent-pdf:image:1";
    internal static PdfValue? Write(PdfImageMetadata metadata, PdfOperationContext context, bool force = false)
    {
        if (!force && metadata.Orientation == PdfImageOrientation.Normal && metadata.HorizontalDpi == 0 && metadata.PixelAspectRatio == 1)
            return null;
        string xml = "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\" xmlns:fp=\"" + Namespace + "\" fp:Orientation=\"" + ((int)metadata.Orientation).ToString(CultureInfo.InvariantCulture) + "\" fp:HorizontalDpi=\"" + metadata.HorizontalDpi.ToString("R", CultureInfo.InvariantCulture) + "\" fp:VerticalDpi=\"" + metadata.VerticalDpi.ToString("R", CultureInfo.InvariantCulture) + "\" fp:PixelAspectRatio=\"" + metadata.PixelAspectRatio.ToString("R", CultureInfo.InvariantCulture) + "\"/></rdf:RDF></x:xmpmeta>";
        context.Consume(PdfResourceKind.ExpandedBytes, xml.Length * 8L + 256);
        var bytes = Encoding.UTF8.GetBytes(xml);
        var builder = new PdfByteBuilder(context);
        foreach (byte b in bytes)
            builder.Add(b);
        return PdfValue.StreamValue(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Metadata")), ("Subtype", PdfValue.Name("XML"))), builder.Build(), 0, bytes.Length);
    }

    internal static PdfImageMetadata Read(PdfValue stream, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.InputBytes, stream.RawStreamLength);
        using var decoded = PdfFilters.Decode(stream, context);
        long length = decoded.Length;
        if (length < 1 || length > 1000000)
            throw PdfImages.Error(context, "Image.MetadataLength", "Image display metadata must fit the bounded XML profile.");
        context.Consume(PdfResourceKind.ExpandedBytes, length * 8L + 4096);
        context.Consume(PdfResourceKind.Objects, length / 4 + 1);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = length,
            MaxCharactersFromEntities = 1024,
            CloseInput = false
        };
        var scopes = new List<IDisposable>();
        try
        {
            using var reader = XmlReader.Create(decoded, settings);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            while (reader.Read())
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                while (scopes.Count > reader.Depth + 1)
                {
                    scopes[scopes.Count - 1].Dispose();
                    scopes.RemoveAt(scopes.Count - 1);
                }

                while (scopes.Count < reader.Depth + 1)
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, 32);
                    scopes.Add(context.EnterScope(PdfResourceKind.Depth));
                }

                if (!reader.HasAttributes)
                    continue;
                while (reader.MoveToNextAttribute())
                {
                    using var attributeDepth = context.EnterScope(PdfResourceKind.Depth);
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.ExpandedBytes, reader.Value.Length * 2L + 64);
                    if (reader.NamespaceURI == Namespace)
                    {
                        if (values.ContainsKey(reader.LocalName))
                            throw PdfImages.Error(context, "Image.Metadata", "Image display metadata repeats a field.");
                        values.Add(reader.LocalName, reader.Value);
                    }
                }

                reader.MoveToElement();
            }

            double Number(string key, double fallback)
            {
                if (!values.TryGetValue(key, out var value))
                    return fallback;
                if (!double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double n) || double.IsNaN(n) || double.IsInfinity(n))
                    throw PdfImages.Error(context, "Image.Metadata", "Image display metadata contains an invalid number.");
                return n;
            }

            double orientation = Number("Orientation", 1);
            if (orientation < 1 || orientation > 8 || orientation != Math.Floor(orientation))
                throw PdfImages.Error(context, "Image.Metadata", "Image display orientation must be an integer from one through eight.");
            return new((PdfImageOrientation)(int)orientation, Number("HorizontalDpi", 0), Number("VerticalDpi", 0), Number("PixelAspectRatio", 1));
        }
        catch (XmlException)
        {
            context.Checkpoint();
            throw PdfImages.Error(context, "Image.MetadataXml", "Image metadata must be bounded XML without document types or external entities.");
        }
        catch (ArgumentOutOfRangeException)
        {
            context.Checkpoint();
            throw PdfImages.Error(context, "Image.Metadata", "Image metadata contains invalid display geometry.");
        }
        finally
        {
            for (int i = scopes.Count - 1; i >= 0; i--)
                scopes[i].Dispose();
        }
    }
}
