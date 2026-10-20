using System.Security.Cryptography;
using System.Text;

namespace Fluent.Pdf;
internal sealed class PdfStandardSecurity : IPdfFileSecurity
{
    private byte[] _key;
    internal PdfValue Dictionary { get; }
    internal int Revision { get; }
    internal uint PermissionBits { get; }
    internal bool EncryptMetadata { get; }
    internal PdfAuthenticationRole Role { get; }
    internal byte[] Key => _key;

    PdfValue IPdfFileSecurity.Dictionary => Dictionary;

    byte[] IPdfFileSecurity.Key => Key;

    int IPdfFileSecurity.Version => Revision == 2 ? 1 : Revision == 3 ? 2 : Revision == 4 ? 4 : 5;

    bool IPdfFileSecurity.PublicKey => false;

    bool IPdfFileSecurity.EncryptMetadata => EncryptMetadata;

    PdfAuthenticationRole IPdfFileSecurity.Role => Role;

    uint IPdfFileSecurity.PermissionBits => PermissionBits;

    int IPdfFileSecurity.Revision => Revision;

    string IPdfFileSecurity.Description => "Standard-R" + Revision.ToString(System.Globalization.CultureInfo.InvariantCulture) + "-" + (_key.Length * 8).ToString(System.Globalization.CultureInfo.InvariantCulture) + "-bit";

    private static readonly byte[] PasswordPadding =
    {
        0x28,
        0xbf,
        0x4e,
        0x5e,
        0x4e,
        0x75,
        0x8a,
        0x41,
        0x64,
        0x00,
        0x4e,
        0x56,
        0xff,
        0xfa,
        0x01,
        0x08,
        0x2e,
        0x2e,
        0x00,
        0xb6,
        0xd0,
        0x68,
        0x3e,
        0x80,
        0x2f,
        0x0c,
        0xa9,
        0xfe,
        0x64,
        0x53,
        0x69,
        0x7a
    };
    private PdfStandardSecurity(PdfValue dictionary, byte[] key, int revision, uint permissions, bool metadata, PdfAuthenticationRole role)
    {
        Dictionary = dictionary;
        _key = key;
        Revision = revision;
        PermissionBits = permissions;
        EncryptMetadata = metadata;
        Role = role;
    }

    internal static PdfStandardSecurity Create(PdfPasswordProtection protection, byte[] identity, PdfOperationContext context)
    {
        byte[] user = Array.Empty<byte>(), owner = Array.Empty<byte>();
        try
        {
            user = protection.UserPassword.Copy(context);
            owner = protection.OwnerPassword.Copy(context);
            if (owner.Length == 0)
                throw Error(context, "OwnerPasswordRequired", "New password protection requires an explicit nonempty owner credential.");
            uint permissions = 0xfffff0c0u | (uint)protection.Permissions;
            if (protection.Cipher == PdfPasswordCipher.Aes256)
                permissions |= 512;
            return protection.Cipher == PdfPasswordCipher.Aes256 ? Create6(user, owner, permissions, protection.EncryptMetadata, context) : Create4(user, owner, identity, permissions, protection.EncryptMetadata, context);
        }
        finally
        {
            Clear(user);
            Clear(owner);
        }
    }

