using Fluent.Pdf;
using PdfFacade = Fluent.Pdf.Pdf;

namespace ContractExamples.CSharp;

public static class ContractExample
{
    public static PdfDocument GenerateData(IEnumerable<string> rows, PdfFlowOptions layout, PdfFlowTextStyle style, PdfProviderRegistry providers)
    {
        var template = new PdfGenerationTemplate<string>("sample-data", (context, row) => context.Paragraph(row, style));
        return PdfDataGeneration.Generate(rows, template, new PdfGenerationOptions(layout, ""), providers);
    }
    public static PdfTableExtractionResult ExtractTables(PdfDocumentSnapshot source, PdfTableDetectionMode mode) => PdfTableExtraction.Read(source, new[] { 0 }, new PdfTableExtractionOptions(mode));
    public static PdfTextSearchResult Search(PdfDocumentSnapshot source, string literal) => PdfTextSearch.FindPage(source, 0, new PdfTextQuery(literal));
    public static PdfPageResourceManifest ExtractResources(PdfDocumentSnapshot source, int page) => PdfResourceExtraction.ReadPage(source, page);
    public static PdfDocument Stamp(PdfDocumentSnapshot source, string identifier, PdfStampContent content, int[] pages, PdfStampOptions options) => PdfStamps.Apply(source, identifier, content, pages, options);
    public static PdfProviderRegistry HtmlRenderer(Fluent.Pdf.Html.ChromiumConnectionFactory host, string exactVersion, string exactRevision) => new(new Fluent.Pdf.Html.ChromiumHtmlRenderer(host, exactVersion, exactRevision));
    public static PdfProviderRegistry HtmlRendererWithResources(Fluent.Pdf.Html.ChromiumConnectionFactory host, string exactVersion, string exactRevision, Fluent.Pdf.Html.HtmlResourceLoader resources) => new(new Fluent.Pdf.Html.ChromiumHtmlRenderer(host, exactVersion, exactRevision, resources.OpenSession));
    public static PdfHtmlOptions HtmlOptions(PdfHtmlReadinessOptions readiness, bool enableJavaScript, string locale, string timeZone, PdfHtmlMedia media) => new(readiness: readiness, enableJavaScript: enableJavaScript, locale: locale, timeZone: timeZone, media: media);
    public static PdfHtmlOptions HtmlPrintOptions(PdfHtmlOptions options, PdfHtmlPrintOptions print) => options.WithPrint(print);
    public static PdfDocument ConvertHtml(PdfHtmlSource source, PdfProviderRegistry providers, PdfHtmlOptions options) => PdfHtml.Render(source, providers, options);

    public static PdfCompatibilityInspection InspectCompatibility(PdfDocumentSnapshot source) => PdfCompatibility.Inspect(source);
    public static PdfOperationReport WriteUtf8MetadataPdf(Stream output)
    {
        var title = PdfTextString.Create("Original title 一😀", PdfTextStringEncoding.Utf8);
        using var blank = PdfFacade.Create().AddPage();
        using var document = PdfMetadata.Edit(blank.CreateSnapshot(), e => e.SetInformationText("Title", title));
        return document.Save(output);
    }

    public static PdfStructuralInspection InspectStructure(Stream input) => PdfStructuralValidation.Inspect(input);
    public static PdfRepairResult RecoverPdf(Stream input, Stream output, Stream ledger, PdfRepairOptions options)
    {
        var result = PdfRepair.Recover(input, output, options);
        result.WriteLedger(ledger);
        return result;
    }

    public static PdfProviderRegistry ArchivalValidator(Fluent.Pdf.Compliance.PdfArchivalValidationRunner host) => new(new Fluent.Pdf.Compliance.VeraPdfReportValidator(host));
    public static PdfArchivalConversion ConvertToArchive(PdfDocumentSnapshot source, Stream output, PdfArchivalOptions options, PdfProviderRegistry providers) => PdfArchival.Convert(source, output, options, providers);

