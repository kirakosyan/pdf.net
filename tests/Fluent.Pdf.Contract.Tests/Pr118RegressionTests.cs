using System.Text;
using System.Threading;
using Fluent.Pdf.Fonts;
using Fluent.Pdf.Cryptography;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class Pr118RegressionTests
{
    private static PdfValue D(params (string, PdfValue)[] values) => NavigationTests.D(values);
    private static PdfValue N(string name) => PdfValue.Name(name);
    private static byte[] Save(PdfDocument document)
    {
        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    [Theory]
    [InlineData(PdfCapability.Signatures)]
    [InlineData(PdfCapability.ConvertWord)]
    public void DeclaredServicesRequireTheirInterfacesAtRegistration(PdfCapability capability) =>
        Assert.Throws<ArgumentException>(() => new PdfProviderRegistry(new ServiceProvider(new("fixture.mismatch", "1", capability))));

    [Theory]
    [InlineData("Z")]
    [InlineData("Z00")]
    [InlineData("Z00'00'")]
    public void LegacyUtcDateFormsHaveTheSameInstant(string zone)
    {
        PolicyTests.Run(c => Assert.Equal("2024-01-02T12:00:00Z", PdfMetadataDates.ToXmp("D:20240102120000" + zone, c)));
    }

    [Fact]
    public void LegacyXmpWrapperAndRemovedPacketsDoNotRetainOldPrivateBytes()
    {
        using var blank = Pdf.Create().AddPage();
        string packet = MetadataTests.Packet.Replace("xmpmeta", "xapmeta").Replace("original", "PRIVATE-XMP-REPLACED");
        using var initial = PdfMetadata.Edit(blank.CreateSnapshot(), e => e.SetXmp(packet).SetInformation("Title", "PRIVATE-INFO-REMOVED"));
        Assert.Contains("xapmeta", PdfMetadata.Inspect(initial.CreateSnapshot()).XmpXml!);
        using var clean = PdfMetadata.Edit(initial.CreateSnapshot(), e => e.SetXmp(null).SetInformation("Title", null));
        string bytes = Encoding.ASCII.GetString(Save(clean));
        Assert.DoesNotContain("PRIVATE-XMP-REPLACED", bytes);
        Assert.DoesNotContain("PRIVATE-INFO-REMOVED", bytes);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public void SharedPruningPreservesLongSiblingChainsAndRemovesOrphans(int operation)
    {
        using var source = NavigationTests.Fixture((edit, root, pages) =>
        {
            var chain = Enumerable.Range(0, 300).Select(_ => edit.Add(PdfValue.Null)).ToArray();
            for (int i = 0; i < chain.Length; i++)
                edit.Set(chain[i], i + 1 < chain.Length ? D(("Next", PdfValue.Reference(chain[i + 1]))) : D(("Last", PdfValue.Boolean(true))));
            NavigationTests.Put(edit, root, "OriginalChain", PdfValue.Reference(chain[0]));
            edit.Add(D(("Secret", NavigationTests.T("ORPHAN-PRIVATE-BYTES"))));
        }, 1);
        using var clean = operation == 0 ? PdfMetadata.Edit(source.CreateSnapshot(), e => e.SetInformation("Title", "new")) :
            operation == 1 ? PdfMetadata.Remove(source.CreateSnapshot(), new()) : PdfAttachments.RemoveAll(source.CreateSnapshot());
        Assert.DoesNotContain("ORPHAN-PRIVATE-BYTES", Encoding.ASCII.GetString(Save(clean)));
        PolicyTests.Run(c =>
        {
            using var graph = new PdfGraphEdit(clean.CreateSnapshot(), new(), c);
            var item = graph.Get(graph.Catalog, "OriginalChain");
            for (int i = 1; i < 300; i++) item = graph.Get(item, "Next");
            Assert.True(item.Entries["Last"].BooleanValue);
        });
    }

    [Fact]
    public void ReplacedSourceStreamsCannotRecoverTheOriginalText()
    {
        using var source = ExtractionTests.Fixture("BT /F1 10 Tf 20 100 Td (PRIVATE-REPLACEMENT-TEXT) Tj ET");
        using var replaced = PdfTextSearch.ReplacePage(source.CreateSnapshot(), 0, new("PRIVATE-REPLACEMENT-TEXT"), "AB", new(FontTests.Read("original.ttf")), new(new PdfSimpleTextShaper()), new(10));
        Assert.DoesNotContain("PRIVATE-REPLACEMENT-TEXT", Encoding.ASCII.GetString(Save(replaced)));
        Assert.Equal("AB", PdfTextExtraction.ReadPage(replaced.CreateSnapshot(), 0).Text.Trim());
    }

    [Theory]
    [InlineData(PdfPasswordCipher.Aes128, false)]
    [InlineData(PdfPasswordCipher.Aes128, true)]
    [InlineData(PdfPasswordCipher.Aes256, false)]
    [InlineData(PdfPasswordCipher.Aes256, true)]
    public void OptionalFileLengthsAndByteOrBitFilterLengthsAuthenticate(PdfPasswordCipher cipher, bool bits)
    {
        PolicyTests.Run(c =>
        {
            using var user = new PdfPassword(Encoding.ASCII.GetBytes("user"));
            using var owner = new PdfPassword(Encoding.ASCII.GetBytes("owner"));
            using var original = PdfStandardSecurity.Create(new(user, owner, cipher: cipher), new byte[16], c);
            var entries = original.Dictionary.Entries.ToDictionary(x => x.Key, x => x.Value);
            entries.Remove("Length");
            var filters = entries["CF"].Entries.ToDictionary(x => x.Key, x => x.Value);
            var filter = filters["StdCF"].Entries.ToDictionary(x => x.Key, x => x.Value);
            filter["Length"] = PdfValue.Integer(original.Key.Length * (bits ? 8 : 1));
            filters["StdCF"] = PdfValue.Dictionary(filter);
            entries["CF"] = PdfValue.Dictionary(filters);
            using var authenticated = PdfStandardSecurity.Authenticate(PdfValue.Dictionary(entries), new byte[16], Encoding.ASCII.GetBytes("user"), false, c);
            Assert.Equal(original.Key, authenticated.Key);
            var crypt = new PdfObjectCrypt(authenticated, new(99), false, c);
            var value = PdfValue.String(1, 2, 3);
            var encrypted = crypt.Transform(value, new(10), true, v => v);
            Assert.Equal(value.StringBytes, crypt.Transform(encrypted, new(10), false, v => v).StringBytes);
        });
    }

    [Fact]
    public void SignatureContentsWithoutOptionalTypeAreNeverEncrypted()
    {
        PolicyTests.Run(c =>
        {
            using var user = new PdfPassword(new byte[] { 1 });
            using var owner = new PdfPassword(new byte[] { 2 });
            using var security = PdfStandardSecurity.Create(new(user, owner), new byte[16], c);
            var crypt = new PdfObjectCrypt(security, new(99), false, c);
            var cms = PdfValue.String(1, 2, 3, 4);
            var signature = D(("Filter", N("Adobe.PPKLite")), ("ByteRange", PdfValue.Array(PdfValue.Integer(0), PdfValue.Integer(1), PdfValue.Integer(2), PdfValue.Integer(3))), ("Contents", cms));
            Assert.Same(cms, crypt.Transform(signature, new(10), true, v => v).Entries["Contents"]);
            Assert.Same(cms, crypt.Transform(signature, new(10), false, v => v).Entries["Contents"]);
        });
    }

    [Theory]
    [InlineData(PdfFormDataFormat.Fdf)]
    [InlineData(PdfFormDataFormat.Xfdf)]
    public void PassiveFormInterchangeIdentifiersNeverLoadTheSourceFile(PdfFormDataFormat format)
    {
        string text = format == PdfFormDataFormat.Fdf ? "%FDF-1.2\n1 0 obj << /FDF << /F (missing.pdf) /UF (missing.pdf) /ID [<01><02>] /Fields [<< /T (Name) /V (value) >>] >> >> endobj\ntrailer << /Root 1 0 R >>\n%%EOF" :
            "<xfdf xmlns='http://ns.adobe.com/xfdf/'><f href='https://invalid.example/missing.pdf'/><fields><field name='Name'><value>value</value></field></fields><ids original='01' modified='02'/></xfdf>";
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
        var result = PdfFormData.Read(input, format);
        Assert.Equal("Name", Assert.Single(result.Fields).Name);
        PolicyTests.Run(c => Assert.Equal("value", PdfTextStrings.Decode(result.Fields[0].Value, c)));
    }

    [Fact]
    public void CalculationOrderInconsistencyIsDiagnosticOnlyForPassiveReads()
    {
        using var source = FormTests.Source((edit, root, pages, field) => NavigationTests.Put(edit, field, "AA", D(("C", D(("S", N("JavaScript")), ("JS", NavigationTests.T("passive only")))))));
        var inspection = PdfForms.Inspect(source.CreateSnapshot());
        Assert.Single(inspection.Fields);
        Assert.Contains(inspection.OperationReport.Diagnostics, d => d.Code == "Forms.CalculationOrder");
        Assert.Single(PdfForms.ReadValues(source.CreateSnapshot()).Fields);
        Assert.Equal("Forms.CalculationOrder", Assert.Throws<PdfException>(() => PdfForms.Fill(source.CreateSnapshot(), Array.Empty<PdfFormUpdate>())).Diagnostic.Code);
    }

    [Fact]
    public void CompositeCodesWithoutUnicodeNeverUseTheSimpleByteCache()
    {
        using var source = ExtractionTests.Fixture("BT /F1 10 Tf 20 100 Td <0123> Tj ET", (edit, root, page, font) =>
            edit.Set(font, D(("Type", N("Font")), ("Subtype", N("Type0")), ("BaseFont", N("OriginalCID")), ("Encoding", N("Identity-H")), ("DescendantFonts", PdfValue.Array(D(("Subtype", N("CIDFontType2")), ("DW", PdfValue.Integer(600)), ("FontDescriptor", D(("Ascent", PdfValue.Integer(800)), ("Descent", PdfValue.Integer(-200))))))))), unicode: false);
        var result = PdfTextExtraction.ReadPage(source.CreateSnapshot(), 0, providers: new(ManagedFontProvider.Instance), policy: new(PdfProcessingMode.Permissive));
        Assert.Equal("\ufffd", Assert.Single(result.Characters).Text);
        Assert.Equal((uint)0x123, result.Characters[0].Code);
    }

    [Fact]
    public void UnsupportedFontRunsDoNotAbortOtherTextInPermissiveMode()
    {
        using var source = ExtractionTests.Fixture("BT /Bad 10 Tf 20 100 Td [(omitted) 20 (runs)] TJ /F1 10 Tf 1 0 0 1 20 80 Tm (AB) Tj ET", (edit, root, page, font) =>
        {
            var unsupported = edit.Add(D(("Type", N("Font")), ("Subtype", N("Type0")), ("BaseFont", N("OriginalCID")), ("Encoding", N("UniJIS-UCS2-H"))));
            NavigationTests.Put(edit, page, "Resources", D(("Font", D(("F1", PdfValue.Reference(font)), ("Bad", PdfValue.Reference(unsupported))))));
        });
        var result = PdfTextExtraction.ReadPage(source.CreateSnapshot(), 0, policy: new(PdfProcessingMode.Permissive));
        Assert.Equal("AB", result.Text);
        Assert.Contains(result.OperationReport.Diagnostics, d => d.Code == "Extraction.CidEncodingProfile" && d.LossCategory == PdfLossCategory.OmittedContent);
        Assert.Throws<PdfException>(() => PdfTextExtraction.ReadPage(source.CreateSnapshot(), 0));
    }

    [Theory]
    [InlineData("AT&T R&D")]
    [InlineData("snake_case_name")]
    [InlineData("5 * 3 = 15")]
    [InlineData("5 < 10 ~ 12")]
    public void OrdinaryMarkdownPunctuationRemainsLiteralUnderStrictPolicy(string text)
    {
        using var input = new MemoryStream(Encoding.UTF8.GetBytes(text));
        using var output = PdfInputConversion.Generate(input, PdfInputFormat.Markdown, InputConversionTests.Options(), InputConversionTests.Providers());
        Assert.Equal(text, PdfTextExtraction.ReadPage(output.CreateSnapshot(), 0, providers: InputConversionTests.Providers()).Text);
    }

    [Fact]
    public void TokenAccessDoesNotConsumeTheDocumentBudgetOrCreatePerOperationThreads()
    {
        var contexts = Enumerable.Range(0, 100).Select(_ => new PdfOperationContext("fixture", "fixture", PdfCapability.ReadDocument, new(limits: new(maximumExpandedBytes: 1)), default)).ToArray();
        try
        {
            foreach (var context in contexts)
            {
                var token = context.CancellationToken;
                Assert.Equal(token, context.CancellationToken);
                context.Consume(PdfResourceKind.ExpandedBytes, 1);
                Assert.False(token.IsCancellationRequested);
            }
        }
        finally
        {
            foreach (var context in contexts) { context.Finish(true); context.DisposeSources(); }
        }
    }

    [Fact]
    public void SignatureTimeRejectsFractionalSecondsAtTheOptionBoundary()
    {
        var failure = Assert.Throws<ArgumentException>(() => new PdfSignatureOptions("First", signingTime: new DateTimeOffset(2024, 1, 2, 3, 4, 5, TimeSpan.Zero).AddTicks(1)));
        Assert.Equal("signingTime", failure.ParamName);
    }

    [Fact]
    public void PassiveResourceInventoryDoesNotChargeDecodedPixels()
    {
        using var document = Pdf.Open(new MemoryStream(ResourceExtractionTests.Fixture()));
        var result = PdfResourceExtraction.ReadPage(document.CreateSnapshot(), 0, policy: new(limits: new(maximumPixels: 1)));
        Assert.NotEmpty(result.Resources);
        Assert.Equal(0, result.OperationReport.Resources.Single(x => x.Resource == PdfResourceKind.Pixels).Consumed);
    }

    [Fact]
    public void OcrWordsShareThePageFontSubset()
    {
        using var source = Pdf.Create().AddPage(new PdfPageSize(200, 200));
        var words = Enumerable.Range(0, 100).Select(i => new PdfOcrWord(i % 2 == 0 ? "AB" : "BA", new(2 + i % 10 * 19, 2 + i / 10 * 19, 16, 10), 95, 1, 1, i / 10 + 1, i % 10 + 1)).ToArray();
        var result = new PdfOcrResult(string.Join(" ", words.Select(x => x.Text)), words, "eng", "fixture", "fixture");
        using var searchable = PdfOcrSearchable.Apply(source.CreateSnapshot(), 0, result, new(source.Pages[0].Geometry, 200, 200), new(FontTests.Read("original.ttf")), new(new PdfSimpleTextShaper(), ManagedFontProvider.Instance));
        var resources = PdfResourceExtraction.ReadPage(searchable.CreateSnapshot(), 0);
        Assert.Single(resources.Resources, x => x.Kind == PdfExtractedResourceKind.FontProgram);
        Assert.Equal(200, PdfTextExtraction.ReadPage(searchable.CreateSnapshot(), 0).Characters.Count);
    }
    [Fact]
    public void DenseRuledTablesStayWithinDefaultBudgets()
    {
        var content = new StringBuilder();
        for (int col = 0; col <= 10; col++) content.Append($"{20 + col * 80} 20 m {20 + col * 80} 620 l S ");
        for (int row = 0; row <= 30; row++) content.Append($"20 {20 + row * 20} m 820 {20 + row * 20} l S ");
        for (int row = 0; row < 30; row++)
            for (int col = 0; col < 10; col++) content.Append($"BT /F1 5 Tf 1 0 0 1 {22 + col * 80} {25 + row * 20} Tm (ABCDEFGHIJ) Tj ET ");
        using var source = ExtractionTests.Fixture(content.ToString(), (edit, root, page, font) => NavigationTests.Put(edit, page, "MediaBox", PdfGraphicsNumbers.Array(0, 0, 1000, 1000)));
        var result = PdfTableExtraction.Read(source.CreateSnapshot(), new[] { 0 }, new(PdfTableDetectionMode.Ruled));
        Assert.Equal(300, Assert.Single(result.Tables).Cells.Count);
        Assert.All(result.Tables[0].Cells, cell => Assert.Equal("ABCDEFGHIJ", cell.Text));
    }

    [Fact]
    public void AttachmentRemovalPreservesPositionalSlotsAndDropsAnnotationObjectReferences()
    {
        using var source = NavigationTests.Fixture((edit, root, pages) =>
        {
            using var payload = new MemoryStream(new byte[] { 1, 2, 3 });
            var stream = edit.Add(PdfValues.Stream(D(("Type", N("EmbeddedFile"))), payload));
            var file = edit.Add(D(("Type", N("Filespec")), ("F", NavigationTests.T("fixture.bin")), ("EF", D(("F", PdfValue.Reference(stream))))));
            var annotation = edit.Add(D(("Type", N("Annot")), ("Subtype", N("FileAttachment")), ("FS", PdfValue.Reference(file)), ("Rect", PdfGraphicsNumbers.Array(0, 0, 10, 10))));
            NavigationTests.Put(edit, pages[0], "Annots", PdfValue.Array(PdfValue.Reference(annotation)));
            NavigationTests.Put(edit, root, "OriginalNums", PdfValue.Array(PdfValue.Integer(7), PdfValue.Reference(annotation), PdfValue.Integer(8), PdfValue.Array(PdfValue.Integer(1))));
            NavigationTests.Put(edit, root, "OriginalObjr", D(("Type", N("OBJR")), ("Obj", PdfValue.Reference(annotation))));
        }, 1);
        using var clean = PdfAttachments.RemoveAll(source.CreateSnapshot());
        PolicyTests.Run(c =>
        {
            using var graph = new PdfGraphEdit(clean.CreateSnapshot(), new(), c);
            var numbers = graph.Get(graph.Catalog, "OriginalNums");
            Assert.Equal(4, numbers.Items.Count);
            Assert.Equal(PdfValueKind.Null, numbers.Items[1].Kind);
            Assert.Equal(8, numbers.Items[2].NumberValue);
            Assert.Equal(PdfValueKind.Null, graph.Get(graph.Catalog, "OriginalObjr").Kind);
            Assert.Empty(graph.Get(graph.Edit.Resolve(graph.Snapshot.Pages[0].Source!.ObjectId), "Annots").Items);
        });
    }

    [Theory]
    [InlineData("/Contents\n<")]
    [InlineData(" /Contents<")]
    public void SignatureInspectionAcceptsLegalDictionaryTokenSpacing(string replacement)
    {
        using var certificate = new CertificateSecurityTests.Certificate("Original spacing");
        using var provider = new ManagedCmsSignatureProvider(certificate.Credential);
        var original = PdfSignatures.Sign(SignatureTests.Prepared(), new("First"), new(provider));
        var source = PdfSyntax.Read(original.OpenPdfStream());
        var record = Assert.Single(PdfSignatures.Inspect(source, new(provider)));
        var bytes = SignatureTests.Bytes(original.OpenPdfStream());
        int offset = Encoding.ASCII.GetString(bytes).LastIndexOf("/Contents <", StringComparison.Ordinal);
        Encoding.ASCII.GetBytes(replacement).CopyTo(bytes, offset);
        var ranges = record.ByteRange;
        using var covered = new MemoryStream();
        covered.Write(bytes, 0, checked((int)ranges[1]));
        covered.Write(bytes, checked((int)ranges[2]), checked((int)ranges[3]));
        covered.Position = 0;
        using var cms = new MemoryStream();
        PolicyTests.Run(c => provider.Sign(covered, cms, c));
        var hex = Encoding.ASCII.GetBytes(string.Concat(cms.ToArray().Select(b => b.ToString("X2", System.Globalization.CultureInfo.InvariantCulture))));
        hex.CopyTo(bytes, checked((int)ranges[1] + 1));
        // Clear the old CMS padding before inspecting the exact newly signed bytes.
        for (int i = checked((int)ranges[1] + 1 + hex.Length); i < ranges[2] - 1; i++) bytes[i] = (byte)'0';
        using var input = new MemoryStream(bytes);
        Assert.True(Assert.Single(PdfSignatures.Inspect(PdfSyntax.Read(input), new(provider))).CryptographicallyValid);
    }

    [Fact]
    public void UnsupportedSignatureDoesNotHideASupportedSignedRevision()
    {
        using var certificate = new CertificateSecurityTests.Certificate("Original mixed signatures");
        using var provider = new ManagedCmsSignatureProvider(certificate.Credential);
        var signed = PdfSignatures.Sign(SignatureTests.Prepared(), new("First"), new(provider));
        var source = PdfSyntax.Read(signed.OpenPdfStream());
        using var edit = new PdfTransaction(source);
        var extra = edit.Add(D(("Type", N("DocTimeStamp")), ("SubFilter", N("ETSI.RFC3161")), ("ByteRange", PdfValue.Array(PdfValue.Integer(0), PdfValue.Integer(1), PdfValue.Integer(2), PdfValue.Integer(3))), ("Contents", PdfValue.String(48, 0))));
        var root = source.Revisions[0].Trailer.Entries["Root"].ReferenceId;
        NavigationTests.Put(edit, root, "OriginalTimestamp", PdfValue.Reference(extra));
        using var output = new MemoryStream();
        edit.Save(output, new(mode: PdfWriteMode.Incremental, allowSignatureInvalidation: true));
        output.Position = 0;
        var observations = PdfSignatures.Inspect(PdfSyntax.Read(output), new(provider));
        Assert.Equal(2, observations.Count);
        Assert.Single(observations, x => x.CryptographicallyValid == true);
        var unsupported = Assert.Single(observations, x => x.CryptographicallyValid == null);
        Assert.Equal("Signature.ByteRangeProfile", unsupported.UnsupportedReason);
    }

    private sealed class AttributeSigner : IPdfSignatureProvider
    {
        private readonly ManagedCmsSignatureProvider _inner;
        internal AttributeSigner(ManagedCmsSignatureProvider inner) => _inner = inner;
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.attributes", "1", PdfCapability.Signatures);
        public bool Verify(Stream signedBytes, Stream cms, PdfOperationContext context) => true;
        public void Sign(Stream signedBytes, Stream output, PdfOperationContext context)
        {
            using var cms = new MemoryStream();
            _inner.Sign(signedBytes, cms, context);
            var tag = new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 0, true);
            var reader = new System.Formats.Asn1.AsnReader(cms.ToArray(), System.Formats.Asn1.AsnEncodingRules.DER);
            var outer = reader.ReadSequence();
            string oid = outer.ReadObjectIdentifier();
            var data = outer.ReadSequence(tag).ReadSequence();
            var fields = Enumerable.Range(0, 4).Select(_ => data.ReadEncodedValue()).ToArray();
            var signer = data.ReadSetOf().ReadSequence();
            var prefix = Enumerable.Range(0, 3).Select(_ => signer.ReadEncodedValue()).ToArray();
            var writer = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
            writer.PushSequence(); writer.WriteObjectIdentifier(oid); writer.PushSequence(tag); writer.PushSequence();
            foreach (var field in fields) writer.WriteEncodedValue(field.Span);
            writer.PushSetOf(); writer.PushSequence();
            foreach (var field in prefix) writer.WriteEncodedValue(field.Span);
            writer.PushSetOf(tag);
            writer.PushSequence(); writer.WriteObjectIdentifier("1.2.840.113549.1.9.3"); writer.PushSetOf(); writer.WriteObjectIdentifier("1.2.840.113549.1.7.1"); writer.PopSetOf(); writer.PopSequence();
            writer.PopSetOf(tag);
            while (signer.HasData) writer.WriteEncodedValue(signer.ReadEncodedValue().Span);
            writer.PopSequence(); writer.PopSetOf(); writer.PopSequence(); writer.PopSequence(tag); writer.PopSequence();
            var bytes = writer.Encode(); output.Write(bytes, 0, bytes.Length);
        }
    }

    [Fact]
    public void SignedAttributesAreUnsupportedRatherThanAnInvalidIntegrityVerdict()
    {
        using var certificate = new CertificateSecurityTests.Certificate("Original attributes");
        using var provider = new ManagedCmsSignatureProvider(certificate.Credential);
        // Deliberately unsupported attribute syntax, not evidence of a verified attribute signature.
        var signed = PdfSignatures.Sign(SignatureTests.Prepared(), new("First"), new(new AttributeSigner(provider)));
        var observations = PdfSignatures.Inspect(PdfSyntax.Read(signed.OpenPdfStream()), new(provider));
        var observation = Assert.Single(observations);
        Assert.Null(observation.CryptographicallyValid);
        Assert.Equal("Signature.UnsupportedProfile", observation.UnsupportedReason);
    }
    [Fact]
    public void AnUnrelatedNegativeRecipientSerialDoesNotBlockTheMatchingCredential()
    {
        using var first = new CertificateSecurityTests.Certificate("Original legacy recipient");
        using var second = new CertificateSecurityTests.Certificate("Original valid recipient");
        var provider = new ManagedCmsEnvelopeProvider();
        using var encrypted = new MemoryStream();
        PolicyTests.Run(c => provider.Encrypt(new[] { new PdfCertificateRecipient(first.Value), new PdfCertificateRecipient(second.Value) }, new MemoryStream(Enumerable.Range(0, 24).Select(i => (byte)i).ToArray()), encrypted, c));
        var tag = new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 0, true);
        var input = new System.Formats.Asn1.AsnReader(encrypted.ToArray(), System.Formats.Asn1.AsnEncodingRules.DER);
        var outer = input.ReadSequence();
        string oid = outer.ReadObjectIdentifier();
        var envelope = outer.ReadSequence(tag).ReadSequence();
        var version = envelope.ReadEncodedValue();
        var recipients = envelope.ReadSetOf();
        var writer = new System.Formats.Asn1.AsnWriter(System.Formats.Asn1.AsnEncodingRules.DER);
        writer.PushSequence(); writer.WriteObjectIdentifier(oid); writer.PushSequence(tag); writer.PushSequence(); writer.WriteEncodedValue(version.Span); writer.PushSetOf();
        int changed = 0;
        while (recipients.HasData)
        {
            var recipient = recipients.ReadSequence();
            var recipientVersion = recipient.ReadEncodedValue();
            var sid = recipient.ReadSequence();
            var issuer = sid.ReadEncodedValue();
            var serial = sid.ReadIntegerBytes();
            var expected = first.Value.GetSerialNumber(); Array.Reverse(expected);
            bool mutate = serial.Span.ToArray().SkipWhile(b => b == 0).SequenceEqual(expected.SkipWhile(b => b == 0));
            writer.PushSequence(); writer.WriteEncodedValue(recipientVersion.Span); writer.PushSequence(); writer.WriteEncodedValue(issuer.Span);
            if (mutate) { writer.WriteInteger(-1); changed++; } else writer.WriteInteger(serial.Span);
            writer.PopSequence();
            while (recipient.HasData) writer.WriteEncodedValue(recipient.ReadEncodedValue().Span);
            writer.PopSequence();
        }
        writer.PopSetOf();
        while (envelope.HasData) writer.WriteEncodedValue(envelope.ReadEncodedValue().Span);
        writer.PopSequence(); writer.PopSequence(tag); writer.PopSequence();
        Assert.Equal(1, changed);
        using var malformedRecipient = new MemoryStream(writer.Encode());
        using var decoded = new MemoryStream();
        PolicyTests.Run(c => Assert.True(provider.TryDecrypt(second.Credential, false, malformedRecipient, decoded, c)));
        Assert.Equal(Enumerable.Range(0, 24).Select(i => (byte)i), decoded.ToArray());
    }

    [Fact]
    public void LayerBindingsRejectABareTopLevelVisibilityArray()
    {
        using var source = NavigationTests.Fixture((edit, root, pages) =>
        {
            using var bytes = new MemoryStream(new byte[] { 0 });
            var image = edit.Add(PdfValues.Stream(D(("Type", N("XObject")), ("Subtype", N("Image")), ("Width", PdfValue.Integer(1)), ("Height", PdfValue.Integer(1)), ("BitsPerComponent", PdfValue.Integer(8)), ("ColorSpace", N("DeviceGray"))), bytes));
            NavigationTests.Put(edit, pages[0], "Resources", D(("XObject", D(("Im1", PdfValue.Reference(image))))));
        }, 1);
        var failure = Assert.Throws<PdfException>(() => PdfLayers.BindXObject(source.CreateSnapshot(), 0, "Im1", PdfValue.Array(N("And"))));
        Assert.Equal("Layers.MembershipType", failure.Diagnostic.Code);
    }
}
