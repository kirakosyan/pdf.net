namespace Fluent.Pdf.Rendering;
internal static class PdfRenderProfile
{
    internal static void Validate(PdfPageSource page, PdfRenderOptions options, PdfOperationContext context)
    {
        var source = page.Document;
        var root = Resolve(source.Revisions[0].Trailer.Entries["Root"]);
        if (source.Version == "2.0" || root.Entries.ContainsKey("Extensions") || Get(root, "Version").Kind == PdfValueKind.Name && Get(root, "Version").NameValue == "2.0")
            throw Error("Render.VersionProfile", "PDF 2.0 and extension rendering require a separately qualified engine profile.");
        var form = Get(root, "AcroForm");
        if (Get(form, "XFA").Kind != PdfValueKind.Null)
            throw Error("Render.XfaProfile", "XFA rendering is outside this candidate profile.");
        if (options.Annotations && Get(form, "NeedAppearances")is var need && need.Kind == PdfValueKind.Boolean && need.BooleanValue)
            throw Error("Render.AppearanceRequired", "Rendering requires explicit current widget appearances; viewer regeneration is not implicit.");
        PdfPageContent.Read(page, PdfUnknownOperatorPolicy.Reject, null, PdfProviderRegistry.Default, context);
        var visited = new HashSet<PdfObjectId>();
        Visit(Get(page.EffectiveAttributes, "Resources"));
        if (options.Annotations)
        {
            var annotations = Get(page.Dictionary, "Annots");
            if (annotations.Kind != PdfValueKind.Null && annotations.Kind != PdfValueKind.Array)
                throw Error("Render.Annotations", "Page annotations must resolve to an array.");
            if (annotations.Kind == PdfValueKind.Array)
                foreach (var item in annotations.Items)
                {
                    var annotation = Resolve(item);
                    if (annotation.Kind != PdfValueKind.Dictionary)
                        throw Error("Render.Annotations", "An annotation must resolve to a dictionary.");
                    var flags = Get(annotation, "F");
                    int bits = 0;
                    if (flags.Kind != PdfValueKind.Null && (flags.Kind != PdfValueKind.Number || !int.TryParse(flags.NumberText, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out bits)))
                        throw Error("Render.Annotations", "Annotation flags must be a nonnegative integer.");
                    if ((bits & 3) != 0 || (options.Print ? (bits & 4) == 0 : (bits & 32) != 0))
                        continue;
                    var appearances = Get(annotation, "AP");
                    string subtype = Name(Get(annotation, "Subtype"));
                    if (subtype != "Link" && subtype != "Popup" && Get(appearances, "N").Kind == PdfValueKind.Null)
                        throw Error("Render.AppearanceRequired", "A visible annotation requires an explicit normal appearance in this source profile.");
                    if (subtype == "Widget")
                    {
                        var normal = Get(appearances, "N");
                        if (normal.Kind == PdfValueKind.Dictionary && (Get(annotation, "AS")is var state && (state.Kind != PdfValueKind.Name || Get(normal, state.NameValue).Kind != PdfValueKind.Stream)) || normal.Kind != PdfValueKind.Dictionary && normal.Kind != PdfValueKind.Stream)
                            throw Error("Render.AppearanceRequired", "A widget must select an existing normal appearance stream.");
                    }

                    Visit(appearances);
                }
        }

        PdfValue Resolve(PdfValue value) => ManagedPdfProvider.Resolve(source, value, context);
        PdfValue Get(PdfValue dictionary, string key) => dictionary.Kind == PdfValueKind.Dictionary || dictionary.Kind == PdfValueKind.Stream ? dictionary.Entries.TryGetValue(key, out var value) ? Resolve(value) : PdfValue.Null : PdfValue.Null;
        string Name(PdfValue value) => value.Kind == PdfValueKind.Name ? value.NameValue : "";
        void Visit(PdfValue value)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            while (value.Kind == PdfValueKind.Reference)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 48);
                if (!visited.Add(value.ReferenceId))
                    return;
                value = source.Resolve(value.ReferenceId);
            }

            if (value.Kind == PdfValueKind.Array)
            {
                foreach (var child in value.Items)
                    Visit(child);
                return;
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return;
            if (Name(Get(value, "Type")) == "Font")
                Font(value);
            if (value.Kind == PdfValueKind.Stream)
            {
                if (value.Entries.ContainsKey("F"))
                    throw Error("Render.ExternalStream", "External stream data is not loaded by the offline renderer.");
                if (Name(Get(value, "Subtype")) == "Form")
                {
                    var resolved = PdfFilterDeclarations.Resolve(value, id => source.Resolve(id), context);
                    using var decoded = PdfFilters.Decode(resolved, context, PdfProviderRegistry.Default);
                    var bytes = PdfByteStore.Read(decoded, context);
                    PdfContentProgram.ParseCore(bytes, PdfContentKind.Page, PdfUnknownOperatorPolicy.Reject, null, context);
                }
            }

            foreach (var pair in value.Entries)
            {
                // Passive action/parent/page links cannot affect the raster and are never followed or executed here.
                if (pair.Key == "Parent" || pair.Key == "P" || pair.Key == "A" || pair.Key == "AA" || pair.Key == "Dest")
                    continue;
                Visit(pair.Value);
            }
        }

        void Font(PdfValue font)
        {
            string kind = Name(Get(font, "Subtype"));
            if (kind == "Type3")
                return;
            if (kind == "Type0")
            {
                var descendants = Get(font, "DescendantFonts");
                if (descendants.Kind != PdfValueKind.Array || descendants.Items.Count != 1)
                    throw Error("Render.FontProfile", "A composite font requires one explicit descendant.");
                Font(Resolve(descendants.Items[0]));
                return;
            }

            if (kind != "Type1" && kind != "TrueType" && kind != "CIDFontType0" && kind != "CIDFontType2")
                throw Error("Render.FontProfile", "The font subtype is outside this rendering profile.");
            var descriptor = Get(font, "FontDescriptor");
            if (new[]
            {
                "FontFile",
                "FontFile2",
                "FontFile3"
            }.Any(key => Get(descriptor, key).Kind == PdfValueKind.Stream))
                return;
            string name = Name(Get(font, "BaseFont"));
            if (kind == "Type1" && new[]
            {
                "Courier",
                "Courier-Bold",
                "Courier-Oblique",
                "Courier-BoldOblique",
                "Helvetica",
                "Helvetica-Bold",
                "Helvetica-Oblique",
                "Helvetica-BoldOblique",
                "Times-Roman",
                "Times-Bold",
                "Times-Italic",
                "Times-BoldItalic",
                "Symbol",
                "ZapfDingbats"
            }.Contains(name))
                return;
            throw Error("Render.FontRequired", "Nonstandard fonts must be embedded; machine-font discovery and implicit replacement are disabled.");
        }

        PdfException Error(string code, string message) => PdfWriterCore.Error(context, code, message);
    }
}
