using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class PreflightTests
{
    private static byte[] Fixture()
    {
        using var f = new SyntaxFixtures();
        var entries = new List<(int, int, long, bool)>
        {
            (0, 65535, 0, true)
        };
        void Obj(int id, string body) => entries.Add((id, 0, f.Object(id, body), false));
        Obj(1, "<< /Type /Catalog /Pages 2 0 R /OpenAction 4 0 R >>");
        Obj(2, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
        Obj(3, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 72 36] /Resources <<>> >>");
        Obj(4, "<< /Type /Action /S /JavaScript /JS (original passive fixture; never executed) >>");
        Obj(5, "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");
        entries.Add((6, 0, f.StreamObject(6, "/Type /XObject /Subtype /Image /Width 0 /Height 1 /BitsPerComponent 8 /ColorSpace /DeviceRGB", Array.Empty<byte>()), false));
        Obj(7, "<< /Type /Action /S /Launch /F (not-run) >>");
        f.Classic(entries.ToArray(), "/Root 1 0 R", 8);
        return f.Bytes.ToArray();
    }

    private static string Hash(byte[] bytes)
    {
        using var hash = SHA256.Create();
        return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }

    [Fact]
    public void PassiveReportContainsStableFaultsProvenanceAndOnlyExplicitCapabilitiesWithoutMutation()
    {
        byte[] bytes = Fixture();
        using var source = new MemoryStream(bytes);
        var report = PdfPreflight.Inspect(source, PdfProviderRegistry.Default);
        Assert.Equal(bytes, source.ToArray());
        Assert.Equal(Hash(bytes), report.SourceSha256);
        Assert.False(report.ConformanceValidated);
        Assert.Contains(PdfCapability.ReadDocument, report.AvailableCapabilities);
        Assert.Contains(report.Findings, f => f.Code == "Security.BlockedAction" && f.RemediationCode == "RemoveBlockedActions");
        Assert.Contains(report.Findings, f => f.Code == "Font.NotEmbedded" && f.Location == "5 0 obj");
        Assert.Contains(report.Findings, f => f.Code == "Image.InvalidGeometry" && f.Location == "6 0 obj");
        using var json = new MemoryStream();
        PdfPreflight.WriteReport(report, json);
        using var document = JsonDocument.Parse(json.ToArray());
        Assert.Equal(1, document.RootElement.GetProperty("schema").GetInt32());
        Assert.False(document.RootElement.GetProperty("conformanceValidated").GetBoolean());
        Assert.Equal(Hash(bytes), document.RootElement.GetProperty("report").GetProperty("sourceSha256").GetString());
    }

    [Fact]
    public void ExplicitFixIsBoundToSourceAndPostChecksPreserveUnresolvedFindingsAndAuditHashes()
    {
        byte[] bytes = Fixture();
        using var source = new MemoryStream(bytes);
        var report = PdfPreflight.Inspect(source);
        var plan = new PdfRemediationPlan(report, new[] { "RemoveBlockedActions" });
        source.Position = 0;
        using var output = new MemoryStream();
        var audit = PdfPreflight.Apply(source, output, plan, new(PdfProcessingMode.Permissive));
        Assert.Equal(Hash(bytes), audit.Before.SourceSha256);
        Assert.Equal(Hash(output.ToArray()), audit.After.SourceSha256);
        Assert.NotEqual(audit.Before.SourceSha256, audit.After.SourceSha256);
        Assert.DoesNotContain(audit.After.Findings, f => f.Code == "Security.BlockedAction");
        Assert.Contains(audit.After.Findings, f => f.Code == "Font.NotEmbedded");
        Assert.Contains(audit.After.Findings, f => f.Code == "Image.InvalidGeometry");
        Assert.Contains(audit.OperationReport.Diagnostics, d => d.Code == "Preflight.RemoveBlockedActions" && d.LossCategory == PdfLossCategory.FidelityReduction);
        Assert.Equal(bytes, source.ToArray());
        using var json = new MemoryStream();
        PdfPreflight.WriteAudit(audit, json);
        using var parsed = JsonDocument.Parse(json.ToArray());
        Assert.Equal(audit.After.SourceSha256, parsed.RootElement.GetProperty("after").GetProperty("sourceSha256").GetString());
        var fix = Assert.Single(parsed.RootElement.GetProperty("fixes").EnumerateArray());
        Assert.Equal("RemoveBlockedActions", fix.GetProperty("code").GetString());
        Assert.NotEmpty(fix.GetProperty("affectedLocations").EnumerateArray());
        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures");
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(Path.Combine(directory, "preflight-original.pdf"), bytes);
        File.WriteAllBytes(Path.Combine(directory, "preflight-fixed.pdf"), output.ToArray());
        File.WriteAllBytes(Path.Combine(directory, "preflight-audit.json"), json.ToArray());
    }

    [Fact]
    public void StrictLossStalePlansBudgetsAndCancellationCannotModifyDestination()
    {
        byte[] bytes = Fixture();
        using var source = new MemoryStream(bytes);
        var plan = new PdfRemediationPlan(PdfPreflight.Inspect(source), new[] { "RemoveBlockedActions" });
        using var output = new MemoryStream();
        output.WriteByte(42);
        source.Position = 0;
        Assert.Equal("Preflight.RemoveBlockedActions", Assert.Throws<PdfException>(() => PdfPreflight.Apply(source, output, plan)).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        using var changed = new MemoryStream(bytes.Concat(Encoding.ASCII.GetBytes("\n")).ToArray());
        Assert.Equal("Preflight.StalePlan", Assert.Throws<PdfException>(() => PdfPreflight.Apply(changed, output, plan, new(PdfProcessingMode.Permissive))).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
        source.Position = 0;
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => PdfPreflight.Apply(source, output, plan, new(PdfProcessingMode.Permissive, limits: new(maximumObjects: 1)))).Diagnostic.Code);
        source.Position = 0;
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        Assert.Throws<OperationCanceledException>(() => PdfPreflight.Apply(source, output, plan, cancellationToken: cancelled.Token));
        Assert.Equal(0, source.Position);
        Assert.Equal(new byte[] { 42 }, output.ToArray());
    }

    [Fact]
    public void AtomicFileFailuresRetainPriorBytesAndOnlyVerifiedFixesReplaceThem()
    {
        using var source = new MemoryStream(Fixture());
        var plan = new PdfRemediationPlan(PdfPreflight.Inspect(source), new[] { "RemoveBlockedActions" });
        string directory = Path.Combine(AppContext.BaseDirectory, "format-fixtures");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "preflight-atomic.pdf");
        File.WriteAllBytes(path, new byte[] { 42 });
        source.Position = 0;
        Assert.Equal("Preflight.RemoveBlockedActions", Assert.Throws<PdfException>(() => PdfPreflight.ApplyFile(source, path, plan)).Diagnostic.Code);
        Assert.Equal(new byte[] { 42 }, File.ReadAllBytes(path));
        source.Position = 0;
        var audit = PdfPreflight.ApplyFile(source, path, plan, new(PdfProcessingMode.Permissive));
        Assert.Equal(Hash(File.ReadAllBytes(path)), audit.After.SourceSha256);
        Assert.Throws<ArgumentException>(() => new PdfRemediationPlan(audit.After, new[] { "RemoveBlockedActions" }));
    }
}
