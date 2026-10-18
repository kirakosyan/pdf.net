using System.IO;
using System.IO.Compression;
using System.Net;
using System.Text;
using System.Threading;
using Fluent.Pdf.Html;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class HtmlResourceTests
{
    private static readonly Uri A = new("https://a.example.invalid/"), B = new("https://b.example.invalid/");
    private static PdfOperationReport Run(Action<PdfOperationContext> action, PdfOperationPolicy? policy = null, CancellationToken token = default) => PdfFont.Run("HtmlResources", "original.fixture", policy, token, action, PdfCapability.RenderHtml);
    private static string Code(Action<PdfOperationContext> action, PdfOperationPolicy? policy = null) => Assert.Throws<PdfException>(() => Run(action, policy)).Diagnostic.Code;
    private static HtmlNetworkPolicy Policy(int redirects = 5, long bytes = 8388608) => new(new[] { new HtmlOriginRule(A), new HtmlOriginRule(B) }, maximumRedirects: redirects, maximumResponseBytes: bytes);
    private static HtmlResourceRequest Request(string path = "a", string id = "1", string? previous = null) => new(new Uri(A, path), id, previous);
    private static byte[] Bytes(PdfHtmlResource resource)
    {
        using var raw = resource.OpenContent();
        using var copy = new MemoryStream();
        raw.CopyTo(copy);
        return copy.ToArray();
    }

    private sealed class Names : IHtmlNameResolver
    {
        internal int Calls;
        internal Func<int, IPAddress[]> Answers = _ => new[]
        {
            IPAddress.Parse("93.184.216.34")
        };
        public IReadOnlyList<IPAddress> Resolve(string host, PdfOperationContext context)
        {
            context.Checkpoint();
            return Answers(++Calls);
        }
    }

    private sealed class Transport : IHtmlHttpTransport
    {
        internal readonly List<HtmlHttpRequest> Requests = new();
        internal Func<HtmlHttpRequest, PdfOperationContext, HtmlHttpResponse> Reply = (_, c) => new(200, "text/plain", new MemoryStream(Encoding.UTF8.GetBytes("original 一😀")), null, c);
        public HtmlHttpResponse Send(HtmlHttpRequest request, PdfOperationContext context)
        {
            Requests.Add(request);
            return Reply(request, context);
        }
    }

    [Fact]
    public void OfflineAndUnauthorizedOriginsNeverInvokeDnsOrTransport()
    {
        var names = new Names();
        var transport = new Transport();
        Run(c =>
        {
            using var session = new HtmlResourceLoader(resolver: names, transport: transport).OpenSession(c);
            Assert.Null(session.Resolve(Request(), c));
        });
        Assert.Equal("Html.OriginDenied", Code(c =>
        {
            using var s = new HtmlResourceLoader(Policy(), names, transport).OpenSession(c);
            s.Resolve(new(new("https://other.invalid/private?secret=do-not-log"), "1"), c);
        }));
        Assert.Equal(0, names.Calls);
        Assert.Empty(transport.Requests);
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.1")]
    [InlineData("169.254.169.254")]
    [InlineData("172.16.0.1")]
    [InlineData("192.168.1.1")]
    [InlineData("0.0.0.0")]
    [InlineData("100.64.0.1")]
    [InlineData("198.18.0.1")]
    [InlineData("192.0.2.1")]
    [InlineData("224.0.0.1")]
    [InlineData("::1")]
    [InlineData("fc00::1")]
    [InlineData("fe80::1")]
    [InlineData("::ffff:127.0.0.1")]
    [InlineData("2001:db8::1")]
    [InlineData("2002:7f00:1::1")]
    public void EveryDnsAnswerMustPassEvenWhenTheFirstIsPublic(string forbidden)
    {
        var names = new Names
        {
            Answers = _ => new[]
            {
                IPAddress.Parse("93.184.216.34"),
                IPAddress.Parse(forbidden)
            }
        };
        var transport = new Transport();
        Assert.Equal("Html.AddressDenied", Code(c =>
        {
            using var s = new HtmlResourceLoader(Policy(), names, transport).OpenSession(c);
            s.Resolve(Request(), c);
        }));
        Assert.Empty(transport.Requests);
    }

    [Fact]
    public void RedirectsRecheckOriginsAndDnsAndCannotReuseAnIdentity()
    {
        var names = new Names
        {
            Answers = call => new[]
            {
                IPAddress.Parse(call == 1 ? "93.184.216.34" : "127.0.0.1")
            }
        };
        var transport = new Transport
        {
            Reply = (_, c) => new(302, "text/plain", new MemoryStream(), new[] { new KeyValuePair<string, string>("Location", "/next") }, c)
        };
        Assert.Equal("Html.AddressDenied", Code(c =>
        {
            using var s = new HtmlResourceLoader(Policy(), names, transport).OpenSession(c);
            Assert.Equal(302, s.Resolve(Request(), c)!.StatusCode);
            s.Resolve(Request("next", "2", "1"), c);
        }));
        Assert.Single(transport.Requests);
        Assert.Equal(2, names.Calls);
        names = new();
        transport.Requests.Clear();
        Assert.Equal("Html.RedirectIdentity", Code(c =>
        {
            using var s = new HtmlResourceLoader(Policy(), names, transport).OpenSession(c);
            s.Resolve(Request(), c);
            s.Resolve(Request("different", "2", "1"), c);
        }));
        Assert.Single(transport.Requests);
        transport.Reply = (_, c) => new(302, "text/plain", new MemoryStream(), new[] { new KeyValuePair<string, string>("Location", "https://denied.invalid/") }, c);
        Assert.Equal("Html.OriginDenied", Code(c =>
        {
            using var s = new HtmlResourceLoader(Policy(), names, transport).OpenSession(c);
            s.Resolve(Request(), c);
        }));
    }

    [Fact]
    public void RedirectBudgetAndExactNumericDestinationAreObservable()
    {
        var transport = new Transport
        {
            Reply = (_, c) => new(302, "text/plain", new MemoryStream(), new[] { new KeyValuePair<string, string>("Location", "/next") }, c)
        };
        Assert.Equal("Html.RedirectLimit", Code(c =>
        {
            using var s = new HtmlResourceLoader(Policy(1), new Names(), transport).OpenSession(c);
            s.Resolve(Request(), c);
            s.Resolve(Request("next", "2", "1"), c);
        }));
        Assert.Equal(2, transport.Requests.Count);
        Assert.All(transport.Requests, r => Assert.Equal("93.184.216.34", r.DestinationAddress));
    }

    [Fact]
    public void HeadersCookiesAndProxyCredentialsAreOriginAndJobScoped()
    {
        var policy = new HtmlNetworkPolicy(new[] { new HtmlOriginRule(A, new[] { new KeyValuePair<string, string>("Authorization", "Bearer private-a") }, new[] { "initial=private-a; Path=/; Secure" }), new HtmlOriginRule(B) }, proxy: new(new("http://proxy.example.invalid:8080"), "Basic private-proxy"));
        var transport = new Transport
        {
            Reply = (r, c) => new(200, "text/plain", new MemoryStream(), r.Address.Host == A.Host ? new[] { new KeyValuePair<string, string>("Set-Cookie", "session=private-response; Domain=example.invalid; Path=/; Secure") } : null, c)
        };
        Run(c =>
        {
            using var s = new HtmlResourceLoader(policy, new Names(), transport).OpenSession(c);
            s.Resolve(Request(), c);
            s.Resolve(Request("next", "2"), c);
            s.Resolve(new(new Uri(B, "b"), "3"), c);
        });
        Assert.Equal("Bearer private-a", transport.Requests[0].Headers["Authorization"]);
        Assert.Contains("initial=private-a", transport.Requests[0].Headers["Cookie"]);
        Assert.Contains("session=private-response", transport.Requests[1].Headers["Cookie"]);
        Assert.False(transport.Requests[2].Headers.ContainsKey("Authorization"));
        Assert.False(transport.Requests[2].Headers.ContainsKey("Cookie"));
        Assert.All(transport.Requests, r =>
        {
            Assert.False(r.Headers.ContainsKey("Proxy-Authorization"));
            Assert.Equal("93.184.216.34", r.ProxyAddress);
        });
        Run(c =>
        {
            using var s = new HtmlResourceLoader(policy, new Names(), transport).OpenSession(c);
            s.Resolve(Request(), c);
        });
        Assert.DoesNotContain("private-response", transport.Requests[3].Headers["Cookie"]);
    }

    [Fact]
    public void DecodingChargesTheSharedInputAndExpandedBudgetsAndRejectsBombs()
    {
        byte[] compressed;
        using (var buffer = new MemoryStream())
        {
            using (var gzip = new GZipStream(buffer, CompressionMode.Compress, true))
            {
                byte[] plain = Encoding.UTF8.GetBytes(new string ('x', 8192));
                gzip.Write(plain, 0, plain.Length);
            }

            compressed = buffer.ToArray();
        }

        var transport = new Transport
        {
            Reply = (_, c) => new(200, "text/plain", new MemoryStream(compressed), new[] { new KeyValuePair<string, string>("Content-Encoding", "gzip"), new KeyValuePair<string, string>("Content-Length", compressed.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)) }, c)
        };
        var report = Run(c =>
        {
            using var s = new HtmlResourceLoader(Policy(bytes: 8192), new Names(), transport).OpenSession(c);
            var result = s.Resolve(Request(), c)!;
            Assert.Equal(8192, result.Content.Length);
            Assert.DoesNotContain(result.Headers, h => h.Key == "Content-Encoding" || h.Key == "Content-Length");
        });
        Assert.True(report.Resources.Single(r => r.Resource == PdfResourceKind.InputBytes).Consumed >= compressed.Length);
        Assert.Equal("Html.ResponseExtent", Code(c =>
        {
            using var s = new HtmlResourceLoader(Policy(bytes: 8191), new Names(), transport).OpenSession(c);
            s.Resolve(Request(), c);
        }));
        Assert.Equal("Limit.ExpandedBytes", Code(c =>
        {
            using var s = new HtmlResourceLoader(Policy(), new Names(), transport).OpenSession(c);
            s.Resolve(Request(), c);
        }, new(limits: new(maximumExpandedBytes: 4096))));
    }

    [Fact]
    public void PermissionDenialsStayFatalInPermissiveModeAndDiagnosticsContainNoPrivateData()
    {
        var failure = Assert.Throws<PdfException>(() => Run(c =>
        {
            using var s = new HtmlResourceLoader(Policy(), new Names(), new Transport()).OpenSession(c);
            s.Resolve(new(new("https://denied.invalid/?token=private-secret"), "1"), c);
        }, new(PdfProcessingMode.Permissive)));
        Assert.Equal("Html.OriginDenied", failure.Diagnostic.Code);
        Assert.DoesNotContain("private-secret", failure.ToString());
        Assert.DoesNotContain("denied.invalid", failure.ToString());
        var transport = new Transport
        {
            Reply = (_, c) => new(404, "text/plain", new MemoryStream(Encoding.UTF8.GetBytes("private-body")), null, c)
        };
        var report = Run(c =>
        {
            using var s = new HtmlResourceLoader(Policy(), new Names(), transport).OpenSession(c);
            Assert.Equal(404, s.Resolve(Request(), c)!.StatusCode);
        }, new(PdfProcessingMode.Permissive));
        Assert.Contains(report.Diagnostics, d => d.Code == "Html.ResourceStatus");
        Assert.All(report.Diagnostics, d => Assert.DoesNotContain("private", d.Message));
    }

    [Fact]
    public void NameProviderFailuresAreRedactedAndCookieOverflowCannotSilentlyEvictState()
    {
        var names = new Names
        {
            Answers = _ => throw new InvalidOperationException("private-provider-secret")
        };
        var failure = Assert.Throws<PdfException>(() => Run(c =>
        {
            using var s = new HtmlResourceLoader(Policy(), names, new Transport()).OpenSession(c);
            s.Resolve(Request(), c);
        }));
        Assert.Equal("Html.DnsProvider", failure.Diagnostic.Code);
        Assert.DoesNotContain("private-provider-secret", failure.ToString());
        var initial = Enumerable.Range(0, 128).Select(i => "c" + i + "=original; Path=/").ToArray();
        var policy = new HtmlNetworkPolicy(new[] { new HtmlOriginRule(A, initialCookies: initial) });
        var transport = new Transport
        {
            Reply = (_, c) => new(200, "text/plain", new MemoryStream(), new[] { new KeyValuePair<string, string>("Set-Cookie", "overflow=original; Path=/") }, c)
        };
        Assert.Equal("Html.CookieExtent", Code(c =>
        {
            using var s = new HtmlResourceLoader(policy, new Names(), transport).OpenSession(c);
            s.Resolve(Request(), c);
        }));
    }

    [Theory]
    [InlineData("data:text/plain;base64,QQD/", "text/plain", "4100FF")]
    [InlineData("data:,%41%00%ff+", "text/plain;charset=US-ASCII", "4100FF2B")]
    [InlineData("data:;charset=utf-8,%E4%B8%80", "text/plain;charset=utf-8", "E4B880")]
    public void DataUrisCaptureExactOctetsWithoutTextGuessing(string input, string media, string hex)
    {
        var resource = HtmlAssets.ReadDataUri(input, A);
        Assert.Equal(media, resource.MediaType);
        Assert.Equal(hex, BitConverter.ToString(Bytes(resource)).Replace("-", ""));
    }

    [Theory]
    [InlineData("data:,private%")]
    [InlineData("data:,private%xx")]
    [InlineData("data:text/plain;base64,QQ")]
    [InlineData("data:text/plain;base64,AA A")]
    [InlineData("data:,raw一")]
    [InlineData("data:,private#fragment")]
    public void DataUriErrorsOmitPayload(string input)
    {
        var failure = Assert.Throws<PdfException>(() => HtmlAssets.ReadDataUri(input, A));
        Assert.Equal("Html.DataUriSyntax", failure.Diagnostic.Code);
        Assert.DoesNotContain("private", failure.ToString());
    }

    [Fact]
    public void ExplicitProtectedFilesAreDetachedAndPathTraversalIsRejected()
    {
        string root = Path.Combine(AppContext.BaseDirectory, "fluent-html-assets-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        string file = Path.Combine(root, "original.txt");
        File.WriteAllBytes(file, new byte[] { 0, 255, 42 });
        try
        {
            var resource = HtmlAssets.ReadFile(root, "original.txt", A, "application/octet-stream");
            File.WriteAllText(file, "changed");
            Assert.Equal(new byte[] { 0, 255, 42 }, Bytes(resource));
            foreach (string path in new[]
            {
                "../escape",
                "sub/../../escape",
                "/absolute",
                "C:\\escape",
                "%2e%2e/escape",
                "sub\\..\\escape",
                "original.txt:stream"
            }

            )
                Assert.Equal("Html.LocalPathDenied", Assert.Throws<PdfException>(() => HtmlAssets.ReadFile(root, path, A, "text/plain")).Diagnostic.Code);
        }
        finally
        {
            File.Delete(file);
            Directory.Delete(root);
        }
    }

    [Fact]
    public void CancellationAndSessionDisposalDoNotLeaveWorkOrAllowReuse()
    {
        using var cancel = new CancellationTokenSource();
        var names = new Names();
        var transport = new Transport();
        Assert.Throws<OperationCanceledException>(() => Run(c =>
        {
            using var s = new HtmlResourceLoader(Policy(), names, transport).OpenSession(c);
            cancel.Cancel();
            s.Resolve(Request(), c);
        }, token: cancel.Token));
        Assert.Equal(0, names.Calls);
        Assert.Empty(transport.Requests);
        Run(c =>
        {
            var s = new HtmlResourceLoader().OpenSession(c);
            s.Dispose();
            Assert.Throws<ObjectDisposedException>(() => s.Resolve(Request(), c));
        });
    }

    [Fact]
    public void PortableTransportSupportIsExplicitAndNeverFallsBackToUnpinnedHttp()
    {
#if NET8_0_OR_GREATER && !TEST_NETSTANDARD_ASSEMBLY
        Assert.True(new PinnedHtmlHttpTransport().IsSupported);
#else
        Assert.False(new PinnedHtmlHttpTransport().IsSupported);
        var local = new Uri("http://127.0.0.1:1/");
        var policy = new HtmlNetworkPolicy(new[] { new HtmlOriginRule(local) }, new[] { "127.0.0.1" });
        Assert.Equal("Html.HttpTransportRuntime", Code(c =>
        {
            using var s = new HtmlResourceLoader(policy).OpenSession(c);
            s.Resolve(new(local, "1"), c);
        }));
#endif
    }
}
