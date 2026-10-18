using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Xml;

namespace Fluent.Pdf.Word;
// Original minimal WordprocessingML writer. Only owned parts and passive package relationships are emitted.
internal static class WordOpenXml
{
    private const string W = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
    private const string R = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    private const string WP = "http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing";
    private const string A = "http://schemas.openxmlformats.org/drawingml/2006/main";
    private const string PIC = "http://schemas.openxmlformats.org/drawingml/2006/picture";
    internal static PdfByteStore Write(WordModel model, PdfWordConversionOptions options, PdfOperationContext c)
    {
        WordErrors.Node(c, 131072);
        using var output = new PdfCodecBufferStream(c, options.MaximumOutputBytes);
        using (var zip = new ZipArchive(output, ZipArchiveMode.Create, true))
        {
            var pictures = new List<WordPicture>();
            foreach (var section in model.Sections)
                foreach (var block in section.Blocks)
                    if (block is WordPicture picture)
                    {
                        WordErrors.Node(c);
                        pictures.Add(picture);
                    }

            Xml("[Content_Types].xml", x =>
            {
                x.WriteStartElement("Types", "http://schemas.openxmlformats.org/package/2006/content-types");
                x.WriteStartElement("Default", "http://schemas.openxmlformats.org/package/2006/content-types");
                x.WriteAttributeString("Extension", "rels");
                x.WriteAttributeString("ContentType", "application/vnd.openxmlformats-package.relationships+xml");
                x.WriteEndElement();
                x.WriteStartElement("Default", "http://schemas.openxmlformats.org/package/2006/content-types");
                x.WriteAttributeString("Extension", "png");
                x.WriteAttributeString("ContentType", "image/png");
                x.WriteEndElement();
                x.WriteStartElement("Override", "http://schemas.openxmlformats.org/package/2006/content-types");
                x.WriteAttributeString("PartName", "/word/document.xml");
                x.WriteAttributeString("ContentType", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml");
                x.WriteEndElement();
                x.WriteEndElement();
            });
            Xml("_rels/.rels", x =>
            {
                x.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                Relationship(x, "rDocument", R + "/officeDocument", "word/document.xml");
                x.WriteEndElement();
            });
            if (pictures.Count > 0)
                Xml("word/_rels/document.xml.rels", x =>
                {
                    x.WriteStartElement("Relationships", "http://schemas.openxmlformats.org/package/2006/relationships");
                    for (int i = 0; i < pictures.Count; i++)
                        Relationship(x, "rImage" + (i + 1).ToString(CultureInfo.InvariantCulture), R + "/image", "media/image" + (i + 1).ToString(CultureInfo.InvariantCulture) + ".png");
                    x.WriteEndElement();
                });
            for (int i = 0; i < pictures.Count; i++)
            {
                WordErrors.Node(c, 65536);
                var entry = zip.CreateEntry("word/media/image" + (i + 1).ToString(CultureInfo.InvariantCulture) + ".png", CompressionLevel.NoCompression);
                entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var target = entry.Open();
                using var source = pictures[i].Bytes.Open(0, pictures[i].Bytes.Length);
                PdfWordConversion.Copy(source, target, pictures[i].Bytes.Length, c);
            }

            Xml("word/document.xml", x =>
            {
                x.WriteStartElement("w", "document", W);
                x.WriteAttributeString("xmlns", "r", null, R);
                x.WriteStartElement("w", "body", W);
                int image = 0;
                for (int index = 0; index < model.Sections.Count; index++)
                {
                    var section = model.Sections[index];
                    c.Checkpoint();
                    foreach (var block in section.Blocks)
                    {
                        if (block is WordParagraph paragraph)
                            Paragraph(x, paragraph, c);
                        else if (block is WordPicture picture)
                            Picture(x, picture, ++image, c);
                        else if (block is WordTable table)
                            Table(x, table, c);
                    }

                    if (index + 1 == model.Sections.Count)
                        Section(x, section);
                    else
                    {
                        x.WriteStartElement("w", "p", W);
                        x.WriteStartElement("w", "pPr", W);
                        Section(x, section);
                        x.WriteEndElement();
                        x.WriteEndElement();
                    }
                }

                x.WriteEndElement();
                x.WriteEndElement();
            });
            void Xml(string name, Action<XmlWriter> write)
            {
                WordErrors.Node(c, 65536);
                var entry = zip.CreateEntry(name, CompressionLevel.Optimal);
                entry.LastWriteTime = new DateTimeOffset(2000, 1, 1, 0, 0, 0, TimeSpan.Zero);
                using var stream = entry.Open();
                using var writer = XmlWriter.Create(stream, new XmlWriterSettings { Encoding = new UTF8Encoding(false, true), CloseOutput = false, CheckCharacters = true });
                write(writer);
                writer.Flush();
                c.Checkpoint();
            }
        }

        return output.Finish();
    }

    private static string Number(double value, double scale = 20)
    {
        double result = Math.Round(value * scale);
        if (double.IsNaN(result) || double.IsInfinity(result) || result < 0 || result > int.MaxValue)
            throw new ArgumentException("Word geometry exceeds the bounded integer extent.");
        return ((int)result).ToString(CultureInfo.InvariantCulture);
    }

    private static void Relationship(XmlWriter x, string id, string type, string target)
    {
        x.WriteStartElement("Relationship", "http://schemas.openxmlformats.org/package/2006/relationships");
        x.WriteAttributeString("Id", id);
        x.WriteAttributeString("Type", type);
        x.WriteAttributeString("Target", target);
        x.WriteEndElement();
    }

    private static void Value(XmlWriter x, string name, string value)
    {
        x.WriteStartElement("w", name, W);
        x.WriteAttributeString("w", "val", W, value);
        x.WriteEndElement();
    }

    private static void Section(XmlWriter x, WordSection page)
    {
        x.WriteStartElement("w", "sectPr", W);
        Value(x, "type", "nextPage");
        x.WriteStartElement("w", "pgSz", W);
        x.WriteAttributeString("w", "w", W, Number(page.Width));
        x.WriteAttributeString("w", "h", W, Number(page.Height));
        x.WriteEndElement();
        x.WriteStartElement("w", "pgMar", W);
        foreach (var name in new[]
        {
            "top",
            "right",
            "bottom",
            "left",
            "header",
            "footer",
            "gutter"
        }

        )
            x.WriteAttributeString("w", name, W, "0");
        x.WriteEndElement();
        x.WriteEndElement();
    }

    private static void Paragraph(XmlWriter x, WordParagraph paragraph, PdfOperationContext c)
    {
        WordErrors.Node(c);
        x.WriteStartElement("w", "p", W);
        x.WriteStartElement("w", "pPr", W);
        x.WriteStartElement("w", "spacing", W);
        x.WriteAttributeString("w", "before", W, "0");
        x.WriteAttributeString("w", "after", W, "0");
        x.WriteEndElement();
        if (paragraph.Heading > 0)
            Value(x, "outlineLvl", (paragraph.Heading - 1).ToString(CultureInfo.InvariantCulture));
        x.WriteEndElement();
        foreach (var run in paragraph.Runs)
        {
            c.Checkpoint();
            x.WriteStartElement("w", "r", W);
            x.WriteStartElement("w", "rPr", W);
            x.WriteStartElement("w", "rFonts", W);
            x.WriteAttributeString("w", "ascii", W, run.Family);
            x.WriteAttributeString("w", "hAnsi", W, run.Family);
            x.WriteAttributeString("w", "cs", W, run.Family);
            x.WriteEndElement();
            if (run.Bold)
                x.WriteElementString("w", "b", W, "");
            if (run.Italic)
                x.WriteElementString("w", "i", W, "");
            Value(x, "sz", Number(run.Size, 2));
            x.WriteEndElement();
            c.Consume(PdfResourceKind.ExpandedBytes, run.Text.Length * 8L + 128);
            var lines = run.Text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int i = 0; i < lines.Length; i++)
            {
                c.Checkpoint();
                if (i > 0)
                    x.WriteElementString("w", "br", W, "");
                x.WriteStartElement("w", "t", W);
                x.WriteAttributeString("xml", "space", "http://www.w3.org/XML/1998/namespace", "preserve");
                x.WriteString(lines[i]);
                x.WriteEndElement();
            }

            x.WriteEndElement();
        }

        x.WriteEndElement();
    }

