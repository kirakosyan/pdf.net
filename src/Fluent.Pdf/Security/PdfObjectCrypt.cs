using System.IO;
using System.Security.Cryptography;

namespace Fluent.Pdf;
internal interface IPdfFileSecurity : IDisposable
{
    PdfValue Dictionary { get; }

    byte[] Key { get; }

    int Version { get; }

    bool PublicKey { get; }

    bool EncryptMetadata { get; }

    PdfAuthenticationRole Role { get; }

    uint PermissionBits { get; }

    int Revision { get; }

    string Description { get; }
}

internal sealed class PdfObjectCrypt
{
    private enum Mode
    {
        Identity,
        Arcfour,
        Aes128,
        Aes256
    }

    private sealed class Filter
    {
        internal readonly Mode Mode;
        internal readonly bool EmbeddedOnly;
        internal Filter(Mode mode, bool embedded = false)
        {
            Mode = mode;
            EmbeddedOnly = embedded;
        }
    }

    private readonly IPdfFileSecurity _security;
    private readonly PdfObjectId _encryptionId;
    private readonly PdfOperationContext _context;
    private readonly bool _allowLegacy;
    private readonly Dictionary<string, Filter> _filters = new(StringComparer.Ordinal);
    private readonly Filter _strings, _streams, _embedded;
    internal PdfObjectCrypt(IPdfFileSecurity security, PdfObjectId encryptionId, bool allowLegacy, PdfOperationContext context)
    {
        _security = security;
        _encryptionId = encryptionId;
        _allowLegacy = allowLegacy;
        _context = context;
        context.Consume(PdfResourceKind.ExpandedBytes, 512);
        if (security.Version < 4)
            _strings = _streams = _embedded = Legacy();
        else
        {
            _strings = Get(Choice("StrF", "Identity"));
            _streams = Get(Choice("StmF", "Identity"));
            _embedded = Get(Choice("EFF", Choice("StmF", "Identity")));
            if (_strings.EmbeddedOnly || _streams.EmbeddedOnly)
                throw Error("AuthEvent", "An embedded-file-only crypt filter cannot protect ordinary strings or streams.");
        }
    }