    private static PdfStandardSecurity Create6(byte[] user, byte[] owner, uint permissions, bool metadata, PdfOperationContext context)
    {
        byte[] uPassword = Array.Empty<byte>(), oPassword = Array.Empty<byte>(), key = Array.Empty<byte>();
        byte[]? u = null, o = null, ue = null, oe = null, perms = null;
        bool transferred = false;
        try
        {
            uPassword = Prepared(user, context);
            oPassword = Prepared(owner, context);
            key = PdfSecurityCryptography.Random(32, context);
            (u, ue) = Entry(uPassword, Array.Empty<byte>());
            (o, oe) = Entry(oPassword, u);
            var plainPerms = PdfSecurityCryptography.Random(16, context);
            try
            {
                for (int i = 0; i < 4; i++)
                    plainPerms[i] = (byte)(permissions >> (8 * i));
                for (int i = 4; i < 8; i++)
                    plainPerms[i] = 255;
                plainPerms[8] = metadata ? (byte)84 : (byte)70;
                plainPerms[9] = 97;
                plainPerms[10] = 100;
                plainPerms[11] = 98;
                perms = Aes(key, plainPerms, true, CipherMode.ECB, context);
            }
            finally
            {
                Clear(plainPerms);
            }

            var dictionary = Base(6, permissions, metadata, context);
            dictionary["U"] = Owned(u);
            dictionary["O"] = Owned(o);
            dictionary["UE"] = Owned(ue);
            dictionary["OE"] = Owned(oe);
            dictionary["Perms"] = Owned(perms);
            var result = new PdfStandardSecurity(PdfValue.OwnedDictionary(dictionary), key, 6, permissions, metadata, PdfAuthenticationRole.CreatedProtection);
            transferred = true;
            return result;
        }
        finally
        {
            Clear(uPassword);
            Clear(oPassword);
            if (!transferred)
            {
                Clear(key);
                Clear(u);
                Clear(o);
                Clear(ue);
                Clear(oe);
                Clear(perms);
            }
        }

        (byte[] entry, byte[] encryptedKey) Entry(byte[] password, byte[] userEntry)
        {
            var salts = PdfSecurityCryptography.Random(16, context);
            byte[]? validation = null, wrapping = null, validationSalt = null, keySalt = null, entry = null, encrypted = null;
            bool done = false;
            try
            {
                validationSalt = Slice(salts, 0, 8, context);
                keySalt = Slice(salts, 8, 8, context);
                validation = Hash6(password, validationSalt, userEntry, context);
                entry = Concat(context, validation, salts);
                wrapping = Hash6(password, keySalt, userEntry, context);
                encrypted = Aes(wrapping, key, true, CipherMode.CBC, context);
                done = true;
                return (entry, encrypted);
            }
            finally
            {
                Clear(salts);
                Clear(validation);
                Clear(wrapping);
                Clear(validationSalt);
                Clear(keySalt);
                if (!done)
                {
                    Clear(entry);
                    Clear(encrypted);
                }
            }
        }
    }

    private static PdfStandardSecurity Create4(byte[] user, byte[] owner, byte[] identity, uint permissions, bool metadata, PdfOperationContext context)
    {
        byte[] padded = Array.Empty<byte>(), ownerKey = Array.Empty<byte>();
        byte[]? o = null, u = null, key = null;
        bool transferred = false;
        try
        {
            padded = Pad(user, context);
            ownerKey = OwnerKey(owner, 4, 16, context);
            o = Rc4Rounds(ownerKey, padded, false, context);
            key = LegacyKey(user, o, permissions, identity, 4, 16, metadata, context);
            u = UserEntry(key, identity, 4, context);
            var dictionary = Base(4, permissions, metadata, context);
            dictionary["O"] = Owned(o);
            dictionary["U"] = Owned(u);
            var result = new PdfStandardSecurity(PdfValue.OwnedDictionary(dictionary), key, 4, permissions, metadata, PdfAuthenticationRole.CreatedProtection);
            transferred = true;
            return result;
        }
        finally
        {
            Clear(padded);
            Clear(ownerKey);
            if (!transferred)
            {
                Clear(o);
                Clear(u);
                Clear(key);
            }
        }
    }

