using Fluent.Pdf;
using Fluent.Pdf.Html;
using System.Security.Cryptography;
using System.Text.Json;

// Original trusted print observations. Browser/host, fonts and standards qualification remain separate.
internal static class PrintFixture
{
    internal static void Observe(string outputRoot, Func<PdfOperationContext, FixtureConnection> host, string fontPath, string version, string revision)
    {
        using var input = File.OpenRead(fontPath);
        var font = PdfHtmlResource.Read(new("https://fluent-pdf.invalid/font.ttf"), "font/ttf", input);
        const string prefix = "<!doctype html><html lang=en><meta charset=utf-8><style>@font-face{font-family:Original;src:url('/font.ttf')}body{margin:0;font:20px Original}section{break-after:page}section:last-child{break-after:auto}svg{display:block}</style>";
        const string counter = "<div style=\"font-family:monospace;font-size:10px;width:100%;text-align:center\"><span class=\"pageNumber\"></span>/<span class=\"totalPages\"></span></div>";
        var observations = new List<object>();
        foreach (string mode in new[]
        {
            "breaks",
            "css-size",
            "fit",
            "landscape",
            "scale-one",
            "scale-half",
            "table"
        }

        )
        {
            string html = prefix;
            var print = new PdfHtmlPrintOptions();
            if (mode == "breaks")
            {
                html += "<section><h1>A</h1></section><section><p>B</p></section><section><p>A B</p></section>";
                print = new(marginTop: 36, marginBottom: 36, marginLeft: 24, marginRight: 24, headerHtml: counter, footerHtml: counter, tagged: true);
            }
            else if (mode == "table")
            {
                html += "<style>table{border-collapse:collapse;width:100%}th,td{padding:0;height:24px}thead{display:table-header-group}tr{break-inside:avoid}</style><table><thead><tr><th>A</th></tr></thead><tbody>";
                html += string.Concat(Enumerable.Repeat("<tr><td>B</td></tr>", 90)) + "</tbody></table>";
                print = new(marginTop: 36, marginBottom: 36, marginLeft: 24, marginRight: 24, tagged: true);
            }
            else if (mode == "css-size" || mode == "fit")
            {
                html += "<style>@page{size:240pt 360pt;margin:24pt}</style><p>A B</p>";
                print = new(preferCssPageSize: mode == "css-size");
            }
            else
            {
                html += "<p>A B</p><svg width=100 height=40 xmlns='http://www.w3.org/2000/svg'><rect width=100 height=40 fill='red'/></svg>";
                print = new(landscape: mode == "landscape", scale: mode == "scale-half" ? 0.5 : 1);
            }

            var options = new PdfHtmlOptions(pageSize: new(300, 400), readiness: new(networkQuietPeriod: TimeSpan.Zero)).WithPrint(print);
            var registry = new PdfProviderRegistry(new ChromiumHtmlRenderer(c => host(c), version, revision));
            using var document = PdfHtml.Render(PdfHtmlSource.FromHtml(html, resources: new[] { font }), registry, options, new(limits: new(maximumExpandedBytes: 2147483648, maximumElapsedTime: TimeSpan.FromSeconds(30))));
            int expectedPages = mode == "breaks" ? 3 : mode == "table" ? 6 : 1;
            if (document.Pages.Count != expectedPages)
                throw new InvalidDataException("Original print fixture page count differs from its declared expectation.");
            string path = Path.Combine(outputRoot, "print-" + mode + ".pdf");
            document.SaveFile(path);
            var pages = document.Pages.Select((page, index) =>
            {
                string? text = null, extractionDiagnostic = null;
                try
                {
                    text = PdfTextExtraction.ReadPage(document.CreateSnapshot(), index).Text;
                }
                catch (PdfException failure)when (failure.Diagnostic.Code == "Extraction.Type3MatrixProfile")
                {
                    extractionDiagnostic = failure.Diagnostic.Code;
                }

                return new
                {
                    width = page.Size.Width,
                    height = page.Size.Height,
                    text,
                    extractionDiagnostic
                };
            }).ToArray();
            double expectedWidth = mode == "css-size" ? 240 : mode == "landscape" ? 400 : 300;
            double expectedHeight = mode == "css-size" ? 360 : mode == "landscape" ? 300 : 400;
            if (pages.Any(p => Math.Abs(p.width - expectedWidth) > 1 || Math.Abs(p.height - expectedHeight) > 1))
                throw new InvalidDataException("Original physical paper observation exceeds the declared one-point browser rounding tolerance.");
            using var output = File.OpenRead(path);
            observations.Add(new { mode, pageCount = pages.Length, pages, taggedRequestedAndStructurallyValidated = print.Tagged, outputSha256 = Convert.ToHexStringLower(SHA256.HashData(output)) });
        }

        File.WriteAllText(Path.Combine(outputRoot, "print-observations.json"), JsonSerializer.Serialize(new { purpose = "trusted-original-print-source-observations-not-production-qualification", version, revision, paperTolerancePoints = 1, headerFooterFont = "explicit generic monospace; host font identity requires separate qualification", observations, productionQualified = false }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