    internal PdfValue Transform(PdfValue value, PdfObjectId id, bool encrypt, Func<PdfValue, PdfValue> resolve)
    {
        if (id == _encryptionId || value.Kind == PdfValueKind.Stream && Name(Entry(value, "Type")) == "XRef")
            return value;
        return Visit(value);
        PdfValue Entry(PdfValue dictionary, string key) => dictionary.Entries.TryGetValue(key, out var entry) ? resolve(entry) : PdfValue.Null;
        PdfValue Visit(PdfValue item)
        {
            _context.Checkpoint();
            using var depth = _context.EnterScope(PdfResourceKind.Depth);
            _context.Consume(PdfResourceKind.Objects, 1);
            if (item.Kind == PdfValueKind.String)
            {
                if (_strings.Mode == Mode.Identity)
                    return item;
                _context.Consume(PdfResourceKind.ExpandedBytes, item.StringBytes.Count);
                var source = item.StringBytes.ToArray();
                try
                {
                    return PdfValue.OwnedString(Bytes(source, _strings, id, encrypt));
                }
                finally
                {
                    Clear(source);
                }
            }

            if (item.Kind == PdfValueKind.Array)
            {
                _context.Consume(PdfResourceKind.ExpandedBytes, item.Items.Count * 16L + 64);
                var children = new List<PdfValue>(item.Items.Count);
                foreach (var child in item.Items)
                    children.Add(Visit(child));
                return PdfValue.OwnedArray(children);
            }

            if (item.Kind != PdfValueKind.Dictionary && item.Kind != PdfValueKind.Stream)
                return item;
            var entries = PdfWriterCore.CopyDictionary(item.Entries, _context);
            string type = Name(Entry(item, "Type"));
            bool signature = type == "Sig" || type == "DocTimeStamp" || item.Entries.ContainsKey("ByteRange") && (item.Entries.ContainsKey("Filter") || item.Entries.ContainsKey("SubFilter"));
            foreach (var pair in item.Entries)
                if (!(signature && pair.Key == "Contents"))
                    entries[pair.Key] = Visit(pair.Value);
            if (item.Kind != PdfValueKind.Stream)
                return PdfValue.OwnedDictionary(entries);
            bool embedded = type == "EmbeddedFile";
            var selected = embedded ? _embedded : _streams;
            bool explicitFilter = StreamFilter(item, entries, out var declared);
            if (explicitFilter)
                selected = declared!;
            if (type == "Metadata" && !_security.EncryptMetadata)
            {
                if (explicitFilter && selected.Mode != Mode.Identity)
                    throw Error("MetadataCryptProfile", "An explicitly encrypted metadata stream with plaintext metadata policy requires another profile.");
                selected = new(Mode.Identity);
            }

            if (selected.EmbeddedOnly && !embedded)
                throw Error("AuthEvent", "An embedded-file-only crypt filter was requested for another stream type.");
            if (selected.Mode == Mode.Identity)
                return item.WithStreamDictionary(PdfValue.OwnedDictionary(entries));
            if (item.RawStreamLength > int.MaxValue)
                throw Error("CipherExtent", "The stream exceeds the supported cryptographic array extent.");
            _context.Consume(PdfResourceKind.ExpandedBytes, item.RawStreamLength);
            var input = new byte[(int)item.RawStreamLength];
            byte[]? result = null;
            try
            {
                using var raw = item.OpenRawStream();
                int at = 0;
                while (at < input.Length)
                {
                    _context.Checkpoint();
                    int n = raw.Read(input, at, Math.Min(65536, input.Length - at));
                    if (n == 0)
                        throw Error("CipherExtent", "The stream ended before its declared cryptographic extent.");
                    at += n;
                }

                result = Bytes(input, selected, id, encrypt);
                using var output = new MemoryStream(result, false);
                var store = PdfByteStore.Read(output, _context);
                entries["Length"] = PdfValue.Integer(store.Length);
                return PdfValue.StreamValue(PdfValue.OwnedDictionary(entries), store, 0, store.Length);
            }
            finally
            {
                Clear(input);
                Clear(result);
            }
        }

        bool StreamFilter(PdfValue stream, Dictionary<string, PdfValue> entries, out Filter? selected)
        {
            selected = null;
            var filter = Entry(stream, "Filter");
            if (filter.Kind == PdfValueKind.Null)
                return false;
            IReadOnlyList<PdfValue> filters;
            if (filter.Kind == PdfValueKind.Name)
            {
                _context.Consume(PdfResourceKind.ExpandedBytes, 16);
                filters = new[]
                {
                    filter
                };
            }
            else if (filter.Kind == PdfValueKind.Array)
                filters = filter.Items;
            else
                throw Error("CryptFilter", "Stream filters must be names or an array of names.");
            int index = -1;
            for (int i = 0; i < filters.Count; i++)
            {
                var current = resolve(filters[i]);
                if (current.Kind != PdfValueKind.Name)
                    throw Error("CryptFilter", "A stream filter must resolve to a name.");
                if (current.NameValue == "Crypt")
                {
                    if (index >= 0 || i != 0)
                        throw Error("CryptFilter", "Crypt must occur once and first in a stream filter chain.");
                    index = i;
                }
            }

            if (index < 0)
                return false;
            var parameters = Entry(stream, "DecodeParms");
            PdfValue cryptParameters;
            if (parameters.Kind == PdfValueKind.Array)
            {
                if (parameters.Items.Count != filters.Count)
                    throw Error("CryptFilter", "Crypt filter parameters must match the filter chain extent.");
                cryptParameters = resolve(parameters.Items[0]);
            }
            else if (parameters.Kind == PdfValueKind.Null || filters.Count == 1 && parameters.Kind == PdfValueKind.Dictionary)
                cryptParameters = parameters;
            else
                throw Error("CryptFilter", "Crypt filter parameters have an invalid shape.");
            if (cryptParameters.Kind != PdfValueKind.Null && cryptParameters.Kind != PdfValueKind.Dictionary)
                throw Error("CryptFilter", "Crypt decode parameters must resolve to a dictionary or null.");
            var named = cryptParameters.Kind == PdfValueKind.Null ? PdfValue.Null : Entry(cryptParameters, "Name");
            if (named.Kind != PdfValueKind.Null && named.Kind != PdfValueKind.Name)
                throw Error("CryptFilter", "A crypt filter selection must resolve to a name.");
            selected = Get(named.Kind == PdfValueKind.Null ? "Identity" : named.NameValue);
            if (encrypt)
                return true; // The explicit override must remain in encrypted output to prevent default-filter decryption.
            _context.Consume(PdfResourceKind.ExpandedBytes, filters.Count * 32L + 128);
            if (filters.Count == 1)
            {
                entries.Remove("Filter");
                entries.Remove("DecodeParms");
            }
            else
            {
                entries["Filter"] = PdfValue.OwnedArray(filters.Skip(1).ToList());
                if (parameters.Kind == PdfValueKind.Array)
                    entries["DecodeParms"] = PdfValue.OwnedArray(parameters.Items.Skip(1).ToList());
                else
                    entries.Remove("DecodeParms");
            }

            return true;
        }
    }

