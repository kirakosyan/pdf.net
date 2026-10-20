using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace Fluent.Pdf.Cryptography;
public sealed partial class ManagedCmsEnvelopeProvider
{
    private sealed class Selection
    {
        internal byte[] EncryptedKey = Array.Empty<byte>(), Iv = Array.Empty<byte>(), Ciphertext = Array.Empty<byte>();
        internal RSAEncryptionPadding Padding = RSAEncryptionPadding.OaepSHA256;
        internal int KeyBytes;
        internal string Cipher = "AES";
    }

    private static Selection? Parse(byte[] bytes, X509Certificate2 certificate, bool allowLegacy, PdfOperationContext context)
    {
        var input = new AsnReader(bytes, AsnEncodingRules.DER);
        var outer = input.ReadSequence();
        if (outer.ReadObjectIdentifier() != EnvelopedOid)
            throw Error(context, "CmsContent", "A CMS EnvelopedData object is required.");
        var content = outer.ReadSequence(ExplicitContent);
        var envelope = content.ReadSequence();
        if (!envelope.TryReadInt32(out int version) || version != 0 && version != 2)
            throw Error(context, "CmsVersion", "The CMS envelope version is outside the supported RSA profile.");
        var recipients = envelope.ReadSetOf();
        Selection? selected = null;
        int count = 0;
        while (recipients.HasData)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            if (++count > 64)
                throw Error(context, "CmsExtent", "The CMS recipient inventory exceeds its supported extent.");
            var encoded = recipients.ReadEncodedValue();
            if (encoded.Span[0] != 0x30)
                continue; // Other recipient mechanisms do not match an explicit RSA key-transport request.
            var reader = new AsnReader(encoded, AsnEncodingRules.DER);
            var recipient = reader.ReadSequence();
            if (!recipient.TryReadInt32(out int recipientVersion) || recipientVersion != 0 && recipientVersion != 2)
                throw Error(context, "CmsVersion", "The RSA recipient version is unsupported.");
            bool matches = Identifier(recipient, certificate, recipientVersion, context);
            var algorithm = recipient.ReadSequence();
            string oid = algorithm.ReadObjectIdentifier();
            var parameters = algorithm.HasData ? algorithm.ReadEncodedValue() : ReadOnlyMemory<byte>.Empty;
            algorithm.ThrowIfNotEmpty();
            var encrypted = recipient.ReadOctetString();
            recipient.ThrowIfNotEmpty();
            reader.ThrowIfNotEmpty();
            if (matches && selected == null)
                selected = new Selection
                {
                    EncryptedKey = encrypted,
                    Padding = Padding(oid, parameters, allowLegacy, context)
                };
        }

        if (count == 0)
            throw Error(context, "CmsContent", "The CMS envelope has no recipients.");
        var encryptedContent = envelope.ReadSequence();
        if (encryptedContent.ReadObjectIdentifier() != DataOid)
            throw Error(context, "CmsContent", "The CMS envelope does not contain a PDF seed data payload.");
        var cipher = encryptedContent.ReadSequence();
        string cipherOid = cipher.ReadObjectIdentifier();
        string contentCipher = "AES";
        int keyBytes;
        byte[] iv;
        if (cipherOid == "1.2.840.113549.3.2")
        {
            contentCipher = "RC2";
            var parameters = cipher.ReadSequence();
            if (!parameters.TryReadInt32(out int parameterVersion))
                throw Error(context, "CmsContentCipher", "A bounded explicit RC2 parameter version is required.");
            keyBytes = parameterVersion switch
            {
                58 => 16,
                120 => 8,
                160 => 5,
                _ => throw Error(context, "CmsContentCipher", "The legacy RC2 effective key size is unsupported.")};
            iv = parameters.ReadOctetString();
            parameters.ThrowIfNotEmpty();
        }
        else
        {
            keyBytes = cipherOid switch
            {
                Aes256Oid => 32,
                "2.16.840.1.101.3.4.1.22" => 24,
                "2.16.840.1.101.3.4.1.2" => 16,
                "1.2.840.113549.3.7" => 24,
                _ => throw Error(context, "CmsContentCipher", "The CMS content cipher is outside the supported profile.")};
            if (cipherOid == "1.2.840.113549.3.7")
                contentCipher = "3DES";
            iv = cipher.ReadOctetString();
        }

