using System.IO;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Fluent.Pdf.Html;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class HtmlHttpTransportTests
{
    [Fact]
    public void CertificateLessWindowsTlsRequiresAnExplicitQualifiedIsolatedTransport()
    {
#if NET8_0_OR_GREATER && !TEST_NETSTANDARD_ASSEMBLY
        if (OperatingSystem.IsWindows())
        {
            var origin = new Uri("https://127.0.0.1:1/");
            var policy = new HtmlNetworkPolicy(new[] { new HtmlOriginRule(origin) }, new[] { "127.0.0.1" });
            var failure = Assert.Throws<PdfException>(() => Capture(origin, policy));
            Assert.Equal("Html.TlsIsolationRequired", failure.Diagnostic.Code);
        }
        else
            Assert.True(new PinnedHtmlHttpTransport().IsSupported);
#else
        Assert.False(new PinnedHtmlHttpTransport().IsSupported);
#endif
    }

    [Fact]
    public async Task ActualPinnedHttpUsesExplicitHeadersAndReadsExactBytes()
    {
#if NET8_0_OR_GREATER && !TEST_NETSTANDARD_ASSEMBLY
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        int port = ((IPEndPoint)listener.LocalEndpoint).Port;
        var origin = new Uri("http://original.example.invalid:" + port + "/");
        Task server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = client.GetStream();
            string request = await Header(stream, timeout.Token);
            Assert.Contains("Host: original.example.invalid:" + port, request);
            Assert.Contains("Authorization: Bearer original-fixture", request);
            Assert.DoesNotContain("Proxy-Authorization", request);
            await Reply(stream, timeout.Token);
        }, timeout.Token);
        var policy = new HtmlNetworkPolicy(new[] { new HtmlOriginRule(origin, new[] { new KeyValuePair<string, string>("Authorization", "Bearer original-fixture") }) }, new[] { "127.0.0.1" });
        try
        {
            Capture(origin, policy);
        }
        finally
        {
            listener.Stop();
        }

        await server;
#else
        await Task.CompletedTask;
        Assert.False(new PinnedHtmlHttpTransport().IsSupported);
#endif
    }

    [Theory]
    [InlineData("valid")]
    [InlineData("pin")]
    [InlineData("name")]
    public async Task ActualTlsRequiresThePinnedServerNameAndExplicitClientCertificate(string condition)
    {
#if NET8_0_OR_GREATER && !TEST_NETSTANDARD_ASSEMBLY
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var serverKey = RSA.Create(2048);
        using var clientKey = RSA.Create(2048);
        var request = new CertificateRequest("CN=original.example.invalid", serverKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        var names = new SubjectAlternativeNameBuilder();
        names.AddDnsName("original.example.invalid");
        request.CertificateExtensions.Add(names.Build());
        request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.1") }, false));
        using var generatedServer = request.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        using var serverCertificate = TransportIdentity(generatedServer);
        var identity = new CertificateRequest("CN=Original explicit client", clientKey, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        identity.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection { new("1.3.6.1.5.5.7.3.2") }, false));
        using var generatedClient = identity.CreateSelfSigned(DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddDays(1));
        using var clientCertificate = TransportIdentity(generatedClient);
        string serverPin = Hash(serverCertificate), clientPin = Hash(clientCertificate);
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var origin = new Uri("https://" + (condition == "name" ? "different.example.invalid" : "original.example.invalid") + ":" + ((IPEndPoint)listener.LocalEndpoint).Port + "/");
        Task server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            using var wire = client.GetStream();
            using var tls = new SslStream(wire, true, (_, certificate, _, _) => certificate != null && Hash(certificate) == clientPin);
            try
            {
                await tls.AuthenticateAsServerAsync(new SslServerAuthenticationOptions { ServerCertificate = serverCertificate, ClientCertificateRequired = true, EnabledSslProtocols = SslProtocols.Tls12, CertificateRevocationCheckMode = X509RevocationMode.NoCheck }, timeout.Token);
                Assert.True(tls.IsMutuallyAuthenticated);
                await Header(tls, timeout.Token);
                await Reply(tls, timeout.Token);
            }
            catch (AuthenticationException)when (condition != "valid")
            {
            }
            catch (IOException)when (condition != "valid")
            {
            }
        }, timeout.Token);
        var policy = new HtmlNetworkPolicy(new[] { new HtmlOriginRule(origin, serverCertificateSha256: condition == "pin" ? new string ('0', 64) : serverPin, clientCertificate: clientCertificate) }, new[] { "127.0.0.1" });
        PdfException? requestFailure = null;
        try
        {
            if (condition == "valid")
            {
                try
                {
                    Capture(origin, policy);
                }
                catch (PdfException failure)
                {
                    requestFailure = failure;
                }
            }
            else
            {
                var failure = Assert.Throws<PdfException>(() => Capture(origin, policy));
                Assert.Equal("Html.HttpTransport", failure.Diagnostic.Code);
                Assert.IsType<int>(failure.Data["Fluent.Pdf.Html.TransportErrorCode"]);
            }
        }
        finally
        {
            listener.Stop();
        }

        string? serverFailure = null;
        try
        {
            await server;
        }
        catch (Exception failure)when (requestFailure != null)
        {
            serverFailure = failure.GetType().Name;
        }

        if (requestFailure != null)
        {
            object? code = requestFailure.Data["Fluent.Pdf.Html.TransportErrorCode"];
            throw new Xunit.Sdk.XunitException("Original loopback TLS failed; sanitized platform code=" + (code is int number ? number.ToString("X8", System.Globalization.CultureInfo.InvariantCulture) : "unavailable") + "; joined server outcome=" + (serverFailure ?? "completed") + ".");
        }

        Assert.True(clientCertificate.HasPrivateKey);
