using System.IO;
using System.Security;
using System.Text;
using System.Xml;

namespace Fluent.Pdf.Html;
// Original bounded resource-free print fragment profile. Full arbitrary template/assets remain explicit pending scope.
internal static class ChromiumPrintTemplate
{
    private const string Tags = " div span p br b strong i em u small sup sub table thead tbody tfoot tr th td ul ol li h1 h2 h3 h4 h5 h6 ";
    private const string Properties = " font-family font-size font-style font-weight color background-color line-height letter-spacing word-spacing white-space text-align text-decoration vertical-align width height max-width min-width max-height min-height margin margin-top margin-bottom margin-left margin-right padding padding-top padding-bottom padding-left padding-right border border-top border-bottom border-left border-right border-color border-style border-width border-collapse border-spacing box-sizing display position top bottom left right overflow flex flex-direction flex-wrap justify-content align-items ";
    internal static string Normalize(string? html, PdfOperationContext context)
    {
        if (html == null)
            return "";
        context.Consume(PdfResourceKind.ExpandedBytes, html.Length * 32L + 8192);
        context.Consume(PdfResourceKind.Objects, 4);
        var settings = new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 65543,
            MaxCharactersFromEntities = 0,
            IgnoreComments = false,
            IgnoreProcessingInstructions = false
        };
        var result = new StringBuilder();
        var depths = new Stack<IDisposable>();
        using var input = new StringReader("<r>" + html + "</r>");
        try
        {
            using var reader = XmlReader.Create(input, settings);
            while (reader.Read())
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                if (reader.Depth > 32)
                    throw Error(context);
                if (reader.NodeType == XmlNodeType.Element)
                {
                    if (reader.Depth == 0)
                        continue;
                    string name = reader.LocalName.ToLowerInvariant();
                    if (reader.NamespaceURI.Length != 0 || reader.Prefix.Length != 0 || Tags.IndexOf(" " + name + " ", StringComparison.Ordinal) < 0 || reader.AttributeCount > 16 || name == "br" && !reader.IsEmptyElement)
                        throw Error(context);
                    var attributes = new HashSet<string>(StringComparer.Ordinal);
                    var scope = context.EnterScope(PdfResourceKind.Depth);
                    depths.Push(scope);
                    result.Append('<').Append(name);
                    if (reader.MoveToFirstAttribute())
                    {
                        do
                        {
                            context.Checkpoint();
                            context.Consume(PdfResourceKind.Objects, 1);
                            string attribute = reader.LocalName.ToLowerInvariant();
                            if (reader.NamespaceURI.Length != 0 || reader.Prefix.Length != 0 || reader.Value.Length > 8192 || !attributes.Add(attribute) || attribute != "class" && attribute != "style" && attribute != "title")
                                throw Error(context);
                            if (attribute == "style")
                                ValidateStyle(reader.Value, context);
                            result.Append(' ').Append(attribute).Append("=\"").Append(SecurityElement.Escape(reader.Value)).Append('"');
                        }
                        while (reader.MoveToNextAttribute());
                        reader.MoveToElement();
                    }

                    result.Append('>');
                    if (reader.IsEmptyElement)
                    {
                        if (name != "br")
                            result.Append("</").Append(name).Append('>');
                        depths.Pop().Dispose();
                    }
                }
                else if (reader.NodeType == XmlNodeType.EndElement)
                {
                    if (reader.Depth == 0)
                        continue;
                    string name = reader.LocalName.ToLowerInvariant();
                    if (name != "br")
                        result.Append("</").Append(name).Append('>');
                    depths.Pop().Dispose();
                }
                else if (reader.NodeType == XmlNodeType.Text || reader.NodeType == XmlNodeType.Whitespace || reader.NodeType == XmlNodeType.SignificantWhitespace || reader.NodeType == XmlNodeType.CDATA)
                    result.Append(SecurityElement.Escape(reader.Value));
                else
                    throw Error(context);
                if (result.Length > 393216)
                    throw Error(context);
            }

            return result.ToString();
        }
        catch (XmlException)
        {
            throw Error(context);
        }
        finally
        {
            while (depths.Count != 0)
                depths.Pop().Dispose();
        }
    }

    private static void ValidateStyle(string css, PdfOperationContext context)
    {
        foreach (string declaration in css.Split(';'))
        {
            context.Checkpoint();
            if (declaration.Trim().Length == 0)
                continue;
            int colon = declaration.IndexOf(':');
            if (colon < 1)
                throw Error(context);
            string name = declaration.Substring(0, colon).Trim().ToLowerInvariant(), value = declaration.Substring(colon + 1).Trim();
            if (Properties.IndexOf(" " + name + " ", StringComparison.Ordinal) < 0 || value.Length == 0 || value.Any(c => !(c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z' || c >= '0' && c <= '9' || " \t-#%.,/'\"".IndexOf(c) >= 0)))
                throw Error(context);
        }
    }

    private static PdfException Error(PdfOperationContext context) => PdfWriterCore.Error(context, "Html.PrintTemplateProfile", "The print fragment is outside the bounded resource-free XHTML/inline literal-CSS profile; source content is omitted.");
}
