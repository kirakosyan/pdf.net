using System.IO;

namespace Fluent.Pdf;
internal static class PdfPageImposer
{
    internal static PdfDocument Write(PdfDocument document, PdfPageSize sheet, IReadOnlyList<PdfImposedPage> placements, PdfOrganizationOptions options, PdfOperationContext context)
    {
        var source = document.SourceDocument!;
        int minimumVersion = PdfVersionNumber.Parse(source.Version);
        using var edit = new PdfTransaction(source, context.Policy);
        PdfValue R(PdfValue value) => ManagedPdfProvider.Resolve(source, value, context);
        var rootId = source.Revisions[0].Trailer.Entries["Root"].ReferenceId;
        var catalog = R(PdfValue.Reference(rootId));
        if (catalog.Entries.ContainsKey("StructTreeRoot"))
            minimumVersion = Math.Max(minimumVersion, 13);
        var pagesRootId = catalog.Entries["Pages"].ReferenceId;
        int count = placements.Max(p => p.Sheet) + 1;
        context.Consume(PdfResourceKind.Objects, count * 4L + placements.Count * 8L);
        context.Consume(PdfResourceKind.ExpandedBytes, count * 1024L + placements.Count * 512L + document.Pages.Count * 128L);
        var sheets = Enumerable.Range(0, count).Select(_ => edit.Add(PdfValue.Null)).ToArray();
        var oldPages = document.Pages.Select(p => p.Source!.ObjectId).ToArray();
        var byPage = new Dictionary<PdfObjectId, PdfImposedPage>();
        var referenceMap = new Dictionary<PdfObjectId, PdfObjectId>();
        var formIds = new Dictionary<PdfObjectId, PdfObjectId>();
        var annotations = new List<PdfValue>[count];
        var annotationTransforms = new Dictionary<PdfObjectId, PdfMatrix>();
        for (int i = 0; i < annotations.Length; i++)
            annotations[i] = new();
        bool repeated = placements.Where(p => p.Page >= 0).GroupBy(p => p.Page).Any(g => g.Count() > 1);
        bool namedNavigation = catalog.Entries.ContainsKey("Dests") || catalog.Entries.TryGetValue("Names", out var names) && R(names).Entries.ContainsKey("Dests");
        if (repeated && (catalog.Entries.ContainsKey("StructTreeRoot") || catalog.Entries.ContainsKey("Outlines") || namedNavigation))
            throw Error("Organization.TileInteractiveProfile", "Repeated tiles with structure or navigation require an explicit per-tile destination/structure-copy profile.");
        foreach (var placement in placements.Where(p => p.Page >= 0))
        {
            var page = document.Pages[placement.Page];
            var binding = page.Source!;
            var old = binding.ObjectId;
            if (!byPage.ContainsKey(old))
            {
                byPage.Add(old, placement);
                referenceMap.Add(old, sheets[placement.Sheet]);
            }

            if (!formIds.ContainsKey(old))
            {
                var program = PdfPageContent.Read(binding, PdfUnknownOperatorPolicy.Reject, null, new(), context);
                minimumVersion = Math.Max(minimumVersion, PdfVersionNumber.Parse(program.MinimumVersion));
                var dictionary = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
                {
                    ["Type"] = PdfValue.Name("XObject"),
                    ["Subtype"] = PdfValue.Name("Form"),
                    ["FormType"] = PdfValue.Integer(1),
                    ["BBox"] = page.Geometry.Boxes.Crop.Value,
                    ["Resources"] = binding.EffectiveAttributes.Entries.TryGetValue("Resources", out var resources) ? resources : PdfValue.Dictionary(new Dictionary<string, PdfValue>())
                };
                if (binding.Dictionary.Entries.TryGetValue("Group", out var group))
                    dictionary["Group"] = group;
                if (binding.Dictionary.Entries.TryGetValue("StructParents", out var parent))
                    dictionary["StructParents"] = parent;
                formIds.Add(old, edit.Add(program.ToStream(context).WithStreamDictionary(PdfValue.OwnedDictionary(dictionary))));
            }

            if (!binding.Dictionary.Entries.TryGetValue("Annots", out var declared))
                continue;
            if (repeated)
                throw Error("Organization.TileAnnotationProfile", "Repeated tiles with annotations require an explicit annotation-copy profile.");
            var annots = R(declared);
            if (annots.Kind != PdfValueKind.Array)
                throw Error("Organization.Annotations", "Page annotations must resolve to an array.");
            foreach (var reference in annots.Items)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 256);
                if (reference.Kind != PdfValueKind.Reference || referenceMap.ContainsKey(reference.ReferenceId))
                    throw Error("Organization.AnnotationIdentity", "Annotations must have unique indirect page-owned identities.");
                var annotation = R(reference);
                if (!annotation.Entries.TryGetValue("Subtype", out var subtype) || subtype.NameValue != "Link" || annotation.Entries.ContainsKey("AP"))
                    throw Error("Organization.AnnotationImpositionProfile", "This annotation requires an explicit appearance/geometry transformation profile.");
                double sx = Math.Sqrt(placement.Matrix.A * placement.Matrix.A + placement.Matrix.B * placement.Matrix.B), sy = Math.Sqrt(placement.Matrix.C * placement.Matrix.C + placement.Matrix.D * placement.Matrix.D);
                if (Math.Abs(sx - sy) > Math.Max(sx, sy) * 1e-12)
                    throw Error("Organization.AnnotationStretchProfile", "Nonuniform annotation stretching requires an explicit appearance profile.");
                var id = edit.Add(PdfValue.Null);
                referenceMap.Add(reference.ReferenceId, id);
                annotationTransforms.Add(reference.ReferenceId, placement.Matrix);
                annotations[placement.Sheet].Add(PdfValue.Reference(id));
            }
        }

        PdfValue Destination(PdfValue value)
        {
            value = R(value);
            if (value.Kind != PdfValueKind.Array || value.Items.Count < 2 || value.Items[0].Kind != PdfValueKind.Reference || !byPage.TryGetValue(value.Items[0].ReferenceId, out var placement))
                return Rewrite(value, null);
            var view = value.Items[1];
            if (view.Kind != PdfValueKind.Name)
                throw Error("Organization.Destination", "A destination view must be a name.");
            var target = PdfValue.Reference(sheets[placement.Sheet]);
            var geometry = document.Pages[placement.Page].Geometry;
            if (view.NameValue == "Fit" || view.NameValue == "FitB")
                return FitRect(geometry.Boxes.Crop, placement, target);
            if (view.NameValue == "FitR" && value.Items.Count == 6)
                return FitRect(Rect(value.Items.Skip(2).ToArray()), placement, target);
            if (view.NameValue == "XYZ" && value.Items.Count == 5 && value.Items[2].Kind == PdfValueKind.Number && value.Items[3].Kind == PdfValueKind.Number)
            {
                var point = placement.Matrix.Transform(new(value.Items[2].NumberValue, value.Items[3].NumberValue));
                var zoom = value.Items[4];
                if (zoom.Kind == PdfValueKind.Number && zoom.NumberValue > 0)
                {
                    double scale = Math.Sqrt(placement.Matrix.A * placement.Matrix.A + placement.Matrix.B * placement.Matrix.B) / geometry.UserUnit;
                    zoom = PdfValues.Real(zoom.NumberValue / scale);
                }

                return PdfValue.Array(target, view, PdfValues.Real(point.X), PdfValues.Real(point.Y), zoom);
            }

            throw Error("Organization.DestinationViewProfile", "This destination view requires an explicit transformed-view profile.");
        }

        PdfValue FitRect(PdfRect rectangle, PdfImposedPage placement, PdfValue target)
        {
            var b = PdfGeometryMath.Transform(rectangle, placement.Matrix).Bounds;
            return PdfValue.Array(target, PdfValue.Name("FitR"), PdfValues.Real(b.X), PdfValues.Real(b.Y), PdfValues.Real(b.Right), PdfValues.Real(b.Top));
        }

        PdfObjectId? InheritedPage(PdfObjectId? id)
        {
            var seen = new HashSet<PdfObjectId>();
            while (id.HasValue)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 48);
                if (!seen.Add(id.Value))
                    throw Error("Organization.StructureCycle", "Structure inheritance contains a cycle.");
                var node = source.Resolve(id.Value);
                if (node.Kind != PdfValueKind.Dictionary)
                    return null;
                if (node.Entries.TryGetValue("Pg", out var pg) && pg.Kind == PdfValueKind.Reference)
                    return pg.ReferenceId;
                id = node.Entries.TryGetValue("P", out var parent) && parent.Kind == PdfValueKind.Reference ? parent.ReferenceId : null;
            }

            return null;
        }

        PdfValue MarkedChild(PdfValue value, PdfObjectId page)
        {
            if (value.Kind == PdfValueKind.Reference && (R(value).Kind == PdfValueKind.Array || R(value).Kind == PdfValueKind.Number))
                return MarkedChild(R(value), page);
            if (value.Kind == PdfValueKind.Number)
                return PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("MCR")), ("Pg", PdfValue.Reference(referenceMap[page])), ("Stm", PdfValue.Reference(formIds[page])), ("MCID", value));
            if (value.Kind == PdfValueKind.Array)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 16L + 32);
                return PdfValue.OwnedArray(value.Items.Select(v => MarkedChild(v, page)).ToList());
            }

            return Rewrite(value, null);
        }

        PdfValue Rewrite(PdfValue value, PdfObjectId? identity)
        {
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Reference)
                return referenceMap.TryGetValue(value.ReferenceId, out var mapped) ? PdfValue.Reference(mapped) : value;
            if (value.Kind == PdfValueKind.Array && value.Items.Count >= 2 && value.Items[0].Kind == PdfValueKind.Reference && byPage.ContainsKey(value.Items[0].ReferenceId) && value.Items[1].Kind == PdfValueKind.Name && new[]
            {
                "XYZ",
                "Fit",
                "FitB",
                "FitR",
                "FitH",
                "FitV",
                "FitBH",
                "FitBV"
            }.Contains(value.Items[1].NameValue))
                return Destination(value);
            if (value.Kind == PdfValueKind.Array)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 16L + 32);
                return PdfValue.OwnedArray(value.Items.Select(v => Rewrite(v, null)).ToList());
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return value;
            context.Consume(PdfResourceKind.ExpandedBytes, value.Entries.Count * 96L + 128);
            var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
            string? type = value.Entries.TryGetValue("Type", out var t) && t.Kind == PdfValueKind.Name ? t.NameValue : null;
            var inherited = value.Entries.TryGetValue("Pg", out var pg) && pg.Kind == PdfValueKind.Reference ? pg.ReferenceId : InheritedPage(identity);
            foreach (var pair in value.Entries)
            {
                if (pair.Key == "Dest" && (type == "Annot" || value.Entries.TryGetValue("Title", out var title) && title.Kind == PdfValueKind.String && value.Entries.ContainsKey("Parent")) || pair.Key == "D" && value.Entries.TryGetValue("S", out var s) && s.Kind == PdfValueKind.Name && s.NameValue == "GoTo" || pair.Key == "OpenAction" && R(pair.Value).Kind == PdfValueKind.Array)
                {
                    entries[pair.Key] = Destination(pair.Value);
                    continue;
                }

                if (pair.Key == "K" && type == "StructElem" && inherited.HasValue && byPage.ContainsKey(inherited.Value))
                {
                    entries[pair.Key] = MarkedChild(pair.Value, inherited.Value);
                    continue;
                }

                entries[pair.Key] = Rewrite(pair.Value, null);
            }

            if (type == "MCR" && inherited.HasValue && byPage.ContainsKey(inherited.Value) && !entries.ContainsKey("Stm"))
                entries["Stm"] = PdfValue.Reference(formIds[inherited.Value]);
            if (identity.HasValue && annotationTransforms.TryGetValue(identity.Value, out var matrix))
            {
                if (!value.Entries.TryGetValue("Rect", out var rect))
                    throw Error("Organization.AnnotationRectangle", "A link requires a rectangle.");
                entries["Rect"] = PdfGeometryMath.Transform(Rect(R(rect).Items), matrix).Bounds.Value;
                if (value.Entries.TryGetValue("QuadPoints", out var quads))
                    entries["QuadPoints"] = Points(R(quads), matrix);
                double scale = Math.Sqrt(matrix.A * matrix.A + matrix.B * matrix.B);
                if (value.Entries.TryGetValue("Border", out var border))
                {
                    var original = R(border);
                    if (original.Kind != PdfValueKind.Array || original.Items.Count < 3)
                        throw Error("Organization.AnnotationBorder", "An annotation border requires three numeric entries.");
                    var items = original.Items.Select(v => Rewrite(v, null)).ToArray();
                    for (int i = 0; i < 3; i++)
                        items[i] = PdfValues.Real(items[i].NumberValue * scale);
                    if (items.Length > 3 && R(items[3]).Kind == PdfValueKind.Array)
                        items[3] = PdfValue.OwnedArray(R(items[3]).Items.Select(v => PdfValues.Real(v.NumberValue * scale)).ToList());
                    entries["Border"] = PdfValue.Array(items);
                }

                if (value.Entries.TryGetValue("BS", out var borderStyle))
                {
                    var bs = PdfWriterCore.CopyDictionary(R(borderStyle).Entries, context);
                    if (bs.TryGetValue("W", out var width))
                        bs["W"] = PdfValues.Real(width.NumberValue * scale);
                    entries["BS"] = PdfValue.OwnedDictionary(bs);
                }
            }

            return value.Kind == PdfValueKind.Stream ? value.WithStreamDictionary(PdfValue.OwnedDictionary(entries)) : PdfValue.OwnedDictionary(entries);
        }

        PdfValue Points(PdfValue value, PdfMatrix matrix)
        {
            if (value.Kind != PdfValueKind.Array || (value.Items.Count & 1) != 0)
                throw Error("Organization.AnnotationPoints", "Annotation geometry requires coordinate pairs.");
            context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 32L + 32);
            var values = new List<PdfValue>();
            for (int i = 0; i < value.Items.Count; i += 2)
            {
                var point = matrix.Transform(new(value.Items[i].NumberValue, value.Items[i + 1].NumberValue));
                values.Add(PdfValues.Real(point.X));
                values.Add(PdfValues.Real(point.Y));
            }

            return PdfValue.OwnedArray(values);
        }

        PdfRect Rect(IReadOnlyList<PdfValue> values)
        {
            if (values.Count != 4 || values.Any(v => v.Kind != PdfValueKind.Number))
                throw Error("Organization.Rectangle", "A transformed rectangle requires four numeric bounds.");
            double x = Math.Min(values[0].NumberValue, values[2].NumberValue), y = Math.Min(values[1].NumberValue, values[3].NumberValue);
            return new(x, y, Math.Abs(values[2].NumberValue - values[0].NumberValue), Math.Abs(values[3].NumberValue - values[1].NumberValue));
        }

        var seenObjects = new HashSet<int>();
        foreach (var revision in source.Revisions)
            foreach (var entry in revision.Entries)
            {
                context.Checkpoint();
                if (!seenObjects.Add(entry.Id.Number) || entry.Kind == PdfCrossReferenceKind.Free)
                    continue;
                var rewritten = Rewrite(source.Resolve(entry.Id), entry.Id);
                if (annotationTransforms.ContainsKey(entry.Id))
                    edit.Set(referenceMap[entry.Id], rewritten);
                else
                    edit.Set(entry.Id, rewritten);
            }

        foreach (var old in oldPages)
            edit.Remove(old);
        foreach (var old in annotationTransforms.Keys)
            edit.Remove(old);
        for (int i = 0; i < count; i++)
        {
            var resources = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
            var operations = new List<PdfContentOperation>();
            foreach (var placement in placements.Where(p => p.Sheet == i && p.Page >= 0))
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 1024);
                string name = "Page" + placement.Page.ToString(System.Globalization.CultureInfo.InvariantCulture);
                resources[name] = PdfValue.Reference(formIds[oldPages[placement.Page]]);
                operations.Add(new(PdfOperator.SaveState));
                operations.Add(new(PdfOperator.Rectangle, PdfValues.Real(placement.Clip.X), PdfValues.Real(placement.Clip.Y), PdfValues.Real(placement.Clip.Width), PdfValues.Real(placement.Clip.Height)));
                operations.Add(new(PdfOperator.Clip));
                operations.Add(new(PdfOperator.EndPath));
                var m = placement.Matrix;
                operations.Add(new(PdfOperator.Transform, PdfValues.Real(m.A), PdfValues.Real(m.B), PdfValues.Real(m.C), PdfValues.Real(m.D), PdfValues.Real(m.E), PdfValues.Real(m.F)));
                operations.Add(new(PdfOperator.DrawObject, PdfValue.Name(name)));
                operations.Add(new(PdfOperator.RestoreState));
            }

            var program = PdfContentProgram.CreateCore(operations, PdfContentKind.Page, context);
            var page = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
            {
                ["Type"] = PdfValue.Name("Page"),
                ["Parent"] = PdfValue.Reference(pagesRootId),
                ["MediaBox"] = new PdfRect(0, 0, sheet.Width, sheet.Height).Value,
                ["Resources"] = PdfGraphicsNumbers.Dict(("XObject", PdfValue.OwnedDictionary(resources))),
                ["Contents"] = PdfValue.Reference(edit.Add(program.ToStream(context)))
            };
            if (annotations[i].Count > 0)
                page["Annots"] = PdfValue.OwnedArray(annotations[i]);
            edit.Set(sheets[i], PdfValue.OwnedDictionary(page));
        }

        edit.Set(pagesRootId, PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Pages")), ("Count", PdfValue.Integer(count)), ("Kids", PdfValue.Array(sheets.Select(PdfValue.Reference).ToArray()))));
        var updated = PdfWriterCore.CopyDictionary(edit.Resolve(rootId).Entries, context);
        updated.Remove("PageLabels");
        edit.Set(rootId, PdfValue.OwnedDictionary(updated));
        using var output = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
        using (var lease = new BorrowedStream(output, false, context))
            edit.WriteCore(lease, new(allowSignatureInvalidation: options.AllowSignatureInvalidation, minimumVersion: PdfVersionNumber.Text(minimumVersion)), context);
        return PdfPageOrganization.Read(output.Finish(), options.MaximumPages, context);
        PdfException Error(string code, string message) => PdfWriterCore.Error(context, code, message);
    }
}
