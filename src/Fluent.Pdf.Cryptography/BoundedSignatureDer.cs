namespace Fluent.Pdf.Cryptography;

// Preflight recursive DER before passing untrusted timestamp/certificate data to the optional ASN.1 engine.
internal static class BoundedSignatureDer
{
    // CMS eContent and X509 extension values are OCTET STRINGs whose contents
    // the downstream engine reparses as ASN.1. Check those separate DER trees too.
    internal static void TimestampContent(ReadOnlyMemory<byte> encoded, byte[] certificate, PdfOperationContext c)
    {
        var reader = new System.Formats.Asn1.AsnReader(encoded, System.Formats.Asn1.AsnEncodingRules.DER);
        var outer = reader.ReadSequence();
        reader.ThrowIfNotEmpty();
        if (outer.ReadObjectIdentifier() != "1.2.840.113549.1.7.2") throw new System.Formats.Asn1.AsnContentException();
        var wrapper = outer.ReadSequence(new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 0, true));
        outer.ThrowIfNotEmpty();
        var signed = wrapper.ReadSequence();
        wrapper.ThrowIfNotEmpty();
        _ = signed.ReadInteger();
        _ = signed.ReadSetOf();
        var content = signed.ReadSequence();
        if (content.ReadObjectIdentifier() != "1.2.840.113549.1.9.16.1.4") throw new System.Formats.Asn1.AsnContentException();
        var explicitContent = content.ReadSequence(new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 0, true));
        c.Consume(PdfResourceKind.ExpandedBytes, explicitContent.PeekEncodedValue().Length);
        var inner = explicitContent.ReadOctetString();
        explicitContent.ThrowIfNotEmpty();
        content.ThrowIfNotEmpty();
        Validate(inner, inner.Length, c);
        if (signed.HasData && signed.PeekTag().HasSameClassAndValue(new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 0)))
        {
            var certificates = signed.ReadSetOf(new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 0, true));
            while (certificates.HasData) CertificateExtensions(certificates.ReadEncodedValue(), c);
        }
        if (signed.HasData && signed.PeekTag().HasSameClassAndValue(new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 1)))
            _ = signed.ReadEncodedValue();
        var signers = signed.ReadSetOf();
        signed.ThrowIfNotEmpty();
        while (signers.HasData)
        {
            var signer = signers.ReadSequence();
            _ = signer.ReadInteger();
            _ = signer.ReadEncodedValue(); // issuer/serial or subject key identifier
            _ = signer.ReadSequence(); // digest algorithm
            if (signer.PeekTag().HasSameClassAndValue(new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 0)))
                _ = signer.ReadEncodedValue();
            var algorithm = signer.ReadSequence();
            string oid = algorithm.ReadObjectIdentifier();
            c.Consume(PdfResourceKind.ExpandedBytes, signer.PeekEncodedValue().Length);
            var signature = signer.ReadOctetString();
            // These verifiers interpret the signature itself as a DER pair of integers.
            if (oid.StartsWith("1.2.840.10045.4.", StringComparison.Ordinal) || oid == "1.2.840.10040.4.3" || IsNistDerSignature(oid) || oid == "1.2.156.10197.1.501")
                Validate(signature, signature.Length, c);
        }
        CertificateExtensions(certificate, c);
    }
    private static bool IsNistDerSignature(string oid)
    {
        const string prefix = "2.16.840.1.101.3.4.3.";
        return oid.StartsWith(prefix, StringComparison.Ordinal) && int.TryParse(oid.Substring(prefix.Length), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int algorithm) && algorithm >= 1 && algorithm <= 12;
    }
    private static void CertificateExtensions(ReadOnlyMemory<byte> encoded, PdfOperationContext c)
    {
        var reader = new System.Formats.Asn1.AsnReader(encoded, System.Formats.Asn1.AsnEncodingRules.DER);
        var certificate = reader.ReadSequence();
        reader.ThrowIfNotEmpty();
        var tbs = certificate.ReadSequence();
        var version = new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 0, true);
        if (tbs.PeekTag().HasSameClassAndValue(version)) _ = tbs.ReadEncodedValue();
        for (int i = 0; i < 6; i++) _ = tbs.ReadEncodedValue();
        var extensionsTag = new System.Formats.Asn1.Asn1Tag(System.Formats.Asn1.TagClass.ContextSpecific, 3, true);
        while (tbs.HasData && !tbs.PeekTag().HasSameClassAndValue(extensionsTag)) _ = tbs.ReadEncodedValue();
        if (!tbs.HasData) return;
        var wrapper = tbs.ReadSequence(extensionsTag);
        var extensions = wrapper.ReadSequence();
        wrapper.ThrowIfNotEmpty();
        while (extensions.HasData)
        {
            c.Checkpoint();
            var extension = extensions.ReadSequence();
            _ = extension.ReadObjectIdentifier();
            if (extension.PeekTag().HasSameClassAndValue(System.Formats.Asn1.Asn1Tag.Boolean)) _ = extension.ReadBoolean();
            c.Consume(PdfResourceKind.ExpandedBytes, extension.PeekEncodedValue().Length);
            var value = extension.ReadOctetString();
            extension.ThrowIfNotEmpty();
            Validate(value, value.Length, c);
        }
    }
    internal static void Validate(byte[] bytes, int length, PdfOperationContext context)
    {
        int position = 0;
        Visit(length);
        if (position != length)
            throw PdfSignatures.Error(context, "DerProfile", "A complete finite DER value is required.");

        void Visit(int end)
        {
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            while (position < end)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 128);
                int tag = bytes[position++];
                if ((tag & 31) == 31)
                {
                    int groups = 0, first = -1, value;
                    do
                    {
                        if (position >= end || ++groups > 5)
                            throw PdfSignatures.Error(context, "DerProfile", "A bounded DER tag is required.");
                        value = bytes[position++];
                        if (first == -1)
                            first = value;
                    } while ((value & 128) != 0);
                    if ((first & 127) == 0 || groups == 1 && (first & 127) < 31)
                        throw PdfSignatures.Error(context, "DerProfile", "DER tags require minimal encoding.");
                }
                if (position >= end)
                    throw PdfSignatures.Error(context, "DerProfile", "DER length is truncated.");
                long extent = bytes[position++];
                if (extent >= 128)
                {
                    int count = (int)extent & 127;
                    if (count < 1 || count > 4 || count > end - position || bytes[position] == 0)
                        throw PdfSignatures.Error(context, "DerProfile", "Minimal definite DER lengths are required.");
                    extent = 0;
                    for (int i = 0; i < count; i++)
                        extent = extent << 8 | bytes[position++];
                    if (extent < 128)
                        throw PdfSignatures.Error(context, "DerProfile", "DER lengths require minimal encoding.");
                }
                if (extent > end - position)
                    throw PdfSignatures.Error(context, "DerProfile", "DER content exceeds its enclosing extent.");
                int contentEnd = position + (int)extent;
                if ((tag & 32) != 0)
                    Visit(contentEnd);
                else
                    position = contentEnd;
            }
        }
    }
}
