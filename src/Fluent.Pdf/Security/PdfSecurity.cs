using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit bounded full-rewrite security transformations. Credentials and plaintext staging remain in memory; caller streams remain owned by the caller.</summary>
public static partial class PdfSecurity
{
    /// <summary>Writes password-encrypted output from a detached snapshot. AES-256 revision 6 is the default. Existing signature rewrites require explicit authorization.</summary>
    public static PdfSecurityResult EncryptWithPassword(PdfDocumentSnapshot source, Stream output, PdfPasswordProtection protection, bool allowSignatureInvalidation = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        if (protection == null)
            throw new ArgumentNullException(nameof(protection));
        Output(output);
        return EncryptCore(source, output, (identity, context) => PdfStandardSecurity.Create(protection, identity, context), protection.Cipher == PdfPasswordCipher.Aes256 ? "2.0" : "1.6", allowSignatureInvalidation, policy, cancellationToken);
    }

    /// <summary>Authenticates and writes a plaintext full rewrite. Legacy RC4 and revision 5 require explicit permission. Failed authentication writes no output; signed rewrites require explicit authorization.</summary>
    public static PdfSecurityResult DecryptWithPassword(Stream input, Stream output, PdfPassword password, bool allowLegacyEncryption = false, bool allowSignatureInvalidation = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (password == null)
            throw new ArgumentNullException(nameof(password));
        InputOutput(input, output);
        return DecryptCore(input, output, (dictionary, identity, context) =>
        {
            byte[] supplied = password.Copy(context), id = Array.Empty<byte>();
            try
            {
                id = Identifier(identity, context);
                return PdfStandardSecurity.Authenticate(dictionary, id, supplied, allowLegacyEncryption, context);
            }
            finally
            {
                Clear(supplied);
                Clear(id);
            }
        }, allowLegacyEncryption, allowSignatureInvalidation, policy, cancellationToken);
    }