    public static PdfProviderRegistry CertificateSecurity() => new(new Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider());
    public static PdfSecurityResult ProtectWithPassword(PdfDocumentSnapshot source, Stream output, byte[] preparedUser, byte[] preparedOwner)
    {
        using var user = new PdfPassword(preparedUser);
        using var owner = new PdfPassword(preparedOwner);
        return PdfSecurity.EncryptWithPassword(source, output, new(user, owner));
    }
    public static PdfSecurityResult DecryptWithPassword(Stream input, Stream output, byte[] preparedPassword)
    {
        using var password = new PdfPassword(preparedPassword);
        return PdfSecurity.DecryptWithPassword(input, output, password);
    }

    public static PdfProviderRegistry PageRenderer(Fluent.Pdf.Workers.NativeWorkerOptions worker) => new(new Fluent.Pdf.Rendering.IsolatedPdfRenderer(worker));
    public static PdfRaster RenderPagePreview(Stream input, PdfProviderRegistry providers, PdfRenderOptions options)
    {
        using var document = PdfFacade.Open(input);
        return PdfRendering.RenderPage(document.CreateSnapshot(), 0, providers, options);
    }
    public static PdfOperationReport FillAndFlattenForm(Stream input, Stream output, PdfFormData data, PdfFont font, PdfWriteMode writeMode = PdfWriteMode.Full)
    {
        using var source = PdfFacade.Open(input);
        var style = new PdfFormTextStyle(new PdfFontFallback(font), new PdfProviderRegistry(new PdfSimpleTextShaper()));
        using var filled = PdfForms.FillData(source.CreateSnapshot(), data, style, writeMode: writeMode);
        using var flattened = PdfForms.Flatten(filled.CreateSnapshot(), data.Fields.Select(f => f.Name), writeMode: writeMode);
        return flattened.Save(output, writeOptions: new PdfWriteOptions(writeMode));
    }

    public static PdfOperationReport WriteFormPdf(Stream output, PdfFont font)
    {
        var text = PdfText.Shape("office", new PdfFontFallback(font), new PdfProviderRegistry(new PdfSimpleTextShaper()));
        var appearance = PdfForms.CreateTextAppearance(text, new PdfRect(0, 0, 160, 40), 12);
        var editingFont = PdfFontResource.CreateText(font, "office", subset: false);
        using var blank = PdfFacade.Create().AddPage();
        var widget = new PdfFormWidgetDefinition(0, new PdfRect(20, 50, 160, 40), appearance);
        byte[] unicodeValue = System.Text.Encoding.BigEndianUnicode.GetPreamble().Concat(System.Text.Encoding.BigEndianUnicode.GetBytes("office")).ToArray();
        using var document = PdfForms.Add(blank.CreateSnapshot(), new PdfFormFieldDefinition("Customer.Name", PdfFormFieldKind.Text, new[] { widget }, PdfValue.String(unicodeValue), editingFont: editingFont));
        return document.Save(output);
    }

