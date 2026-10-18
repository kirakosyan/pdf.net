using System.Threading;

namespace Fluent.Pdf;
public sealed partial class PdfPageGraphics
{
    /// <summary>Extracts an invocation as a detached reusable resource, preserving encoded streams and effective resource scopes. Owner-dependent structure/optional-content graphs are rejected.</summary>
    public PdfGraphicsResource ExtractResource(int occurrenceIndex, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        var occurrence = Selected(occurrenceIndex);
        PdfGraphicsResource? result = null;
        var report = PdfFont.Run("ExtractGraphicsResource", "managed.images", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(Source, new(), context);
            var value = Detach(occurrence.Definition, occurrence.Resources, graph);
            result = PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.XObject, value, graph.Graph.Version, PdfResourceSet.Empty, context);
        }, PdfCapability.ImageCodecs);
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Extracts typed image samples, masks and owned display metadata without decoding or recompressing their bytes. Images whose geometry is only inside a codec require an explicit adapter.</summary>
    public PdfImageAsset ExtractImage(int occurrenceIndex, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        var occurrence = Selected(occurrenceIndex);
        if (occurrence.Kind == PdfGraphicsOccurrenceKind.Form)
            throw new ArgumentException("Select an image invocation.", nameof(occurrenceIndex));
        PdfImageAsset? result = null;
        var report = PdfFont.Run("ExtractImage", "managed.images", policy, cancellationToken, context =>
        {
            using var graph = new PdfGraphEdit(Source, new(), context);
            result = Image(Detach(occurrence.Definition, occurrence.Resources, graph));
            PdfImageAsset Image(PdfValue value)
            {
                using var depth = context.EnterScope(PdfResourceKind.Depth);
                context.Checkpoint();
                bool stencil = value.Entries.TryGetValue("ImageMask", out var im) && im.Kind == PdfValueKind.Boolean && im.BooleanValue;
                int Integer(string key)
                {
                    var n = graph.Get(value, key);
                    if (!n.TryGetInteger(out long number) || number < 1 || number > int.MaxValue)
                        throw graph.Error("Image.GeometryRequired", "Typed extraction requires explicit positive dimensions and sample precision.");
                    return (int)number;
                }

                int w = Integer("Width"), h = Integer("Height"), bpc = stencil ? 1 : Integer("BitsPerComponent");
                if (bpc != 1 && bpc != 2 && bpc != 4 && bpc != 8 && bpc != 16)
                    throw graph.Error("Image.Precision", "Typed PDF image extraction requires 1, 2, 4, 8 or 16 bits per component.");
                context.ConsumePixels(w, h);
                int components = stencil ? 1 : PdfImageColorResolver.Components(graph.Get(value, "ColorSpace"), PdfValue.Null, v => v, context);
                PdfImageAsset? mask = null;
                if (value.Entries.TryGetValue("SMask", out var smask) && smask.Kind != PdfValueKind.Null && !(smask.Kind == PdfValueKind.Name && smask.NameValue == "None"))
                {
                    if (smask.Kind != PdfValueKind.Stream)
                        throw graph.Error("Image.SoftMask", "A typed soft mask must be an image stream.");
                    mask = Image(smask);
                    if (mask.IsStencil || mask.Info.Components != 1 || mask.Info.Width != w || mask.Info.Height != h || mask.Info.BitsPerComponent != 8 && mask.Info.BitsPerComponent != 16 || mask.SoftMask != null || graph.Get(smask, "ColorSpace").Kind != PdfValueKind.Name || graph.Get(smask, "ColorSpace").NameValue != "DeviceGray")
                        throw graph.Error("Image.SoftMask", "A soft mask requires matching 8/16-bit DeviceGray geometry without another mask.");
                }

                if (value.Entries.TryGetValue("Decode", out var decode) && (decode.Kind != PdfValueKind.Array || decode.Items.Count != components * 2 || decode.Items.Any(v => v.Kind != PdfValueKind.Number)))
                    throw graph.Error("Image.Decode", "Decode requires one finite pair per image component.");
                var metadata = value.Entries.TryGetValue("Metadata", out var xml) && xml.Kind == PdfValueKind.Stream ? PdfImageMetadataCodec.Read(xml, context) : new PdfImageMetadata();
                var resource = PdfGraphicsResource.CreateCore(PdfGraphicsResourceCategory.XObject, value, graph.Graph.Version, PdfResourceSet.Empty, context);
                return new(resource, new(w, h, components, bpc), metadata, mask);
            }
        }, PdfCapability.ImageCodecs);
        void Complete(PdfImageAsset image)
        {
            PdfImages.Complete(image, report);
            if (image.SoftMask != null)
                Complete(image.SoftMask);
        }

        Complete(result!);
        return result!;
    }

    internal static PdfValue Detach(PdfValue root, PdfValue resources, PdfGraphEdit graph)
    {
        var context = graph.Context;
        var active = new HashSet<PdfValue>();
        PdfValue Color(PdfValue value, PdfValue scope, HashSet<string> names)
        {
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            value = graph.Resolve(value);
            if (value.Kind == PdfValueKind.Name)
            {
                string name = value.NameValue;
                if (name == "G")
                    return PdfValue.Name("DeviceGray");
                if (name == "RGB")
                    return PdfValue.Name("DeviceRGB");
                if (name == "CMYK")
                    return PdfValue.Name("DeviceCMYK");
                if (name == "DeviceGray" || name == "DeviceRGB" || name == "DeviceCMYK")
                    return value;
                context.Consume(PdfResourceKind.ExpandedBytes, 64);
                if (!names.Add(name))
                    throw graph.Error("Image.ColorSpaceCycle", "Image color resources contain a cycle.");
                try
                {
                    var spaces = graph.Get(scope, "ColorSpace");
                    if (!spaces.Entries.TryGetValue(name, out var entry))
                        throw graph.Error("Image.NamedColorSpace", "The image color resource is absent.");
                    return Color(entry, scope, names);
                }
                finally
                {
                    names.Remove(name);
                }
            }

            if (value.Kind == PdfValueKind.Array && value.Items.Count > 0)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 16L + 32);
                var items = value.Items.ToArray();
                string? family = items[0].Kind == PdfValueKind.Name ? items[0].NameValue : null;
                if (family == "I")
                    items[0] = PdfValue.Name("Indexed");
                if ((family == "Indexed" || family == "I") && items.Length >= 2)
                    items[1] = Color(items[1], scope, names);
                if ((family == "Separation" || family == "DeviceN") && items.Length >= 3)
                    items[2] = Color(items[2], scope, names);
                context.Consume(PdfResourceKind.ExpandedBytes, items.Length * 16L + 32);
                return PdfValue.Array(items);
            }

            return value;
        }

        PdfValue Copy(PdfValue declared, PdfValue scope)
        {
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 64);
            var value = graph.Resolve(declared);
            if (value.Kind == PdfValueKind.Array)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 16L);
                return PdfValue.OwnedArray(value.Items.Select(v => Copy(v, scope)).ToList());
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return value;
            if (!active.Add(value))
                throw graph.Error("Image.DetachCycle", "A cyclic resource graph requires an explicit owner-preserving import profile.");
            try
            {
                if (value.Entries.ContainsKey("OC") || value.Entries.ContainsKey("StructParent") || value.Entries.ContainsKey("StructParents"))
                    throw graph.Error("Image.OwnerDependentResource", "Optional-content and structure-associated resources require an explicit owner-preserving import profile.");
                var entries = graph.Copy(value);
                bool form = PdfPageImportSource.Name(value, "Subtype") == "Form";
                if (form)
                {
                    scope = value.Entries.ContainsKey("Resources") ? graph.Get(value, "Resources") : scope;
                    entries["Resources"] = scope;
                }

                if (PdfPageImportSource.Name(value, "Subtype") == "Image" || ReferenceEquals(value, root))
                {
                    if (entries.TryGetValue("ColorSpace", out var color))
                        entries["ColorSpace"] = Color(color, scope, new(StringComparer.Ordinal));
                    entries["Type"] = PdfValue.Name("XObject");
                    if (!form)
                        entries["Subtype"] = PdfValue.Name("Image");
                    if (entries.TryGetValue("Filter", out var filters))
                        entries["Filter"] = filters.Kind == PdfValueKind.Name ? PdfValue.Name(PdfStreamFilters.Canonical(filters.NameValue)) : filters.Kind == PdfValueKind.Array ? PdfValue.Array(filters.Items.Select(f => PdfValue.Name(PdfStreamFilters.Canonical(f.NameValue))).ToArray()) : filters;
                }

                foreach (string key in entries.Keys.ToArray())
                    entries[key] = Copy(entries[key], scope);
                var dictionary = PdfValue.OwnedDictionary(entries);
                return value.Kind == PdfValueKind.Stream ? value.WithStreamDictionary(dictionary) : dictionary;
            }
            finally
            {
                active.Remove(value);
            }
        }

        return Copy(root, resources);
    }
}