    private static PdfSecurityResult EncryptCore(PdfDocumentSnapshot source, Stream output, Func<byte[], PdfOperationContext, IPdfFileSecurity> create, string minimumVersion, bool allowSignatures, PdfOperationPolicy? policy, CancellationToken token)
    {
        PdfAuthenticationRole role = default;
        PdfDocumentPermissions permissions = default;
        string cipher = "";
        int revision = 0;
        bool metadata = true;
        var report = PdfFont.Run("EncryptDocument", "managed.security", policy, token, context =>
        {
            using var graph = new PdfGraphEdit(source, new(allowSignatureInvalidation: allowSignatures), context);
            var trailer = graph.Copy(graph.Edit.Trailer);
            if (trailer.TryGetValue("Encrypt", out var existing) && existing.Kind != PdfValueKind.Null)
                throw Error(context, "AlreadyEncrypted", "Decrypt existing protection explicitly before applying new protection.");
            var ids = NewIdentity(graph, trailer, context);
            byte[] identity = Identifier(ids, context);
            try
            {
                using var security = create(identity, context);
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 128);
                var encryptionId = graph.Edit.Add(security.Dictionary);
                trailer["Encrypt"] = PdfValue.Reference(encryptionId);
                trailer["ID"] = ids;
                graph.Edit.Trailer = PdfValue.OwnedDictionary(trailer);
                var crypt = new PdfObjectCrypt(security, encryptionId, false, context);
                using var buffer = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
                using (var lease = new BorrowedStream(buffer, false, context))
                    graph.Edit.WriteCore(lease, new(allowSignatureInvalidation: allowSignatures, minimumVersion: minimumVersion), context, crypt);
                Copy(buffer.Finish(), output, context);
                Capture(security);
            }
            finally
            {
                Clear(identity);
            }
        }, PdfCapability.WriteDocument);
        return new(role, permissions, cipher, revision, metadata, report);
        void Capture(IPdfFileSecurity security)
        {
            role = security.Role;
            permissions = (PdfDocumentPermissions)(security.PermissionBits & (uint)PdfDocumentPermissions.All);
            cipher = security.Description;
            revision = security.Revision;
            metadata = security.EncryptMetadata;
        }
    }

    private static PdfSecurityResult DecryptCore(Stream input, Stream output, Func<PdfValue, PdfValue, PdfOperationContext, IPdfFileSecurity> authenticate, bool allowLegacy, bool allowSignatures, PdfOperationPolicy? policy, CancellationToken token)
    {
        PdfAuthenticationRole role = default;
        PdfDocumentPermissions permissions = default;
        string cipher = "";
        int revision = 0;
        bool metadata = true;
        var report = PdfFont.Run("DecryptDocument", "managed.security", policy, token, context =>
        {
            IPdfFileSecurity? security = null;
            PdfObjectId encryptionId = default;
            try
            {
                using var lease = new BorrowedStream(input, true, context);
                var bytes = PdfByteStore.Read(lease, context);
                var parsed = new PdfSyntaxReader(bytes, context, (dictionary, id, identity) =>
                {
                    encryptionId = id;
                    security = authenticate(dictionary, identity, context);
                    return new PdfObjectCrypt(security, id, allowLegacy, context);
                }).Read();
                using var edit = new PdfTransaction(parsed, context.Policy);
                var trailer = PdfWriterCore.CopyDictionary(edit.Trailer.Entries, context);
                trailer.Remove("Encrypt");
                edit.Trailer = PdfValue.OwnedDictionary(trailer);
                if (encryptionId.Number > 0)
                    edit.Remove(encryptionId);
                using var buffer = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
                using (var destination = new BorrowedStream(buffer, false, context))
                    edit.WriteCore(destination, new(allowSignatureInvalidation: allowSignatures), context);
                Copy(buffer.Finish(), output, context);
                role = security!.Role;
                permissions = (PdfDocumentPermissions)(security.PermissionBits & (uint)PdfDocumentPermissions.All);
                cipher = security.Description;
                revision = security.Revision;
                metadata = security.EncryptMetadata;
            }
            finally
            {
                security?.Dispose();
            }
        }, PdfCapability.WriteDocument);
        return new(role, permissions, cipher, revision, metadata, report);
    }

    private static PdfValue NewIdentity(PdfGraphEdit graph, Dictionary<string, PdfValue> trailer, PdfOperationContext context)
    {
        PdfValue permanent;
        if (trailer.TryGetValue("ID", out var raw) && graph.Resolve(raw).Kind != PdfValueKind.Null)
        {
            var ids = graph.Resolve(raw);
            if (ids.Kind != PdfValueKind.Array || ids.Items.Count != 2 || graph.Resolve(ids.Items[0]).Kind != PdfValueKind.String || graph.Resolve(ids.Items[1]).Kind != PdfValueKind.String)
                throw Error(context, "IdentifierProfile", "Existing file identifiers must be a pair of strings.");
            permanent = graph.Resolve(ids.Items[0]);
        }
        else
            permanent = PdfValue.OwnedString(PdfSecurityCryptography.Random(16, context));
        context.Consume(PdfResourceKind.ExpandedBytes, 64);
        return PdfValue.Array(permanent, PdfValue.OwnedString(PdfSecurityCryptography.Random(16, context)));
    }

    private static byte[] Identifier(PdfValue identity, PdfOperationContext context)
    {
        if (identity.Kind == PdfValueKind.Null)
            return Array.Empty<byte>();
        if (identity.Kind != PdfValueKind.Array || identity.Items.Count != 2 || identity.Items.Any(value => value.Kind != PdfValueKind.String))
            throw Error(context, "IdentifierProfile", "The security handler requires direct string file identifiers.");
        context.Consume(PdfResourceKind.ExpandedBytes, identity.Items[0].StringBytes.Count);
        return identity.Items[0].StringBytes.ToArray();
    }

    private static void Copy(PdfByteStore source, Stream output, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 65536);
        var buffer = new byte[65536];
        try
        {
            using var input = source.Open(0, source.Length);
            using var lease = new BorrowedStream(output, false, context);
            int count;
            while ((count = input.Read(buffer, 0, buffer.Length)) > 0)
            {
                context.Checkpoint();
                lease.Write(buffer, 0, count);
            }

            context.Checkpoint();
        }
        finally
        {
            Clear(buffer);
        }
    }

    private static void InputOutput(Stream input, Stream output)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (!input.CanRead)
            throw new ArgumentException("A readable borrowed input is required.", nameof(input));
        Output(output);
        if (ReferenceEquals(input, output))
            throw new ArgumentException("Distinct input and output streams are required.", nameof(output));
    }

    private static void Output(Stream output)
    {
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (!output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
    }

    private static void Clear(byte[]? value) => PdfSecurityCryptography.Clear(value);
    private static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, "Security." + code, message);
}
