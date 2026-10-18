using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using Fluent.Pdf.Html;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class HtmlTests
{
    [Fact]
    public void ExplicitResourceSessionsAcquireMainBytesWithTheSameContextAndAreDisposedPerJob()
    {
        var sessions = new List<RemoteResources>();
        HtmlResourceSessionFactory resources = context =>
        {
            var session = new RemoteResources(context);
            sessions.Add(session);
            return session;
        };
        foreach (bool suppliedMarkup in new[]
        {
            false,
            true
        }

        )
        {
            var peer = new Peer
            {
                Behavior = "missing"
            };
            var renderer = new ChromiumHtmlRenderer(_ => peer, Peer.Version, Peer.Revision, resources);
            var source = suppliedMarkup ? PdfHtmlSource.FromHtml("<p>A</p>") : PdfHtmlSource.FromResource(new("https://fluent-pdf.invalid/document"), Array.Empty<PdfHtmlResource>());
            using var document = PdfHtml.Render(source, new PdfProviderRegistry(renderer));
            Assert.Single(document.Pages);
            Assert.True(sessions[sessions.Count - 1].Disposed);
            Assert.Equal(suppliedMarkup ? 1 : 2, sessions[sessions.Count - 1].Requests.Count);
            Assert.Equal(!suppliedMarkup, sessions[sessions.Count - 1].Requests.Any(r => r.IsMainDocument));
            string headers = peer.ResponseHeaders[peer.ResponseHeaders.Count - 1];
            Assert.DoesNotContain("Set-Cookie", headers);
            Assert.DoesNotContain("Access-Control-Allow-Origin", headers);
        }
    }

    private sealed class RemoteResources : IHtmlResourceSession
    {
        private readonly PdfOperationContext _context;
        internal readonly List<HtmlResourceRequest> Requests = new();
        internal bool Disposed;
        internal RemoteResources(PdfOperationContext context) => _context = context;
        public HtmlResourceResult Resolve(HtmlResourceRequest request, PdfOperationContext context)
        {
            Assert.Same(_context, context);
            Requests.Add(request);
            var resource = HtmlAssets.CaptureDataUri(request.IsMainDocument ? "data:text/html,%3Cp%3EA%3C/p%3E" : "data:image/png;base64,AA==", request.Address, context);
            return new(resource, 200, new[] { new KeyValuePair<string, string>("Set-Cookie", "private=secret") }, context);
        }

        public void Dispose() => Disposed = true;
    }

    [Fact]
    public void LargeCapturedResourceFitsTheDocumentedTransportWithAnExplicitMemoryBudget()
    {
        byte[] bytes = new byte[9 * 1024 * 1024];
        using var input = new MemoryStream(bytes);
        var resource = PdfHtmlResource.Read(new("https://fluent-pdf.invalid/large.bin"), "application/octet-stream", input);
        var peer = new Peer
        {
            Behavior = "resource"
        };
        using var document = PdfHtml.Render(PdfHtmlSource.FromHtml("<p>A</p>", resources: new[] { resource }), Registry(peer), policy: new(limits: new(maximumExpandedBytes: 1073741824)));
        Assert.Equal(2, peer.Bodies.Count);
        byte[] returned = Convert.FromBase64String(peer.Bodies[1]);
        Assert.Equal(bytes.Length, returned.Length);
        using var expectedHash = System.Security.Cryptography.SHA256.Create();
        using var actualHash = System.Security.Cryptography.SHA256.Create();
        Assert.Equal(expectedHash.ComputeHash(bytes), actualHash.ComputeHash(returned));
    }

    [Fact]
    public async System.Threading.Tasks.Task BothLanguageConsumersRenderWithTheExplicitHostAndIndependentJobsHavePrivateConnections()
    {
        var source = PdfHtmlSource.FromHtml("<p>A</p>");
        var peers = new System.Collections.Concurrent.ConcurrentBag<Peer>();
        ChromiumConnectionFactory host = _ =>
        {
            var peer = new Peer();
            peers.Add(peer);
            return peer;
        };
        var csharp = ContractExamples.CSharp.ContractExample.HtmlRenderer(host, Peer.Version, Peer.Revision);
        var visualBasic = ContractExamples.VisualBasic.ContractExample.HtmlRenderer(host, Peer.Version, Peer.Revision);
        using (var first = ContractExamples.CSharp.ContractExample.ConvertHtml(source, csharp, new()))
            Assert.Single(first.Pages);
        using (var second = ContractExamples.VisualBasic.ContractExample.ConvertHtml(source, visualBasic, new()))
            Assert.Single(second.Pages);
        var jobs = Enumerable.Range(0, 4).Select(_ => System.Threading.Tasks.Task.Run(() =>
        {
            using var document = PdfHtml.Render(source, csharp);
            Assert.Single(document.Pages);
        })).ToArray();
        await System.Threading.Tasks.Task.WhenAll(jobs);
        Assert.Equal(6, peers.Count);
        Assert.All(peers, peer =>
        {
            Assert.True(peer.Disposed);
            Assert.Single(peer.Commands, c => c == "Target.createBrowserContext");
        });
    }

    [Fact]
    public void SourceCaptureOwnsExactUtf8AndResourceBytesWhileInputsRemainBorrowed()
    {
        var bytes = Encoding.UTF8.GetBytes("\ufeff<p>Հայերեն 一😀</p>");
        using var input = new MemoryStream(bytes);
        using var data = new MemoryStream(new byte[] { 0, 255, 42 });
        var resource = PdfHtmlResource.Read(new("https://fluent-pdf.invalid/data.bin"), "application/octet-stream", data);
        var source = PdfHtmlSource.Read(input, resources: new[] { resource });
        bytes[3] = 0;
        Assert.Equal("<p>Հայերեն 一😀</p>", source.Markup);
        Assert.True(input.CanRead);
        Assert.True(data.CanRead);
        input.Dispose();
        data.Dispose();
        using var raw = resource.OpenContent();
        using var copy = new MemoryStream();
        raw.CopyTo(copy);
        Assert.Equal(new byte[] { 0, 255, 42 }, copy.ToArray());
        Assert.Throws<NotSupportedException>(() => ((IList<PdfHtmlResource>)source.Resources).Clear());
        Assert.True(source.OperationReport.Succeeded);
    }

    [Fact]
    public void SourcePreparationRejectsMalformedTextUnsafeAddressesAndAmbiguousMaps()
    {
        Assert.Equal("Html.TextEncoding", Error(() => PdfHtmlSource.FromHtml("\ud800")));
        using var invalid = new MemoryStream(new byte[] { 192, 128 });
        Assert.Equal("Html.TextEncoding", Error(() => PdfHtmlSource.Read(invalid)));
        Assert.Throws<ArgumentException>(() => PdfHtmlSource.FromHtml("", new Uri("file:///not-authorized.html")));
        Assert.Throws<ArgumentException>(() => PdfHtmlSource.FromHtml("", new Uri("https://user:password@example.invalid/")));
        using var bytes = new MemoryStream();
        var item = PdfHtmlResource.Read(new("https://fluent-pdf.invalid/a"), "text/plain", bytes);
        Assert.Equal("Html.ResourceInventory", Error(() => PdfHtmlSource.FromHtml("", resources: new[] { item, item })));
        Assert.Throws<ArgumentException>(() => PdfHtmlResource.Read(item.Address, "text/plain\r\nInjected: yes", bytes));
        Assert.Equal("Html.InputExtent", Error(() => PdfHtmlSource.FromHtml(new string ('a', 4194305))));
    }

    [Fact]
    public void InputPreparationHonorsCancellationAndInputExpansionObjectBounds()
    {
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfHtmlSource.FromHtml("", cancellationToken: canceled.Token));
        Assert.Equal("Limit.ExpandedBytes", Error(() => PdfHtmlSource.FromHtml("x", policy: new(limits: new(maximumExpandedBytes: 64)))));
        Assert.Equal("Limit.Objects", Error(() => PdfHtmlSource.FromHtml("", policy: new(limits: new(maximumObjects: 2)))));
        using var input = new MemoryStream(new byte[3]);
        Assert.Equal("Limit.InputBytes", Error(() => PdfHtmlSource.Read(input, policy: new(limits: new(maximumInputBytes: 2)))));
        Assert.True(input.CanRead);
    }

    [Fact]
    public void OptionalRendererOutputImportsIntoTheEditableModelAndLeaseExpires()
    {
        var renderer = new BytesRenderer(PdfBytes());
        using var document = PdfHtml.Render(PdfHtmlSource.FromHtml("<p>Original</p>"), new(renderer));
        Assert.Single(document.Pages);
        Assert.True(document.LastOperationReport!.Succeeded);
        Assert.Equal("RenderHtml", document.LastOperationReport.Operation);
        Assert.Throws<ObjectDisposedException>(() => renderer.Retained!.WriteByte(1));
        using var edited = PdfMetadata.Edit(document.CreateSnapshot(), e => e.SetInformation("Title", "Editable HTML result"));
        Assert.Equal("Editable HTML result", PdfMetadata.Inspect(edited.CreateSnapshot()).Text["Title"]);
        Assert.False(renderer.Disposed);
    }

    [Fact]
    public void MissingMalformedAndSwallowedProviderFailuresCannotReturnDocuments()
    {
        var source = PdfHtmlSource.FromHtml("");
        Assert.Throws<PdfProviderUnavailableException>(() => PdfHtml.Render(source, PdfProviderRegistry.Empty));
        Assert.Throws<PdfException>(() => PdfHtml.Render(source, new(new BytesRenderer(new byte[] { 1, 2, 3 }))));
        var swallowed = new BytesRenderer(PdfBytes())
        {
            SwallowFailure = true
        };
        Assert.Equal("Fixture.HtmlFailure", Error(() => PdfHtml.Render(source, new(swallowed))));
    }

    [Fact]
    public void ViewportBudgetStopsTheHostBeforeLaunchAndPinnedVersionPrecedesDocumentInput()
    {
        int opened = 0;
        var renderer = new ChromiumHtmlRenderer(_ =>
        {
            opened++;
            return new Peer();
        }, Peer.Version, Peer.Revision);
        var source = PdfHtmlSource.FromHtml("<p>private source</p>");
        Assert.Equal("Limit.Pixels", Error(() => PdfHtml.Render(source, new(renderer), policy: new(limits: new(maximumPixels: 1)))));
        Assert.Equal(0, opened);
        var peer = new Peer
        {
            WrongVersion = true
        };
        Assert.Equal("Html.EngineVersion", Error(() => PdfHtml.Render(source, Registry(peer))));
        Assert.Equal(new[] { "Browser.getVersion" }, peer.Commands);
        Assert.True(peer.Disposed);
    }

    [Fact]
    public void ProtocolPrintsThroughFreshPrivateContextWithExplicitScriptAndResourcePolicy()
    {
        var peer = new Peer();
        var source = PdfHtmlSource.FromHtml("<p>Original 一😀</p>");
        using var document = PdfHtml.Render(source, Registry(peer), new(enableJavaScript: true));
        Assert.Single(document.Pages);
        Assert.Equal(source.Markup, Encoding.UTF8.GetString(Convert.FromBase64String(Assert.Single(peer.Bodies))));
        Assert.False(peer.ScriptDisabled);
        Assert.Contains("Target.createBrowserContext", peer.Commands);
        Assert.Contains("Target.setAutoAttach", peer.Commands);
        Assert.Contains("Page.createIsolatedWorld", peer.Commands);
        Assert.Contains("IO.close", peer.Commands);
        Assert.Contains("Target.disposeBrowserContext", peer.Commands);
        Assert.True(peer.Disposed);
    }

    [Fact]
    public void AddressedHtmlAndExplicitFontResourcesStayWithinTheCapturedMap()
    {
        using var html = new MemoryStream(Encoding.UTF8.GetBytes("<p>A</p>"));
        var resource = PdfHtmlResource.Read(new("https://fluent-pdf.invalid/document"), "text/html; charset=utf-8", html);
        using var document = PdfHtml.Render(PdfHtmlSource.FromResource(resource.Address, new[] { resource }), Registry(new Peer()));
        Assert.Single(document.Pages);
        var missing = PdfHtmlSource.FromResource(resource.Address, Array.Empty<PdfHtmlResource>());
        Assert.Equal("Html.MainResourceMissing", Error(() => PdfHtml.Render(missing, Registry(new Peer()))));
    }

    [Theory]
    [InlineData("missing", "Html.ResourceMissing")]
    [InlineData("font", "Html.FontUnavailable")]
    [InlineData("script", "Html.ScriptException")]
    [InlineData("worker", "Html.SubtargetUnsupported")]
    [InlineData("dialog", "Html.DialogUnsupported")]
    [InlineData("auth", "Html.AuthenticationUnsupported")]
    [InlineData("crash", "Html.BrowserTerminated")]
    [InlineData("session", "Html.TargetIdentity")]
    [InlineData("json", "Html.ProtocolJson")]
    [InlineData("array", "Html.Protocol")]
    [InlineData("identity", "Html.ResponseIdentity")]
    [InlineData("base64", "Html.OutputEncoding")]
    [InlineData("empty", "Html.OutputExtent")]
    public void ProtocolAndUnsupportedContentFailuresAreTypedAndDisposeThePrivateHost(string behavior, string code)
    {
        var peer = new Peer
        {
            Behavior = behavior
        };
        Assert.Equal(code, Error(() => PdfHtml.Render(PdfHtmlSource.FromHtml("<p>A</p>"), Registry(peer))));
        Assert.True(peer.Disposed);
    }

    [Theory]
    [InlineData("missing", "Html.ResourceMissing", PdfLossCategory.OmittedContent)]
    [InlineData("font", "Html.FontUnavailable", PdfLossCategory.SubstitutedContent)]
    public void PermissiveLossesRemainReportedWithoutAuthorizingNetworkFallback(string behavior, string code, PdfLossCategory loss)
    {
        var peer = new Peer
        {
            Behavior = behavior
        };
        using var result = PdfHtml.Render(PdfHtmlSource.FromHtml("<p>A</p>"), Registry(peer), policy: new(PdfProcessingMode.Permissive));
        Assert.Contains(result.LastOperationReport!.Diagnostics, d => d.Code == code && d.LossCategory == loss && d.Severity == PdfDiagnosticSeverity.Warning);
        if (behavior == "missing")
            Assert.Contains("Fetch.failRequest", peer.Commands);
    }

    [Fact]
    public void ProtocolSharesCancellationDepthAndOutputBudgets()
    {
        var source = PdfHtmlSource.FromHtml("<p>A</p>");
        using var canceled = new CancellationTokenSource();
        canceled.Cancel();
        Assert.ThrowsAny<OperationCanceledException>(() => PdfHtml.Render(source, Registry(new Peer()), cancellationToken: canceled.Token));
        var shallow = new Peer();
        Assert.Equal("Limit.Depth", Error(() => PdfHtml.Render(source, Registry(shallow), policy: new(limits: new(maximumDepth: 1)))));
        Assert.True(shallow.Disposed);
        Assert.Equal("Limit.OutputBytes", Error(() => PdfHtml.Render(source, Registry(new Peer()), policy: new(limits: new(maximumOutputBytes: 1)))));
    }

    private static PdfProviderRegistry Registry(Peer peer) => new(new ChromiumHtmlRenderer(_ => peer, Peer.Version, Peer.Revision));
    private static string Error(Action action) => Assert.Throws<PdfException>(action).Diagnostic.Code;
    private static byte[] PdfBytes()
    {
        using var document = Pdf.Create().AddPage();
        using var output = new MemoryStream();
        document.Save(output);
        return output.ToArray();
    }

    private sealed class BytesRenderer : IPdfHtmlRenderer, IDisposable
    {
        private readonly byte[] _bytes;
        internal BytesRenderer(byte[] bytes) => _bytes = bytes;
        internal Stream? Retained;
        internal bool SwallowFailure;
        internal bool Disposed;
        public PdfProviderDescriptor Descriptor { get; } = new("fixture.html", "1", PdfCapability.RenderHtml);

        public void Render(PdfHtmlSource source, PdfHtmlOptions options, Stream pdfOutput, PdfOperationContext context)
        {
            Retained = pdfOutput;
            if (SwallowFailure)
            {
                try
                {
                    context.Report(new("Fixture.HtmlFailure", "Original latched source fixture."));
                }
                catch (PdfException)
                {
                }

                return;
            }

            pdfOutput.Write(_bytes, 0, _bytes.Length);
        }

        public void Dispose() => Disposed = true;
    }

    // A protocol peer, not a browser engine. Actual Chromium observations are separately retained.
    internal sealed class Peer : IChromiumConnection
    {
        internal const string Version = "154.0.8037.57", Revision = "@original-source-revision";
        private readonly Queue<string> _messages = new();
        private readonly byte[] _pdf;
        internal Peer(byte[]? pdf = null) => _pdf = pdf ?? PdfBytes();
        internal string Behavior = "";
        internal bool WrongVersion, Disposed, ScriptDisabled, PromiseObserved;
        internal int ReadinessChecks;
        internal string? ObservedLocale, ObservedTimeZone, ObservedMedia;
        internal JsonElement PrintParameters;
        internal readonly List<string> Commands = new(), Bodies = new(), ResponseHeaders = new();
        public void Send(string message, PdfOperationContext context)
        {
            using var parsed = JsonDocument.Parse(message);
            var root = parsed.RootElement;
            int id = root.GetProperty("id").GetInt32();
            string method = root.GetProperty("method").GetString()!;
            var p = root.GetProperty("params");
            Commands.Add(method);
            if (Behavior == "json" && method == "Browser.getVersion")
            {
                _messages.Enqueue("{\"id\":1,\"id\":1,\"result\":{}}");
                return;
            }

            if (Behavior == "array" && method == "Browser.getVersion")
            {
                _messages.Enqueue("[]");
                return;
            }

            if (Behavior == "identity" && method == "Browser.getVersion")
            {
                _messages.Enqueue("{\"id\":999,\"result\":{}}");
                return;
            }

            object result = new
            {
            };
            switch (method)
            {
                case "Browser.getVersion":
                    result = new
                    {
                        product = "Chrome/" + (WrongVersion ? "0.0.0.0" : Version),
                        revision = Revision,
                        protocolVersion = "1.3"
                    };
                    break;
                case "Target.createBrowserContext":
                    result = new
                    {
                        browserContextId = "private-context"
                    };
                    break;
                case "Target.createTarget":
                    result = new
                    {
                        targetId = "private-target"
                    };
                    break;
                case "Target.attachToTarget":
                    result = new
                    {
                        sessionId = "private-session"
                    };
                    break;
                case "Emulation.setLocaleOverride":
                    ObservedLocale = p.GetProperty("locale").GetString();
                    break;
                case "Emulation.setTimezoneOverride":
                    ObservedTimeZone = p.GetProperty("timezoneId").GetString();
                    break;
                case "Emulation.setEmulatedMedia":
                    ObservedMedia = p.GetProperty("media").GetString();
                    break;
                case "Emulation.setScriptExecutionDisabled":
                    ScriptDisabled = p.GetProperty("value").GetBoolean();
                    break;
                case "Page.navigate":
                    if (Behavior == "networkNever" || Behavior == "networkChurn")
                        Event("Network.requestWillBeSent", new { requestId = "background-poll" });
                    if (Behavior == "frame")
                        Event("Page.frameAttached", new { frameId = "child-frame" });
                    Event("Fetch.requestPaused", new { requestId = "main-request", request = new { url = p.GetProperty("url").GetString(), method = "GET" } });
                    if (Behavior == "resource")
                        Event("Fetch.requestPaused", new { requestId = "large-resource", resourceType = "Other", request = new { url = "https://fluent-pdf.invalid/large.bin", method = "GET" } });
                    if (Behavior == "missing")
                        Event("Fetch.requestPaused", new { requestId = "missing-request", resourceType = "Image", request = new { url = "https://not-supplied.invalid/image.png", method = "GET" } });
                    string? failure = Behavior switch
                    {
                        "script" => "Runtime.exceptionThrown",
                        "worker" => "Target.attachedToTarget",
                        "dialog" => "Page.javascriptDialogOpening",
                        "auth" => "Fetch.authRequired",
                        "crash" => "Inspector.targetCrashed",
                        _ => null
                    };
                    if (failure != null)
                        Event(failure, new { });
                    if (Behavior == "session")
                        _messages.Enqueue(JsonSerializer.Serialize(new { method = "Page.lifecycleEvent", sessionId = "other-session", @params = new { } }));
                    result = new
                    {
                        frameId = "main-frame",
                        loaderId = "main-loader"
                    };
                    break;
                case "Page.createIsolatedWorld":
                    result = new
                    {
                        executionContextId = 7
                    };
                    break;
                case "Runtime.evaluate":
                    if (p.TryGetProperty("awaitPromise", out var promise) && promise.GetBoolean())
                    {
                        PromiseObserved = true;
                        if (Behavior == "promiseNever")
                            return;
                        result = Behavior == "promiseRejected" ? new
                        {
                            exceptionDetails = new
                            {
                                text = "private-fixture-data"
                            }
                        }

                        : (object)new
                        {
                            result = new
                            {
                                type = "undefined"
                            }
                        };
                        break;
                    }

                    ReadinessChecks++;
                    if (Behavior == "networkChurn")
                    {
                        Event("Network.loadingFinished", new { requestId = "background-poll" });
                        Event("Network.requestWillBeSent", new { requestId = "background-poll" });
                    }

                    result = new
                    {
                        result = new
                        {
                            type = "object",
                            value = new
                            {
                                documentReady = true,
                                selectorsReady = Behavior != "selectorNever" && (Behavior != "late" || ReadinessChecks >= 3),
                                fontsReady = Behavior != "fontNever" && (Behavior != "late" || ReadinessChecks >= 3),
                                imagesReady = Behavior != "imageNever" && (Behavior != "late" || ReadinessChecks >= 3),
                                failedImages = Behavior == "imageFailure" ? 1 : 0,
                                pendingImages = Behavior == "imageNever" || Behavior == "late" && ReadinessChecks < 3 ? 1 : 0,
                                failedFonts = Behavior == "font" ? 1 : 0
                            }
                        }
                    };
                    break;
                case "Page.printToPDF":
                    PrintParameters = p.Clone();
                    result = new
                    {
                        stream = "private-pdf"
                    };
                    break;
                case "IO.read":
                    result = Behavior == "empty" ? new
                    {
                        data = "",
                        base64Encoded = true,
                        eof = false
                    }

                    : new
                    {
                        data = Behavior == "base64" ? "%%%" : Convert.ToBase64String(_pdf),
                        base64Encoded = true,
                        eof = true
                    };
                    break;
                case "Fetch.fulfillRequest":
                    Bodies.Add(p.GetProperty("body").GetString()!);
                    if (p.TryGetProperty("responseHeaders", out var responseHeaders) && Behavior == "missing")
                        ResponseHeaders.Add(responseHeaders.GetRawText());
                    break;
                case "Target.closeTarget":
                    Event("Inspector.detached", new { reason = "target_closed" });
                    break;
            }

            _messages.Enqueue(JsonSerializer.Serialize(new { id, result }));
            if (method == "Page.navigate")
            {
                if (Behavior != "domNever")
                    Event("Page.lifecycleEvent", new { frameId = "main-frame", loaderId = "main-loader", name = "DOMContentLoaded" });
                Event("Page.lifecycleEvent", new { frameId = "main-frame", loaderId = "main-loader", name = "networkIdle" });
            }
        }

        private void Event(string method, object parameters) => _messages.Enqueue(JsonSerializer.Serialize(new { method, sessionId = "private-session", @params = parameters }));
        public string Receive(PdfOperationContext context)
        {
            context.Checkpoint();
            if (Behavior == "io" && Commands.Contains("Page.navigate"))
                throw new EndOfStreamException("private-transport-fixture");
            while (_messages.Count == 0 && (Behavior == "promiseNever" || Behavior == "domNever"))
            {
                context.CancellationToken.WaitHandle.WaitOne(10);
                context.Checkpoint();
            }

            return _messages.Dequeue();
        }

        public void Dispose() => Disposed = true;
    }
}