    internal static PdfStandardSecurity Authenticate(PdfValue dictionary, byte[] identity, byte[] password, bool allowLegacy, PdfOperationContext context)
    {
        if (dictionary.Kind != PdfValueKind.Dictionary || Name("Filter") != "Standard")
            throw Error(context, "SecurityHandler", "An explicit supported security handler is required.");
        int revision = Number("R"), version = Number("V");
        uint permissions = Permission();
        if (!(revision == 2 && version == 1 || revision == 3 && version == 2 || revision == 4 && version == 4 || (revision == 5 || revision == 6) && version == 5))
            throw Error(context, "RevisionProfile", "The security handler revision and algorithm version are unsupported or inconsistent.");
        bool hasMetadata = dictionary.Entries.TryGetValue("EncryptMetadata", out var meta) && meta.Kind != PdfValueKind.Null;
        if (hasMetadata && meta!.Kind != PdfValueKind.Boolean)
            throw Error(context, "EncryptionDictionary", "EncryptMetadata must be a direct boolean.");
        bool metadata = !hasMetadata || meta!.BooleanValue;
        if ((revision <= 3 || revision == 5) && !allowLegacy)
            throw Error(context, "LegacyReadBlocked", "Legacy password encryption requires explicit read permission.");
        if (revision >= 5)
            return AuthenticateModern();
        int length = revision == 2 ? 5 : revision == 4 ? 16 : dictionary.Entries.TryGetValue("Length", out var bits) ? Integer(bits) / 8 : 5;
        if (length < 5 || length > 16 || revision == 2 && length != 5 || dictionary.Entries.TryGetValue("Length", out var declared) && Integer(declared) != length * 8)
            throw Error(context, "KeyLength", "The legacy file key length is invalid.");
        byte[] o = Array.Empty<byte>(), u = Array.Empty<byte>(), ownerKey = Array.Empty<byte>();
        byte[]? recovered = null, fileKey = null;
        bool transferred = false;
        try
        {
            o = Bytes("O", 32);
            u = Bytes("U", 32);
            ownerKey = OwnerKey(password, revision, length, context);
            recovered = revision == 2 ? PdfSecurityCryptography.Arcfour(ownerKey, o, context) : Rc4Rounds(ownerKey, o, true, context);
            fileKey = LegacyKey(recovered, o, permissions, identity, revision, length, metadata, context);
            PdfAuthenticationRole role = PdfAuthenticationRole.Owner;
            if (!Matches(fileKey))
            {
                Clear(fileKey);
                fileKey = LegacyKey(password, o, permissions, identity, revision, length, metadata, context);
                role = PdfAuthenticationRole.User;
                if (!Matches(fileKey))
                    throw Error(context, "AuthenticationFailed", "The supplied credential did not authenticate.");
            }

            var result = new PdfStandardSecurity(dictionary, fileKey, revision, permissions, metadata, role);
            transferred = true;
            return result;
        }
        finally
        {
            Clear(o);
            Clear(u);
            Clear(ownerKey);
            Clear(recovered);
            if (!transferred)
                Clear(fileKey);
        }

        bool Matches(byte[] candidate)
        {
            var expected = UserEntry(candidate, identity, revision, context);
            try
            {
                int difference = 0;
                for (int i = 0; i < (revision == 2 ? 32 : 16); i++)
                    difference |= expected[i] ^ u[i];
                return difference == 0;
            }
            finally
            {
                Clear(expected);
            }
        }

        PdfStandardSecurity AuthenticateModern()
        {
            if (dictionary.Entries.TryGetValue("Length", out var modernLength) && Integer(modernLength) != 256)
                throw Error(context, "KeyLength", "AES-256 requires a 256-bit file key.");
            byte[] p = Array.Empty<byte>(), user = Array.Empty<byte>(), owner = Array.Empty<byte>(), ue = Array.Empty<byte>(), oe = Array.Empty<byte>(), encryptedPerms = Array.Empty<byte>();
            byte[]? key = null;
            bool done = false;
            try
            {
                p = Prepared(password, context);
                user = Bytes("U", 48);
                owner = Bytes("O", 48);
                ue = Bytes("UE", 32);
                oe = Bytes("OE", 32);
                encryptedPerms = Bytes("Perms", 16);
                var role = PdfAuthenticationRole.Owner;
                if (Valid(owner, user))
                    key = Unwrap(owner, oe, user);
                else if (Valid(user, Array.Empty<byte>()))
                {
                    role = PdfAuthenticationRole.User;
                    key = Unwrap(user, ue, Array.Empty<byte>());
                }
                else
                    throw Error(context, "AuthenticationFailed", "The supplied credential did not authenticate.");
                var plainPerms = Aes(key, encryptedPerms, false, CipherMode.ECB, context);
                try
                {
                    int mismatch = plainPerms[8] ^ (metadata ? 84 : 70);
                    mismatch |= plainPerms[9] ^ 97;
                    mismatch |= plainPerms[10] ^ 100;
                    mismatch |= plainPerms[11] ^ 98;
                    for (int i = 0; i < 4; i++)
                        mismatch |= plainPerms[i] ^ (byte)(permissions >> (i * 8));
                    for (int i = 4; i < 8; i++)
                        mismatch |= plainPerms[i] ^ 255;
                    if (mismatch != 0)
                        throw Error(context, "PermissionsIntegrity", "The encrypted permissions do not match the security dictionary.");
                }
                finally
                {
                    Clear(plainPerms);
                }

                var result = new PdfStandardSecurity(dictionary, key, revision, permissions, metadata, role);
                done = true;
                return result;
            }
            finally
            {
                Clear(p);
                Clear(user);
                Clear(owner);
                Clear(ue);
                Clear(oe);
                Clear(encryptedPerms);
                if (!done)
                    Clear(key);
            }

            byte[] Compute(byte[] salt, byte[] userKey)
            {
                if (revision == 6)
                    return Hash6(p, salt, userKey, context);
                var seed = Concat(context, p, salt, userKey);
                try
                {
                    return PdfSecurityCryptography.Hash(seed, 256, context);
                }
                finally
                {
                    Clear(seed);
                }
            }

            bool Valid(byte[] entry, byte[] userKey)
            {
                var salt = Slice(entry, 32, 8, context);
                byte[]? hash = null;
                try
                {
                    hash = Compute(salt, userKey);
                    int mismatch = 0;
                    for (int i = 0; i < 32; i++)
                        mismatch |= hash[i] ^ entry[i];
                    return mismatch == 0;
                }
                finally
                {
                    Clear(salt);
                    Clear(hash);
                }
            }

            byte[] Unwrap(byte[] entry, byte[] encryptedKey, byte[] userKey)
            {
                var salt = Slice(entry, 40, 8, context);
                byte[]? wrapping = null;
                try
                {
                    wrapping = Compute(salt, userKey);
                    return Aes(wrapping, encryptedKey, false, CipherMode.CBC, context);
                }
                finally
                {
                    Clear(salt);
                    Clear(wrapping);
                }
            }
        }

        int Integer(PdfValue value) => value.TryGetInteger(out long n) && n >= int.MinValue && n <= int.MaxValue ? (int)n : throw Error(context, "EncryptionDictionary", "A direct bounded integer is required in the security dictionary.");
        int Number(string name) => dictionary.Entries.TryGetValue(name, out var value) ? Integer(value) : throw Error(context, "EncryptionDictionary", "A required security parameter is absent.");
        string Name(string name) => dictionary.Entries.TryGetValue(name, out var value) && value.Kind == PdfValueKind.Name ? value.NameValue : "";
        uint Permission()
        {
            if (!dictionary.Entries.TryGetValue("P", out var value) || !value.TryGetInteger(out long n) || n < int.MinValue || n > uint.MaxValue)
                throw Error(context, "EncryptionDictionary", "A bounded permissions bitfield is required.");
            return unchecked((uint)n);
        }

        byte[] Bytes(string name, int count)
        {
            if (!dictionary.Entries.TryGetValue(name, out var value) || value.Kind != PdfValueKind.String || value.StringBytes.Count != count)
                throw Error(context, "EncryptionDictionary", "A security entry is absent or has an invalid extent.");
            context.Consume(PdfResourceKind.ExpandedBytes, count);
            return value.StringBytes.ToArray();
        }
    }

