using System.IO;
using System.Security.Cryptography;

namespace Fluent.Pdf;
internal sealed partial class PdfOptimizationEngine
{
    private readonly PdfGraphEdit _g;
    private readonly PdfOperationContext _c;
    private readonly PdfOptimizationOptions _options;
    private readonly PdfProviderRegistry _providers;
    internal PdfByteStore Output = null!;
    internal long SourceBytes;
    internal string SourceHash = "", OutputHash = "";
    internal int Removed, Shared, Compressed, Downsampled, RemovedBindings;
    internal PdfOptimizationEngine(PdfGraphEdit graph, PdfOptimizationOptions options, PdfProviderRegistry providers)
    {
        _g = graph;
        _c = graph.Context;
        _options = options;
        _providers = providers;
    }

    internal void Run()
    {
        Info("FullRewrite", "Optimization produces a full rewritten revision; prior revision bytes are not retained and no conformance/signature verdict is inferred.");
        SourceBytes = _g.Graph.Source.Length;
        SourceHash = PdfArchival.Digest(_g.Graph.Source, _c);
        if (_options.CleanupObjects)
            Cleanup();
        if (_options.RemoveUnusedResources)
            RemovedBindings = PdfOptimizationResources.Prune(_g, _providers);
        if (_options.DeduplicateResources)
            Deduplicate();
        if (_options.Preset == PdfOptimizationPreset.CompactImages)
            Downsample();
        if (_options.CompressStreams)
            Compress();
        if (_options.DeduplicateResources && Downsampled > 0)
            Deduplicate();
        if (_options.CleanupObjects)
            Cleanup();
        using var staged = new PdfCodecBufferStream(_c, _c.Available(PdfResourceKind.ExpandedBytes));
        using (var destination = new BorrowedStream(staged, false, _c))
            _g.Edit.WriteCore(destination, new(allowSignatureInvalidation: _options.AllowSignatureInvalidation), _c);
        Output = staged.Finish();
        using var checkedDocument = PdfPageOrganization.Read(Output, _g.Snapshot.Options.MaximumPages, _c);
        if (checkedDocument.Pages.Count != _g.Snapshot.Pages.Count)
            throw Error(_c, "PageRetention", "Optimization must retain the exact page count.");
        for (int i = 0; i < checkedDocument.Pages.Count; i++)
        {
            _c.Checkpoint();
            if (!checkedDocument.Pages[i].Geometry.Equals(_g.Snapshot.Pages[i].Geometry))
                throw Error(_c, "PageRetention", "Optimization must retain exact page geometry and order.");
        }

        OutputHash = PdfArchival.Digest(Output, _c);
        if (Output.Length >= SourceBytes)
            Info("OutputNotSmaller", "The measured output is not smaller than its normalized source; savings are never assumed.");
    }

    private List<PdfObjectId> Ids()
    {
        var result = new List<PdfObjectId>();
        foreach (var id in _g.Edit.ActiveIds(_c))
        {
            _c.Consume(PdfResourceKind.ExpandedBytes, 32);
            result.Add(id);
        }

        _c.Checkpoint();
        result.Sort((a, b) => a.Number != b.Number ? a.Number.CompareTo(b.Number) : a.Generation.CompareTo(b.Generation));
        _c.Checkpoint();
        return result;
    }

    private void Cleanup()
    {
        int before = Ids().Count;
        _g.PruneUnreachable();
        int removed = before - Ids().Count;
        Removed += removed;
        if (removed > 0)
            Info("UnreachableObjectsRemoved", "Unreachable active objects were removed from the complete rewrite.");
    }

