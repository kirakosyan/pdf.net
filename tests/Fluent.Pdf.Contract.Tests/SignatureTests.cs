using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using Fluent.Pdf.Cryptography;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class SignatureTests
{
    internal static PdfSyntaxDocument Prepared(bool visible = false)
    {
        using var blank = Pdf.Create().AddPage(new PdfPageSize(200, 100));
        using var prepared = PdfSignatures.PrepareFields(blank.CreateSnapshot(), new[] { new PdfSignatureFieldDefinition("First", bounds: visible ? new(10, 20, 80, 30) : default, appearance: visible ? AnnotationTests.Appearance(new(0, 0, 80, 30)) : null), new PdfSignatureFieldDefinition("Second"), new PdfSignatureFieldDefinition("Third") });
        using var bytes = new MemoryStream();
        prepared.Save(bytes);
        bytes.Position = 0;
        return PdfSyntax.Read(bytes);
    }

    internal static byte[] Bytes(Stream stream)
    {
        using (stream)
        {
            using var result = new MemoryStream();
            stream.CopyTo(result);
            return result.ToArray();
        }
    }

    private static PdfSyntaxDocument Parsed(PdfSignedResult result)
    {
        using var input = result.OpenPdfStream();
        return PdfSyntax.Read(input);
    }

    [Fact]
    public void DetachedCmsBindsExactOriginalPrefixAndTwoRangesWithUnicodeReasonAndExplicitTime()
    {
        using var certificate = new CertificateSecurityTests.Certificate("Original signature");
        using var provider = new ManagedCmsSignatureProvider(certificate.Credential);
        var source = Prepared(true);
        var original = Bytes(source.OpenOriginalStream());
        var signed = PdfSignatures.Sign(source, new("First", reason: "Original – π", signingTime: new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero)), new(provider));
        var bytes = Bytes(signed.OpenPdfStream());
        Assert.Equal(original, bytes.Take(original.Length));
        var parsed = Parsed(signed);
        Assert.Equal(2, parsed.Revisions.Count);
        var record = Assert.Single(PdfSignatures.Inspect(parsed, new(provider)));
        Assert.True(record.CryptographicallyValid);
        Assert.False(record.TrustEvaluated);
        Assert.Equal(bytes.LongLength, record.SignedRevisionLength);
        Assert.Equal(0, record.ByteRange[0]);
        Assert.True(record.ByteRange[1] < record.ByteRange[2]);
        var signature = parsed.Resolve(record.ObjectId);
        PolicyTests.Run(c => Assert.Equal("Original – π", PdfTextStrings.Decode(signature.Entries["Reason"], c)));
        Assert.Equal("SignPdf", signed.OperationReport.Operation);
        using var copy = new MemoryStream();
        signed.Save(copy);
        Assert.Equal(bytes, copy.ToArray());
        Assert.True(copy.CanWrite);
        Assert.True(certificate.Key.SignHash(new byte[32], HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1).Length > 0);
    }

    [Fact]
    public void MultipleSignaturesPreserveEveryEarlierSignedByteAndReportTheirSeparateRevisionExtents()
    {
        using var first = new CertificateSecurityTests.Certificate("First original");
        using var second = new CertificateSecurityTests.Certificate("Second original");
        using var p1 = new ManagedCmsSignatureProvider(first.Credential);
        using var p2 = new ManagedCmsSignatureProvider(second.Credential);
        var a = PdfSignatures.Sign(Prepared(), new("First"), new(p1));
        var b = PdfSignatures.Sign(Parsed(a), new("Second"), new(p2));
        var before = Bytes(a.OpenPdfStream());
        var after = Bytes(b.OpenPdfStream());
        Assert.Equal(before, after.Take(before.Length));
        var records = PdfSignatures.Inspect(Parsed(b), new(new ManagedCmsSignatureProvider()));
        Assert.Equal(2, records.Count);
        Assert.All(records, r => Assert.True(r.CryptographicallyValid));
        Assert.Contains(records, r => r.SignedRevisionLength == before.Length);
        Assert.Contains(records, r => r.SignedRevisionLength == after.Length);
    }

    [Theory]
    [InlineData(PdfCertificationPermission.FormsAndSignatures)]
    [InlineData(PdfCertificationPermission.FormsSignaturesAndAnnotations)]
    public void CertificationAllowsOnlyPrecreatedUnlockedSigningAndGenericEditsReject(PdfCertificationPermission permission)
    {
        using var certificate = new CertificateSecurityTests.Certificate("Original certifier");
        using var provider = new ManagedCmsSignatureProvider(certificate.Credential);
        var first = PdfSignatures.Sign(Prepared(), new("First", certification: permission, fieldLock: new(PdfSignatureLockAction.Include, "Third")), new(provider));
        var parsed = Parsed(first);
        var second = PdfSignatures.Sign(parsed, new("Second"), new(provider));
        Assert.All(PdfSignatures.Inspect(Parsed(second), new(provider)), r => Assert.True(r.CryptographicallyValid));
        Assert.Equal("Signature.CertificationPermission", Assert.Throws<PdfException>(() => PdfSignatures.Sign(parsed, new("Third"), new(provider))).Diagnostic.Code);
        using var edit = new PdfTransaction(parsed);
        var root = parsed.Revisions[0].Trailer.Entries["Root"].ReferenceId;
        var d = edit.Resolve(root).Entries.ToDictionary(p => p.Key, p => p.Value);
        d["PageMode"] = PdfValue.Name("UseOutlines");
        edit.Set(root, PdfValue.Dictionary(d));
        using var sink = new MemoryStream();
        Assert.Equal("Signature.CertifiedEditProfile", Assert.Throws<PdfException>(() => edit.Save(sink, new(PdfWriteMode.Incremental))).Diagnostic.Code);
        Assert.Equal(0, sink.Length);
        using var unchanged = new PdfTransaction(parsed);
        using var copy = new MemoryStream();
        unchanged.Save(copy, new(PdfWriteMode.Incremental));
        Assert.Equal(Bytes(first.OpenPdfStream()), copy.ToArray());
    }

    [Theory]
    [InlineData(PdfSignatureLockAction.All, true)]
    [InlineData(PdfSignatureLockAction.Include, false)]
    [InlineData(PdfSignatureLockAction.Exclude, true)]
    public void FieldMdpSelectionsApplyOrdinalNamesAndRemainAuthoritative(PdfSignatureLockAction action, bool blocked)
    {
        using var certificate = new CertificateSecurityTests.Certificate("Original lock");
        using var provider = new ManagedCmsSignatureProvider(certificate.Credential);
        var fieldLock = action == PdfSignatureLockAction.All ? new PdfSignatureFieldLock(action) : new(action, "Third");
        var first = PdfSignatures.Sign(Prepared(), new("First", fieldLock: fieldLock), new(provider));
        if (blocked)
            Assert.Equal("Signature.CertificationPermission", Assert.Throws<PdfException>(() => PdfSignatures.Sign(Parsed(first), new("Second"), new(provider))).Diagnostic.Code);
        else
            Assert.Equal(2, PdfSignatures.Inspect(Parsed(PdfSignatures.Sign(Parsed(first), new("Second"), new(provider))), new(provider)).Count);
    }

    [Fact]
    public void NoChangesCertificationAndLateCertificationRejectBeforeCallingTheSigner()
    {
        using var certificate = new CertificateSecurityTests.Certificate("Original final");
        using var provider = new ManagedCmsSignatureProvider(certificate.Credential);
        var source = Prepared();
        var first = PdfSignatures.Sign(source, new("First", certification: PdfCertificationPermission.NoChanges), new(provider));
        var observer = new FaultProvider();
        Assert.Equal("Signature.CertificationPermission", Assert.Throws<PdfException>(() => PdfSignatures.Sign(Parsed(first), new("Second"), new(observer))).Diagnostic.Code);
        Assert.Equal(0, observer.Calls);
        Assert.Equal("Signature.CertificationOrder", Assert.Throws<PdfException>(() => PdfSignatures.Sign(Parsed(first), new("Second", certification: PdfCertificationPermission.NoChanges), new(observer))).Diagnostic.Code);
        using var document = Pdf.Open(first.OpenPdfStream());
        Assert.Equal("Signature.PreparationAfterSigning", Assert.Throws<PdfException>(() => PdfSignatures.PrepareFields(document.CreateSnapshot(), new[] { new PdfSignatureFieldDefinition("Late") })).Diagnostic.Code);
    }

    [Fact]
    public void CoveredByteTamperingFailsIntegrityWithoutClaimingTrustOrAcceptingWrongRanges()
    {
        using var certificate = new CertificateSecurityTests.Certificate("Original tamper");
        using var provider = new ManagedCmsSignatureProvider(certificate.Credential);
        var result = PdfSignatures.Sign(Prepared(), new("First", reason: "Original"), new(provider));
        var raw = Bytes(result.OpenPdfStream());
        raw[8] = (byte)(raw[8] == 10 ? 32 : 10);
        using var changed = new MemoryStream(raw);
        var modified = PdfSyntax.Read(changed);
        Assert.False(Assert.Single(PdfSignatures.Inspect(modified, new(provider))).CryptographicallyValid);
        var good = Parsed(result);
        var record = Assert.Single(PdfSignatures.Inspect(good, new(provider)));
        using var edit = new PdfTransaction(good);
        var d = good.Resolve(record.ObjectId).Entries.ToDictionary(p => p.Key, p => p.Value);
        d["ByteRange"] = PdfValue.Array(PdfValue.Integer(0), PdfValue.Integer(1), PdfValue.Integer(2), PdfValue.Integer(3));
        edit.Set(record.ObjectId, PdfValue.Dictionary(d));
        using var bad = new MemoryStream();
        edit.Save(bad, new(PdfWriteMode.Incremental));
        bad.Position = 0;
        Assert.Equal("Signature.ByteRange", Assert.Throws<PdfException>(() => PdfSignatures.Inspect(PdfSyntax.Read(bad), new(provider))).Diagnostic.Code);
    }

    private sealed class FaultProvider : IPdfSignatureProvider
    {
        internal int Calls;
        internal bool HideLimit, ReturnFalse, Malformed;
        internal int Size = 2;
        internal PdfOperationContext? Seen;
        public PdfProviderDescriptor Descriptor { get; } = new("own.signature-fault", "1", PdfCapability.Signatures);

        public void Sign(Stream input, Stream output, PdfOperationContext c)
        {
            Calls++;
            Seen = c;
            if (HideLimit)
            {
                try
                {
                    c.RejectLimit(PdfResourceKind.Objects);
                }
                catch (PdfException)
                {
                }
            }

            var bytes = new byte[Size];
            if (!Malformed)
            {
                bytes[0] = 48;
                bytes[1] = 0;
            }

            output.Write(bytes, 0, bytes.Length);
        }

        public bool Verify(Stream input, Stream cms, PdfOperationContext c)
        {
            Seen = c;
            return !ReturnFalse;
        }
    }

    [Fact]
    public void MissingOrAlreadySignedFieldsRejectAndNoProviderFallbackOccurs()
    {
        var source = Prepared();
        Assert.Throws<PdfProviderUnavailableException>(() => PdfSignatures.Sign(source, new("First"), PdfProviderRegistry.Empty));
        var observer = new FaultProvider();
        Assert.Equal("Signature.FieldMissing", Assert.Throws<PdfException>(() => PdfSignatures.Sign(source, new("Absent"), new(observer))).Diagnostic.Code);
        Assert.Equal(0, observer.Calls);
        using var certificate = new CertificateSecurityTests.Certificate("Original repeat");
        using var provider = new ManagedCmsSignatureProvider(certificate.Credential);
        var result = PdfSignatures.Sign(source, new("First"), new(provider));
        Assert.Equal("Signature.FieldIdentity", Assert.Throws<PdfException>(() => PdfSignatures.Sign(Parsed(result), new("First"), new(observer))).Diagnostic.Code);
    }

    [Fact]
    public void OversizedMalformedAndUnverifiableSignaturesNeverReplaceTheDestination()
    {
        var path = Path.Combine(Path.GetTempPath(), "signature-original-" + Guid.NewGuid().ToString("N") + ".pdf");
        File.WriteAllText(path, "existing-original-destination");
        try
        {
            foreach (var provider in new[]
            {
                new FaultProvider
                {
                    Size = 1025
                },
                new FaultProvider
                {
                    Malformed = true
                },
                new FaultProvider
                {
                    ReturnFalse = true
                },
                new FaultProvider
                {
                    HideLimit = true
                }
            }

            )
            {
                Assert.Throws<PdfException>(() => PdfSignatures.SignFile(Prepared(), new("First", 1024), new(provider), path));
                Assert.Equal("existing-original-destination", File.ReadAllText(path));
                Assert.Equal(1, provider.Calls);
                Assert.Throws<ObjectDisposedException>(() => provider.Seen!.Checkpoint());
            }

            using var cancel = new CancellationTokenSource();
            cancel.Cancel();
            Assert.ThrowsAny<OperationCanceledException>(() => PdfSignatures.SignFile(Prepared(), new("First"), new(new FaultProvider()), path, cancellationToken: cancel.Token));
            Assert.Equal("existing-original-destination", File.ReadAllText(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Pkcs12ImportUsesEphemeralOwnedKeysWrongPasswordsRejectAndCallerStreamsStayOpen()
    {
        using var certificate = new CertificateSecurityTests.Certificate("Original PKCS12");
        var bytes = certificate.Value.Export(X509ContentType.Pkcs12, "owned-password");
        using var input = new MemoryStream(bytes);
        using var provider = ManagedCmsSignatureProvider.FromPkcs12(input, "owned-password");
        Assert.True(input.CanRead);
        Assert.NotNull(provider.ImportReport);
        var signed = PdfSignatures.Sign(Prepared(), new("First"), new(provider));
        Assert.True(Assert.Single(PdfSignatures.Inspect(Parsed(signed), new(provider))).CryptographicallyValid);
        input.Position = 0;
        Assert.Equal("Signature.Pkcs12Import", Assert.Throws<PdfException>(() => ManagedCmsSignatureProvider.FromPkcs12(input, "wrong-password")).Diagnostic.Code);
        provider.Dispose();
        Assert.Throws<ObjectDisposedException>(() => PdfSignatures.Sign(Prepared(), new("First"), new(provider)));
        Assert.True(certificate.Key.KeySize >= 2048);
    }

    [Fact]
    public void ExactSignedCopiesRejectOldPrefixesAndFiniteBudgetsBeforePublication()
    {
        using var certificate = new CertificateSecurityTests.Certificate("Original extents");
        using var provider = new ManagedCmsSignatureProvider(certificate.Credential);
        var source = Prepared();
        var signed = PdfSignatures.Sign(source, new("First"), new(provider));
        using var sink = new MemoryStream();
        sink.WriteByte(42);
        Assert.Equal("Signature.OutputExtent", Assert.Throws<PdfException>(() => signed.Save(sink)).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, sink.ToArray());
        Assert.Throws<PdfException>(() => PdfSignatures.Sign(source, new("First"), new(provider), new(limits: new(maximumExpandedBytes: 1024))));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfSignatureOptions("First", 1023));
        Assert.Throws<ArgumentException>(() => new PdfSignatureFieldDefinition("Nested.Name"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CmsIssuerSerialMatchesTheUnsignedIntegerWithOrWithoutADerSignPaddingByte(bool highBit)
    {
        using var owner = new CertificateSecurityTests.Certificate("Original serial owner");
        var request = new CertificateRequest("CN=Original Serial", owner.Key, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        using var certificate = request.Create(new X500DistinguishedName("CN=Original Serial"), X509SignatureGenerator.CreateForRSA(owner.Key, RSASignaturePadding.Pkcs1), new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero), new DateTimeOffset(2035, 1, 1, 0, 0, 0, TimeSpan.Zero), new byte[] { highBit ? (byte)128 : (byte)127, 1 });
        using var provider = new ManagedCmsSignatureProvider(new PdfCertificateCredential(certificate, owner.Key));
        var result = PdfSignatures.Sign(Prepared(), new("First"), new(provider));
        Assert.True(Assert.Single(PdfSignatures.Inspect(Parsed(result), new(provider))).CryptographicallyValid);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExecutableLanguageConsumersPreserveExactSignedBytes(bool visualBasic)
    {
        using var certificate = new CertificateSecurityTests.Certificate("Original language signer");
        using var provider = new ManagedCmsSignatureProvider(certificate.Credential);
        var source = Prepared();
        var registry = new PdfProviderRegistry(provider);
        var result = visualBasic ? ContractExamples.VisualBasic.ContractExample.SignPdf(source, "First", registry) : ContractExamples.CSharp.ContractExample.SignPdf(source, "First", registry);
        Assert.True(Assert.Single(PdfSignatures.Inspect(Parsed(result), registry)).CryptographicallyValid);
        Assert.Equal(Bytes(source.OpenOriginalStream()), Bytes(result.OpenPdfStream()).Take((int)source.Source.Length));
    }

    [Fact]
    public async System.Threading.Tasks.Task IndependentSigningJobsRetainSeparateKeysSourcesAndOutputs()
    {
        var jobs = Enumerable.Range(0, 8).Select(i => System.Threading.Tasks.Task.Run(() =>
        {
            using var certificate = new CertificateSecurityTests.Certificate("Original job " + i);
            using var provider = new ManagedCmsSignatureProvider(certificate.Credential);
            var result = PdfSignatures.Sign(Prepared(), new("First", reason: "Original job " + i), new(provider));
            Assert.True(Assert.Single(PdfSignatures.Inspect(Parsed(result), new(provider))).CryptographicallyValid);
            using var hash = SHA256.Create();
            return Convert.ToBase64String(hash.ComputeHash(Bytes(result.OpenPdfStream())));
        })).ToArray();
        var results = await System.Threading.Tasks.Task.WhenAll(jobs);
        Assert.Equal(8, results.Distinct().Count());
    }

    [Fact]
    public void AProviderCannotHideALatchedFailureDuringSelfVerification()
    {
        var observer = new VerifyFailureProvider();
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfSignatures.Sign(Prepared(), new("First"), new(observer))).Diagnostic.Code);
        Assert.NotNull(observer.SignContext);
        Assert.Same(observer.SignContext, observer.VerifyContext);
        Assert.Throws<ObjectDisposedException>(() => observer.SignContext!.Checkpoint());
    }

    private sealed class VerifyFailureProvider : IPdfSignatureProvider
    {
        internal PdfOperationContext? SignContext, VerifyContext;
        public PdfProviderDescriptor Descriptor { get; } = new("own.verify-latched-failure", "1", PdfCapability.Signatures);

        public void Sign(Stream input, Stream output, PdfOperationContext c)
        {
            SignContext = c;
            output.Write(new byte[] { 48, 0 }, 0, 2);
        }

        public bool Verify(Stream input, Stream cms, PdfOperationContext c)
        {
            VerifyContext = c;
            try
            {
                c.RejectLimit(PdfResourceKind.Objects);
            }
            catch (PdfException)
            {
            }

            return true;
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DirectOrMalformedCatalogCertificationCannotBypassTheGenericWriter(bool malformed)
    {
        var source = Prepared();
        using var edit = new PdfTransaction(source);
        var root = source.Revisions[0].Trailer.Entries["Root"].ReferenceId;
        var signature = NavigationTests.D(("Type", PdfValue.Name("Sig")), ("ByteRange", PdfValue.Array(PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(0), PdfValue.Integer(0))), ("Contents", PdfValue.String(new byte[] { 48, 0 })), ("Reference", PdfValue.Array(NavigationTests.D(("TransformMethod", PdfValue.Name("DocMDP")), ("TransformParams", NavigationTests.D(("P", PdfValue.Integer(1))))))));
        var catalog = edit.Resolve(root).Entries.ToDictionary(p => p.Key, p => p.Value);
        catalog["Perms"] = NavigationTests.D(("DocMDP", malformed ? PdfValue.Name("Malformed") : signature));
        edit.Set(root, PdfValue.Dictionary(catalog));
        using var fixture = new MemoryStream();
        // A deliberately unverified structural-policy fixture, never a claimed valid CMS.
        edit.Save(fixture, new(allowSignatureInvalidation: true));
        fixture.Position = 0;
        var parsed = PdfSyntax.Read(fixture);
        using var change = new PdfTransaction(parsed);
        var changed = change.Resolve(root).Entries.ToDictionary(p => p.Key, p => p.Value);
        changed["PageMode"] = PdfValue.Name("UseOutlines");
        change.Set(root, PdfValue.Dictionary(changed));
        using var output = new MemoryStream();
        Assert.Equal(malformed ? "Signature.TransformProfile" : "Signature.CertifiedEditProfile", Assert.Throws<PdfException>(() => change.Save(output, new(PdfWriteMode.Incremental))).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        var observer = new FaultProvider();
        Assert.Equal(malformed ? "Signature.TransformProfile" : "Signature.CertificationPermission", Assert.Throws<PdfException>(() => PdfSignatures.Sign(parsed, new("First"), new(observer))).Diagnostic.Code);
        Assert.Equal(0, observer.Calls);
    }

    [Fact]
    public void PreparingOrSigningFieldsCannotSilentlyDisableExistingAppearanceRegeneration()
    {
        var source = Prepared();
        using var edit = new PdfTransaction(source);
        var root = source.Resolve(source.Revisions[0].Trailer.Entries["Root"].ReferenceId);
        var form = root.Entries["AcroForm"].ReferenceId;
        var dictionary = source.Resolve(form).Entries.ToDictionary(p => p.Key, p => p.Value);
        dictionary["NeedAppearances"] = PdfValue.Boolean(true);
        edit.Set(form, PdfValue.Dictionary(dictionary));
        using var bytes = new MemoryStream();
        edit.Save(bytes);
        bytes.Position = 0;
        using var document = Pdf.Open(bytes);
        Assert.Equal("Signature.FormProfile", Assert.Throws<PdfException>(() => PdfSignatures.PrepareFields(document.CreateSnapshot(), new[] { new PdfSignatureFieldDefinition("Fourth") })).Diagnostic.Code);
        var observer = new FaultProvider();
        Assert.Equal("Signature.FormProfile", Assert.Throws<PdfException>(() => PdfSignatures.Sign(document.SourceDocument!, new("First"), new(observer))).Diagnostic.Code);
        Assert.Equal(0, observer.Calls);
        Assert.True(document.SourceDocument!.Resolve(form).Entries["NeedAppearances"].BooleanValue);
    }
}