    private byte[] Bytes(byte[] input, Filter filter, PdfObjectId id, bool encrypt)
    {
        byte[] key = Array.Empty<byte>(), iv = Array.Empty<byte>(), data = Array.Empty<byte>();
        byte[]? transformed = null;
        try
        {
            key = ObjectKey(filter.Mode, id);
            if (filter.Mode == Mode.Arcfour)
                return PdfSecurityCryptography.Arcfour(key, input, _context);
            if (encrypt)
            {
                iv = PdfSecurityCryptography.Random(16, _context);
                transformed = PdfSecurityCryptography.Aes(key, iv, input, true, CipherMode.CBC, PaddingMode.PKCS7, _context);
                return PdfSecurityCryptography.Concat(_context, iv, transformed);
            }

            if (input.Length < 32 || (input.Length - 16) % 16 != 0)
                throw Error("CipherExtent", "An AES string or stream requires an IV and complete padded blocks.");
            iv = PdfSecurityCryptography.Slice(input, 0, 16, _context);
            data = PdfSecurityCryptography.Slice(input, 16, input.Length - 16, _context);
            return PdfSecurityCryptography.Aes(key, iv, data, false, CipherMode.CBC, PaddingMode.PKCS7, _context);
        }
        finally
        {
            Clear(key);
            Clear(iv);
            Clear(data);
            Clear(transformed);
        }
    }

    private byte[] ObjectKey(Mode mode, PdfObjectId id)
    {
        if (mode == Mode.Aes256)
            return PdfSecurityCryptography.Slice(_security.Key, 0, _security.Key.Length, _context);
        _context.Consume(PdfResourceKind.ExpandedBytes, 9);
        var suffix = new byte[mode == Mode.Aes128 ? 9 : 5];
        for (int i = 0; i < 3; i++)
            suffix[i] = (byte)(id.Number >> (8 * i));
        suffix[3] = (byte)id.Generation;
        suffix[4] = (byte)(id.Generation >> 8);
        if (mode == Mode.Aes128)
        {
            suffix[5] = 115;
            suffix[6] = 65;
            suffix[7] = 108;
            suffix[8] = 84;
        }

        byte[]? seed = null, hash = null;
        try
        {
            seed = PdfSecurityCryptography.Concat(_context, _security.Key, suffix);
            hash = PdfSecurityCryptography.Hash(seed, 128, _context);
            return PdfSecurityCryptography.Slice(hash, 0, Math.Min(_security.Key.Length + 5, 16), _context);
        }
        finally
        {
            Clear(seed);
            Clear(hash);
            Clear(suffix);
        }
    }

