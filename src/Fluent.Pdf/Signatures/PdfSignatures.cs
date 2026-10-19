using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Exact-byte detached signatures, pre-created fields and conservative certification-aware writing. Certificate trust is separate.</summary>
public static partial class PdfSignatures
{
    /// <summary>Creates at most128 flat signature fields on an unsigned source, including explicitly supplied Form appearances. Pre-create all future signing fields before certification.</summary>
    public static PdfDocument PrepareFields(PdfDocumentSnapshot source, IEnumerable<PdfSignatureFieldDefinition> fields, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || fields == null)
            throw new ArgumentNullException(source == null ? nameof(source) : nameof(fields));
        return PdfGraphEdit.Run("PrepareSignatureFields", source, null, policy ?? source.Options.OperationPolicy, cancellationToken, g =>
        {
            var c = g.Context;
            if (g.Graph.HasSignatureByteRanges)
                throw Error(c, "PreparationAfterSigning", "Prepare all signature fields before signing any revision.");
            g.RequireUnstructuredContent();
            var form = g.Get(g.Catalog, "AcroForm");
            if (form.Kind != PdfValueKind.Null && form.Kind != PdfValueKind.Dictionary || g.Get(form, "XFA").Kind != PdfValueKind.Null)
                throw Error(c, "FormProfile", "A passive AcroForm dictionary without XFA is required.");
            var needs = g.Get(form, "NeedAppearances");
            if (needs.Kind != PdfValueKind.Null && (needs.Kind != PdfValueKind.Boolean || needs.BooleanValue))
                throw Error(c, "FormProfile", "Signature preparation requires explicit existing appearances; implicit appearance regeneration must be resolved first.");
            var roots = g.Get(form, "Fields");
            if (roots.Kind != PdfValueKind.Null && roots.Kind != PdfValueKind.Array)
                throw Error(c, "FormProfile", "A direct field array is required.");
            c.Consume(PdfResourceKind.ExpandedBytes, (roots.Kind == PdfValueKind.Array ? roots.Items.Count : 0) * 32L + 4096);
            var list = roots.Kind == PdfValueKind.Array ? roots.Items.ToList() : new List<PdfValue>();
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in list)
                names.Add(Name(g.Get(g.Resolve(field), "T"), c));
            var writer = new PdfResourceWriter(g.Edit, c, new());
            int count = 0;
            foreach (var definition in fields)
            {
                c.Checkpoint();
                if (++count > 128)
                    throw Error(c, "FieldLimit", "At most128 signature fields can be prepared in one operation.");
                if (definition == null || definition.PageIndex >= g.Snapshot.Pages.Count || !names.Add(definition.Name))
                    throw Error(c, "FieldIdentity", "Signature fields require unique names and valid pages.");
                c.Consume(PdfResourceKind.Objects, 8);
                c.Consume(PdfResourceKind.ExpandedBytes, 2048 + definition.Name.Length * 4L);
                var page = g.Snapshot.Pages[definition.PageIndex].Source!;
                var b = definition.Bounds;
                var d = D(("Type", N("Annot")), ("Subtype", N("Widget")), ("FT", N("Sig")), ("T", PdfTextStrings.Encode(definition.Name, c)), ("Rect", PdfGraphicsNumbers.Array(b.X, b.Y, b.Right, b.Top)), ("P", PdfValue.Reference(page.ObjectId)), ("F", PdfValue.Integer(132)));
                if (definition.Appearance != null)
                {
                    PdfAnnotation.ValidateAppearance(definition.Appearance, c);
                    d["AP"] = PdfGraphicsNumbers.Dict(("N", writer.Embed(definition.Appearance)));
                }

                var id = g.Edit.Add(PdfValue.OwnedDictionary(d));
                list.Add(PdfValue.Reference(id));
                var pd = g.Copy(g.Edit.Resolve(page.ObjectId));
                var old = g.Get(PdfValue.OwnedDictionary(pd), "Annots");
                if (old.Kind != PdfValueKind.Null && old.Kind != PdfValueKind.Array)
                    throw Error(c, "AnnotationProfile", "Page annotations must be an array.");
                c.Consume(PdfResourceKind.ExpandedBytes, (old.Kind == PdfValueKind.Array ? old.Items.Count : 0) * 32L + 64);
                var annots = old.Kind == PdfValueKind.Array ? old.Items.ToList() : new List<PdfValue>();
                annots.Add(PdfValue.Reference(id));
                pd["Annots"] = PdfValue.OwnedArray(annots);
                g.Edit.Set(page.ObjectId, PdfValue.OwnedDictionary(pd));
            }

            if (count == 0)
                throw Error(c, "FieldLimit", "At least one explicit signature field is required.");
            var fd = g.Copy(form);
            fd["Fields"] = PdfValue.OwnedArray(list);
            fd["SigFlags"] = PdfValue.Integer(3);
            fd["NeedAppearances"] = PdfValue.Boolean(false);
            var catalog = g.Copy(g.Catalog);
            catalog["AcroForm"] = PdfValue.Reference(g.Edit.Add(PdfValue.OwnedDictionary(fd)));
            g.SetCatalog(catalog);
        }, minimumVersion: "1.5");
    }

    /// <summary>Signs one pre-created blank field by appending a revision to the exact immutable source. Existing bytes are never rewritten; cryptographic self-verification is mandatory before returning.</summary>
    public static PdfSignedResult Sign(PdfSyntaxDocument source, PdfSignatureOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Validate(source, options, providers);
        PdfByteStore? result = null;
        var selected = policy ?? source.ReadPolicy;
        var report = PdfFont.Run("SignPdf", "managed.signatures", selected, cancellationToken, c => result = Build(source, options, providers, c), PdfCapability.Signatures);
        return new(result!, report, selected);
    }

    /// <summary>Signs privately and atomically replaces a file only after complete exact-byte self-verification.</summary>
    public static PdfSignedResult SignFile(PdfSyntaxDocument source, PdfSignatureOptions options, PdfProviderRegistry providers, string path, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Validate(source, options, providers);
        Guard.Text(path, nameof(path), 32767);
        PdfByteStore? result = null;
        var selected = policy ?? source.ReadPolicy;
        var report = PdfFont.Run("SignPdfFile", "managed.signatures", selected, cancellationToken, c =>
        {
            result = Build(source, options, providers, c);
            PdfAtomicFile.Write(path, c, s => Copy(result, s, c));
        }, PdfCapability.Signatures);
        return new(result!, report, selected);
    }

    /// <summary>Checks active indirect detached signatures, exact excluded Contents tokens and covered revision boundaries. This does not evaluate trust/revocation or authorize later document modifications.</summary>
    public static IReadOnlyList<PdfSignatureObservation> Inspect(PdfSyntaxDocument source, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || providers == null)
            throw new ArgumentNullException(source == null ? nameof(source) : nameof(providers));
        var provider = providers.GetRequired<IPdfSignatureProvider>(PdfCapability.Signatures);
        var records = new List<PdfSignatureObservation>();
        PdfFont.Run("InspectSignatures", "managed.signatures", policy ?? source.ReadPolicy, cancellationToken, c =>
        {
            using var edit = new PdfTransaction(source, c.Policy);
            foreach (var id in edit.ActiveIds(c))
            {
                var value = edit.Resolve(id);
                if (value.Kind != PdfValueKind.Dictionary || !value.Entries.ContainsKey("ByteRange"))
                    continue;
                var validated = Ranges(source, id, value, c);
                using var bytes = new RangeStream(source.Source, validated.Ranges);
                using var signed = c.BorrowExpandedInput(bytes);
                using var cms = c.BorrowExpandedInput(new MemoryStream(validated.Cms, false));
                bool valid = provider.Verify(signed, cms, c);
                c.Checkpoint();
                c.Consume(PdfResourceKind.ExpandedBytes, 128);
                records.Add(new(id, validated.Ranges, valid, validated.Ranges[2] + validated.Ranges[3]));
            }
        }, PdfCapability.Signatures);
        return records.AsReadOnly();
    }

    private static void Validate(PdfSyntaxDocument source, PdfSignatureOptions options, PdfProviderRegistry providers)
    {
        if (source == null || options == null || providers == null)
            throw new ArgumentNullException(source == null ? nameof(source) : options == null ? nameof(options) : nameof(providers));
        _ = providers.GetRequired<IPdfSignatureProvider>(PdfCapability.Signatures);
    }

    private static PdfByteStore Build(PdfSyntaxDocument source, PdfSignatureOptions options, PdfProviderRegistry providers, PdfOperationContext c)
    {
        if (source.DecryptionApplied || source.Revisions.Any(r => r.Trailer.Entries.ContainsKey("Encrypt")))
            throw Error(c, "EncryptedProfile", "Signing encrypted inputs requires a separately qualified crypt-filter profile.");
        PdfSignaturePolicy.RequireSigning(source, options, c);
        using var edit = new PdfTransaction(source, c.Policy);
        PdfValue Resolve(PdfValue v) => ResolveCore(source, v, c);
        var root = edit.Trailer.Entries["Root"];
        var catalog = Resolve(root);
        if (root.Kind != PdfValueKind.Reference)
            throw Error(c, "CatalogProfile", "An indirect catalog is required.");
        var form = Get(catalog, "AcroForm", Resolve);
        var fields = Get(form, "Fields", Resolve);
        if (fields.Kind != PdfValueKind.Array || Get(form, "XFA", Resolve).Kind != PdfValueKind.Null)
            throw Error(c, "FormProfile", "Pre-created passive signature fields are required.");
        var needs = Get(form, "NeedAppearances", Resolve);
        if (needs.Kind != PdfValueKind.Null && (needs.Kind != PdfValueKind.Boolean || needs.BooleanValue))
            throw Error(c, "FormProfile", "Signing requires explicit existing appearances; implicit appearance regeneration must be resolved first.");
        PdfObjectId? target = null;
        foreach (var field in fields.Items)
        {
            c.Checkpoint();
            var d = Resolve(field);
            if (Get(d, "Kids", Resolve).Kind != PdfValueKind.Null)
                throw Error(c, "HierarchicalFieldProfile", "This signing profile requires flat pre-created fields.");
            if (Name(Get(d, "T", Resolve), c) != options.FieldName)
                continue;
            if (target.HasValue || field.Kind != PdfValueKind.Reference || Get(d, "FT", Resolve).Kind != PdfValueKind.Name || Get(d, "FT", Resolve).NameValue != "Sig" || Get(d, "V", Resolve).Kind != PdfValueKind.Null)
                throw Error(c, "FieldIdentity", "The exact target must be one unique blank indirect signature field.");
            target = field.ReferenceId;
        }

        if (!target.HasValue)
            throw Error(c, "FieldMissing", "The explicitly named blank signature field is absent.");
        c.Consume(PdfResourceKind.ExpandedBytes, options.ReservedCmsBytes * 4L + 16384);
        c.Consume(PdfResourceKind.Objects, 32);
        var signature = D(("Type", N("Sig")), ("Filter", N("Adobe.PPKLite")), ("SubFilter", N("adbe.pkcs7.detached")), ("ByteRange", PdfValue.Array(Enumerable.Repeat(PdfValue.Integer(long.MaxValue), 4).ToArray())), ("Contents", PdfValue.String(new byte[options.ReservedCmsBytes])));
        if (options.Reason != null)
            signature["Reason"] = PdfTextStrings.Encode(options.Reason, c);
        if (options.SigningTime.HasValue)
            signature["M"] = PdfTextStrings.Encode(PdfMetadataDates.Format(options.SigningTime.Value), c);
        var references = new List<PdfValue>();
        if (options.Certification != PdfCertificationPermission.None)
            references.Add(PdfValue.OwnedDictionary(D(("Type", N("SigRef")), ("TransformMethod", N("DocMDP")), ("TransformParams", PdfValue.OwnedDictionary(D(("Type", N("TransformParams")), ("P", PdfValue.Integer((int)options.Certification)), ("V", N("1.2"))))))));
        if (options.FieldLock != null)
        {
            var p = D(("Type", N("TransformParams")), ("Action", N(options.FieldLock.Action.ToString())), ("V", N("1.2")));
            if (options.FieldLock.Fields.Count > 0)
                p["Fields"] = PdfValue.Array(options.FieldLock.Fields.Select(f => PdfTextStrings.Encode(f, c)).ToArray());
            references.Add(PdfValue.OwnedDictionary(D(("Type", N("SigRef")), ("TransformMethod", N("FieldMDP")), ("Data", root), ("TransformParams", PdfValue.OwnedDictionary(p)))));
        }

        if (references.Count > 0)
            signature["Reference"] = PdfValue.OwnedArray(references);
        var signatureId = edit.Add(PdfValue.OwnedDictionary(signature));
        var changed = PdfWriterCore.CopyDictionary(edit.Resolve(target.Value).Entries, c);
        changed["V"] = PdfValue.Reference(signatureId);
        edit.Set(target.Value, PdfValue.OwnedDictionary(changed));
        if (options.Certification != PdfCertificationPermission.None)
        {
            var cat = PdfWriterCore.CopyDictionary(catalog.Entries, c);
            var permissions = Get(catalog, "Perms", Resolve);
            var p = permissions.Kind == PdfValueKind.Null ? new Dictionary<string, PdfValue>() : PdfWriterCore.CopyDictionary(permissions.Entries, c);
            p["DocMDP"] = PdfValue.Reference(signatureId);
            cat["Perms"] = PdfValue.OwnedDictionary(p);
            edit.Set(root.ReferenceId, PdfValue.OwnedDictionary(cat));
        }

        using var buffer = new PdfCodecBufferStream(c, c.Available(PdfResourceKind.ExpandedBytes));
        using (var output = new BorrowedStream(buffer, false, c))
            edit.WriteCore(output, new(PdfWriteMode.Incremental, PdfCrossReferenceFormat.Table), c, validatedSignatureEdit: true);
        var staged = buffer.Finish();
        if (staged.Length > int.MaxValue - 32)
            throw Error(c, "Extent", "The contiguous signature-patching profile exceeds its finite extent.");
        c.Consume(PdfResourceKind.ExpandedBytes, staged.Length * 2 + options.ReservedCmsBytes * 4L + 4096);
        var raw = new byte[(int)staged.Length];
        using (var input = staged.Open(0, staged.Length))
        {
            int at = 0, n;
            while ((n = input.Read(raw, at, raw.Length - at)) > 0)
            {
                c.Checkpoint();
                at += n;
            }
        }

        var parsed = new PdfSyntaxReader(staged, c).Read();
        var entry = parsed.Revisions[0].Entries.Single(e => e.Id == signatureId);
        int start = checked((int)entry.Offset);
        int contents = Find(raw, start, "/Contents <", c) + 10;
        int after = checked(contents + options.ReservedCmsBytes * 2 + 2);
        if (raw[contents] != 60 || raw[after - 1] != 62)
            throw Error(c, "Reservation", "The owned signature reservation does not match its serialized extent.");
        long[] ranges =
        {
            0,
            contents,
            after,
            raw.Length - after
        };
        int range = Find(raw, start, "/ByteRange [", c) + 12;
        for (int i = 0; i < 4; i++)
        {
            var number = Encoding.ASCII.GetBytes(ranges[i].ToString("D19", CultureInfo.InvariantCulture));
            Array.Copy(number, 0, raw, range + i * 20, 19);
        }

        var provider = providers.GetRequired<IPdfSignatureProvider>(PdfCapability.Signatures);
        using var cmsBuffer = new PdfCodecBufferStream(c, options.ReservedCmsBytes);
        using (var signed = c.BorrowExpandedInput(new RangeStream(raw, ranges)))
        using (var output = new BorrowedStream(cmsBuffer, false, c))
            provider.Sign(signed, output, c);
        c.Checkpoint();
        var encoded = cmsBuffer.Finish();
        if (encoded.Length == 0 || encoded.Length > options.ReservedCmsBytes)
            throw Error(c, "Reservation", "The signer returned an empty or oversized CMS; the signer is never retried.");
        var cmsBytes = new byte[(int)encoded.Length];
        for (int i = 0; i < cmsBytes.Length; i++)
            cmsBytes[i] = (byte)encoded.At(i);
        if (DerLength(cmsBytes, c) != cmsBytes.Length)
            throw Error(c, "CmsEncoding", "A single definite-length DER CMS value is required.");
        using (var signed = c.BorrowExpandedInput(new RangeStream(raw, ranges)))
        using (var cms = c.BorrowExpandedInput(new MemoryStream(cmsBytes, false)))
            if (!provider.Verify(signed, cms, c))
                throw Error(c, "SelfVerification", "The explicit signer failed exact-byte cryptographic self-verification.");
        c.Checkpoint();
        const string hex = "0123456789ABCDEF";
        for (int i = 0; i < cmsBytes.Length; i++)
        {
            if ((i & 4095) == 0)
                c.Checkpoint();
            raw[contents + 1 + i * 2] = (byte)hex[cmsBytes[i] >> 4];
            raw[contents + 2 + i * 2] = (byte)hex[cmsBytes[i] & 15];
        }

        var result = PdfByteStore.Read(new MemoryStream(raw, false), c);
        var final = new PdfSyntaxReader(result, c).Read();
        _ = Ranges(final, signatureId, final.Resolve(signatureId), c);
        return result;
    }

    internal static PdfValue ResolveCore(PdfSyntaxDocument source, PdfValue value, PdfOperationContext c, int revision = 0)
    {
        var seen = new HashSet<PdfObjectId>();
        while (value.Kind == PdfValueKind.Reference)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 48);
            if (!seen.Add(value.ReferenceId))
                throw Error(c, "ReferenceCycle", "A signature reference cycle was rejected.");
            value = source.Resolve(value.ReferenceId, revision);
        }

        return value;
    }

    internal static PdfValue Get(PdfValue dictionary, string key, Func<PdfValue, PdfValue> resolve) => dictionary.Kind != PdfValueKind.Null && dictionary.Entries.TryGetValue(key, out var value) ? resolve(value) : PdfValue.Null;
    internal static string Name(PdfValue value, PdfOperationContext c) => value.Kind == PdfValueKind.String ? PdfTextStrings.Decode(value, c) : throw Error(c, "FieldName", "A valid PDF text field name is required.");
    internal static Dictionary<string, PdfValue> D(params (string Key, PdfValue Value)[] values) => values.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
    internal static PdfValue N(string value) => PdfValue.Name(value);
    internal static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Signature." + code, message);
}