    private static readonly HashSet<string> ImageKeys = new(new[] { "Type", "Subtype", "Width", "Height", "ColorSpace", "BitsPerComponent", "Filter", "DecodeParms", "Decode", "ImageMask", "Mask", "SMask", "Interpolate", "Intent", "Length", "Matte" }, StringComparer.Ordinal);
    private void Deduplicate()
    {
        var ids = Ids();
        var fonts = new HashSet<PdfObjectId>();
        foreach (var id in ids)
            Walk(_g.Edit.Resolve(id), value =>
            {
                if (Name(value, "Type") != "FontDescriptor")
                    return;
                foreach (string name in new[]
                {
                    "FontFile",
                    "FontFile2",
                    "FontFile3"
                }

                )
                    if (value.Entries.TryGetValue(name, out var reference) && reference.Kind == PdfValueKind.Reference)
                    {
                        _c.Consume(PdfResourceKind.ExpandedBytes, 48);
                        fonts.Add(reference.ReferenceId);
                    }
            });
        _c.Consume(PdfResourceKind.ExpandedBytes, ids.Count * 24L + 128);
        var candidates = ids.Where(id => Eligible(id, _g.Edit.Resolve(id))).OrderBy(id => _g.Edit.Resolve(id).Entries.ContainsKey("SMask") || _g.Edit.Resolve(id).Entries.ContainsKey("Mask") ? 1 : 0).ThenBy(id => id.Number).ToArray();
        var buckets = new Dictionary<string, List<PdfObjectId>>(StringComparer.Ordinal);
        var aliases = new Dictionary<PdfObjectId, PdfObjectId>();
        foreach (var id in candidates)
        {
            _c.Checkpoint();
            var value = _g.Edit.Resolve(id);
            _c.Consume(PdfResourceKind.ExpandedBytes, 8192);
            using var hash = SHA256.Create();
            using var raw = value.OpenRawStream();
            using var input = new BorrowedStream(raw, true, _c, null);
            string key = (fonts.Contains(id) ? "font:" : "image:") + BitConverter.ToString(hash.ComputeHash(input));
            _c.Consume(PdfResourceKind.ExpandedBytes, 256);
            if (!buckets.TryGetValue(key, out var possible))
                buckets.Add(key, possible = new());
            PdfObjectId? same = null;
            foreach (var candidate in possible)
            {
                _c.Checkpoint();
                if (Equal(value, _g.Edit.Resolve(candidate), true))
                {
                    same = candidate;
                    break;
                }
            }

            if (same.HasValue)
                aliases.Add(id, same.Value);
            else
                possible.Add(id);
        }

        if (aliases.Count == 0)
            return;
        foreach (var id in ids)
            if (!aliases.ContainsKey(id))
                _g.Edit.Set(id, Rewrite(_g.Edit.Resolve(id)));
        _g.Edit.Trailer = Rewrite(_g.Edit.Trailer);
        foreach (var alias in aliases)
            _g.Edit.Remove(alias.Key);
        Shared += aliases.Count;
        Info("ResourcesShared", "Exactly equal safe image/font-program streams were shared after byte-for-byte and dictionary verification.");
        bool Eligible(PdfObjectId id, PdfValue value) => value.Kind == PdfValueKind.Stream && (fonts.Contains(id) || Name(value, "Subtype") == "Image" && value.Entries.Keys.All(ImageKeys.Contains));
        PdfObjectId Canonical(PdfObjectId id) => aliases.TryGetValue(id, out var target) ? target : id;
        bool Equal(PdfValue a, PdfValue b, bool root = false)
        {
            using var depth = _c.EnterScope(PdfResourceKind.Depth);
            _c.Consume(PdfResourceKind.Objects, 1);
            if (a.Kind != b.Kind)
                return false;
            switch (a.Kind)
            {
                case PdfValueKind.Null:
                    return true;
                case PdfValueKind.Boolean:
                    return a.BooleanValue == b.BooleanValue;
                case PdfValueKind.Number:
                    return a.NumberText == b.NumberText;
                case PdfValueKind.Name:
                    return a.NameValue == b.NameValue;
                case PdfValueKind.Reference:
                    return Canonical(a.ReferenceId) == Canonical(b.ReferenceId);
                case PdfValueKind.String:
                    if (a.StringBytes.Count != b.StringBytes.Count)
                        return false;
                    for (int i = 0; i < a.StringBytes.Count; i++)
                    {
                        if ((i & 4095) == 0)
                            _c.Checkpoint();
                        if (a.StringBytes[i] != b.StringBytes[i])
                            return false;
                    }

                    return true;
                case PdfValueKind.Array:
                    if (a.Items.Count != b.Items.Count)
                        return false;
                    for (int i = 0; i < a.Items.Count; i++)
                        if (!Equal(a.Items[i], b.Items[i]))
                            return false;
                    return true;
                default:
                    int left = a.Entries.Count - (root && a.Entries.ContainsKey("Length") ? 1 : 0), right = b.Entries.Count - (root && b.Entries.ContainsKey("Length") ? 1 : 0);
                    if (left != right)
                        return false;
                    foreach (var entry in a.Entries)
                        if (!(root && entry.Key == "Length") && (!b.Entries.TryGetValue(entry.Key, out var child) || !Equal(entry.Value, child)))
                            return false;
                    if (a.Kind != PdfValueKind.Stream)
                        return true;
                    if (a.RawStreamLength != b.RawStreamLength)
                        return false;
                    using (var x = a.OpenRawStream())
                    using (var y = b.OpenRawStream())
                    {
                        _c.Consume(PdfResourceKind.ExpandedBytes, 16384);
                        var one = new byte[8192];
                        var two = new byte[8192];
                        while (true)
                        {
                            _c.Checkpoint();
                            int n = x.Read(one, 0, one.Length), count = 0;
                            while (count < n)
                            {
                                int m = y.Read(two, count, n - count);
                                if (m == 0)
                                    return false;
                                count += m;
                            }

                            if (n == 0)
                                return y.ReadByte() < 0;
                            for (int i = 0; i < n; i++)
                                if (one[i] != two[i])
                                    return false;
                        }
                    }
            }
        }

        PdfValue Rewrite(PdfValue value)
        {
            using var depth = _c.EnterScope(PdfResourceKind.Depth);
            _c.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Reference)
            {
                var target = Canonical(value.ReferenceId);
                if (target == value.ReferenceId)
                    return value;
                _c.Consume(PdfResourceKind.ExpandedBytes, 64);
                return PdfValue.Reference(target);
            }

            if (value.Kind == PdfValueKind.Array)
            {
                _c.Consume(PdfResourceKind.ExpandedBytes, value.Items.Count * 16L + 64);
                return PdfValue.OwnedArray(value.Items.Select(Rewrite).ToList());
            }

            if (value.Kind != PdfValueKind.Dictionary && value.Kind != PdfValueKind.Stream)
                return value;
            _c.Consume(PdfResourceKind.ExpandedBytes, value.Entries.Count * 128L + 64);
            var entries = new Dictionary<string, PdfValue>(StringComparer.Ordinal);
            foreach (var entry in value.Entries)
                entries.Add(entry.Key, Rewrite(entry.Value));
            var dict = PdfValue.OwnedDictionary(entries);
            return value.Kind == PdfValueKind.Stream ? value.WithStreamDictionary(dict) : dict;
        }
    }

    private void Compress()
    {
        foreach (var id in Ids())
        {
            _c.Checkpoint();
            var value = _g.Edit.Resolve(id);
            if (value.Kind != PdfValueKind.Stream)
                continue;
            // Archival XMP metadata must retain its plain, unfiltered packet.
            if (Name(value, "Type") == "Metadata")
                continue;
            var filter = _g.Get(value, "Filter");
            if (filter.Kind != PdfValueKind.Null && !(filter.Kind == PdfValueKind.Array && filter.Items.Count == 0))
                continue;
            if (_g.Get(value, "F").Kind != PdfValueKind.Null)
            {
                Info("ExternalStreamRetained", "External stream references are retained without accessing their targets.");
                continue;
            }

            using var input = value.OpenRawStream();
            var encoded = PdfFilterEncoders.Flate(input, _c);
            if (encoded.Length + 24 >= value.RawStreamLength)
                continue;
            var entries = _g.Copy(value);
            entries["Filter"] = PdfValue.Name("FlateDecode");
            entries.Remove("DecodeParms");
            entries.Remove("Length");
            _g.Edit.Set(id, PdfValue.StreamValue(PdfValue.OwnedDictionary(entries), encoded, 0, encoded.Length));
            Compressed++;
        }

        if (Compressed > 0)
            Info("StreamsCompressed", "Unfiltered internal streams were losslessly compressed only where encoded payloads shrink.");
    }

    private void Walk(PdfValue value, Action<PdfValue> dictionary)
    {
        using var depth = _c.EnterScope(PdfResourceKind.Depth);
        _c.Consume(PdfResourceKind.Objects, 1);
        if (value.Kind == PdfValueKind.Array)
            foreach (var child in value.Items)
                Walk(child, dictionary);
        else if (value.Kind == PdfValueKind.Dictionary || value.Kind == PdfValueKind.Stream)
        {
            dictionary(value);
            foreach (var child in value.Entries.Values)
                Walk(child, dictionary);
        }
    }

    private string Name(PdfValue value, string key)
    {
        var v = _g.Get(value, key);
        return v.Kind == PdfValueKind.Name ? v.NameValue : "";
    }

    private void Info(string code, string message) => _c.Report(new("Optimization." + code, message, severity: PdfDiagnosticSeverity.Information));
    internal static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Optimization." + code, message);
}
