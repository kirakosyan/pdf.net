using System.IO;

namespace Fluent.Pdf;
internal sealed class PdfPublicKeySecurity : IPdfFileSecurity
{
    public PdfValue Dictionary { get; }
    public byte[] Key { get; private set; }
    public int Version { get; }
    public bool PublicKey => true;
    public bool EncryptMetadata { get; }
    public PdfAuthenticationRole Role { get; }
    public uint PermissionBits { get; }
    public int Revision => 0;
    public string Description => Version < 4 ? "PubSec-legacy-RC4-" + Key.Length * 8 : Version == 5 ? "PubSec-AES-256" : "PubSec-crypt-filter-128";

    private PdfPublicKeySecurity(PdfValue dictionary, byte[] key, int version, bool metadata, PdfAuthenticationRole role, uint permissions)
    {
        Dictionary = dictionary;
        Key = key;
        Version = version;
        EncryptMetadata = metadata;
        Role = role;
        PermissionBits = permissions;
    }

    internal static PdfPublicKeySecurity Create(PdfCertificateProtection protection, IPdfCmsEnvelopeProvider provider, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.Objects, protection.Recipients.Count * 4L + 32);
        context.Consume(PdfResourceKind.ExpandedBytes, protection.Recipients.Count * 512L + 4096);
        var groups = new SortedDictionary<uint, List<PdfCertificateRecipient>>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var recipient in protection.Recipients)
        {
            context.Checkpoint();
            using var certificate = recipient.OpenCertificate();
            using var lease = context.BorrowExpandedInput(certificate);
            var raw = ReadBytes(lease, 65536, context);
            byte[]? hash = null;
            try
            {
                hash = PdfSecurityCryptography.Hash(raw, 256, context);
                string fingerprint = BitConverter.ToString(hash);
                if (!identities.Add(fingerprint))
                    throw Error(context, "DuplicateRecipient", "The same public certificate was supplied more than once.");
            }
            finally
            {
                Clear(raw);
                Clear(hash);
            }

            uint permissions = recipient.OwnerAuthority ? 0xfffffffeu : 0xfffff0c0u | (uint)recipient.Permissions;
            if (protection.Cipher == PdfPublicKeyCipher.Aes256)
                permissions |= 512;
            if (!groups.TryGetValue(permissions, out var group))
                groups.Add(permissions, group = new());
            group.Add(recipient);
        }

        byte[] seed = PdfSecurityCryptography.Random(20, context);
        byte[]? key = null;
        bool transferred = false;
        var envelopes = new List<byte[]>(groups.Count);
        try
        {
            foreach (var pair in groups)
            {
                context.Consume(PdfResourceKind.ExpandedBytes, 24);
                var payload = new byte[24];
                Buffer.BlockCopy(seed, 0, payload, 0, 20);
                for (int i = 0; i < 4; i++)
                    payload[20 + i] = (byte)(pair.Key >> (24 - i * 8));
                try
                {
                    using var input = new MemoryStream(payload, false);
                    using var source = context.BorrowExpandedInput(input);
                    using var output = new PdfCodecBufferStream(context, 65536);
                    using (var destination = new BorrowedStream(output, false, context))
                        provider.Encrypt(pair.Value.AsReadOnly(), source, destination, context);
                    var encoded = output.Finish();
                    if (encoded.Length == 0)
                        throw Error(context, "CmsEnvelope", "The CMS provider returned an empty envelope.");
                    using var bytes = encoded.Open(0, encoded.Length);
                    envelopes.Add(ReadBytes(bytes, 65536, context));
                }
                finally
                {
                    Clear(payload);
                }
            }

            int version = protection.Cipher == PdfPublicKeyCipher.Aes256 ? 5 : 4;
            key = Derive(seed, envelopes, version, protection.EncryptMetadata, context);
            var recipients = PdfValue.Array(envelopes.Select(PdfValue.OwnedString).ToArray());
            var filter = D(("Type", N("CryptFilter")), ("CFM", N(version == 5 ? "AESV3" : "AESV2")), ("Length", PdfValue.Integer(version == 5 ? 256 : 128)), ("AuthEvent", N("DocOpen")), ("EncryptMetadata", PdfValue.Boolean(protection.EncryptMetadata)), ("Recipients", recipients));
            var dictionary = D(("Filter", N("Adobe.PubSec")), ("SubFilter", N("adbe.pkcs7.s5")), ("V", PdfValue.Integer(version)), ("Length", PdfValue.Integer(version == 5 ? 256 : 128)), ("P", PdfValue.Integer(-4)), ("CF", D(("DefaultCryptFilter", filter))), ("StmF", N("DefaultCryptFilter")), ("StrF", N("DefaultCryptFilter")));
            var result = new PdfPublicKeySecurity(dictionary, key, version, protection.EncryptMetadata, PdfAuthenticationRole.CreatedProtection, 0xfffffffcu);
            transferred = true;
            return result;
        }
        finally
        {
            Clear(seed);
            if (!transferred)
            {
                Clear(key);
                foreach (var envelope in envelopes)
                    Clear(envelope);
            }
        }
    }

    internal static PdfPublicKeySecurity Authenticate(PdfValue dictionary, PdfCertificateCredential credential, IPdfCmsEnvelopeProvider provider, bool allowLegacy, PdfOperationContext context)
    {
        if (dictionary.Kind != PdfValueKind.Dictionary || Name(dictionary, "Filter") != "Adobe.PubSec")
            throw Error(context, "PublicKeyProfile", "An explicit public-key security handler is required.");
        int version = Integer(dictionary, "V");
        string subFilter = Name(dictionary, "SubFilter");
        bool legacy = version == 1 || version == 2;
        int length = version == 1 && Entry(dictionary, "Length").Kind == PdfValueKind.Null ? 40 : Integer(dictionary, "Length");
        bool metadata = true;
        PdfValue recipients;
        if (legacy)
        {
            if (!allowLegacy)
                throw Error(context, "LegacyReadBlocked", "Legacy public-key encryption requires explicit read permission.");
            if (subFilter != "adbe.pkcs7.s3" && subFilter != "adbe.pkcs7.s4" || length < 40 || length > 128 || length % 8 != 0 || version == 1 && length != 40)
                throw Error(context, "PublicKeyLegacyProfile", "The legacy public-key handler parameters are unsupported.");
            recipients = Entry(dictionary, "Recipients");
        }
        else
        {
            if (subFilter != "adbe.pkcs7.s5" || version != 4 && version != 5 || length != (version == 5 ? 256 : 128))
                throw Error(context, "PublicKeyProfile", "The public-key crypt-filter handler parameters are unsupported.");
            var filters = Entry(dictionary, "CF");
            if (filters.Kind != PdfValueKind.Dictionary || filters.Entries.Count != 1)
                throw Error(context, "PublicKeyFilterProfile", "Multiple independently keyed public-key filters require another profile.");
            var declaration = filters.Entries.Single();
            string filterName = declaration.Key;
            var filter = declaration.Value;
            string method = Name(filter, "CFM");
            if (filter.Kind != PdfValueKind.Dictionary || filterName == "Identity" || method != (version == 5 ? "AESV3" : "AESV2") && !(version == 4 && method == "V2" && allowLegacy))
                throw Error(context, "PublicKeyFilterProfile", "The public-key filter must match its file cipher and legacy policy.");
            if (Name(dictionary, "StmF") != filterName || Name(dictionary, "StrF") != filterName)
                throw Error(context, "PublicKeyFilterProfile", "Embedded-only and independently selected public-key filters require another profile.");
            foreach (string selector in new[]
            {
                "StmF",
                "StrF",
                "EFF"
            }

            )
            {
                var selected = Entry(dictionary, selector);
                if (selected.Kind != PdfValueKind.Null && (selected.Kind != PdfValueKind.Name || selected.NameValue != "Identity" && selected.NameValue != filterName))
                    throw Error(context, "PublicKeyFilterProfile", "The public-key filter selection is inconsistent.");
            }

            var metadataValue = Entry(filter, "EncryptMetadata");
            metadata = metadataValue.Kind == PdfValueKind.Null || metadataValue.Kind == PdfValueKind.Boolean && metadataValue.BooleanValue;
            if (metadataValue.Kind != PdfValueKind.Null && metadataValue.Kind != PdfValueKind.Boolean)
                throw Error(context, "EncryptionDictionary", "Public-key metadata policy must be a direct boolean.");
            recipients = Entry(filter, "Recipients");
        }

        if (recipients.Kind != PdfValueKind.Array || recipients.Items.Count < 1 || recipients.Items.Count > 64)
            throw Error(context, "PublicKeyRecipients", "A bounded document-level CMS recipient array is required.");
        context.Consume(PdfResourceKind.ExpandedBytes, recipients.Items.Count * 32L + 128);
        var envelopes = new List<byte[]>(recipients.Items.Count);
        byte[]? key = null, seed = null;
        bool transferred = false;
        try
        {
            foreach (var value in recipients.Items)
            {
                if (value.Kind != PdfValueKind.String || value.StringBytes.Count < 1 || value.StringBytes.Count > 65536)
                    throw Error(context, "PublicKeyRecipients", "A recipient envelope has an invalid extent.");
                context.Consume(PdfResourceKind.ExpandedBytes, value.StringBytes.Count);
                envelopes.Add(value.StringBytes.ToArray());
            }

            uint permissions = 0;
            foreach (var envelope in envelopes)
            {
                using var input = new MemoryStream(envelope, false);
                using var source = context.BorrowExpandedInput(input);
                using var output = new PdfCodecBufferStream(context, 24);
                bool matched;
                using (var destination = new BorrowedStream(output, false, context))
                    matched = provider.TryDecrypt(credential, allowLegacy, source, destination, context);
                context.Checkpoint();
                if (!matched)
                {
                    if (output.BufferedLength != 0)
                        throw Error(context, "CmsProviderContract", "An unmatched recipient returned plaintext.");
                    continue;
                }

                if (output.BufferedLength != 24)
                    throw Error(context, "CmsSeed", "A document recipient envelope must contain a seed and permissions.");
                var data = output.Finish();
                using var plaintext = data.Open(0, data.Length);
                var payload = ReadBytes(plaintext, 24, context);
                try
                {
                    seed = PdfSecurityCryptography.Slice(payload, 0, 20, context);
                    for (int i = 0; i < 4; i++)
                        permissions = permissions << 8 | payload[20 + i];
                }
                finally
                {
                    Clear(payload);
                }

                break;
            }

            if (seed == null)
                throw Error(context, "AuthenticationFailed", "The explicit certificate does not match an encrypted document recipient.");
            key = Derive(seed, envelopes, version, metadata, context, length / 8);
            var result = new PdfPublicKeySecurity(dictionary, key, version, metadata, (permissions & 2) != 0 ? PdfAuthenticationRole.Owner : PdfAuthenticationRole.Certificate, permissions);
            transferred = true;
            return result;
        }
        finally
        {
            Clear(seed);
            foreach (var envelope in envelopes)
                Clear(envelope);
            if (!transferred)
                Clear(key);
        }

        int Integer(PdfValue value, string name)
        {
            var item = Entry(value, name);
            return item.TryGetInteger(out long n) && n > 0 && n <= int.MaxValue ? (int)n : throw Error(context, "EncryptionDictionary", "A bounded public-key security parameter is required.");
        }
    }

    private static byte[] Derive(byte[] seed, IReadOnlyList<byte[]> recipients, int version, bool metadata, PdfOperationContext context, int keyBytes = 0)
    {
        long length = seed.Length + (metadata ? 0 : 4);
        foreach (var value in recipients)
            length += value.Length;
        if (length > int.MaxValue)
            throw Error(context, "CmsExtent", "The recipient digest input exceeds its supported extent.");
        context.Consume(PdfResourceKind.ExpandedBytes, length);
        var input = new byte[(int)length];
        byte[]? hash = null;
        try
        {
            Buffer.BlockCopy(seed, 0, input, 0, seed.Length);
            int at = seed.Length;
            foreach (var value in recipients)
            {
                context.Checkpoint();
                Buffer.BlockCopy(value, 0, input, at, value.Length);
                at += value.Length;
            }

            if (!metadata)
                for (int i = 0; i < 4; i++)
                    input[at + i] = 255;
            hash = PdfSecurityCryptography.Hash(input, version == 5 ? 256 : 160, context);
            return PdfSecurityCryptography.Slice(hash, 0, keyBytes == 0 ? version == 5 ? 32 : 16 : keyBytes, context);
        }
        finally
        {
            Clear(input);
            Clear(hash);
        }
    }

    private static byte[] ReadBytes(Stream source, int maximum, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, maximum + 1L);
        var buffer = new byte[maximum + 1];
        try
        {
            int count = 0;
            while (count <= maximum)
            {
                context.Checkpoint();
                int n = source.Read(buffer, count, Math.Min(4096, buffer.Length - count));
                if (n == 0)
                    break;
                count += n;
                if (count > maximum)
                    throw Error(context, "CmsExtent", "The recipient material exceeds its declared extent.");
            }

            return PdfSecurityCryptography.Slice(buffer, 0, count, context);
        }
        finally
        {
            Clear(buffer);
        }
    }

    private static PdfValue Entry(PdfValue value, string key) => value.Kind == PdfValueKind.Dictionary && value.Entries.TryGetValue(key, out var entry) ? entry : PdfValue.Null;
    private static string Name(PdfValue value, string key) => Entry(value, key)is var entry && entry.Kind == PdfValueKind.Name ? entry.NameValue : "";
    private static PdfValue N(string value) => PdfValue.Name(value);
    private static PdfValue D(params (string, PdfValue)[] values) => PdfValue.Dictionary(values.ToDictionary(value => value.Item1, value => value.Item2, StringComparer.Ordinal));
    private static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, "Security." + code, message);
    private static void Clear(byte[]? value) => PdfSecurityCryptography.Clear(value);
    public void Dispose()
    {
        Clear(Key);
        Key = Array.Empty<byte>();
    }
}
