namespace Fluent.Pdf;
// Original basic ISO PDF structure/number-tree writer. This is not a PDF/UA conformance profile.
internal static class PdfBasicStructure
{
    internal static void Write(PdfTransaction edit, PdfObjectId catalogId, IReadOnlyList<PdfValue> pageIds, IReadOnlyList<PdfPageSnapshot> pages, PdfOperationContext context)
    {
        if (!pages.Any(p => p.StructureRoles.Count > 0))
            return;
        var root = edit.Add(PdfValue.Null);
        var groups = new List<PdfValue>();
        var nums = new List<PdfValue>();
        for (int page = 0; page < pages.Count; page++)
        {
            var roles = pages[page].StructureRoles;
            if (roles.Count == 0)
                continue;
            context.Consume(PdfResourceKind.Objects, roles.Count * 2L + 4);
            context.Consume(PdfResourceKind.ExpandedBytes, roles.Count * 1024L + 512);
            var parent = edit.Add(PdfValue.Null);
            var children = new List<PdfValue>();
            for (int i = 0; i < roles.Count; i++)
            {
                context.Checkpoint();
                children.Add(PdfValue.Reference(edit.Add(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("StructElem")), ("S", PdfValue.Name(roles[i])), ("P", PdfValue.Reference(parent)), ("Pg", pageIds[page]), ("K", PdfValue.Integer(i))))));
            }

            edit.Set(parent, PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("StructElem")), ("S", PdfValue.Name("Div")), ("P", PdfValue.Reference(root)), ("Pg", pageIds[page]), ("K", PdfValue.OwnedArray(children))));
            groups.Add(PdfValue.Reference(parent));
            nums.Add(PdfValue.Integer(page));
            nums.Add(PdfValue.OwnedArray(children));
        }

        var numberTree = edit.Add(PdfGraphicsNumbers.Dict(("Nums", PdfValue.OwnedArray(nums))));
        edit.Set(root, PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("StructTreeRoot")), ("K", PdfValue.OwnedArray(groups)), ("ParentTree", PdfValue.Reference(numberTree)), ("ParentTreeNextKey", PdfValue.Integer(pages.Count))));
        var catalog = PdfWriterCore.CopyDictionary(edit.Resolve(catalogId).Entries, context);
        catalog["StructTreeRoot"] = PdfValue.Reference(root);
        catalog["MarkInfo"] = PdfGraphicsNumbers.Dict(("Marked", PdfValue.Boolean(true)));
        edit.Set(catalogId, PdfValue.OwnedDictionary(catalog));
    }
}
