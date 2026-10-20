using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class MetadataTests
{
    internal const string Packet = "<x:xmpmeta xmlns:x='adobe:ns:meta/'><rdf:RDF xmlns:rdf='http://www.w3.org/1999/02/22-rdf-syntax-ns#'><rdf:Description rdf:about='' xmlns:dc='http://purl.org/dc/elements/1.1/' xmlns:app='urn:fluent-pdf:fixture'><dc:title><rdf:Alt><rdf:li xml:lang='x-default'>original</rdf:li><rdf:li xml:lang='fr'>Titre conservé</rdf:li></rdf:Alt></dc:title><app:Structured app:flag='yes'><app:Child><![CDATA[private & structured]]></app:Child></app:Structured></rdf:Description></rdf:RDF></x:xmpmeta>";
    private static PdfValue D(params (string, PdfValue)[] pairs) => NavigationTests.D(pairs);
    private static PdfDocument Bare()
    {
        using var blank = Pdf.Create().AddPage();
        using var bytes = new MemoryStream();
        blank.Save(bytes);
        bytes.Position = 0;
        return Pdf.Open(bytes);
    }

    private static byte[] Save(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    [Fact]
    public void EmptyMetadataInspectionIsPassiveAndDetached()
    {
        using var source = Bare();
        var set = PdfMetadata.Inspect(source.CreateSnapshot());
        Assert.Empty(set.Text);
        Assert.Null(set.XmpXml);
        Assert.Equal(PdfValueKind.Null, set.Identity.Kind);
        Assert.True(set.OperationReport.Succeeded);
        source.Dispose();
        Assert.Single(set.Source.Pages);
    }

    [Fact]
    public void UnicodeInfoDatesIdentityAndUnknownXmpNamespacesRoundTrip()
    {
        using var source = Bare();
        byte[] id = Enumerable.Range(0, 16).Select(i => (byte)i).ToArray();
        var instant = new DateTimeOffset(2024, 2, 29, 13, 14, 15, TimeSpan.FromMinutes(330));
        using var edited = PdfMetadata.Edit(source.CreateSnapshot(), editor => editor.SetInformation("Title", "Հայերեն مرحبا 🗎").SetInformation("Author", "Writer").SetInformation("Project", "One & Two").SetCreationTime(instant).SetModificationTime(instant).SetIdentity(id).SetXmp(Packet).SetXmpProperty("urn:fluent-pdf:fixture", "Scalar", "value < & >").Synchronize(PdfMetadataSynchronization.InformationToXmp));
        id[0] = 255;
        var set = PdfMetadata.Inspect(edited.CreateSnapshot());
        Assert.Equal("Հայերեն مرحبا 🗎", set.Text["Title"]);
        Assert.Equal("One & Two", set.Text["Project"]);
        Assert.Equal("D:20240229131415+05'30'", set.Text["CreationDate"]);
        Assert.Equal(0, set.Identity.Items[0].StringBytes[0]);
        Assert.Equal(set.Identity.Items[0].StringBytes, set.Identity.Items[1].StringBytes);
        var xml = XDocument.Parse(set.XmpXml!);
        XNamespace custom = "urn:fluent-pdf:fixture";
        XNamespace rdf = PdfMetadataXml.Rdf;
        Assert.Equal("private & structured", xml.Descendants(custom + "Child").Single().Value);
        Assert.Equal("yes", xml.Descendants(custom + "Structured").Single().Attribute(custom + "flag")!.Value);
        Assert.Equal("Titre conservé", xml.Descendants(rdf + "li").Single(x => (string? )x.Attribute(XNamespace.Xml + "lang") == "fr").Value);
        Assert.Equal("value < & >", xml.Descendants(custom + "Scalar").Single().Value);
        using var loaded = Pdf.Open(new MemoryStream(Save(edited)));
        Assert.Equal(set.Text["Title"], PdfMetadata.Inspect(loaded.CreateSnapshot()).Text["Title"]);
    }

    [Fact]
    public void ExplicitMetadataValuesProduceDeterministicBytesWithoutAClockOrRandomId()
    {
        using var source = Bare();
        var snapshot = source.CreateSnapshot();
        PdfDocument Make() => PdfMetadata.Edit(snapshot, e => e.SetInformation("Title", "deterministic").SetModificationTime(new DateTimeOffset(2023, 1, 2, 3, 4, 5, TimeSpan.Zero)).SetIdentity(new byte[] { 1, 2, 3 }).Synchronize(PdfMetadataSynchronization.InformationToXmp));
        using var first = Make();
        using var second = Make();
        Assert.Equal(Save(first), Save(second));
    }

    [Fact]
    public void XmpAuthoritativeSynchronizationPreservesPartialDatesAndExplicitOffsets()
    {
        using var source = Bare();
        string packet = Packet.Replace("<app:Structured", "<xmp:CreateDate xmlns:xmp='http://ns.adobe.com/xap/1.0/'>2024-02</xmp:CreateDate><xmp:ModifyDate xmlns:xmp='http://ns.adobe.com/xap/1.0/'>2024-02-29T12:30:00-03:30</xmp:ModifyDate><dc:creator><rdf:Seq><rdf:li>One author</rdf:li></rdf:Seq></dc:creator><app:Structured");
        using var document = PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetXmp(packet).Synchronize(PdfMetadataSynchronization.XmpToInformation));
        var set = PdfMetadata.Inspect(document.CreateSnapshot());
        Assert.Equal("original", set.Text["Title"]);
        Assert.Equal("One author", set.Text["Author"]);
        Assert.Equal("D:202402", set.Text["CreationDate"]);
        Assert.Equal("D:20240229123000-03'30'", set.Text["ModDate"]);
    }

    [Theory]
    [InlineData("2024-01-01T00:00:00Z", "xmp")]
    [InlineData("2023-12-31T23:59:59Z", "info")]
    public void NewestAuthorityUsesExplicitMetadataDateAndInformationModificationTime(string stamp, string expected)
    {
        using var source = Bare();
        using var document = PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetInformation("Title", "info").SetModificationTime(new(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)).SetXmpProperty(PdfMetadataXml.Xmp.NamespaceName, "MetadataDate", stamp).SetXmpProperty(PdfMetadataXml.Dc.NamespaceName, "title", "xmp").Synchronize(PdfMetadataSynchronization.PreferNewest));
        Assert.Equal(expected, PdfMetadata.Inspect(document.CreateSnapshot()).Text["Title"]);
    }

    [Fact]
    public void UnknownAuthorityAndMultipleCreatorsRejectWithoutFlattening()
    {
        using var source = Bare();
        Assert.Equal("Metadata.AuthorityUndetermined", Error(() => PdfMetadata.Edit(source.CreateSnapshot(), e => e.Synchronize(PdfMetadataSynchronization.PreferNewest))));
        string packet = Packet.Replace("<app:Structured", "<dc:creator><rdf:Seq><rdf:li>A</rdf:li><rdf:li>B</rdf:li></rdf:Seq></dc:creator><app:Structured");
        Assert.Equal("Metadata.MultipleAuthors", Error(() => PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetXmp(packet).Synchronize(PdfMetadataSynchronization.XmpToInformation))));
        Assert.Null(PdfMetadata.Inspect(source.CreateSnapshot()).XmpXml);
    }

    [Theory]
    [InlineData("<x>", "Metadata.MalformedXml")]
    [InlineData("<!DOCTYPE x [<!ENTITY e SYSTEM 'file:///not-a-resource'>]><x>&e;</x>", "Metadata.MalformedXml")]
    [InlineData("<ordinary/>", "Metadata.XmpStructure")]
    public void MalformedAndExternalEntityPacketsHaveStableFailures(string xml, string code)
    {
        using var source = Bare();
        Assert.Equal(code, Error(() => PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetXmp(xml))));
    }

    [Fact]
    public void SwallowedXmlFailuresCannotCommitAndEditorsExpireOnReturn()
    {
        using var source = Bare();
        Assert.Equal("Metadata.MalformedXml", Error(() => PdfMetadata.Edit(source.CreateSnapshot(), e =>
        {
            try
            {
                e.SetXmp("<x>");
            }
            catch (PdfException)
            {
            }

            e.SetInformation("Title", "must not commit");
        })));
        PdfMetadataEditor? retained = null;
        using var document = PdfMetadata.Edit(source.CreateSnapshot(), e =>
        {
            retained = e;
            Exception? wrongThread = null;
            var thread = new Thread(() =>
            {
                try
                {
                    e.SetInformation("Title", "wrong thread");
                }
                catch (Exception error)
                {
                    wrongThread = error;
                }
            });
            thread.Start();
            thread.Join();
            Assert.IsType<InvalidOperationException>(wrongThread);
            e.SetInformation("Title", "owner");
        });
        Assert.Throws<InvalidOperationException>(() => retained!.SetInformation("Title", "expired"));
        Assert.Equal("owner", PdfMetadata.Inspect(document.CreateSnapshot()).Text["Title"]);
    }

    [Fact]
    public void XmlValuesAndUnrepresentableAuthorityDatesHaveStableDiagnostics()
    {
        using var source = Bare();
        Assert.Equal("Metadata.XmlCharacters", Error(() => PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetXmpProperty("urn:fixture", "Value", "invalid\u0001"))));
        Assert.Equal("Metadata.DatePrecisionUnsupported", Error(() => PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetInformation("CreationDate", "D:2024010101Z").Synchronize(PdfMetadataSynchronization.InformationToXmp))));
        Assert.Equal("Metadata.AuthorityUndetermined", Error(() => PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetInformation("ModDate", "D:00010101000000+14'00'").SetXmpProperty(PdfMetadataXml.Xmp.NamespaceName, "MetadataDate", "2024-01-01T00:00:00Z").Synchronize(PdfMetadataSynchronization.PreferNewest))));
    }

    [Fact]
    public void UnicodeStringPacketsDoNotReinterpretTheirEncodingDeclaration()
    {
        using var source = Bare();
        using var edited = PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetXmp("<?xml version='1.0' encoding='UTF-16'?>" + Packet));
        Assert.Contains("private & structured", PdfMetadata.Inspect(edited.CreateSnapshot()).XmpXml!);
    }

    [Fact]
    public void MetadataChecksCancellationBytesObjectsAndXmlDepthBeforeCommit()
    {
        using var source = Bare();
        Assert.Throws<OperationCanceledException>(() => PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetInformation("Title", "x"), cancellationToken: new CancellationToken(true)));
        Assert.Equal("Limit.ExpandedBytes", Error(() => PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetInformation("Title", new string ('x', 20000)), policy: new(limits: new(maximumExpandedBytes: 8192)))));
        Assert.Equal("Limit.Objects", Error(() => PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetInformation("Title", "x"), policy: new(limits: new(maximumObjects: 1)))));
        string deep = Packet.Replace("private & structured", string.Concat(Enumerable.Repeat("<app:N>", 20)) + "x" + string.Concat(Enumerable.Repeat("</app:N>", 20))).Replace("<![CDATA[", "").Replace("]]>", "");
        Assert.Equal("Limit.Depth", Error(() => PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetXmp(deep), policy: new(limits: new(maximumDepth: 8)))));
        Assert.Empty(PdfMetadata.Inspect(source.CreateSnapshot()).Text);
    }

    [Fact]
    public void SubsecondInformationLossIsExplicitAndTrappingUsesAName()
    {
        using var source = Bare();
        Action<PdfMetadataEditor> change = e => e.SetXmpProperty(PdfMetadataXml.Xmp.NamespaceName, "ModifyDate", "2024-01-01T00:00:00.123Z").Synchronize(PdfMetadataSynchronization.XmpToInformation);
        Assert.Equal("Metadata.SubsecondPrecisionLoss", Error(() => PdfMetadata.Edit(source.CreateSnapshot(), change)));
        using var permissive = PdfMetadata.Edit(source.CreateSnapshot(), change, policy: new(PdfProcessingMode.Permissive));
        Assert.Equal("D:20240101000000Z", PdfMetadata.Inspect(permissive.CreateSnapshot()).Text["ModDate"]);
        Assert.Contains(permissive.LastOperationReport!.Diagnostics, d => d.Code == "Metadata.SubsecondPrecisionLoss" && d.Severity == PdfDiagnosticSeverity.Warning);
        using var trapped = PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetInformation("Trapped", "True"));
        Assert.Equal(PdfValueKind.Name, PdfMetadata.Inspect(trapped.CreateSnapshot()).Information.Entries["Trapped"].Kind);
        Assert.Throws<ArgumentException>(() => PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetCreationTime(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero).AddTicks(1))));
    }

    [Fact]
    public void RemovalDropsMetadataComponentsOrphansAndHistoricalRevisions()
    {
        using var original = NavigationTests.Fixture((edit, root, pages) =>
        {
            var secret = edit.Add(NavigationTests.T("obsolete-secret"));
            var info = edit.Add(D(("Title", PdfValue.Reference(secret))));
            var trailer = edit.Trailer.Entries.ToDictionary(x => x.Key, x => x.Value);
            trailer["Info"] = PdfValue.Reference(info);
            trailer["ID"] = PdfValue.Array(PdfValue.String(new byte[] { 1 }), PdfValue.String(new byte[] { 1 }));
            edit.Trailer = PdfValue.Dictionary(trailer);
            using var packet = new MemoryStream(Encoding.UTF8.GetBytes(Packet));
            var metadata = edit.Add(PdfValues.Stream(D(("Type", PdfValue.Name("Metadata")), ("Subtype", PdfValue.Name("XML"))), packet));
            NavigationTests.Put(edit, root, "Metadata", PdfValue.Reference(metadata));
            NavigationTests.Put(edit, pages[0], "Metadata", PdfValue.Reference(metadata));
            edit.Add(NavigationTests.T("unreachable-secret"));
        }, 1);
        byte[] first = Save(original);
        using var input = new MemoryStream(first);
        using var revision = new PdfTransaction(PdfSyntax.Read(input));
        var infoId = revision.Trailer.Entries["Info"].ReferenceId;
        revision.Set(infoId, D(("Title", NavigationTests.T("current-secret"))));
        using var appended = new MemoryStream();
        revision.Save(appended, new(PdfWriteMode.Incremental));
        appended.Position = 0;
        using var source = Pdf.Open(appended);
        Assert.Equal(2, source.SourceDocument!.Revisions.Count);
        using var cleaned = PdfMetadata.Remove(source.CreateSnapshot());
        var set = PdfMetadata.Inspect(cleaned.CreateSnapshot());
        Assert.Empty(set.Text);
        Assert.Null(set.XmpXml);
        Assert.Equal(PdfValueKind.Null, set.Identity.Kind);
        Assert.Single(cleaned.SourceDocument!.Revisions);
        string output = Encoding.ASCII.GetString(Save(cleaned));
        foreach (string secret in new[]
        {
            "obsolete-secret",
            "current-secret",
            "unreachable-secret",
            "private & structured"
        }

        )
        {
            Assert.DoesNotContain(secret, output);
            Assert.DoesNotContain(BitConverter.ToString(Encoding.BigEndianUnicode.GetBytes(secret)).Replace("-", ""), output);
        }

        var page = cleaned.Pages[0].Source!.Dictionary;
        Assert.False(page.Entries.ContainsKey("Metadata"));
        Assert.True(original.SourceDocument!.Revisions.Count >= 1);
    }

    [Fact]
    public void EmbeddedFileRetentionRequiresAnExplicitDecisionAndAVisibleDiagnostic()
    {
        byte[] payload = Encoding.UTF8.GetBytes("embedded internal metadata");
        using var source = NavigationTests.Fixture((edit, root, pages) =>
        {
            using var bytes = new MemoryStream(payload);
            var stream = edit.Add(PdfValues.Stream(D(("Type", PdfValue.Name("EmbeddedFile"))), bytes));
            var file = edit.Add(D(("Type", PdfValue.Name("Filespec")), ("F", NavigationTests.T("data.txt")), ("EF", D(("F", PdfValue.Reference(stream))))));
            NavigationTests.Put(edit, root, "AF", PdfValue.Array(PdfValue.Reference(file)));
        }, 1);
        Assert.Equal("Metadata.EmbeddedFilePolicyRequired", Error(() => PdfMetadata.Remove(source.CreateSnapshot())));
        using var retained = PdfMetadata.Remove(source.CreateSnapshot(), new(embeddedFiles: PdfMetadataEmbeddedFilePolicy.Retain));
        Assert.Contains(retained.LastOperationReport!.Diagnostics, d => d.Code == "Metadata.EmbeddedFilesRetained");
        var graph = retained.SourceDocument!;
        var root = graph.Resolve(graph.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        var fs = graph.Resolve(root.Entries["AF"].Items[0].ReferenceId);
        var fileStream = graph.Resolve(fs.Entries["EF"].Entries["F"].ReferenceId);
        using var content = fileStream.OpenRawStream();
        using var copied = new MemoryStream();
        content.CopyTo(copied);
        Assert.Equal(payload, copied.ToArray());
    }

    [Fact]
    public void CSharpAndVisualBasicConsumersWriteActualSynchronizedMetadata()
    {
        foreach (var write in new Func<Stream, PdfOperationReport>[]
        {
            ContractExamples.CSharp.ContractExample.WriteMetadataPdf,
            ContractExamples.VisualBasic.ContractExample.WriteMetadataPdf
        }

        )
        {
            using var output = new MemoryStream();
            Assert.True(write(output).Succeeded);
            output.Position = 0;
            using var document = Pdf.Open(output);
            var set = PdfMetadata.Inspect(document.CreateSnapshot());
            Assert.Equal("Metadata example", set.Text["Title"]);
            Assert.NotNull(set.XmpXml);
            Assert.True(output.CanRead);
        }
    }

    [Fact]
    public void IncrementalRevisionsPreservePermanentIdentityAndAllowAChangedRevisionId()
    {
        using var bare = Bare();
        using var source = PdfMetadata.Edit(bare.CreateSnapshot(), e => e.SetIdentity(new byte[] { 1, 2, 3 }));
        using (var invalid = new PdfTransaction(source.SourceDocument))
        {
            var trailer = invalid.Trailer.Entries.ToDictionary(x => x.Key, x => x.Value);
            trailer["ID"] = PdfValue.Array(PdfValue.String(new byte[] { 9 }), PdfValue.String(new byte[] { 9 }));
            invalid.Trailer = PdfValue.Dictionary(trailer);
            using var output = new MemoryStream();
            Assert.Equal("Metadata.PermanentIdentifierChanged", Error(() => invalid.Save(output, new(PdfWriteMode.Incremental))));
            Assert.Equal(0, output.Length);
        }

        using var valid = new PdfTransaction(source.SourceDocument);
        var entries = valid.Trailer.Entries.ToDictionary(x => x.Key, x => x.Value);
        entries["ID"] = PdfValue.Array(PdfValue.String(new byte[] { 1, 2, 3 }), PdfValue.String(new byte[] { 4, 5 }));
        valid.Trailer = PdfValue.Dictionary(entries);
        using var written = new MemoryStream();
        valid.Save(written, new(PdfWriteMode.Incremental));
        written.Position = 0;
        var graph = PdfSyntax.Read(written);
        Assert.Equal(new byte[] { 1, 2, 3 }, graph.Revisions[0].Trailer.Entries["ID"].Items[0].StringBytes);
        Assert.Equal(new byte[] { 4, 5 }, graph.Revisions[0].Trailer.Entries["ID"].Items[1].StringBytes);
    }
}
