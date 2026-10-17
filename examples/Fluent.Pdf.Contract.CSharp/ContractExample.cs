using Fluent.Pdf;
using PdfFacade = Fluent.Pdf.Pdf;

namespace ContractExamples.CSharp;

public static class ContractExample
{
    public static int Run()
    {
        var options = new PdfOptions(PdfPageSize.Letter, maximumPages: 10);
        using var document = PdfFacade.Create(options)
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
}
