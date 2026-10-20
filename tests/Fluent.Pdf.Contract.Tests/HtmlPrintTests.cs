using System.Text;
using Fluent.Pdf.Html;
using Xunit;
using Peer = Fluent.Pdf.Contract.Tests.HtmlTests.Peer;

namespace Fluent.Pdf.Contract.Tests;
public sealed class HtmlPrintTests
{
    [Fact]
    public void ImmutablePrintSnapshotPassesEveryExplicitChoiceInNativeUnits()
    {
        var original = new PdfHtmlOptions(pageSize: new(300, 400), readiness: new(networkQuietPeriod: TimeSpan.Zero));
        var print = new PdfHtmlPrintOptions(true, true, 0.75, 36, 18, 9, 27, "<DIV style=\"font-size:10px\"><span class=\"pageNumber\"/>/<span class=\"totalPages\"/></DIV>", "<p>B</p>");
        var options = original.WithPrint(print);
        Assert.Same(print, ContractExamples.CSharp.ContractExample.HtmlPrintOptions(original, print).Print);
        Assert.Same(print, ContractExamples.VisualBasic.ContractExample.HtmlPrintOptions(original, print).Print);
        Assert.Same(PdfHtmlPrintOptions.Default, original.Print);
        Assert.Same(print, options.Print);
        var peer = new Peer();
        using var doc = PdfHtml.Render(PdfHtmlSource.FromHtml("<p>A</p>"), new(new ChromiumHtmlRenderer(_ => peer, Peer.Version, Peer.Revision)), options);
        var p = peer.PrintParameters;
        Assert.Equal(300d / 72, p.GetProperty("paperWidth").GetDouble());
        Assert.Equal(400d / 72, p.GetProperty("paperHeight").GetDouble());
        Assert.Equal(0.5, p.GetProperty("marginTop").GetDouble());
        Assert.Equal(0.25, p.GetProperty("marginBottom").GetDouble());
        Assert.Equal(0.125, p.GetProperty("marginLeft").GetDouble());
        Assert.Equal(0.375, p.GetProperty("marginRight").GetDouble());
        Assert.Equal(0.75, p.GetProperty("scale").GetDouble());
        Assert.True(p.GetProperty("landscape").GetBoolean());
        Assert.True(p.GetProperty("preferCSSPageSize").GetBoolean());
        Assert.True(p.GetProperty("displayHeaderFooter").GetBoolean());
        Assert.False(p.GetProperty("generateTaggedPDF").GetBoolean());
        Assert.Equal("<div style=\"font-size:10px\"><span class=\"pageNumber\"></span>/<span class=\"totalPages\"></span></div>", p.GetProperty("headerTemplate").GetString());
        Assert.True(peer.Disposed);
    }

    [Theory]
    [InlineData("<img src=\"https://private.invalid/secret\"/>")]
    [InlineData("<script>private()</script>")]
    [InlineData("<style>@import 'https://private.invalid'</style>")]
    [InlineData("<div style=\"background:url('https://private.invalid')\">A</div>")]
    [InlineData("<!DOCTYPE x [<!ENTITY x SYSTEM 'file:///private'>]><p>&x;</p>")]
    [InlineData("<div onclick=\"private()\">A</div>")]
    [InlineData("<div CLASS=\"a\" class=\"b\">A</div>")]
    [InlineData("<p><b>private</p>")]
    public void UnsupportedOrActivePrintFragmentsFailBeforeOpeningTheHostAndRedactContent(string html)
    {
        int hosts = 0;
        var renderer = new ChromiumHtmlRenderer(_ =>
        {
            hosts++;
            return new Peer();
        }, Peer.Version, Peer.Revision);
        var failure = Assert.Throws<PdfException>(() => PdfHtml.Render(PdfHtmlSource.FromHtml("<p>A</p>"), new(renderer), new PdfHtmlOptions().WithPrint(new(marginTop: 36, headerHtml: html))));
        Assert.Equal("Html.PrintTemplateProfile", failure.Diagnostic.Code);
        Assert.DoesNotContain("private", failure.ToString());
        Assert.Equal(0, hosts);
    }

