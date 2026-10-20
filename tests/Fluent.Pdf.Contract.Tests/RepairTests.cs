using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Xml.Linq;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class RepairTests
{
    internal static readonly byte[] Content = Encoding.ASCII.GetBytes("BT /F1 12 Tf 20 40 Td (Original recovered text) Tj ET\n");
    internal static byte[] Fixture(bool damaged = true, int? declaredLength = null, bool index = true, bool freeOrphan = false, bool duplicate = false, bool dangling = false, bool signature = false, bool encrypted = false, bool eof = true, bool impostors = false)
    {
        using var f = new SyntaxFixtures();
        var offsets = new Dictionary<int, long>();
        offsets[1] = f.Object(1, "<< /Type /Catalog /Pages 2 0 R /Original (88 0 obj << /Fake true >> endobj)" + (dangling ? " /Unknown 98 0 R" : "") + " >>");
        offsets[2] = f.Object(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        offsets[3] = f.Object(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 100] /Contents 4 0 R /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> >>");
        byte[] payload = impostors ? Content.Concat(Encoding.ASCII.GetBytes("BT (endstream\nendobj\n99 0 obj\n<< /Fake true >>\nendobj) Tj ET\n")).ToArray() : Content;
        if (declaredLength.HasValue)
        {
            offsets[4] = f.Bytes.Position;
            f.Text("4 0 obj\n<< /Length " + declaredLength.Value + " >>\nstream\n");
            f.Bytes.Write(payload, 0, payload.Length);
            f.Text("\nendstream\nendobj\n");
        }
        else
            offsets[4] = f.StreamObject(4, "", payload);
        offsets[5] = f.Object(5, signature ? "<< /Type /Sig /ByteRange [0 1 2 3] /Contents <00> >>" : "(Original unreachable object)");
        if (duplicate)
            f.Object(1, "<< /Type /Catalog /Pages 2 0 R >>");
        if (index)
        {
            var rows = new List<(int, int, long, bool)>
            {
                (0, 65535, 0, true)
            };
            foreach (var pair in offsets)
                rows.Add((pair.Key, 0, damaged ? 1 : pair.Value, pair.Key == 5 && freeOrphan));
            f.Classic(rows.ToArray(), "/Root 1 0 R" + (encrypted ? " /Encrypt 5 0 R" : ""), 6);
        }
        else
            f.Text("trailer\n<< /Root 1 0 R /Size 6 >>\nstartxref\n0\n%%EOF\n");
        var bytes = f.Bytes.ToArray();
        return eof ? bytes : bytes.Take(bytes.Length - 6).ToArray();
    }

    private static string Hash(byte[] data)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(data)).Replace("-", "").ToLowerInvariant();
    }

    [Fact]
    public void BrokenOffsetsRequireExplicitRecoveryAndKeepExactSourceAndCompleteObjectLedger()
    {
        var bytes = Fixture();
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        Assert.Throws<PdfException>(() => PdfSyntax.Read(input));
        input.Position = 0;
        Assert.Throws<PdfException>(() => PdfStructuralValidation.Inspect(input));
        input.Position = 0;
        var result = PdfRepair.Recover(input, output);
        Retain("damaged-offsets", result);
        Assert.Equal(Hash(bytes), result.OriginalSha256);
        Assert.True(input.CanRead);
        Assert.True(output.CanWrite);
        using var original = result.OpenOriginalStream();
        using var copy = new MemoryStream();
        original.CopyTo(copy);
        Assert.Equal(bytes, copy.ToArray());
        Assert.Equal(5, result.Ledger.Count(r => r.Code == "Repair.ObjectReindexed"));
        Assert.Contains(result.Ledger, r => r.Code == "Repair.OrphanPreserved" && r.ObjectId == new PdfObjectId(5));
        Assert.DoesNotContain(result.Ledger, r => r.ObjectId == new PdfObjectId(88));
        output.Position = 0;
        using var document = Pdf.Open(output);
        Assert.Single(document.Pages);
        using var xml = new MemoryStream();
        result.WriteLedger(xml);
        xml.Position = 0;
        var ledger = XDocument.Load(xml);
        Assert.Equal(result.OriginalSha256, ledger.Root!.Attribute("originalSha256")!.Value);
        Assert.Equal(result.Ledger.Count, ledger.Root.Elements("record").Count());
        Assert.True(xml.CanRead);
    }

    [Fact]
    public void ExplicitLengthOverridesRecoverPayloadsWithoutSearchingTheirFakeObjectMarkers()
    {
        var bytes = Fixture(declaredLength: 1);
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        Assert.Throws<PdfException>(() => PdfRepair.Recover(input, output));
        Assert.Equal(0, output.Length);
        input.Position = 0;
        var result = PdfRepair.Recover(input, output, new(streamLengths: new[] { new PdfRepairStreamLength(new(4), Content.Length) }));
        Retain("exact-stream-length", result);
        Assert.Contains(result.Ledger, r => r.Code == "Repair.StreamLengthOverridden" && r.ObjectId == new PdfObjectId(4));
        output.Position = 0;
        var syntax = PdfSyntax.Read(output);
        using var stream = syntax.Resolve(new(4)).OpenRawStream();
        using var recovered = new MemoryStream();
        stream.CopyTo(recovered);
        Assert.Equal(Content, recovered.ToArray());
        using var impostors = new MemoryStream(Fixture(impostors: true));
        using var second = new MemoryStream();
        var scanned = PdfRepair.Recover(impostors, second);
        Assert.DoesNotContain(scanned.Ledger, r => r.ObjectId == new PdfObjectId(99));
    }

    [Fact]
    public void FreedPhysicalBodiesAreExcludedAndNeverResurrectedBySourcePosition()
    {
        using var input = new MemoryStream(Fixture(freeOrphan: true));
        using var output = new MemoryStream();
        var result = PdfRepair.Recover(input, output);
        Retain("freed-body-excluded", result);
        Assert.Contains(result.Ledger, r => r.Code == "Repair.InactiveBodyExcluded" && r.ObjectId == new PdfObjectId(5));
        output.Position = 0;
        Assert.Equal(PdfValueKind.Null, PdfSyntax.Read(output).Resolve(new(5)).Kind);
    }

    [Fact]
    public void OrphanDiscardRequiresPermissiveExplicitLossAndRecordsEveryDiscard()
    {
        var bytes = Fixture();
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        var options = new PdfRepairOptions(orphans: PdfRepairOrphanPolicy.Discard);
        Assert.Equal("Repair.OrphanDiscarded", Assert.Throws<PdfException>(() => PdfRepair.Recover(input, output, options)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        input.Position = 0;
        var result = PdfRepair.Recover(input, output, options, new(PdfProcessingMode.Permissive));
        Retain("orphan-discard", result);
        Assert.Contains(result.Ledger, r => r.Code == "Repair.OrphanDiscarded" && r.ObjectId == new PdfObjectId(5));
        Assert.Equal(4, result.Ledger.Count(r => r.Code == "Repair.ObjectReindexed"));
        output.Position = 0;
        Assert.Equal(PdfValueKind.Null, PdfSyntax.Read(output).Resolve(new(5)).Kind);
    }

    [Fact]
    public void MissingXrefStatusNeedsCompleteCallerAuthorityRatherThanAutomaticResurrection()
    {
        var bytes = Fixture(index: false);
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        Assert.Equal("Repair.ActiveInventoryRequired", Assert.Throws<PdfException>(() => PdfRepair.Recover(input, output)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        input.Position = 0;
        var result = PdfRepair.Recover(input, output, new(activeObjects: new[] { new PdfObjectId(1), new(2), new(3), new(4) }));
        Retain("explicit-active-inventory", result);
        Assert.Contains(result.Ledger, r => r.Code == "Repair.ActiveInventorySelected");
        Assert.Contains(result.Ledger, r => r.Code == "Repair.InactiveBodyExcluded" && r.ObjectId == new PdfObjectId(5));
    }

    [Theory]
    [InlineData("duplicate", "Repair.AmbiguousObject")]
    [InlineData("encrypted", "Repair.EncryptionProfile")]
    [InlineData("dangling", "Repair.UnresolvedReference")]
    [InlineData("signature", "Repair.SignatureRewrite")]
    public void UnsafeAmbiguityProtectionAndSignatureRewritesFailWithoutOutput(string change, string code)
    {
        using var input = new MemoryStream(Fixture(duplicate: change == "duplicate", encrypted: change == "encrypted", dangling: change == "dangling", signature: change == "signature"));
        using var output = new MemoryStream();
        Assert.Equal(code, Assert.Throws<PdfException>(() => PdfRepair.Recover(input, output)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void MissingEofAndExplicitNullReferenceSemanticsAreRecorded()
    {
        using var input = new MemoryStream(Fixture(eof: false, dangling: true));
        using var output = new MemoryStream();
        var result = PdfRepair.Recover(input, output, new(rejectDanglingReferences: false));
        Retain("missing-eof-null-reference", result);
        Assert.Contains(result.Ledger, r => r.Code == "Repair.EofRestored");
        Assert.Contains(result.Ledger, r => r.Code == "Repair.UnresolvedReference");
        output.Position = 0;
        PdfSyntax.Read(output);
    }

    [Fact]
    public void StrictStructuralInspectionReportsWithoutMutatingOrRepairing()
    {
        var bytes = Fixture(damaged: false, dangling: true);
        using var input = new MemoryStream(bytes);
        var result = PdfStructuralValidation.Inspect(input);
        Assert.Contains(result.Findings, r => r.Code == "Structure.UnresolvedReference");
        Assert.Contains(result.Findings, r => r.Code == "Structure.OrphanObject" && r.ObjectId == new PdfObjectId(5));
        Assert.Equal(Hash(bytes), Hash(Read(result.Syntax.OpenOriginalStream())));
    }

    [Fact]
    public void SharedBudgetsCancellationAndSourceLifetimeRemainExplicit()
    {
        var bytes = Fixture();
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfRepair.Recover(input, output, cancellationToken: canceled.Token));
        Assert.Equal(0, output.Length);
        foreach (var limits in new[]
        {
            new PdfResourceLimits(maximumInputBytes: 1),
            new PdfResourceLimits(maximumExpandedBytes: 64),
            new PdfResourceLimits(maximumOutputBytes: 1),
            new PdfResourceLimits(maximumObjects: 2)
        }

        )
        {
            input.Position = 0;
            Assert.StartsWith("Limit.", Assert.Throws<PdfException>(() => PdfRepair.Recover(input, output, policy: new(limits: limits))).Diagnostic.Code);
            Assert.Equal(0, output.Length);
        }

        input.Position = 0;
        var result = PdfRepair.Recover(input, output);
        input.Dispose();
        output.Dispose();
        Assert.Equal(bytes, Read(result.OpenOriginalStream()));
        Assert.NotEmpty(Read(result.OpenRepairedStream()));
    }

    [Fact]
    public void MissingRootRequiresUniqueActiveCatalogOrAnExplicitSelection()
    {
        var bytes = Encoding.ASCII.GetBytes(Encoding.ASCII.GetString(Fixture()).Replace("/Root 1 0 R", ""));
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();
        var result = PdfRepair.Recover(input, output);
        Assert.Contains(result.Ledger, r => r.Code == "Repair.RootInferred" && r.ObjectId == new PdfObjectId(1));
        Retain("root-inferred", result);
        input.Position = 0;
        using var rejected = new MemoryStream();
        Assert.Equal("Repair.RootAuthority", Assert.Throws<PdfException>(() => PdfRepair.Recover(input, rejected, new(root: new PdfObjectId(3)))).Diagnostic.Code);
        Assert.Equal(0, rejected.Length);
    }

    [Fact]
    public void InvalidExplicitInventoriesAndUnusedOverridesCannotAuthorizeGuessing()
    {
        Assert.Throws<ArgumentException>(() => new PdfRepairOptions(activeObjects: new[] { new PdfObjectId(1), new PdfObjectId(1, 1) }));
        Assert.Throws<ArgumentException>(() => new PdfRepairOptions(streamLengths: new[] { new PdfRepairStreamLength(new(4), 1), new PdfRepairStreamLength(new(4), 2) }));
        using var input = new MemoryStream(Fixture());
        using var output = new MemoryStream();
        Assert.Equal("Repair.MissingSelectedObject", Assert.Throws<PdfException>(() => PdfRepair.Recover(input, output, new(activeObjects: new[] { new PdfObjectId(1, 1) }))).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        input.Position = 0;
        Assert.Equal("Repair.UnusedStreamOverride", Assert.Throws<PdfException>(() => PdfRepair.Recover(input, output, new(streamLengths: new[] { new PdfRepairStreamLength(new(5), 1) }))).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void InactiveSignaturesStillNeedExplicitInvalidationAuthority()
    {
        using var input = new MemoryStream(Fixture(signature: true, freeOrphan: true));
        using var output = new MemoryStream();
        Assert.Equal("Repair.SignatureRewrite", Assert.Throws<PdfException>(() => PdfRepair.Recover(input, output)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
        input.Position = 0;
        var result = PdfRepair.Recover(input, output, new(allowSignatureInvalidation: true));
        Assert.Contains(result.Ledger, r => r.Code == "Repair.SignatureInvalidated");
        Assert.Contains(result.Ledger, r => r.Code == "Repair.InactiveBodyExcluded" && r.ObjectId == new PdfObjectId(5));
    }

    [Theory]
    [InlineData("stream", "Repair.StreamExtent")]
    [InlineData("history", "Repair.HistoryProfile")]
    [InlineData("compressed", "Repair.CompressedIndexProfile")]
    public void UnsupportedUnsafeFramingProfilesFailWithStableDiagnostics(string change, string code)
    {
        var text = Encoding.ASCII.GetString(Fixture());
        if (change == "history")
            text += "trailer << /Root 1 0 R /Size 6 >>\n";
        if (change == "compressed")
            text = text.Replace("<< /Length ", "<< /Type /ObjStm /Length ");
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(text));
        using var output = new MemoryStream();
        var options = change == "stream" ? new PdfRepairOptions(streamLengths: new[] { new PdfRepairStreamLength(new(4), long.MaxValue) }) : new();
        Assert.Equal(code, Assert.Throws<PdfException>(() => PdfRepair.Recover(input, output, options)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void WrongIndexedGenerationsCannotSubstitutePhysicalObjects()
    {
        string text = Encoding.ASCII.GetString(Fixture()).Replace("0000000001 00000 n", "0000000001 00001 n");
        using var input = new MemoryStream(Encoding.ASCII.GetBytes(text));
        using var output = new MemoryStream();
        Assert.Equal("Repair.MissingIndexedObject", Assert.Throws<PdfException>(() => PdfRepair.Recover(input, output)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }

    [Fact]
    public void NullObjectsAreActiveWhileMissingReferencesInsideOrphansAreReported()
    {
        var bytes = Fixture(damaged: false);
        using var source = new MemoryStream(bytes);
        using var edit = new PdfTransaction(PdfSyntax.Read(source));
        edit.Set(new(5), PdfValue.Null);
        var catalog = edit.Resolve(new(1)).Entries.ToDictionary(p => p.Key, p => p.Value);
        catalog["Nullable"] = PdfValue.Reference(new(5));
        edit.Set(new(1), PdfValue.Dictionary(catalog));
        using var output = new MemoryStream();
        edit.Save(output);
        output.Position = 0;
        Assert.DoesNotContain(PdfStructuralValidation.Inspect(output).Findings, f => f.Code == "Structure.UnresolvedReference");
        edit.Set(new(5), NavigationTests.D(("Missing", PdfValue.Reference(new(99)))));
        catalog.Remove("Nullable");
        edit.Set(new(1), PdfValue.Dictionary(catalog));
        using var second = new MemoryStream();
        edit.Save(second);
        second.Position = 0;
        Assert.Contains(PdfStructuralValidation.Inspect(second).Findings, f => f.Code == "Structure.UnresolvedReference" && f.ObjectId == new PdfObjectId(5));
        second.Position = 0;
        using var refused = new MemoryStream();
        Assert.Equal("Repair.UnresolvedReference", Assert.Throws<PdfException>(() => PdfRepair.Recover(second, refused)).Diagnostic.Code);
        Assert.Equal(0, refused.Length);
    }

    [Fact]
    public void CompressedOriginalSyntaxRemainsASeparateExplicitRecoveryProfile()
    {
        using var input = new MemoryStream(SyntaxFixtures.CrossReferenceStream());
        using var output = new MemoryStream();
        Assert.Equal("Repair.CompressedIndexProfile", Assert.Throws<PdfException>(() => PdfRepair.Recover(input, output)).Diagnostic.Code);
        Assert.Equal(0, output.Length);
    }

    private static void Retain(string name, PdfRepairResult result)
    {
        string directory = Path.Combine(ArchivalFixture.Root(), "artifacts", "contracts", "repair", name + "-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        using (var original = result.OpenOriginalStream())
        using (var output = File.Create(Path.Combine(directory, "source.pdf")))
            original.CopyTo(output);
        using (var repaired = result.OpenRepairedStream())
        using (var output = File.Create(Path.Combine(directory, "recovered.pdf")))
            repaired.CopyTo(output);
        using var ledger = File.Create(Path.Combine(directory, "ledger.xml"));
        result.WriteLedger(ledger);
    }

    private static byte[] Read(Stream stream)
    {
        using (stream)
        {
            using var bytes = new MemoryStream();
            stream.CopyTo(bytes);
            return bytes.ToArray();
        }
    }
}
