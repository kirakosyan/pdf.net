using Fluent.Pdf.Html;
using Xunit;
using Peer = Fluent.Pdf.Contract.Tests.HtmlTests.Peer;

namespace Fluent.Pdf.Contract.Tests;
public sealed class HtmlReadinessTests
{
    private static PdfProviderRegistry Registry(Peer peer) => new(new ChromiumHtmlRenderer(_ => peer, Peer.Version, Peer.Revision));
    private static PdfHtmlSource Source() => PdfHtmlSource.FromHtml("<p>A</p>");
    private static PdfHtmlReadinessOptions Ready(TimeSpan? timeout = null, int pending = 0, string? promise = null, IEnumerable<string>? selectors = null, TimeSpan? quiet = null) => new(timeout ?? TimeSpan.FromSeconds(2), selectors, promise, networkQuietPeriod: quiet ?? TimeSpan.Zero, maximumInFlightRequests: pending, pollingInterval: TimeSpan.FromMilliseconds(10));
    [Fact]
    public void LateSelectorsFontsAndImagesAreInspectedUntilAllRequestedConditionsHold()
    {
        var peer = new Peer
        {
            Behavior = "late"
        };
        using var result = PdfHtml.Render(Source(), Registry(peer), new(readiness: Ready(selectors: new[] { "#ready" })));
        Assert.Single(result.Pages);
        Assert.Equal(3, peer.ReadinessChecks);
        Assert.True(peer.Disposed);
        Assert.Equal(1, peer.Commands.Count(c => c == "Page.printToPDF"));
    }

    [Theory]
    [InlineData("selectorNever")]
    [InlineData("fontNever")]
    [InlineData("imageNever")]
    [InlineData("networkNever")]
    [InlineData("domNever")]
    [InlineData("promiseNever")]
    public void EveryUnresolvedReadinessConditionHasADeadlineAndDisposesItsPrivateHost(string behavior)
    {
        var peer = new Peer
        {
            Behavior = behavior
        };
        bool promise = behavior == "promiseNever";
        var readiness = Ready(TimeSpan.FromMilliseconds(100), promise: promise ? "new Promise(()=>{})" : null, quiet: behavior == "networkNever" ? TimeSpan.FromMilliseconds(20) : TimeSpan.Zero);
        var clock = System.Diagnostics.Stopwatch.StartNew();
        var failure = Assert.Throws<PdfException>(() => PdfHtml.Render(Source(), Registry(peer), new(enableJavaScript: promise, readiness: readiness)));
        Assert.Equal("Limit.ElapsedTime", failure.Diagnostic.Code);
        Assert.True(clock.Elapsed < TimeSpan.FromSeconds(1));
        Assert.True(peer.Disposed);
        Assert.DoesNotContain("Page.printToPDF", peer.Commands);
    }

    [Fact]
    public void BoundedBackgroundChurnCanCompleteWithAnExplicitInFlightAllowance()
    {
        var peer = new Peer
        {
            Behavior = "networkChurn"
        };
        using var result = PdfHtml.Render(Source(), Registry(peer), new(readiness: Ready(pending: 1, quiet: TimeSpan.FromMilliseconds(50))));
        Assert.Single(result.Pages);
        Assert.True(peer.ReadinessChecks >= 2);
        Assert.True(peer.Disposed);
    }

    [Fact]
    public void PromiseExecutionIsExplicitAndRejectsWithoutEchoingItsSourceOrFailurePayload()
    {
        int factories = 0;
        var renderer = new ChromiumHtmlRenderer(_ =>
        {
            factories++;
            return new Peer();
        }, Peer.Version, Peer.Revision);
        Assert.Equal("Html.PromiseRequiresJavaScript", Assert.Throws<PdfException>(() => PdfHtml.Render(Source(), new(renderer), new(readiness: Ready(promise: "Promise.resolve()")))).Diagnostic.Code);
        Assert.Equal(0, factories);
        var successful = new Peer();
        using (var result = PdfHtml.Render(Source(), Registry(successful), new(enableJavaScript: true, readiness: Ready(promise: "Promise.resolve()"))))
            Assert.Single(result.Pages);
        Assert.True(successful.PromiseObserved);
        Assert.Contains("Runtime.releaseObjectGroup", successful.Commands);
        var rejected = new Peer
        {
            Behavior = "promiseRejected"
        };
        var failure = Assert.Throws<PdfException>(() => PdfHtml.Render(Source(), Registry(rejected), new(enableJavaScript: true, readiness: Ready(promise: "Promise.reject('private-promise-source')"))));
        Assert.Equal("Html.PromiseRejected", failure.Diagnostic.Code);
        Assert.DoesNotContain("private-promise-source", failure.ToString());
        Assert.DoesNotContain("private-fixture-data", failure.ToString());
        Assert.True(rejected.Disposed);
    }

