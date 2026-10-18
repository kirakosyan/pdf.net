using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>A detached page font binding for immutable source inspection, binary extraction and exact Unicode decoding.</summary>
public sealed class PdfPageFont
{
    private readonly PdfSyntaxDocument? _source;
    private readonly PdfFontResource? _replacement;
    private readonly PdfProviderRegistry _providers;
    private readonly PdfOperationPolicy _policy;
    internal PdfPageFont(string name, PdfValue? dictionary, PdfSyntaxDocument? source, PdfFontResource? replacement, PdfProviderRegistry providers, PdfOperationPolicy policy, PdfOperationContext context)
    {
        ResourceName = name;
        SourceDictionary = dictionary;
        _source = source;
        _replacement = replacement;
        _providers = providers;
        _policy = policy;
        if (replacement != null)
        {
            BaseFont = replacement.EmittedName;
            Subtype = replacement.ResourceSubtype;
        }
        else
        {
            var type = Resolve(Entry(dictionary!, "Subtype"), context);
            var fontName = Resolve(Entry(dictionary!, "BaseFont"), context);
            if (type.Kind != PdfValueKind.Name)
                throw PdfWriterCore.Error(context, "Font.SourceSubtype", "A source font requires a direct or resolved subtype name.");
            Subtype = type.NameValue;
            BaseFont = fontName.Kind == PdfValueKind.Name ? fontName.NameValue : "";
        }
    }

    /// <summary>Gets the page resource dictionary key.</summary>
    public string ResourceName { get; }
    /// <summary>Gets the PDF font subtype.</summary>
    public string Subtype { get; }
    /// <summary>Gets the declared/generated BaseFont name, or an empty string when a Type 3 source omits it.</summary>
    public string BaseFont { get; }
    /// <summary>Gets the immutable source dictionary, or null for an authored replacement.</summary>
    public PdfValue? SourceDictionary { get; }

    /// <summary>Extracts exact decoded binary font bytes under a fresh budget. Standard and Type 3 fonts have no external program.</summary>
    public PdfDecodedData ExtractProgram(PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfByteStore? bytes = null;
        var report = PdfFont.Run("ExtractFontProgram", "core.fonts", policy ?? _policy, cancellationToken, context => bytes = ReadBytes(ProgramStream(context), context));
        return new PdfDecodedData(bytes!, report);
    }

    /// <summary>Loads the exact embedded font through an explicit font provider, or returns the already loaded authored face.</summary>
    public PdfFont Load(PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfFont? result = null;
        var selected = providers ?? _providers;
        var provider = _replacement == null ? selected.GetRequired<IPdfFontProvider>(PdfCapability.Fonts) : null;
        string id = _replacement?.Font.ProviderId ?? selected.GetIdentifier(PdfCapability.Fonts);
        var report = PdfFont.Run("LoadPageFont", id, policy ?? _policy, cancellationToken, context =>
        {
            if (_replacement != null)
            {
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 128);
                result = new PdfFont(_replacement.Font.Program, id);
                return;
            }

            if (Subtype == "Type3")
                throw PdfWriterCore.Error(context, "Font.Type3Procedures", "A source Type 3 font consists of CharProcs in SourceDictionary and has no external binary face.");
            PdfValue? program = FindProgram(context);
            if (program == null)
            {
                var standard = provider!.Standard(BaseFont, context);
                result = new PdfFont(standard, id);
                return;
            }

            context.Consume(PdfResourceKind.InputBytes, program.RawStreamLength);
            var resolved = PdfFilterDeclarations.Resolve(program, reference => _source!.Resolve(reference), context);
            using var decoded = PdfFilters.Decode(resolved, context, selected);
            using var lease = context.BorrowExpandedInput(decoded);
            var face = provider!.Read(lease, 0, context) ?? throw PdfWriterCore.Error(context, "Font.ProviderResult", "The provider returned no embedded font face.");
            if (face is IPdfCidFontProgram cid && Subtype == "Type0")
            {
                var encoding = Resolve(Entry(SourceDictionary!, "Encoding"), context);
                var unicode = Resolve(Entry(SourceDictionary!, "ToUnicode"), context);
                if (encoding.Kind == PdfValueKind.Name && (encoding.NameValue == "Identity-H" || encoding.NameValue == "Identity-V") && unicode.Kind == PdfValueKind.Stream)
                    face = new PdfMappedFontProgram(face, PdfUnicodeMap.ParseCore(ReadBytes(unicode, context), null, context), cid, context);
            }

            result = new PdfFont(face, id);
        });
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Reads the explicit ToUnicode stream. Named inherited CMaps must be supplied by the caller.</summary>
    public PdfUnicodeMap ReadToUnicode(IReadOnlyDictionary<string, PdfUnicodeMap>? inheritedMaps = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        PdfUnicodeMap? result = null;
        var report = PdfFont.Run("ReadPageToUnicode", "core.fonts", policy ?? _policy, cancellationToken, context =>
        {
            var stream = UnicodeStream(context);
            result = PdfUnicodeMap.ParseCore(ReadBytes(stream, context), inheritedMaps, context);
        });
        result!.OperationReport = report;
        return result;
    }

