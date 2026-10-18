Imports System
Imports System.IO
Imports Fluent.Pdf
Imports PdfFacade = Fluent.Pdf.Pdf

Public Module ContractExample
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
