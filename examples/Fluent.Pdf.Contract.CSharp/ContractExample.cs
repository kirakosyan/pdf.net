using Fluent.Pdf;
using PdfFacade = Fluent.Pdf.Pdf;

namespace ContractExamples.CSharp;

public static class ContractExample
{
    public static PdfOperationReport WriteFontPdf(Stream fontInput,Stream output,string text)
    {
        var providers=new PdfProviderRegistry(ManagedPdfProvider.Instance,Fluent.Pdf.Fonts.ManagedFontProvider.Instance);
        var font=PdfFont.Read(fontInput,providers);var resource=PdfFontResource.CreateText(font,text);using var document=PdfFacade.Create(providers:providers).AddPage();document.Pages[0].SetFont("F1",resource);
        document.Pages[0].Content=PdfContentProgram.Create(new[]{new PdfContentOperation(PdfOperator.BeginText),new PdfContentOperation(PdfOperator.Font,PdfValue.Name("F1"),PdfValue.Integer(24)),new PdfContentOperation(PdfOperator.MoveText,PdfValue.Integer(20),PdfValue.Integer(60)),new PdfContentOperation(PdfOperator.ShowText,resource.EncodedText),new PdfContentOperation(PdfOperator.EndText)});
        return document.Save(output);
    }
    public static PdfOperationReport WriteRectangle(Stream output)
    {
        using var document=PdfFacade.Create().AddPage();
        document.Pages[0].Content=PdfContentProgram.Create(new[]{
            new PdfContentOperation(PdfOperator.SaveState),
            new PdfContentOperation(PdfOperator.Rectangle,PdfValue.Integer(20),PdfValue.Integer(20),PdfValue.Integer(100),PdfValue.Integer(50)),
            new PdfContentOperation(PdfOperator.Fill),
            new PdfContentOperation(PdfOperator.RestoreState)});
        return document.Save(output);
    }
    public static PdfOperationReport WriteManagedPdf(Stream output)
    {
        using var document=PdfFacade.Create().AddPage(PdfPageSize.Letter);
        return document.Save(output);
    }
    public static PdfProviderRegistry ImageCodecs(Fluent.Pdf.Workers.NativeWorkerOptions worker)=>
        new(ManagedPdfProvider.Instance,new Fluent.Pdf.Codecs.IsolatedImageCodecProvider(worker));
    public static PdfSyntaxDocument InspectSyntax(Stream borrowedInput)=>PdfSyntax.Read(borrowedInput);
    public static PdfDecodedData DecodeStream(PdfSyntaxDocument syntax,PdfObjectId id)=>syntax.DecodeStream(id);

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

    public static int Run()
    {
        var options = new PdfOptions(PdfPageSize.Letter, maximumPages: 10);
        using var document = PdfFacade.Create(options,PdfProviderRegistry.Empty)
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
