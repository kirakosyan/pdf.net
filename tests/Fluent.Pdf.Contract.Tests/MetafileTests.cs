using System.IO;
using Fluent.Pdf.Formats;
using Xunit;
namespace Fluent.Pdf.Contract.Tests;
public sealed class MetafileTests
{
    private static MemoryStream Fixture() { using var document=Pdf.Create().AddPage(new PdfPageSize(72,36));var output=new MemoryStream();PdfMetafiles.WriteRasterEmf(document.CreateSnapshot(),0,output,new(72,72),new(new RasterViewTests.PatternRenderer()));output.Position=0;return output; }
    [Fact]
    public void OriginalEmfRecordsRoundTripPhysicalPageAndExactBgrSamplesWithoutGdi()
    {
        using var emf=Fixture();using var document=PdfMetafiles.ImportRasterEmf(emf);Assert.True(emf.CanRead);Assert.Equal(new PdfPageSize(72,36),document.Pages[0].Size);var graphics=PdfPageGraphics.Inspect(document.CreateSnapshot(),0);int index=Enumerable.Range(0,graphics.Occurrences.Count).Single(n=>graphics.Occurrences[n].Kind==PdfGraphicsOccurrenceKind.Image);using var image=graphics.ExtractImage(index).OpenSamples();Assert.Equal(0,image.ReadByte());Assert.Equal(0,image.ReadByte());Assert.Equal(0,image.ReadByte());Assert.Equal(0,image.ReadByte());Assert.Equal(0,image.ReadByte());Assert.Equal(1,image.ReadByte());string directory=Path.Combine(AppContext.BaseDirectory,"format-fixtures");Directory.CreateDirectory(directory);File.WriteAllBytes(Path.Combine(directory,"raster-page.emf"),emf.ToArray());document.SaveFile(Path.Combine(directory,"raster-page.emf.pdf"));
    }
    [Theory]
    [InlineData(108,70)]
    [InlineData(176,0)]
    [InlineData(160,39)]
    [InlineData(52,4)]
    public void UnknownCommentsOperationsAndConflictingExtentsCannotReturnPartialSuccess(int offset,byte value)
    {using var original=Fixture();byte[] bytes=original.ToArray();bytes[offset]=value;using var input=new MemoryStream(bytes);Assert.Equal("Metafile.RecordUnsupported",Assert.Throws<PdfException>(()=>PdfMetafiles.ImportRasterEmf(input)).Diagnostic.Code);}
    [Fact]
    public void QuantizationAndResourceFailuresCannotPublishPartialEmfOrActivateOptionalConverters()
    {
        using var document=Pdf.Create().AddPage(new PdfPageSize(20,10));using var output=new MemoryStream();output.WriteByte(42);var renderer=new RasterViewTests.PatternRenderer();Assert.Equal("Metafile.GeometryQuantized",Assert.Throws<PdfException>(()=>PdfMetafiles.WriteRasterEmf(document.CreateSnapshot(),0,output,new(72,72),new(renderer))).Diagnostic.Code);Assert.Equal(0,renderer.Calls);Assert.Equal(new byte[]{42},output.ToArray());Assert.Equal("Limit.Objects",Assert.Throws<PdfException>(()=>PdfMetafiles.WriteRasterEmf(document.CreateSnapshot(),0,output,new(72,72),new(renderer),new(PdfProcessingMode.Permissive,limits:new(maximumObjects:1)))).Diagnostic.Code);
        using var source=new MemoryStream(new byte[]{1,2,3});Assert.Equal("Metafile.ConverterUnavailable",Assert.Throws<PdfException>(()=>PdfMetafiles.Import(source,PdfMetafileFormat.EmfPlus)).Diagnostic.Code);Assert.Equal(0,source.Position);
    }
}
