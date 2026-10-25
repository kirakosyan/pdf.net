using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Fluent.Pdf.Formats;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class CollectionTests
{
    private static readonly XNamespace Ns = "urn:fluent-pdf:objects:1";
    private static PdfDocumentCollection Collection(params string[] names)
        => PdfDocumentCollection.Capture(names.Select(n => new KeyValuePair<string, Stream>(n, new MemoryStream(Encoding.UTF8.GetBytes(n)))));
    private static PdfCollectionAdapters Adapters(PdfCollectionAdapter adapter) => new(new[] { new KeyValuePair<string, PdfCollectionAdapter>("owned", adapter) });
    private static MemoryStream Zip(params string[] names)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, true, new UTF8Encoding(false, true)))
            foreach (string name in names) { using var part = zip.CreateEntry(name).Open(); part.WriteByte(42); }
        stream.Position = 0; return stream;
    }
    private static string Artifact(string name)
    { string path = Path.Combine(AppContext.BaseDirectory, "artifacts", "collections"); Directory.CreateDirectory(path); return Path.Combine(path, name); }
    private static MemoryStream Xml(byte[]? input = null)
    {
        using var source = input == null ? File.OpenRead(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Automation", "original.pdf")) : (Stream)new MemoryStream(input);
        var xml = new MemoryStream(); PdfObjectXml.Export(source, xml); xml.Position = 0; return xml;
    }
    private static void RejectXml(Action<XElement> change, string code = "Interchange.Schema")
    {
        using var xml = Xml(); var document = XDocument.Load(xml); change(document.Root!);
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(document.ToString())); using var output = new MemoryStream();
        Assert.Equal(code, Assert.Throws<PdfException>(() => PdfObjectXml.Import(input, output)).Diagnostic.Code); Assert.Equal(0, output.Length);
    }
    [Fact]
    public void UnicodeArchiveRoundTripPreservesOrderBytesAndBorrowedStreams()
    {
        using var input = new MemoryStream(new byte[] { 0, 128, 255 });
        var collection = PdfDocumentCollection.Capture(new[] { new KeyValuePair<string, Stream>("Հայերեն-東京-😀.pdf", input), new KeyValuePair<string, Stream>("é.txt", new MemoryStream(new byte[] { 7 })) });
        input.Position = 0; input.WriteByte(9);
        using var output = new MemoryStream(); collection.WriteArchive(output); Assert.True(input.CanRead); Assert.True(output.CanWrite);
        File.WriteAllBytes(Artifact("unicode.zip"), output.ToArray()); output.Position = 0;
        var restored = PdfDocumentCollection.ReadArchive(output);
        Assert.Equal(collection.Items.Select(i => i.Name), restored.Items.Select(i => i.Name));
        using var first = restored.Items[0].Open(); Assert.Equal(0, first.ReadByte()); Assert.Equal(128, first.ReadByte()); Assert.Equal(255, first.ReadByte());
        using var other = restored.Items[0].Open(); Assert.Equal(0, other.Position);
        using var again = new MemoryStream(); restored.WriteArchive(again); Assert.Equal(output.ToArray(), again.ToArray());
    }
    [Theory]
    [InlineData("../escape.pdf")] [InlineData("a/b.pdf")] [InlineData("a\\b.pdf")] [InlineData("C:evil.pdf")] [InlineData("/root.pdf")]
    [InlineData("a%2fb.pdf")] [InlineData("a.pdf.")] [InlineData("a.pdf ")] [InlineData("CON.pdf")] [InlineData("LPT¹.txt")]
    [InlineData("e\u0301.pdf")] [InlineData("\u202eevil.pdf")] [InlineData("a\n.pdf")]
    public void UnsafeArchiveAndCaptureNamesRejectOnEveryPlatform(string name)
    {
        Assert.Equal("Collection.Name", Assert.Throws<PdfException>(() => Collection(name)).Diagnostic.Code);
        using var archive = Zip(name.Replace('\n', '_'));
        byte[] bytes = archive.ToArray();
        if (name.IndexOf('\n') >= 0)
        {
            int central = Find(bytes, new byte[] { 0x50, 0x4b, 1, 2 });
            bytes[31] = bytes[central + 47] = 10;
        }
        using var malformed = new MemoryStream(bytes); Assert.Throws<PdfException>(() => PdfDocumentCollection.ReadArchive(malformed));
    }
    [Theory] [InlineData("a.pdf", "A.PDF")] [InlineData("é.pdf", "É.pdf")] [InlineData("a.pdf", "a.pdf")]
    public void CaseAndExactCollisionsRejectBeforeReturningAnyEntries(string first, string second)
    {
        Assert.Throws<PdfException>(() => Collection(first, second)); using var zip = Zip(first, second);
        Assert.Throws<PdfException>(() => PdfDocumentCollection.ReadArchive(zip));
    }
    [Fact]
    public void LinksCorruptionAndHeaderIdentityMismatchesReject()
    {
        using var original = new MemoryStream(); Collection("a.txt").WriteArchive(original);
        byte[] bytes = original.ToArray(); int central = Find(bytes, new byte[] { 0x50, 0x4b, 1, 2 });
        var link = (byte[])bytes.Clone(); link[central + 5] = 3; link[central + 40] = 0xff; link[central + 41] = 0xa1;
        Assert.Equal("Format.PackageProfile", Assert.Throws<PdfException>(() => PdfDocumentCollection.ReadArchive(new MemoryStream(link))).Diagnostic.Code);
        var corrupt = (byte[])bytes.Clone(); corrupt[35] ^= 1;
        Assert.Equal("Format.PackageIntegrity", Assert.Throws<PdfException>(() => PdfDocumentCollection.ReadArchive(new MemoryStream(corrupt))).Diagnostic.Code);
        var mismatch = (byte[])bytes.Clone(); mismatch[30] = (byte)'b';
        Assert.Equal("Format.PackageIdentity", Assert.Throws<PdfException>(() => PdfDocumentCollection.ReadArchive(new MemoryStream(mismatch))).Diagnostic.Code);
    }
    private static int Find(byte[] bytes, byte[] pattern)
    { for (int i = 0; i <= bytes.Length - pattern.Length; i++) if (bytes.Skip(i).Take(pattern.Length).SequenceEqual(pattern)) return i; throw new InvalidOperationException(); }
    [Fact]
    public void FailedBatchItemDiscardsPartialBytesAndContinuesInOrder()
    {
        var original = Collection("first", "bad", "last");
        var result = original.Process("owned", Adapters((name, input, output, c) => { input.CopyTo(output); if (name == "bad") throw new IOException("private details"); }));
        Assert.Equal(new[] { "first", "bad", "last" }, result.Outcomes.Select(i => i.Name));
        Assert.NotNull(result.Outcomes[0].Output); Assert.Null(result.Outcomes[1].Output); Assert.NotNull(result.Outcomes[2].Output);
        Assert.Equal("Collection.AdapterFailed", result.Outcomes[1].DiagnosticCode); Assert.True(result.OperationReport.Succeeded);
        Assert.Single(result.OperationReport.Diagnostics); Assert.DoesNotContain("private", result.OperationReport.Diagnostics[0].Message);
        var retry = original.Process("owned", Adapters((_, input, output, c) => input.CopyTo(output))); Assert.All(retry.Outcomes, o => Assert.NotNull(o.Output));
    }
    [Fact]
    public void LatchedSdkAdapterErrorsAbortTheSharedBatchEvenWhenAdapterSwallowsThem()
    {
        var attempted = new List<string>();
        var failure = Assert.Throws<PdfException>(() => Collection("first", "bad", "last").Process("owned", Adapters((name, input, output, c) =>
        {
            attempted.Add(name);
            if (name != "bad") { input.CopyTo(output); return; }
            try { c.Report(new PdfDiagnostic("Fixture.InvalidItem", "Malformed item.")); }
            catch (PdfException) { }
        })));
        Assert.Equal("Fixture.InvalidItem", failure.Diagnostic.Code);
        Assert.Equal(new[] { "first", "bad" }, attempted);
    }
    [Fact]
    public void AdapterRegistriesAreDetachedAndDoNotShareProcessConfiguration()
    {
        int first = 0, second = 0;
        var registrations = new List<KeyValuePair<string, PdfCollectionAdapter>> { new("owned", (_, input, output, c) => first++) };
        var one = new PdfCollectionAdapters(registrations); registrations.Clear();
        var two = Adapters((_, input, output, c) => second++);
        Collection("a").Process("owned", one); Collection("b").Process("owned", two);
        Assert.Equal(1, first); Assert.Equal(1, second);
        Assert.Equal("Collection.AdapterUnavailable", Assert.Throws<PdfException>(() => Collection("c").Process("missing", one)).Diagnostic.Code);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void SwallowedLimitsAndLeakedScopesStillAbortBatch(bool leak)
    {
        int invoked = 0;
        var adapter = Adapters((name, input, output, c) =>
        {
            invoked++;
            if (leak) { _ = c.EnterScope(PdfResourceKind.Depth); return; }
            try { c.Consume(PdfResourceKind.OutputBytes, long.MaxValue); } catch (PdfException) { }
        });
        Assert.Equal(leak ? "Operation.ScopeLeaked" : "Limit.OutputBytes", Assert.Throws<PdfException>(() => Collection("a", "b").Process("owned", adapter)).Diagnostic.Code);
        Assert.Equal(1, invoked);
    }
    [Fact]
    public void CancellationStopsBeforeNextItemAndEveryBoundaryHonorsBudgets()
    {
        using var cancel = new CancellationTokenSource(); int invoked = 0;
        Assert.ThrowsAny<OperationCanceledException>(() => Collection("a", "b").Process("owned", Adapters((_, input, output, c) => { invoked++; cancel.Cancel(); }), cancellationToken: cancel.Token));
        Assert.Equal(1, invoked);
        var collection = Collection("a"); using var output = new MemoryStream();
        Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => collection.WriteArchive(output, new(limits: new(maximumOutputBytes: 1)))).Diagnostic.Code);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => collection.WriteArchive(output, new(limits: new(maximumExpandedBytes: 1)))).Diagnostic.Code);
        using var input = new MemoryStream(new byte[10]);
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => PdfDocumentCollection.Capture(new[] { new KeyValuePair<string, Stream>("a", input) }, new(limits: new(maximumInputBytes: 1)))).Diagnostic.Code);
        Assert.ThrowsAny<OperationCanceledException>(() => PdfDocumentCollection.ReadArchive(input, cancellationToken: cancel.Token));
    }
    [Fact]
    public void AtomicArchiveFailurePreservesExistingDestination()
    {
        string path = Artifact("atomic-" + Guid.NewGuid().ToString("N") + ".zip"); File.WriteAllText(path, "original");
        Assert.Throws<PdfException>(() => Collection("a").WriteArchiveFile(path, new(limits: new(maximumOutputBytes: 1))));
        Assert.Equal("original", File.ReadAllText(path));
        Collection("a").WriteArchiveFile(path); using var file = File.OpenRead(path); Assert.Single(PdfDocumentCollection.ReadArchive(file).Items);
    }
    [Theory] [InlineData(false)] [InlineData(true)]
    public void CompiledLanguagesAndIndependentReaderFixturesRoundTrip(bool visualBasic)
    {
        byte[] original = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "Automation", "original.pdf"));
        using var archive = new MemoryStream(); using var restored = new MemoryStream();
        if (visualBasic) ContractExamples.VisualBasic.ContractExample.CollectionRoundTrip(original, archive, restored);
        else ContractExamples.CSharp.ContractExample.CollectionRoundTrip(original, archive, restored);
        string stem = visualBasic ? "visual-basic" : "csharp";
        File.WriteAllBytes(Artifact(stem + ".zip"), archive.ToArray()); File.WriteAllBytes(Artifact(stem + ".pdf"), restored.ToArray()); File.WriteAllBytes(Artifact("original.pdf"), original);
        using var doc = Pdf.Open(new MemoryStream(restored.ToArray())); using var expected = Pdf.Open(new MemoryStream(original)); Assert.Equal(expected.Pages.Count, doc.Pages.Count);
        Assert.True(archive.CanWrite); Assert.True(restored.CanWrite);
    }
    [Theory] [InlineData(PdfCrossReferenceFormat.Table)] [InlineData(PdfCrossReferenceFormat.Stream)]
    public void TypedXmlRetainsNamesExactNumbersOpaqueBytesAndTrailerIds(PdfCrossReferenceFormat format)
    {
        using var transaction = WriterTests.Create();
        var id = transaction.Add(PdfValue.Array(PdfValue.Null, PdfValue.Boolean(true), PdfValue.Number("+0001.2300"), PdfValue.String(0, 255), PdfValue.Name("space \u00ff")));
        using var pdf = new MemoryStream(); transaction.Save(pdf, new(crossReferenceFormat: format));
        using var xml = Xml(pdf.ToArray()); File.WriteAllBytes(Artifact("objects-" + format + ".xml"), xml.ToArray());
        using var output = new MemoryStream(); var report = PdfObjectXml.Import(xml, output); Assert.Contains(report.Diagnostics, d => d.Code == "Interchange.ActiveGraph");
        output.Position = 0; var graph = PdfSyntax.Read(output);
        Assert.Equal("+0001.2300", graph.Resolve(id).Items[2].NumberText); Assert.Equal(new byte[] { 0, 255 }, graph.Resolve(id).Items[3].StringBytes);
        Assert.Equal("space \u00ff", graph.Resolve(id).Items[4].NameValue);
        Assert.Equal(new byte[] { 0, 255 }, graph.Revisions[0].Trailer.Entries["ID"].Items[0].StringBytes);
        File.WriteAllBytes(Artifact("objects-" + format + ".pdf"), output.ToArray());
    }
    [Fact]
    public void XmlRejectsUnknownVocabularyAndAttributesDuplicateObjectsAndProtection()
    {
        RejectXml(r => r.SetAttributeValue("extension", "silent-loss"));
        RejectXml(r => r.Add(new XElement(Ns + "unknown")));
        RejectXml(r => r.Add(new XElement(r.Elements(Ns + "object").First())), "Interchange.Identity");
        RejectXml(r => r.Element(Ns + "trailer")!.Element(Ns + "dictionary")!.Add(new XElement(Ns + "entry", new XAttribute("key", Convert.ToBase64String(Encoding.ASCII.GetBytes("Encrypt"))), new XElement(Ns + "null"))), "Interchange.Protection");
        RejectXml(r => r.SetAttributeValue("version", "2"), "Interchange.Version");
        RejectXml(r => r.Elements(Ns + "object").First().SetAttributeValue("number", "1000001"), "Interchange.Identity");
    }
    [Fact]
    public void XmlRejectsExternalEntitiesAndHonorsInputExpansionOutputAndCancellation()
    {
        using var evil = new MemoryStream(Encoding.UTF8.GetBytes("<!DOCTYPE pdf [<!ENTITY a SYSTEM 'file:///never-read'>]><pdf xmlns='urn:fluent-pdf:objects:1' version='1' pdfVersion='1.7'>&a;</pdf>")); using var output = new MemoryStream();
        Assert.Equal("Format.Xml", Assert.Throws<PdfException>(() => PdfObjectXml.Import(evil, output)).Diagnostic.Code); Assert.Equal(0, output.Length);
        using var xml = Xml();
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => PdfObjectXml.Import(xml, output, new(limits: new(maximumInputBytes: 1)))).Diagnostic.Code);
        xml.Position = 0; Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfObjectXml.Import(xml, output, new(limits: new(maximumExpandedBytes: 1)))).Diagnostic.Code);
        xml.Position = 0; Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => PdfObjectXml.Import(xml, output, new(limits: new(maximumOutputBytes: 1)))).Diagnostic.Code);
        Assert.ThrowsAny<OperationCanceledException>(() => PdfObjectXml.Export(xml, output, cancellationToken: new CancellationToken(true)));
    }
    [Fact]
    public void FiniteEntryAndByteLimitsRejectWithoutTruncation()
    {
        Assert.Equal("Collection.Empty", Assert.Throws<PdfException>(() => Collection()).Diagnostic.Code);
        Assert.Equal("Collection.Identity", Assert.Throws<PdfException>(() => Collection(Enumerable.Range(0, 4097).Select(i => "entry-" + i).ToArray())).Diagnostic.Code);
        using var oversized = new MemoryStream(new byte[16777217]);
        Assert.Equal("Format.InputExtent", Assert.Throws<PdfException>(() => PdfDocumentCollection.Capture(new[] { new KeyValuePair<string, Stream>("large", oversized) })).Diagnostic.Code);
    }
    [Fact]
    public void MalformedUtf8NameCannotAcquireAnAlternateIdentity()
    {
        using var original = new MemoryStream(); Collection("a.txt").WriteArchive(original);
        byte[] bytes = original.ToArray(); int central = Find(bytes, new byte[] { 0x50, 0x4b, 1, 2 });
        bytes[30] = bytes[central + 46] = 255;
        Assert.Equal("Format.PackageName", Assert.Throws<PdfException>(() => PdfDocumentCollection.ReadArchive(new MemoryStream(bytes))).Diagnostic.Code);
        Assert.Equal("Collection.Name", Assert.Throws<PdfException>(() => Collection("bad\ud800.txt")).Diagnostic.Code);
    }
    [Fact]
    public void BatchInputsAndOutputsUseTheOperationLease()
    {
        var collection = Collection("first", "second"); int invoked = 0;
        Assert.Equal("Limit.InputBytes", Assert.Throws<PdfException>(() => collection.Process("owned", Adapters((name, input, output, c) => { invoked++; input.CopyTo(output); }), new(limits: new(maximumInputBytes: 1)))).Diagnostic.Code);
        Assert.Equal(0, invoked);
        Assert.Equal("Limit.OutputBytes", Assert.Throws<PdfException>(() => collection.Process("owned", Adapters((name, input, output, c) => { invoked++; input.CopyTo(output); }), new(limits: new(maximumOutputBytes: 1)))).Diagnostic.Code);
        Assert.Equal(1, invoked);
    }
    [Fact]
    public void XmlRejectsRootTextCommentsDuplicateKeysAndNoncanonicalBase64()
    {
        RejectXml(r => r.Add("discarded text"));
        RejectXml(r => r.Add(new XComment("unsupported annotation")));
        RejectXml(r => { var dictionary = r.Element(Ns + "trailer")!.Element(Ns + "dictionary")!; dictionary.Add(new XElement(dictionary.Elements().First())); });
        RejectXml(r => r.Descendants(Ns + "entry").First().SetAttributeValue("key", "bad padding"));
    }
    [Fact]
    public void XmlCannotSilentlyImportOrExportSignedObjectGraphs()
    {
        RejectXml(r => r.Element(Ns + "trailer")!.Element(Ns + "dictionary")!.Add(new[] { "Contents", "ByteRange" }.Select(name => new XElement(Ns + "entry", new XAttribute("key", Convert.ToBase64String(Encoding.ASCII.GetBytes(name))), new XElement(Ns + "null")))), "Interchange.Protection");
        using var transaction = WriterTests.Create(); transaction.Add(WriterTests.Dictionary(("Contents", PdfValue.String(1, 2)), ("ByteRange", PdfValue.Array(PdfValue.Integer(0), PdfValue.Integer(1)))));
        using var signed = new MemoryStream(); transaction.Save(signed, new(allowSignatureInvalidation: true)); signed.Position = 0;
        using var output = new MemoryStream(); Assert.Equal("Interchange.Protection", Assert.Throws<PdfException>(() => PdfObjectXml.Export(signed, output)).Diagnostic.Code); Assert.Equal(0, output.Length);
    }
}
