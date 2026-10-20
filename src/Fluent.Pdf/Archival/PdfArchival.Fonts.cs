using System.IO;

namespace Fluent.Pdf;
public static partial class PdfArchival
{
    // A missing CIDSet is repaired only where the source supplies an explicit complete CID-to-GID map.
    // Identity maps and other CID font programs require their separate validated inventory profiles.
    private static void AddExplicitCidSets(PdfGraphEdit graph)
    {
        var context = graph.Context;
        var candidates = new List<PdfObjectId>();
        foreach (var id in graph.Edit.ActiveIds(context))
        {
            context.Checkpoint();
            var font = graph.Edit.Resolve(id);
            if (font.Kind != PdfValueKind.Dictionary || Name(graph.Get(font, "Type")) != "Font" || Name(graph.Get(font, "Subtype")) != "CIDFontType2")
                continue;
            context.Consume(PdfResourceKind.ExpandedBytes, 64);
            candidates.Add(id);
        }

        foreach (var id in candidates)
        {
            var font = graph.Edit.Resolve(id);
            var descriptor = graph.Get(font, "FontDescriptor");
            if (descriptor.Kind != PdfValueKind.Dictionary || graph.Get(descriptor, "CIDSet").Kind != PdfValueKind.Null)
                continue;
            var map = graph.Get(font, "CIDToGIDMap");
            if (map.Kind != PdfValueKind.Stream)
                continue;
            using var decoded = PdfFilters.Decode(PdfFilterDeclarations.Resolve(map, reference => graph.Resolve(PdfValue.Reference(reference)), context), context);
            using var staging = new PdfCodecBufferStream(context, 131072);
            context.Consume(PdfResourceKind.ExpandedBytes, 4096);
            var part = new byte[4096];
            int count;
            while ((count = decoded.Read(part, 0, part.Length)) > 0)
            {
                context.Checkpoint();
                if (count > 131072 - staging.BufferedLength)
                    throw Error(context, "CidMapExtent", "An explicit CID map cannot exceed the 16-bit CID inventory.");
                staging.Write(part, 0, count);
            }

            var bytes = staging.Finish();
            if (bytes.Length < 2 || bytes.Length % 2 != 0)
                throw Error(context, "CidMapExtent", "An explicit CID map requires complete two-byte glyph entries.");
            int cids = (int)(bytes.Length / 2), length = (cids + 7) / 8;
            context.Consume(PdfResourceKind.ExpandedBytes, length);
            var bits = new byte[length];
            for (int cid = 0; cid < cids; cid++)
            {
                context.Checkpoint();
                if (cid == 0 || bytes.At(cid * 2) != 0 || bytes.At(cid * 2 + 1) != 0)
                    bits[cid / 8] |= (byte)(128 >> (cid % 8));
            }

            using var input = new MemoryStream(bits, false);
            var store = PdfByteStore.Read(input, context);
            context.Consume(PdfResourceKind.Objects, 2);
            var cidSet = graph.Edit.Add(PdfValue.StreamValue(PdfGraphicsNumbers.Dict(), store, 0, store.Length));
            var descriptorEntries = graph.Copy(descriptor);
            descriptorEntries["CIDSet"] = PdfValue.Reference(cidSet);
            var fontEntries = graph.Copy(font);
            fontEntries["FontDescriptor"] = PdfValue.Reference(graph.Edit.Add(PdfValue.OwnedDictionary(descriptorEntries)));
            graph.Edit.Set(id, PdfValue.OwnedDictionary(fontEntries));
            context.Report(new PdfDiagnostic("Archival.CidSetAdded", "PDF/A-1 conversion added a CID inventory from the explicit source CID-to-GID map, with a private font descriptor.", severity: PdfDiagnosticSeverity.Information));
        }

        static string Name(PdfValue value) => value.Kind == PdfValueKind.Name ? value.NameValue : "";
    }
}
