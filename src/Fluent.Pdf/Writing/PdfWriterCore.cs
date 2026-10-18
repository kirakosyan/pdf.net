using System.Globalization;
using System.IO;

namespace Fluent.Pdf;
internal static class PdfWriterCore
{
    private static readonly HashSet<string> XrefKeys = new(StringComparer.Ordinal)
    {
        "Size",
        "Prev",
        "XRefStm"
    };
    private static readonly HashSet<string> StreamKeys = new(StringComparer.Ordinal)
    {
        "Type",
        "W",
        "Index",
        "Length",
        "Filter",
        "DecodeParms"
    };
    internal static PdfValue TrailerFrom(PdfSyntaxDocument? source)
    {
        if (source == null)
            return PdfValue.Dictionary(new Dictionary<string, PdfValue>());
        return source.Revisions[0].Trailer.DictionaryView();
    }

    internal static Dictionary<string, PdfValue> CopyDictionary(IReadOnlyDictionary<string, PdfValue> entries, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.Objects, entries.Count + 1L);
        context.Consume(PdfResourceKind.ExpandedBytes, entries.Count * 128L + 512);
        var result = new Dictionary<string, PdfValue>(entries.Count, StringComparer.Ordinal);
        foreach (var pair in entries)
        {
            context.Checkpoint();
            result.Add(pair.Key, pair.Value);
        }

