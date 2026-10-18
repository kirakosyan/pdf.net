Imports System
Imports System.IO
Imports Fluent.Pdf
Imports PdfFacade = Fluent.Pdf.Pdf

Public Module ContractExample
    Public Function ImageCodecs(worker As Fluent.Pdf.Workers.NativeWorkerOptions) As PdfProviderRegistry
        Return New PdfProviderRegistry(New Fluent.Pdf.Codecs.IsolatedImageCodecProvider(worker))
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
        Using document As PdfDocument = PdfFacade.Create(options).
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
