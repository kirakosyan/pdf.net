namespace Fluent.Pdf;
internal static class PdfDeferredNavigation
{
    internal static void Write(PdfTransaction edit, PdfObjectId catalogId, IReadOnlyList<PdfValue> pageIds, PdfDocumentSnapshot snapshot, PdfOperationContext context)
    {
        for (int i = 0; i < snapshot.Pages.Count; i++)
        {
            var links = snapshot.Pages[i].Links;
            if (links.Count == 0)
                continue;
            context.Consume(PdfResourceKind.ExpandedBytes, links.Count * 1024L + 512);
            context.Consume(PdfResourceKind.Objects, links.Count * 4L);
            var page = PdfWriterCore.CopyDictionary(edit.Resolve(pageIds[i].ReferenceId).Entries, context);
            var annots = new List<PdfValue>();
            if (page.TryGetValue("Annots", out var declared))
            {
                var visited = new HashSet<PdfObjectId>();
                while (declared.Kind == PdfValueKind.Reference)
                {
                    context.Checkpoint();
                    context.Consume(PdfResourceKind.Objects, 1);
                    if (!visited.Add(declared.ReferenceId))
                        throw PdfWriterCore.Error(context, "Navigation.AnnotationCycle", "Source annotation references contain a cycle.");
                    declared = edit.Resolve(declared.ReferenceId);
                }

                if (declared.Kind != PdfValueKind.Null && declared.Kind != PdfValueKind.Array)
                    throw PdfWriterCore.Error(context, "Navigation.Annotations", "Source Annots must resolve to an array.");
                if (declared.Kind == PdfValueKind.Array)
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, declared.Items.Count * 16L);
                    annots.AddRange(declared.Items);
                }
            }

            foreach (var link in links)
            {
                context.Checkpoint();
                if (link.TargetPageIndex < 0 || link.TargetPageIndex >= pageIds.Count)
                    throw PdfWriterCore.Error(context, "Navigation.TargetPage", "An internal destination no longer identifies a page.");
                var destination = PdfValue.Array(pageIds[link.TargetPageIndex], PdfValue.Name("XYZ"), PdfValues.Real(link.TargetPoint.X), PdfValues.Real(link.TargetPoint.Y), PdfValue.Null);
                annots.Add(PdfValue.Reference(edit.Add(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Annot")), ("Subtype", PdfValue.Name("Link")), ("P", pageIds[i]), ("Rect", link.Bounds.Value), ("Border", PdfGraphicsNumbers.Array(0, 0, 0)), ("Dest", destination)))));
            }

            page["Annots"] = PdfValue.OwnedArray(annots);
            edit.Set(pageIds[i].ReferenceId, PdfValue.OwnedDictionary(page));
        }

        if (snapshot.PageNumbering != null)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, snapshot.PageNumbering.Ranges.Count * 512L + 256);
            var nums = new List<PdfValue>();
            foreach (var range in snapshot.PageNumbering.Ranges)
            {
                if (range.StartPageIndex >= pageIds.Count)
                    continue;
                var values = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
                if (range.Style != PdfPageLabelStyle.None)
                {
                    values["S"] = PdfValue.Name(range.Style switch
                    {
                        PdfPageLabelStyle.Decimal => "D",
                        PdfPageLabelStyle.LowerRoman => "r",
                        PdfPageLabelStyle.UpperRoman => "R",
                        PdfPageLabelStyle.LowerLetters => "a",
                        _ => "A"
                    });
                    values["St"] = PdfValue.Integer(range.FirstNumber);
                }

                if (range.Prefix.Length > 0)
                    values["P"] = Text(range.Prefix, context);
                nums.Add(PdfValue.Integer(range.StartPageIndex));
                nums.Add(PdfValue.OwnedDictionary(values));
            }

            var catalog = PdfWriterCore.CopyDictionary(edit.Resolve(catalogId).Entries, context);
            catalog["PageLabels"] = PdfValue.Reference(edit.Add(PdfGraphicsNumbers.Dict(("Nums", PdfValue.OwnedArray(nums)))));
            edit.Set(catalogId, PdfValue.OwnedDictionary(catalog));
        }
    }

    internal static PdfValue Text(string value, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, value.Length * 4L + 32);
        var bytes = new byte[value.Length * 2 + 2];
        bytes[0] = 254;
        bytes[1] = 255;
        for (int i = 0; i < value.Length; i++)
        {
            bytes[i * 2 + 2] = (byte)(value[i] >> 8);
            bytes[i * 2 + 3] = (byte)value[i];
        }

        return PdfValue.String(bytes);
    }
}
