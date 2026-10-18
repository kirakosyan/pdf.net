using System.IO;

namespace Fluent.Pdf;
internal sealed class PdfPageImportEngine : IDisposable
{
    internal readonly PdfTransaction Edit;
    internal readonly PdfObjectId CatalogId, PagesId, StructureId, OutlineId;
    internal readonly PdfOrganizationOptions Options;
    internal readonly PdfOperationContext Context;
    internal readonly List<PdfValue> Pages = new();
    internal readonly List<PdfValue> StructureChildren = new(), OutlineChildren = new(), Fields = new(), LabelNumbers = new(), ParentNumbers = new();
    internal readonly Dictionary<string, PdfValue> Catalog = new(StringComparer.Ordinal), Roles = new(StringComparer.Ordinal), FormResources = new(StringComparer.Ordinal);
    internal readonly HashSet<string> FieldNames = new(StringComparer.Ordinal);
    private readonly List<PdfPageImportSource> _sources = new();
    private int _version = 12;
    internal int NextParentKey, SignatureFlags;
    internal bool NeedAppearances, HasStructure;
    internal readonly Dictionary<string, PdfValue> MarkInfo = new(StringComparer.Ordinal);
    internal readonly List<PdfValue> CalculationOrder = new();
    internal PdfPageImportEngine(PdfOrganizationOptions options, PdfOperationContext context)
    {
        Options = options;
        Context = context;
        context.Consume(PdfResourceKind.Objects, 4);
        context.Consume(PdfResourceKind.ExpandedBytes, 2048);
        Edit = new(null, context.Policy);
        CatalogId = Edit.Add(PdfValue.Null);
        PagesId = Edit.Add(PdfValue.Null);
        StructureId = Edit.Add(PdfValue.Null);
        OutlineId = Edit.Add(PdfValue.Null);
    }

    internal void Add(PdfPageInput input)
    {
        int count = input.Selection?.Indices.Count ?? input.Document.Pages.Count;
        if (count > Options.MaximumPages - Pages.Count)
            throw Error("Organization.PageLimit", "The selected output exceeds its finite page limit.");
        using var normalized = PdfPageOrganization.Normalize(input.Document, Options, Context);
        Context.Consume(PdfResourceKind.ExpandedBytes, normalized.Pages.Count * 128L + 256);
        var selected = input.Selection?.Indices ?? Enumerable.Range(0, normalized.Pages.Count).ToArray();
        if (selected.Count > Options.MaximumPages - Pages.Count)
            throw Error("Organization.PageLimit", "The selected output exceeds its finite page limit.");
        var source = new PdfPageImportSource(this, normalized, selected, _sources.Count);
        _sources.Add(source);
        _version = Math.Max(_version, PdfVersionNumber.Parse(source.Graph.Version));
        source.Import();
    }

    internal PdfDocument Finish()
    {
        if (Pages.Count == 0)
            throw Error("Organization.EmptySelection", "Organization requires at least one selected page.");
        foreach (var source in _sources)
            source.FinishStructureParents();
        Catalog["Type"] = PdfValue.Name("Catalog");
        Catalog["Pages"] = PdfValue.Reference(PagesId);
        if (HasStructure)
        {
            Edit.Set(StructureId, PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("StructTreeRoot")), ("K", PdfValue.OwnedArray(StructureChildren)), ("ParentTree", PdfGraphicsNumbers.Dict(("Nums", PdfValue.OwnedArray(ParentNumbers)))), ("ParentTreeNextKey", PdfValue.Integer(NextParentKey)), ("RoleMap", PdfValue.OwnedDictionary(Roles))));
            Catalog["StructTreeRoot"] = PdfValue.Reference(StructureId);
            MarkInfo["Marked"] = PdfValue.Boolean(true);
            Catalog["MarkInfo"] = PdfValue.OwnedDictionary(MarkInfo);
            _version = Math.Max(_version, 13);
        }

        if (OutlineChildren.Count > 0)
        {
            PdfPageImportSource.LinkSiblings(Edit, OutlineChildren, PdfValue.Reference(OutlineId), Context);
            Edit.Set(OutlineId, PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Outlines")), ("First", OutlineChildren[0]), ("Last", OutlineChildren[OutlineChildren.Count - 1]), ("Count", PdfValue.Integer(OutlineChildren.Sum(x => PdfPageImportSource.OutlineVisible(Edit, x, Context))))));
            Catalog["Outlines"] = PdfValue.Reference(OutlineId);
        }

        if (Fields.Count > 0)
            Catalog["AcroForm"] = PdfGraphicsNumbers.Dict(("Fields", PdfValue.OwnedArray(Fields)), ("DR", PdfValue.OwnedDictionary(FormResources)), ("NeedAppearances", PdfValue.Boolean(NeedAppearances)), ("SigFlags", PdfValue.Integer(SignatureFlags)), ("CO", PdfValue.OwnedArray(CalculationOrder)));
        if (LabelNumbers.Count > 0)
        {
            Catalog["PageLabels"] = PdfGraphicsNumbers.Dict(("Nums", PdfValue.OwnedArray(LabelNumbers)));
            _version = Math.Max(_version, 13);
        }

        Edit.Set(CatalogId, PdfValue.OwnedDictionary(Catalog));
        Edit.Set(PagesId, PdfGraphicsNumbers.Dict(("Type", PdfValue.Name("Pages")), ("Kids", PdfValue.OwnedArray(Pages)), ("Count", PdfValue.Integer(Pages.Count))));
        Edit.Trailer = PdfGraphicsNumbers.Dict(("Root", PdfValue.Reference(CatalogId)));
        using var output = new PdfCodecBufferStream(Context, Context.Available(PdfResourceKind.ExpandedBytes));
        using (var lease = new BorrowedStream(output, false, Context))
            Edit.WriteCore(lease, new(version: PdfVersionNumber.Text(_version), allowSignatureInvalidation: Options.AllowSignatureInvalidation), Context);
        return PdfPageOrganization.Read(output.Finish(), Options.MaximumPages, Context);
    }

    internal PdfException Error(string code, string message) => PdfWriterCore.Error(Context, code, message);
    public void Dispose() => Edit.Dispose();
}
