using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Fluent.Pdf.Formats;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ConversionMatrixTests
{
    [Fact]
    public void EveryAdvertisedExportProfileProducesNonemptyIdentifiableContentWithOneContext()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(72, 36));
        using var matrix = new MemoryStream();
        PdfConversions.WriteMatrix(matrix);
        using var json = JsonDocument.Parse(matrix.ToArray());
        Assert.False(json.RootElement.GetProperty("productionQualified").GetBoolean());
        var targets = json.RootElement.GetProperty("targets").EnumerateArray().Select(t => (PdfConversionTarget)Enum.Parse(typeof(PdfConversionTarget), t.GetProperty("profile").GetString()!)).ToArray();
        Assert.Equal(Enum.GetValues(typeof(PdfConversionTarget)).Cast<PdfConversionTarget>(), targets);
        foreach (var target in targets)
        {
            var renderer = new RasterViewTests.PatternRenderer();
            var providers = new PdfProviderRegistry(renderer);
            var contexts = new List<PdfOperationContext>();
            var phases = new List<PdfConversionPhase>();
            using var output = new MemoryStream();
            var report = PdfConversions.Export(source.CreateSnapshot(), new[] { 0 }, output, new(target, new(72, 72), "en", "Title", new("0123456789ABCDEF0123456789ABCDEF", "Title", "2026-10-20")), providers, (p, c) =>
            {
                phases.Add(p);
                contexts.Add(c);
            });
            Assert.True(report.Succeeded);
            Assert.True(output.Length > 0);
            Assert.Equal(new[] { PdfConversionPhase.Starting, PdfConversionPhase.OutputPrepared }, phases);
            Assert.Same(contexts[0], contexts[1]);
            if (renderer.Calls > 0)
                Assert.Same(contexts[0], renderer.Context);
            Assert.Throws<ObjectDisposedException>(() => contexts[0].Checkpoint());
            output.Position = 0;
            var detected = PdfConversions.Detect(output);
            var expected = target switch
            {
                PdfConversionTarget.Svg => PdfDetectedFormat.Svg,
                PdfConversionTarget.StaticEps => PdfDetectedFormat.Eps,
                PdfConversionTarget.Png => PdfDetectedFormat.Png,
                PdfConversionTarget.Bmp => PdfDetectedFormat.Bmp,
                PdfConversionTarget.Tiff => PdfDetectedFormat.Tiff,
                PdfConversionTarget.RasterXps => PdfDetectedFormat.Xps,
                PdfConversionTarget.RasterOpenXps => PdfDetectedFormat.OpenXps,
                PdfConversionTarget.RasterOfd => PdfDetectedFormat.Ofd,
                PdfConversionTarget.RasterHtml => PdfDetectedFormat.Html,
                _ => PdfDetectedFormat.Unknown
            };
            Assert.Equal(expected, detected.Format);
        }
    }

    [Fact]
    public void ContentOverridesExtensionAndPlainTextNeverGetsInventedSemantics()
    {
        using var input = new MemoryStream(Encoding.ASCII.GetBytes("%PDF-1.7\n"));
        var detection = PdfConversions.Detect(input, ".svg");
        Assert.Equal(PdfDetectedFormat.Pdf, detection.Format);
        Assert.Contains(detection.OperationReport.Diagnostics, d => d.Code == "Conversion.ExtensionMismatch");
        Assert.True(input.CanRead);
        using var text = new MemoryStream(Encoding.UTF8.GetBytes("\\input{not-run}"));
        Assert.Equal(PdfDetectedFormat.Unknown, PdfConversions.Detect(text, "tex").Format);
        using var binary = new MemoryStream(Encoding.ASCII.GetBytes("%!PS-Adobe-3.0 EPSF-3.0\n").Concat(new byte[] { 255, 254 }).ToArray());
        Assert.Equal(PdfDetectedFormat.Eps, PdfConversions.Detect(binary).Format);
    }

    [Fact]
    public void AmbiguousPackageIdentitiesAndPrecommitFailuresCannotPublishOutput()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(72, 36));
        using var original = new MemoryStream();
        PdfConversions.Export(source.CreateSnapshot(), new[] { 0 }, original, new(PdfConversionTarget.RasterOfd, ofdIdentity: new("0123456789ABCDEF0123456789ABCDEF", "Title", "2026-10-20")), new(new RasterViewTests.PatternRenderer()));
        using var mixed = new MemoryStream();
        original.Position = 0;
        using (var read = new ZipArchive(original, ZipArchiveMode.Read, true))
        using (var zip = new ZipArchive(mixed, ZipArchiveMode.Create, true))
        {
            foreach (var entry in read.Entries)
            {
                using var input = entry.Open();
                using var output = zip.CreateEntry(entry.FullName).Open();
                input.CopyTo(output);
            }

            foreach (var pair in new[]
            {
                ("mimetype", "application/epub+zip"),
                ("META-INF/container.xml", "<container xmlns='urn:oasis:names:tc:opendocument:xmlns:container'/>")
            }

            )
            {
                using var output = zip.CreateEntry(pair.Item1).Open();
                byte[] bytes = Encoding.UTF8.GetBytes(pair.Item2);
                output.Write(bytes, 0, bytes.Length);
            }
        }

        mixed.Position = 0;
        Assert.Equal("Conversion.Ambiguous", Assert.Throws<PdfException>(() => PdfConversions.Detect(mixed)).Diagnostic.Code);
        using var destination = new MemoryStream();
        destination.WriteByte(42);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfConversions.Export(source.CreateSnapshot(), new[] { 0 }, destination, new(PdfConversionTarget.Png), new(new RasterViewTests.PatternRenderer()), (phase, c) =>
        {
            if (phase == PdfConversionPhase.OutputPrepared)
                c.RejectLimit(PdfResourceKind.Objects);
        })).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, destination.ToArray());
        Assert.Throws<PdfProviderUnavailableException>(() => PdfConversions.Export(source.CreateSnapshot(), new[] { 0 }, destination, new(PdfConversionTarget.Png)));
        Assert.Equal(new byte[] { 42 }, destination.ToArray());
    }
}
