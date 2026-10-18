using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>The declared role of an immutable captured PDF resource. Unknown roles remain observable.</summary>
public enum PdfExtractedResourceKind
{
    /// <summary>An unknown or unsupported resource role.</summary>
    Unknown,
    /// <summary>An image XObject or inline image.</summary>
    Image,
    /// <summary>A Form XObject.</summary>
    Form,
    /// <summary>A font dictionary.</summary>
    Font,
    /// <summary>An embedded font file or explicitly identified Type3 glyph procedure.</summary>
    FontProgram,
    /// <summary>A shading resource.</summary>
    Shading,
    /// <summary>A pattern resource.</summary>
    Pattern,
    /// <summary>A color space declaration.</summary>
    ColorSpace,
    /// <summary>An extended graphics-state dictionary.</summary>
    GraphicsState,
    /// <summary>A marked-content property resource.</summary>
    Properties
}

/// <summary>An exact container/operator location. Page indices refer to concatenated page Contents; Form indices refer to that Form's program.</summary>
public sealed class PdfResourceSource
{
    internal PdfResourceSource(int page, PdfObjectId container, int operation, IReadOnlyList<PdfObjectId> forms, IReadOnlyList<int> calls)
    {
        PageIndex = page;
        ContainerObjectId = container;
        OperationIndex = operation;
        FormPath = forms;
        FormInvocationIndices = calls;
    }

    /// <summary>Gets the zero-based current page.</summary>
    public int PageIndex { get; }
    /// <summary>Gets the captured page or Form object identity.</summary>
    public PdfObjectId ContainerObjectId { get; }
    /// <summary>Gets the zero-based operator index within the declared container program.</summary>
    public int OperationIndex { get; }
    /// <summary>Gets the ordered indirect Form invocation path; direct Forms have no indirect identity to append.</summary>
    public IReadOnlyList<PdfObjectId> FormPath { get; }
    /// <summary>Gets each Form call's operator index in its parent container, distinguishing repeated invocations of the same shared Form.</summary>
    public IReadOnlyList<int> FormInvocationIndices { get; }
}

/// <summary>One resource binding, separate from resource identity and actual use.</summary>
public sealed class PdfResourceDeclaration
{
    internal PdfResourceDeclaration(int resource, string category, string name, PdfObjectId container)
    {
        ResourceIndex = resource;
        Category = category;
        Name = name;
        ContainerObjectId = container;
    }

    /// <summary>Gets its manifest resource index.</summary>
    public int ResourceIndex { get; }
    /// <summary>Gets the original resource dictionary category.</summary>
    public string Category { get; }
    /// <summary>Gets the original byte-valued resource name.</summary>
    public string Name { get; }
    /// <summary>Gets the captured page/Form declaring the effective scope.</summary>
    public PdfObjectId ContainerObjectId { get; }
}

/// <summary>An actual invocation or selection. Visibility, clipping, color and glyph ink are not inferred from this inventory.</summary>
public sealed class PdfResourceOccurrence
{
    internal PdfResourceOccurrence(int resource, string usage, PdfResourceSource source, PdfMatrix transform, bool artifact)
    {
        ResourceIndex = resource;
        Usage = usage;
        Source = source;
        Transform = transform;
        IsArtifact = artifact;
    }

    /// <summary>Gets the shared resource index.</summary>
    public int ResourceIndex { get; }
    /// <summary>Gets the operator role, such as Do, Tf, sh, scn or inline image.</summary>
    public string Usage { get; }
    /// <summary>Gets exact source provenance.</summary>
    public PdfResourceSource Source { get; }
    /// <summary>Gets the accumulated transform into native page user coordinates, including a Form's own matrix.</summary>
    public PdfMatrix Transform { get; }
    /// <summary>Gets whether the use is within an explicit inherited Artifact scope.</summary>
    public bool IsArtifact { get; }
}

/// <summary>One path-construction operator and its construction-time CTM, preserving mixed-transform paths.</summary>
public sealed class PdfExtractedPathElement
{
    internal PdfExtractedPathElement(PdfContentOperation operation, PdfMatrix transform, PdfResourceSource source)
    {
        Operation = operation;
        Transform = transform;
        Source = source;
    }

    /// <summary>Gets the immutable original construction operator/operands.</summary>
    public PdfContentOperation Operation { get; }
    /// <summary>Gets its construction-time transform into native page user coordinates.</summary>
    public PdfMatrix Transform { get; }
    /// <summary>Gets its original source location.</summary>
    public PdfResourceSource Source { get; }
}

/// <summary>A literal source path ending in a paint/end operator; this does not claim rendered ink or effective clipping geometry.</summary>
public sealed class PdfExtractedPath
{
    internal PdfExtractedPath(IReadOnlyList<PdfExtractedPathElement> elements, PdfContentOperation end, PdfResourceSource source, bool clip, bool evenOdd, bool artifact)
    {
        Elements = elements;
        EndOperation = end;
        Source = source;
        SetsClip = clip;
        EvenOddClip = evenOdd;
        IsArtifact = artifact;
    }

