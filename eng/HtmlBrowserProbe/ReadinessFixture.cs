using Fluent.Pdf;
using Fluent.Pdf.Html;
using System.Security.Cryptography;
using System.Text.Json;

// Fixed original trusted fixtures. This observer does not qualify a deployable browser host or its OS security boundary.
internal static class ReadinessFixture
{
    internal static void Observe(string outputRoot, Func<PdfOperationContext, FixtureConnection> host, string fontPath, string version, string revision)
    {
        var origin = new Uri("https://fluent-pdf.invalid/");
        using var fontInput = File.OpenRead(fontPath);
        var font = PdfHtmlResource.Read(new Uri(origin, "font.ttf"), "font/ttf", fontInput);
        using var lateInput = File.OpenRead(fontPath);
        var late = PdfHtmlResource.Read(new Uri(origin, "late.ttf"), "font/ttf", lateInput);
        var image = HtmlAssets.ReadDataUri("data:image/svg+xml,%3Csvg%20xmlns='http://www.w3.org/2000/svg'%20width='100'%20height='40'%3E%3Crect%20width='100'%20height='40'%20fill='red'/%3E%3C/svg%3E", new Uri(origin, "late.svg"));
        var poll = HtmlAssets.ReadDataUri("data:text/plain,original", new Uri(origin, "poll"));
        var observations = new List<object>();
        const string prefix = "<!doctype html><meta charset=utf-8><style>@font-face{font-family:Original;src:url('/font.ttf')}body{margin:0;font:24px Original}img{display:block}</style><div id=state>A</div>";
        foreach (string mode in new[]
        {
            "late-assets",
            "promise",
            "polling",
            "image-failure",
            "timeout",
            "cancellation",
            "crash",
            "after-crash"
        }

        )
        {
            string html = prefix;
            string? promise = null;
            string[] selectors = Array.Empty<string>();
            int pending = 0;
            if (mode == "late-assets")
            {
                html += "<script>setTimeout(()=>{const f=new FontFace('Late',\"url('/late.ttf')\");document.fonts.add(f);f.load();document.body.style.fontFamily='Late';const i=document.createElement('img');i.width=100;i.height=40;i.src='/late.svg';document.body.append(i);const d=document.querySelector('#state');d.id='ready';d.textContent='A B 一😀'},150)</script>";
                selectors = new[]
                {
                    "#ready"
                };
            }
            else if (mode == "promise")
            {
                html += "<script>window.originalReady=new Promise(resolve=>setTimeout(()=>{const d=document.querySelector('#state');d.id='ready';d.textContent='A B';if(new Intl.DateTimeFormat().resolvedOptions().locale!=='hy-AM'||Intl.DateTimeFormat().resolvedOptions().timeZone!=='Asia/Yerevan'||!matchMedia('screen').matches)throw Error('Original environment mismatch');resolve()},150))</script>";
                promise = "window.originalReady";
                selectors = new[]
                {
                    "#ready"
                };
            }
            else if (mode == "polling")
            {
                html += "<script>let n=0;setInterval(()=>fetch('/poll').then(r=>r.text()).then(()=>{if(++n===3){const d=document.querySelector('#state');d.id='ready';d.textContent='A B'}}),30)</script>";
                selectors = new[]
                {
                    "#ready"
                };
                pending = 4;
            }
            else if (mode == "image-failure")
                html += "<img src='data:image/png;base64,AA=='>";
            else if (mode == "timeout" || mode == "cancellation")
            {
                html += "<script>window.originalNever=new Promise(()=>{})</script>";
                promise = "window.originalNever";
            }

            var settings = new PdfHtmlReadinessOptions(timeout: mode == "timeout" ? TimeSpan.FromSeconds(1) : TimeSpan.FromSeconds(10), selectors: selectors, javaScriptPromise: promise, networkQuietPeriod: TimeSpan.FromMilliseconds(100), maximumInFlightRequests: pending, pollingInterval: TimeSpan.FromMilliseconds(25));
            var options = new PdfHtmlOptions(enableJavaScript: true, readiness: settings, locale: mode == "promise" ? "hy-AM" : "en-US", timeZone: mode == "promise" ? "Asia/Yerevan" : "UTC", media: mode == "promise" ? PdfHtmlMedia.Screen : PdfHtmlMedia.Print);
            var source = PdfHtmlSource.FromHtml(html, resources: new[] { font, poll });
            using var cancel = new CancellationTokenSource();
            Task cancelWork = Task.CompletedTask;
            long cancelledAt = 0;
            ObservedConnection? observed = null;
            var slowAssets = new SlowAssets(late, image);
            var elapsed = System.Diagnostics.Stopwatch.StartNew();
            var renderer = new ChromiumHtmlRenderer(c =>
            {
                c.Consume(PdfResourceKind.ExpandedBytes, 4096);
                return observed = new(host(c), mode == "crash", () =>
                {
                    if (mode == "cancellation")
                        cancelWork = Task.Run(async () =>
                        {
                            await Task.Delay(50);
                            Interlocked.Exchange(ref cancelledAt, System.Diagnostics.Stopwatch.GetTimestamp());
                            cancel.Cancel();
                        });
                });
            }, version, revision, c =>
            {
                c.Consume(PdfResourceKind.Objects, 1);
                c.Consume(PdfResourceKind.ExpandedBytes, 256);
                return slowAssets;
            });
            try
            {
                using var document = PdfHtml.Render(source, new PdfProviderRegistry(renderer), options, new(limits: new(maximumExpandedBytes: 2147483648, maximumElapsedTime: TimeSpan.FromSeconds(30))), cancellationToken: cancel.Token);
                if (mode == "image-failure" || mode == "timeout" || mode == "cancellation" || mode == "crash")
                    throw new InvalidDataException("An original negative readiness fixture unexpectedly produced output.");
                string text = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0).Text;
                string expected = mode == "late-assets" ? "A B 一😀" : mode == "promise" || mode == "polling" ? "A B" : "A";
                File.WriteAllText(Path.Combine(outputRoot, "readiness-" + mode + "-observed.json"), JsonSerializer.Serialize(new { mode, text, expected, pageCount = document.Pages.Count, inspectionCalls = observed?.InspectionCalls, pendingAssetObserved = observed?.PendingAssetObserved, promiseCalls = observed?.PromiseCalls }));
                if (text != expected || document.Pages.Count != 1 || observed == null || mode == "late-assets" && (!observed.PendingAssetObserved || slowAssets.Served != 2 || elapsed.Elapsed < TimeSpan.FromMilliseconds(300) || observed.InspectionCalls < 2) || mode == "promise" && observed.PromiseCalls != 1)
                    throw new InvalidDataException("An original browser readiness fixture did not retain its declared semantics/observations.");
                string path = Path.Combine(outputRoot, "readiness-" + mode + ".pdf");
                document.SaveFile(path);
                using var bytes = File.OpenRead(path);
                observations.Add(new { mode, text, pageCount = 1, inspectionCalls = observed.InspectionCalls, delayedAssets = slowAssets.Served, elapsedMilliseconds = elapsed.Elapsed.TotalMilliseconds, pendingAssetObserved = observed.PendingAssetObserved, promiseCalls = observed.PromiseCalls, outputSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), outcome = "expected-success" });
            }
            catch (PdfException failure)when (mode == "image-failure" || mode == "timeout" || mode == "crash")
            {
                string expected = mode == "image-failure" ? "Html.ImageUnavailable" : mode == "timeout" ? "Limit.ElapsedTime" : "Html.BrowserConnection";
                if (failure.Diagnostic.Code != expected || mode == "crash" && observed?.CrashTriggered != true)
                    throw;
                observations.Add(new { mode, diagnostic = failure.Diagnostic.Code, outcome = "expected-failure-no-output" });
            }
            catch (OperationCanceledException)when (mode == "cancellation")
            {
                var duration = System.Diagnostics.Stopwatch.GetElapsedTime(Interlocked.Read(ref cancelledAt));
                if (cancelledAt == 0 || duration > TimeSpan.FromSeconds(5))
                    throw new InvalidDataException("Original caller cancellation did not terminate the owned host within its declared bound.");
                observations.Add(new { mode, outcome = "expected-caller-cancellation-no-output", signalToExitMilliseconds = duration.TotalMilliseconds });
            }
            catch (PdfException failure)
            {
                File.WriteAllText(Path.Combine(outputRoot, "readiness-" + mode + "-failure-observed.json"), JsonSerializer.Serialize(new { mode, diagnostic = failure.Diagnostic.Code, inspectionCalls = observed?.InspectionCalls, pendingRequests = observed?.PendingRequests, lastReadinessState = observed?.LastReadinessState, nativeEvents = observed?.Events, commands = observed?.Commands }));
                throw;
            }
            finally
            {
                cancelWork.GetAwaiter().GetResult();
            }
        }

        File.WriteAllText(Path.Combine(outputRoot, "readiness-observations.json"), JsonSerializer.Serialize(new { purpose = "trusted-original-readiness-source-observations-not-production-qualification", version, revision, observations, productionQualified = false }, new JsonSerializerOptions { WriteIndented = true }));
    }

    private sealed class SlowAssets : IHtmlResourceSession
    {
        private readonly PdfHtmlResource _font, _image;
        internal int Served;
        internal SlowAssets(PdfHtmlResource font, PdfHtmlResource image)
        {
            _font = font;
            _image = image;
        }

        public HtmlResourceResult? Resolve(HtmlResourceRequest request, PdfOperationContext context)
        {
            var resource = request.Address == _font.Address ? _font : request.Address == _image.Address ? _image : null;
            if (resource == null)
                return null;
            context.CancellationToken.WaitHandle.WaitOne(150);
            context.Checkpoint();
            Served++;
            return new(resource, 200, null, context);
        }

        public void Dispose()
        {
        }
    }

    private sealed class ObservedConnection : IChromiumConnection
    {
        private readonly FixtureConnection _host;
        private readonly bool _crash;
        private readonly Action _afterPromise;
        internal int InspectionCalls, PromiseCalls;
        internal bool PendingAssetObserved, CrashTriggered;
        private readonly HashSet<string> _pending = new(StringComparer.Ordinal);
        internal int PendingRequests => _pending.Count;

        internal string LastReadinessState = "";
        internal readonly List<string> Events = new(), Commands = new();
        internal ObservedConnection(FixtureConnection host, bool crash, Action afterPromise)
        {
            _host = host;
            _crash = crash;
            _afterPromise = afterPromise;
        }

        public void Send(string message, PdfOperationContext context)
        {
            if (Commands.Count < 128)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, message.Length * 4L + 1024);
                using var command = JsonDocument.Parse(message);
                Commands.Add(command.RootElement.GetProperty("id").GetInt32() + ":" + command.RootElement.GetProperty("method").GetString());
            }

            if (message.Contains("\"method\":\"Runtime.evaluate\"", StringComparison.Ordinal))
            {
                if (message.Contains("\"awaitPromise\":true", StringComparison.Ordinal))
                    PromiseCalls++;
                else
                    InspectionCalls++;
                if (_crash && !CrashTriggered)
                {
                    CrashTriggered = true;
                    _host.TerminateForOriginalFixture();
                    return;
                }
            }

            _host.Send(message, context);
            if (message.Contains("\"awaitPromise\":true", StringComparison.Ordinal))
                _afterPromise();
        }

        public string Receive(PdfOperationContext context)
        {
            string text = _host.Receive(context);
            if (text.Contains("\"fontsReady\"", StringComparison.Ordinal) || text.Contains("\"Network.requestWillBeSent\"", StringComparison.Ordinal) || text.Contains("\"Network.loadingFinished\"", StringComparison.Ordinal) || text.Contains("\"Network.loadingFailed\"", StringComparison.Ordinal))
            {
                context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 12L + 4096);
                using var parsed = JsonDocument.Parse(text);
                if (parsed.RootElement.TryGetProperty("result", out var result) && result.TryGetProperty("result", out var remote) && remote.TryGetProperty("value", out var value))
                {
                    PendingAssetObserved |= value.GetProperty("fontsReady").ValueKind == JsonValueKind.False || value.GetProperty("pendingImages").GetInt32() != 0;
                    LastReadinessState = value.GetRawText();
                }

                if (parsed.RootElement.TryGetProperty("method", out var method) && parsed.RootElement.TryGetProperty("params", out var parameters) && parameters.TryGetProperty("requestId", out var request))
                {
                    string id = request.GetString()!;
                    if (Events.Count < 128)
                        Events.Add(method.GetString() + ":" + id);
                    if (method.GetString() == "Network.requestWillBeSent")
                    {
                        if (_pending.Count >= 4096)
                            throw new InvalidDataException("Original observer request inventory exceeded its bound.");
                        context.Consume(PdfResourceKind.Objects, 1);
                        context.Consume(PdfResourceKind.ExpandedBytes, 1024);
                        _pending.Add(id);
                    }
                    else
                        _pending.Remove(id);
                }
            }

            return text;
        }

        public void Dispose() => _host.Dispose();
    }
}
