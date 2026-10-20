using System.Formats.Asn1;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.Crypto.Digests;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Macs;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Security;

namespace Fluent.Pdf.Cryptography;
// Original bounded DER credential orchestration over pinned managed KDF/cipher primitives. No native PFX loader or store.
internal static class ManagedPkcs12Import
{
    private const string Data = "1.2.840.113549.1.7.1", EncryptedData = "1.2.840.113549.1.7.6", Rsa = "1.2.840.113549.1.1.1";
    private static readonly Asn1Tag Explicit = new(TagClass.ContextSpecific, 0, true);
    internal static (X509Certificate2 Certificate, RSA Key) Read(byte[] encoded, string password, PdfOperationContext c)
    {
        if (password.Length > 1024)
            throw Error(c, "Pkcs12Extent", "The explicit PKCS12 password exceeds the finite import profile.");
        c.Consume(PdfResourceKind.ExpandedBytes, encoded.Length * 32L + 1048576);
        var limits = new Limits(c);
        var input = new AsnReader(encoded, AsnEncodingRules.DER);
        var pfx = input.ReadSequence();
        if (pfx.ReadInteger() != 3)
            throw limits.Error("Unsupported PKCS12 version.");
        var content = pfx.ReadSequence();
        if (content.ReadObjectIdentifier() != Data)
            throw limits.Error("Only password-integrity authenticated safe data is accepted.");
        var wrapper = content.ReadSequence(Explicit);
        var authenticated = wrapper.ReadOctetString();
        wrapper.ThrowIfNotEmpty();
        content.ThrowIfNotEmpty();
        if (!pfx.HasData)
            throw limits.Error("A password-integrity MAC is mandatory.");
        var mac = pfx.ReadSequence();
        var digest = mac.ReadSequence();
        var hash = digest.ReadSequence();
        string hashName = HashOid(hash.ReadObjectIdentifier(), limits);
        if (hash.HasData)
            hash.ReadNull();
        hash.ThrowIfNotEmpty();
        var declared = digest.ReadOctetString();
        digest.ThrowIfNotEmpty();
        var salt = mac.ReadOctetString();
        Salt(salt, limits);
        int iterations = limits.Add(mac.HasData ? mac.ReadInteger() : 1);
        mac.ThrowIfNotEmpty();
        pfx.ThrowIfNotEmpty();
        input.ThrowIfNotEmpty();
        var bmp = new byte[(password.Length + 1) * 2];
        for (int i = 0; i < password.Length; i++)
        {
            bmp[i * 2] = (byte)(password[i] >> 8);
            bmp[i * 2 + 1] = (byte)password[i];
        }

        RSA? key = null;
        X509Certificate2? selected = null;
        var certificates = new List<X509Certificate2>();
        int bagCount = 0;
        try
        {
            var generator = new Pkcs12ParametersGenerator(Digest(hashName));
            generator.Init(bmp, salt, iterations);
            var hmac = new HMac(Digest(hashName));
            hmac.Init(generator.GenerateDerivedMacParameters(hmac.GetMacSize() * 8));
            hmac.BlockUpdate(authenticated, 0, authenticated.Length);
            var actual = new byte[hmac.GetMacSize()];
            hmac.DoFinal(actual, 0);
            int difference = actual.Length ^ declared.Length;
            for (int i = 0; i < actual.Length; i++)
                difference |= actual[i] ^ (i < declared.Length ? declared[i] : 0);
            Array.Clear(actual, 0, actual.Length);
            if (difference != 0)
                throw Error(c, "Pkcs12Import", "The explicit PKCS12 integrity/password check failed.");
            c.Checkpoint();
            var safeInput = new AsnReader(authenticated, AsnEncodingRules.DER);
            var safes = safeInput.ReadSequence();
            int sections = 0;
            while (safes.HasData)
            {
                c.Checkpoint();
                if (++sections > 16)
                    throw limits.Error("Too many authenticated safe sections.");
                var item = safes.ReadSequence();
                string oid = item.ReadObjectIdentifier();
                var body = item.ReadSequence(Explicit);
                byte[] clear;
                if (oid == Data)
                    clear = body.ReadOctetString();
                else if (oid == EncryptedData)
                {
                    var encrypted = body.ReadSequence();
                    if (encrypted.ReadInteger() != 0)
                        throw limits.Error("Only encrypted-data version0 is accepted.");
                    var info = encrypted.ReadSequence();
                    if (info.ReadObjectIdentifier() != Data)
                        throw limits.Error("Only safe-content data encryption is accepted.");
                    var plan = Algorithm(info.ReadSequence(), limits);
                    var cipher = info.ReadOctetString(new Asn1Tag(TagClass.ContextSpecific, 0));
                    info.ThrowIfNotEmpty();
                    encrypted.ThrowIfNotEmpty();
                    clear = Decrypt(plan, cipher, password, bmp, c);
                }
                else
                    throw limits.Error("Unsupported authenticated safe content type.");
                try
                {
                    body.ThrowIfNotEmpty();
                    item.ThrowIfNotEmpty();
                    Bags(clear);
                }
                finally
                {
                    Array.Clear(clear, 0, clear.Length);
                }
            }

            safeInput.ThrowIfNotEmpty();
            if (key == null)
                throw Error(c, "Pkcs12KeyProfile", "Exactly one explicit RSA private key is required.");
            var proof = key.SignHash(new byte[32], HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
            try
            {
                foreach (var certificate in certificates)
                {
                    c.Checkpoint();
                    using var publicKey = certificate.GetRSAPublicKey();
                    if (publicKey == null || publicKey.KeySize != key.KeySize || !publicKey.VerifyHash(new byte[32], proof, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1))
                        continue;
                    if (selected != null)
                        throw Error(c, "Pkcs12Identity", "Multiple certificates match the private key; select an identity explicitly outside this profile.");
                    selected = certificate;
                }
            }
            finally
            {
                Array.Clear(proof, 0, proof.Length);
            }

            if (selected == null)
                throw Error(c, "Pkcs12Identity", "No public certificate matches the explicit private key.");
            var result = (selected, key);
            certificates.Remove(selected);
            selected = null;
            key = null;
            return result;
        }
        finally
        {
            Array.Clear(authenticated, 0, authenticated.Length);
            Array.Clear(bmp, 0, bmp.Length);
            key?.Dispose();
            foreach (var certificate in certificates)
                certificate.Dispose();
        }

        void Bags(byte[] bytes)
        {
            using var depth = c.EnterScope(PdfResourceKind.Depth);
            var reader = new AsnReader(bytes, AsnEncodingRules.DER);
            var bags = reader.ReadSequence();
            while (bags.HasData)
            {
                c.Checkpoint();
                if (++bagCount > 128)
                    throw limits.Error("Too many PKCS12 bags.");
                c.Consume(PdfResourceKind.Objects, 1);
                c.Consume(PdfResourceKind.ExpandedBytes, 65536);
                var bag = bags.ReadSequence();
                string oid = bag.ReadObjectIdentifier();
                var value = bag.ReadSequence(Explicit);
                if (oid == "1.2.840.113549.1.12.10.1.3")
                {
                    var certificate = value.ReadSequence();
                    if (certificate.ReadObjectIdentifier() != "1.2.840.113549.1.9.22.1")
                        throw limits.Error("Only X509 public certificate bags are accepted.");
                    var data = certificate.ReadSequence(Explicit);
                    var der = data.ReadOctetString();
                    data.ThrowIfNotEmpty();
                    certificate.ThrowIfNotEmpty();
                    if (der.Length > 65536 || certificates.Count == 64)
                        throw limits.Error("Public certificate extent/count exceeded.");
#if NET9_0_OR_GREATER
                    certificates.Add(X509CertificateLoader.LoadCertificate(der));
#else
                    certificates.Add(new X509Certificate2(der));
#endif
                }
                else if (oid == "1.2.840.113549.1.12.10.1.2" || oid == "1.2.840.113549.1.12.10.1.1")
                {
                    if (key != null)
                        throw Error(c, "Pkcs12KeyProfile", "Multiple private keys require explicit external identity selection.");
                    byte[] privateInfo;
                    if (oid.EndsWith(".2", StringComparison.Ordinal))
                    {
                        var encrypted = value.ReadSequence();
                        var plan = Algorithm(encrypted.ReadSequence(), limits);
                        var ciphertext = encrypted.ReadOctetString();
                        encrypted.ThrowIfNotEmpty();
                        privateInfo = Decrypt(plan, ciphertext, password, bmp, c);
                    }
                    else
                        privateInfo = value.ReadEncodedValue().ToArray();
                    try
                    {
                        key = Import(privateInfo, c);
                    }
                    finally
                    {
                        Array.Clear(privateInfo, 0, privateInfo.Length);
                    }
                }
                else if (oid == "1.2.840.113549.1.12.10.1.6")
                {
                    var nested = value.ReadEncodedValue().ToArray();
                    try
                    {
                        Bags(nested);
                    }
                    finally
                    {
                        Array.Clear(nested, 0, nested.Length);
                    }
                }
                else
                    throw limits.Error("Unsupported secret bag; no implicit credential discovery is performed.");
                value.ThrowIfNotEmpty();
                if (bag.HasData)
                {
                    var attributes = bag.ReadSetOf();
                    while (attributes.HasData)
                    {
                        c.Checkpoint();
                        attributes.ReadEncodedValue();
                    }
                }

                bag.ThrowIfNotEmpty();
            }

            reader.ThrowIfNotEmpty();
        }
    }

    private sealed class Plan
    {
        internal bool Pkcs12;
        internal string Cipher = "", Hash = "";
        internal int KeyBytes, Iterations;
        internal byte[] Salt = Array.Empty<byte>(), Iv = Array.Empty<byte>();
    }

    private static Plan Algorithm(AsnReader algorithm, Limits limits)
    {
        var plan = new Plan();
        string oid = algorithm.ReadObjectIdentifier();
        var p = algorithm.ReadSequence();
        if (oid == "1.2.840.113549.1.5.13")
        {
            var kdf = p.ReadSequence();
            if (kdf.ReadObjectIdentifier() != "1.2.840.113549.1.5.12")
                throw limits.Error("Only PBKDF2 is accepted for PBES2.");
            var parameters = kdf.ReadSequence();
            plan.Salt = parameters.ReadOctetString();
            Salt(plan.Salt, limits);
            var iterations = parameters.ReadInteger();
            int declaredKey = 0;
            if (parameters.HasData && parameters.PeekTag().HasSameClassAndValue(Asn1Tag.Integer))
            {
                var length = parameters.ReadInteger();
                if (length < 16 || length > 32)
                    throw limits.Error("Unsupported PBKDF2 key extent.");
                declaredKey = (int)length;
            }

            plan.Hash = "SHA1";
            if (parameters.HasData)
            {
                var prf = parameters.ReadSequence();
                string hash = prf.ReadObjectIdentifier();
                plan.Hash = hash == "1.2.840.113549.2.7" ? "SHA1" : hash == "1.2.840.113549.2.9" ? "SHA256" : hash == "1.2.840.113549.2.10" ? "SHA384" : hash == "1.2.840.113549.2.11" ? "SHA512" : throw limits.Error("Unsupported PBKDF2 PRF.");
                if (prf.HasData)
                    prf.ReadNull();
                prf.ThrowIfNotEmpty();
            }

            parameters.ThrowIfNotEmpty();
            kdf.ThrowIfNotEmpty();
            var cipher = p.ReadSequence();
            string aes = cipher.ReadObjectIdentifier();
            plan.KeyBytes = aes == "2.16.840.1.101.3.4.1.2" ? 16 : aes == "2.16.840.1.101.3.4.1.22" ? 24 : aes == "2.16.840.1.101.3.4.1.42" ? 32 : throw limits.Error("Only AES-CBC128/192/256 is accepted for PBES2.");
            if (declaredKey != 0 && declaredKey != plan.KeyBytes)
                throw limits.Error("PBKDF2 key length disagrees with its cipher.");
            plan.Iv = cipher.ReadOctetString();
            if (plan.Iv.Length != 16)
                throw limits.Error("AES-CBC requires an exact IV.");
            cipher.ThrowIfNotEmpty();
            plan.Cipher = "AES/CBC/PKCS7Padding";
            plan.Iterations = limits.Add(iterations, (plan.KeyBytes + Digest(plan.Hash).GetDigestSize() - 1) / Digest(plan.Hash).GetDigestSize());
        }
        else if (oid == "1.2.840.113549.1.12.1.3" || oid == "1.2.840.113549.1.12.1.4" || oid == "1.2.840.113549.1.12.1.5" || oid == "1.2.840.113549.1.12.1.6")
        {
            plan.Pkcs12 = true;
            plan.Hash = "SHA1";
            plan.KeyBytes = oid.EndsWith(".3", StringComparison.Ordinal) ? 24 : oid.EndsWith(".6", StringComparison.Ordinal) ? 5 : 16;
            plan.Cipher = oid.EndsWith(".3", StringComparison.Ordinal) || oid.EndsWith(".4", StringComparison.Ordinal) ? "DESEDE/CBC/PKCS7Padding" : "RC2/CBC/PKCS7Padding";
            plan.Salt = p.ReadOctetString();
            Salt(plan.Salt, limits);
            plan.Iterations = limits.Add(p.ReadInteger(), (plan.KeyBytes + 19) / 20 + 1);
        }
        else
            throw limits.Error("Unsupported credential encryption algorithm.");
        p.ThrowIfNotEmpty();
        algorithm.ThrowIfNotEmpty();
        return plan;
    }

    private static byte[] Decrypt(Plan plan, byte[] cipher, string password, byte[] bmp, PdfOperationContext c)
    {
        c.Checkpoint();
        c.Consume(PdfResourceKind.ExpandedBytes, cipher.Length * 2L + 65536);
        ICipherParameters parameters;
        byte[]? utf8 = null, derived = null;
        try
        {
            if (plan.Pkcs12)
            {
                var generator = new Pkcs12ParametersGenerator(Digest(plan.Hash));
                generator.Init(bmp, plan.Salt, plan.Iterations);
                parameters = generator.GenerateDerivedParameters(plan.Cipher.StartsWith("RC2", StringComparison.Ordinal) ? "RC2" : "DESEDE", plan.KeyBytes * 8, 64);
                if (plan.Cipher.StartsWith("RC2", StringComparison.Ordinal))
                {
                    var withIv = (ParametersWithIV)parameters;
                    derived = ((KeyParameter)withIv.Parameters).GetKey();
                    parameters = new ParametersWithIV(new RC2Parameters(derived, plan.KeyBytes * 8), withIv.GetIV());
                }
            }
            else
            {
                utf8 = new UTF8Encoding(false, true).GetBytes(password);
                var generator = new Pkcs5S2ParametersGenerator(Digest(plan.Hash));
                generator.Init(utf8, plan.Salt, plan.Iterations);
                parameters = new ParametersWithIV(generator.GenerateDerivedParameters("AES", plan.KeyBytes * 8), plan.Iv);
            }

            var engine = CipherUtilities.GetCipher(plan.Cipher);
            engine.Init(false, parameters);
            var clear = engine.DoFinal(cipher);
            c.Checkpoint();
            return clear;
        }
        finally
        {
            Zero(utf8);
            Zero(derived);
        }
    }

    private static RSA Import(byte[] privateInfo, PdfOperationContext c)
    {
        if (privateInfo.Length > 16384)
            throw Error(c, "Pkcs12KeyProfile", "The private key exceeds its finite extent.");
        c.Consume(PdfResourceKind.ExpandedBytes, 131072);
        var input = new AsnReader(privateInfo, AsnEncodingRules.DER);
        var info = input.ReadSequence();
        if (info.ReadInteger() != 0)
            throw Error(c, "Pkcs12KeyProfile", "Only PKCS8version0 RSA keys are accepted.");
        var algorithm = info.ReadSequence();
        if (algorithm.ReadObjectIdentifier() != Rsa)
            throw Error(c, "Pkcs12KeyProfile", "Only RSA private keys are accepted.");
        if (algorithm.HasData)
            algorithm.ReadNull();
        algorithm.ThrowIfNotEmpty();
        var encoded = info.ReadOctetString();
        if (info.HasData)
            info.ReadEncodedValue();
        info.ThrowIfNotEmpty();
        input.ThrowIfNotEmpty();
        var reader = new AsnReader(encoded, AsnEncodingRules.DER);
        var sequence = reader.ReadSequence();
        if (sequence.ReadInteger() != 0)
            throw Error(c, "Pkcs12KeyProfile", "Only two-prime RSA version0 is accepted.");
        var parameters = new RSAParameters();
        RSA? key = null;
        try
        {
            parameters.Modulus = Integer(sequence, 1024);
            parameters.Exponent = Integer(sequence, 8);
            parameters.D = Integer(sequence, 1024);
            parameters.P = Integer(sequence, 512);
            parameters.Q = Integer(sequence, 512);
            parameters.DP = Integer(sequence, 512);
            parameters.DQ = Integer(sequence, 512);
            parameters.InverseQ = Integer(sequence, 512);
            sequence.ThrowIfNotEmpty();
            reader.ThrowIfNotEmpty();
            if (parameters.Modulus.Length < 256)
                throw Error(c, "Pkcs12KeyProfile", "Imported RSA keys must contain2048–8192bits.");
            int half = (parameters.Modulus.Length + 1) / 2;
            parameters.D = Pad(parameters.D, parameters.Modulus.Length);
            parameters.P = Pad(parameters.P, half);
            parameters.Q = Pad(parameters.Q, half);
            parameters.DP = Pad(parameters.DP, half);
            parameters.DQ = Pad(parameters.DQ, half);
            parameters.InverseQ = Pad(parameters.InverseQ, half);
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                key = new RSACng();
            else
                key = RSA.Create();
            key.ImportParameters(parameters);
            c.Checkpoint();
            if (key.KeySize < 2048 || key.KeySize > 8192)
                throw Error(c, "Pkcs12KeyProfile", "Imported RSA keys must contain2048–8192bits.");
            var result = key;
            key = null;
            return result;
        }
        finally
        {
            Array.Clear(encoded, 0, encoded.Length);
            key?.Dispose();
            Zero(parameters.Modulus);
            Zero(parameters.Exponent);
            Zero(parameters.D);
            Zero(parameters.P);
            Zero(parameters.Q);
            Zero(parameters.DP);
            Zero(parameters.DQ);
            Zero(parameters.InverseQ);
        }

        byte[] Pad(byte[] bytes, int size)
        {
            if (bytes.Length > size)
                throw Error(c, "Pkcs12KeyProfile", "An RSA parameter exceeds its balanced-key extent.");
            if (bytes.Length == size)
                return bytes;
            var padded = new byte[size];
            Array.Copy(bytes, 0, padded, size - bytes.Length, bytes.Length);
            Array.Clear(bytes, 0, bytes.Length);
            return padded;
        }

        byte[] Integer(AsnReader source, int maximum)
        {
            var bytes = source.ReadIntegerBytes();
            if (bytes.Length == 0 || (bytes.Span[0] & 128) != 0)
                throw Error(c, "Pkcs12KeyProfile", "RSA key parameters must be positive integers.");
            int trim = bytes.Length > 1 && bytes.Span[0] == 0 ? 1 : 0;
            if (bytes.Length - trim < 1 || bytes.Length - trim > maximum)
                throw Error(c, "Pkcs12KeyProfile", "An RSA key parameter exceeds its finite extent.");
            return bytes.Slice(trim).ToArray();
        }
    }

    private static IDigest Digest(string name) => name == "SHA1" ? new Sha1Digest() : name == "SHA256" ? new Sha256Digest() : name == "SHA384" ? new Sha384Digest() : new Sha512Digest();
    private static string HashOid(string oid, Limits limits) => oid == "1.3.14.3.2.26" ? "SHA1" : oid == "2.16.840.1.101.3.4.2.1" ? "SHA256" : oid == "2.16.840.1.101.3.4.2.2" ? "SHA384" : oid == "2.16.840.1.101.3.4.2.3" ? "SHA512" : throw limits.Error("Unsupported MAC hash.");
    private static void Salt(byte[] salt, Limits limits)
    {
        if (salt.Length < 8 || salt.Length > 64)
            throw limits.Error("Credential KDF salts require8–64bytes.");
    }

    private sealed class Limits(PdfOperationContext context)
    {
        private long _iterations;
        internal int Add(System.Numerics.BigInteger value, int hashBlocks = 1)
        {
            context.Checkpoint();
            if (value < 1 || value > 100000 || (_iterations += (long)value * hashBlocks) > 1000000)
                throw Error("The credential KDF exceeds its prior100000per-derivation/1000000aggregate hash-block iteration ceiling.");
            return (int)value;
        }

        internal PdfException Error(string message) => ManagedPkcs12Import.Error(context, "Pkcs12Profile", message);
    }

    private static void Zero(byte[]? bytes)
    {
        if (bytes != null)
            Array.Clear(bytes, 0, bytes.Length);
    }

    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Signature." + code, message);
}
