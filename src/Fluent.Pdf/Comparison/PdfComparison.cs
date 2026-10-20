using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Independent comparison dimensions; equality in one dimension never implies equality in another.</summary>
public enum PdfDifferenceKind
{
    /// <summary>Ordinal page presence changed.</summary>
    Page,
    /// <summary>Logical extracted text changed.</summary>
    Text,
    /// <summary>Page or advance-cell geometry changed.</summary>
    Geometry,
    /// <summary>Info or exact XMP packet changed.</summary>
    Metadata,
    /// <summary>Rendered samples changed outside the explicit channel tolerance.</summary>
    Visual
}
/// <summary>Explicit ordinal-page comparison choices. No page matching/reflow inference is implied.</summary>
public sealed class PdfComparisonOptions
{
    /// <summary>Captures dimensions, finite displayed-point tolerance, pixel-channel tolerance and exact volatile Info keys. XMP is compared by exact decoded bytes.</summary>
    public PdfComparisonOptions(bool text = true, bool geometry = true, bool metadata = true, bool visual = false, double geometryTolerance = .5, byte pixelTolerance = 0, string[]? ignoredInformationKeys = null, PdfRenderOptions? rendering = null)
    {
        if (double.IsNaN(geometryTolerance) || double.IsInfinity(geometryTolerance) || geometryTolerance < 0 || geometryTolerance > 1000)
            throw new ArgumentOutOfRangeException(nameof(geometryTolerance));
        var ignored = ignoredInformationKeys ?? Array.Empty<string>();
        if (ignored.Length > 128 || ignored.Any(k => string.IsNullOrEmpty(k) || k.Length > 256) || ignored.Distinct(StringComparer.Ordinal).Count() != ignored.Length)
            throw new ArgumentException("Distinct bounded Info keys are required.", nameof(ignoredInformationKeys));
        Text = text;
        Geometry = geometry;
        Metadata = metadata;
        Visual = visual;
        GeometryTolerance = geometryTolerance;
        PixelTolerance = pixelTolerance;
        IgnoredInformationKeys = Array.AsReadOnly((string[])ignored.Clone());
        Rendering = rendering ?? new();
    }
    /// <summary>Gets text comparison selection.</summary>
    public bool Text
    {
        get;
    }
    /// <summary>Gets geometry comparison selection.</summary>
    public bool Geometry
    {
        get;
    }
    /// <summary>Gets Info/XMP comparison selection.</summary>
    public bool Metadata
    {
        get;
    }
    /// <summary>Gets explicit renderer comparison selection.</summary>
    public bool Visual
    {
        get;
    }
    /// <summary>Gets displayed-point tolerance.</summary>
    public double GeometryTolerance
    {
        get;
    }
    /// <summary>Gets maximum ignored absolute channel difference.</summary>
    public byte PixelTolerance
    {
        get;
    }
    /// <summary>Gets exact case-sensitive ignored Info keys.</summary>
    public IReadOnlyList<string> IgnoredInformationKeys
    {
        get;
    }
    /// <summary>Gets explicit render policy including annotations, layers/print intent and DPI.</summary>
    public PdfRenderOptions Rendering
    {
        get;
    }
}
/// <summary>One detached difference with exact page/object provenance on both inputs.</summary>
public sealed class PdfDifference
{
    internal PdfDifference(PdfDifferenceKind kind, int? page, PdfObjectId? beforeId, PdfObjectId? afterId, string before, string after)
    {
        Kind = kind;
        PageIndex = page;
        BeforeObject = beforeId;
        AfterObject = afterId;
        Before = before;
        After = after;
    }
    /// <summary>Gets independent difference dimension.</summary>
    public PdfDifferenceKind Kind
    {
        get;
    }
    /// <summary>Gets ordinal page, or null for document metadata.</summary>
    public int? PageIndex
    {
        get;
    }
    /// <summary>Gets original page/catalog identity when present.</summary>
    public PdfObjectId? BeforeObject
    {
        get;
    }
    /// <summary>Gets revised page/catalog identity when present.</summary>
    public PdfObjectId? AfterObject
    {
        get;
    }
    /// <summary>Gets original observation.</summary>
    public string Before
    {
        get;
    }
    /// <summary>Gets revised observation.</summary>
    public string After
    {
        get;
    }
}
/// <summary>A detached comparison report. Zero differences only covers the selected dimensions and explicit tolerances.</summary>
public sealed class PdfComparisonResult
{
    private readonly PdfDocumentSnapshot _after;
    internal PdfComparisonResult(List<PdfDifference> differences, PdfComparisonOptions options, PdfOperationReport report, PdfDocumentSnapshot after)
    {
        Differences = differences.AsReadOnly();
        Options = options;
        OperationReport = report;
        _after = after;
    }
    /// <summary>Gets stable dimension/page/key ordered differences.</summary>
    public IReadOnlyList<PdfDifference> Differences
    {
        get;
    }
    /// <summary>Gets exact comparison choices.</summary>
    public PdfComparisonOptions Options
    {
        get;
    }
    /// <summary>Gets shared operation accounting and diagnostics.</summary>
    public PdfOperationReport OperationReport
    {
        get;
    }
    /// <summary>Writes version-one JSON or escaped static HTML to a borrowed output under fresh export limits.</summary>
    public PdfOperationReport WriteReport(Stream output, bool html = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed stream is required.", nameof(output));
        return PdfFont.Run("WriteComparisonReport", "managed.comparison", policy, cancellationToken, c =>
        {
            using var sink = new BorrowedStream(output, false, c);
            void Write(string value)
            {
                c.Checkpoint();
                c.Consume(PdfResourceKind.ExpandedBytes, value.Length * 5L + 32);
                var bytes = Encoding.UTF8.GetBytes(value);
                sink.Write(bytes, 0, bytes.Length);
            }
            if (!html)
            {
                Write("{\"schemaVersion\":1,\"options\":{\"text\":" + Options.Text.ToString().ToLowerInvariant() + ",\"geometry\":" + Options.Geometry.ToString().ToLowerInvariant() + ",\"metadata\":" + Options.Metadata.ToString().ToLowerInvariant() + ",\"visual\":" + Options.Visual.ToString().ToLowerInvariant() + ",\"geometryTolerance\":" + Options.GeometryTolerance.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ",\"pixelTolerance\":" + Options.PixelTolerance + ",\"ignoredInformationKeys\":[");
                for (int i = 0; i < Options.IgnoredInformationKeys.Count; i++)
                {
                    c.Checkpoint();
                    if (i > 0)
                        Write(",");
                    Write(Quote(Options.IgnoredInformationKeys[i]));
                }
                Write("],\"dpiX\":" + Options.Rendering.DpiX.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ",\"dpiY\":" + Options.Rendering.DpiY.ToString("R", System.Globalization.CultureInfo.InvariantCulture) + ",\"annotations\":" + Options.Rendering.Annotations.ToString().ToLowerInvariant() + ",\"print\":" + Options.Rendering.Print.ToString().ToLowerInvariant() + "},\"differences\":[");
            }
            else
                Write("<!doctype html><meta charset=\"utf-8\"><title>PDF comparison</title><p>Comparison uses the explicitly selected dimensions and tolerances.</p><table><tr><th>Kind</th><th>Page</th><th>Before</th><th>After</th></tr>");
            bool first = true;
            foreach (var d in Differences)
            {
                c.Consume(PdfResourceKind.Objects, 1);
                c.Consume(PdfResourceKind.ExpandedBytes, (d.Before.Length + d.After.Length) * 16L + 1024);
                if (html)
                    Write("<tr><td>" + Escape(d.Kind.ToString()) + "</td><td>" + d.PageIndex + "</td><td>" + Escape(d.Before) + "</td><td>" + Escape(d.After) + "</td></tr>");
                else
                {
                    if (!first)
                        Write(",");
                    Write("{\"kind\":" + Quote(d.Kind.ToString()) + ",\"page\":" + (d.PageIndex?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "null") + ",\"beforeObject\":" + Quote(d.BeforeObject?.ToString() ?? "") + ",\"afterObject\":" + Quote(d.AfterObject?.ToString() ?? "") + ",\"before\":" + Quote(d.Before) + ",\"after\":" + Quote(d.After) + "}");
                }
                first = false;
            }
            Write(html ? "</table>" : "]}");
        }, PdfCapability.ReadDocument);
    }
    /// <summary>Creates a separate annotated copy of the revised snapshot with explicit comment markers for each difference. Deleted-page/document differences attach to page zero; full details remain in the structured report. Tagged destinations require a separate association profile.</summary>
    public PdfDocument CreateAnnotatedDocument(PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        return PdfGraphEdit.Run("AnnotateComparison", _after, options, policy, cancellationToken, g =>
        {
            if (g.Snapshot.Pages.Count == 0 || g.Catalog.Entries.ContainsKey("StructTreeRoot"))
                throw g.Error("Compare.AnnotationProfile", "Comparison markers require a nonempty untagged destination or a separate structure association profile.");
            var occupied = new HashSet<string>(PdfAnnotationSet.Read(g).Where(a => a.Identifier != null).Select(a => a.Identifier!), StringComparer.Ordinal);
            using var program = new MemoryStream(Encoding.ASCII.GetBytes("q 1 0 0 RG 1 w 1 1 10 10 re S Q"));
            var bytes = PdfByteStore.Read(program, g.Context);
            var appearance = g.Edit.Add(PdfValue.StreamValue(PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("XObject")), ("Subtype", PdfValue.Name("Form")), ("BBox", PdfGraphicsNumbers.Array(0, 0, 12, 12)), ("Resources", PdfGraphicsNumbers.Dict())), bytes, 0, bytes.Length));
            int index = 0;
            foreach (var difference in Differences)
            {
                g.Context.Checkpoint();
                g.Context.Consume(PdfResourceKind.Objects, 2);
                g.Context.Consume(PdfResourceKind.ExpandedBytes, 2048);
                int page = difference.PageIndex.HasValue && difference.PageIndex.Value < g.Snapshot.Pages.Count ? difference.PageIndex.Value : 0;
                var binding = g.Snapshot.Pages[page].Source!;
                var crop = binding.Geometry.Boxes.Crop;
                if (crop.Width <= 0 || crop.Height <= 0)
                    throw g.Error("Compare.AnnotationProfile", "Markers require a nonempty effective CropBox.");
                double width = Math.Min(12, crop.Width), height = Math.Min(12, crop.Height);
                var rectangle = new PdfRect(crop.X, crop.Top - height, width, height);
                string name;
                do
                {
                    g.Context.Checkpoint();
                    g.Context.Consume(PdfResourceKind.Objects, 1);
                    name = "FP-Compare-" + index++;
                } while (!occupied.Add(name));
                var entries = PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Annot")), ("Subtype", PdfValue.Name("Text")), ("Rect", rectangle.Value), ("P", PdfValue.Reference(binding.ObjectId)), ("NM", PdfTextStrings.Encode(name, g.Context)), ("Contents", PdfTextStrings.Encode(difference.Kind + " difference; see the comparison report for exact before/after observations.", g.Context)), ("AP", PdfGraphicsNumbers.Dict(("N", PdfValue.Reference(appearance)))));
                var existing = g.Get(g.Edit.Resolve(binding.ObjectId), "Annots");
                g.Context.Consume(PdfResourceKind.ExpandedBytes, (existing.Kind == PdfValueKind.Array ? existing.Items.Count : 0) * 16L + 64);
                var annotations = existing.Kind == PdfValueKind.Array ? existing.Items.ToList() : new List<PdfValue>();
                annotations.Add(PdfValue.Reference(g.Edit.Add(entries)));
                PdfAnnotationSet.SetPageAnnotations(g, page, annotations);
            }
            PdfAnnotationSet.ValidateRelationships(PdfAnnotationSet.Read(g), g);
        }, "1.7");
    }

    private static string Escape(string text) => text.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;").Replace("\"", "&quot;").Replace("'", "&#39;");
    private static string Quote(string text)
    {
        var b = new StringBuilder("\"");
        foreach (char ch in text)
        {
            if (ch == '"' || ch == '\\')
                b.Append('\\').Append(ch);
            else if (ch < 32 || char.IsSurrogate(ch))
                b.Append("\\u").Append(((int)ch).ToString("x4", System.Globalization.CultureInfo.InvariantCulture));
            else
                b.Append(ch);
        }
        return b.Append('"').ToString();
    }
}
/// <summary>Bounded original ordinal-page text, geometry, metadata and optional raster comparison.</summary>
public static class PdfComparison
{
    /// <summary>Compares both inputs under one shared context. Optional providers are explicit; no renderer, font or action is discovered or executed implicitly.</summary>
    public static PdfComparisonResult Compare(PdfDocumentSnapshot before, PdfDocumentSnapshot after, PdfComparisonOptions? options = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (before == null || after == null)
            throw new ArgumentNullException(before == null ? nameof(before) : nameof(after));
        options ??= new();
        providers ??= PdfProviderRegistry.Empty;
        if (options.Visual)
            _ = providers.GetRequired<IPdfPageRenderer>(PdfCapability.RenderPages);
        var differences = new List<PdfDifference>();
        var report = PdfFont.Run("CompareDocuments", "managed.comparison", policy ?? before.Options.OperationPolicy, cancellationToken, c =>
        {
            using var a = new PdfGraphEdit(before, new(allowSignatureInvalidation: true), c);
            using var b = new PdfGraphEdit(after, new(allowSignatureInvalidation: true), c);
            void Add(PdfDifferenceKind kind, int? page, string oldValue, string newValue)
            {
                c.Checkpoint();
                c.Consume(PdfResourceKind.Objects, 1);
                c.Consume(PdfResourceKind.ExpandedBytes, (oldValue.Length + newValue.Length) * 2L + 256);
                differences.Add(new(kind, page, page.HasValue && page.Value < a.Snapshot.Pages.Count ? a.Snapshot.Pages[page.Value].Source?.ObjectId : !page.HasValue ? a.Edit.Trailer.Entries["Root"].ReferenceId : null, page.HasValue && page.Value < b.Snapshot.Pages.Count ? b.Snapshot.Pages[page.Value].Source?.ObjectId : !page.HasValue ? b.Edit.Trailer.Entries["Root"].ReferenceId : null, oldValue, newValue));
            }
            int count = Math.Max(a.Snapshot.Pages.Count, b.Snapshot.Pages.Count);
            for (int page = 0; page < count; page++)
            {
                c.Checkpoint();
                if (page >= a.Snapshot.Pages.Count || page >= b.Snapshot.Pages.Count)
                {
                    Add(PdfDifferenceKind.Page, page, page < a.Snapshot.Pages.Count ? "present" : "absent", page < b.Snapshot.Pages.Count ? "present" : "absent");
                    continue;
                }
                if (options.Geometry && !a.Snapshot.Pages[page].Geometry.Equals(b.Snapshot.Pages[page].Geometry))
                    Add(PdfDifferenceKind.Geometry, page, "original page geometry", "revised page geometry");
                if (options.Text || options.Geometry)
                {
                    var left = new PdfTextExtractionEngine(a, new(), providers).Read(page);
                    var right = new PdfTextExtractionEngine(b, new(), providers).Read(page);
                    if (options.Text && left.Text != right.Text)
                        Add(PdfDifferenceKind.Text, page, left.Text, right.Text);
                    if (options.Geometry && (left.Characters.Count != right.Characters.Count || left.Characters.Where((ch, i) => !Near(ch.Quad.Bounds, right.Characters[i].Quad.Bounds, options.GeometryTolerance)).Any()))
                        Add(PdfDifferenceKind.Geometry, page, "original advance cells", "revised advance cells");
                }
                if (options.Visual)
                {
                    var left = PdfRendering.RenderCore(a, page, providers, options.Rendering, c);
                    var right = PdfRendering.RenderCore(b, page, providers, options.Rendering, c);
                    long changed = 0;
                    if (left.Request.Width != right.Request.Width || left.Request.Height != right.Request.Height)
                        Add(PdfDifferenceKind.Visual, page, $"{left.Request.Width}x{left.Request.Height}", $"{right.Request.Width}x{right.Request.Height}");
                    else
                    {
                        for (long i = 0; i < left.Samples.Length; i++)
                        {
                            if ((i & 4095) == 0)
                                c.Checkpoint();
                            if (Math.Abs(left.Samples.At(i) - right.Samples.At(i)) > options.PixelTolerance)
                                changed++;
                        }
                        if (changed > 0)
                            Add(PdfDifferenceKind.Visual, page, "0 changed channels", changed.ToString(System.Globalization.CultureInfo.InvariantCulture) + " changed channels");
                    }
                }
            }
            if (options.Metadata)
            {
                var left = a.Get(a.Edit.Trailer, "Info");
                var right = b.Get(b.Edit.Trailer, "Info");
                var keys = (left.Kind == PdfValueKind.Null ? Array.Empty<string>() : left.Entries.Keys).Concat(right.Kind == PdfValueKind.Null ? Array.Empty<string>() : right.Entries.Keys).Distinct(StringComparer.Ordinal).OrderBy(k => k, StringComparer.Ordinal);
                foreach (var key in keys)
                {
                    if (options.IgnoredInformationKeys.Contains(key, StringComparer.Ordinal))
                        continue;
                    string x = Info(a, left, key), y = Info(b, right, key);
                    if (x != y)
                        Add(PdfDifferenceKind.Metadata, null, "Info/" + key + ":" + x, "Info/" + key + ":" + y);
                }
                string xmpA = Xmp(a), xmpB = Xmp(b);
                if (xmpA != xmpB)
                    Add(PdfDifferenceKind.Metadata, null, "XMP:" + xmpA, "XMP:" + xmpB);
            }
        }, PdfCapability.ReadDocument);
        return new(differences, options, report, after);
    }
    private static bool Near(PdfRect a, PdfRect b, double tolerance) => Math.Abs(a.X - b.X) <= tolerance && Math.Abs(a.Y - b.Y) <= tolerance && Math.Abs(a.Width - b.Width) <= tolerance && Math.Abs(a.Height - b.Height) <= tolerance;
    private static string Info(PdfGraphEdit g, PdfValue info, string key)
    {
        var v = g.Get(info, key);
        return v.Kind switch
        {
            PdfValueKind.Null => "absent",
            PdfValueKind.String => "text:" + PdfTextStrings.Decode(v, g.Context),
            PdfValueKind.Name => "name:" + v.NameValue,
            PdfValueKind.Number => "number:" + v.NumberText,
            PdfValueKind.Boolean => v.BooleanValue ? "true" : "false",
            _ => throw g.Error("Compare.MetadataProfile", "Complex Info values require a separate comparison profile.")
        };
    }
    private static string Xmp(PdfGraphEdit g)
    {
        var v = g.Get(g.Catalog, "Metadata");
        if (v.Kind == PdfValueKind.Null)
            return "absent";
        if (v.Kind != PdfValueKind.Stream)
            throw g.Error("Compare.MetadataProfile", "XMP must be a stream.");
        using var decoded = PdfFilters.Decode(PdfFilterDeclarations.Resolve(v, id => g.Resolve(PdfValue.Reference(id)), g.Context), g.Context);
        return PdfArchival.Digest(PdfByteStore.Read(decoded, g.Context), g.Context);
    }
}