    private static Dictionary<string, PdfValue> Base(int revision, uint permissions, bool metadata, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.Objects, 32);
        context.Consume(PdfResourceKind.ExpandedBytes, 4096);
        var cipher = PdfValue.Dictionary(new Dictionary<string, PdfValue> { { "CFM", PdfValue.Name(revision == 6 ? "AESV3" : "AESV2") }, { "AuthEvent", PdfValue.Name("DocOpen") }, { "Length", PdfValue.Integer(revision == 6 ? 32 : 16) } });
        return new(StringComparer.Ordinal)
        {
            {
                "Filter",
                PdfValue.Name("Standard")
            },
            {
                "V",
                PdfValue.Integer(revision == 6 ? 5 : 4)
            },
            {
                "R",
                PdfValue.Integer(revision)
            },
            {
                "Length",
                PdfValue.Integer(revision == 6 ? 256 : 128)
            },
            {
                "P",
                PdfValue.Integer(unchecked((int)permissions))
            },
            {
                "EncryptMetadata",
                PdfValue.Boolean(metadata)
            },
            {
                "StmF",
                PdfValue.Name("StdCF")
            },
            {
                "StrF",
                PdfValue.Name("StdCF")
            },
            {
                "CF",
                PdfValue.Dictionary(new Dictionary<string, PdfValue> { { "StdCF", cipher } })
            }
        };
    }

    private static byte[] Prepared(byte[] password, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 1024);
        try
        {
            new UTF8Encoding(false, true).GetCharCount(password);
        }
        catch (DecoderFallbackException)
        {
            throw Error(context, "PasswordEncoding", "Revision 6 credentials require standard-prepared UTF-8 bytes.");
        }

        return Slice(password, 0, Math.Min(127, password.Length), context);
    }

    private static byte[] Pad(byte[] password, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 32);
        var result = new byte[32];
        int count = Math.Min(password.Length, 32);
        Buffer.BlockCopy(password, 0, result, 0, count);
        Buffer.BlockCopy(PasswordPadding, 0, result, count, 32 - count);
        return result;
    }

    private static byte[] OwnerKey(byte[] password, int revision, int length, PdfOperationContext context)
    {
        byte[] padded = Pad(password, context), hash;
        try
        {
            hash = PdfSecurityCryptography.Hash(padded, 128, context);
        }
        finally
        {
            Clear(padded);
        }

        try
        {
            if (revision >= 3)
                for (int i = 0; i < 50; i++)
                {
                    var next = PdfSecurityCryptography.Hash(hash, 128, context);
                    Clear(hash);
                    hash = next;
                }

            return Slice(hash, 0, length, context);
        }
        finally
        {
            Clear(hash);
        }
    }

    private static byte[] LegacyKey(byte[] password, byte[] ownerEntry, uint permissions, byte[] identity, int revision, int length, bool metadata, PdfOperationContext context)
    {
        byte[] padded = Array.Empty<byte>(), permissionBytes = Array.Empty<byte>();
        byte[]? input = null, hash = null;
        try
        {
            padded = Pad(password, context);
            context.Consume(PdfResourceKind.ExpandedBytes, 8);
            permissionBytes = new byte[4];
            for (int i = 0; i < 4; i++)
                permissionBytes[i] = (byte)(permissions >> (i * 8));
            input = Concat(context, padded, ownerEntry, permissionBytes, identity, revision >= 4 && !metadata ? new byte[] { 255, 255, 255, 255 } : Array.Empty<byte>());
            hash = PdfSecurityCryptography.Hash(input, 128, context);
            if (revision >= 3)
                for (int i = 0; i < 50; i++)
                {
                    var part = Slice(hash, 0, length, context);
                    byte[] next;
                    try
                    {
                        next = PdfSecurityCryptography.Hash(part, 128, context);
                    }
                    finally
                    {
                        Clear(part);
                    }

                    Clear(hash);
                    hash = next;
                }

            return Slice(hash, 0, length, context);
        }
        finally
        {
            Clear(padded);
            Clear(input);
            Clear(hash);
            Clear(permissionBytes);
        }
    }

    private static byte[] UserEntry(byte[] key, byte[] identity, int revision, PdfOperationContext context)
    {
        if (revision == 2)
            return PdfSecurityCryptography.Arcfour(key, PasswordPadding, context);
        byte[] input = Concat(context, PasswordPadding, identity);
        byte[]? hash = null, encrypted = null;
        try
        {
            hash = PdfSecurityCryptography.Hash(input, 128, context);
            encrypted = Rc4Rounds(key, hash, false, context);
            context.Consume(PdfResourceKind.ExpandedBytes, 32);
            var result = new byte[32];
            Buffer.BlockCopy(encrypted, 0, result, 0, 16);
            return result;
        }
        finally
        {
            Clear(input);
            Clear(hash);
            Clear(encrypted);
        }
    }

    private static byte[] Rc4Rounds(byte[] key, byte[] input, bool reverse, PdfOperationContext context)
    {
        byte[] value = Array.Empty<byte>(), roundKey = Array.Empty<byte>();
        try
        {
            value = Slice(input, 0, input.Length, context);
            roundKey = Slice(key, 0, key.Length, context);
            for (int i = 0; i < 20; i++)
            {
                int round = reverse ? 19 - i : i;
                for (int j = 0; j < key.Length; j++)
                    roundKey[j] = (byte)(key[j] ^ round);
                var next = PdfSecurityCryptography.Arcfour(roundKey, value, context);
                Clear(value);
                value = next;
            }

            var result = value;
            value = Array.Empty<byte>();
            return result;
        }
        finally
        {
            Clear(value);
            Clear(roundKey);
        }
    }

    private static byte[] Aes(byte[] key, byte[] input, bool encrypt, CipherMode mode, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 16);
        var zero = new byte[16];
        return PdfSecurityCryptography.Aes(key, zero, input, encrypt, mode, PaddingMode.None, context);
    }

    private static byte[] Hash6(byte[] password, byte[] salt, byte[] user, PdfOperationContext context) => PdfSecurityCryptography.Revision6Hash(password, salt, user, context);
    private static byte[] Slice(byte[] input, int start, int count, PdfOperationContext context) => PdfSecurityCryptography.Slice(input, start, count, context);
    private static byte[] Concat(PdfOperationContext context, params byte[][] values) => PdfSecurityCryptography.Concat(context, values);
    private static PdfValue Owned(byte[] bytes) => PdfValue.OwnedString(bytes);
    private static void Clear(byte[]? value) => PdfSecurityCryptography.Clear(value);
    private static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, "Security." + code, message);
    public void Dispose()
    {
        Clear(_key);
        _key = Array.Empty<byte>();
    }
}
