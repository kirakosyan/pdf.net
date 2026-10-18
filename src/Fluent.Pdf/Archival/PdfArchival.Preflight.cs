namespace Fluent.Pdf;
public static partial class PdfArchival
{
    private static PdfArchivalFinding[] Preflight(PdfGraphEdit graph, PdfArchivalProfile profile)
    {
        var context = graph.Context;
        var findings = new List<PdfArchivalFinding>();
        int part = Part(profile);
        var visited = new HashSet<PdfObjectId>();
        if (graph.Get(graph.Edit.Trailer, "Encrypt").Kind != PdfValueKind.Null)
            Add("Encryption", "trailer", "Archival output requires explicit plaintext input; decrypt protection first.");
        bool tagged = profile == PdfArchivalProfile.A1A || profile == PdfArchivalProfile.A2A || profile == PdfArchivalProfile.A3A;
        if (tagged && graph.Get(graph.Catalog, "StructTreeRoot").Kind != PdfValueKind.Dictionary)
            Add("StructureRequired", "catalog", "Level A conversion requires existing semantic structure and complete independent structure validation.");
        if (part == 1 && graph.Get(graph.Catalog, "OCProperties").Kind != PdfValueKind.Null)
            Add("OptionalContent", "catalog", "PDF/A-1 requires an explicit layer-flattening decision before conversion.");
        if (profile == PdfArchivalProfile.A4F)
        {
            var names = graph.Get(graph.Catalog, "Names");
            if (names.Kind != PdfValueKind.Dictionary || graph.Get(names, "EmbeddedFiles").Kind == PdfValueKind.Null)
                Add("EmbeddedFileRequired", "catalog", "PDF/A-4f requires an embedded-file name tree.");
        }

        foreach (var id in graph.Edit.ActiveIds(context))
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 64);
            if (visited.Add(id))
                Visit(graph.Edit.Resolve(id), id.Number + " " + id.Generation + " obj");
        }

        return findings.ToArray();
        void Visit(PdfValue value, string location)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            if (value.Kind == PdfValueKind.Reference)
                return; // Active indirect objects are inspected once, including unreachable active objects.
            if (value.Kind == PdfValueKind.Array)
            {
                foreach (var item in value.Items)
                    Visit(item, location);
                return;
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return;
            string type = Name(graph.Get(value, "Type")), subtype = Name(graph.Get(value, "Subtype")), action = Name(graph.Get(value, "S"));
            if (action == "JavaScript" || action == "Launch" || action == "Sound" || action == "Movie" || action == "Rendition" || action == "ImportData" || action == "SubmitForm")
                Add("ProhibitedAction", location, "An active action requires an explicit retention/removal policy before archival conversion.");
            if (value.Entries.ContainsKey("XFA") || Bool(graph.Get(value, "NeedAppearances")))
                Add("DynamicForms", location, "XFA and viewer-regenerated form appearances require an explicit static form workflow before archival conversion.");
            if (value.Kind == PdfValueKind.Stream && (value.Entries.ContainsKey("F") || value.Entries.ContainsKey("FFilter") || value.Entries.ContainsKey("FDecodeParms")))
                Add("ExternalStream", location, "Archival streams must be self-contained; external file filters require explicit import.");
            if (type == "EmbeddedFile" && part < 3)
                Add("AttachmentProfile", location, "This source profile requires attachment removal before PDF/A-1 or PDF/A-2 conversion; recursive archival attachment validation remains a separate mandatory profile.");
            if (type == "Font")
                Font(value, subtype, location);
            if (part == 1)
            {
                var softMask = graph.Get(value, "SMask");
                if (softMask.Kind != PdfValueKind.Null && Name(softMask) != "None" || type == "Group" && action == "Transparency" || subtype == "Transparency" || value.Entries.ContainsKey("ca") && !One(graph.Get(value, "ca")) || value.Entries.ContainsKey("CA") && !One(graph.Get(value, "CA")))
                    Add("Transparency", location, "PDF/A-1 transparency requires an explicit fidelity-preserving flattening operation.");
                var blend = graph.Get(value, "BM");
                if (blend.Kind != PdfValueKind.Null && Name(blend) != "Normal" && Name(blend) != "Compatible")
                    Add("BlendMode", location, "PDF/A-1 conversion cannot silently substitute a blend mode.");
                var filters = graph.Get(value, "Filter");
                if (Name(filters) == "JPXDecode" || filters.Kind == PdfValueKind.Array && filters.Items.Any(f => Name(graph.Resolve(f)) == "JPXDecode"))
                    Add("JpxProfile", location, "PDF/A-1 requires explicit JPEG 2000 decoding/re-encoding.");
            }

            foreach (var child in value.Entries.Values)
                Visit(child, location);
        }

        void Font(PdfValue font, string subtype, string location)
        {
            if (subtype == "Type0")
            {
                var descendants = graph.Get(font, "DescendantFonts");
                if (descendants.Kind != PdfValueKind.Array || descendants.Items.Count != 1)
                    Add("FontDescendant", location, "A composite archival font requires one valid descendant.");
                return;
            }

            if (subtype == "Type3")
            {
                if (graph.Get(font, "CharProcs").Kind != PdfValueKind.Dictionary)
                    Add("FontProgram", location, "A Type 3 archival font requires self-contained glyph procedures.");
                return;
            }

            var descriptor = graph.Get(font, "FontDescriptor");
            if (descriptor.Kind != PdfValueKind.Dictionary || !(new[]
            {
                "FontFile",
                "FontFile2",
                "FontFile3"
            }.Any(key => graph.Get(descriptor, key).Kind == PdfValueKind.Stream)))
                Add("FontNotEmbedded", location, "Every used archival font requires an embedded self-contained font program; no machine-font substitution is performed.");
        }

        void Add(string rule, string location, string message)
        {
            if (findings.Count >= 10000)
                throw Error(context, "FindingLimit", "The structural preflight exceeds its bounded finding inventory.");
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, (rule.Length + location.Length + message.Length) * 2L + 128);
            findings.Add(new("Archival." + rule, location, message));
        }

        static string Name(PdfValue value) => value.Kind == PdfValueKind.Name ? value.NameValue : "";
        static bool Bool(PdfValue value) => value.Kind == PdfValueKind.Boolean && value.BooleanValue;
        static bool One(PdfValue value) => value.Kind == PdfValueKind.Number && double.TryParse(value.NumberText, System.Globalization.NumberStyles.AllowLeadingSign | System.Globalization.NumberStyles.AllowDecimalPoint, System.Globalization.CultureInfo.InvariantCulture, out double number) && number == 1;
    }

    private static void Metadata(PdfGraphEdit graph, PdfArchivalOptions options)
    {
        var editor = new PdfMetadataEditor(graph);
        try
        {
            if (options.ReplaceMetadata && graph.Get(graph.Catalog, "Metadata").Kind != PdfValueKind.Null)
            {
                graph.Context.Report(new PdfDiagnostic("Archival.MetadataReplaced", "Explicit archival repair replaces the existing XMP packet and may omit custom metadata; standard Info fields are synchronized.", lossCategory: PdfLossCategory.OmittedContent));
                editor.SetXmp(null);
            }

            var identity = graph.Get(graph.Edit.Trailer, "ID");
            if (identity.Kind == PdfValueKind.Null)
                editor.SetIdentity(PdfSecurityCryptography.Random(16, graph.Context));
            else if (identity.Kind != PdfValueKind.Array || identity.Items.Count != 2 || identity.Items.Any(item => graph.Resolve(item).Kind != PdfValueKind.String || graph.Resolve(item).StringBytes.Count == 0))
                throw Error(graph.Context, "Identity", "Archival conversion requires valid nonempty file identifiers or an explicit identity repair first.");
            editor.Synchronize(PdfMetadataSynchronization.InformationToXmp);
            const string schema = "http://www.aiim.org/pdfa/ns/id/";
            string token = ProfileIdentifier(options.Profile);
            int part = Part(options.Profile);
            editor.SetXmpProperty(schema, "part", part.ToString(System.Globalization.CultureInfo.InvariantCulture));
            editor.SetXmpProperty(schema, "conformance", part == 4 ? token.Length > 1 ? token.Substring(1).ToUpperInvariant() : null : token.Substring(1).ToUpperInvariant());
            editor.SetXmpProperty(schema, "rev", part == 4 ? "2020" : null);
            editor.SetXmpProperty("http://purl.org/dc/elements/1.1/", "format", "application/pdf");
            editor.Commit();
        }
        finally
        {
            editor.Expire();
        }
    }
}
