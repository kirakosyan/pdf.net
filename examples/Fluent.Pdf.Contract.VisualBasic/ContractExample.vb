Imports System
Imports System.IO
Imports Fluent.Pdf
Imports PdfFacade = Fluent.Pdf.Pdf

Public Module ContractExample
    Public Function GenerateData(rows As System.Collections.Generic.IEnumerable(Of String), layout As PdfFlowOptions, style As PdfFlowTextStyle, providers As PdfProviderRegistry) As PdfDocument
        Dim template = New PdfGenerationTemplate(Of String)("sample-data", Sub(context, row) context.Paragraph(row, style))
        Return PdfDataGeneration.Generate(rows, template, New PdfGenerationOptions(layout, ""), providers)
    End Function
    Public Function ExtractTables(source As PdfDocumentSnapshot, mode As PdfTableDetectionMode) As PdfTableExtractionResult
        Return PdfTableExtraction.Read(source, New Integer() {0}, New PdfTableExtractionOptions(mode))
    End Function
    Public Function Search(source As PdfDocumentSnapshot, literal As String) As PdfTextSearchResult
        Return PdfTextSearch.FindPage(source, 0, New PdfTextQuery(literal))
    End Function
    Public Function ExtractResources(source As PdfDocumentSnapshot, page As Integer) As PdfPageResourceManifest
        Return PdfResourceExtraction.ReadPage(source, page)
    End Function
    Public Function Stamp(source As PdfDocumentSnapshot, identifier As String, content As PdfStampContent, pages As Integer(), options As PdfStampOptions) As PdfDocument
        Return PdfStamps.Apply(source, identifier, content, pages, options)
    End Function
    Public Function HtmlRenderer(host As Fluent.Pdf.Html.ChromiumConnectionFactory, exactVersion As String, exactRevision As String) As PdfProviderRegistry
        Return New PdfProviderRegistry(New Fluent.Pdf.Html.ChromiumHtmlRenderer(host, exactVersion, exactRevision))
    End Function
    Public Function HtmlRendererWithResources(host As Fluent.Pdf.Html.ChromiumConnectionFactory, exactVersion As String, exactRevision As String, resources As Fluent.Pdf.Html.HtmlResourceLoader) As PdfProviderRegistry
        Return New PdfProviderRegistry(New Fluent.Pdf.Html.ChromiumHtmlRenderer(host, exactVersion, exactRevision, AddressOf resources.OpenSession))
    End Function
    Public Function HtmlOptions(readiness As PdfHtmlReadinessOptions, enableJavaScript As Boolean, locale As String, timeZone As String, media As PdfHtmlMedia) As PdfHtmlOptions
        Return New PdfHtmlOptions(readiness:=readiness, enableJavaScript:=enableJavaScript, locale:=locale, timeZone:=timeZone, media:=media)
    End Function
    Public Function ConvertHtml(source As PdfHtmlSource, providers As PdfProviderRegistry, options As PdfHtmlOptions) As PdfDocument
        Return PdfHtml.Render(source, providers, options)
    End Function
    Public Function HtmlPrintOptions(options As PdfHtmlOptions, print As PdfHtmlPrintOptions) As PdfHtmlOptions
        Return options.WithPrint(print)
    End Function

    Public Function InspectCompatibility(source As PdfDocumentSnapshot) As PdfCompatibilityInspection
        Return PdfCompatibility.Inspect(source)
    End Function
    Public Function WriteUtf8MetadataPdf(output As Stream) As PdfOperationReport
        Dim title = PdfTextString.Create("Original title 一😀", PdfTextStringEncoding.Utf8)
        Using blank = PdfFacade.Create().AddPage(), document = PdfMetadata.Edit(blank.CreateSnapshot(), Function(e) e.SetInformationText("Title", title))
            Return document.Save(output)
        End Using
    End Function

    Public Function InspectStructure(input As Stream) As PdfStructuralInspection
        Return PdfStructuralValidation.Inspect(input)
    End Function
    Public Function RecoverPdf(input As Stream, output As Stream, ledger As Stream, options As PdfRepairOptions) As PdfRepairResult
        Dim result = PdfRepair.Recover(input, output, options)
        result.WriteLedger(ledger)
        Return result
    End Function

    Public Function ArchivalValidator(host As Fluent.Pdf.Compliance.PdfArchivalValidationRunner) As PdfProviderRegistry
        Return New PdfProviderRegistry(New Fluent.Pdf.Compliance.VeraPdfReportValidator(host))
    End Function
    Public Function ConvertToArchive(source As PdfDocumentSnapshot, output As Stream, options As PdfArchivalOptions, providers As PdfProviderRegistry) As PdfArchivalConversion
        Return PdfArchival.Convert(source, output, options, providers)
    End Function

    Public Function CertificateSecurity() As PdfProviderRegistry
        Return New PdfProviderRegistry(New Fluent.Pdf.Cryptography.ManagedCmsEnvelopeProvider())
    End Function
    Public Function ProtectWithPassword(source As PdfDocumentSnapshot, output As Stream, preparedUser As Byte(), preparedOwner As Byte()) As PdfSecurityResult
        Using user As New PdfPassword(preparedUser), owner As New PdfPassword(preparedOwner)
            Return PdfSecurity.EncryptWithPassword(source, output, New PdfPasswordProtection(user, owner))
        End Using
    End Function
    Public Function DecryptWithPassword(input As Stream, output As Stream, preparedPassword As Byte()) As PdfSecurityResult
        Using password As New PdfPassword(preparedPassword)
            Return PdfSecurity.DecryptWithPassword(input, output, password)
        End Using
    End Function

    Public Function PageRenderer(worker As Fluent.Pdf.Workers.NativeWorkerOptions) As PdfProviderRegistry
        Return New PdfProviderRegistry(New Fluent.Pdf.Rendering.IsolatedPdfRenderer(worker))
    End Function
    Public Function RenderPagePreview(input As Stream, providers As PdfProviderRegistry, options As PdfRenderOptions) As PdfRaster
        Using document = PdfFacade.Open(input)
            Return PdfRendering.RenderPage(document.CreateSnapshot(), 0, providers, options)
        End Using
    End Function
    Public Function FillAndFlattenForm(input As Stream, output As Stream, data As PdfFormData, font As PdfFont, Optional writeMode As PdfWriteMode = PdfWriteMode.Full) As PdfOperationReport
        Using source = PdfFacade.Open(input)
            Dim style As New PdfFormTextStyle(New PdfFontFallback(font), New PdfProviderRegistry(New PdfSimpleTextShaper()))
            Using filled = PdfForms.FillData(source.CreateSnapshot(), data, style, writeMode:=writeMode)
                Using flattened = PdfForms.Flatten(filled.CreateSnapshot(), data.Fields.Select(Function(field) field.Name), writeMode:=writeMode)
                    Return flattened.Save(output, writeOptions:=New PdfWriteOptions(writeMode))
                End Using
            End Using
        End Using
    End Function

    Public Function WriteFormPdf(output As Stream, font As PdfFont) As PdfOperationReport
        Dim text = PdfText.Shape("office", New PdfFontFallback(font), New PdfProviderRegistry(New PdfSimpleTextShaper()))
        Dim appearance = PdfForms.CreateTextAppearance(text, New PdfRect(0, 0, 160, 40), 12)
        Dim editingFont = PdfFontResource.CreateText(font, "office", subset:=False)
        Using blank = PdfFacade.Create().AddPage()
            Dim widget As New PdfFormWidgetDefinition(0, New PdfRect(20, 50, 160, 40), appearance)
            Dim unicodeValue = System.Text.Encoding.BigEndianUnicode.GetPreamble().Concat(System.Text.Encoding.BigEndianUnicode.GetBytes("office")).ToArray()
            Using document = PdfForms.Add(blank.CreateSnapshot(), New PdfFormFieldDefinition("Customer.Name", PdfFormFieldKind.Text, {widget}, PdfValue.String(unicodeValue), editingFont:=editingFont))
                Return document.Save(output)
            End Using
        End Using
    End Function

    Public Function ExtractPageText(input As Stream) As String
        Using document = PdfFacade.Open(input)
            Return PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0,
                providers:=New PdfProviderRegistry(Fluent.Pdf.Fonts.ManagedFontProvider.Instance)).Text
        End Using
    End Function

    Public Function WriteLayerPdf(output As Stream) As PdfOperationReport
        Using blank = PdfFacade.Create().AddPage()
            Dim path = PdfPath.Create({PdfPathSegment.Rectangle(New PdfRect(20, 20, 80, 40))})
            PdfCanvas.Create(blank.Pages(0).Geometry, Sub(canvas) canvas.DrawPath(path)).ApplyTo(blank.Pages(0))
            Dim resource = blank.CreateSnapshot().Pages(0).CanvasLayers(0).ResourceName
            Using grouped = PdfLayers.AddGroup(blank.CreateSnapshot(), "Details", visible:=False)
                Dim group = PdfLayers.Inspect(grouped.CreateSnapshot()).Groups(0)
                Using document = PdfLayers.BindXObject(grouped.CreateSnapshot(), 0, resource, PdfValue.Reference(group.Id))
                    Return document.Save(output)
                End Using
            End Using
        End Using
    End Function

    Public Function WriteAttachmentPdf(payload As Stream, output As Stream) As PdfOperationReport
        Using blank = PdfFacade.Create().AddPage()
            Using document = PdfAttachments.Add(blank.CreateSnapshot(), payload,
                New PdfAttachmentOptions("data.json", "application/json", modificationTime:=New DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero), relationship:="Data"))
                Return document.Save(output)
            End Using
        End Using
    End Function

    Public Function WriteMetadataPdf(output As Stream) As PdfOperationReport
        Using blank = PdfFacade.Create().AddPage()
            Using document = PdfMetadata.Edit(blank.CreateSnapshot(),
                Sub(editor)
                    editor.SetInformation("Title", "Metadata example").SetInformation("Author", "Example writer").
                        SetCreationTime(New DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero)).
                        SetModificationTime(New DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero)).
                        SetIdentity(New Byte() {1, 2, 3, 4}).Synchronize(PdfMetadataSynchronization.InformationToXmp)
                End Sub)
                Return document.Save(output)
            End Using
        End Using
    End Function
    Public Function WritePresentationPdf(output As Stream) As PdfOperationReport
        Using blank = PdfFacade.Create().AddPage().AddPage()
            Dim items = {New PdfBookmark("Report", New PdfDestination(0)).WithChildren(New PdfBookmark("Details", New PdfDestination(1)))}
            Using outlines = PdfPresentation.SetOutlines(blank.CreateSnapshot(), items)
                Using labels = PdfPresentation.SetPageLabels(outlines.CreateSnapshot(), New PdfPageNumbering(New PdfPageNumberRange(0, PdfPageLabelStyle.LowerRoman), New PdfPageNumberRange(1, prefix:="Report-")))
                    Using viewed = PdfPresentation.SetView(labels.CreateSnapshot(), PdfPageLayout.OneColumn, PdfPageMode.UseOutlines)
                        Using document = PdfPresentation.SetPageTransition(viewed.CreateSnapshot(), 1, New PdfPageTransition(PdfTransitionStyle.Fade, .5), 8)
                            Return document.Save(output)
                        End Using
                    End Using
                End Using
            End Using
        End Using
    End Function
    Public Function WriteNavigationPdf(output As Stream) As PdfOperationReport
        Using blank = PdfFacade.Create().AddPage().AddPage()
            Using named = PdfNavigation.SetNamedDestination(blank.CreateSnapshot(), "chapter", New PdfDestination(1))
                Using linked = PdfNavigation.AddLink(named.CreateSnapshot(), 0, New PdfRect(20, 30, 100, 24), PdfAction.GoToNamed("chapter"))
                    Using document = PdfNavigation.SetDocumentAction(linked.CreateSnapshot(), "OpenAction", PdfAction.GoToNamed("chapter"))
                        Return document.Save(output)
                    End Using
                End Using
            End Using
        End Using
    End Function
    Public Function WriteAnnotationPdf(output As Stream) As PdfOperationReport
        Using blank = PdfFacade.Create().AddPage(New PdfPageSize(240, 160))
            Dim note = PdfAnnotation.CreateMarkup(PdfAnnotationKind.Text, "note", New PdfRect(20, 90, 40, 40), New PdfColor(PdfColorSpace.Rgb, 1, .85, .2), metadata:=New PdfAnnotationMetadata("Review this page", "Reviewer"))
            Dim box = PdfAnnotation.CreateMarkup(PdfAnnotationKind.Square, "box", New PdfRect(80, 30, 120, 90), New PdfColor(PdfColorSpace.Rgb, .8, .1, .1))
            Using document = PdfAnnotations.Add(blank.CreateSnapshot(), 0, {note, box})
                Return document.Save(output)
            End Using
        End Using
    End Function
    Public Function WriteImagePdf(imageInput As Stream, output As Stream, Optional allowLosslessTranscode As Boolean = False) As PdfOperationReport
        Dim image = PdfImages.Read(imageInput, New PdfImageReadOptions(allowLosslessTranscode))
        Dim geometry = New PdfPageGeometry(New PdfPageBoxes(New PdfRect(0, 0, 200, 160)))
        Dim canvas = PdfCanvas.Create(geometry, Sub(c) c.DrawImageAsset(image, New PdfRect(10, 10, 180, 140)))
        Using document = PdfFacade.Create().AddPage(geometry)
            canvas.ApplyTo(document.Pages(0))
            Return document.Save(output)
        End Using
    End Function
    Public Function CombinePages(firstInput As Stream, secondInput As Stream, output As Stream) As PdfOperationReport
        Using first = PdfFacade.Open(firstInput), second = PdfFacade.Open(secondInput)
            Using combined = PdfPageOrganization.Merge({New PdfPageInput(first.CreateSnapshot()), New PdfPageInput(second.CreateSnapshot())})
                Return combined.Save(output)
            End Using
        End Using
    End Function
    Public Function WriteTemplatePdf(fontInput As Stream, output As Stream, shaper As IPdfTextShaper) As PdfOperationReport
        Dim font = PdfFont.Read(fontInput, New PdfProviderRegistry(Fluent.Pdf.Fonts.ManagedFontProvider.Instance))
        Dim style = New PdfFlowTextStyle(New PdfFontFallback(font), 10)
        Dim blocks As PdfFlowBlock() = {New PdfFlowContents(style), New PdfFlowHeading("intro", "Introduction", style), New PdfFlowParagraph("A document with final page totals and linked contents.", style)}
        Dim geometry = New PdfPageGeometry(New PdfPageBoxes(New PdfRect(0, 0, 300, 200)))
        Dim template = New PdfPageTemplate(footerHeight:=18, footer:=Function(c) PdfPageDecoration.Flow({New PdfFlowParagraph(c.PageLabel & " of " & c.TotalPages.ToString(System.Globalization.CultureInfo.InvariantCulture), style)}))
        Dim result = PdfTemplateLayout.Layout(blocks, New PdfFlowOptions(geometry, PdfMargins.Uniform(12)), New PdfProviderRegistry(shaper), template)
        Using document = result.CreateDocument()
            Return document.Save(output)
        End Using
    End Function
    Public Function WriteCompositionPdf(fontInput As Stream, output As Stream, shaper As IPdfTextShaper) As PdfOperationReport
        Dim font = PdfFont.Read(fontInput, New PdfProviderRegistry(Fluent.Pdf.Fonts.ManagedFontProvider.Instance))
        Dim style = New PdfFlowTextStyle(New PdfFontFallback(font), 12)
        Dim small = New PdfFlowTextStyle(style.Fonts, 8)
        Dim list = PdfFlowList.Create({New PdfFlowListItem({New PdfFlowParagraph("first item", style)}), New PdfFlowListItem({New PdfFlowParagraph("second item", style)})}, style)
        Dim note = New PdfFlowNote("source", "1", {New PdfFlowParagraph("source note", small)}, small)
        Dim blocks As PdfFlowBlock() = {New PdfFlowColumns(New PdfFlowBlock() {list, New PdfFlowParagraph("office office office office office office", style)}, height:=100), New PdfFlowNoteReference(New PdfFlowParagraph("reference", style), note)}
        Dim geometry = New PdfPageGeometry(New PdfPageBoxes(New PdfRect(0, 0, 300, 200)))
        Dim result = PdfFlow.Layout(blocks, New PdfFlowOptions(geometry, PdfMargins.Uniform(12)), New PdfProviderRegistry(shaper))
        Using document = result.CreateDocument()
            Return document.Save(output)
        End Using
    End Function
    Public Function WriteTablePdf(fontInput As Stream, output As Stream, shaper As IPdfTextShaper) As PdfOperationReport
        Dim font = PdfFont.Read(fontInput, New PdfProviderRegistry(Fluent.Pdf.Fonts.ManagedFontProvider.Instance))
        Dim style = New PdfFlowTextStyle(New PdfFontFallback(font), 12)
        Dim columns = {New PdfTableDataColumn(Of String)(PdfTableColumn.Fixed(108), Function(value) value, style),
                       New PdfTableDataColumn(Of String)(PdfTableColumn.Fixed(108), Function(value) value, style, paragraphStyle:=New PdfFlowParagraphStyle(PdfFlowAlignment.End))}
        Dim header = New PdfTableRow({PdfTableCell.Text("A", style), PdfTableCell.Text("office", style)})
        Dim table = PdfTableData.Bind(System.Linq.Enumerable.Repeat("office", 8), columns, System.Globalization.CultureInfo.InvariantCulture, headerRows:={header})
        Dim geometry = New PdfPageGeometry(New PdfPageBoxes(New PdfRect(0, 0, 240, 140)))
        Dim result = table.Layout(New PdfFlowOptions(geometry, PdfMargins.Uniform(12)), New PdfProviderRegistry(shaper))
        Using document = result.CreateDocument()
            Return document.Save(output)
        End Using
    End Function
    Public Function WriteFlowPdf(fontInput As Stream, output As Stream, text As String, shaper As IPdfTextShaper) As PdfOperationReport
        Dim font = PdfFont.Read(fontInput, New PdfProviderRegistry(Fluent.Pdf.Fonts.ManagedFontProvider.Instance))
        Dim style = New PdfFlowTextStyle(New PdfFontFallback(font), 12)
        Dim geometry = New PdfPageGeometry(New PdfPageBoxes(New PdfRect(0, 0, 200, 160)))
        Dim result = PdfFlow.Layout({New PdfFlowParagraph(text, style)}, New PdfFlowOptions(geometry, PdfMargins.Uniform(12)), New PdfProviderRegistry(shaper))
        Using document = result.CreateDocument()
            Return document.Save(output)
        End Using
    End Function
    Public Function WriteCanvasPdf(output As Stream) As PdfOperationReport
        Dim geometry = New PdfPageGeometry(New PdfPageBoxes(New PdfRect(-20, 10, 240, 160)), PdfPageRotation.Clockwise90)
        Dim path = PdfPath.Create({PdfPathSegment.Rectangle(New PdfRect(0, 0, 30, 15))})
        Dim canvas = PdfCanvas.Create(geometry, Sub(c)
                                                    Using c.Save()
                                                        c.Translate(10, 10)
                                                        c.Rotate(30)
                                                        c.DrawPath(path)
                                                    End Using
                                                End Sub, New PdfCanvasOptions(PdfLengthUnit.Millimeter, margins:=PdfMargins.Uniform(5)))
        Using document = PdfFacade.Create().AddPage(geometry)
            canvas.ApplyTo(document.Pages(0))
            Return document.Save(output)
        End Using
    End Function
    Public Function WriteVectorPdf(output As Stream) As PdfOperationReport
        Dim path = PdfPath.Create({PdfPathSegment.Move(20, 20), PdfPathSegment.Curve(40, 100, 120, 100, 160, 20), PdfPathSegment.Close()})
        Dim operations As New System.Collections.Generic.List(Of PdfContentOperation) From {New PdfContentOperation(PdfOperator.FillRgb, PdfValues.Real(0.2), PdfValues.Real(0.4), PdfValues.Real(0.6))}
        operations.AddRange(path.Operations)
        operations.Add(New PdfContentOperation(PdfOperator.FillStroke))
        Dim drawing = PdfDrawing.Create(operations)
        Dim form = PdfGraphicsObjects.Form(drawing, New PdfRect(0, 0, 200, 120))
        Using document = PdfFacade.Create().AddPage(New PdfPageSize(200, 120))
            document.Pages(0).SetResource("Art", form)
            document.Pages(0).Content = PdfContentProgram.Create({New PdfContentOperation(PdfOperator.SaveState), New PdfContentOperation(PdfOperator.DrawObject, PdfValue.Name("Art")), New PdfContentOperation(PdfOperator.RestoreState)})
            Return document.Save(output)
        End Using
    End Function
    Public Function ShapeText(text As String, fonts As PdfFontFallback, shaper As IPdfTextShaper, Optional policy As PdfOperationPolicy = Nothing) As PdfTextDrawing
        Dim shaped = PdfText.Shape(text, fonts, New PdfProviderRegistry(shaper), policy:=policy)
        Return PdfTextDrawing.Create(shaped, 20, 80, 24)
    End Function
    Public Function WriteFontPdf(fontInput As Stream, output As Stream, text As String) As PdfOperationReport
        Dim providers As New PdfProviderRegistry(ManagedPdfProvider.Instance, Fluent.Pdf.Fonts.ManagedFontProvider.Instance)
        Dim font = PdfFont.Read(fontInput, providers)
        Dim resource = PdfFontResource.CreateText(font, text)
        Using document = PdfFacade.Create(providers:=providers).AddPage()
            document.Pages(0).SetFont("F1", resource)
            document.Pages(0).Content = PdfContentProgram.Create({
                New PdfContentOperation(PdfOperator.BeginText),
                New PdfContentOperation(PdfOperator.Font, PdfValue.Name("F1"), PdfValue.Integer(24)),
                New PdfContentOperation(PdfOperator.MoveText, PdfValue.Integer(20), PdfValue.Integer(60)),
                New PdfContentOperation(PdfOperator.ShowText, resource.EncodedText),
                New PdfContentOperation(PdfOperator.EndText)})
            Return document.Save(output)
        End Using
    End Function
    Public Function WriteRectangle(output As Stream) As PdfOperationReport
        Using document = PdfFacade.Create().AddPage()
            document.Pages(0).Content = PdfContentProgram.Create({
                New PdfContentOperation(PdfOperator.SaveState),
                New PdfContentOperation(PdfOperator.Rectangle, PdfValue.Integer(20), PdfValue.Integer(20), PdfValue.Integer(100), PdfValue.Integer(50)),
                New PdfContentOperation(PdfOperator.Fill),
                New PdfContentOperation(PdfOperator.RestoreState)})
            Return document.Save(output)
        End Using
    End Function
    Public Function WriteManagedPdf(output As Stream) As PdfOperationReport
        Using document = PdfFacade.Create().AddPage(PdfPageSize.Letter)
            Return document.Save(output)
        End Using
    End Function
    Public Function ImageCodecs(worker As Fluent.Pdf.Workers.NativeWorkerOptions) As PdfProviderRegistry
        Return New PdfProviderRegistry(ManagedPdfProvider.Instance, New Fluent.Pdf.Codecs.IsolatedImageCodecProvider(worker))
    End Function
    Public Function InspectSyntax(borrowedInput As Stream) As PdfSyntaxDocument
        Return PdfSyntax.Read(borrowedInput)
    End Function
    Public Function DecodeStream(syntax As PdfSyntaxDocument, id As PdfObjectId) As PdfDecodedData
        Return syntax.DecodeStream(id)
    End Function

    Public Function ExecuteNativeWorker(options As Fluent.Pdf.Workers.NativeWorkerOptions, request As Byte(), context As PdfOperationContext) As Byte()
        Return New Fluent.Pdf.Workers.NativeWorkerHost().Execute(options, request, context)
    End Function

    Public Function InspectDeployment(directory As String, rid As String, engineVersion As String,
                                      abi As String, maximumBytes As Long, ParamArray assets As PdfNativeAsset()) As PdfNativeDeploymentReport
        Dim deployment As New PdfNativeDeployment(directory, rid, "example.native", engineVersion, abi, maximumBytes, assets)
        Return PdfNativeDeploymentInspector.Inspect(deployment)
    End Function

    Public Function SaveWithReport(providers As PdfProviderRegistry, output As Stream) As PdfOperationReport
        Dim limits As New PdfResourceLimits(maximumInputBytes:=1048576, maximumOutputBytes:=1048576)
        Dim policy As New PdfOperationPolicy(PdfProcessingMode.Permissive, limits)
        Using document = PdfFacade.Create(New PdfOptions(operationPolicy:=policy), providers).AddPage()
            Return document.Save(output)
        End Using
    End Function

    Public Function ConvertInput(input As Stream, format As PdfInputFormat, options As PdfInputConversionOptions, providers As PdfProviderRegistry, output As Stream) As PdfOperationReport
        Return PdfInputConversion.Write(input, format, options, providers, output)
    End Function

    Public Function ExportWord(source As PdfDocumentSnapshot, format As PdfWordFormat, options As PdfWordConversionOptions, providers As PdfProviderRegistry, output As Stream) As PdfOperationReport
        Return PdfWordConversion.Write(source, format, options, providers, output, New PdfOperationPolicy(PdfProcessingMode.Permissive))
    End Function

    Public Function Run() As Integer
        Dim options As New PdfOptions(PdfPageSize.Letter, maximumPages:=10)
        Using document As PdfDocument = PdfFacade.Create(options, PdfProviderRegistry.Empty).
            AddPage().
            AddPage(PdfPageSize.A4)
            document.Pages(0).Rotation = PdfPageRotation.Clockwise90
            Dim snapshot = document.CreateSnapshot()
            document.Pages.Move(0, 1)

            Using output As New MemoryStream()
                Try
                    document.Save(output)
                    Throw New InvalidOperationException("A writer was unexpectedly available.")
                Catch failure As PdfProviderUnavailableException
                    If failure.Diagnostic.Code <> "Provider.Unavailable" OrElse
                       failure.Diagnostic.Capability <> PdfCapability.WriteDocument OrElse
                       output.Length <> 0 Then
                        Throw New InvalidOperationException("The provider contract was not honored.")
                    End If
                End Try
            End Using

            If snapshot.Pages(0).Size <> PdfPageSize.Letter OrElse
               document.Pages(1).Rotation <> PdfPageRotation.Clockwise90 Then
                Throw New InvalidOperationException("The page/snapshot contract was not honored.")
            End If
            Return snapshot.Pages.Count
        End Using
    End Function
End Module
