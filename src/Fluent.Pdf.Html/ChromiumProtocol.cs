using System.Text;
using System.Text.Json;
using System.IO;

namespace Fluent.Pdf.Html;
// Original bounded synchronous DevTools dispatch. Intercepted requests receive asynchronous acknowledgements while another command is awaited.
internal sealed class ChromiumProtocol
{
    private readonly IChromiumConnection _connection;
    private readonly PdfOperationContext _context;
    private readonly HashSet<int> _acknowledgements = new();
    private int _next;
    internal Action<JsonElement>? Notification;
    internal ChromiumProtocol(IChromiumConnection connection, PdfOperationContext context)
    {
        _connection = connection;
        _context = context;
    }

    internal static Dictionary<string, object?> P(params (string Key, object? Value)[] pairs) => pairs.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
    internal JsonDocument Call(string method, Dictionary<string, object?>? parameters = null, string? session = null)
    {
        int id = Send(method, parameters, session);
        while (true)
        {
            _context.Checkpoint();
            var document = Read();
            bool returned = false;
            try
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                    throw Error("Protocol", "A browser message must be a JSON object.");
                if (root.TryGetProperty("id", out var response))
                {
                    if (response.ValueKind != JsonValueKind.Number || !response.TryGetInt32(out int received) || received != id && !_acknowledgements.Remove(received))
                        throw Error("ResponseIdentity", "The browser returned an unexpected command identity.");
                    if (root.TryGetProperty("error", out _))
                        throw Error("CommandRejected", "The pinned browser rejected a required DevTools command.");
                    if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
                        throw Error("Protocol", "A complete browser command result is required.");
                    if (received == id)
                    {
                        returned = true;
                        return document;
                    }
                }
                else
                    Notification?.Invoke(root);
            }
            finally
            {
                if (!returned)
                    document.Dispose();
            }
        }
    }

    internal void Acknowledge(string method, Dictionary<string, object?> parameters, string session)
    {
        _context.Consume(PdfResourceKind.Objects, 1);
        _context.Consume(PdfResourceKind.ExpandedBytes, 64);
        _acknowledgements.Add(Send(method, parameters, session));
    }

    internal void PumpUntil(Func<bool> complete)
    {
        while (!complete())
        {
            using var document = Read();
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw Error("Protocol", "A browser message must be a JSON object.");
            if (root.TryGetProperty("id", out var response))
            {
                if (response.ValueKind != JsonValueKind.Number || !response.TryGetInt32(out int id) || !_acknowledgements.Remove(id))
                    throw Error("ResponseIdentity", "An unexpected response arrived while waiting for browser lifecycle events.");
                if (root.TryGetProperty("error", out _))
                    throw Error("CommandRejected", "The browser rejected a resource interception command.");
                if (!root.TryGetProperty("result", out var result) || result.ValueKind != JsonValueKind.Object)
                    throw Error("Protocol", "A complete browser resource acknowledgement is required.");
            }
            else
                Notification?.Invoke(root);
            _context.Checkpoint();
        }
    }

    private int Send(string method, Dictionary<string, object?>? parameters, string? session)
    {
        _context.Checkpoint();
        if (_next == 1000000)
            throw Error("CommandExtent", "The browser command extent was exhausted.");
        int id = ++_next;
        var message = P(("id", id), ("method", method), ("params", parameters ?? P()));
        if (session != null)
            message["sessionId"] = session;
        long extent = Extent(message);
        if (extent > 67108864)
            throw Error("ProtocolExtent", "A browser command exceeds the bounded transport profile.");
        // Reserve worst-case escaping/transcoding before serialization; validate the actual frame separately.
        _context.Consume(PdfResourceKind.ExpandedBytes, extent * 32L + 1024);
        string json = JsonSerializer.Serialize(message);
        if (json.Length > 67108864)
            throw Error("ProtocolExtent", "The serialized browser command exceeds the bounded transport profile.");
        try
        {
            _connection.Send(json, _context);
        }
        catch (OperationCanceledException)
        {
            _context.Checkpoint();
            throw Error("BrowserConnection", "The private browser connection stopped unexpectedly.");
        }
        catch (IOException)
        {
            _context.Checkpoint();
            throw Error("BrowserConnection", "The private browser connection failed; transport and document data are omitted.");
        }

        _context.Checkpoint();
        return id;
    }

    private long Extent(object? value)
    {
        _context.Checkpoint();
        using var depth = _context.EnterScope(PdfResourceKind.Depth);
        _context.Consume(PdfResourceKind.Objects, 1);
        if (value is string text)
            return text.Length + 16L;
        long result = 32;
        if (value is Dictionary<string, object?> dictionary)
            foreach (var pair in dictionary)
                result += pair.Key.Length + Extent(pair.Value);
        else if (value is object[] array)
            foreach (var item in array)
                result += Extent(item);
        return result;
    }

    private JsonDocument Read()
    {
        string text;
        try
        {
            text = _connection.Receive(_context);
        }
        catch (OperationCanceledException)
        {
            _context.Checkpoint();
            throw Error("BrowserConnection", "The private browser connection stopped unexpectedly.");
        }
        catch (IOException)
        {
            _context.Checkpoint();
            throw Error("BrowserConnection", "The private browser connection closed; transport and document data are omitted.");
        }

        _context.Checkpoint();
        if (text == null || text.Length == 0 || text.Length > 67108864)
            throw Error("ProtocolExtent", "The browser message has an invalid transport extent.");
        _context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 12L + 4096);
        var scopes = new Stack<IDisposable>();
        var keys = new Stack<HashSet<string>?>();
        try
        {
            byte[] bytes = new UTF8Encoding(false, true).GetBytes(text);
            var reader = new Utf8JsonReader(bytes, new JsonReaderOptions { MaxDepth = 128 });
            while (reader.Read())
            {
                _context.Checkpoint();
                _context.Consume(PdfResourceKind.Objects, 1);
                if (reader.TokenType == JsonTokenType.StartArray || reader.TokenType == JsonTokenType.StartObject)
                {
                    _context.Consume(PdfResourceKind.ExpandedBytes, 128);
                    scopes.Push(_context.EnterScope(PdfResourceKind.Depth));
                    keys.Push(reader.TokenType == JsonTokenType.StartObject ? new HashSet<string>(StringComparer.Ordinal) : null);
                }
                else if (reader.TokenType == JsonTokenType.EndArray || reader.TokenType == JsonTokenType.EndObject)
                {
                    scopes.Pop().Dispose();
                    keys.Pop();
                }
                else if (reader.TokenType == JsonTokenType.PropertyName && !keys.Peek()!.Add(reader.GetString()!))
                    throw Error("ProtocolJson", "Duplicate browser JSON property names are rejected.");
            }

            return JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = 128 });
        }
        catch (JsonException)
        {
            throw Error("ProtocolJson", "The browser returned malformed or over-deep JSON.");
        }
        catch (EncoderFallbackException)
        {
            throw Error("ProtocolJson", "The browser returned malformed Unicode.");
        }
        finally
        {
            while (scopes.Count != 0)
                scopes.Pop().Dispose();
        }
    }

    internal PdfException Error(string code, string message) => PdfWriterCore.Error(_context, "Html." + code, message);
    internal static JsonElement Result(JsonDocument document) => document.RootElement.GetProperty("result");
    internal string String(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out var child) || child.ValueKind != JsonValueKind.String)
            throw Error("Protocol", "The browser response lacks a required string field.");
        return child.GetString()!;
    }

    internal JsonElement Object(JsonElement value, string name)
    {
        if (value.ValueKind != JsonValueKind.Object || !value.TryGetProperty(name, out var child) || child.ValueKind != JsonValueKind.Object)
            throw Error("Protocol", "The browser response lacks a required object field.");
        return child;
    }
}
