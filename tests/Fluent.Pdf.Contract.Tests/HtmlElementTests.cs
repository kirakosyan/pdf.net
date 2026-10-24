using System.IO;
using System.Text.Json;
using System.Threading;
using Fluent.Pdf.Html;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class HtmlElementTests
{
    private sealed class Peer : IChromiumConnection
    {
        internal readonly HtmlTests.Peer Inner = new();
        internal string? SelectionError;
        internal bool Changed, Rejected, BadGeometry, BadPng, ExcessOutput;
        internal int Screenshots;
        private string? _answer;
        public void Send(string message, PdfOperationContext context)
        {
            using var parsed = JsonDocument.Parse(message);
            var root = parsed.RootElement;
            string method = root.GetProperty("method").GetString()!;
            var parameters = root.GetProperty("params");
            object? value = null;
            if (method == "Runtime.evaluate" && parameters.TryGetProperty("expression", out var expression) && expression.GetString()!.StartsWith("(()=>{const q=", StringComparison.Ordinal))
                value = SelectionError != null ? new { result = new { value = new { error = SelectionError } } } : (object)new { result = new { value = new { x = 1.2, y = 2.3, width = BadGeometry ? 999999999 : 2.5, height = 1.5, sx = 0, sy = 0, dpr = 1, scale = 1, address = "https://fluent-pdf.invalid/document", markup = (string?)null } } };
            else if (method == "Runtime.evaluate" && parameters.GetProperty("expression").GetString()!.StartsWith("({stable:", StringComparison.Ordinal))
                value = new { result = new { value = new { stable = !Changed } } };
            else if (method == "Page.getLayoutMetrics") value = new { cssContentSize = new { x = 0, y = 0, width = 1280, height = 720 } };
            else if (method == "Page.captureScreenshot")
            {
                Screenshots++;
                Assert.True(parameters.GetProperty("captureBeyondViewport").GetBoolean());
                Assert.Equal(1, parameters.GetProperty("clip").GetProperty("scale").GetInt32());
                byte[] png = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "images", "rgb.png"));
                value = new { data = ExcessOutput ? new string('A', 1000) : BadPng ? "%%%" : Convert.ToBase64String(png) };
            }
            if (value == null) { Inner.Send(message, context); return; }
            _answer = Rejected ? JsonSerializer.Serialize(new { id = root.GetProperty("id").GetInt32(), error = new { message = "private-source-sentinel" } }) : JsonSerializer.Serialize(new { id = root.GetProperty("id").GetInt32(), result = value });
        }
        public string Receive(PdfOperationContext context)
        {
            if (_answer == null) return Inner.Receive(context);
            string value = _answer; _answer = null; return value;
        }
        public void Dispose() => Inner.Dispose();
    }
    private static ChromiumHtmlRenderer Renderer(Peer peer) => new(_ => peer, HtmlTests.Peer.Version, HtmlTests.Peer.Revision);
    private static PdfHtmlSource Source() => PdfHtmlSource.FromHtml("<div id='private-source-sentinel'>Original</div>");

    [Fact]
    public void RoundedCaptureIsDetachedAndChangingDomNeverExposesAResult()
    {
        foreach (int mode in new[] { 0, 1, 2 })
        {
            var peer = new Peer();
            var captured = mode == 0 ? ContractExamples.CSharp.ContractExample.CaptureHtmlElement(Renderer(peer), Source(), "div") : mode == 1 ? ContractExamples.VisualBasic.ContractExample.CaptureHtmlElement(Renderer(peer), Source(), "div") : Renderer(peer).Capture(Source(), new(literalText: "Original"));
            Assert.Equal(1.2, captured.DocumentBounds.X);
            Assert.Equal(1, captured.CaptureBounds.X);
            Assert.Equal(3, captured.CaptureBounds.Width);
            Assert.Equal(2, captured.CaptureBounds.Height);
            Assert.Null(captured.Markup);
            Assert.True(captured.OperationReport.Succeeded);
            Assert.True(peer.Inner.Disposed);
            using var first = captured.OpenPng();
            using var second = captured.OpenPng();
            Assert.False(first.CanWrite);
            _ = first.ReadByte(); Assert.Equal(0, second.Position);
        }
        var changed = new Peer { Changed = true };
        Assert.Equal("Html.ElementChanged", Assert.Throws<PdfException>(() => Renderer(changed).Capture(Source(), new("a"))).Diagnostic.Code);
        Assert.True(changed.Inner.Disposed);
    }

    [Theory]
    [InlineData("Missing")]
    [InlineData("Ambiguous")]
    [InlineData("Selector")]
    [InlineData("SearchExtent")]
    [InlineData("MarkupExtent")]
    [InlineData("Empty")]
    public void BoundedSelectionFailuresNeverPrintOrExposePrivateData(string code)
    {
        var peer = new Peer { SelectionError = code };
        var failure = Assert.Throws<PdfException>(() => Renderer(peer).Capture(Source(), new(selector: "#private-source-sentinel")));
        Assert.Equal("Html.Element" + code, failure.Diagnostic.Code);
        Assert.DoesNotContain("private-source-sentinel", failure.ToString());
        Assert.True(peer.Inner.Disposed);
        Assert.Equal(0, peer.Screenshots);
        Assert.DoesNotContain("Page.printToPDF", peer.Inner.Commands);
    }

    [Fact]
    public void InvalidRequestAndCanceledOrExhaustedJobsNeverOpenAHost()
    {
        Assert.Throws<ArgumentException>(() => new HtmlElementRequest());
        Assert.Throws<ArgumentException>(() => new HtmlElementRequest("a", "a"));
        Assert.Throws<ArgumentException>(() => new HtmlElementRequest(literalText: "\ud800"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HtmlElementRequest("a", maximumNodes: 100001));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HtmlElementRequest("a", maximumPixels: 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HtmlElementRequest("a", maximumPngBytes: 8388609));
        Assert.Throws<ArgumentOutOfRangeException>(() => new HtmlElementRequest("a", maximumMarkupCharacters: -1));
        int calls = 0;
        var renderer = new ChromiumHtmlRenderer(_ => { calls++; return new Peer(); }, HtmlTests.Peer.Version, HtmlTests.Peer.Revision);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => renderer.Capture(Source(), new("a"), cancellationToken: cancel.Token));
        Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => renderer.Capture(Source(), new("a"), policy: new(limits: new(maximumPixels: 1)))).Diagnostic.Code);
        Assert.Equal(0, calls);
    }

    [Fact]
    public void PixelAndOutputAdmissionPrecedeExposureAndProtocolErrorsAreRedacted()
    {
        var large = new Peer { BadGeometry = true };
        Assert.Equal("Html.ElementPixels", Assert.Throws<PdfException>(() => Renderer(large).Capture(Source(), new("a", maximumPixels: 1))).Diagnostic.Code);
        Assert.Equal(0, large.Screenshots);
        var rejected = new Peer { Rejected = true };
        var failure = Assert.Throws<PdfException>(() => Renderer(rejected).Capture(Source(), new("a")));
        Assert.Equal("Html.CommandRejected", failure.Diagnostic.Code);
        Assert.DoesNotContain("private-source-sentinel", failure.ToString());
        Assert.Contains("Runtime.evaluate", failure.Message);
        var excess = new Peer { ExcessOutput = true };
        Assert.Equal("Html.ElementOutputExtent", Assert.Throws<PdfException>(() => Renderer(excess).Capture(Source(), new("a", maximumPngBytes: 64))).Diagnostic.Code);
        var invalid = new Peer { BadPng = true };
        Assert.Equal("Html.ElementEncoding", Assert.Throws<PdfException>(() => Renderer(invalid).Capture(Source(), new("a"))).Diagnostic.Code);
    }
}