    /// <summary>Decodes encoded glyph bytes using explicit ToUnicode, otherwise an explicitly configured simple-font encoding provider.</summary>
    public PdfDecodedText DecodeText(PdfValue encodedText, IReadOnlyDictionary<string, PdfUnicodeMap>? inheritedMaps = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (encodedText == null)
            throw new ArgumentNullException(nameof(encodedText));
        if (encodedText.Kind != PdfValueKind.String)
            throw new ArgumentException("An immutable encoded byte string is required.", nameof(encodedText));
        string? text = null;
        var selected = providers ?? _providers;
        var report = PdfFont.Run("DecodePageFontText", "core.fonts", policy ?? _policy, cancellationToken, context =>
        {
            context.Consume(PdfResourceKind.InputBytes, encodedText.StringBytes.Count);
            PdfValue value = _replacement?.ToUnicodeStream ?? Resolve(Entry(SourceDictionary!, "ToUnicode"), context);
            if (value.Kind == PdfValueKind.Stream)
            {
                var map = PdfUnicodeMap.ParseCore(ReadBytes(value, context), inheritedMaps, context);
                text = map.DecodeCore(encodedText.StringBytes, context);
                return;
            }

            if (value.Kind != PdfValueKind.Null)
                throw PdfWriterCore.Error(context, "Font.ToUnicodeStream", "ToUnicode must resolve to a stream.");
            if (Subtype == "Type0")
                throw PdfWriterCore.Error(context, "Font.ToUnicodeRequired", "CID text without ToUnicode requires an explicitly supplied collection-to-Unicode CMap.");
            var provider = selected.GetRequired<IPdfFontProvider>(PdfCapability.Fonts);
            if (provider is not IPdfFontEncodingProvider encoding)
                throw PdfWriterCore.Error(context, "Font.EncodingProviderRequired", "Simple text without ToUnicode requires an explicit encoding provider.");
            var declaration = ResolveDeep(Entry(SourceDictionary!, "Encoding"), context);
            context.Report(new PdfDiagnostic("Font.UnicodeInferred", "The source omits ToUnicode; logical text is inferred from its explicit standard/glyph-name encoding.", severity: PdfDiagnosticSeverity.Information));
            text = encoding.DecodeSimple(declaration, encodedText, BaseFont, context);
        });
        return new PdfDecodedText(text!, report);
    }

    private PdfValue ProgramStream(PdfOperationContext context)
    {
        if (_replacement != null)
        {
            if (_replacement.Font.Info.Format == PdfFontFormat.Standard || _replacement.Font.Info.Format == PdfFontFormat.Type3)
                throw PdfWriterCore.Error(context, "Font.NoEmbeddedProgram", "The selected standard/Type 3 font has no external binary program.");
            return _replacement.EmbeddedProgram;
        }

        return FindProgram(context) ?? throw PdfWriterCore.Error(context, "Font.NoEmbeddedProgram", "The source font contains no external binary program.");
    }

