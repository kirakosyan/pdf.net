using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class FormDataTests
{
    private static PdfValue T(string text) => NavigationTests.T(text);
    private static string Text(PdfValue value) => value.Kind == PdfValueKind.Name ? value.NameValue : value.Kind == PdfValueKind.Null ? "<null>" : Encoding.BigEndianUnicode.GetString(value.StringBytes.Skip(2).ToArray());
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    private static PdfFormData Read(string text, PdfFormDataFormat format)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
        return PdfFormData.Read(input, format);
    }

    [Theory]
    [InlineData(PdfFormDataFormat.Fdf)]
    [InlineData(PdfFormDataFormat.Xfdf)]
    [InlineData(PdfFormDataFormat.Xml)]
    [InlineData(PdfFormDataFormat.Json)]
    public void FormatsRetainUnicodeExactNamesButtonExportsAndMultipleValues(PdfFormDataFormat format)
    {
        var data = PdfFormData.Create(new[] { new PdfFormDataField("Customer.Անուն", T("Καλημέρα 😀\nline")), new PdfFormDataField("Agree", PdfValue.Name("Yes Exact")), new PdfFormDataField("Choices", PdfValue.Array(T("export-b"), T("export-a"))), new PdfFormDataField("Empty", T("")), new PdfFormDataField("Absent", PdfValue.Null) });
        using var output = new MemoryStream();
        data.Write(output, format);
        Assert.True(output.CanWrite);
        output.Position = 0;
        var read = PdfFormData.Read(output, format);
        Assert.True(output.CanRead);
        Assert.Equal(5, read.Fields.Count);
        var map = read.Fields.ToDictionary(x => x.Name, x => x.Value, StringComparer.Ordinal);
        Assert.Equal("Καλημέρα 😀\nline", Text(map["Customer.Անուն"]));
        Assert.Equal("Yes Exact", Text(map["Agree"]));
        Assert.Equal(new[] { "export-b", "export-a" }, map["Choices"].Items.Select(Text));
        Assert.Equal("", Text(map["Empty"]));
        Assert.Equal(PdfValueKind.Null, map["Absent"].Kind);
        if (format != PdfFormDataFormat.Xfdf)
            Assert.Equal(PdfValueKind.Name, map["Agree"].Kind);
    }

    [Theory]
    [InlineData(PdfFormDataFormat.Fdf)]
    [InlineData(PdfFormDataFormat.Xml)]
    [InlineData(PdfFormDataFormat.Json)]
    public void TypedFormatsRetainEmptyArraysAndXmlInexpressibleText(PdfFormDataFormat format)
    {
        var data = PdfFormData.Create(new[] { new PdfFormDataField("N\0ame", T("text\0\u0001\t")), new PdfFormDataField("Array", PdfValue.Array()) });
        using var output = new MemoryStream();
        data.Write(output, format);
        output.Position = 0;
        var read = PdfFormData.Read(output, format);
        Assert.Equal("text\0\u0001\t", Text(read.Fields.Single(x => x.Name == "N\0ame").Value));
        var array = read.Fields.Single(x => x.Name == "Array").Value;
        Assert.Equal(PdfValueKind.Array, array.Kind);
        Assert.Empty(array.Items);
    }

    [Theory]
    [InlineData("{\"version\":1,\"version\":1,\"fields\":[]}")]
    [InlineData("{\"version\":01,\"fields\":[]}")]
    [InlineData("{\"version\":1,\"fields\":[{\"name\":\"a\",\"type\":\"text\",\"value\":\"\\ud800\"}]}")]
    [InlineData("{\"version\":1,\"fields\":[{\"name\":\"a\",\"type\":\"name\",\"value\":[\"Yes\"]}]}")]
    [InlineData("{\"version\":1,\"fields\":[],}")]
    public void JsonRejectsDuplicatePropertiesMalformedUnicodeAndAmbiguousShapes(string json) => Assert.Equal("Forms.DataJson", Error(() => Read(json, PdfFormDataFormat.Json)));
    [Fact]
    public void UntrustedXmlCannotResolveEntitiesOrImportAdditionalInstructions()
    {
        string dtd = "<!DOCTYPE xfdf [<!ENTITY x SYSTEM 'file:///MUST-NOT-OPEN'>]><xfdf xmlns='http://ns.adobe.com/xfdf/'><fields><field name='a'><value>&x;</value></field></fields></xfdf>";
        Assert.Equal("Forms.DataXml", Error(() => Read(dtd, PdfFormDataFormat.Xfdf)));
        Assert.StartsWith("Forms.DataXml", Error(() => Read("<xfdf xmlns='http://ns.adobe.com/xfdf/'><fields/><annots/></xfdf>", PdfFormDataFormat.Xfdf)));
        Assert.Equal("Forms.DataXmlProfile", Error(() => Read("<form-data xmlns='urn:fluent-pdf:form-data:1' version='1'><field name='a' type='text' execute='yes'><value>x</value></field></form-data>", PdfFormDataFormat.Xml)));
    }

    [Fact]
    public void FdfAcceptsOptionalCrossReferencesAndIndirectFieldsWithoutExecutingActions()
    {
        string body = "%FDF-1.2\n1 0 obj\n<< /FDF << /Fields [2 0 R] >> >>\nendobj\n";
        int offset = Encoding.ASCII.GetByteCount(body);
        body += "2 0 obj\n<< /T (Agree) /V /Yes >>\nendobj\n";
        int xref = Encoding.ASCII.GetByteCount(body);
        string rows = "xref\n0 3\n0000000000 65535 f\n0000000009 00000 n\n" + offset.ToString("D10", System.Globalization.CultureInfo.InvariantCulture) + " 00000 n\n";
        var data = Read(body + rows + "trailer << /Root 1 0 R /Size 3 >>\nstartxref\n" + xref + "\n%%EOF", PdfFormDataFormat.Fdf);
        Assert.Equal("Yes", Assert.Single(data.Fields).Value.NameValue);
        var noTable = Read(body + "trailer << /Root 1 0 R >>\n%%EOF", PdfFormDataFormat.Fdf);
        Assert.Equal("Agree", Assert.Single(noTable.Fields).Name);
        string action = "%FDF-1.2\n1 0 obj << /FDF << /Fields [] /JavaScript << /Before (MUST NOT EXECUTE) >> >> >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF";
        Assert.Equal("Forms.DataFdfProfile", Error(() => Read(action, PdfFormDataFormat.Fdf)));
    }

    [Theory]
    [InlineData("1 1 obj << /FDF << /Fields [] >> >> endobj", "Forms.DataFdfObject")]
    [InlineData("1 0 obj << /FDF << /Fields [2 0 R] >> >> endobj 2 0 obj << /T (A) /Kids [2 0 R] >> endobj", "Forms.DataFdfFieldCycle")]
    [InlineData("1 0 obj << /FDF << /Fields [] >> >> endobj 1 0 obj null endobj", "Forms.DataFdfObject")]
    public void InvalidFdfGenerationsCyclesAndDuplicateObjectsReject(string body, string code) => Assert.Equal(code, Error(() => Read("%FDF-1.2\n" + body + "\ntrailer << /Root 1 0 R >>\n%%EOF", PdfFormDataFormat.Fdf)));
    [Fact]
    public void NamesSchemasUtf8DepthAndCancellationAreBounded()
    {
        Assert.Equal("Forms.DataDuplicateName", Error(() => PdfFormData.Create(new[] { new PdfFormDataField("a", T("x")), new PdfFormDataField("a", T("y")) })));
        var distinct = PdfFormData.Create(new[] { new PdfFormDataField("a", T("x")), new PdfFormDataField("A", T("y")) });
        Assert.Equal(2, distinct.Fields.Count);
        using var badUtf8 = new MemoryStream(new byte[] { 0xff, 0xfe });
        Assert.Equal("Forms.DataUtf8", Error(() => PdfFormData.Read(badUtf8, PdfFormDataFormat.Json)));
        Assert.Throws<OperationCanceledException>(() => PdfFormData.Create(Array.Empty<PdfFormDataField>(), cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.Objects", Error(() => PdfFormData.Create(new[] { new PdfFormDataField("a", T("x")) }, new(limits: new(maximumObjects: 1)))));
        var nested = PdfFormData.Create(new[] { new PdfFormDataField("a.b.c.d.e.f.g.h.i", T("x")) });
        using var output = new MemoryStream();
        Assert.Equal("Limit.Depth", Error(() => nested.Write(output, PdfFormDataFormat.Xfdf, new(limits: new(maximumDepth: 8)))));
        Assert.Equal(0, output.Length);
    }
}
