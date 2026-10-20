using System.IO;

namespace Fluent.Pdf;
// Original sequential FDF value reader. Cross references are optional; supplied tables are verified.
internal static class PdfFormFdf
{
    internal static List<PdfFormDataField> Read(PdfByteStore bytes, PdfOperationContext context)
    {
        var cursor = new PdfSyntaxCursor(bytes, context);
        if (bytes.Length < 14 || cursor.Text(0, 8) != "%FDF-1.2" || !PdfSyntaxCursor.White(bytes.At(8)))
            throw Error("Header", "The FDF header must be %FDF-1.2.");
        long end = bytes.Length;
        while (end > 0 && PdfSyntaxCursor.White(bytes.At(end - 1)))
        {
            context.Checkpoint();
            end--;
        }

        if (end < 5 || cursor.Text(end - 5, 5) != "%%EOF")
            throw Error("End", "A final FDF EOF marker is required.");
        cursor = new(bytes, context, 8, end - 5);
        var objects = new Dictionary<int, (PdfValue Value, long Offset)>();
        PdfValue? trailer = null;
        long xref = -1;
        while (true)
        {
            cursor.Skip();
            if (cursor.Position == cursor.End)
                break;
            long offset = cursor.Position;
            if (cursor.TryKeyword("xref"))
            {
                xref = offset;
                var seen = new HashSet<int>();
                while (!cursor.TryKeyword("trailer"))
                {
                    long first = cursor.Integer(), count = cursor.Integer();
                    if (first < 0 || first > int.MaxValue || count < 0 || count > int.MaxValue - first)
                        throw Error("Xref", "FDF cross-reference counts are outside range.");
                    for (long i = 0; i < count; i++)
                    {
                        context.Checkpoint();
                        context.Consume(PdfResourceKind.Objects, 1);
                        context.Consume(PdfResourceKind.ExpandedBytes, 48);
                        long position = cursor.Integer(), generation = cursor.Integer();
                        string kind = cursor.Token();
                        int number = (int)(first + i);
                        if (!seen.Add(number) || position < 0 || generation < 0 || generation > 65535 || kind != "f" && kind != "n")
                            throw Error("Xref", "FDF cross-reference entries must have unique valid identities.");
                        if (kind == "n" && (generation != 0 || !objects.TryGetValue(number, out var item) || item.Offset != position))
                            throw Error("Xref", "An FDF cross-reference entry does not match its object.");
                        if (kind == "f" && objects.ContainsKey(number))
                            throw Error("Xref", "An FDF object cannot also be declared free.");
                    }
                }

                foreach (int number in objects.Keys)
                    if (!seen.Contains(number))
                        throw Error("Xref", "A supplied FDF table must cover the file's objects.");
                trailer = cursor.Value();
                break;
            }

            if (cursor.TryKeyword("trailer"))
            {
                trailer = cursor.Value();
                break;
            }

            long id = cursor.Integer(), gen = cursor.Integer();
            cursor.Require("obj");
            if (id <= 0 || id > int.MaxValue || gen != 0 || objects.ContainsKey((int)id))
                throw Error("Object", "FDF objects require unique positive numbers and generation zero.");
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 128);
            var value = cursor.Value();
            if (value.Kind == PdfValueKind.Dictionary && cursor.TryKeyword("stream"))
            {
                if (cursor.Peek() == 13)
                {
                    cursor.Position++;
                    if (cursor.Peek() == 10)
                        cursor.Position++;
                }
                else if (cursor.Peek() == 10)
                    cursor.Position++;
                else
                    throw Error("Stream", "An FDF stream requires an immediate line ending.");
                if (!value.Entries.TryGetValue("Length", out var lengthValue) || !lengthValue.TryGetInteger(out long length) || length < 0 || length > cursor.End - cursor.Position)
                    throw Error("Stream", "FDF stream Length must be direct and within the input.");
                long start = cursor.Position;
                cursor.Position += length;
                cursor.Require("endstream");
                value = PdfValue.StreamValue(value, bytes, start, length);
            }

            cursor.Require("endobj");
            objects.Add((int)id, (value, offset));
        }

        if (trailer != null && cursor.TryKeyword("startxref"))
        {
            if (xref < 0 || cursor.Integer() != xref)
                throw Error("Xref", "The optional FDF cross-reference offset must identify the verified table.");
        }

