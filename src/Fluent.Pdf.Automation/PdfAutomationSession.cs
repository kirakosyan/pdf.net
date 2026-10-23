using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace Fluent.Pdf.Automation;
// Portable implementation tests exercise this exact session; COM activation evidence is separate and Windows-only.
internal sealed class PdfAutomationSession : IDisposable
{
    private readonly object _gate = new();
    private CancellationTokenSource? _active;
    private PdfDocument? _document;
    private bool _disposed;
    private string _diagnostic = "";
    private PdfOperationPolicy _policy = Policy(16777216, 134217728, 16777216, 250000, 30000);
    internal string LastDiagnosticCode
    {
        get
        {
            lock (_gate)
                return _diagnostic;
        }
    }

    internal string RuntimeIdentity => System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription + ";" + System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture + ";" + typeof(PdfAutomation).Assembly.GetName().Version;

    private static PdfOperationPolicy Policy(int input, int expanded, int output, int objects, int milliseconds)
    {
        if (input < 1 || input > 16777216 || expanded < 1 || expanded > 268435456 || output < 1 || output > 16777216 || objects < 1 || objects > 1000000 || milliseconds < 1 || milliseconds > 300000)
            throw new ArgumentOutOfRangeException(nameof(input));
        return new(limits: new(maximumInputBytes: input, maximumExpandedBytes: expanded, maximumOutputBytes: output, maximumObjects: objects, maximumElapsedTime: TimeSpan.FromMilliseconds(milliseconds)));
    }

    internal void Configure(int input, int expanded, int output, int objects, int milliseconds) => Run(token =>
    {
        if (_document != null)
            throw Failure("Automation.ConfigurationLocked", 0x8004A103);
        _policy = Policy(input, expanded, output, objects, milliseconds);
        return 0;
    });
    internal int PageCount => Run(token => Document().Pages.Count);

    private PdfDocument Document() => _document ?? throw Failure("Automation.NoDocument", 0x8004A104);
    internal void OpenBytes(object value) => Run(token =>
    {
        if (value is not byte[] bytes || bytes.Length == 0)
            throw new ArgumentException("A document array is required.");
        if (bytes.LongLength > _policy.Limits.MaximumInputBytes)
            throw Failure("Limit.InputBytes", 0x8004A106);
        using var input = new MemoryStream(bytes, false);
        var candidate = Pdf.Open(input, new(operationPolicy: _policy), cancellationToken: token);
        Swap(candidate, token);
        return 0;
    });
    internal void OpenFile(string path) => Run(token =>
    {
        Path(path);
        using var input = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        var candidate = Pdf.Open(input, new(operationPolicy: _policy), cancellationToken: token);
        Swap(candidate, token);
        return 0;
    });
    private void Swap(PdfDocument candidate, CancellationToken token)
    {
        try
        {
            token.ThrowIfCancellationRequested();
        }
        catch
        {
            candidate.Dispose();
            throw;
        }

        var old = _document;
        _document = candidate;
        old?.Dispose();
    }

