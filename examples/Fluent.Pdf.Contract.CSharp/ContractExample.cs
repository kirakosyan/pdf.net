using Fluent.Pdf;
using PdfFacade = Fluent.Pdf.Pdf;

namespace ContractExamples.CSharp;

public static class ContractExample
{
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
