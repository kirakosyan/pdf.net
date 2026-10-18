using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class ManagedPdfConcurrencyTests
{
    [Fact]
    public async Task SharedStatelessProviderKeepsIndependentPdfJobsIsolated()
    {
        await Task.WhenAll(Enumerable.Range(0,16).Select(worker=>Task.Run(()=>
        {
            for(int iteration=0;iteration<8;iteration++)
            {
                var expected=new PdfPageSize(100+worker,200+iteration);using var document=Pdf.Create().AddPage(expected);using var output=new MemoryStream();document.Save(output);
                output.Position=0;using var reopened=Pdf.Open(output);Assert.Single(reopened.Pages);Assert.Equal(expected,reopened.Pages[0].Size);
            }
        })));
    }
}
