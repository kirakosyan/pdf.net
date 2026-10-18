using System.Collections.ObjectModel;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>The standard resource dictionary containing a named reusable graphics object.</summary>
public enum PdfGraphicsResourceCategory
{
    /// <summary>Font resources.</summary>
    Font,
    /// <summary>Color spaces.</summary>
    ColorSpace,
    /// <summary>Tiling and shading patterns.</summary>
    Pattern,
    /// <summary>Shading dictionaries.</summary>
    Shading,
    /// <summary>Extended graphics states.</summary>
    ExtGState,
    /// <summary>Form and image XObjects.</summary>
    XObject
}

/// <summary>One immutable exact resource name and object binding.</summary>
public sealed class PdfResourceBinding
{
    /// <summary>Captures a PDF name and immutable graphics resource.</summary>
    public PdfResourceBinding(string name, PdfGraphicsResource resource)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 127)
            throw new ArgumentException("A bounded resource name is required.", nameof(name));
        PdfValue.Name(name);
        Name = name;
        Resource = resource ?? throw new ArgumentNullException(nameof(resource));
    }

    /// <summary>Gets its exact name, without a slash.</summary>
    public string Name { get; }
    /// <summary>Gets its immutable resource.</summary>
    public PdfGraphicsResource Resource { get; }
}

/// <summary>An explicit immutable resource scope. Duplicate names in one category are rejected.</summary>
public sealed class PdfResourceSet
{
    /// <summary>Captures at most 4096 explicit bindings; no global resource lookup occurs.</summary>
    public PdfResourceSet(params PdfResourceBinding[] bindings)
    {
        if (bindings == null || bindings.Length > 4096)
            throw new ArgumentException("At most 4096 bindings are allowed.", nameof(bindings));
        var seen = new HashSet<(PdfGraphicsResourceCategory, string)>();
        foreach (var binding in bindings)
            if (binding == null || !seen.Add((binding.Resource.Category, binding.Name)))
                throw new ArgumentException("Bindings must be nonnull and unique within their resource category.", nameof(bindings));
        Bindings = new ReadOnlyCollection<PdfResourceBinding>((PdfResourceBinding[])bindings.Clone());
    }

    /// <summary>Gets the explicit captured bindings.</summary>
    public IReadOnlyList<PdfResourceBinding> Bindings { get; }
    /// <summary>Gets a shared empty immutable scope.</summary>
    public static PdfResourceSet Empty { get; } = new();
    internal string MinimumVersion => Bindings.Count == 0 ? "1.2" : PdfVersionNumber.Text(Bindings.Max(x => PdfVersionNumber.Parse(x.Resource.MinimumVersion)));
}

/// <summary>A detached immutable resource graph. Nested streams become indirect objects when saved.</summary>
public sealed class PdfGraphicsResource
{
    internal PdfGraphicsResource(PdfGraphicsResourceCategory category, PdfValue definition, string version, PdfResourceSet? dependencies = null, PdfFontResource? font = null, Func<PdfResourceWriter, PdfValue>? materialize = null)
    {
        Category = category;
        Definition = definition;
        MinimumVersion = version;
        Dependencies = dependencies ?? PdfResourceSet.Empty;
        FontResource = font;
        Materialize = materialize;
    }

    internal PdfValue Definition { get; }
    internal Func<PdfResourceWriter, PdfValue>? Materialize { get; }
    internal PdfResourceSet Dependencies { get; }
    internal PdfFontResource? FontResource { get; }
    /// <summary>Gets the standard resource category.</summary>
    public PdfGraphicsResourceCategory Category { get; }
    /// <summary>Gets its aggregate explicitly validated PDF version floor.</summary>
    public string MinimumVersion { get; }
    /// <summary>Gets the bounded construction report.</summary>
    public PdfOperationReport OperationReport { get; internal set; } = null!;

    /// <summary>Wraps an eagerly embedded exact font for a reusable graphics resource scope.</summary>
    public static PdfGraphicsResource Font(PdfFontResource font)
    {
        if (font == null)
            throw new ArgumentNullException(nameof(font));
        return new(PdfGraphicsResourceCategory.Font, PdfValue.Null, font.MinimumVersion, font: font)
        {
            OperationReport = font.OperationReport
        };
    }