#else
        await Task.CompletedTask;
        Assert.Contains(condition, new[] { "valid", "pin", "name" });
        Assert.False(new PinnedHtmlHttpTransport().IsSupported);
#endif
    }

    [Fact]
    public async Task CancellationDuringAStalledBodyClosesTheOwnedConnection()
    {
#if NET8_0_OR_GREATER && !TEST_NETSTANDARD_ASSEMBLY
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        using var cancel = new CancellationTokenSource();
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var origin = new Uri("http://original.example.invalid:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/");
        var policy = new HtmlNetworkPolicy(new[] { new HtmlOriginRule(origin) }, new[] { "127.0.0.1" });
        long cancelledAt = 0;
        Task server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            using var wire = client.GetStream();
            await Header(wire, timeout.Token);
            await wire.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Length: 1000\r\n\r\n"), timeout.Token);
            await wire.FlushAsync(timeout.Token);
            Interlocked.Exchange(ref cancelledAt, System.Diagnostics.Stopwatch.GetTimestamp());
            cancel.Cancel();
            var one = new byte[1];
            try
            {
                Assert.Equal(0, await wire.ReadAsync(one, timeout.Token));
            }
            catch (IOException)
            { /* A reset also proves the client closed the owned connection. */
            }
        }, timeout.Token);
        Assert.Throws<OperationCanceledException>(() => PdfFont.Run("OriginalBodyCancellation", "original.fixture", null, cancel.Token, c =>
        {
            using var s = new HtmlResourceLoader(policy, new Loopback()).OpenSession(c);
            s.Resolve(new(origin, "1"), c);
        }, PdfCapability.RenderHtml));
        TimeSpan cancellationElapsed = System.Diagnostics.Stopwatch.GetElapsedTime(Interlocked.Read(ref cancelledAt));
        listener.Stop();
        await server;
        Assert.True(cancellationElapsed < TimeSpan.FromSeconds(1));
#else
        await Task.CompletedTask;
        Assert.False(new PinnedHtmlHttpTransport().IsSupported);
