using System.IO;
using System.IO.Compression;
using System.Xml;

namespace Fluent.Pdf.Word;
// SDK-owned passive preflight precedes the opaque dependency. No package part is extracted to a filesystem.
internal static class WordPackagePreflight
{
    internal static void Validate(Stream input, PdfWordConversionOptions options, PdfOperationContext c)
    {
        if (!input.CanSeek)
            throw WordErrors.Error(c, "PackageSeek", "The private Word package snapshot must be seekable.");
        long position = input.Position, length = input.Length - position;
        c.Consume(PdfResourceKind.ExpandedBytes, length * 8L + 65536);
        c.Consume(PdfResourceKind.Objects, length / 20L + 64);
        try
        {
            using var lease = new BorrowedStream(input, true, c);
            using var zip = new ZipArchive(lease, ZipArchiveMode.Read, true);
            if (zip.Entries.Count > options.MaximumArchiveEntries)
                throw WordErrors.Error(c, "ArchiveLimit", "The explicit package-entry ceiling was exceeded.");
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in zip.Entries)
            {
                WordErrors.Node(c);
                string name = entry.FullName;
                if (name.Length == 0 || name.Length > 1024 || name.StartsWith("/", StringComparison.Ordinal) || name.IndexOf('\\') >= 0 || name.Split('/').Any(part => part == ".." || part == ".") || !names.Add(name))
                    throw WordErrors.Error(c, "PackagePart", "Package names must be bounded, unique relative part paths without traversal.");
                if (entry.Length < 0 || entry.Length > options.MaximumProviderExpandedBytes - total)
                    throw WordErrors.Error(c, "PackageExpansion", "Package parts exceed the explicit expansion envelope.");
                total += entry.Length;
                string lower = name.ToLowerInvariant();
                if (lower.IndexOf("vbaproject", StringComparison.Ordinal) >= 0 || lower.IndexOf("activex", StringComparison.Ordinal) >= 0 || lower.StartsWith("word/embeddings/", StringComparison.Ordinal))
                    throw WordErrors.Error(c, "PackageActiveContent", "Macros, active controls and embedded executable/OLE parts are prohibited.");
                if (name.EndsWith("/", StringComparison.Ordinal))
                    continue;
                if (lower.EndsWith(".xml", StringComparison.Ordinal) || lower.EndsWith(".rels", StringComparison.Ordinal))
                {
                    c.Consume(PdfResourceKind.ExpandedBytes, entry.Length * 3L + 8192);
                    using var part = entry.Open();
                    using var source = new BorrowedStream(part, true, c);
                    using var reader = XmlReader.Create(source, new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, CloseInput = false, MaxCharactersInDocument = options.MaximumProviderExpandedBytes, MaxCharactersFromEntities = 1 });
                    while (true)
                    {
                        WordErrors.Node(c, 256);
                        if (!reader.Read())
                            break;
                        if (reader.Depth > options.MaximumDepth)
                            throw WordErrors.Error(c, "PackageDepth", "Package XML exceeds the explicit nesting ceiling.");
                        if (reader.NodeType == XmlNodeType.ProcessingInstruction || reader.NodeType == XmlNodeType.DocumentType)
                            throw WordErrors.Error(c, "PackageXml", "Package processing instructions and DTDs are prohibited.");
                        if (reader.NodeType == XmlNodeType.Element && reader.LocalName == "Relationship" && reader.NamespaceURI == "http://schemas.openxmlformats.org/package/2006/relationships")
                        {
                            string? target = reader.GetAttribute("Target"), mode = reader.GetAttribute("TargetMode");
                            if (string.IsNullOrEmpty(target) || target.Length > 8192 || target.Any(char.IsControl) || target.IndexOf('\\') >= 0)
                                throw WordErrors.Error(c, "PackageRelationship", "A relationship target must be a bounded passive part or URI.");
                            if (mode == "External")
                            {
                                if (reader.GetAttribute("Type") != "http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink")
                                    throw WordErrors.Error(c, "PackageExternalResource", "Only passive hyperlinks may name an external package target; external resources are never loaded.");
                                PdfInputLinks.ValidateUri(target, c);
                            }
                            else
                            {
                                if (mode != null && mode != "Internal" || target.IndexOf(':') >= 0)
                                    throw WordErrors.Error(c, "PackageRelationship", "An internal package target cannot name an external URI.");
                                int count = target.StartsWith("/", StringComparison.Ordinal) ? 0 : Math.Max(0, name.Split('/').Length - 2);
                                foreach (string raw in target.TrimStart('/').Split('/'))
                                {
                                    string segment = Uri.UnescapeDataString(raw);
                                    if (segment == "..")
                                    {
                                        if (count-- == 0)
                                            throw WordErrors.Error(c, "PackageRelationship", "A relationship escapes the private package root.");
                                    }
                                    else if (segment != ".")
                                    {
                                        if (segment.IndexOfAny(new[] { '/', '\\', ':', '\0' }) >= 0)
                                            throw WordErrors.Error(c, "PackageRelationship", "An encoded relationship target is not a passive package part.");
                                        count++;
                                    }
                                }
                            }
                        }
                    }
                }
                else if (lower.StartsWith("word/media/", StringComparison.Ordinal))
                {
                    using var part = entry.Open();
                    var bytes = PdfWordConversion.Capture(part, options.MaximumProviderExpandedBytes, c);
                    if (lower.EndsWith(".png", StringComparison.Ordinal))
                    {
                        byte[] signature =
                        {
                            137,
                            80,
                            78,
                            71,
                            13,
                            10,
                            26,
                            10
                        };
                        if (bytes.Length < 8 || signature.Where((v, i) => bytes.At(i) != v).Any())
                            throw WordErrors.Error(c, "PackageImage", "A PNG package image has an invalid signature.");
                        _ = PdfImagePng.Read(bytes, new(), c);
                    }
                    else if (lower.EndsWith(".jpg", StringComparison.Ordinal) || lower.EndsWith(".jpeg", StringComparison.Ordinal))
                        _ = PdfImageJpeg.Read(bytes, new(), c);
                    else
                        throw WordErrors.Error(c, "PackageImageProfile", "This package source profile requires explicit PNG or JPEG image parts.");
                }
            }
        }
        catch (XmlException)
        {
            throw WordErrors.Error(c, "PackageXml", "Malformed XML, prohibited entities or XML character limits were rejected.");
        }
        catch (InvalidDataException)
        {
            throw WordErrors.Error(c, "PackageArchive", "The private Word package archive is malformed.");
        }
        finally
        {
            input.Position = position;
        }
    }
}