    [Fact]
    public void PhysicalBoundsAndTemplateUnicodeAreValidatedAtCapture()
    {
        foreach (double invalid in new[]
        {
            double.NaN,
            double.PositiveInfinity,
            0.09,
            2.01
        }

        )
            Assert.Throws<ArgumentOutOfRangeException>(() => new PdfHtmlPrintOptions(scale: invalid));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfHtmlPrintOptions(marginLeft: -1));
        Assert.Throws<ArgumentException>(() => new PdfHtmlPrintOptions(headerHtml: "<p>A</p>"));
        Assert.Throws<ArgumentException>(() => new PdfHtmlPrintOptions(marginTop: 1, headerHtml: "\ud800"));
        Assert.Throws<ArgumentException>(() => new PdfHtmlOptions(pageSize: new(100, 200)).WithPrint(new(marginLeft: 100)));
        Assert.Throws<ArgumentException>(() => new PdfHtmlOptions(pageSize: new(100, 200)).WithPrint(new(landscape: true, marginTop: 100)));
    }

    [Theory]
    [InlineData("")]
    [InlineData("mcr")]
    [InlineData("numberTree")]
    public void RequestedTagsAreValidatedEvenForAnAlternateRendererAndSurviveOrdinarySaving(string mode)
    {
        var renderer = new BytesRenderer(Fixture(mode));
        using var doc = Render(renderer);
        Assert.Single(doc.Pages);
        Assert.Throws<ObjectDisposedException>(() => renderer.Output!.WriteByte(0));
        using var output = new MemoryStream();
        doc.Save(output);
        output.Position = 0;
        var syntax = PdfSyntax.Read(output);
        var catalog = syntax.Resolve(syntax.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        Assert.True(catalog.Entries.ContainsKey("StructTreeRoot"));
    }

    [Fact]
    public void NativeTagFlagRequiresTheSameValidatedAssociationsAfterImport()
    {
        var peer = new Peer(Fixture());
        using var doc = PdfHtml.Render(PdfHtmlSource.FromHtml("<p>A</p>"), new(new ChromiumHtmlRenderer(_ => peer, Peer.Version, Peer.Revision)), new PdfHtmlOptions(readiness: new(networkQuietPeriod: TimeSpan.Zero)).WithPrint(new(tagged: true)));
        Assert.True(peer.PrintParameters.GetProperty("generateTaggedPDF").GetBoolean());
        Assert.True(peer.Disposed);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("parent")]
    [InlineData("cycle")]
    [InlineData("parentCycle")]
    [InlineData("wrongOwner")]
    [InlineData("missingMcid")]
    [InlineData("duplicateMcid")]
    [InlineData("duplicateOwner")]
    [InlineData("wrongLimits")]
    [InlineData("nextKey")]
    public void CorruptStructureAssociationsRejectWithoutReturningADocument(string mode)
    {
        var renderer = new BytesRenderer(Fixture(mode));
        Assert.Equal("Html.TagStructure", Assert.Throws<PdfException>(() => Render(renderer)).Diagnostic.Code);
        Assert.Throws<ObjectDisposedException>(() => renderer.Output!.WriteByte(0));
    }

    [Theory]
    [InlineData("form")]
    [InlineData("objectReference")]
    public void StructureProfilesOutsidePageMcidsAreExplicitlyRejected(string mode)
    {
        Assert.Equal("Html.TagStructureProfile", Assert.Throws<PdfException>(() => Render(new(Fixture(mode)))).Diagnostic.Code);
    }

    [Fact]
    public void StructuralTraversalUsesTheOriginalBudgetAndCallerCancellation()
    {
        using var input = new MemoryStream(Fixture());
        using var doc = Pdf.Open(input);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfFont.Run("PrintTags", "fixture", new(limits: new(maximumObjects: 5)), default, c => PdfHtmlTagStructure.Validate(doc.CreateSnapshot(), new(), c))).Diagnostic.Code);
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => PdfFont.Run("PrintTags", "fixture", null, cancelled.Token, c => PdfHtmlTagStructure.Validate(doc.CreateSnapshot(), new(), c)));
    }

    private static PdfDocument Render(BytesRenderer renderer) => PdfHtml.Render(PdfHtmlSource.FromHtml("<p>A</p>"), new(renderer), new PdfHtmlOptions().WithPrint(new(tagged: true)));
    private sealed class BytesRenderer : IPdfHtmlRenderer
    {
        private readonly byte[] _bytes;
        internal Stream? Output;
        internal BytesRenderer(byte[] bytes) => _bytes = bytes;
        public PdfProviderDescriptor Descriptor { get; } = new("original.print", "1", PdfCapability.RenderHtml);

        public void Render(PdfHtmlSource source, PdfHtmlOptions options, Stream pdfOutput, PdfOperationContext context)
        {
            Output = pdfOutput;
            pdfOutput.Write(_bytes, 0, _bytes.Length);
        }
    }

    internal static byte[] Fixture(string mode = "")
    {
        using var f = new SyntaxFixtures();
        var offsets = new Dictionary<int, long>();
        offsets[1] = f.Object(1, "<< /Type /Catalog /Pages 2 0 R " + (mode == "missing" ? "" : "/MarkInfo << /Marked true >> /StructTreeRoot 5 0 R") + " >>");
        offsets[2] = f.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        offsets[3] = f.Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 400] /StructParents 0 /Contents 4 0 R /Resources << " + (mode == "form" ? "/XObject << /X 8 0 R >>" : "") + " >> >>");
        string content = "/P <</MCID " + (mode == "missingMcid" ? "1" : "0") + ">> BDC 0 0 10 10 re f EMC\n";
        if (mode == "duplicateMcid")
            content += content;
        if (mode == "form")
            content += "/X Do\n";
        offsets[4] = f.StreamObject(4, "", Encoding.ASCII.GetBytes(content));
        offsets[5] = f.Object(5, "<< /Type /StructTreeRoot /K 6 0 R /ParentTree 7 0 R" + (mode == "nextKey" ? " /ParentTreeNextKey 0" : "") + " >>");
        offsets[6] = f.Object(6, "<< /Type /StructElem /S /P /Pg 3 0 R /P " + (mode == "parent" ? "2" : "5") + " 0 R /K " + (mode == "cycle" ? "6 0 R" : mode == "mcr" ? "<< /Type /MCR /MCID 0 >>" : mode == "objectReference" ? "<< /Type /OBJR /Obj 3 0 R >>" : mode == "duplicateOwner" ? "[0 0]" : "0") + " >>");
        offsets[7] = f.Object(7, mode == "parentCycle" ? "<< /Kids [7 0 R] >>" : mode == "numberTree" ? "<< /Kids [8 0 R] /Limits [0 0] >>" : "<< /Nums [0 [" + (mode == "wrongOwner" ? "5" : "6") + " 0 R]]" + (mode == "wrongLimits" ? " /Limits [1 2]" : "") + " >>");
        if (mode == "numberTree") offsets[8] = f.Object(8, "<< /Nums [0 [6 0 R]] /Limits [0 0] >>");
        if (mode == "form")
            offsets[8] = f.StreamObject(8, "/Type /XObject /Subtype /Form /BBox [0 0 10 10] /Resources << >>", Array.Empty<byte>());
        var rows = new List<(int, int, long, bool)>
        {
            (0, 65535, 0, true)
        };
        rows.AddRange(offsets.Select(p => (p.Key, 0, p.Value, false)));
        f.Classic(rows.ToArray(), "/Root 1 0 R", mode == "form" || mode == "numberTree" ? 9 : 8);
        return f.Bytes.ToArray();
    }
}