    public static string ExtractPageText(Stream input)
    {
        using var document = PdfFacade.Open(input);
        return PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0, providers: new PdfProviderRegistry(Fluent.Pdf.Fonts.ManagedFontProvider.Instance)).Text;
    }

    public static PdfOperationReport WriteLayerPdf(Stream output)
    {
        using var blank = PdfFacade.Create().AddPage();
        var path = PdfPath.Create(new[] { PdfPathSegment.Rectangle(new PdfRect(20, 20, 80, 40)) });
        PdfCanvas.Create(blank.Pages[0].Geometry, canvas => canvas.DrawPath(path)).ApplyTo(blank.Pages[0]);
        string resource = blank.CreateSnapshot().Pages[0].CanvasLayers[0].ResourceName;
        using var grouped = PdfLayers.AddGroup(blank.CreateSnapshot(), "Details", visible: false);
        var group = PdfLayers.Inspect(grouped.CreateSnapshot()).Groups[0];
        using var document = PdfLayers.BindXObject(grouped.CreateSnapshot(), 0, resource, PdfValue.Reference(group.Id));
        return document.Save(output);
    }

    public static PdfOperationReport WriteAttachmentPdf(Stream payload, Stream output)
    {
        using var blank = PdfFacade.Create().AddPage();
        using var document = PdfAttachments.Add(blank.CreateSnapshot(), payload, new PdfAttachmentOptions("data.json", "application/json", modificationTime: new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), relationship: "Data"));
        return document.Save(output);
    }

    public static PdfOperationReport WriteMetadataPdf(Stream output)
    {
        using var blank = PdfFacade.Create().AddPage();
        using var document = PdfMetadata.Edit(blank.CreateSnapshot(), e => e.SetInformation("Title", "Metadata example").SetInformation("Author", "Example writer").SetCreationTime(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero)).SetModificationTime(new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero)).SetIdentity(new byte[] { 1, 2, 3, 4 }).Synchronize(PdfMetadataSynchronization.InformationToXmp));
        return document.Save(output);
    }
    public static PdfOperationReport WritePresentationPdf(Stream output)
    {
        using var blank = PdfFacade.Create().AddPage().AddPage();
        using var outlines = PdfPresentation.SetOutlines(blank.CreateSnapshot(), new[] { new PdfBookmark("Report", new PdfDestination(0), children: new[] { new PdfBookmark("Details", new PdfDestination(1)) }) });
        using var labels = PdfPresentation.SetPageLabels(outlines.CreateSnapshot(), new PdfPageNumbering(new PdfPageNumberRange(0, PdfPageLabelStyle.LowerRoman), new PdfPageNumberRange(1, prefix: "Report-")));
        using var viewed = PdfPresentation.SetView(labels.CreateSnapshot(), PdfPageLayout.OneColumn, PdfPageMode.UseOutlines);
        using var document = PdfPresentation.SetPageTransition(viewed.CreateSnapshot(), 1, new PdfPageTransition(PdfTransitionStyle.Fade, .5), 8);
        return document.Save(output);
    }
    public static PdfOperationReport WriteNavigationPdf(Stream output)
    {
        using var blank = PdfFacade.Create().AddPage().AddPage();
        using var named = PdfNavigation.SetNamedDestination(blank.CreateSnapshot(), "chapter", new PdfDestination(1));
        using var linked = PdfNavigation.AddLink(named.CreateSnapshot(), 0, new PdfRect(20, 30, 100, 24), PdfAction.GoToNamed("chapter"));
        using var document = PdfNavigation.SetDocumentAction(linked.CreateSnapshot(), "OpenAction", PdfAction.GoToNamed("chapter"));
        return document.Save(output);
    }
    public static PdfOperationReport WriteAnnotationPdf(Stream output)
    {
        using var blank = PdfFacade.Create().AddPage(new PdfPageSize(240, 160));
        var note = PdfAnnotation.CreateMarkup(PdfAnnotationKind.Text, "note", new PdfRect(20, 90, 40, 40), new PdfColor(PdfColorSpace.Rgb, 1, .85, .2), metadata: new PdfAnnotationMetadata("Review this page", "Reviewer"));
        var box = PdfAnnotation.CreateMarkup(PdfAnnotationKind.Square, "box", new PdfRect(80, 30, 120, 90), new PdfColor(PdfColorSpace.Rgb, .8, .1, .1));
        using var document = PdfAnnotations.Add(blank.CreateSnapshot(), 0, new[] { note, box });
        return document.Save(output);
    }
    public static PdfOperationReport WriteImagePdf(Stream imageInput, Stream output, bool allowLosslessTranscode = false)
    {
        var image = PdfImages.Read(imageInput, new PdfImageReadOptions(allowLosslessTranscode));
        var geometry = new PdfPageGeometry(new PdfPageBoxes(new PdfRect(0, 0, 200, 160)));
        var canvas = PdfCanvas.Create(geometry, c => c.DrawImageAsset(image, new PdfRect(10, 10, 180, 140)));
        using var document = PdfFacade.Create().AddPage(geometry);
        canvas.ApplyTo(document.Pages[0]);
        return document.Save(output);
    }
    public static PdfOperationReport CombinePages(Stream firstInput, Stream secondInput, Stream output)
    {
        using var first = PdfFacade.Open(firstInput);
        using var second = PdfFacade.Open(secondInput);
        using var combined = PdfPageOrganization.Merge(new[] { new PdfPageInput(first.CreateSnapshot()), new PdfPageInput(second.CreateSnapshot()) });
        return combined.Save(output);
    }
    public static PdfOperationReport WriteTemplatePdf(Stream fontInput, Stream output, IPdfTextShaper shaper)
    {
        var font = PdfFont.Read(fontInput, new PdfProviderRegistry(Fluent.Pdf.Fonts.ManagedFontProvider.Instance));
        var style = new PdfFlowTextStyle(new PdfFontFallback(font), 10);
        var blocks = new PdfFlowBlock[] { new PdfFlowContents(style), new PdfFlowHeading("intro", "Introduction", style), new PdfFlowParagraph("A document with final page totals and linked contents.", style) };
        var geometry = new PdfPageGeometry(new PdfPageBoxes(new PdfRect(0, 0, 300, 200)));
        var template = new PdfPageTemplate(footerHeight: 18, footer: c => PdfPageDecoration.Flow(new[] { new PdfFlowParagraph(c.PageLabel + " of " + c.TotalPages.ToString(System.Globalization.CultureInfo.InvariantCulture), style) }));
        var result = PdfTemplateLayout.Layout(blocks, new PdfFlowOptions(geometry, PdfMargins.Uniform(12)), new PdfProviderRegistry(shaper), template);
        using var document = result.CreateDocument();
        return document.Save(output);
    }
    public static PdfOperationReport WriteCompositionPdf(Stream fontInput, Stream output, IPdfTextShaper shaper)
    {
        var font = PdfFont.Read(fontInput, new PdfProviderRegistry(Fluent.Pdf.Fonts.ManagedFontProvider.Instance));
        var style = new PdfFlowTextStyle(new PdfFontFallback(font), 12);
        var small = new PdfFlowTextStyle(style.Fonts, 8);
        var list = PdfFlowList.Create(new[] {
            new PdfFlowListItem(new[] { new PdfFlowParagraph("first item", style) }),
            new PdfFlowListItem(new[] { new PdfFlowParagraph("second item", style) }) }, style);
        var note = new PdfFlowNote("source", "1", new[] { new PdfFlowParagraph("source note", small) }, small);
        var blocks = new PdfFlowBlock[] {
            new PdfFlowColumns(new PdfFlowBlock[] { list, new PdfFlowParagraph("office office office office office office", style) }, height: 100),
            new PdfFlowNoteReference(new PdfFlowParagraph("reference", style), note) };
        var geometry = new PdfPageGeometry(new PdfPageBoxes(new PdfRect(0, 0, 300, 200)));
        var result = PdfFlow.Layout(blocks, new PdfFlowOptions(geometry, PdfMargins.Uniform(12)), new PdfProviderRegistry(shaper));
        using var document = result.CreateDocument();
        return document.Save(output);
    }
    public static PdfOperationReport WriteTablePdf(Stream fontInput, Stream output, IPdfTextShaper shaper)
    {
        var font = PdfFont.Read(fontInput, new PdfProviderRegistry(Fluent.Pdf.Fonts.ManagedFontProvider.Instance));
        var style = new PdfFlowTextStyle(new PdfFontFallback(font), 12);
        var columns = new[] {
            new PdfTableDataColumn<string>(PdfTableColumn.Fixed(108), value => value, style),
            new PdfTableDataColumn<string>(PdfTableColumn.Fixed(108), value => value, style,
                paragraphStyle: new PdfFlowParagraphStyle(PdfFlowAlignment.End)) };
        var header = new PdfTableRow(new[] { PdfTableCell.Text("A", style), PdfTableCell.Text("office", style) });
        var table = PdfTableData.Bind(Enumerable.Repeat("office", 8), columns,
            System.Globalization.CultureInfo.InvariantCulture, headerRows: new[] { header });
        var geometry = new PdfPageGeometry(new PdfPageBoxes(new PdfRect(0, 0, 240, 140)));
        var result = table.Layout(new PdfFlowOptions(geometry, PdfMargins.Uniform(12)), new PdfProviderRegistry(shaper));
        using var document = result.CreateDocument();
        return document.Save(output);
    }
    public static PdfOperationReport WriteFlowPdf(Stream fontInput, Stream output, string text, IPdfTextShaper shaper)
    {
        var font = PdfFont.Read(fontInput, new PdfProviderRegistry(Fluent.Pdf.Fonts.ManagedFontProvider.Instance));
        var style = new PdfFlowTextStyle(new PdfFontFallback(font), 12);
        var geometry = new PdfPageGeometry(new PdfPageBoxes(new PdfRect(0, 0, 200, 160)));
        var result = PdfFlow.Layout(new[] { new PdfFlowParagraph(text, style) },
            new PdfFlowOptions(geometry, PdfMargins.Uniform(12)), new PdfProviderRegistry(shaper));
        using var document = result.CreateDocument();
        return document.Save(output);
    }
    public static PdfOperationReport WriteCanvasPdf(Stream output)
    {
        var geometry = new PdfPageGeometry(new PdfPageBoxes(new PdfRect(-20, 10, 240, 160)), PdfPageRotation.Clockwise90);
        var path = PdfPath.Create(new[] { PdfPathSegment.Rectangle(new PdfRect(0, 0, 30, 15)) });
        var canvas = PdfCanvas.Create(geometry, c =>
        {
            using (c.Save())
            {
                c.Translate(10, 10);
                c.Rotate(30);
                c.DrawPath(path);
            }
        }, new PdfCanvasOptions(PdfLengthUnit.Millimeter, margins: PdfMargins.Uniform(5)));
        using var document = PdfFacade.Create().AddPage(geometry);
        canvas.ApplyTo(document.Pages[0]);
        return document.Save(output);
    }
    public static PdfOperationReport WriteVectorPdf(Stream output)
    {
        var path = PdfPath.Create(new[] { PdfPathSegment.Move(20, 20), PdfPathSegment.Curve(40, 100, 120, 100, 160, 20), PdfPathSegment.Close() });
        var operations = new List<PdfContentOperation> { new(PdfOperator.FillRgb, PdfValues.Real(.2), PdfValues.Real(.4), PdfValues.Real(.6)) };
        operations.AddRange(path.Operations);
        operations.Add(new(PdfOperator.FillStroke));
        var drawing = PdfDrawing.Create(operations);
        var form = PdfGraphicsObjects.Form(drawing, new(0, 0, 200, 120));
        using var document = PdfFacade.Create().AddPage(new PdfPageSize(200, 120));
        document.Pages[0].SetResource("Art", form);
        document.Pages[0].Content = PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.SaveState), new PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Art")), new PdfContentOperation(PdfOperator.RestoreState) });
        return document.Save(output);
    }
    public static PdfTextDrawing ShapeText(string text, PdfFontFallback fonts, IPdfTextShaper shaper, PdfOperationPolicy? policy = null)
    {
        var shaped = PdfText.Shape(text, fonts, new PdfProviderRegistry(shaper), policy: policy);
        return PdfTextDrawing.Create(shaped, 20, 80, 24);
    }
    public static PdfOperationReport WriteFontPdf(Stream fontInput, Stream output, string text)
    {
        var providers = new PdfProviderRegistry(ManagedPdfProvider.Instance, Fluent.Pdf.Fonts.ManagedFontProvider.Instance);
        var font = PdfFont.Read(fontInput, providers);
        var resource = PdfFontResource.CreateText(font, text);
        using var document = PdfFacade.Create(providers: providers).AddPage();
        document.Pages[0].SetFont("F1", resource);
        document.Pages[0].Content = PdfContentProgram.Create(new[] { new PdfContentOperation(PdfOperator.BeginText), new PdfContentOperation(PdfOperator.Font, PdfValue.Name("F1"), PdfValue.Integer(24)), new PdfContentOperation(PdfOperator.MoveText, PdfValue.Integer(20), PdfValue.Integer(60)), new PdfContentOperation(PdfOperator.ShowText, resource.EncodedText), new PdfContentOperation(PdfOperator.EndText) });
        return document.Save(output);
    }
    public static PdfOperationReport WriteRectangle(Stream output)
    {
        using var document = PdfFacade.Create().AddPage();
        document.Pages[0].Content = PdfContentProgram.Create(new[]{
            new PdfContentOperation(PdfOperator.SaveState),
            new PdfContentOperation(PdfOperator.Rectangle,PdfValue.Integer(20),PdfValue.Integer(20),PdfValue.Integer(100),PdfValue.Integer(50)),
            new PdfContentOperation(PdfOperator.Fill),
            new PdfContentOperation(PdfOperator.RestoreState)});
        return document.Save(output);
    }
    public static PdfOperationReport WriteManagedPdf(Stream output)
    {
        using var document = PdfFacade.Create().AddPage(PdfPageSize.Letter);
        return document.Save(output);
    }
    public static PdfProviderRegistry ImageCodecs(Fluent.Pdf.Workers.NativeWorkerOptions worker) =>
        new(ManagedPdfProvider.Instance, new Fluent.Pdf.Codecs.IsolatedImageCodecProvider(worker));
    public static PdfSyntaxDocument InspectSyntax(Stream borrowedInput) => PdfSyntax.Read(borrowedInput);
    public static PdfDecodedData DecodeStream(PdfSyntaxDocument syntax, PdfObjectId id) => syntax.DecodeStream(id);

    public static byte[] ExecuteNativeWorker(Fluent.Pdf.Workers.NativeWorkerOptions options, byte[] request, PdfOperationContext context) =>
        new Fluent.Pdf.Workers.NativeWorkerHost().Execute(options, request, context);

    public static PdfNativeDeploymentReport InspectDeployment(string directory, string rid, string engineVersion,
        string abi, long maximumBytes, params PdfNativeAsset[] assets)
    {
        var deployment = new PdfNativeDeployment(directory, rid, "example.native", engineVersion, abi, maximumBytes, assets);
        return PdfNativeDeploymentInspector.Inspect(deployment);
    }

    public static PdfOperationReport SaveWithReport(PdfProviderRegistry providers, Stream output)
    {
        var policy = new PdfOperationPolicy(PdfProcessingMode.Permissive,
            new PdfResourceLimits(maximumInputBytes: 1048576, maximumOutputBytes: 1048576));
        using var document = PdfFacade.Create(new PdfOptions(operationPolicy: policy), providers).AddPage();
        return document.Save(output);
    }

    public static PdfOperationReport ConvertInput(Stream input, PdfInputFormat format, PdfInputConversionOptions options, PdfProviderRegistry providers, Stream output)
        => PdfInputConversion.Write(input, format, options, providers, output);

    public static PdfOperationReport ExportWord(PdfDocumentSnapshot source, PdfWordFormat format, PdfWordConversionOptions options, PdfProviderRegistry providers, Stream output)
        => PdfWordConversion.Write(source, format, options, providers, output, new PdfOperationPolicy(PdfProcessingMode.Permissive));

    public static PdfOperationReport MergePdf(PdfMergeTemplate template, PdfMergeRecord data, PdfMailMergeOptions options, PdfProviderRegistry providers, Stream output)
        => PdfMailMerge.Write(template, data, options, providers, output);

    public static int Run()
    {
        var options = new PdfOptions(PdfPageSize.Letter, maximumPages: 10);
        using var document = PdfFacade.Create(options, PdfProviderRegistry.Empty)
            .AddPage()
            .AddPage(PdfPageSize.A4);
        document.Pages[0].Rotation = PdfPageRotation.Clockwise90;
        var snapshot = document.CreateSnapshot();
        document.Pages.Move(0, 1);

        using var output = new MemoryStream();
        try
        {
            document.Save(output);
            throw new InvalidOperationException("A writer was unexpectedly available.");
        }
        catch (PdfProviderUnavailableException failure)
        {
            if (failure.Diagnostic.Code != "Provider.Unavailable" ||
                failure.Diagnostic.Capability != PdfCapability.WriteDocument ||
                output.Length != 0)
                throw new InvalidOperationException("The provider contract was not honored.");
        }

        if (snapshot.Pages[0].Size != PdfPageSize.Letter ||
            document.Pages[1].Rotation != PdfPageRotation.Clockwise90)
            throw new InvalidOperationException("The page/snapshot contract was not honored.");
        return snapshot.Pages.Count;
    }
    public static PdfDocument TagPdf(PdfDocumentSnapshot source, PdfStructurePlan plan) => PdfAccessibility.Author(source, plan);
    public static PdfScanPreprocessingResult PreprocessScan(PdfOcrRaster original, PdfScanPreprocessingOptions options) => PdfScanPreprocessing.Process(original, options);
    public static PdfScanLayoutResult AnalyzeScanLayout(PdfOcrResult recognition, int width, int height, PdfScanLayoutOptions options) => PdfScanLayout.Analyze(recognition, width, height, options);
    public static PdfDocument ApplyOcrLayer(PdfDocumentSnapshot source, PdfOcrResult result, PdfOcrPageFrame frame, PdfFontFallback fonts, PdfProviderRegistry providers) => PdfOcrSearchable.Apply(source, 0, result, frame, fonts, providers);
    public static PdfOperationReport WriteOcrJson(PdfOcrResult result, int width, int height, Stream output) => PdfOcrOutput.Write(result, width, height, PdfOcrOutputFormat.Json, output);
    public static PdfOcrModelSelection SelectOcrModels(PdfOcrModelCatalog catalog, string language) => catalog.Select(language);
    public static PdfOcrAccuracyReport MeasureOcrAccuracy(PdfOcrAccuracySample[] samples, PdfOcrAccuracyOptions options) => PdfOcrAccuracy.Evaluate(samples, "caller-labeled-corpus", "1", options);
    public static PdfOcrResult RecognizeOcr(PdfOcrRaster image, PdfOcrOptions options, PdfProviderRegistry providers) => PdfOcr.Recognize(image, options, providers);
    public static PdfRedactionResult RedactPdf(PdfDocumentSnapshot source, PdfRedactionRegion[] regions, PdfProviderRegistry providers, int dpi) => PdfRedaction.RasterRewrite(source, regions, providers, new(dpi));

    public static PdfSignedResult SignPdf(PdfSyntaxDocument source, string fieldName, PdfProviderRegistry providers) => PdfSignatures.Sign(source, new(fieldName), providers);

    public static IReadOnlyList<PdfSignatureVerification> VerifySignatures(PdfSyntaxDocument source, PdfProviderRegistry providers, DateTimeOffset instant) => PdfSignatures.Verify(source, providers, new(instant));
    public static System.Threading.Tasks.Task<PdfSignedResult> SignExternalAsync(PdfSyntaxDocument source, string fieldName, PdfProviderRegistry providers) => PdfSignatures.SignAsync(source, new(fieldName), providers);
    public static PdfSignedResult UpgradePades(PdfSyntaxDocument source, PdfPadesProfile profile, PdfProviderRegistry providers) => PdfPades.Upgrade(source, profile, providers);
    public static IReadOnlyList<PdfRevisionAuditRecord> AuditSignatures(PdfSyntaxDocument source, PdfProviderRegistry providers) => PdfSignatures.AuditRevisions(source, providers);
    public static PdfComparisonResult CompareDocuments(PdfDocumentSnapshot before, PdfDocumentSnapshot after) => PdfComparison.Compare(before, after);
    public static PdfXfaSet ReadXfa(PdfDocumentSnapshot source) => PdfXfa.Inspect(source);
    public static decimal Calculate(string source, IReadOnlyDictionary<string, decimal> values) => PdfScripts.EvaluateCalculation(source, values, enableExecution: true).Value;
    public static PdfRaster RenderColor(PdfDocumentSnapshot source, PdfProviderRegistry providers, PdfColorRenderOptions colors) => PdfColorRendering.RenderPage(source, 0, providers, colors);
    public static PdfOperationReport ExportRaster(PdfDocumentSnapshot source, Stream output, PdfProviderRegistry providers) => PdfRasterExport.Write(source, new[] { 0 }, output, new(PdfRasterFormat.Png), providers);
    public static PdfViewSession CreateViewer(PdfDocumentSnapshot source, PdfProviderRegistry providers) => new(source, providers, maximumCachedBytes: 4 * 1024 * 1024);
}