    internal string ExtractText(int page, int view, bool invisible, int maximum) => Run(token =>
    {
        var snapshot = Document().CreateSnapshot();
        if (page < 0 || page >= snapshot.Pages.Count || view < 0 || view > 2 || maximum < 1 || maximum > 1000000)
            throw new ArgumentOutOfRangeException(nameof(page));
        string text = "";
        PdfFont.Run("AutomationExtractText", "windows.automation", _policy, token, c =>
        {
            using var graph = new PdfGraphEdit(snapshot, new(allowSignatureInvalidation: true), c);
            var data = new PdfTextExtractionEngine(graph, new(includeInvisibleText: invisible), PdfProviderRegistry.Empty).Read(page);
            text = view == 0 ? data.Raw : view == 1 ? data.Text : data.Layout;
            if (text.Length > maximum)
                throw PdfWriterCore.Error(c, "Automation.CharacterLimit", "The explicit text result exceeds its finite UTF16 ceiling.");
            c.Consume(PdfResourceKind.ExpandedBytes, text.Length * 2L + 128);
            c.Consume(PdfResourceKind.OutputBytes, text.Length * 2L);
        }, PdfCapability.ReadDocument);
        return text;
    });
    internal void SelectPages(string expression, bool signatures) => Run(token =>
    {
        if (expression == null || expression.Length == 0 || expression.Length > 4096)
            throw new ArgumentException("A bounded selection is required.");
        var snapshot = Document().CreateSnapshot();
        PdfDocument? candidate = null;
        try
        {
            PdfFont.Run("AutomationSelectPages", "windows.automation", _policy, token, c =>
            {
                var selection = PdfPageSelection.ParseCore(expression, snapshot.Pages.Count, 10000, c);
                using var import = new PdfPageImportEngine(new(allowSignatureInvalidation: signatures), c);
                import.Add(new(snapshot, selection));
                candidate = import.Finish();
            }, PdfCapability.WriteDocument);
            Swap(candidate!, token);
            candidate = null;
            return 0;
        }
        finally
        {
            candidate?.Dispose();
        }
    });
    internal byte[] SaveBytes(bool signatures) => Run(token =>
    {
        var snapshot = Document().CreateSnapshot();
        byte[]? result = null;
        PdfFont.Run("AutomationSaveBytes", "windows.automation", _policy, token, c =>
        {
            var source = new PdfDocumentSnapshot(snapshot.Options, snapshot.Pages.ToArray(), snapshot.SourceDocument, snapshot.OriginalOrder, new(allowSignatureInvalidation: signatures), snapshot.PageNumbering);
            using var buffer = new PdfCodecBufferStream(c, _policy.Limits.MaximumOutputBytes);
            using (var output = new BorrowedStream(buffer, false, c))
                ManagedPdfProvider.Instance.Write(source, output, c);
            var bytes = buffer.Finish();
            if (bytes.Length > 16777216)
                throw PdfWriterCore.Error(c, "Automation.OutputLimit", "The COM byte array exceeds its finite ceiling.");
            c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 2L + 128);
            result = new byte[checked((int)bytes.Length)];
            for (int i = 0; i < result.Length; i++)
            {
                if ((i & 4095) == 0)
                    c.Checkpoint();
                result[i] = checked((byte)bytes.At(i));
            }
        }, PdfCapability.WriteDocument);
        return result!;
    });
    internal void SaveFile(string path, bool signatures) => Run(token =>
    {
        Path(path);
        Document().SaveFile(path, token, new(allowSignatureInvalidation: signatures));
        return 0;
    });
    internal object[] GetFieldNames() => Run(token =>
    {
        var snapshot = Document().CreateSnapshot();
        object[]? names = null;
        PdfFont.Run("AutomationFieldNames", "windows.automation", _policy, token, c =>
        {
            using var graph = new PdfGraphEdit(snapshot, new(), c);
            var reader = new PdfFormReader(graph, passive: true);
            if (reader.Fields.Count > 4096)
                throw PdfWriterCore.Error(c, "Automation.FieldLimit", "The field-name array exceeds its finite ceiling.");
            long characters = reader.Fields.Sum(field => (long)field.Name.Length);
            c.Consume(PdfResourceKind.OutputBytes, characters * 2 + reader.Fields.Count * 16L);
            c.Consume(PdfResourceKind.ExpandedBytes, characters * 2 + reader.Fields.Count * 32L + 256);
            names = reader.Fields.Select(field => (object)field.Name).ToArray();
        }, PdfCapability.ReadDocument);
        return names!;
    });
    internal void Close() => Run(token =>
    {
        _document?.Dispose();
        _document = null;
        return 0;
    });
    internal void Cancel()
    {
        CancellationTokenSource? source;
        lock (_gate)
            source = _active;
        try
        {
            source?.Cancel();
        }
        catch (ObjectDisposedException)
        {
        }
    }

    internal T Run<T>(Func<CancellationToken, T> action)
    {
        CancellationTokenSource source;
        lock (_gate)
        {
            if (_disposed)
                throw Failure("Automation.Disposed", 0x8004A105);
            if (_active != null)
                throw Failure("Automation.Busy", 0x8004A103);
            source = _active = new();
        }

        try
        {
            T result = action(source.Token);
            lock (_gate)
                _diagnostic = "";
            return result;
        }
        catch (Exception error)
        {
            string code;
            uint hr;
            if (error is COMException com && com.Message.StartsWith("Automation.", StringComparison.Ordinal) || error is COMException limit && limit.Message.StartsWith("Limit.", StringComparison.Ordinal))
            {
                code = error.Message;
                hr = unchecked((uint)error.HResult);
            }
            else if (error is OperationCanceledException)
            {
                code = "Automation.Canceled";
                hr = 0x8004A107;
            }
            else if (error is PdfException pdf)
            {
                code = pdf.Diagnostic.Code;
                hr = code.StartsWith("Limit.", StringComparison.Ordinal) ? 0x8004A106u : 0x8004A108u;
            }
            else if (error is ArgumentException)
            {
                code = "Automation.InvalidArgument";
                hr = 0x80070057;
            }
            else if (error is IOException || error is UnauthorizedAccessException)
            {
                code = "Automation.IO";
                hr = 0x8004A109;
            }
            else
            {
                code = "Automation.Failed";
                hr = 0x8004A10A;
            }

            lock (_gate)
                _diagnostic = code;
            throw Failure(code, hr);
        }
        finally
        {
            lock (_gate)
                _active = null;
            source.Dispose();
        }
    }

    private static COMException Failure(string code, uint hr) => new(code, unchecked((int)hr));
    private static void Path(string value)
    {
        if (value == null || value.Length == 0 || value.Length > 32767 || value.IndexOf('\0') >= 0)
            throw new ArgumentException("An explicit bounded path is required.");
    }

    public void Dispose()
    {
        lock (_gate)
        {
            if (_disposed)
                return;
            if (_active != null)
                throw Failure("Automation.Busy", 0x8004A103);
            _document?.Dispose();
            _document = null;
            _disposed = true;
        }
    }
}