    /// <summary>Gets ordered construction operators and their individual transforms.</summary>
    public IReadOnlyList<PdfExtractedPathElement> Elements { get; }
    /// <summary>Gets the terminating paint or n operator, retaining implicit-close/fill rules.</summary>
    public PdfContentOperation EndOperation { get; }
    /// <summary>Gets the terminating operator's source location.</summary>
    public PdfResourceSource Source { get; }
    /// <summary>Gets whether W/W* requests a clip update at this path end.</summary>
    public bool SetsClip { get; }
    /// <summary>Gets whether the requested clip uses even-odd winding.</summary>
    public bool EvenOddClip { get; }
    /// <summary>Gets explicit inherited Artifact classification.</summary>
    public bool IsArtifact { get; }
}

/// <summary>An explicit source Artifact scope, distinct from inferred decorations or authenticated ownership.</summary>
public sealed class PdfExtractedArtifact
{
    internal PdfExtractedArtifact(PdfResourceSource source, int end, PdfValue properties, IReadOnlyList<int> uses, IReadOnlyList<int> paths)
    {
        Source = source;
        EndOperationIndex = end;
        Properties = properties;
        OccurrenceIndices = uses;
        PathIndices = paths;
    }

    /// <summary>Gets its begin location.</summary>
    public PdfResourceSource Source { get; }
    /// <summary>Gets the matching EMC index in the same container.</summary>
    public int EndOperationIndex { get; }
    /// <summary>Gets resolved explicit properties, or null for generic BMC.</summary>
    public PdfValue Properties { get; }
    /// <summary>Gets all resource uses within the scope, including invoked Forms.</summary>
    public IReadOnlyList<int> OccurrenceIndices { get; }
    /// <summary>Gets all literal paths within the scope, including invoked Forms.</summary>
    public IReadOnlyList<int> PathIndices { get; }
}

/// <summary>A captured content program with effective resources for operator cross-checks. References belong to the manifest snapshot.</summary>
public sealed class PdfResourceContent
{
    internal PdfResourceContent(PdfObjectId container, PdfContentProgram program, PdfValue resources)
    {
        ContainerObjectId = container;
        Program = program;
        Resources = resources;
    }

    /// <summary>Gets the page/Form container identity.</summary>
    public PdfObjectId ContainerObjectId { get; }
    /// <summary>Gets the bounded parsed program; unknown operators are preserved only when explicitly requested.</summary>
    public PdfContentProgram Program { get; }
    /// <summary>Gets the effective original resource dictionary.</summary>
    public PdfValue Resources { get; }
}

/// <summary>An immutable resource definition; shared indirect identities are separate from occurrences. Raw bytes are PDF stream bytes, not a reconstructed PNG or original container.</summary>
public sealed class PdfExtractedResource
{
    private readonly PdfDocumentSnapshot _source;
    internal PdfExtractedResource(PdfDocumentSnapshot source, int index, PdfExtractedResourceKind kind, PdfObjectId? id, PdfValue definition, string subtype)
    {
        _source = source;
        Index = index;
        Kind = kind;
        ObjectId = id;
        Definition = definition;
        DeclaredSubtype = subtype;
    }

    /// <summary>Gets its stable index within this manifest.</summary>
    public int Index { get; }
    /// <summary>Gets the declared resource role.</summary>
    public PdfExtractedResourceKind Kind { get; internal set; }
    /// <summary>Gets the defining indirect identity, or null for direct/inline definitions.</summary>
    public PdfObjectId? ObjectId { get; }
    /// <summary>Gets the immutable original definition; references belong to the manifest snapshot.</summary>
    public PdfValue Definition { get; }
    /// <summary>Gets the declared subtype or font-file key. This is a declaration, not program validation.</summary>
    public string DeclaredSubtype { get; }
    /// <summary>Gets associated embedded font-program resource indices. An empty list does not authorize font substitution.</summary>
    public IReadOnlyList<int> FontProgramIndices { get; internal set; } = Array.Empty<int>();
    /// <summary>Gets whether this resource has captured stream bytes.</summary>
    public bool HasStream => Definition.Kind == PdfValueKind.Stream;

    /// <summary>Copies exact encoded PDF stream bytes, or explicitly decoded bytes. Decoding completes privately before caller output; the borrowed output remains open.</summary>
    public PdfOperationReport WriteStream(Stream output, bool decoded = false, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        return PdfFont.Run("ExtractResourceStream", "managed.resources", policy, cancellationToken, c =>
        {
            var bytes = Bytes(decoded, providers ?? PdfProviderRegistry.Empty, c);
            using var lease = new BorrowedStream(output, false, c);
            Copy(bytes, lease, c);
        }, PdfCapability.ReadDocument);
    }

    /// <summary>Atomically replaces a caller-selected file only after exact stream capture/decoding succeeds.</summary>
    public PdfOperationReport WriteFile(string path, bool decoded = false, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        return PdfFont.Run("ExtractResourceFile", "managed.resources", policy, cancellationToken, c =>
        {
            var bytes = Bytes(decoded, providers ?? PdfProviderRegistry.Empty, c);
            PdfAtomicFile.Write(path, c, output =>
            {
                using var lease = new BorrowedStream(output, false, c);
                Copy(bytes, lease, c);
            });
        }, PdfCapability.ReadDocument);
    }