    private static void Table(XmlWriter x, WordTable table, PdfOperationContext c)
    {
        x.WriteStartElement("w", "tbl", W);
        x.WriteStartElement("w", "tblPr", W);
        x.WriteStartElement("w", "tblW", W);
        x.WriteAttributeString("w", "w", W, "0");
        x.WriteAttributeString("w", "type", W, "auto");
        x.WriteEndElement();
        x.WriteEndElement();
        int columns = table.Rows.Count == 0 ? 0 : table.Rows[0].Count;
        x.WriteStartElement("w", "tblGrid", W);
        for (int i = 0; i < columns; i++)
        {
            x.WriteStartElement("w", "gridCol", W);
            x.WriteAttributeString("w", "w", W, table.Widths.Count == columns ? Number(table.Widths[i]) : "2000");
            x.WriteEndElement();
        }

        x.WriteEndElement();
        foreach (var row in table.Rows)
        {
            WordErrors.Node(c);
            x.WriteStartElement("w", "tr", W);
            int column = 0;
            foreach (var cell in row)
            {
                string cellWidth = table.Widths.Count == row.Count ? Number(table.Widths[column]) : "2000";
                column++;
                x.WriteStartElement("w", "tc", W);
                x.WriteStartElement("w", "tcPr", W);
                x.WriteStartElement("w", "tcW", W);
                x.WriteAttributeString("w", "w", W, cellWidth);
                x.WriteAttributeString("w", "type", W, "dxa");
                x.WriteEndElement();
                x.WriteEndElement();
                Paragraph(x, cell, c);
                x.WriteEndElement();
            }

            x.WriteEndElement();
        }

        x.WriteEndElement();
    }