    private PdfValue? FindProgram(PdfOperationContext context)
    {
        var font = SourceDictionary!;
        if (Subtype == "Type0")
        {
            var descendants = Resolve(Entry(font, "DescendantFonts"), context);
            if (descendants.Kind != PdfValueKind.Array || descendants.Items.Count != 1)
                throw PdfWriterCore.Error(context, "Font.DescendantFont", "A Type 0 font requires one descendant font dictionary.");
            font = Resolve(descendants.Items[0], context);
        }

        if (font.Kind != PdfValueKind.Dictionary)
            throw PdfWriterCore.Error(context, "Font.SourceDictionary", "A font or descendant must resolve to a dictionary.");
        var descriptor = Resolve(Entry(font, "FontDescriptor"), context);
        if (descriptor.Kind == PdfValueKind.Null)
            return null;
        if (descriptor.Kind != PdfValueKind.Dictionary)
            throw PdfWriterCore.Error(context, "Font.SourceDescriptor", "FontDescriptor must resolve to a dictionary.");
        PdfValue? program = null;
        foreach (string key in new[]
        {
            "FontFile",
            "FontFile2",
            "FontFile3"
        }

        )
        {
            var value = Resolve(Entry(descriptor, key), context);
            if (value.Kind == PdfValueKind.Null)
                continue;
            if (value.Kind != PdfValueKind.Stream || program != null)
                throw PdfWriterCore.Error(context, "Font.SourceProgram", "A font descriptor must contain at most one applicable font-file stream.");
            program = value;
        }

        return program;
    }

    private PdfValue UnicodeStream(PdfOperationContext context)
    {
        var value = _replacement?.ToUnicodeStream ?? Resolve(Entry(SourceDictionary!, "ToUnicode"), context);
        if (value.Kind != PdfValueKind.Stream)
            throw PdfWriterCore.Error(context, "Font.ToUnicodeRequired", "The selected font has no explicit ToUnicode stream.");
        return value;
    }

    private PdfByteStore ReadBytes(PdfValue stream, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.InputBytes, stream.RawStreamLength);
        var resolved = _source == null ? stream : PdfFilterDeclarations.Resolve(stream, reference => _source.Resolve(reference), context);
        using var input = PdfFilters.Decode(resolved, context, _providers);
        return PdfByteStore.Read(input, context);
    }

    private PdfValue ResolveDeep(PdfValue value, PdfOperationContext context)
    {
        value = Resolve(value, context);
        using var scope = context.EnterScope(PdfResourceKind.Depth);
        context.Consume(PdfResourceKind.Objects, 1);
        if (value.Kind == PdfValueKind.Array)
        {
            context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 16L);
            return PdfValue.OwnedArray(value.Items.Select(item => ResolveDeep(item, context)).ToList());
        }

        if (value.Kind == PdfValueKind.Dictionary)
        {
            var entries = PdfWriterCore.CopyDictionary(value.Entries, context);
            foreach (string key in entries.Keys.ToArray())
                entries[key] = ResolveDeep(entries[key], context);
            return PdfValue.OwnedDictionary(entries);
        }

        return value;
    }

    private PdfValue Resolve(PdfValue value, PdfOperationContext context)
    {
        if (value.Kind != PdfValueKind.Reference)
            return value;
        context.Consume(PdfResourceKind.ExpandedBytes, 64);
        var seen = new HashSet<PdfObjectId>();
        int depth = 0;
        while (value.Kind == PdfValueKind.Reference)
        {
            context.Checkpoint();
            if (++depth > context.Available(PdfResourceKind.Depth))
                context.RejectLimit(PdfResourceKind.Depth);
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 64);
            if (!seen.Add(value.ReferenceId))
                throw PdfWriterCore.Error(context, "Font.ReferenceCycle", "A font resource reference chain is cyclic.");
            value = _source!.Resolve(value.ReferenceId);
        }

        return value;
    }

    private static PdfValue Entry(PdfValue value, string key) => value.Entries.TryGetValue(key, out var result) ? result : PdfValue.Null;
}
