using Fluent.Pdf;
using Fluent.Pdf.Html;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

// Explicit trusted-original loopback observer. It does not qualify the browser host's OS security boundary.
internal static class ResourceFixture
{
    internal static async Task ObserveAsync(string outputRoot, ChromiumConnectionFactory host, string fontPath, string version, string revision)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var origin = new Uri("http://original.example.invalid:" + ((IPEndPoint)listener.LocalEndpoint).Port + "/");
        var requests = new List<string>();
        Task server = Task.Run(async () =>
        {
            for (int i = 0; i < 3; i++)
            {
                using var client = await listener.AcceptTcpClientAsync(timeout.Token);
                using var wire = client.GetStream();
                string request = await Header(wire, timeout.Token);
                string path = request.Split(' ')[1];
                if (!request.Contains("Authorization: Bearer original-fixture", StringComparison.Ordinal) || i != 0 && !request.Contains("Cookie: session=original-session", StringComparison.Ordinal))
                    throw new InvalidDataException("The original authentication fixture did not receive its explicit origin credentials.");
                requests.Add(path);
                string headers, text;
                if (path == "/start")
                {
                    headers = "302 Found\r\nLocation: /protected/page.html\r\nSet-Cookie: session=original-session; Path=/; HttpOnly\r\n";
                    text = "";
                }
                else if (path == "/protected/page.html")
                {
                    headers = "200 OK\r\nContent-Type: text/html; charset=utf-8\r\n";
                    text = "<!doctype html><meta charset=utf-8><style>@font-face{font-family:Original;src:url('/font.ttf')}body{margin:0;font:24px Original}</style><div>A B</div><img width=100 height=40 src='../asset.svg'>";
                }
                else if (path == "/asset.svg")
                {
                    headers = "200 OK\r\nContent-Type: image/svg+xml\r\n";
                    text = "<svg xmlns='http://www.w3.org/2000/svg' width='100' height='40'><rect width='100' height='40' fill='red'/></svg>";
                }
                else
                    throw new InvalidDataException("An unexpected original fixture resource was requested.");
                byte[] body = Encoding.UTF8.GetBytes(text);
                byte[] head = Encoding.ASCII.GetBytes("HTTP/1.1 " + headers + "Content-Length: " + body.Length + "\r\nConnection: close\r\n\r\n");
                await wire.WriteAsync(head, timeout.Token);
                await wire.WriteAsync(body, timeout.Token);
                await wire.FlushAsync(timeout.Token);
            }
        }, timeout.Token);
        try
        {
            using var input = File.OpenRead(fontPath);
            var font = PdfHtmlResource.Read(new Uri(origin, "font.ttf"), "font/ttf", input);
            var source = PdfHtmlSource.FromResource(new Uri(origin, "start"), new[] { font });
            var policy = new HtmlNetworkPolicy(new[] { new HtmlOriginRule(origin, new[] { new KeyValuePair<string, string>("Authorization", "Bearer original-fixture") }) }, new[] { "127.0.0.1" });
            var loader = new HtmlResourceLoader(policy, new Loopback());
            var renderer = new ChromiumHtmlRenderer(host, version, revision, loader.OpenSession);
            using var document = PdfHtml.Render(source, new PdfProviderRegistry(renderer), policy: new(limits: new(maximumExpandedBytes: 2147483648, maximumElapsedTime: TimeSpan.FromSeconds(30))), cancellationToken: timeout.Token);
            await server;
            string text = PdfTextExtraction.ReadPage(document.CreateSnapshot(), 0).Text;
            if (text != "A B" || document.Pages.Count != 1 || !requests.SequenceEqual(new[] { "/start", "/protected/page.html", "/asset.svg" }))
                throw new InvalidDataException("The original redirected/base-relative/authenticated fixture did not retain its expected semantics.");
            string output = Path.Combine(outputRoot, "authenticated-resources.pdf");
            document.SaveFile(output);
            using var bytes = File.OpenRead(output);
            File.WriteAllText(Path.Combine(outputRoot, "resource-observations.json"), JsonSerializer.Serialize(new { purpose = "trusted-original-source-observation-not-production-qualification", version, revision, requests, text, pageCount = document.Pages.Count, outputSha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)), productionQualified = false }, new JsonSerializerOptions { WriteIndented = true }));
        }
        finally
        {
            timeout.Cancel();
            listener.Stop();
            try
            {
                await server;
            }
            catch (OperationCanceledException)
            {
            }
            catch (SocketException)when (timeout.IsCancellationRequested)
            {
            }
        }
    }

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

    private static async Task<string> Header(Stream stream, CancellationToken token)
    {
        using var output = new MemoryStream();
        var one = new byte[1];
        while (output.Length < 32768)
        {
            if (await stream.ReadAsync(one, token) != 1)
                throw new EndOfStreamException();
            output.WriteByte(one[0]);
            int n = (int)output.Length;
            byte[] bytes = output.GetBuffer();
            if (n >= 4 && bytes[n - 4] == 13 && bytes[n - 3] == 10 && bytes[n - 2] == 13 && bytes[n - 1] == 10)
                return Encoding.ASCII.GetString(output.ToArray());
        }

        throw new InvalidDataException("The original fixture request header exceeds its bound.");
    }
}
