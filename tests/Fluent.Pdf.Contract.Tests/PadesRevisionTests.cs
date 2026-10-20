using System.IO;
using System.Security.Cryptography;
using Fluent.Pdf.Cryptography;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class PadesRevisionTests
{
    [Fact]
    public void AuditRetainsSignaturesDeletedByALaterUnsignedRevision()
    {
        var signed = SignatureVerificationTests.Signed();
        var providers = new PdfProviderRegistry(new ManagedCmsSignatureProvider());
        var observation = Assert.Single(PdfSignatures.Inspect(signed, providers));
        using var edit = new PdfTransaction(signed);
        edit.Remove(observation.ObjectId);
        using var output = new MemoryStream();
        edit.Save(output, new(PdfWriteMode.Incremental));
        output.Position = 0;
        var changed = PdfSyntax.Read(output);
        Assert.Empty(PdfSignatures.Inspect(changed, providers));
        var audit = PdfSignatures.AuditRevisions(changed, providers);
        var historical = Assert.Single(audit.SelectMany(r => r.Signatures));
        Assert.True(historical.CryptographicallyValid);
        Assert.Equal(observation.SignedRevisionLength, historical.SignedRevisionLength);
        Assert.Contains(audit[0].Changes, c => c.ObjectId.Number == observation.ObjectId.Number && c.Kind == PdfCrossReferenceKind.Free);
        Assert.False(PdfSignatures.PreflightEdit(signed, new()).Allowed);
        Assert.True(PdfSignatures.PreflightEdit(signed, new(PdfWriteMode.Incremental)).PreservesOriginalPrefix);
    }
    [Fact]
    public void CertifiedPreflightRejectsOrdinaryEditsAndLockedSigning()
    {
        using var certificate = new CertificateSecurityTests.Certificate("Preflight certifier");
        using var signer = new ManagedCmsSignatureProvider(certificate.Credential);
        var result = PdfSignatures.Sign(SignatureTests.Prepared(), new("First", certification: PdfCertificationPermission.NoChanges), new(signer));
        using var input = result.OpenPdfStream();
        var source = PdfSyntax.Read(input);
        Assert.False(PdfSignatures.PreflightEdit(source, new(PdfWriteMode.Incremental)).Allowed);
        Assert.False(PdfSignatures.PreflightEdit(source, new(PdfWriteMode.Incremental), new("Second")).Allowed);
    }
    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, false)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, true)]
    public void PadesPublicationRequiresCompleteExactByteValidation(bool complete, bool valid, bool exact, bool succeeds)
    {
        var producer = new GateProducer();
        var validator = new GateValidator(complete, valid, exact);
        var source = SignatureVerificationTests.Signed();
        if (succeeds)
        {
            // A gate fixture tests orchestration only. This is not a PAdES conformance fixture or claim.
            var result = PdfPades.Upgrade(source, PdfPadesProfile.LTA, new(producer, validator));
            Assert.Equal(SignatureTests.Bytes(source.OpenOriginalStream()), SignatureTests.Bytes(result.OpenPdfStream()).Take((int)source.Source.Length));
        }
        else
            Assert.Equal("Pades.Validation", Assert.Throws<PdfException>(() => PdfPades.Upgrade(source, PdfPadesProfile.LTA, new(producer, validator))).Diagnostic.Code);
        Assert.Same(producer.Context, validator.Context);
    }
    [Fact]
    public void PadesRejectsRewrittenPrefixBeforeCallingValidatorAndTimestampParserIsBounded()
    {
        var producer = new GateProducer { Rewrite = true };
        var validator = new GateValidator(true, true, true);
        Assert.Equal("Pades.OriginalPrefix", Assert.Throws<PdfException>(() => PdfPades.Upgrade(SignatureVerificationTests.Signed(), PdfPadesProfile.T, new(producer, validator))).Diagnostic.Code);
        Assert.Null(validator.Context);
        using var certificate = new CertificateSecurityTests.Certificate("Timestamp negative");
        using var input = new MemoryStream(new byte[] { 48, 0 });
        Assert.Equal("Signature.TimestampInvalid", Assert.Throws<PdfException>(() => Rfc3161TimestampVerifier.Verify(input, certificate.Value.RawData, new byte[32], new byte[] { 1 })).Diagnostic.Code);
        Assert.True(input.CanRead);
    }
    private sealed class GateProducer : IPdfPadesProvider
    {
        internal PdfOperationContext? Context; internal bool Rewrite;
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.pades-producer", "1", PdfCapability.Pades);
        public void Upgrade(Stream source, Stream output, PdfPadesProfile profile, PdfOperationContext context)
        {
            Context = context;
            var parsed = PdfSyntax.Read(source);
            using var edit = new PdfTransaction(parsed);
            var root = parsed.Revisions[0].Trailer.Entries["Root"].ReferenceId;
            var d = edit.Resolve(root).Entries.ToDictionary(p => p.Key, p => p.Value);
            d["FixtureRenewal"] = PdfValue.Integer(1);
            edit.Set(root, PdfValue.Dictionary(d));
            using var bytes = new MemoryStream();
            edit.Save(bytes, new(PdfWriteMode.Incremental));
            var raw = bytes.ToArray();
            if (Rewrite)
                raw[10] ^= 1;
            output.Write(raw, 0, raw.Length);
        }
    }
    private sealed class GateValidator : IPdfPadesValidator
    {
        private readonly bool _complete, _valid, _exact; internal PdfOperationContext? Context;
        internal GateValidator(bool complete, bool valid, bool exact)
        {
            _complete = complete;
            _valid = valid;
            _exact = exact;
        }
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.pades-validator", "1", PdfCapability.ValidatePades);
        public PdfPadesValidation Validate(Stream source, PdfPadesProfile profile, PdfOperationContext context)
        {
            Context = context;
            using var hash = SHA256.Create();
            var digest = BitConverter.ToString(hash.ComputeHash(source)).Replace("-", "").ToLowerInvariant();
            return new(_exact ? digest : new string('0', 64), profile, _complete, _valid);
        }
    }
}
