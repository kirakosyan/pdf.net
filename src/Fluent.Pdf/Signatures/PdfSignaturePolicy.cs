namespace Fluent.Pdf;
// A conservative writer barrier: a generic edit cannot silently claim certification preservation.
internal static class PdfSignaturePolicy
{
    internal static void RequireWriter(PdfSyntaxDocument? source, PdfWriteOptions options, PdfOperationContext c, bool validatedSignatureEdit)
    {
        if (source == null || options.AllowSignatureInvalidation || validatedSignatureEdit)
            return;
        if (Protection(source, null, c).Protected)
            throw PdfSignatures.Error(c, "CertifiedEditProfile", "This document has certification/field locks. Only an explicitly validated permitted-edit profile or explicit signature invalidation authority may change it.");
    }

    internal static void RequireSigning(PdfSyntaxDocument source, PdfSignatureOptions options, PdfOperationContext c)
    {
        if (options.Certification != PdfCertificationPermission.None && source.HasSignatureByteRanges)
            throw PdfSignatures.Error(c, "CertificationOrder", "Certification must be the first signature.");
        var p = Protection(source, options.FieldName, c);
        if (p.NoChanges || p.Locked)
            throw PdfSignatures.Error(c, "CertificationPermission", "An existing DocMDP/FieldMDP policy prohibits signing this field.");
        if (options.Certification != PdfCertificationPermission.None && p.Protected)
            throw PdfSignatures.Error(c, "CertificationOrder", "A prior document protection transform already exists.");
    }

    private static (bool Protected, bool NoChanges, bool Locked) Protection(PdfSyntaxDocument source, string? field, PdfOperationContext c)
    {
        bool protect = false, noChanges = false, locked = false;
        // Historical policies remain authoritative even if a later untrusted revision removed their active objects.
        for (int revision = source.Revisions.Count - 1; revision >= 0; revision--)
        {
            PdfValue Resolve(PdfValue v) => PdfSignatures.ResolveCore(source, v, c, revision);
            var trailer = source.Revisions[revision].Trailer;
            var catalog = PdfSignatures.Get(trailer, "Root", Resolve);
            var permissions = PdfSignatures.Get(catalog, "Perms", Resolve);
            var certification = PdfSignatures.Get(permissions, "DocMDP", Resolve);
            if (certification.Kind != PdfValueKind.Null)
            {
                protect = true;
                if (certification.Kind != PdfValueKind.Dictionary || !certification.Entries.ContainsKey("ByteRange") || !Process(certification))
                    throw PdfSignatures.Error(c, "TransformProfile", "Catalog DocMDP must identify a signature with a valid certification transform.");
            }

            if (!source.HasSignatureByteRanges)
                continue;
            var seen = new HashSet<PdfValue>();
            foreach (var value in source.Revisions[revision].Values.Values)
                Walk(value);
            void Walk(PdfValue value)
            {
                c.Checkpoint();
                using var depth = c.EnterScope(PdfResourceKind.Depth);
                c.Consume(PdfResourceKind.Objects, 1);
                c.Consume(PdfResourceKind.ExpandedBytes, 48);
                if (!seen.Add(value))
                    return;
                if (value.Kind == PdfValueKind.Dictionary && value.Entries.ContainsKey("ByteRange"))
                    Process(value);
                if (value.Kind == PdfValueKind.Array)
                    foreach (var child in value.Items)
                        Walk(child);
                if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
                    foreach (var child in value.Entries.Values)
                        Walk(child);
            }

            bool Process(PdfValue signature)
            {
                bool docMdp = false;
                var refs = PdfSignatures.Get(signature, "Reference", Resolve);
                if (refs.Kind == PdfValueKind.Null)
                    return false;
                if (refs.Kind != PdfValueKind.Array)
                    throw PdfSignatures.Error(c, "TransformProfile", "Signature transforms require an explicit reference array.");
                foreach (var reference in refs.Items)
                {
                    var r = Resolve(reference);
                    var method = PdfSignatures.Get(r, "TransformMethod", Resolve);
                    var p = PdfSignatures.Get(r, "TransformParams", Resolve);
                    if (method.Kind != PdfValueKind.Name || p.Kind != PdfValueKind.Dictionary)
                        throw PdfSignatures.Error(c, "TransformProfile", "Malformed signature protection transform.");
                    if (method.NameValue == "DocMDP")
                    {
                        protect = true;
                        docMdp = true;
                        var permission = PdfSignatures.Get(p, "P", Resolve);
                        long value = 2;
                        if (permission.Kind != PdfValueKind.Null && (!permission.TryGetInteger(out value) || value < 1 || value > 3))
                            throw PdfSignatures.Error(c, "TransformProfile", "DocMDP permission must be1,2or3.");
                        noChanges |= value == 1;
                    }
                    else if (method.NameValue == "FieldMDP")
                    {
                        protect = true;
                        var action = PdfSignatures.Get(p, "Action", Resolve);
                        var names = PdfSignatures.Get(p, "Fields", Resolve);
                        if (action.Kind != PdfValueKind.Name || action.NameValue != "All" && action.NameValue != "Include" && action.NameValue != "Exclude")
                            throw PdfSignatures.Error(c, "TransformProfile", "Unknown FieldMDP lock action.");
                        if (action.NameValue == "All")
                        {
                            locked |= field != null;
                            continue;
                        }

                        if (names.Kind != PdfValueKind.Array || names.Items.Count > 1024)
                            throw PdfSignatures.Error(c, "TransformProfile", "A finite FieldMDP field list is required.");
                        bool match = false;
                        foreach (var name in names.Items)
                        {
                            string value = PdfSignatures.Name(Resolve(name), c);
                            match |= field != null && (field == value || field.StartsWith(value + ".", StringComparison.Ordinal));
                        }

                        if (field != null)
                            locked |= action.NameValue == "Include" ? match : !match;
                    }
                    else
                        throw PdfSignatures.Error(c, "TransformProfile", "An unsupported protection transform cannot be silently preserved.");
                }

                return docMdp;
            }
        }

        return (protect, noChanges, locked);
    }
}
