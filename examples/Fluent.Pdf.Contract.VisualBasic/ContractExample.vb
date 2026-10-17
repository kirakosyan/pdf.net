Imports System
Imports System.IO
Imports Fluent.Pdf
Imports PdfFacade = Fluent.Pdf.Pdf

Public Module ContractExample
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
