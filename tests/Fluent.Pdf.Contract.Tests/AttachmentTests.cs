using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class AttachmentTests
{
    private static PdfValue D(params (string, PdfValue)[] pairs) => NavigationTests.D(pairs);
    private static PdfValue T(string value) => NavigationTests.T(value);
    private static readonly DateTimeOffset Stamp = new(2024, 2, 29, 3, 4, 5, TimeSpan.Zero);
    private static PdfDocument Bare() => NavigationTests.Fixture((_, _, _) =>
    {
    }, 1);
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    private static PdfDocument FileFixture(byte[] bytes, PdfValue? parameters = null, string fileName = "data.bin", bool typedStream = true, string? filter = null) => NavigationTests.Fixture((edit, root, pages) =>
    {
        using var input = new MemoryStream(bytes);
        var dictionary = parameters == null ? D() : D(("Params", parameters));
        var entries = dictionary.Entries.ToDictionary(p => p.Key, p => p.Value);
        if (typedStream)
            entries["Type"] = PdfValue.Name("EmbeddedFile");
        if (filter != null)
            entries["Filter"] = PdfValue.Name(filter);
        var stream = edit.Add(PdfValues.Stream(PdfValue.Dictionary(entries), input));
        var specification = edit.Add(D(("Type", PdfValue.Name("Filespec")), ("UF", T(fileName)), ("F", T("fallback.bin")), ("EF", D(("F", PdfValue.Reference(stream))))));
        NavigationTests.Put(edit, root, "Names", D(("EmbeddedFiles", D(("Names", PdfValue.Array(T(fileName), PdfValue.Reference(specification)))))));
    }, 1);
    [Fact]
    public void UnicodeAttachmentMetadataAndAssociatedRelationshipSurviveRoundTrip()
    {
        byte[] bytes = Encoding.UTF8.GetBytes("payload العربية Հայերեն 😀");
        using var source = Bare();
        using var input = new MemoryStream(bytes);
        using var document = PdfAttachments.Add(source.CreateSnapshot(), input, new("տվյալներ.json", "application/json", "Unicode description", Stamp, Stamp, "Data"));
        Assert.True(input.CanRead);
        Assert.Empty(PdfAttachments.Inspect(source.CreateSnapshot()).Items);
        var set = PdfAttachments.Inspect(document.CreateSnapshot());
        var file = Assert.Single(set.Items);
        Assert.Equal("տվյալներ.json", file.FileName);
        Assert.Equal(file.FileName, file.Name);
        Assert.Equal("Data", file.Relationship);
        Assert.Equal("Unicode description", file.Description);
        Assert.Equal(2, file.Variants.Count);
        Assert.Single(set.EmbeddedStreams);
        Assert.Equal(PdfValueKind.Null, set.Collection.Kind);
        Assert.Equal("2.0", document.SourceDocument!.Version);
        using var output = new MemoryStream();
        Assert.True(PdfAttachments.Extract(file, output).Succeeded);
        Assert.Equal(bytes, output.ToArray());
        Assert.True(output.CanWrite);
        var root = document.SourceDocument.Resolve(document.SourceDocument.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        Assert.Equal(file.Id, root.Entries["AF"].Items[0].ReferenceId);
        var parameters = file.Variants["F"].Entries["Params"];
        Assert.Equal(bytes.Length, parameters.Entries["Size"].NumberValue);
        Assert.Equal(16, parameters.Entries["CheckSum"].StringBytes.Count);
        Assert.Equal("D:20240229030405Z", Encoding.BigEndianUnicode.GetString(parameters.Entries["ModDate"].StringBytes.Skip(2).ToArray()));
        document.Dispose();
        using var afterDispose = new MemoryStream();
        PdfAttachments.Extract(file, afterDispose);
        Assert.Equal(bytes, afterDispose.ToArray());
    }

    [Fact]
    public void EmbeddingStartsAtBorrowedInputPositionAndDuplicateNamesRejectWithoutMutation()
    {
        using var source = Bare();
        using var input = new MemoryStream(new byte[] { 9, 8, 1, 2, 3 });
        input.Position = 2;
        using var document = PdfAttachments.Add(source.CreateSnapshot(), input, new("a.bin"));
        using var output = new MemoryStream();
        PdfAttachments.Extract(Assert.Single(PdfAttachments.Inspect(document.CreateSnapshot()).Items), output);
        Assert.Equal(new byte[] { 1, 2, 3 }, output.ToArray());
        input.Position = 2;
        Assert.Equal("Attachments.DuplicateName", Error(() => PdfAttachments.Add(document.CreateSnapshot(), input, new("a.bin"))));
        Assert.Equal(2, input.Position);
        Assert.Single(PdfAttachments.Inspect(document.CreateSnapshot()).Items);
    }

    [Theory]
    [InlineData("../escape.txt")]
    [InlineData("..\\escape.txt")]
    [InlineData("/root.txt")]
    [InlineData("C:drive.txt")]
    [InlineData("name:stream")]
    [InlineData("CON.txt")]
    [InlineData("LPT¹.bin")]
    [InlineData("COM9")]
    [InlineData("trailing.")]
    [InlineData("trailing ")]
    [InlineData("bad\u0001.bin")]
    [InlineData(".")]
    [InlineData("..")]
    public void SourceNamesStayInspectableButCannotChooseUnsafeExtractionPaths(string name)
    {
        Assert.Throws<ArgumentException>(() => new PdfAttachmentOptions(name));
        using var source = FileFixture(new byte[] { 1 }, fileName: name);
        var file = Assert.Single(PdfAttachments.Inspect(source.CreateSnapshot()).Items);
        Assert.Equal(name, file.FileName);
        Assert.Throws<ArgumentException>(() => PdfAttachments.ExtractToDirectory(file, Path.GetTempPath()));
    }

    [Fact]
    public void ExtractionHasAtomicNoOverwriteDefaultAndExplicitReplacement()
    {
        string directory = Path.Combine(Path.GetTempPath(), "fluent-attachments-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string target = Path.Combine(directory, "chosen.bin");
        try
        {
            using var source = FileFixture(new byte[] { 1, 2, 3 }, fileName: "../unsafe");
            var file = Assert.Single(PdfAttachments.Inspect(source.CreateSnapshot()).Items);
            File.WriteAllBytes(target, new byte[] { 9 });
            Assert.Throws<IOException>(() => PdfAttachments.ExtractToDirectory(file, directory, "chosen.bin"));
            Assert.Equal(new byte[] { 9 }, File.ReadAllBytes(target));
            Assert.Single(Directory.GetFiles(directory));
            PdfAttachments.ExtractToDirectory(file, directory, "chosen.bin", overwrite: true);
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(target));
            File.Delete(target);
            PdfAttachments.ExtractToDirectory(file, directory, "chosen.bin");
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(target));
            Assert.Single(Directory.GetFiles(directory));
        }
        finally
        {
            File.Delete(target);
            Directory.Delete(directory);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DeclaredChecksumAndSizeFailuresPrecedeAnyOutput(bool checksum)
    {
        using var source = FileFixture(new byte[] { 1, 2, 3 }, checksum ? D(("CheckSum", PdfValue.String(new byte[16]))) : D(("Size", PdfValue.Integer(4))));
        var file = Assert.Single(PdfAttachments.Inspect(source.CreateSnapshot()).Items);
        using var output = new MemoryStream();
        output.WriteByte(99);
        Assert.Equal(checksum ? "Attachments.ChecksumMismatch" : "Attachments.SizeMismatch", Error(() => PdfAttachments.Extract(file, output)));
        Assert.Equal(new byte[] { 99 }, output.ToArray());
    }

    [Fact]
    public void BoundariesAndCancellationPreserveSourceAndCallerStreams()
    {
        using var source = Bare();
        using var input = new MemoryStream(new byte[50000]);
        Assert.Equal("Limit.InputBytes", Error(() => PdfAttachments.Add(source.CreateSnapshot(), input, new("a.bin"), policy: new(limits: new(maximumInputBytes: 32)))));
        input.Position = 0;
        Assert.Equal("Limit.ExpandedBytes", Error(() => PdfAttachments.Add(source.CreateSnapshot(), input, new("a.bin"), policy: new(limits: new(maximumExpandedBytes: 16384)))));
        Assert.True(input.CanRead);
        Assert.Throws<OperationCanceledException>(() => PdfAttachments.Add(source.CreateSnapshot(), input, new("a.bin"), cancellationToken: new CancellationToken(true)));
        using var document = FileFixture(new byte[50000]);
        var file = Assert.Single(PdfAttachments.Inspect(document.CreateSnapshot()).Items);
        using var output = new MemoryStream();
        Assert.Equal("Limit.ExpandedBytes", Error(() => PdfAttachments.Extract(file, output, policy: new(limits: new(maximumExpandedBytes: 16384)))));
        Assert.Equal(0, output.Length);
        Assert.Equal("Limit.OutputBytes", Error(() => PdfAttachments.Extract(file, output, policy: new(limits: new(maximumOutputBytes: 32)))));
        Assert.Equal(0, output.Length);
        Assert.Empty(PdfAttachments.Inspect(source.CreateSnapshot()).Items);
    }

    [Fact]
    public void AllActiveVariantsOrphansRelatedStreamsAndHistoricalRevisionsAreRemoved()
    {
        byte[] marker = Encoding.ASCII.GetBytes("private-attachment-marker-2d248762");
        using var source = NavigationTests.Fixture((edit, root, pages) =>
        {
            using var bytes = new MemoryStream(marker);
            var stream = edit.Add(PdfValues.Stream(D(), bytes));
            bytes.Position = 0;
            edit.Add(PdfValues.Stream(D(("Type", PdfValue.Name("EmbeddedFile"))), bytes));
            var alias = edit.Add(PdfValue.Reference(stream));
            var fs = edit.Add(D(("Type", PdfValue.Name("Filespec")), ("F", T("payload.txt")), ("EF", D(("F", PdfValue.Reference(alias)))), ("RF", D(("F", PdfValue.Array(T("related.txt"), PdfValue.Reference(stream)))))));
            NavigationTests.Put(edit, root, "AF", PdfValue.Array(PdfValue.Reference(fs)));
            NavigationTests.Put(edit, root, "UnusualAlias", PdfValue.Array(PdfValue.Reference(stream), PdfValue.Null, PdfValue.Integer(7)));
            NavigationTests.Put(edit, root, "Names", D(("EmbeddedFiles", D(("Names", PdfValue.Array(T("payload.txt"), PdfValue.Reference(fs)))))));
            NavigationTests.Put(edit, root, "Collection", D(("View", PdfValue.Name("D")), ("D", T("payload.txt"))));
            var annotation = edit.Add(D(("Type", PdfValue.Name("Annot")), ("Subtype", PdfValue.Name("FileAttachment")), ("Rect", PdfGraphicsNumbers.Array(0, 0, 20, 20)), ("FS", PdfValue.Reference(fs))));
            NavigationTests.Put(edit, pages[0], "Annots", PdfValue.Array(PdfValue.Reference(annotation)));
            var keep = edit.Add(T("keep me"));
            var keepAlias = edit.Add(PdfValue.Reference(keep));
            NavigationTests.Put(edit, root, "Keep", PdfValue.Reference(keepAlias));
        }, 1);
        using var edit = new PdfTransaction(source.SourceDocument);
        var rootId = edit.Trailer.Entries["Root"].ReferenceId;
        NavigationTests.Put(edit, rootId, "RevisionTest", PdfValue.Integer(1));
        using var incremental = new MemoryStream();
        edit.Save(incremental, new(PdfWriteMode.Incremental));
        incremental.Position = 0;
        using var revised = Pdf.Open(incremental);
        Assert.Equal(2, revised.SourceDocument!.Revisions.Count);
        using var cleaned = PdfAttachments.RemoveAll(revised.CreateSnapshot());
        var inventory = PdfAttachments.Inspect(cleaned.CreateSnapshot());
        Assert.Empty(inventory.Items);
        Assert.Empty(inventory.EmbeddedStreams);
        Assert.Equal(PdfValueKind.Null, inventory.Collection.Kind);
        Assert.Single(cleaned.SourceDocument!.Revisions);
        using var output = new MemoryStream();
        cleaned.Save(output);
        Assert.DoesNotContain(Encoding.ASCII.GetString(marker), Encoding.ASCII.GetString(output.ToArray()));
        var root = cleaned.SourceDocument.Resolve(cleaned.SourceDocument.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        Assert.False(root.Entries.ContainsKey("AF"));
        Assert.Equal(3, root.Entries["UnusualAlias"].Items.Count);
        Assert.Equal(PdfValueKind.Null, root.Entries["UnusualAlias"].Items[1].Kind);
        Assert.Equal(7, root.Entries["UnusualAlias"].Items[2].NumberValue);
        Assert.Equal(PdfValueKind.Null, root.Entries["UnusualAlias"].Items[0].Kind);
        var kept = cleaned.SourceDocument.Resolve(cleaned.SourceDocument.Resolve(root.Entries["Keep"].ReferenceId).ReferenceId);
        Assert.Equal("keep me", Encoding.BigEndianUnicode.GetString(kept.StringBytes.Skip(2).ToArray()));
        Assert.Empty(cleaned.Pages[0].Source!.Dictionary.Entries["Annots"].Items);
    }

    [Fact]
    public void PortableCollectionsValidateSchemaSortAndTypedSubitems()
    {
        using var source = Bare();
        using var input = new MemoryStream(new byte[] { 1 });
        using var attached = PdfAttachments.Add(source.CreateSnapshot(), input, new("a.txt", "text/plain"));
        var collection = D(("Type", PdfValue.Name("Collection")), ("View", PdfValue.Name("T")), ("D", T("a.txt")), ("Schema", D(("Title", D(("Subtype", PdfValue.Name("S")), ("N", T("Title")))), ("Count", D(("Subtype", PdfValue.Name("N")), ("N", T("Count")))))), ("Sort", D(("S", PdfValue.Name("Count")), ("A", PdfValue.Boolean(false)))));
        using var collected = PdfAttachments.SetCollection(attached.CreateSnapshot(), collection);
        var file = Assert.Single(PdfAttachments.Inspect(collected.CreateSnapshot()).Items);
        using var edited = PdfAttachments.SetCollectionItem(file, D(("Title", D(("D", T("A title")), ("P", T("Prefix: ")))), ("Count", PdfValue.Integer(2))));
        var result = PdfAttachments.Inspect(edited.CreateSnapshot());
        Assert.Equal("T", result.Collection.Entries["View"].NameValue);
        var item = edited.SourceDocument!.Resolve(Assert.Single(result.Items).Specification.Entries["CI"].ReferenceId);
        Assert.Equal(2, item.Entries["Count"].NumberValue);
        Assert.Equal("Attachments.CollectionItemField", Error(() => PdfAttachments.SetCollectionItem(file, D(("Unknown", T("x"))))));
        Assert.Equal("Attachments.CollectionInitial", Error(() => PdfAttachments.SetCollection(attached.CreateSnapshot(), D(("D", T("missing"))))));
        Assert.Equal("Attachments.CollectionOwnership", Error(() => PdfAttachments.SetCollection(attached.CreateSnapshot(), D(("Schema", PdfValue.Reference(file.Id!.Value))))));
        Assert.Equal("Attachments.CollectionUnsupported", Error(() => PdfAttachments.SetCollection(attached.CreateSnapshot(), D(("Unsupported", PdfValue.Integer(1))))));
    }

    [Fact]
    public void CSharpAndVisualBasicConsumersPreserveThePayloadAndCallerOwnership()
    {
        foreach (var write in new Func<Stream, Stream, PdfOperationReport>[]
        {
            ContractExamples.CSharp.ContractExample.WriteAttachmentPdf,
            ContractExamples.VisualBasic.ContractExample.WriteAttachmentPdf
        }

        )
        {
            using var input = new MemoryStream(Encoding.UTF8.GetBytes("{\"value\":42}"));
            using var output = new MemoryStream();
            Assert.True(write(input, output).Succeeded);
            Assert.True(input.CanRead);
            Assert.True(output.CanWrite);
            output.Position = 0;
            using var document = Pdf.Open(output);
            var file = Assert.Single(PdfAttachments.Inspect(document.CreateSnapshot()).Items);
            Assert.Equal("Data", file.Relationship);
            using var extracted = new MemoryStream();
            PdfAttachments.Extract(file, extracted);
            Assert.Equal(input.ToArray(), extracted.ToArray());
        }
    }

    [Fact]
    public void NamesMimeTypesDatesAndAssociatedFileProfilesRejectInvalidConfiguration()
    {
        Assert.Throws<ArgumentException>(() => new PdfAttachmentOptions("a.bin", "text/plain; charset=utf8"));
        Assert.Throws<ArgumentException>(() => new PdfAttachmentOptions("a.bin", "bad/{type}"));
        Assert.Throws<ArgumentException>(() => new PdfAttachmentOptions("a.bin", relationship: "Data"));
        Assert.Throws<ArgumentException>(() => new PdfAttachmentOptions("a.bin", modificationTime: Stamp.AddTicks(1)));
        Assert.Throws<ArgumentException>(() => new PdfAttachmentOptions("a.bin", modificationTime: Stamp, relationship: "EncryptedPayload"));
        Assert.Throws<ArgumentException>(() => new PdfAttachmentOptions(new string ('ա', 121)));
        Assert.Throws<ArgumentException>(() => new PdfAttachmentOptions("bad\ud800"));
    }

    [Fact]
    public void MetadataRemovalRetainsAnUnrelatedIndirectAliasChain()
    {
        using var source = NavigationTests.Fixture((edit, root, _) =>
        {
            var target = edit.Add(PdfValue.Integer(42));
            var alias = edit.Add(PdfValue.Reference(target));
            NavigationTests.Put(edit, root, "KeepAlias", PdfValue.Reference(alias));
        }, 1);
        using var document = PdfMetadata.Remove(source.CreateSnapshot());
        var graph = document.SourceDocument!;
        var root = graph.Resolve(graph.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        var alias = graph.Resolve(root.Entries["KeepAlias"].ReferenceId);
        Assert.Equal(42, graph.Resolve(alias.ReferenceId).NumberValue);
    }

    [Fact]
    public void CompressedPayloadExpansionIsBoundedBeforeOutput()
    {
        // Original fixture: Python zlib level 9 applied to 50,000 zero bytes.
        var encoded = Convert.FromBase64String("eNrtwQENAAAAwqD3T20PBxQAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAABPBsNQAAE=");
        using var source = FileFixture(encoded, D(("Size", PdfValue.Integer(50000))), filter: "FlateDecode");
        var file = Assert.Single(PdfAttachments.Inspect(source.CreateSnapshot()).Items);
        using var output = new MemoryStream();
        Assert.Equal("Limit.ExpandedBytes", Error(() => PdfAttachments.Extract(file, output, policy: new(limits: new(maximumExpandedBytes: 32768)))));
        Assert.Equal(0, output.Length);
        PdfAttachments.Extract(file, output);
        Assert.Equal(new byte[50000], output.ToArray());
    }

    [Fact]
    public void UnknownPayloadFiltersRemainExplicitAndCannotWriteOutput()
    {
        using var source = FileFixture(new byte[] { 1, 2, 3 }, filter: "UnrecognizedFixtureFilter");
        var file = Assert.Single(PdfAttachments.Inspect(source.CreateSnapshot()).Items);
        using var output = new MemoryStream();
        Assert.Equal("Filter.Unsupported", Error(() => PdfAttachments.Extract(file, output)));
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void ExternalSpecificationsArePassiveAndNeverFetched()
    {
        using var source = NavigationTests.Fixture((edit, root, _) => NavigationTests.Put(edit, root, "External", D(("Type", PdfValue.Name("Filespec")), ("FS", PdfValue.Name("URL")), ("F", T("https://invalid.example/payload")))), 1);
        var file = Assert.Single(PdfAttachments.Inspect(source.CreateSnapshot()).Items);
        Assert.Empty(file.Variants);
        using var output = new MemoryStream();
        Assert.Equal("Attachments.VariantRequired", Error(() => PdfAttachments.Extract(file, output)));
        Assert.Equal(0, output.Length);
    }
}