    /// <summary>Captures an advanced direct resource definition with an explicit version floor. Foreign references are rejected; immutable nested streams are supported.</summary>
    public static PdfGraphicsResource Create(PdfGraphicsResourceCategory category, PdfValue definition, string minimumVersion, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (!Enum.IsDefined(typeof(PdfGraphicsResourceCategory), category) || category == PdfGraphicsResourceCategory.Font)
            throw new ArgumentOutOfRangeException(nameof(category));
        PdfVersionNumber.Parse(minimumVersion);
        PdfGraphicsResource? result = null;
        var report = PdfFont.Run("CreateGraphicsResource", "managed.graphics", policy, cancellationToken, context => result = CreateCore(category, definition, minimumVersion, PdfResourceSet.Empty, context), PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    internal static PdfGraphicsResource CreateCore(PdfGraphicsResourceCategory category, PdfValue definition, string version, PdfResourceSet resources, PdfOperationContext context)
    {
        bool valid = category == PdfGraphicsResourceCategory.ColorSpace ? definition.Kind == PdfValueKind.Name || definition.Kind == PdfValueKind.Array : category == PdfGraphicsResourceCategory.XObject ? definition.Kind == PdfValueKind.Stream : definition.Kind == PdfValueKind.Dictionary || definition.Kind == PdfValueKind.Stream;
        if (!valid)
            throw PdfWriterCore.Error(context, "Graphics.ResourceDefinition", "The definition has an invalid lexical kind for its resource category.");
        int floor = Math.Max(PdfVersionNumber.Parse(version), PdfVersionNumber.Parse(resources.MinimumVersion));
        void Inspect(PdfValue value)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 64);
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            if (value.Kind == PdfValueKind.Reference)
                throw PdfWriterCore.Error(context, "Graphics.ForeignReference", "A detached authored resource cannot contain references into another object graph.");
            if (value.Kind == PdfValueKind.Array)
            {
                floor = Math.Max(floor, PdfFeatureVersion.Array(value, v => v));
                context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 8L);
                foreach (var item in value.Items)
                    Inspect(item);
            }

            if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
            {
                floor = Math.Max(floor, PdfFeatureVersion.Dictionary(value, v => v));
                if (value.Entries.TryGetValue("BM", out var blend))
                {
                    var modes = blend.Kind == PdfValueKind.Name ? new[]
                    {
                        blend
                    }

                    : blend.Kind == PdfValueKind.Array ? blend.Items : System.Array.Empty<PdfValue>();
                    if (modes.Count == 0 || modes.Any(x => x.Kind != PdfValueKind.Name))
                        throw PdfWriterCore.Error(context, "Graphics.BlendDefinition", "A blend mode must be a name or nonempty name array.");
                    foreach (var mode in modes)
                        if (!PdfGraphicsState.SupportedBlendModes.Contains(mode.NameValue, StringComparer.Ordinal))
                            context.Report(new PdfDiagnostic("Graphics.UnsupportedBlendMode", "An advanced resource retains a blend mode whose rendering behavior is outside the supported profile.", lossCategory: PdfLossCategory.UnsupportedContent));
                }

                foreach (var item in value.Entries.Values)
                    Inspect(item);
            }
        }

        Inspect(definition);
        return new PdfGraphicsResource(category, definition, PdfVersionNumber.Text(floor), resources);
    }
}

// Original graph embedding: preserve immutable byte stores, hoist every nested stream and share reused resources/fonts.
internal sealed class PdfResourceWriter
{
    private readonly PdfTransaction _edit;
    private readonly PdfOperationContext _context;
    private readonly Dictionary<PdfGraphicsResource, PdfValue> _cache = new();
    private readonly Dictionary<PdfFontResource, PdfObjectId> _fonts;
    internal PdfResourceWriter(PdfTransaction edit, PdfOperationContext context, Dictionary<PdfFontResource, PdfObjectId> fonts)
    {
        _edit = edit;
        _context = context;
        _fonts = fonts;
    }

    internal PdfValue Scope(PdfResourceSet resources)
    {
        _context.Consume(PdfResourceKind.ExpandedBytes, resources.Bindings.Count * 256L + 256);
        var categories = new Dictionary<string, Dictionary<string, PdfValue>>(StringComparer.Ordinal);
        foreach (var binding in resources.Bindings)
        {
            _context.Checkpoint();
            string category = binding.Resource.Category.ToString();
            if (!categories.TryGetValue(category, out var names))
            {
                names = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
                categories.Add(category, names);
            }

            names.Add(binding.Name, Embed(binding.Resource));
        }

        return PdfValue.OwnedDictionary(categories.ToDictionary(x => x.Key, x => PdfValue.OwnedDictionary(x.Value), StringComparer.Ordinal));
    }

    internal PdfValue Embed(PdfGraphicsResource resource)
    {
        _context.Checkpoint();
        if (_cache.TryGetValue(resource, out var cached))
            return cached;
        using var depth = _context.EnterScope(PdfResourceKind.Depth);
        _context.Consume(PdfResourceKind.Objects, 1);
        _context.Consume(PdfResourceKind.ExpandedBytes, 256);
        PdfValue result;
        if (resource.FontResource is PdfFontResource font)
        {
            if (!_fonts.TryGetValue(font, out var id))
            {
                id = font.AddTo(_edit, _context);
                _fonts.Add(font, id);
            }

            result = PdfValue.Reference(id);
        }
        else
        {
            var root = resource.Materialize?.Invoke(this) ?? resource.Definition;
            if (resource.Dependencies.Bindings.Count != 0)
            {
                var entries = PdfWriterCore.CopyDictionary(root.Entries, _context);
                entries["Resources"] = Scope(resource.Dependencies);
                root = root.Kind == PdfValueKind.Stream ? root.WithStreamDictionary(PdfValue.OwnedDictionary(entries)) : PdfValue.OwnedDictionary(entries);
            }

            var promoted = Promote(root, true);
            result = PdfValue.Reference(_edit.Add(promoted));
        }

        _cache.Add(resource, result);
        return result;
    }

    internal PdfValue EmbedValue(PdfValue value) => Promote(value);

    private PdfValue Promote(PdfValue value, bool root = false)
    {
        _context.Checkpoint();
        using var depth = _context.EnterScope(PdfResourceKind.Depth);
        _context.Consume(PdfResourceKind.Objects, 1);
        _context.Consume(PdfResourceKind.ExpandedBytes, 64);
        if (value.Kind == PdfValueKind.Array)
        {
            _context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 16L + 32);
            return PdfValue.OwnedArray(value.Items.Select(x => Promote(x)).ToList());
        }

        if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
        {
            _context.Consume(PdfResourceKind.ExpandedBytes, value.Entries.Count * 128L + 256);
            var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
            foreach (var pair in value.Entries)
                entries.Add(pair.Key, Promote(pair.Value));
            var dictionary = PdfValue.OwnedDictionary(entries);
            if (value.Kind == PdfValueKind.Dictionary)
                return dictionary;
            var stream = value.WithStreamDictionary(dictionary);
            return root ? stream : PdfValue.Reference(_edit.Add(stream));
        }

        return value;
    }
}