    private static void Picture(XmlWriter x, WordPicture image, int index, PdfOperationContext c)
    {
        WordErrors.Node(c);
        string id = index.ToString(CultureInfo.InvariantCulture), width = Number(image.Width, 12700), height = Number(image.Height, 12700);
        x.WriteStartElement("w", "p", W);
        x.WriteStartElement("w", "r", W);
        x.WriteStartElement("w", "drawing", W);
        x.WriteStartElement("wp", image.Fixed ? "anchor" : "inline", WP);
        foreach (string name in new[]
        {
            "distT",
            "distB",
            "distL",
            "distR"
        }

        )
            x.WriteAttributeString(name, "0");
        if (image.Fixed)
        {
            x.WriteAttributeString("simplePos", "0");
            x.WriteAttributeString("relativeHeight", "0");
            x.WriteAttributeString("behindDoc", "0");
            x.WriteAttributeString("locked", "0");
            x.WriteAttributeString("layoutInCell", "1");
            x.WriteAttributeString("allowOverlap", "1");
            x.WriteStartElement("wp", "simplePos", WP);
            x.WriteAttributeString("x", "0");
            x.WriteAttributeString("y", "0");
            x.WriteEndElement();
            foreach (string axis in new[]
            {
                "positionH",
                "positionV"
            }

            )
            {
                x.WriteStartElement("wp", axis, WP);
                x.WriteAttributeString("relativeFrom", "page");
                x.WriteElementString("wp", "posOffset", WP, "0");
                x.WriteEndElement();
            }
        }

        x.WriteStartElement("wp", "extent", WP);
        x.WriteAttributeString("cx", width);
        x.WriteAttributeString("cy", height);
        x.WriteEndElement();
        if (image.Fixed)
            x.WriteElementString("wp", "wrapNone", WP, "");
        x.WriteStartElement("wp", "docPr", WP);
        x.WriteAttributeString("id", id);
        x.WriteAttributeString("name", "Page " + id);
        x.WriteEndElement();
        x.WriteStartElement("a", "graphic", A);
        x.WriteStartElement("a", "graphicData", A);
        x.WriteAttributeString("uri", PIC);
        x.WriteStartElement("pic", "pic", PIC);
        x.WriteStartElement("pic", "nvPicPr", PIC);
        x.WriteStartElement("pic", "cNvPr", PIC);
        x.WriteAttributeString("id", "0");
        x.WriteAttributeString("name", "Page " + id);
        x.WriteEndElement();
        x.WriteElementString("pic", "cNvPicPr", PIC, "");
        x.WriteEndElement();
        x.WriteStartElement("pic", "blipFill", PIC);
        x.WriteStartElement("a", "blip", A);
        x.WriteAttributeString("r", "embed", R, "rImage" + id);
        x.WriteEndElement();
        x.WriteStartElement("a", "stretch", A);
        x.WriteElementString("a", "fillRect", A, "");
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteStartElement("pic", "spPr", PIC);
        x.WriteStartElement("a", "xfrm", A);
        x.WriteStartElement("a", "off", A);
        x.WriteAttributeString("x", "0");
        x.WriteAttributeString("y", "0");
        x.WriteEndElement();
        x.WriteStartElement("a", "ext", A);
        x.WriteAttributeString("cx", width);
        x.WriteAttributeString("cy", height);
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteStartElement("a", "prstGeom", A);
        x.WriteAttributeString("prst", "rect");
        x.WriteElementString("a", "avLst", A, "");
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteEndElement();
        x.WriteEndElement();
    }
}
