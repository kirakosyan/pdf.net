using System.IO;
using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class ProfileConversionTests
{
    internal static PdfColorSpace Icc()
    {
        using var source = File.OpenRead(GraphicsTests.Fixture("original-srgb.icc"));
        return PdfColorSpace.Icc(source);
    }

    internal static byte[] PdfBytes()
    {
        using var doc = Pdf.Create().AddPage(new PdfPageSize(72, 36));
        using var output = new MemoryStream();
        doc.Save(output);
        return output.ToArray();
    }

    private static byte[] Candidate(string condition = "Test print condition")
    {
        using var raw = new MemoryStream(PdfBytes());
        var syntax = PdfSyntax.Read(raw);
        using var edit = new PdfTransaction(syntax);
        var id = edit.Trailer.Entries["Root"].ReferenceId;
        var catalog = syntax.Resolve(id).Entries.ToDictionary(x => x.Key, x => x.Value);
        var profile = edit.Add(Icc().Definition.Items[1]);
        var intent = edit.Add(NavigationTests.D(("Type", PdfValue.Name("OutputIntent")), ("S", PdfValue.Name("GTS_PDFX")), ("OutputConditionIdentifier", PdfValue.String(Encoding.ASCII.GetBytes(condition))), ("DestOutputProfile", PdfValue.Reference(profile))));
        catalog["OutputIntents"] = PdfValue.Array(PdfValue.Reference(intent));
        edit.Set(id, PdfValue.Dictionary(catalog));
        using var output = new MemoryStream();
        edit.Save(output);
        return output.ToArray();
    }

    private sealed class Producer : IPdfProfileProducer
    {
        internal byte[] Bytes = Candidate();
        internal PdfOperationContext? Context;
        internal Stream? Input, Output;
        internal Action<PdfOperationContext>? Before;
        public string EngineId => "fixture.producer";

        public bool Supports(PdfProfileOptions options) => options.Profile == PdfConformanceProfile.X42010;
        public void Convert(Stream source, PdfProfileOptions options, Stream candidate, PdfOperationContext c)
        {
            using var worker = c.EnterScope(PdfResourceKind.Workers);
            Input = source;
            Output = candidate;
            Context = c;
            Before?.Invoke(c);
            candidate.Write(Bytes, 0, Bytes.Length);
        }
    }

    private sealed class Validator : IPdfProfileValidator
    {
        internal bool Complete = true, Compliant = true, WrongBytes, WrongRequest, WrongProfile;
        internal Stream? Lease;
        internal PdfOperationContext? Context;
        public string EngineId => "fixture.binding-only-validator";

        public bool Supports(PdfConformanceProfile profile) => profile == PdfConformanceProfile.X42010;
        // This fixture checks orchestration binding only. It does not implement PDF/X standard validation.
        public PdfProfileValidation Validate(Stream input, PdfProfileOptions options, string request, PdfOperationContext c)
        {
            Lease = input;
            Context = c;
            using var hash = SHA256.Create();
            string digest = BitConverter.ToString(hash.ComputeHash(input)).Replace("-", "").ToLowerInvariant();
            c.Checkpoint();
            return new(WrongProfile ? PdfConformanceProfile.X32003 : options.Profile, WrongBytes ? new string ('0', 64) : digest, WrongRequest ? new string ('0', 64) : request, Complete, Complete && Compliant, Compliant ? Array.Empty<string>() : new[] { "fixture.violation" });
        }
    }

    [Fact]
    public void ExplicitIntentAndIndependentExactByteRequestGateShareOneContextAndExpireLeases()
    {
        using var input = new MemoryStream(PdfBytes());
        using var output = new MemoryStream();
        var producer = new Producer();
        var validator = new Validator();
        var result = PdfProfileConversions.Convert(input, output, new(PdfConformanceProfile.X42010, Icc(), "Test print condition"), producer, validator, new(limits: new(maximumWorkers: 1)));
        Assert.True(result.IsCompliant);
        Assert.True(result.Completed);
        Assert.Same(producer.Context, validator.Context);
        Assert.True(input.CanRead);
        Assert.True(output.CanWrite);
        Assert.Throws<ObjectDisposedException>(() => producer.Input!.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => producer.Output!.WriteByte(0));
        Assert.Throws<ObjectDisposedException>(() => validator.Lease!.ReadByte());
        Assert.Throws<ObjectDisposedException>(() => validator.Context!.Checkpoint());
        Assert.Equal(producer.Bytes, output.ToArray());
    }

    [Theory]
    [InlineData("bytes", "Profile.ValidatorBinding")]
    [InlineData("request", "Profile.ValidatorBinding")]
    [InlineData("profile", "Profile.ValidatorBinding")]
    [InlineData("incomplete", "Profile.IndependentValidation")]
    [InlineData("failed", "Profile.IndependentValidation")]
    [InlineData("intent", "Profile.OutputIntent")]
    public void StaleIncompleteOrWrongOutputCannotBeExposed(string failure, string code)
    {
        using var input = new MemoryStream(PdfBytes());
        using var output = new MemoryStream();
        output.WriteByte(42);
        var producer = new Producer();
        var validator = new Validator();
        switch (failure)
        {
            case "bytes":
                validator.WrongBytes = true;
                break;
            case "request":
                validator.WrongRequest = true;
                break;
            case "profile":
                validator.WrongProfile = true;
                break;
            case "incomplete":
                validator.Complete = false;
                break;
            case "failed":
                validator.Compliant = false;
                break;
            case "intent":
                producer.Bytes = Candidate("Another print condition");
                break;
        }

        Assert.Equal(code, Assert.Throws<PdfException>(() => PdfProfileConversions.Convert(input, output, new(PdfConformanceProfile.X42010, Icc(), "Test print condition"), producer, validator)).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
    }

    [Fact]
    public void MissingPrerequisitesAndSwallowedLimitsOrLossesRemainFatal()
    {
        using var input = new MemoryStream(PdfBytes());
        using var output = new MemoryStream();
        var options = new PdfProfileOptions(PdfConformanceProfile.X42010, Icc(), "Test print condition");
        Assert.Equal("Profile.ProviderUnavailable", Assert.Throws<PdfException>(() => PdfProfileConversions.Convert(input, output, options)).Diagnostic.Code);
        Assert.Equal(0, input.Position);
        var producer = new Producer
        {
            Before = c =>
            {
                try
                {
                    c.RejectLimit(PdfResourceKind.Pixels);
                }
                catch (PdfException)
                {
                }
            }
        };
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => PdfProfileConversions.Convert(input, output, options, producer, new Validator())).Diagnostic.Code);
        input.Position = 0;
        producer.Before = c => c.Report(new("Profile.ColorConversion", "The fixture declares an explicit color conversion loss.", lossCategory: PdfLossCategory.FidelityReduction));
        Assert.Equal("Profile.ColorConversion", Assert.Throws<PdfException>(() => PdfProfileConversions.Convert(input, output, options, producer, new Validator())).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        Assert.Throws<ArgumentException>(() => new PdfProfileOptions(PdfConformanceProfile.X1A2001, Icc(), "Test"));
    }
    [Fact]
    public void ValidationOnlyReturnsAnIncompleteObservationWithoutMutationOrComplianceClaims()
    {
        byte[] bytes = Candidate();
        using var input = new MemoryStream(bytes);
        var result = PdfProfileConversions.Validate(input, new(PdfConformanceProfile.X42010, Icc(), "Test print condition"), new Validator { Complete = false });
        Assert.False(result.Completed);
        Assert.False(result.IsCompliant);
        Assert.Equal(bytes, input.ToArray());
        Assert.True(input.CanRead);
        Assert.NotNull(result.OperationReport);
    }
}
