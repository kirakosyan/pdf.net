using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf.Formats;

/// <summary>An immutable named byte sequence. Opening an entry never executes its content.</summary>
public sealed class PdfCollectionItem
{
    internal PdfCollectionItem(string name, PdfByteStore bytes) { Name = name; Bytes = bytes; }
    internal PdfByteStore Bytes { get; }
    /// <summary>Gets its canonical portable leaf name.</summary>
    public string Name { get; }
    /// <summary>Gets the byte length.</summary>
    public long Length => Bytes.Length;
    /// <summary>Opens an independent read-only stream owned by the caller.</summary>
    public Stream Open() => Bytes.Open(0, Bytes.Length);
}

/// <summary>A bounded ordered collection with no filesystem extraction or implicit format conversion.</summary>
public sealed class PdfDocumentCollection
{
    private PdfDocumentCollection(List<PdfCollectionItem> items, PdfOperationReport report) { Items = items.AsReadOnly(); OperationReport = report; }
    /// <summary>Gets entries in capture or central-directory order.</summary>
    public IReadOnlyList<PdfCollectionItem> Items { get; }
    /// <summary>Gets the capture operation report.</summary>
    public PdfOperationReport OperationReport { get; }
    /// <summary>Copies 1–4096 borrowed inputs, at most 16 MiB each and 64 MiB in total. Names must be distinct portable NFC leaf names.</summary>
    public static PdfDocumentCollection Capture(IEnumerable<KeyValuePair<string, Stream>> inputs, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (inputs == null) throw new ArgumentNullException(nameof(inputs));
        var items = new List<PdfCollectionItem>();
        var report = Run("CaptureCollection", policy, cancellationToken, c =>
        {
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var input in inputs)
            {
                c.Checkpoint();
                c.Consume(PdfResourceKind.Objects, 1);
                c.Consume(PdfResourceKind.ExpandedBytes, 2048);
                SafeName(input.Key, c);
                if (items.Count == 4096 || !names.Add(input.Key)) throw Error(c, "Identity", "Collection names must be unique within a finite inventory.");
                var bytes = FormatIo.Capture(input.Value, c, (int)Math.Min(16777216, 67108864 - total));
                total += bytes.Length;
                items.Add(new(input.Key, bytes));
            }
            if (items.Count == 0) throw Error(c, "Empty", "A collection requires at least one entry.");
        });
        return new(items, report);
    }

    /// <summary>Reads a bounded ZIP into memory, rejecting unsafe paths, collisions, links and corrupt data before returning any entries.</summary>
    public static PdfDocumentCollection ReadArchive(Stream input, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        var items = new List<PdfCollectionItem>();
        var report = Run("ReadCollectionArchive", policy, cancellationToken, c =>
        {
            using var package = new FormatPackage(input, c, collectionNames: true);
            foreach (string name in package.Names) { c.Checkpoint(); items.Add(new(name, package.Read(name))); }
        });
        return new(items, report);
    }

    /// <summary>Writes a deterministic UTF-8 ZIP with stored entries. Caller streams stay open; I/O failure may leave partial stream output.</summary>
    public PdfOperationReport WriteArchive(Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
        => Run("WriteCollectionArchive", policy, cancellationToken, c => FormatIo.Copy(Archive(c), output, c));

    /// <summary>Stages and atomically commits an archive file after every check succeeds.</summary>
    public PdfOperationReport WriteArchiveFile(string path, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
        => Run("WriteCollectionArchiveFile", policy, cancellationToken, c => { var bytes = Archive(c); PdfAtomicFile.Write(path, c, s => FormatIo.Copy(bytes, s, c)); });

    private PdfByteStore Archive(PdfOperationContext c)
        => FormatPackage.Write(Items.Select(item => (item.Name, FormatIo.Raw(item.Bytes, c), true)), c, collectionNames: true);

    /// <summary>Runs one explicitly registered trusted adapter sequentially. Unlatched adapter exceptions are reported in place. All context-latched SDK errors, strict losses, cancellation, limits and leaked scopes abort the shared-budget batch.</summary>
    public PdfCollectionBatchResult Process(string format, PdfCollectionAdapters adapters, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (adapters == null) throw new ArgumentNullException(nameof(adapters));
        var adapter = adapters.Find(format);
        var outcomes = new List<PdfCollectionOutcome>();
        var report = Run("ProcessCollection", policy, cancellationToken, c =>
        {
            long total = 0;
            foreach (var item in Items)
            {
                c.Checkpoint();
                c.Consume(PdfResourceKind.Objects, 1);
                c.Consume(PdfResourceKind.ExpandedBytes, 512);
                using var buffer = new PdfCodecBufferStream(c, Math.Min(16777216, 67108864 - total));
                try
                {
                    using (var source = item.Open())
                    using (var input = new BorrowedStream(source, true, c))
                    using (var target = new BorrowedStream(buffer, false, c)) adapter(item.Name, input, target, c);
                    c.EnsureComplete();
                    var bytes = buffer.Finish(); total += bytes.Length;
                    outcomes.Add(new(item.Name, new(item.Name, bytes), null));
                }
                catch (Exception ex) when (ex is PdfException || ex is IOException || ex is ArgumentException || ex is InvalidOperationException)
                {
                    c.Checkpoint();
                    c.EnsureComplete();
                    outcomes.Add(new(item.Name, null, "Collection.AdapterFailed"));
                    c.Report(new PdfDiagnostic("Collection.AdapterFailed", "An item adapter failed; its partial output was discarded.", severity: PdfDiagnosticSeverity.Warning));
                }
            }
        });
        return new(outcomes, report);
    }

    internal static PdfOperationReport Run(string operation, PdfOperationPolicy? policy, CancellationToken token, Action<PdfOperationContext> action)
        => PdfFont.Run(operation, "managed.collections", policy, token, action, PdfCapability.ReadDocument);
    internal static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Collection." + code, message);
    internal static void SafeName(string name, PdfOperationContext c)
    {
        if (name == null || name.Length == 0 || name.Length > 240 || name == "." || name == ".." || name.EndsWith(".", StringComparison.Ordinal) || name.EndsWith(" ", StringComparison.Ordinal))
            throw Error(c, "Name", "A bounded portable leaf name is required.");
        foreach (char ch in name)
            if (char.IsControl(ch) || char.GetUnicodeCategory(ch) == UnicodeCategory.Format || "<>:\"/\\|?*%".IndexOf(ch) >= 0)
                throw Error(c, "Name", "Paths, control characters and platform-ambiguous names reject.");
        try
        {
            if (!name.IsNormalized(NormalizationForm.FormC)) throw Error(c, "Name", "Archive names must already use NFC normalization.");
            _ = new UTF8Encoding(false, true).GetByteCount(name);
        }
        catch (ArgumentException) { throw Error(c, "Name", "Archive names must contain valid Unicode."); }
        string stem = name.Split('.')[0].TrimEnd(' ').ToUpperInvariant();
        if (stem == "CON" || stem == "PRN" || stem == "AUX" || stem == "NUL" || stem == "CONIN$" || stem == "CONOUT$" ||
            stem.Length == 4 && (stem.StartsWith("COM", StringComparison.Ordinal) || stem.StartsWith("LPT", StringComparison.Ordinal)) && "123456789¹²³".IndexOf(stem[3]) >= 0)
            throw Error(c, "Name", "Reserved device names reject on every platform.");
    }
}

/// <summary>A trusted synchronous adapter. It must use the supplied context, keep streams borrowed and leave process state unchanged. This delegate is not a sandbox.</summary>
public delegate void PdfCollectionAdapter(string name, Stream input, Stream output, PdfOperationContext context);

/// <summary>An immutable instance-owned registry. No process-wide registration or implicit plugin discovery occurs.</summary>
public sealed class PdfCollectionAdapters
{
    private readonly IReadOnlyDictionary<string, PdfCollectionAdapter> _adapters;
    /// <summary>Copies at most 128 distinct ordinal format IDs and explicit trusted adapters.</summary>
    public PdfCollectionAdapters(IEnumerable<KeyValuePair<string, PdfCollectionAdapter>> adapters)
    {
        if (adapters == null) throw new ArgumentNullException(nameof(adapters));
        var values = new Dictionary<string, PdfCollectionAdapter>(StringComparer.Ordinal);
        foreach (var pair in adapters)
        {
            Guard.Text(pair.Key, nameof(adapters), 64);
            if (values.Count == 128 || values.ContainsKey(pair.Key) || pair.Value == null) throw new ArgumentException("A bounded unique explicit adapter registry is required.", nameof(adapters));
            values.Add(pair.Key, pair.Value);
        }
        _adapters = values;
    }
    internal PdfCollectionAdapter Find(string format)
    {
        if (format == null) throw new ArgumentNullException(nameof(format));
        if (!_adapters.TryGetValue(format, out var adapter)) throw new PdfException(new PdfDiagnostic("Collection.AdapterUnavailable", "The requested format has no explicit adapter."));
        return adapter;
    }
}

/// <summary>A detached item outcome in original input order.</summary>
public sealed class PdfCollectionOutcome
{
    internal PdfCollectionOutcome(string name, PdfCollectionItem? output, string? code) { Name = name; Output = output; DiagnosticCode = code; }
    /// <summary>Gets the original name.</summary>
    public string Name { get; }
    /// <summary>Gets successful output, or null when this item failed.</summary>
    public PdfCollectionItem? Output { get; }
    /// <summary>Gets a stable failure code, or null on success.</summary>
    public string? DiagnosticCode { get; }
}

/// <summary>An immutable batch result; a completed batch may contain individual failures.</summary>
public sealed class PdfCollectionBatchResult
{
    internal PdfCollectionBatchResult(List<PdfCollectionOutcome> outcomes, PdfOperationReport report) { Outcomes = outcomes.AsReadOnly(); OperationReport = report; }
    /// <summary>Gets all attempted outcomes in input order.</summary>
    public IReadOnlyList<PdfCollectionOutcome> Outcomes { get; }
    /// <summary>Gets the shared operation report.</summary>
    public PdfOperationReport OperationReport { get; }
}
