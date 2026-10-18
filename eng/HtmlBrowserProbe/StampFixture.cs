using Fluent.Pdf;
using Fluent.Pdf.Fonts;
using Fluent.Pdf.Html;
using System.Security.Cryptography;
using System.Text.Json;

// Original trusted stamping/reader observations; no runtime, host or final-package qualification claim.
internal static class StampFixture
{
    internal static void Observe(string outputRoot, Func<PdfOperationContext, FixtureConnection> host, string fontPath, string version, string revision)
    {
        using var original = Pdf.Create().AddPage(new PdfPageSize(200, 200));
        PdfCanvas.Create(original.Pages[0].Geometry, c => c.DrawPath(Rectangle(new(40, 40, 120, 120)), new(fill: PdfPaint.Solid(new(PdfColorSpace.Rgb, 0, 0, 1))))).ApplyTo(original.Pages[0]);
        original.SaveFile(Path.Combine(outputRoot, "stamp-base.pdf"));
        PdfStampContent Red(double alpha)
        {
            var geometry = new PdfPageGeometry(new(new(0, 0, 100, 100)));
            var drawing = PdfCanvas.Create(geometry, c => c.DrawPath(Rectangle(new(0, 0, 100, 100)), new(fill: PdfPaint.Solid(new(PdfColorSpace.Rgb, 1, 0, 0)), state: PdfGraphicsState.Create(fillAlpha: alpha, strokeAlpha: alpha))));
            return PdfStampContent.FromDrawing(drawing.Drawing, new(0, 0, 100, 100));
        }

        var red = Red(1);
        var halfRed = Red(0.5);
        using var samples = new MemoryStream(new byte[] { 255, 0, 0, 0, 255, 0 });
        var image = PdfStampContent.FromImage(PdfImages.FromSamples(samples, new(2, 1, 3, 8), PdfColorSpace.Rgb, metadata: new(PdfImageOrientation.Rotate90)));
        using var fontInput = File.OpenRead(fontPath);
        var font = PdfFont.Read(fontInput, new(ManagedFontProvider.Instance));
        var shaped = PdfText.Shape("AB", new(font), new(new PdfSimpleTextShaper()));
        var text = PdfStampContent.FromText(shaped, 20);
        using var pageSource = Pdf.Create().AddPage(new PdfPageGeometry(new(new(-10, 20, 100, 200), new(0, 30, 80, 160)), PdfPageRotation.Clockwise90, 2));
        PdfCanvas.Create(pageSource.Pages[0].Geometry, c => c.DrawPath(Rectangle(new(20, 20, 40, 20)), new(fill: PdfPaint.Solid(new(PdfColorSpace.Rgb, 1, 0, 0))))).ApplyTo(pageSource.Pages[0]);
        pageSource.SaveFile(Path.Combine(outputRoot, "stamp-page-source.pdf"));
        using var pageInput = File.OpenRead(Path.Combine(outputRoot, "stamp-page-source.pdf"));
        using var pageLoaded = Pdf.Open(pageInput);
        var pageBinding = pageLoaded.Pages[0].Source!;
        using var pageEdit = new PdfTransaction(pageBinding.Document);
        var pageEntries = pageBinding.Dictionary.Entries.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
        pageEntries["Group"] = PdfValue.Dictionary(new[] { new KeyValuePair<string, PdfValue>("S", PdfValue.Name("Transparency")), new("I", PdfValue.Boolean(false)), new("CS", PdfValue.Name("DeviceRGB")) });
        pageEdit.Set(pageBinding.ObjectId, PdfValue.Dictionary(pageEntries));
        pageEdit.SaveFile(Path.Combine(outputRoot, "stamp-page-group-source.pdf"));
        using var groupedInput = File.OpenRead(Path.Combine(outputRoot, "stamp-page-group-source.pdf"));
        using var groupedPage = Pdf.Open(groupedInput);
        var page = PdfStampContent.FromPage(groupedPage.CreateSnapshot(), 0);
        using var htmlFontInput = File.OpenRead(fontPath);
        var htmlFont = PdfHtmlResource.Read(new("https://fluent-pdf.invalid/font.ttf"), "font/ttf", htmlFontInput);
        var htmlSource = PdfHtmlSource.FromHtml("<!doctype html><html lang=en><meta charset=utf-8><style>@font-face{font-family:Original;src:url('/font.ttf')}body{margin:0;font:24px Original}</style><p>A B</p>", resources: new[] { htmlFont });
        var htmlProviders = new PdfProviderRegistry(new ChromiumHtmlRenderer(c => host(c), version, revision));
        var htmlOptions = new PdfHtmlOptions(pageSize: new(200, 200), readiness: new(networkQuietPeriod: TimeSpan.Zero));
        var html = PdfStampContent.FromHtml(htmlSource, htmlProviders, htmlOptions, new(limits: new(maximumExpandedBytes: 2147483648)));
        var observations = new List<object>();
        foreach (var item in new (string Mode, PdfStampContent Content, PdfStampOptions Options)[]
        {
            ("foreground", red, new(new(20, 20, 80, 80), opacity: 0.5)),
            ("background", red, new(new(20, 20, 80, 80), PdfStampLayer.Background, opacity: 0.5)),
            ("group-alpha", halfRed, new(new(20, 20, 80, 80), opacity: 0.5)),
            ("repeated", red, new(new(20, 20, 40, 40), columns: 2, rows: 2, horizontalGap: 20, verticalGap: 20)),
            ("rotated", red, new(new(20, 20, 80, 40), rotation: 90, fit: PdfCanvasFit.Stretch)),
            ("clipped", red, new(new(-20, 170, 80, 80))),
            ("image", image, new(new(20, 20, 80, 160))),
            ("text", text, new(new(10, 170, 80, 20), artifact: false)),
            ("page", page, new(new(0, 0, 200, 100))),
            ("html", html, new(new(0, 0, 100, 100), artifact: false))
        }

        )
        {
            using var document = PdfStamps.Apply(original.CreateSnapshot(), item.Mode, item.Content, new[] { 0 }, item.Options);
            string path = Path.Combine(outputRoot, "stamp-" + item.Mode + ".pdf");
            document.SaveFile(path);
            var inspected = PdfStamps.Inspect(document.CreateSnapshot());
            if (inspected.Stamps.Count != 1 || inspected.Stamps[0].Identifier != item.Mode)
                throw new InvalidDataException("Original stamp inventory differs from the exact source fixture.");
            using var removed = PdfStamps.Remove(document.CreateSnapshot(), item.Mode);
            string removedPath = Path.Combine(outputRoot, "stamp-" + item.Mode + "-removed.pdf");
            removed.SaveFile(removedPath);
            observations.Add(new { mode = item.Mode, kind = item.Content.Kind.ToString(), pageCount = document.Pages.Count, outputSha256 = Hash(path), removedSha256 = Hash(removedPath), originalStampCount = inspected.Stamps.Count, removedStampCount = PdfStamps.Inspect(removed.CreateSnapshot()).Stamps.Count });
        }

        using var tagged = PdfHtml.Render(htmlSource, htmlProviders, htmlOptions.WithPrint(new(tagged: true)), new(limits: new(maximumExpandedBytes: 2147483648)));
        using var decoration = PdfStamps.Apply(tagged.CreateSnapshot(), "tagged-decoration", red, new[] { 0 }, new(new(100, 100, 40, 40)));
        decoration.SaveFile(Path.Combine(outputRoot, "stamp-tagged-decoration.pdf"));
        using var updated = PdfStamps.Update(decoration.CreateSnapshot(), "tagged-decoration", red, new(new(150, 150, 30, 30), PdfStampLayer.Background, opacity: 0.5));
        updated.SaveFile(Path.Combine(outputRoot, "stamp-tagged-updated.pdf"));
        File.WriteAllText(Path.Combine(outputRoot, "stamp-observations.json"), JsonSerializer.Serialize(new { purpose = "trusted-original-stamping-source-observations-not-production-qualification", version, revision, fontProvider = ManagedFontProvider.Instance.Descriptor, observations, taggedOutputs = new[] { "stamp-tagged-decoration.pdf", "stamp-tagged-updated.pdf" }, stampBlending = "isolated DeviceRGB group; explicit outer opacity multiplies captured inner alpha", productionQualified = false }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private static string Hash(string path)
    {
        using var input = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(input));
    }

    private static PdfPath Rectangle(PdfRect r) => PdfPath.Create(new[] { PdfPathSegment.Move(r.X, r.Y), PdfPathSegment.Line(r.Right, r.Y), PdfPathSegment.Line(r.Right, r.Top), PdfPathSegment.Line(r.X, r.Top), PdfPathSegment.Close() });
}
