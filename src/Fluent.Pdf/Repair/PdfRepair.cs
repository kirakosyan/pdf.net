using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit bounded recovery with an untouched source view, complete object ledger and strict post-write structural parsing.</summary>
public static class PdfRepair
{
    /// <summary>Rebuilds one classic-index revision using recovered active/free identities or a complete explicit caller inventory. Exact stream overrides are explicit; duplicate histories, compressed-index and encrypted profiles fail closed.</summary>
    public static PdfRepairResult Recover(Stream input, Stream output, PdfRepairOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));
        if (output == null)
            throw new ArgumentNullException(nameof(output));
        if (!input.CanRead)
            throw new ArgumentException("Readable borrowed input is required.", nameof(input));
        if (!output.CanWrite || ReferenceEquals(input, output))
            throw new ArgumentException("Distinct writable borrowed output is required.", nameof(output));
        options ??= new();
        PdfByteStore? original = null, repaired = null;
        string originalHash = "", repairedHash = "";
        PdfRepairRecord[] ledger = Array.Empty<PdfRepairRecord>();
        var report = PdfFont.Run("RecoverPdf", "managed.repair", policy, cancellationToken, context =>
        {
            using var source = new BorrowedStream(input, true, context);
            original = PdfByteStore.Read(source, context);
            originalHash = Hash(original, context);
            var scanner = new PdfRepairScanner(original, options, context);
            scanner.Read();
            bool sourceSignatures = scanner.Objects.Values.Any(value => value.HasSignatureByteRanges);
            if (sourceSignatures && !options.AllowSignatureInvalidation)
                throw Error(context, "SignatureRewrite", "Physical source signatures require explicit invalidation permission before any recovery or discard, including inactive/orphan signature bodies.");
            var active = Select(scanner, options, context);
            var trailer = PdfWriterCore.CopyDictionary(scanner.Trailer.Entries, context);
            PdfObjectId root;
            if (options.Root.HasValue)
            {
                root = options.Root.Value;
                scanner.Add("Repair.RootSelected", root, null, "The caller explicitly selected the recovered root identity.");
            }
            else if (trailer.TryGetValue("Root", out var declared) && declared.Kind == PdfValueKind.Reference)
                root = declared.ReferenceId;
            else
            {
                context.Consume(PdfResourceKind.ExpandedBytes, active.Count * 16L + 64);
                var catalogs = active.Where(id => IsCatalog(scanner.Objects[id])).ToArray();
                if (catalogs.Length != 1)
                    throw Error(context, "RootAuthority", "A missing root requires one unique active Catalog or an explicit root selection.");
                root = catalogs[0];
                scanner.Add("Repair.RootInferred", root, scanner.Offsets[root], "The sole active Catalog supplies the missing trailer root.");
            }

            if (!active.Contains(root) || !IsCatalog(scanner.Objects[root]))
                throw Error(context, "RootAuthority", "The selected root must be an active recovered Catalog dictionary.");
            trailer["Root"] = PdfValue.Reference(root);
            foreach (string key in new[]
            {
                "Prev",
                "XRefStm",
                "W",
                "Index",
                "Filter",
                "DecodeParms",
                "Length",
                "Type"
            }

            )
                trailer.Remove(key);
            if (trailer.TryGetValue("Encrypt", out var encrypted) && encrypted.Kind != PdfValueKind.Null)
                throw Error(context, "EncryptionProfile", "Explicitly decrypt the document before structural recovery.");
            int maximum = active.Max(id => id.Number);
            trailer["Size"] = PdfValue.Integer((long)maximum + 1);
            var trailerValue = PdfValue.OwnedDictionary(trailer);
            var reachable = PdfStructuralValidation.Reach(trailerValue, active, id => scanner.Objects[id], context, (owner, target) =>
            {
                scanner.Add("Repair.UnresolvedReference", owner, owner.HasValue ? scanner.Offsets[owner.Value] : null, "A missing exact target identity " + target.Number + " " + target.Generation + " remains PDF null; no object is invented.");
                if (options.RejectDanglingReferences)
                    throw Error(context, "UnresolvedReference", "A dangling/generation-mismatched reference blocks recovery. Supply the missing authoritative object or explicitly retain null-reference semantics.");
            });
            context.Consume(PdfResourceKind.ExpandedBytes, active.Count * 16L + 64);
            foreach (var id in active.OrderBy(id => id.Number).ToArray())
                if (!reachable.Contains(id))
                {
                    if (options.Orphans == PdfRepairOrphanPolicy.Preserve)
                    {
                        scanner.Add("Repair.OrphanPreserved", id, scanner.Offsets[id], "An indexed unreachable object is explicitly retained.");
                        PdfStructuralValidation.InspectReferences(scanner.Objects[id], id, active, context, (owner, target) =>
                        {
                            scanner.Add("Repair.UnresolvedReference", owner, scanner.Offsets[id], "An explicitly retained orphan contains a missing exact reference target " + target.Number + " " + target.Generation + ".");
                            if (options.RejectDanglingReferences)
                                throw Error(context, "UnresolvedReference", "A retained orphan contains a dangling reference. Supply its target, discard the orphan explicitly or explicitly retain PDF null-reference semantics.");
                        });
                    }
                    else
                    {
                        context.Report(new PdfDiagnostic("Repair.OrphanDiscarded", "Explicit orphan removal discards an indexed object; its identity and source offset are retained in the repair ledger.", lossCategory: PdfLossCategory.OmittedContent));
                        active.Remove(id);
                        scanner.Add("Repair.OrphanDiscarded", id, scanner.Offsets[id], "The caller-authorized orphan policy discarded this indexed unreachable object.");
                    }
                }

            context.Consume(PdfResourceKind.ExpandedBytes, active.Count * 160L + 256);
            var index = new Dictionary<int, PdfCrossReference>();
            foreach (var id in active)
                index.Add(id.Number, new(id, PdfCrossReferenceKind.Offset, scanner.Offsets[id]));
            var revision = new PdfRevision(0, trailerValue, index, false);
            foreach (var id in active.OrderBy(id => id.Number))
            {
                revision.Values.Add(id.Number, scanner.Objects[id]);
                scanner.Add("Repair.ObjectReindexed", id, scanner.Offsets[id], "This exact selected generation is emitted with a new independently generated byte offset.");
            }

            var recovered = new PdfSyntaxDocument(scanner.Version, original, new() { revision }, context.Policy);
            using var edit = new PdfTransaction(recovered, context.Policy);
            using var buffer = new PdfCodecBufferStream(context, context.Available(PdfResourceKind.ExpandedBytes));
            using (var destination = new BorrowedStream(buffer, false, context))
                edit.WriteCore(destination, new(allowSignatureInvalidation: options.AllowSignatureInvalidation), context);
            repaired = buffer.Finish();
            var verified = new PdfSyntaxReader(repaired, context).Read();
            if (!verified.Revisions[0].Trailer.Entries.TryGetValue("Root", out var outputRoot) || outputRoot.Kind != PdfValueKind.Reference || !IsCatalog(verified.Resolve(outputRoot.ReferenceId)))
                throw Error(context, "PostValidation", "The reconstructed output failed strict root validation; no output was copied.");
            if (sourceSignatures)
                scanner.Add("Repair.SignatureInvalidated", null, null, "The caller explicitly authorized a signature-invalidating full rewrite; no cryptographic signature claim is made.");
            scanner.Add("Repair.StrictPostValidation", null, null, "The complete reconstructed file passed the ordinary strict syntax reader; payload/content/standards qualification is separate.");
            repairedHash = Hash(repaired, context);
            context.Consume(PdfResourceKind.ExpandedBytes, scanner.Ledger.Count * 8L);
            ledger = scanner.Ledger.ToArray();
            Copy(repaired, output, context);
        }, PdfCapability.WriteDocument);
        return new(original!, repaired!, originalHash, repairedHash, ledger, report);
    }

    private static HashSet<PdfObjectId> Select(PdfRepairScanner scanner, PdfRepairOptions options, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, scanner.Objects.Count * 128L + (options.ActiveObjects?.Count ?? 0) * 64L + 256);
        var active = new HashSet<PdfObjectId>();
        if (options.ActiveObjects != null)
        {
            foreach (var id in options.ActiveObjects)
            {
                if (!scanner.Objects.ContainsKey(id))
                    throw Error(context, "MissingSelectedObject", "An explicitly selected exact object identity has no safely recovered body.");
                active.Add(id);
            }

            scanner.Add("Repair.ActiveInventorySelected", null, null, "The caller supplied the complete active identity inventory; no latest-by-position or generation inference was used.");
        }
        else
            foreach (var pair in scanner.Status)
            {
                if (!pair.Value.Active)
                    continue;
                if (pair.Key == 0)
                    throw Error(context, "XrefIdentity", "Object zero cannot be active.");
                var id = new PdfObjectId(pair.Key, pair.Value.Generation);
                if (!scanner.Objects.ContainsKey(id))
                    throw Error(context, "MissingIndexedObject", "An in-use xref identity has no matching safely framed physical body; object substitution is forbidden.");
                active.Add(id);
            }

        if (active.Count == 0)
            throw Error(context, "NoActiveObjects", "No authoritative active objects remain.");
        foreach (var id in scanner.Objects.Keys.OrderBy(id => id.Number))
            if (!active.Contains(id))
                scanner.Add("Repair.InactiveBodyExcluded", id, scanner.Offsets[id], "An unindexed/free/nonselected physical body is excluded; it cannot be resurrected by source position.");
        return active;
    }

    private static bool IsCatalog(PdfValue value) => value.Kind == PdfValueKind.Dictionary && value.Entries.TryGetValue("Type", out var type) && type.Kind == PdfValueKind.Name && type.NameValue == "Catalog";
    private static string Hash(PdfByteStore bytes, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 69632);
        var part = new byte[65536];
        using var algorithm = SHA256.Create();
        using var input = bytes.Open(0, bytes.Length);
        int count;
        while ((count = input.Read(part, 0, part.Length)) > 0)
        {
            context.Checkpoint();
            algorithm.TransformBlock(part, 0, count, part, 0);
        }

        algorithm.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
        return BitConverter.ToString(algorithm.Hash!).Replace("-", "").ToLowerInvariant();
    }

    private static void Copy(PdfByteStore bytes, Stream output, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 65536);
        var part = new byte[65536];
        using var input = bytes.Open(0, bytes.Length);
        using var lease = new BorrowedStream(output, false, context);
        int count;
        while ((count = input.Read(part, 0, part.Length)) > 0)
        {
            context.Checkpoint();
            lease.Write(part, 0, count);
        }

        context.Checkpoint();
    }

    private static PdfException Error(PdfOperationContext context, string code, string message) => PdfWriterCore.Error(context, "Repair." + code, message);
}
