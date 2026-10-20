using System.IO;
using System.Text;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class CompatibilityTests
{
    [Fact]
    public void BothLanguageConsumersEmitExactUtf8MetadataAndVersion20()
    {
        foreach (var write in new Func<Stream, PdfOperationReport>[]
        {
            ContractExamples.CSharp.ContractExample.WriteUtf8MetadataPdf,
            ContractExamples.VisualBasic.ContractExample.WriteUtf8MetadataPdf
        }

        )
        {
            using var output = new MemoryStream();
            Assert.True(write(output).Succeeded);
            output.Position = 0;
            using var document = Pdf.Open(output);
            Assert.Equal("2.0", PdfCompatibility.Inspect(document.CreateSnapshot()).EffectiveVersion);
            var metadata = PdfMetadata.Inspect(document.CreateSnapshot());
            Assert.Equal("Original title 一😀", metadata.Text["Title"]);
            Assert.Equal(PdfTextStringEncoding.Utf8, PdfTextString.Read(metadata.Information.Entries["Title"]).Encoding);
        }
    }

    private static readonly byte[] Opaque =
    {
        239,
        187,
        191,
        0,
        255,
        254,
        0,
        10,
        13,
        101,
        110,
        100,
        115,
        116,
        114,
        101,
        97,
        109
    };
    private static PdfValue D(params (string, PdfValue)[] pairs) => NavigationTests.D(pairs);
    private static byte[] Save(PdfDocument document, PdfWriteOptions? options = null)
    {
        using var output = new MemoryStream();
        document.Save(output, writeOptions: options);
        Assert.True(output.CanWrite);
        return output.ToArray();
    }

    private static PdfSyntaxDocument Read(byte[] bytes) => PdfSyntax.Read(new MemoryStream(bytes));
    private static PdfValue Catalog(PdfSyntaxDocument syntax) => syntax.Resolve(syntax.Revisions[0].Trailer.Entries["Root"].ReferenceId);
    [Theory]
    [InlineData(PdfTextStringEncoding.PdfDocEncoding, "Résumé € • ﬁ")]
    [InlineData(PdfTextStringEncoding.Utf16BigEndian, "Հայերեն 一😀")]
    [InlineData(PdfTextStringEncoding.Utf8, "Հայերեն 一😀")]
    public void ExplicitEncodingsRetainLogicalUnicodeAndExactBytes(PdfTextStringEncoding encoding, string text)
    {
        var encoded = PdfTextString.Create(text, encoding);
        var decoded = PdfTextString.Read(encoded.Value);
        Assert.Equal(text, decoded.Text);
        Assert.Equal(encoding, decoded.Encoding);
        Assert.Same(encoded.Value, decoded.Value);
        Assert.Equal(encoding == PdfTextStringEncoding.Utf8 ? "2.0" : "1.2", encoded.MinimumVersion);
        Assert.True(encoded.OperationReport.Succeeded);
    }

    [Fact]
    public void EncodingBytesMatchIndependentlySpecifiedMarkersAndLegacyMappings()
    {
        Assert.Equal(new byte[] { 239, 187, 191, 65, 240, 159, 152, 128 }, PdfTextString.Create("A😀", PdfTextStringEncoding.Utf8).Value.StringBytes);
        Assert.Equal(new byte[] { 254, 255, 0, 65, 216, 61, 222, 0 }, PdfTextString.Create("A😀").Value.StringBytes);
        Assert.Equal(new byte[] { 160, 128, 147 }, PdfTextString.Create("€•ﬁ", PdfTextStringEncoding.PdfDocEncoding).Value.StringBytes);
        Assert.Equal("Text.AmbiguousMarker", Assert.Throws<PdfException>(() => PdfTextString.Create("þÿ", PdfTextStringEncoding.PdfDocEncoding)).Diagnostic.Code);
        Assert.Equal("Text.LegacyUnrepresentable", Assert.Throws<PdfException>(() => PdfTextString.Create("一", PdfTextStringEncoding.PdfDocEncoding)).Diagnostic.Code);
    }

    [Theory]
    [InlineData(PdfTextStringEncoding.Utf16BigEndian)]
    [InlineData(PdfTextStringEncoding.Utf8)]
    public void InvalidUnicodeAndMalformedEncodedInputRejectWithoutReplacement(PdfTextStringEncoding encoding)
    {
        Assert.Equal("Text.StringEncoding", Assert.Throws<PdfException>(() => PdfTextString.Create("\ud800", encoding)).Diagnostic.Code);
        var malformed = encoding == PdfTextStringEncoding.Utf8 ? new byte[]
        {
            239,
            187,
            191,
            192,
            128
        }

        : new byte[]
        {
            254,
            255,
            216,
            0
        };
        Assert.Equal("Text.StringEncoding", Assert.Throws<PdfException>(() => PdfTextString.Read(PdfValue.String(malformed))).Diagnostic.Code);
    }

    [Fact]
    public void Utf8MetadataRequiresPdf20AndDoesNotReinterpretArbitraryByteStrings()
    {
        using var source = Pdf.Create().AddPage();
        var text = PdfTextString.Create("Title 一😀", PdfTextStringEncoding.Utf8);
        using var edited = PdfMetadata.Edit(source.CreateSnapshot(), editor => editor.SetInformationText("Title", text));
        var bytes = Save(edited);
        Assert.Equal("2.0", Read(bytes).Version);
        var info = PdfMetadata.Inspect(edited.CreateSnapshot());
        Assert.Equal(text.Value.StringBytes, info.Information.Entries["Title"].StringBytes);
        Assert.Equal(text.Text, info.Text["Title"]);
        using var output = new MemoryStream();
        Assert.Equal("Writer.VersionDowngrade", Assert.Throws<PdfException>(() => edited.Save(output, writeOptions: new(version: "1.7"))).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        Assert.Throws<ArgumentException>(() => PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetInformationText("Trapped", text)));
        Retain("utf8-info", bytes);
    }

    [Theory]
    [InlineData("1.2")]
    [InlineData("1.3")]
    [InlineData("1.4")]
    [InlineData("1.5")]
    [InlineData("1.6")]
    [InlineData("1.7")]
    [InlineData("2.0")]
    public void EachSupportedVersionPreservesOpaqueBinaryAndUnknownFilterStreamsAcrossEdits(string version)
    {
        using var source = Fixture(version);
        using var edited = PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetInformation("Title", "Preserved " + version));
        edited.Pages[0].Size = new PdfPageSize(350, 500);
        foreach (var mode in new[]
        {
            PdfWriteMode.Full,
            PdfWriteMode.Incremental
        }

        )
        {
            var bytes = Save(edited, new(mode));
            var syntax = Read(bytes);
            AssertOpaque(syntax);
            Assert.Equal(version, syntax.Version);
            using var loaded = Pdf.Open(new MemoryStream(bytes));
            var inspection = PdfCompatibility.Inspect(loaded.CreateSnapshot());
            Assert.Equal(version, inspection.EffectiveVersion);
            Retain("opaque-" + version + "-" + mode, bytes);
        }
    }

    [Fact]
    public void ReservedExtensionsTypeIsPreservedAndExcludedFromUnknownVendorPolicy()
    {
        using var source = Fixture("2.0");
        using var transaction = new PdfTransaction(Read(Save(source)));
        using var output = new MemoryStream();
        transaction.Save(output, new(extensions: new[] { new PdfExtensionRequirement("OWN", "2.0", 1, "https://example.invalid/extension", "revision 一") }, unknownExtensions: PdfUnknownExtensionPolicy.Reject));
        var extensions = Catalog(Read(output.ToArray())).Entries["Extensions"];
        Assert.Equal("Extensions", extensions.Entries["Type"].NameValue);
        Assert.Equal(Opaque, extensions.Entries["OWN"].Entries["Opaque"].StringBytes);
        Assert.Equal("https://example.invalid/extension", Encoding.ASCII.GetString(extensions.Entries["OWN"].Entries["URL"].StringBytes.ToArray()));
        Assert.Equal("revision 一", PdfTextString.Read(extensions.Entries["OWN"].Entries["ExtensionRevision"]).Text);
        Retain("developer-extension", output.ToArray());
        Assert.Throws<ArgumentException>(() => new PdfExtensionRequirement("Type", "2.0", 1));
        Assert.Throws<ArgumentException>(() => new PdfExtensionRequirement("OWN", "2.0", 1, "https://example.invalid/é"));
    }

    [Fact]
    public void TypedDocumentationDeclarationNegotiates20AndRejectsExact17()
    {
        using var source = Pdf.Create().AddPage();
        var declaration = new PdfExtensionRequirement("OWN", "1.7", 1, "https://example.invalid/extension");
        Assert.Equal("2.0", Read(Save(source, new(extensions: new[] { declaration }))).Version);
        using var output = new MemoryStream();
        Assert.Equal("Writer.VersionTooLow", Assert.Throws<PdfException>(() => source.Save(output, writeOptions: new(version: "1.7", extensions: new[] { declaration }))).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void IncrementalVersionNegotiationRetainsPhysicalHeaderAndReportsEffective20()
    {
        using var original = Pdf.Create().AddPage();
        byte[] sourceBytes = Save(original, new(version: "1.7"));
        using var source = Pdf.Open(new MemoryStream(sourceBytes));
        var bytes = Save(source, new(PdfWriteMode.Incremental, extensions: new[] { new PdfExtensionRequirement("OWN", "2.0", 1, "https://example.invalid/extension") }));
        Assert.Equal(sourceBytes, bytes.Take(sourceBytes.Length));
        using var loaded = Pdf.Open(new MemoryStream(bytes));
        var inventory = PdfCompatibility.Inspect(loaded.CreateSnapshot());
        Assert.Equal("1.7", inventory.HeaderVersion);
        Assert.Equal("2.0", inventory.EffectiveVersion);
        Retain("incremental-20", bytes);
    }

    [Fact]
    public void Pdf20SecurityEncryptsExtensionStringsAndRetainsUnknownBinaryPayloads()
    {
        using var source = Fixture("2.0");
        using var user = new PdfPassword(Encoding.ASCII.GetBytes("original-user"));
        using var owner = new PdfPassword(Encoding.ASCII.GetBytes("original-owner"));
        using var encrypted = new MemoryStream();
        PdfSecurity.EncryptWithPassword(source.CreateSnapshot(), encrypted, new(user, owner));
        Assert.DoesNotContain("https://example.invalid/extension", Encoding.ASCII.GetString(encrypted.ToArray()));
        encrypted.Position = 0;
        using var plain = new MemoryStream();
        PdfSecurity.DecryptWithPassword(encrypted, plain, owner);
        AssertOpaque(Read(plain.ToArray()));
        Retain("decrypted-20", plain.ToArray());
    }

    [Fact]
    public void InvalidReservedTypeFailsBeforeOutput()
    {
        using var fixture = new SyntaxFixtures("2.0");
        long root = fixture.Object(1, "<< /Type /Catalog /Extensions << /Type /Wrong >> >>");
        fixture.Classic(new[] { (0, 65535, 0L, true), (1, 0, root, false) }, "/Root 1 0 R", size: 2);
        fixture.Bytes.Position = 0;
        using var edit = new PdfTransaction(PdfSyntax.Read(fixture.Bytes));
        using var output = new MemoryStream();
        Assert.Equal("Writer.ExtensionDictionary", Assert.Throws<PdfException>(() => edit.Save(output)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void NamespaceAndAssociatedFileGraphsSurviveMetadataEditsAndUnsafeStructureRemappingRejects()
    {
        using var source = Fixture("2.0");
        using var attachedBytes = new MemoryStream(Opaque);
        using var attached = PdfAttachments.Add(source.CreateSnapshot(), attachedBytes, new("original.bin", modificationTime: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), relationship: "Data"));
        using var edited = PdfMetadata.Edit(attached.CreateSnapshot(), e => e.SetInformation("Author", "Original author"));
        var inspection = PdfCompatibility.Inspect(edited.CreateSnapshot());
        Assert.Single(inspection.StructureNamespaces.Items);
        Assert.Single(inspection.CatalogAssociatedFiles.Items);
        Assert.Equal("2.0", inspection.EffectiveVersion);
        AssertOpaque(Read(Save(edited)));
        Assert.Equal("Organization.StructureProfile", Assert.Throws<PdfException>(() => PdfPageOrganization.Extract(edited.CreateSnapshot(), PdfPageSelection.Parse("all", 1))).Diagnostic.Code);
        Retain("namespace-associated-file", Save(edited));
    }

    [Fact]
    public void InventoryRetainsIncompleteLegacyDeclarationsWithAnExplicitDiagnostic()
    {
        using var source = Pdf.Create().AddPage();
        var bytes = Save(source, new(extensions: new[] { new PdfExtensionRequirement("OWN", "2.0", 1) }));
        using var loaded = Pdf.Open(new MemoryStream(bytes));
        var inventory = PdfCompatibility.Inspect(loaded.CreateSnapshot());
        Assert.Contains(inventory.OperationReport.Diagnostics, d => d.Code == "Compatibility.ExtensionDocumentation");
        loaded.Dispose();
        Assert.Equal(1, inventory.Extensions.Entries["OWN"].Entries["ExtensionLevel"].IntegerValue);
        Assert.Single(inventory.Source.Pages);
    }

    [Fact]
    public void TextAndInventoryApplyCancellationAndSharedBudgets()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfTextString.Create("A", cancellationToken: canceled.Token));
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfTextString.Create("A", policy: new(limits: new(maximumExpandedBytes: 1024)))).Diagnostic.Code);
        using var source = Fixture("2.0");
        Assert.ThrowsAny<OperationCanceledException>(() => PdfCompatibility.Inspect(source.CreateSnapshot(), cancellationToken: canceled.Token));
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => PdfCompatibility.Inspect(source.CreateSnapshot(), new(limits: new(maximumExpandedBytes: 128)))).Diagnostic.Code);
    }

    private static PdfDocument Fixture(string version)
    {
        using var original = Pdf.Create().AddPage();
        using var transaction = new PdfTransaction(Read(Save(original, new(version: version))));
        var root = transaction.Trailer.Entries["Root"].ReferenceId;
        var catalog = transaction.Resolve(root).Entries.ToDictionary(p => p.Key, p => p.Value);
        catalog["OpaqueBinary"] = PdfValue.String(Opaque);
        using var encoded = new MemoryStream(Opaque);
        catalog["OpaqueStream"] = PdfValue.Reference(transaction.Add(PdfValues.Stream(D(("Filter", PdfValue.Name("OWNDecode")), ("OWNParameter", PdfValue.Integer(73))), encoded)));
        if (version == "1.7" || version == "2.0")
        {
            var declaration = new Dictionary<string, PdfValue>
            {
                ["BaseVersion"] = PdfValue.Name(version),
                ["ExtensionLevel"] = PdfValue.Integer(1),
                ["Opaque"] = PdfValue.String(Opaque)
            };
            if (version == "2.0")
                declaration["URL"] = PdfValue.String(Encoding.ASCII.GetBytes("https://example.invalid/extension"));
            catalog["Extensions"] = D(("Type", PdfValue.Name("Extensions")), ("OWN", PdfValue.Dictionary(declaration)));
        }

        if (version == "2.0")
        {
            var ns = transaction.Add(D(("Type", PdfValue.Name("Namespace")), ("NS", PdfValue.String(Encoding.ASCII.GetBytes("urn:original:namespace"))), ("OWNOpaque", PdfValue.String(Opaque))));
            catalog["StructTreeRoot"] = PdfValue.Reference(transaction.Add(D(("Type", PdfValue.Name("StructTreeRoot")), ("K", PdfValue.Array()), ("Namespaces", PdfValue.Array(PdfValue.Reference(ns))))));
        }

        transaction.Set(root, PdfValue.Dictionary(catalog));
        using var output = new MemoryStream();
        transaction.Save(output);
        output.Position = 0;
        return Pdf.Open(output);
    }

    private static void AssertOpaque(PdfSyntaxDocument syntax)
    {
        var catalog = Catalog(syntax);
        Assert.Equal(Opaque, catalog.Entries["OpaqueBinary"].StringBytes);
        var stream = syntax.Resolve(catalog.Entries["OpaqueStream"].ReferenceId);
        Assert.Equal("OWNDecode", stream.Entries["Filter"].NameValue);
        using var raw = stream.OpenRawStream();
        using var bytes = new MemoryStream();
        raw.CopyTo(bytes);
        Assert.Equal(Opaque, bytes.ToArray());
    }

    private static void Retain(string name, byte[] bytes)
    {
        string directory = Path.Combine(ArchivalFixture.Root(), "artifacts", "contracts", "compatibility", name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "output.pdf"), bytes);
    }
}