    /// <summary>Inspects an explicitly selected embedded font file through an explicit provider, exposing actual format/embedding restrictions without host font discovery. Glyph procedures are not standalone font programs.</summary>
    public PdfFont ReadFontProgram(PdfProviderRegistry providers, int faceIndex = 0, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        if (faceIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(faceIndex));
        var provider = providers.GetRequired<IPdfFontProvider>(PdfCapability.Fonts);
        PdfFont? font = null;
        var report = PdfFont.Run("InspectExtractedFont", providers.GetIdentifier(PdfCapability.Fonts), policy, cancellationToken, c =>
        {
            if (Kind != PdfExtractedResourceKind.FontProgram || DeclaredSubtype == "Type3Glyph")
                throw PdfWriterCore.Error(c, "Resources.FontProgramProfile", "Select an embedded standalone font file and an explicit compatible font provider.");
            var bytes = Bytes(true, providers, c);
            using var input = bytes.Open(0, bytes.Length);
            using var lease = new BorrowedStream(input, true, c);
            var program = provider.Read(lease, faceIndex, c) ?? throw PdfWriterCore.Error(c, "Font.ProviderResult", "The explicit provider returned no font program.");
            if (program.Info == null)
                throw PdfWriterCore.Error(c, "Font.ProviderResult", "The explicit provider returned no immutable font identity/restrictions.");
            font = new PdfFont(program, c.ProviderId);
        }, PdfCapability.Fonts);
        font!.OperationReport = report;
        return font;
    }

    private PdfByteStore Bytes(bool decoded, PdfProviderRegistry providers, PdfOperationContext c)
    {
        if (!HasStream)
            throw PdfWriterCore.Error(c, "Resources.NoStream", "The selected resource is not a captured PDF stream.");
        c.Consume(PdfResourceKind.InputBytes, Definition.RawStreamLength);
        using var graph = new PdfGraphEdit(_source, new(), c);
        if (decoded)
        {
            var resolved = PdfFilterDeclarations.Resolve(Definition, graph.Edit.Resolve, c);
            using var input = PdfFilters.Decode(resolved, c, providers);
            return PdfByteStore.Read(input, c);
        }

        using var raw = Definition.OpenRawStream();
        return PdfByteStore.Read(raw, c);
    }

    private static void Copy(PdfByteStore bytes, Stream output, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 4096);
        c.Consume(PdfResourceKind.Objects, 2);
        using var input = bytes.Open(0, bytes.Length);
        var buffer = new byte[4096];
        int n;
        while ((n = input.Read(buffer, 0, buffer.Length)) > 0)
        {
            c.Checkpoint();
            output.Write(buffer, 0, n);
        }
    }
}

/// <summary>A passive source inventory. All conditional/layer content is inventoried; actual visibility and rendered fidelity require explicit evaluation/rendering.</summary>
public sealed class PdfPageResourceManifest
{
    internal PdfPageResourceManifest(PdfDocumentSnapshot source, int page, IReadOnlyList<PdfExtractedResource> resources, IReadOnlyList<PdfResourceDeclaration> declarations, IReadOnlyList<PdfResourceOccurrence> occurrences, IReadOnlyList<PdfExtractedPath> paths, IReadOnlyList<PdfExtractedArtifact> artifacts, IReadOnlyList<PdfResourceContent> content, IReadOnlyList<PdfStampInfo> stamps, PdfOperationReport report)
    {
        Source = source;
        PageIndex = page;
        Resources = resources;
        Declarations = declarations;
        Occurrences = occurrences;
        Paths = paths;
        Artifacts = artifacts;
        Content = content;
        Stamps = stamps;
        OperationReport = report;
    }

    /// <summary>Gets the detached captured graph owning all exposed references.</summary>
    public PdfDocumentSnapshot Source { get; }
    /// <summary>Gets the current page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets unique resource definitions, including declared unused resources.</summary>
    public IReadOnlyList<PdfExtractedResource> Resources { get; }
    /// <summary>Gets resource bindings, separate from identities and actual uses.</summary>
    public IReadOnlyList<PdfResourceDeclaration> Declarations { get; }
    /// <summary>Gets actual page/Form resource uses with accumulated transforms.</summary>
    public IReadOnlyList<PdfResourceOccurrence> Occurrences { get; }
    /// <summary>Gets literal path constructions and paint/clip endings.</summary>
    public IReadOnlyList<PdfExtractedPath> Paths { get; }
    /// <summary>Gets explicit Artifact scopes and associated paths/resource uses.</summary>
    public IReadOnlyList<PdfExtractedArtifact> Artifacts { get; }
    /// <summary>Gets captured container programs and effective resource scopes.</summary>
    public IReadOnlyList<PdfResourceContent> Content { get; }
    /// <summary>Gets recognized owned stamps on this page, validated independently of visual heuristics.</summary>
    public IReadOnlyList<PdfStampInfo> Stamps { get; }
    /// <summary>Gets complete bounded inspection diagnostics.</summary>
    public PdfOperationReport OperationReport { get; }
}
