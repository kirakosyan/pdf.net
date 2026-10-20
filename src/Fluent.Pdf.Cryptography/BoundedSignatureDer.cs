namespace Fluent.Pdf.Cryptography;

// Preflight recursive DER before passing untrusted timestamp/certificate data to the optional ASN.1 engine.
internal static class BoundedSignatureDer
{
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