        if (contentCipher != "AES" && selected != null && !allowLegacy)
            throw Error(context, "LegacyReadBlocked", "A legacy CMS content cipher requires explicit read permission.");
        if (iv.Length != (contentCipher == "AES" ? 16 : 8))
            throw Error(context, "CmsContentCipher", "The CMS content IV has an invalid extent.");
        cipher.ThrowIfNotEmpty();
        var ciphertext = encryptedContent.ReadOctetString(ImplicitOctets);
        encryptedContent.ThrowIfNotEmpty();
        if (envelope.HasData)
            throw Error(context, "CmsAttributesProfile", "Originator and additional CMS attribute profiles require separate validation.");
        envelope.ThrowIfNotEmpty();
        content.ThrowIfNotEmpty();
        outer.ThrowIfNotEmpty();
        input.ThrowIfNotEmpty();
        if (selected != null)
        {
            selected.KeyBytes = keyBytes;
            selected.Cipher = contentCipher;
            selected.Iv = iv;
            selected.Ciphertext = ciphertext;
        }

        return selected;
    }

    private static byte[] DecryptContent(Selection selection, byte[] key, PdfOperationContext context)
    {
        if (selection.Cipher == "AES")
            return PdfSecurityCryptography.Aes(key, selection.Iv, selection.Ciphertext, false, CipherMode.CBC, PaddingMode.PKCS7, context);
        if (selection.Ciphertext.Length == 0 || selection.Ciphertext.Length % 8 != 0)
            throw Error(context, "CmsContentCipher", "The legacy CMS ciphertext has an invalid block extent.");
        context.Checkpoint();
        context.Consume(PdfResourceKind.ExpandedBytes, selection.Ciphertext.LongLength + 32768);
        using SymmetricAlgorithm algorithm = selection.Cipher == "RC2" ? RC2.Create() : TripleDES.Create();
        algorithm.Mode = CipherMode.CBC;
        algorithm.Padding = PaddingMode.PKCS7;
        algorithm.Key = key;
        if (algorithm is RC2 rc2)
            rc2.EffectiveKeySize = key.Length * 8;
        algorithm.IV = selection.Iv;
        using var transform = algorithm.CreateDecryptor();
        var result = transform.TransformFinalBlock(selection.Ciphertext, 0, selection.Ciphertext.Length);
        try
        {
            context.Checkpoint();
            return result;
        }
        catch
        {
            Clear(result);
            throw;
        }
    }

    private static bool Identifier(AsnReader input, X509Certificate2 certificate, int version, PdfOperationContext context)
    {
        if (input.PeekTag().HasSameClassAndValue(ImplicitOctets))
        {
            if (version != 2)
                throw Error(context, "CmsVersion", "A subject-key identifier requires recipient version 2.");
            var id = input.ReadOctetString(ImplicitOctets);
            foreach (var extension in certificate.Extensions)
                if (extension.Oid?.Value == "2.5.29.14")
                {
                    var value = new AsnReader(extension.RawData, AsnEncodingRules.DER);
                    var expected = value.ReadOctetString();
                    value.ThrowIfNotEmpty();
                    return id.SequenceEqual(expected);
                }

            return false;
        }

        if (version != 0)
            throw Error(context, "CmsVersion", "An issuer/serial recipient requires version 0.");
        var issuerAndSerial = input.ReadSequence();
        var issuer = issuerAndSerial.ReadEncodedValue();
        var serial = issuerAndSerial.ReadIntegerBytes();
        issuerAndSerial.ThrowIfNotEmpty();
        if (serial.Span.Length == 0 || (serial.Span[0] & 128) != 0 || serial.Span.ToArray().All(b => b == 0))
            return false;
        var expectedSerial = certificate.GetSerialNumber();
        Array.Reverse(expectedSerial);
        return issuer.Span.SequenceEqual(certificate.IssuerName.RawData) && Positive(serial.Span).SequenceEqual(Positive(expectedSerial));
        static ReadOnlySpan<byte> Positive(ReadOnlySpan<byte> value)
        {
            while (value.Length > 1 && value[0] == 0)
                value = value.Slice(1);
            return value;
        }
    }

    private static RSAEncryptionPadding Padding(string oid, ReadOnlyMemory<byte> parameters, bool allowLegacy, PdfOperationContext context)
    {
        if (oid == RsaOid)
        {
            if (!allowLegacy)
                throw Error(context, "LegacyReadBlocked", "Legacy RSA key transport requires explicit read permission.");
            if (!parameters.IsEmpty)
            {
                var value = new AsnReader(parameters, AsnEncodingRules.DER);
                value.ReadNull();
                value.ThrowIfNotEmpty();
            }

            return RSAEncryptionPadding.Pkcs1;
        }

        if (oid != OaepOid)
            throw Error(context, "CmsKeyTransport", "The requested CMS key transport is unsupported.");
        string hash = Sha1Oid, mgfHash = Sha1Oid;
        var label = Array.Empty<byte>();
        if (!parameters.IsEmpty)
        {
            var input = new AsnReader(parameters, AsnEncodingRules.DER);
            var sequence = input.ReadSequence();
            int prior = -1;
            while (sequence.HasData)
            {
                var tag = sequence.PeekTag();
                if (tag.TagClass != TagClass.ContextSpecific || tag.TagValue < 0 || tag.TagValue > 2 || tag.TagValue <= prior)
                    throw Error(context, "CmsOaep", "RSA-OAEP parameters are malformed or outside the supported profile.");
                prior = tag.TagValue;
                var explicitValue = sequence.ReadSequence(new Asn1Tag(TagClass.ContextSpecific, prior, true));
                if (prior == 0)
                    hash = Hash(explicitValue);
                else if (prior == 1)
                {
                    var algorithm = explicitValue.ReadSequence();
                    if (algorithm.ReadObjectIdentifier() != MgfOid)
                        throw Error(context, "CmsOaep", "Only MGF1 is supported for RSA-OAEP.");
                    mgfHash = Hash(algorithm);
                    algorithm.ThrowIfNotEmpty();
                }
                else
                {
                    var algorithm = explicitValue.ReadSequence();
                    if (algorithm.ReadObjectIdentifier() != LabelOid)
                        throw Error(context, "CmsOaep", "Only an explicit empty OAEP label is supported.");
                    label = algorithm.ReadOctetString();
                    algorithm.ThrowIfNotEmpty();
                }

                explicitValue.ThrowIfNotEmpty();
            }

            input.ThrowIfNotEmpty();
        }

        if (hash != mgfHash || label.Length != 0)
            throw Error(context, "CmsOaep", "RSA-OAEP requires matching hash/MGF1 algorithms and an empty label in this profile.");
        if (hash == Sha1Oid)
        {
            if (!allowLegacy)
                throw Error(context, "LegacyReadBlocked", "RSA-OAEP with SHA-1 requires explicit read permission.");
            return RSAEncryptionPadding.OaepSHA1;
        }

        return hash switch
        {
            Sha256Oid => RSAEncryptionPadding.OaepSHA256,
            "2.16.840.1.101.3.4.2.2" => RSAEncryptionPadding.OaepSHA384,
            "2.16.840.1.101.3.4.2.3" => RSAEncryptionPadding.OaepSHA512,
            _ => throw Error(context, "CmsOaep", "The OAEP hash is unsupported.")};
        static string Hash(AsnReader source)
        {
            var algorithm = source.ReadSequence();
            string result = algorithm.ReadObjectIdentifier();
            if (algorithm.HasData)
                algorithm.ReadNull();
            algorithm.ThrowIfNotEmpty();
            return result;
        }
    }
}
