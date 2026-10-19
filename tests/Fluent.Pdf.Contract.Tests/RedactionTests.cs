using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class RedactionTests
{
    private const string Secret = "ORIGINAL-REMOVAL-PAYLOAD-49";
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    internal sealed class Renderer : IPdfPageRenderer
    {
        internal int Calls;
        internal bool Fail;
        internal bool BadAlpha;
        internal bool Constant;
        internal bool HideLimit;
        internal PdfOperationContext? Context;
        public PdfProviderDescriptor Descriptor { get; } = new("own.redaction-raster-fixture", "1", PdfCapability.RenderPages);

        public void Render(PdfRasterRequest request, Stream output, PdfOperationContext c)
        {
            Calls++;
            Context = c;
            if (Fail)
                throw new InvalidOperationException("Original explicit renderer failure.");
            if (HideLimit)
            {
                try
                {
                    c.Consume(PdfResourceKind.Objects, 1000001);
                }
                catch (PdfException)
                {
                }
            }

            c.Consume(PdfResourceKind.ExpandedBytes, request.ByteLength);
            var bytes = new byte[(int)request.ByteLength];
            for (int y = 0; y < request.Height; y++)
                for (int x = 0; x < request.Width; x++)
                {
                    int p = y * request.Stride + x * 4;
                    bytes[p] = Constant ? (byte)0 : (byte)x;
                    bytes[p + 1] = Constant ? (byte)0 : (byte)y;
                    bytes[p + 2] = Constant ? (byte)0 : (byte)17;
                    bytes[p + 3] = BadAlpha ? (byte)254 : (byte)255;
                }

            output.Write(bytes, 0, bytes.Length);
        }
    }

    private static PdfDocument Source(int width = 8, int height = 4) => Pdf.Create().AddPage(new PdfPageSize(width, height));
    private static byte[] Read(Stream input)
    {
        using (input)
        {
            using var output = new MemoryStream();
            input.CopyTo(output);
            return output.ToArray();
        }
    }

    private static (PdfSyntaxDocument Graph, PdfValue Image, byte[] Pixels) Decoded(PdfRedactionResult result)
    {
        using var input = result.OpenPdfStream();
        using var document = Pdf.Open(input);
        var graph = document.SourceDocument!;
        var image = graph.Revisions[0].Entries.Select(e => (e.Id, Value: graph.Resolve(e.Id))).Single(v => v.Value.Kind == PdfValueKind.Stream && v.Value.Entries.TryGetValue("Subtype", out var subtype) && subtype.Kind == PdfValueKind.Name && subtype.NameValue == "Image");
        return (graph, image.Value, Read(graph.DecodeStream(image.Id).OpenRead()));
    }

    [Fact]
    public void SelectedPixelsAreReplacedBeforeEncodingAndOverlapsAreCountedOnce()
    {
        using var source = Source();
        var renderer = new Renderer();
        var result = PdfRedaction.RasterRewrite(source.CreateSnapshot(), new[] { new PdfRedactionRegion(0, new(1, 1, 3, 2)), new PdfRedactionRegion(0, new(2, 1, 3, 2)) }, new(renderer), new(72, fillRgb: 0xab23f0, paddingPixels: 0));
        var decoded = Decoded(result);
        Assert.Single(decoded.Graph.Revisions);
        Assert.Equal(96, decoded.Pixels.Length);
        for (int y = 0; y < 4; y++)
            for (int x = 0; x < 8; x++)
            {
                bool removed = x >= 1 && x < 5 && y >= 1 && y < 3;
                int p = (y * 8 + x) * 3;
                Assert.Equal(removed ? (byte)0xab : (byte)17, decoded.Pixels[p]);
                Assert.Equal(removed ? (byte)0x23 : (byte)y, decoded.Pixels[p + 1]);
                Assert.Equal(removed ? (byte)0xf0 : (byte)x, decoded.Pixels[p + 2]);
            }

        Assert.Equal(8, result.Audit.MaskedPixels);
        Assert.Equal(2, result.Audit.Coverage.Count);
        Assert.True(result.Audit.OriginalGraphDiscarded);
        Assert.True(result.Audit.FullRewrite);
        Assert.False(result.Audit.SourceHadSignatures);
        Assert.Equal("RasterRedactAndSanitize", renderer.Context!.Operation);
        Assert.Throws<ObjectDisposedException>(() => renderer.Context.Checkpoint());
        Assert.Null(source.Pages[0].Content);
        Assert.Equal(1, renderer.Calls);
        Assert.DoesNotContain(typeof(PdfOperationReport), typeof(PdfRedactionAudit).GetProperties().Select(p => p.PropertyType));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(127)]
    [InlineData(128)]
    [InlineData(129)]
    [InlineData(257)]
    [InlineData(1024)]
    public void StreamingRunLengthBoundariesDecodeToExactAdoptedSamples(int width)
    {
        using var source = Source(width, 1);
        var result = PdfRedaction.SanitizeRasterRewrite(source.CreateSnapshot(), new(new Renderer { Constant = true }), new(72));
        var decoded = Decoded(result);
        Assert.Equal(width * 3, decoded.Pixels.Length);
        Assert.All(decoded.Pixels, value => Assert.Equal(0, value));
        Assert.Equal("RunLengthDecode", decoded.Image.Entries["Filter"].NameValue);
        Assert.Equal(0, result.Audit.MaskedPixels);
    }

    [Fact]
    public void OutwardSnappingIncludesExplicitPaddingAndNormalizesCropRotationAndUserUnit()
    {
        var geometry = new PdfPageGeometry(new(new(-5, 2, 20, 16), new(1, 4, 8, 4)), PdfPageRotation.Clockwise90, 2);
        using var source = Pdf.Create().AddPage(geometry);
        var result = PdfRedaction.RasterRewrite(source.CreateSnapshot(), new[] { new PdfRedactionRegion(0, new(1.25, 2.25, 1.5, 2.5)) }, new(new Renderer()), new(72));
        Assert.Equal(new PdfRect(0, 1, 4, 5), Assert.Single(result.Audit.Coverage).PixelBounds);
        using var input = result.OpenPdfStream();
        using var rewritten = Pdf.Open(input);
        Assert.Equal(PdfPageRotation.None, rewritten.Pages[0].Rotation);
        Assert.Equal(1, rewritten.Pages[0].Geometry.UserUnit);
        Assert.Equal(new PdfRect(0, 0, 8, 16), rewritten.Pages[0].Geometry.Boxes.Media);
        Assert.Equal(geometry, source.Pages[0].Geometry);
    }

    internal static PdfDocument Sensitive(bool signed = false) => SensitiveFixture((edit, root, pages) =>
    {
        NavigationTests.Put(edit, pages[0], "MediaBox", PdfGraphicsNumbers.Array(0, 0, 8, 4));
        var marker = PdfValue.String(Encoding.ASCII.GetBytes(Secret));
        var hidden = edit.Add(NavigationTests.D(("Type", PdfValue.Name("OCG")), ("Name", marker)));
        NavigationTests.Put(edit, root, "OCProperties", NavigationTests.D(("OCGs", PdfValue.Array(PdfValue.Reference(hidden))), ("D", NavigationTests.D(("OFF", PdfValue.Array(PdfValue.Reference(hidden)))))));
        NavigationTests.Put(edit, root, "OpenAction", NavigationTests.D(("S", PdfValue.Name("JavaScript")), ("JS", marker)));
        NavigationTests.Put(edit, root, "Names", NavigationTests.D(("EmbeddedFiles", NavigationTests.D(("Names", PdfValue.Array(marker, NavigationTests.D(("Type", PdfValue.Name("Filespec")), ("F", marker), ("EF", NavigationTests.D(("F", marker))))))))));
        using var xml = new MemoryStream(Encoding.UTF8.GetBytes(Secret));
        var metadata = edit.Add(PdfValues.Stream(NavigationTests.D(("Type", PdfValue.Name("Metadata")), ("Subtype", PdfValue.Name("XML"))), xml));
        NavigationTests.Put(edit, root, "Metadata", PdfValue.Reference(metadata));
        var trailer = edit.Trailer.Entries.ToDictionary(p => p.Key, p => p.Value);
        trailer["Info"] = PdfValue.Reference(edit.Add(NavigationTests.D(("Title", marker), ("Author", marker))));
        edit.Trailer = PdfValue.Dictionary(trailer);
        _ = edit.Add(marker); // An unreachable active payload must disappear too.
        if (signed)
            _ = edit.Add(NavigationTests.D(("Type", PdfValue.Name("Sig")), ("ByteRange", PdfValue.Array(PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(0))), ("Contents", marker)));
    }, 1);
    private static PdfDocument SensitiveFixture(Action<PdfTransaction, PdfObjectId, PdfObjectId[]> change, int count)
    {
        using var edit = new PdfTransaction();
        var root = edit.Add(PdfValue.Null);
        var tree = edit.Add(PdfValue.Null);
        var page = edit.Add(NavigationTests.D(("Type", PdfValue.Name("Page")), ("Parent", PdfValue.Reference(tree)), ("MediaBox", PdfGraphicsNumbers.Array(0, 0, 8, 4)), ("Resources", NavigationTests.D())));
        edit.Set(tree, NavigationTests.D(("Type", PdfValue.Name("Pages")), ("Kids", PdfValue.Array(PdfValue.Reference(page))), ("Count", PdfValue.Integer(count))));
        edit.Set(root, NavigationTests.D(("Type", PdfValue.Name("Catalog")), ("Pages", PdfValue.Reference(tree))));
        edit.Trailer = NavigationTests.D(("Root", PdfValue.Reference(root)));
        change(edit, root, new[] { page });
        using var bytes = new MemoryStream();
        // This is only a signature-structure policy fixture, never a claimed valid CMS signature.
        edit.Save(bytes, new(allowSignatureInvalidation: true));
        bytes.Position = 0;
        return Pdf.Open(bytes);
    }

    [Fact]
    public void FreshGraphContainsNoOriginalNamesMetadataActionsAttachmentsOrUnreachablePayload()
    {
        using var source = Sensitive();
        var result = PdfRedaction.SanitizeRasterRewrite(source.CreateSnapshot(), new(new Renderer()), new(72));
        var original = Encoding.ASCII.GetString(Read(source.SourceDocument!.OpenOriginalStream()));
        Assert.Contains(Secret, original);
        var bytes = Read(result.OpenPdfStream());
        string rewritten = Encoding.ASCII.GetString(bytes);
        Assert.DoesNotContain(Secret, rewritten);
        Assert.DoesNotContain("/Prev", rewritten);
        Assert.DoesNotContain("/JavaScript", rewritten);
        Assert.DoesNotContain("/EmbeddedFiles", rewritten);
        Assert.DoesNotContain("/OCProperties", rewritten);
        Assert.DoesNotContain("/Metadata", rewritten);
        Assert.DoesNotContain("/Info", rewritten);
        Assert.Single(Decoded(result).Graph.Revisions);
        Assert.Equal(1, result.Audit.SourceRevisionCount);
        Assert.True(result.OperationReport.Succeeded);
        Assert.Equal(result.Audit.OutputSha256, Hash(bytes));
    }

    [Fact]
    public void ExistingSignatureAuthorityIsExplicitAndCheckedBeforeRendering()
    {
        using var source = Sensitive(signed: true);
        var renderer = new Renderer();
        Assert.Equal("Redaction.SignaturePolicy", Error(() => PdfRedaction.SanitizeRasterRewrite(source.CreateSnapshot(), new(renderer), new(72))));
        Assert.Equal(0, renderer.Calls);
        var accepted = PdfRedaction.SanitizeRasterRewrite(source.CreateSnapshot(), new(renderer), new(72, allowSignatureInvalidation: true));
        Assert.True(accepted.Audit.SourceHadSignatures);
        Assert.DoesNotContain("/ByteRange", Encoding.ASCII.GetString(Read(accepted.OpenPdfStream())));
    }

    [Fact]
    public void NonemptyAndNonseekableSinksCannotRetainAnOldPrefixOrTail()
    {
        using var source = Source();
        var renderer = new Renderer();
        var providers = new PdfProviderRegistry(renderer);
        using var old = new MemoryStream(Encoding.ASCII.GetBytes(Secret));
        old.Position = 0;
        Assert.Equal("Redaction.OutputExtent", Error(() => PdfRedaction.WriteRasterRewrite(source.CreateSnapshot(), Array.Empty<PdfRedactionRegion>(), old, providers, new(72))));
        Assert.Equal(0, renderer.Calls);
        Assert.Equal(Secret, Encoding.ASCII.GetString(old.ToArray()));
        Assert.True(old.CanWrite);
        var result = PdfRedaction.SanitizeRasterRewrite(source.CreateSnapshot(), providers, new(72));
        Assert.Equal("Redaction.OutputExtent", Error(() => result.Save(old)));
        using var inner = new MemoryStream();
        using var nonseekable = new Nonseekable(inner);
        Assert.Equal("Stream.SeekRequired", Error(() => result.Save(nonseekable)));
        Assert.Equal(0, inner.Length);
        using var fresh = new MemoryStream();
        var written = PdfRedaction.WriteRasterRewrite(source.CreateSnapshot(), Array.Empty<PdfRedactionRegion>(), fresh, providers, new(72));
        Assert.Equal(written.Audit.OutputSha256, Hash(fresh.ToArray()));
        Assert.True(fresh.CanWrite);
        Assert.Equal("WriteRasterRedactedPdf", written.OperationReport.Operation);
    }

    [Fact]
    public void InvalidRegionsPlannedExtentsAndMaskWorkRejectWithoutPartialOutput()
    {
        using var source = Source();
        var renderer = new Renderer();
        var providers = new PdfProviderRegistry(renderer);
        using var output = new MemoryStream();
        Assert.Equal("Redaction.RegionBounds", Error(() => PdfRedaction.WriteRasterRewrite(source.CreateSnapshot(), new[] { new PdfRedactionRegion(0, new(7, 0, 2, 1)) }, output, providers, new(72))));
        Assert.Equal("Redaction.RasterLimit", Error(() => PdfRedaction.WriteRasterRewrite(source.CreateSnapshot(), Array.Empty<PdfRedactionRegion>(), output, providers, new(72, maximumRasterBytes: 4))));
        Assert.Equal(0, renderer.Calls);
        Assert.Equal(0, output.Length);
        Assert.Equal("Redaction.WorkLimit", Error(() => PdfRedaction.WriteRasterRewrite(source.CreateSnapshot(), Array.Empty<PdfRedactionRegion>(), output, providers, new(72, maximumMaskWork: 1))));
        Assert.Equal(0, output.Length);
        Assert.True(output.CanWrite);
        renderer.BadAlpha = true;
        Assert.Equal("Render.OutputAlpha", Error(() => PdfRedaction.SanitizeRasterRewrite(source.CreateSnapshot(), providers, new(72))));
    }

    [Fact]
    public void RendererFailuresLatchedLimitsAndCancellationLeaveAtomicDestinationsUnchanged()
    {
        using var source = Source();
        var renderer = new Renderer
        {
            Fail = true
        };
        string directory = Path.Combine(Path.GetTempPath(), "fluent-pdf-redaction-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "result.pdf");
        File.WriteAllText(path, Secret);
        try
        {
            Assert.Throws<InvalidOperationException>(() => PdfRedaction.WriteRasterRewriteFile(source.CreateSnapshot(), Array.Empty<PdfRedactionRegion>(), path, new(renderer), new(72)));
            Assert.Equal(Secret, File.ReadAllText(path));
            Assert.Single(Directory.GetFiles(directory));
            renderer.Fail = false;
            renderer.HideLimit = true;
            Assert.Equal("Limit.Objects", Error(() => PdfRedaction.WriteRasterRewriteFile(source.CreateSnapshot(), Array.Empty<PdfRedactionRegion>(), path, new(renderer), new(72))));
            Assert.Equal(Secret, File.ReadAllText(path));
            renderer.HideLimit = false;
            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() => PdfRedaction.WriteRasterRewriteFile(source.CreateSnapshot(), Array.Empty<PdfRedactionRegion>(), path, new(renderer), new(72), cancellationToken: cancel.Token));
            Assert.Equal(Secret, File.ReadAllText(path));
            var result = PdfRedaction.WriteRasterRewriteFile(source.CreateSnapshot(), Array.Empty<PdfRedactionRegion>(), path, new(renderer), new(72));
            Assert.Equal(result.Audit.OutputSha256, Hash(File.ReadAllBytes(path)));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    private static string Hash(byte[] bytes)
    {
        using var sha = System.Security.Cryptography.SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    private sealed class Nonseekable(Stream inner) : Stream
    {
        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }

        public override void Flush() => inner.Flush();
        public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => inner.Write(buffer, offset, count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void LanguageConsumersReplaceSelectedSamplesAndDiscardTheOriginalGraph(bool visualBasic)
    {
        using var source = Sensitive();
        var regions = new[]
        {
            new PdfRedactionRegion(0, new(1, 1, 2, 1))
        };
        var providers = new PdfProviderRegistry(new Renderer());
        var result = visualBasic ? ContractExamples.VisualBasic.ContractExample.RedactPdf(source.CreateSnapshot(), regions, providers, 72) : ContractExamples.CSharp.ContractExample.RedactPdf(source.CreateSnapshot(), regions, providers, 72);
        Assert.Equal(12, result.Audit.MaskedPixels);
        Assert.True(result.Audit.OriginalGraphDiscarded);
        Assert.DoesNotContain(Secret, Encoding.ASCII.GetString(Read(result.OpenPdfStream())));
    }
}