        cursor.Skip();
        if (cursor.Position != cursor.End)
            throw Error("Syntax", "Unexpected or incremental data follows the FDF trailer.");
        PdfValue catalog;
        if (trailer != null)
        {
            if (trailer.Kind != PdfValueKind.Dictionary || trailer.Entries.Keys.Any(k => k != "Root" && k != "Size"))
                throw Error("Profile", "FDF trailer updates/encryption and additional instructions require a separate profile.");
            if (!trailer.Entries.TryGetValue("Root", out var root) || root.Kind != PdfValueKind.Reference)
                throw Error("Root", "The FDF trailer requires an indirect Root.");
            catalog = Resolve(root);
        }
        else
        {
            var candidates = new List<PdfValue>();
            foreach (var item in objects.Values)
                if (item.Value.Kind == PdfValueKind.Dictionary && item.Value.Entries.ContainsKey("FDF"))
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, 16);
                    candidates.Add(item.Value);
                }

            if (candidates.Count != 1)
                throw Error("Root", "A trailerless FDF requires one unambiguous catalog.");
            catalog = candidates[0];
        }

        if (catalog.Kind != PdfValueKind.Dictionary || catalog.Entries.Keys.Any(k => k != "FDF" && k != "Version" && k != "Type"))
            throw Error("Profile", "The input is outside the FDF field-value catalog profile.");
        var fdf = Get(catalog, "FDF");
        if (fdf.Kind != PdfValueKind.Dictionary || fdf.Entries.Keys.Any(k => k != "Fields" && k != "F" && k != "UF" && k != "ID"))
            throw Error("Profile", "Only FDF field values are imported; actions, files, annotations and appearance instructions require an explicit profile.");
        var fields = Get(fdf, "Fields");
        if (fields.Kind != PdfValueKind.Array)
            throw Error("Fields", "FDF Fields must be an array.");
        var result = new List<PdfFormDataField>();
        var visiting = new HashSet<PdfValue>();
        foreach (var field in fields.Items)
            Field(field, "");
        return result;
        PdfValue Resolve(PdfValue value)
        {
            var seen = new HashSet<int>();
            while (value.Kind == PdfValueKind.Reference)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                context.Consume(PdfResourceKind.ExpandedBytes, 48);
                if (value.ReferenceId.Generation != 0 || !seen.Add(value.ReferenceId.Number) || !objects.TryGetValue(value.ReferenceId.Number, out var item))
                    throw Error("Reference", "An FDF reference is missing, cyclic or has a nonzero generation.");
                value = item.Value;
            }

            return value;
        }

        PdfValue Get(PdfValue dictionary, string key) => dictionary.Kind == PdfValueKind.Dictionary && dictionary.Entries.TryGetValue(key, out var value) ? Resolve(value) : PdfValue.Null;
        void Field(PdfValue raw, string prefix)
        {
            context.Checkpoint();
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 192);
            var field = Resolve(raw);
            if (field.Kind != PdfValueKind.Dictionary || !visiting.Add(field))
                throw Error("FieldCycle", "FDF fields require a finite dictionary tree.");
            if (field.Entries.Keys.Any(k => k != "T" && k != "V" && k != "Kids"))
                throw Error("Profile", "Additional field import instructions require an explicit FDF metadata profile.");
            string partial = PdfTextStrings.Decode(Get(field, "T"), context);
            context.Consume(PdfResourceKind.ExpandedBytes, (partial.Length + (long)prefix.Length + 1) * 4 + 64);
            string name = prefix.Length == 0 ? partial : prefix + "." + partial;
            var kids = Get(field, "Kids");
            if (kids.Kind != PdfValueKind.Null && kids.Kind != PdfValueKind.Array)
                throw Error("Fields", "FDF Kids must be an array.");
            if (kids.Kind == PdfValueKind.Array && kids.Items.Count > 0)
            {
                if (field.Entries.ContainsKey("V"))
                    throw Error("Profile", "A value-bearing FDF field cannot also be a container in this interchange profile.");
                foreach (var child in kids.Items)
                    Field(child, name);
            }
            else
            {
                if (result.Count >= 100000)
                    throw Error("Count", "FDF field count exceeds the profile ceiling.");
                var value = Get(field, "V");
                if (value.Kind == PdfValueKind.Array)
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 32L + 64);
                    value = PdfValue.Array(value.Items.Select(Resolve).ToArray());
                }

                result.Add(new(name, value));
            }

            visiting.Remove(field);
        }

        PdfException Error(string code, string message) => PdfFormData.Error(context, "Fdf" + code, message);
    }

    internal static void Write(IReadOnlyList<PdfFormDataField> fields, Stream output, PdfOperationContext context)
    {
        var tree = PdfFormDataTree.Create(fields, context);
        var catalog = PdfGraphicsNumbers.Dict(("FDF", PdfGraphicsNumbers.Dict(("Fields", Children(tree)))));
        var sink = new PdfWriterCore.Sink(output, context);
        sink.Text("%FDF-1.2\n1 0 obj\n");
        sink.Value(catalog, true);
        sink.Text("\nendobj\ntrailer\n<< /Root 1 0 R >>\n%%EOF\n");
        sink.Flush();
        PdfValue Children(PdfFormDataTree parent)
        {
            using var depth = context.EnterScope(PdfResourceKind.Depth);
            context.Consume(PdfResourceKind.ExpandedBytes, parent.Children.Count * 192L + 64);
            var values = new List<PdfValue>();
            foreach (var pair in parent.Children)
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                var entries = new Dictionary<string, PdfValue>
                {
                    {
                        "T",
                        PdfTextStrings.Encode(pair.Key, context)
                    }
                };
                if (pair.Value.Value != null)
                    entries["V"] = pair.Value.Value;
                else
                    entries["Kids"] = Children(pair.Value);
                values.Add(PdfValue.OwnedDictionary(entries));
            }

            return PdfValue.OwnedArray(values);
        }
    }
}