        return result;
    }

    internal static void Write(PdfSyntaxDocument? source, PdfValue trailer, IReadOnlyDictionary<PdfObjectId, PdfValue> changes, ISet<PdfObjectId> removed, int maximum, bool trailerChanged, Stream output, PdfWriteOptions options, PdfOperationContext context)
    {
        context.Checkpoint();
        bool incremental = options.Mode == PdfWriteMode.Incremental;
        if (incremental && source == null)
            throw Error(context, "Writer.IncrementalBaseRequired", "An incremental revision requires an immutable original file.");
        if (incremental && changes.Count == 0 && removed.Count == 0 && !trailerChanged && options.Version == null && options.CrossReferenceFormat == PdfCrossReferenceFormat.Auto && options.MinimumVersion==null && options.Extensions.Count==0 && options.UnknownExtensions==PdfUnknownExtensionPolicy.Preserve)
        {
            context.Consume(PdfResourceKind.InputBytes, source!.Source.Length);
            var exact = new Sink(output, context);
            using var original = source.OpenOriginalStream();
            exact.Copy(original);
            return;
        }

        if (Encrypted(trailer) || (source != null && source.Revisions.Any(revision => Encrypted(revision.Trailer))))
            throw Error(context, "Writer.EncryptionProviderRequired", "Changing or rewriting encrypted objects requires an explicitly configured encryption implementation.");
        long estimate = changes.Count + removed.Count + 1L;
        if (source != null)
            foreach (var revision in source.Revisions)
                estimate += revision.Entries.Count;
        if (estimate > context.Available(PdfResourceKind.ExpandedBytes) / 256)
            context.RejectLimit(PdfResourceKind.ExpandedBytes);
        context.Consume(PdfResourceKind.ExpandedBytes, estimate * 256);
        var entries = new Dictionary<int, PdfCrossReference>();
        var objects = new SortedDictionary<int, (PdfObjectId Id, PdfValue Value)>();
        if (source != null)
            foreach (var revision in source.Revisions)
                foreach (var entry in revision.Entries)
                {
                    context.Checkpoint();
                    if (entries.ContainsKey(entry.Id.Number))
                        continue;
                    entries.Add(entry.Id.Number, entry);
                    if (entry.Kind != PdfCrossReferenceKind.Free && entry.Id.Number != 0)
                        objects.Add(entry.Id.Number, (entry.Id, source.Resolve(entry.Id)));
                }

        foreach (var id in removed)
            objects.Remove(id.Number);
        foreach (var pair in changes)
            objects[pair.Key.Number] = (pair.Key, pair.Value);
        if (!trailer.Entries.TryGetValue("Root", out var root) || root.Kind != PdfValueKind.Reference)
            throw Error(context, "Writer.InvalidRoot", "The trailer must reference an active Catalog dictionary.");
        var catalog = Catalog(objects, root, context);
        int extensionFloor=17; // The Extensions catalog mechanism was introduced in PDF 1.7.
        bool extensionChanged=false;
        var sourceExtensions=catalog.Value.Entries.TryGetValue("Extensions",out var ext)?Dereference(objects,ext,context):PdfValue.Null;
        Dictionary<string,PdfValue>? mergedExtensions=null;
        if(sourceExtensions.Kind!=PdfValueKind.Null)
        {
            if(sourceExtensions.Kind!=PdfValueKind.Dictionary)throw Error(context,"Writer.ExtensionDictionary","Catalog Extensions must resolve to a dictionary.");
            foreach(var pair in sourceExtensions.Entries)
            {
                context.Checkpoint();
                if(options.UnknownExtensions==PdfUnknownExtensionPolicy.Reject&&!options.Extensions.Any(item=>item.Prefix==pair.Key))throw Error(context,"Writer.UnknownExtension","An undeclared source developer extension was rejected by policy.");
                var value=Dereference(objects,pair.Value,context);
                if(value.Kind!=PdfValueKind.Dictionary||!value.Entries.TryGetValue("BaseVersion",out var baseVersion)||!value.Entries.TryGetValue("ExtensionLevel",out var level))throw Error(context,"Writer.ExtensionDictionary","A developer extension requires BaseVersion and ExtensionLevel.");
                baseVersion=Dereference(objects,baseVersion,context);level=Dereference(objects,level,context);
                if(baseVersion.Kind!=PdfValueKind.Name||!level.TryGetInteger(out long extensionLevel)||extensionLevel<=0)throw Error(context,"Writer.ExtensionDictionary","A developer extension declares an invalid base version or level.");
                try{extensionFloor=Math.Max(extensionFloor,PdfVersionNumber.Parse(baseVersion.NameValue));}
                catch(ArgumentException){throw Error(context,"Writer.ExtensionVersion","The developer extension's base version is unsupported.");}
            }
        }
        foreach(var extension in options.Extensions)
        {
            mergedExtensions??=sourceExtensions.Kind==PdfValueKind.Null?new Dictionary<string,PdfValue>(StringComparer.Ordinal):CopyDictionary(sourceExtensions.Entries,context);
            extensionFloor=Math.Max(extensionFloor,PdfVersionNumber.Parse(extension.BaseVersion));
            var declaration=new Dictionary<string,PdfValue>(StringComparer.Ordinal);
            if(mergedExtensions.TryGetValue(extension.Prefix,out var previous))
            {
                previous=Dereference(objects,previous,context);declaration=CopyDictionary(previous.Entries,context);
                if(PdfVersionNumber.Parse(Dereference(objects,declaration["BaseVersion"],context).NameValue)>PdfVersionNumber.Parse(extension.BaseVersion)||Dereference(objects,declaration["ExtensionLevel"],context).IntegerValue>extension.ExtensionLevel)throw Error(context,"Writer.ExtensionDowngrade","A declared developer extension cannot lower an existing base version or extension level.");
            }
            context.Consume(PdfResourceKind.Objects,3);context.Consume(PdfResourceKind.ExpandedBytes,512);
            declaration["BaseVersion"]=PdfValue.Name(extension.BaseVersion);declaration["ExtensionLevel"]=PdfValue.Integer(extension.ExtensionLevel);mergedExtensions[extension.Prefix]=PdfValue.OwnedDictionary(declaration);extensionChanged=true;
        }
        if(extensionChanged)
        {
            var updated=CopyDictionary(catalog.Value.Entries,context);updated["Extensions"]=PdfValue.OwnedDictionary(mergedExtensions!);catalog=(catalog.Id,PdfValue.OwnedDictionary(updated));objects[catalog.Id.Number]=catalog;
        }
        int originalVersion = source == null ? 12 : PdfVersionNumber.Parse(source.Version);
        if (catalog.Value.Entries.TryGetValue("Version", out var declared))
        {
            declared = Dereference(objects, declared, context);
            if (declared.Kind != PdfValueKind.Null && declared.Kind != PdfValueKind.Name)
                throw Error(context, "Writer.UnsupportedVersion", "The catalog version must resolve to a supported version name.");
            try
            {
                if (declared.Kind == PdfValueKind.Name)
                    originalVersion = Math.Max(originalVersion, PdfVersionNumber.Parse(declared.NameValue));
            }
            catch (ArgumentException)
            {
                throw Error(context, "Writer.UnsupportedVersion", "The catalog declares an unsupported PDF version.");
            }
        }

        int version = options.Version == null ? (source == null ? 17 : originalVersion) : PdfVersionNumber.Parse(options.Version);
        if (source != null && version < originalVersion)
            throw Error(context, "Writer.VersionDowngrade", "A version downgrade requires explicit content conversion.");
        bool signature = source?.HasSignatureByteRanges ?? false;
        PdfValue ResolveFeature(PdfValue value)=>Dereference(objects,value,context);
        int requiredVersion=options.MinimumVersion==null?12:PdfVersionNumber.Parse(options.MinimumVersion);
        if(sourceExtensions.Kind!=PdfValueKind.Null||options.Extensions.Count>0)requiredVersion=Math.Max(requiredVersion,extensionFloor);
        foreach (var item in objects.Values)
            Inspect(item.Value, context, true, ref signature,ref requiredVersion,ResolveFeature);
        Inspect(trailer, context, false, ref signature,ref requiredVersion,ResolveFeature);
        if(version<requiredVersion)
        {
            if(options.Version!=null)throw Error(context,"Writer.VersionTooLow","The exact target version is below a required emitted feature version.");
            version=requiredVersion;
        }
        var format = options.CrossReferenceFormat;
        if (format == PdfCrossReferenceFormat.Auto)
            format = context.Policy.Limits.MaximumOutputBytes > 9999999999 || (!incremental && (long)maximum + 1 > context.Available(PdfResourceKind.Objects)) ? PdfCrossReferenceFormat.Stream : PdfCrossReferenceFormat.Table;
        if (format == PdfCrossReferenceFormat.Stream && version < 15)
        {
            if (options.Version != null)
                throw Error(context, "Writer.VersionTooLow", "Cross-reference streams require PDF 1.5 or newer.");
            version = 15;
        }

        bool InheritedMetadata(string key, PdfValue value) => source != null && source.Revisions[0].Trailer.Kind == PdfValueKind.Stream && StreamKeys.Contains(key) && source.Revisions[0].Trailer.Entries.TryGetValue(key, out var original) && ReferenceEquals(value, original);
        if (format == PdfCrossReferenceFormat.Stream && trailer.Entries.Any(pair => StreamKeys.Contains(pair.Key) && pair.Value.Kind != PdfValueKind.Null && !InheritedMetadata(pair.Key, pair.Value)))
            throw Error(context, "Writer.TrailerKeyConflict", "An existing trailer extension conflicts with cross-reference stream metadata. Request a classic table to preserve it.");
        if (!incremental && signature && !options.AllowSignatureInvalidation)
            throw Error(context, "Writer.SignatureRewrite", "Full rewriting cannot guarantee preservation of existing signature byte ranges. Use an incremental save or explicitly allow invalidation.");
        if (!incremental && signature)
            context.Report(new PdfDiagnostic("Writer.SignatureRewriteAllowed", "The caller explicitly authorized full rewriting that can invalidate existing signature byte ranges.", severity: PdfDiagnosticSeverity.Information));
        var selected = new SortedDictionary<int, (PdfObjectId Id, PdfValue Value)>();
        if (incremental)
        {
            foreach (var pair in changes)
                selected.Add(pair.Key.Number, (pair.Key, pair.Value));
            if(extensionChanged)selected[catalog.Id.Number]=catalog;
        }
        else
            foreach (var pair in objects)
                selected.Add(pair.Key, pair.Value);
        if (incremental && version > originalVersion)
        {
            var updated = CopyDictionary(catalog.Value.Entries, context);
            updated["Version"] = PdfValue.Name(PdfVersionNumber.Text(version));
            selected[catalog.Id.Number] = (catalog.Id, PdfValue.OwnedDictionary(updated));
        }

        long knownFree = incremental ? 0 : entries.Values.LongCount(entry => entry.Kind == PdfCrossReferenceKind.Free && entry.Id.Number != 0);
        context.Consume(PdfResourceKind.Objects, format == PdfCrossReferenceFormat.Table && !incremental ? (long)maximum + 1 : selected.Count + removed.Count + knownFree + 2L);
        context.Consume(PdfResourceKind.ExpandedBytes, checked((selected.Count + removed.Count + 2L) * 64));
        foreach (var item in selected.Values)
            if (item.Value.Kind == PdfValueKind.Stream)
                context.Consume(PdfResourceKind.InputBytes, item.Value.RawStreamLength);
        var sink = new Sink(output, context);
        var written = new SortedDictionary<int, Row>();
        var removedGenerations = removed.ToDictionary(id => id.Number, id => Math.Min(65535, id.Generation + 1));
        if (incremental)
        {
            context.Consume(PdfResourceKind.InputBytes, source!.Source.Length);
            using var original = source.OpenOriginalStream();
            sink.Copy(original);
            sink.Text("\n");
        }
        else
            sink.Text("%PDF-" + PdfVersionNumber.Text(version) + "\n%\u00e2\u00e3\u00cf\u00d3\n");
        foreach (var pair in selected)
        {
            context.Checkpoint();
            written.Add(pair.Key, new Row(sink.Position, pair.Value.Id.Generation, false));
            sink.Text(pair.Value.Id.Number.ToString(CultureInfo.InvariantCulture) + " " + pair.Value.Id.Generation.ToString(CultureInfo.InvariantCulture) + " obj\n");
            sink.Value(pair.Value.Value, true);
            sink.Text("\nendobj\n");
        }

        long previousFree = entries.TryGetValue(0, out var zero) ? zero.Offset : 0;
        if (incremental)
        {
            var freed = removed.OrderBy(x => x.Number).ToArray();
            written[0] = new Row(freed.Length == 0 ? previousFree : freed[0].Number, 65535, true);
            for (int i = 0; i < freed.Length; i++)
                written[freed[i].Number] = new Row(i + 1 < freed.Length ? freed[i + 1].Number : previousFree, Math.Min(65535, freed[i].Generation + 1), true);
        }

        long xref = sink.Position;
        var finalTrailer = CopyDictionary(trailer.Entries, context);
        foreach (var pair in trailer.Entries)
            if (XrefKeys.Contains(pair.Key) || InheritedMetadata(pair.Key, pair.Value) || (format == PdfCrossReferenceFormat.Stream && StreamKeys.Contains(pair.Key) && pair.Value.Kind == PdfValueKind.Null))
                finalTrailer.Remove(pair.Key);
        if (incremental)
            finalTrailer["Prev"] = PdfValue.Integer(source!.Revisions[0].StartXref);
        if (format == PdfCrossReferenceFormat.Table)
        {
            finalTrailer["Size"] = PdfValue.Integer((long)maximum + 1);
            sink.Text("xref\n");
            if (incremental)
                TableRanges(written, sink, context);
            else
            {
                sink.Text("0 " + ((long)maximum + 1).ToString(CultureInfo.InvariantCulture) + "\n");
                for (long n = 0; n <= maximum; n++)
                {
                    context.Checkpoint();
                    int number = (int)n;
                    if (written.TryGetValue(number, out var row))
                        TableRow(row, sink, context);
                    else
                    {
                        long next = n + 1;
                        while (next <= maximum && written.ContainsKey((int)next))
                        {
                            context.Checkpoint();
                            next++;
                        }

                        int generation = number == 0 ? 65535 : removedGenerations.TryGetValue(number, out int deletedGeneration) ? deletedGeneration : entries.TryGetValue(number, out var prior) && prior.Kind == PdfCrossReferenceKind.Free ? prior.Id.Generation : 0;
                        TableRow(new Row(next > maximum ? 0 : next, generation, true), sink, context);
                    }
                }
            }

            sink.Text("trailer\n");
            sink.Value(PdfValue.OwnedDictionary(finalTrailer), false);
        }
        else
        {
            if (maximum >= int.MaxValue - 1)
                throw Error(context, "Writer.ObjectNumberLimit", "No identity remains for a cross-reference stream.");
            int id = maximum + 1;
            written[id] = new Row(xref, 0, false);
            if (!incremental)
            {
                foreach (var entry in entries.Values)
                    if (entry.Kind == PdfCrossReferenceKind.Free && entry.Id.Number != 0 && !written.ContainsKey(entry.Id.Number))
                        written[entry.Id.Number] = new Row(0, entry.Id.Generation, true);
                foreach (var removedId in removed)
                    written[removedId.Number] = new Row(0, Math.Min(65535, removedId.Generation + 1), true);
                var free = written.Where(pair => pair.Value.Free).Select(pair => pair.Key).ToArray();
                written[0] = new Row(free.Length == 0 ? 0 : free[0], 65535, true);
                for (int n = 0; n < free.Length; n++)
                    written[free[n]] = new Row(n + 1 < free.Length ? free[n + 1] : 0, written[free[n]].Generation, true);
            }

            var data = new PdfByteBuilder(context);
            var ranges = Ranges(written.Keys);
            var index = new List<PdfValue>();
            foreach (var range in ranges)
            {
                index.Add(PdfValue.Integer(range.Start));
                index.Add(PdfValue.Integer(range.Count));
                for (int n = 0; n < range.Count; n++)
                {
                    var row = written[range.Start + n];
                    data.Add(row.Free ? (byte)0 : (byte)1);
                    for (int b = 7; b >= 0; b--)
                        data.Add((byte)((ulong)row.Offset >> (8 * b)));
                    data.Add((byte)(row.Generation >> 8));
                    data.Add((byte)row.Generation);
                }
            }

            finalTrailer["Type"] = PdfValue.Name("XRef");
            finalTrailer["Size"] = PdfValue.Integer((long)id + 1);
            finalTrailer["W"] = PdfValue.Array(PdfValue.Integer(1), PdfValue.Integer(8), PdfValue.Integer(2));
            finalTrailer["Index"] = PdfValue.OwnedArray(index);
            var bytes = data.Build();
            var stream = PdfValue.StreamValue(PdfValue.OwnedDictionary(finalTrailer), bytes, 0, bytes.Length);
            sink.Text(id.ToString(CultureInfo.InvariantCulture) + " 0 obj\n");
            sink.Value(stream, true);
            sink.Text("\nendobj\n");
        }

        sink.Text("\nstartxref\n" + xref.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
        sink.Flush();
        context.Checkpoint();
    }

    private static void TableRanges(SortedDictionary<int, Row> rows, Sink sink, PdfOperationContext context)
    {
        foreach (var range in Ranges(rows.Keys))
        {
            sink.Text(range.Start.ToString(CultureInfo.InvariantCulture) + " " + range.Count.ToString(CultureInfo.InvariantCulture) + "\n");
            for (int n = 0; n < range.Count; n++)
                TableRow(rows[range.Start + n], sink, context);
        }
    }

    private static List<(int Start, int Count)> Ranges(IEnumerable<int> numbers)
    {
        var result = new List<(int, int)>();
        int first = -1, last = -1;
        foreach (int number in numbers)
        {
            if (first < 0)
            {
                first = last = number;
            }
            else if (number == last + 1)
                last = number;
            else
            {
                result.Add((first, last - first + 1));
                first = last = number;
            }
        }

        if (first >= 0)
            result.Add((first, last - first + 1));
        return result;
    }

    private static void TableRow(Row row, Sink sink, PdfOperationContext context)
    {
        if (row.Offset > 9999999999)
            throw Error(context, "Writer.OffsetNotRepresentable", "The classic xref offset exceeds ten decimal digits; request a cross-reference stream.");
        sink.Text(row.Offset.ToString("D10", CultureInfo.InvariantCulture) + " " + row.Generation.ToString("D5", CultureInfo.InvariantCulture) + (row.Free ? " f \n" : " n \n"));
    }

    private static bool Encrypted(PdfValue trailer) => trailer.Entries.TryGetValue("Encrypt", out var value) && value.Kind != PdfValueKind.Null;
    private static (PdfObjectId Id, PdfValue Value) Catalog(SortedDictionary<int, (PdfObjectId Id, PdfValue Value)> objects, PdfValue reference, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 64);
        var seen = new HashSet<PdfObjectId>();
        (PdfObjectId Id, PdfValue Value) item = default;
        for (int depth = 0; reference.Kind == PdfValueKind.Reference; depth++)
        {
            if (depth >= context.Available(PdfResourceKind.Depth))
                context.RejectLimit(PdfResourceKind.Depth);
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 64);
            if (!seen.Add(reference.ReferenceId))
                throw Error(context, "Writer.ReferenceCycle", "The catalog reference chain contains a cycle.");
            if (!objects.TryGetValue(reference.ReferenceId.Number, out item) || item.Id != reference.ReferenceId)
                throw Error(context, "Writer.InvalidRoot", "The catalog reference is missing or has a different generation.");
            reference = item.Value;
        }

        if (item.Value == null || item.Value.Kind != PdfValueKind.Dictionary || !item.Value.Entries.TryGetValue("Type", out var type))
            throw Error(context, "Writer.InvalidRoot", "The trailer must reference an active Catalog dictionary.");
        type = Dereference(objects, type, context);
        if (type.Kind != PdfValueKind.Name || type.NameValue != "Catalog")
            throw Error(context, "Writer.InvalidRoot", "The trailer must reference an active Catalog dictionary.");
        return item;
    }

    private static PdfValue Dereference(SortedDictionary<int, (PdfObjectId Id, PdfValue Value)> objects, PdfValue value, PdfOperationContext context)
    {
        if (value.Kind != PdfValueKind.Reference)
            return value;
        context.Consume(PdfResourceKind.ExpandedBytes, 64);
        var seen = new HashSet<PdfObjectId>();
        for (int depth = 0; value.Kind == PdfValueKind.Reference; depth++)
        {
            if (depth >= context.Available(PdfResourceKind.Depth))
                context.RejectLimit(PdfResourceKind.Depth);
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 64);
            if (!seen.Add(value.ReferenceId))
                throw Error(context, "Writer.ReferenceCycle", "A structural reference chain contains a cycle.");
            if (!objects.TryGetValue(value.ReferenceId.Number, out var item) || item.Id != value.ReferenceId)
                return PdfValue.Null;
            value = item.Value;
        }

        return value;
    }

    private static void Inspect(PdfValue value, PdfOperationContext context, bool topLevel, ref bool signature,ref int requiredVersion,Func<PdfValue,PdfValue> resolve)
    {
        context.Consume(PdfResourceKind.Objects, 1);
        using var scope = context.EnterScope(PdfResourceKind.Depth);
        if (value.Kind == PdfValueKind.Stream && !topLevel)
            throw Error(context, "Writer.DirectStream", "A PDF stream must be an indirect object.");
        if (value.Kind == PdfValueKind.Array)
        {
            requiredVersion=Math.Max(requiredVersion,PdfFeatureVersion.Array(value,resolve));
            foreach (var item in value.Items)
                Inspect(item, context, false, ref signature,ref requiredVersion,resolve);
        }
        if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
        {
            requiredVersion=Math.Max(requiredVersion,PdfFeatureVersion.Dictionary(value,resolve));
            if (value.Entries.ContainsKey("ByteRange") && value.Entries.ContainsKey("Contents"))
                signature = true;
            foreach (var item in value.Entries.Values)
                Inspect(item, context, false, ref signature,ref requiredVersion,resolve);
        }
    }

    internal static PdfException Error(PdfOperationContext context, string code, string message)
    {
        context.Report(new PdfDiagnostic(code, message));
        throw new InvalidOperationException();
    }

    private readonly struct Row
    {
        internal Row(long offset, int generation, bool free)
        {
            Offset = offset;
            Generation = generation;
            Free = free;
        }

        internal long Offset { get; }
        internal int Generation { get; }
        internal bool Free { get; }
    }

    internal sealed class Sink
    {
        private readonly Stream _output;
        private readonly PdfOperationContext _context;
        private readonly byte[] _buffer;
        private int _pending;
        internal Sink(Stream output, PdfOperationContext context)
        {
            _output = output;
            _context = context;
            context.Consume(PdfResourceKind.ExpandedBytes, 4096);
            _buffer = new byte[4096];
        }

        internal long Position { get; private set; }

        internal void Text(string text)
        {
            foreach (char c in text)
            {
                if (c > 255)
                    throw new InvalidOperationException("A PDF byte token was required.");
                Byte((byte)c);
            }
        }

        private void Byte(byte value)
        {
            _context.CheckOutputRange(0, Position, 1);
            if (_pending == _buffer.Length)
                Flush();
            _buffer[_pending++] = value;
            Position = checked(Position + 1);
        }

        internal void Flush()
        {
            _context.Checkpoint();
            if (_pending > 0)
            {
                _output.Write(_buffer, 0, _pending);
                _pending = 0;
            }

            _context.Checkpoint();
        }

        internal void Copy(Stream input)
        {
            Flush();
            int n;
            while ((n = input.Read(_buffer, 0, _buffer.Length)) > 0)
            {
                _context.Checkpoint();
                _output.Write(_buffer, 0, n);
                Position = checked(Position + n);
            }

            _context.Checkpoint();
        }

        internal void Value(PdfValue value, bool topLevel)
        {
            using var scope = _context.EnterScope(PdfResourceKind.Depth);
            _context.Checkpoint();
            switch (value.Kind)
            {
                case PdfValueKind.Null:
                    Text("null");
                    break;
                case PdfValueKind.Boolean:
                    Text(value.BooleanValue ? "true" : "false");
                    break;
                case PdfValueKind.Number:
                    Text(value.NumberText);
                    break;
                case PdfValueKind.Name:
                    Name(value.NameValue);
                    break;
                case PdfValueKind.String:
                    Byte(60);
                    foreach (byte b in value.StringBytes)
                    {
                        Hex(b);
                    }

                    Byte(62);
                    break;
                case PdfValueKind.Reference:
                    Text(value.ReferenceId.ToString());
                    break;
                case PdfValueKind.Array:
                    Byte(91);
                    foreach (var item in value.Items)
                    {
                        Value(item, false);
                        Byte(32);
                    }

                    Byte(93);
                    break;
                case PdfValueKind.Dictionary:
                    Dictionary(value.Entries, null);
                    break;
                case PdfValueKind.Stream:
                    if (!topLevel)
                        throw Error(_context, "Writer.DirectStream", "A PDF stream must be an indirect object.");
                    Dictionary(value.Entries, value.RawStreamLength);
                    Text("\nstream\n");
                    using (var stream = value.OpenRawStream())
                        Copy(stream);
                    Text("\nendstream");
                    break;
                default:
                    throw new InvalidOperationException();
            }
        }

        private void Dictionary(IReadOnlyDictionary<string, PdfValue> entries, long? length)
        {
            _context.Consume(PdfResourceKind.ExpandedBytes, checked(entries.Count * 16L + 32));
            var keys = entries.Keys.ToArray();
            System.Array.Sort(keys, StringComparer.Ordinal);
            Text("<<");
            foreach (string key in keys)
            {
                if (length != null && key == "Length")
                    continue;
                Byte(32);
                Name(key);
                Byte(32);
                Value(entries[key], false);
            }

            if (length != null)
                Text(" /Length " + length.Value.ToString(CultureInfo.InvariantCulture));
            Text(" >>");
        }

        private void Name(string text)
        {
            Byte(47);
            foreach (char c in text)
            {
                if (c >= 33 && c <= 126 && "()<>[]{}/%#".IndexOf(c) < 0)
                    Byte((byte)c);
                else
                {
                    Byte(35);
                    Hex((byte)c);
                }
            }
        }

        private void Hex(byte value)
        {
            const string digits = "0123456789ABCDEF";
            Byte((byte)digits[value >> 4]);
            Byte((byte)digits[value & 15]);
        }
    }
}