    [Fact]
    public void LocaleTimezoneViewportAndMediaAreAppliedBeforeNavigation()
    {
        foreach (var create in new Func<PdfHtmlReadinessOptions, bool, string, string, PdfHtmlMedia, PdfHtmlOptions>[]
        {
            ContractExamples.CSharp.ContractExample.HtmlOptions,
            ContractExamples.VisualBasic.ContractExample.HtmlOptions
        }

        )
        {
            var peer = new Peer();
            using var result = PdfHtml.Render(Source(), Registry(peer), create(Ready(), false, "hy-AM", "Asia/Yerevan", PdfHtmlMedia.Screen));
            Assert.Equal("hy-AM", peer.ObservedLocale);
            Assert.Equal("Asia/Yerevan", peer.ObservedTimeZone);
            Assert.Equal("screen", peer.ObservedMedia);
            Assert.True(peer.Commands.IndexOf("Emulation.setEmulatedMedia") < peer.Commands.IndexOf("Page.navigate"));
        }
    }

    [Fact]
    public void BrokenHostIoIsTypedAndRedactedWhileThePrivateConnectionIsDisposed()
    {
        var peer = new Peer
        {
            Behavior = "io"
        };
        var failure = Assert.Throws<PdfException>(() => PdfHtml.Render(Source(), Registry(peer), new(readiness: Ready())));
        Assert.Equal("Html.BrowserConnection", failure.Diagnostic.Code);
        Assert.DoesNotContain("private-transport-fixture", failure.ToString());
        Assert.True(peer.Disposed);
        Assert.DoesNotContain("Page.printToPDF", peer.Commands);
    }

    [Fact]
    public void FailedOrExplicitlyUnawaitedAssetsFollowStrictAndPermissiveLossPolicy()
    {
        Assert.Equal("Html.ImageUnavailable", Assert.Throws<PdfException>(() => PdfHtml.Render(Source(), Registry(new() { Behavior = "imageFailure" }), new(readiness: Ready()))).Diagnostic.Code);
        foreach (var item in new[]
        {
            ("imageFailure", "Html.ImageUnavailable", new PdfHtmlReadinessOptions(networkQuietPeriod: TimeSpan.Zero)),
            ("imageNever", "Html.ImagesPending", new PdfHtmlReadinessOptions(waitForImages: false, networkQuietPeriod: TimeSpan.Zero)),
            ("fontNever", "Html.FontsPending", new PdfHtmlReadinessOptions(waitForFonts: false, networkQuietPeriod: TimeSpan.Zero))
        }

        )
        {
            using var document = PdfHtml.Render(Source(), Registry(new() { Behavior = item.Item1 }), new(readiness: item.Item3), new(PdfProcessingMode.Permissive));
            Assert.Contains(document.LastOperationReport!.Diagnostics, d => d.Code == item.Item2 && d.Severity == PdfDiagnosticSeverity.Warning);
        }
    }

    [Fact]
    public void CrashedJobsAreDiscardedAndTheNextJobUsesANewPrivateConnection()
    {
        var peers = new List<Peer>();
        var renderer = new ChromiumHtmlRenderer(_ =>
        {
            var peer = new Peer
            {
                Behavior = peers.Count == 0 ? "crash" : ""
            };
            peers.Add(peer);
            return peer;
        }, Peer.Version, Peer.Revision);
        var registry = new PdfProviderRegistry(renderer);
        Assert.Equal("Html.BrowserTerminated", Assert.Throws<PdfException>(() => PdfHtml.Render(Source(), registry, new(readiness: Ready()))).Diagnostic.Code);
        using var document = PdfHtml.Render(Source(), registry, new(readiness: Ready()));
        Assert.Equal(2, peers.Count);
        Assert.All(peers, p => Assert.True(p.Disposed));
        Assert.Single(document.Pages);
    }

    [Fact]
    public void UnsupportedChildFrameReadinessRejectsBeforePrinting()
    {
        var peer = new Peer
        {
            Behavior = "frame"
        };
        Assert.Equal("Html.SubframeReadinessUnsupported", Assert.Throws<PdfException>(() => PdfHtml.Render(Source(), Registry(peer), new(readiness: Ready()))).Diagnostic.Code);
        Assert.True(peer.Disposed);
        Assert.DoesNotContain("Page.printToPDF", peer.Commands);
    }

    [Fact]
    public void ReadinessConfigurationIsAnImmutableBoundedSnapshot()
    {
        var selectors = new[]
        {
            "#ready"
        };
        var options = Ready(selectors: selectors);
        selectors[0] = "#changed";
        Assert.Equal("#ready", Assert.Single(options.Selectors));
        Assert.Throws<NotSupportedException>(() => ((IList<string>)options.Selectors).Clear());
        Assert.Throws<ArgumentOutOfRangeException>(() => Ready(TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => Ready(pending: 65));
        Assert.Throws<ArgumentException>(() => Ready(promise: "\ud800"));
        Assert.Throws<ArgumentException>(() => Ready(selectors: Enumerable.Repeat("a", 65)));
        Assert.Throws<ArgumentException>(() => new PdfHtmlOptions(locale: "en\r\nprivate"));
        Assert.Throws<ArgumentException>(() => new PdfHtmlOptions(timeZone: "../private\0"));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfHtmlOptions(media: (PdfHtmlMedia)2));
        var previousSignature = new PdfHtmlOptions(null, 1280, 720, false, true, 1000);
        Assert.Same(PdfHtmlReadinessOptions.Default, previousSignature.Readiness);
    }
}