    private Filter Get(string name)
    {
        _context.Checkpoint();
        if (name == "Identity")
            return new(Mode.Identity);
        if (_filters.TryGetValue(name, out var cached))
            return cached;
        if (_filters.Count >= 64)
            throw Error("CryptFilter", "The active crypt filter inventory exceeds the supported extent.");
        var dictionary = _security.Dictionary;
        if (!dictionary.Entries.TryGetValue("CF", out var filters) || filters.Kind != PdfValueKind.Dictionary || !filters.Entries.TryGetValue(name, out var entry) || entry.Kind != PdfValueKind.Dictionary)
            throw Error("CryptFilter", "A requested crypt filter must have a direct dictionary declaration.");
        string method = entry.Entries.TryGetValue("CFM", out var value) && value.Kind != PdfValueKind.Null ? value.Kind == PdfValueKind.Name ? value.NameValue : "" : "None";
        Mode mode = method switch
        {
            "None" => Mode.Identity,
            "V2" => Mode.Arcfour,
            "AESV2" => Mode.Aes128,
            "AESV3" => Mode.Aes256,
            _ => throw Error("CryptFilter", "The requested crypt filter algorithm is unsupported.")};
        if (mode == Mode.Arcfour && !_allowLegacy)
            throw Error("LegacyReadBlocked", "Legacy content encryption requires explicit read permission.");
        if (mode == Mode.Aes128 && _security.Key.Length != 16 || mode == Mode.Aes256 && _security.Key.Length != 32 || mode == Mode.Arcfour && (_security.Key.Length < 5 || _security.Key.Length > 16) || _security.Version == 4 && mode == Mode.Aes256 || _security.Version == 5 && mode != Mode.Identity && mode != Mode.Aes256)
            throw Error("CryptFilter", "The selected crypt filter and file key are inconsistent.");
        if (mode != Mode.Identity && entry.Entries.TryGetValue("Length", out var length) && length.Kind != PdfValueKind.Null && (!length.TryGetInteger(out long n) || (_security.PublicKey ? n != _security.Key.Length * 8 : n != _security.Key.Length && n != _security.Key.Length * 8)))
            throw Error("KeyLength", "The crypt filter length does not match the handler's file key.");
        string auth = entry.Entries.TryGetValue("AuthEvent", out var authValue) && authValue.Kind != PdfValueKind.Null ? authValue.Kind == PdfValueKind.Name ? authValue.NameValue : "" : "DocOpen";
        if (auth != "DocOpen" && auth != "EFOpen")
            throw Error("AuthEvent", "The crypt filter authentication event is unsupported.");
        _context.Consume(PdfResourceKind.ExpandedBytes, name.Length * 2L + 128);
        var result = new Filter(mode, auth == "EFOpen");
        _filters.Add(name, result);
        return result;
    }

    private Filter Legacy()
    {
        if (!_allowLegacy)
            throw Error("LegacyReadBlocked", "Legacy content encryption requires explicit read permission.");
        return new(Mode.Arcfour);
    }

    private string Choice(string name, string fallback)
    {
        if (!_security.Dictionary.Entries.TryGetValue(name, out var value) || value.Kind == PdfValueKind.Null)
            return fallback;
        return value.Kind == PdfValueKind.Name ? value.NameValue : throw Error("CryptFilter", "A default crypt filter must be a direct name.");
    }

    private static string Name(PdfValue value) => value.Kind == PdfValueKind.Name ? value.NameValue : "";
    private static void Clear(byte[]? value) => PdfSecurityCryptography.Clear(value);
    private PdfException Error(string code, string message) => PdfWriterCore.Error(_context, "Security." + code, message);
}