#endif
    }

    [Fact]
    public async Task ExplicitProxyReceivesNumericConnectAndKeepsItsCredentialsOutOfOriginHeaders()
    {
#if NET8_0_OR_GREATER && !TEST_NETSTANDARD_ASSEMBLY
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var origin = new Uri("http://original.example.invalid:8087/");
        Task server = Task.Run(async () =>
        {
            using var client = await listener.AcceptTcpClientAsync(timeout.Token);
            using var stream = client.GetStream();
            string connect = await Header(stream, timeout.Token);
            Assert.StartsWith("CONNECT 127.0.0.1:8087 HTTP/1.1\r\n", connect);
            Assert.Contains("Proxy-Authorization: Basic original-proxy", connect);
            await stream.WriteAsync(Encoding.ASCII.GetBytes("HTTP/1.1 200 Connection Established\r\n\r\n"), timeout.Token);
            string request = await Header(stream, timeout.Token);
            Assert.StartsWith("GET / HTTP/1.1\r\n", request);
            Assert.DoesNotContain("Proxy-Authorization", request);
            await Reply(stream, timeout.Token);
        }, timeout.Token);
        var proxy = new HtmlProxySettings(new("http://127.0.0.1:" + ((IPEndPoint)listener.LocalEndpoint).Port), "Basic original-proxy");
        var policy = new HtmlNetworkPolicy(new[] { new HtmlOriginRule(origin) }, new[] { "127.0.0.1" }, proxy: proxy);
        try
        {
            Capture(origin, policy);
        }
        finally
        {
            listener.Stop();
        }

        await server;
#else
        await Task.CompletedTask;
        Assert.False(new PinnedHtmlHttpTransport().IsSupported);
#endif
    }

#if NET8_0_OR_GREATER && !TEST_NETSTANDARD_ASSEMBLY
    private sealed class Loopback : IHtmlNameResolver
    {
        public IReadOnlyList<IPAddress> Resolve(string host, PdfOperationContext context)
        {
            context.Checkpoint();
            return new[]
            {
                IPAddress.Loopback
            };
        }
    }

    private static void Capture(Uri origin, HtmlNetworkPolicy policy) => PdfFont.Run("OriginalHttpFixture", "original.fixture", new(limits: new(maximumElapsedTime: TimeSpan.FromSeconds(8))), default, c =>
    {
        using var session = new HtmlResourceLoader(policy, new Loopback()).OpenSession(c);
        var result = session.Resolve(new(origin, "original-request"), c)!;
        Assert.Equal(200, result.StatusCode);
        using var raw = result.Content.OpenContent();
        using var copy = new MemoryStream();
        raw.CopyTo(copy);
        Assert.Equal("Original exact 一😀", Encoding.UTF8.GetString(copy.ToArray()));
    }, PdfCapability.RenderHtml);
    // Original fixture keys only. Schannel gets explicitly imported disposable identities; the SDK never exports caller keys.
    private static X509Certificate2 TransportIdentity(X509Certificate2 generated)
    {
        if (!OperatingSystem.IsWindows())
            return new X509Certificate2(generated);
        byte[] encoded = generated.Export(X509ContentType.Pkcs12, "Original fixture identity");
        try
        {
#if NET9_0_OR_GREATER
            return X509CertificateLoader.LoadPkcs12(encoded, "Original fixture identity", X509KeyStorageFlags.DefaultKeySet);
#else
            return new X509Certificate2(encoded, "Original fixture identity", X509KeyStorageFlags.DefaultKeySet);
#endif
        }
        finally
        {
            CryptographicOperations.ZeroMemory(encoded);
        }
    }

    private static string Hash(X509Certificate certificate) => Convert.ToHexString(SHA256.HashData(certificate.GetRawCertData())).ToLowerInvariant();
    private static async Task<string> Header(Stream stream, CancellationToken token)
    {
        using var buffer = new MemoryStream();
        var one = new byte[1];
        while (buffer.Length < 32768)
        {
            if (await stream.ReadAsync(one, token) == 0)
                throw new EndOfStreamException();
            buffer.WriteByte(one[0]);
            var bytes = buffer.GetBuffer();
            int n = (int)buffer.Length;
            if (n >= 4 && bytes[n - 4] == 13 && bytes[n - 3] == 10 && bytes[n - 2] == 13 && bytes[n - 1] == 10)
                return Encoding.ASCII.GetString(buffer.ToArray());
        }

        throw new InvalidDataException("Original fixture header exceeded its bound.");
    }

    private static async Task Reply(Stream stream, CancellationToken token)
    {
        byte[] bytes = Encoding.UTF8.GetBytes("Original exact 一😀");
        byte[] header = Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: text/plain; charset=utf-8\r\nContent-Length: " + bytes.Length + "\r\nConnection: close\r\n\r\n");
        await stream.WriteAsync(header, token);
        await stream.WriteAsync(bytes, token);
        await stream.FlushAsync(token);
    }
#endif
}
