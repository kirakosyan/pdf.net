using System.IO;
using System.Threading;

namespace Fluent.Pdf;

/// <summary>Explicit physical sheet duplex policy; no printer defaults are inferred.</summary>
public enum PdfPrintDuplex
{
    /// <summary>One side per sheet.</summary>
    Simplex,
    /// <summary>Bind along the long edge.</summary>
    LongEdge,
    /// <summary>Bind along the short edge.</summary>
    ShortEdge
}
/// <summary>Explicit print scaling.</summary>
public enum PdfPrintScaling
{
    /// <summary>Fit the selected print area.</summary>
    Fit,
    /// <summary>Use physical PDF dimensions.</summary>
    None
}
/// <summary>Observed spool state. Submission does not prove physical printing.</summary>
public enum PdfPrintJobState
{
    /// <summary>The spooler supplied no recognized state.</summary>
    Unknown,
    /// <summary>Queued.</summary>
    Pending,
    /// <summary>Held by the spooler.</summary>
    Held,
    /// <summary>Processing.</summary>
    Processing,
    /// <summary>Stopped pending intervention.</summary>
    Stopped,
    /// <summary>Canceled by the spooler.</summary>
    Canceled,
    /// <summary>Aborted by the spooler.</summary>
    Aborted,
    /// <summary>Completed according to the spooler.</summary>
    Completed,
    /// <summary>Cancellation acknowledged; physical output may already exist.</summary>
    CancellationRequested
}
/// <summary>Immutable explicit printer and sheet choices. Page numbers are one-based and inclusive.</summary>
public sealed class PdfPrintOptions
{
    /// <summary>Selects a printer explicitly. Last page zero means the captured document's final page. N-up supports 1, 2, 4, 6, 9 or 16.</summary>
    public PdfPrintOptions(string printer, int copies = 1, PdfPrintDuplex duplex = PdfPrintDuplex.Simplex, PdfPrintScaling scaling = PdfPrintScaling.Fit, int numberUp = 1, int firstPage = 1, int lastPage = 0)
    {
        Printer = Guard.Text(printer, nameof(printer), 2048);
        if (copies < 1 || copies > 999 || firstPage < 1 || lastPage < 0 || lastPage != 0 && lastPage < firstPage)
            throw new ArgumentOutOfRangeException(nameof(copies));
        if (numberUp != 1 && numberUp != 2 && numberUp != 4 && numberUp != 6 && numberUp != 9 && numberUp != 16)
            throw new ArgumentOutOfRangeException(nameof(numberUp));
        if (!Enum.IsDefined(typeof(PdfPrintDuplex), duplex) || !Enum.IsDefined(typeof(PdfPrintScaling), scaling))
            throw new ArgumentOutOfRangeException(nameof(duplex));
        Copies = copies;
        Duplex = duplex;
        Scaling = scaling;
        NumberUp = numberUp;
        FirstPage = firstPage;
        LastPage = lastPage;
    }
    /// <summary>Gets the exact selected printer identity.</summary>
    public string Printer
    {
        get;
    }
    /// <summary>Gets the number of copies.</summary>
    public int Copies
    {
        get;
    }
    /// <summary>Gets duplex policy.</summary>
    public PdfPrintDuplex Duplex
    {
        get;
    }
    /// <summary>Gets physical scaling policy.</summary>
    public PdfPrintScaling Scaling
    {
        get;
    }
    /// <summary>Gets logical pages per sheet.</summary>
    public int NumberUp
    {
        get;
    }
    /// <summary>Gets the first one-based page.</summary>
    public int FirstPage
    {
        get;
    }
    /// <summary>Gets the inclusive last page, or zero for the final page.</summary>
    public int LastPage
    {
        get;
    }
}
/// <summary>One bounded spool observation. No output success is inferred from an accepted job.</summary>
public sealed class PdfPrintJob
{
    /// <summary>Captures exact printer/job identity and an observed state.</summary>
    public PdfPrintJob(string printer, int jobId, PdfPrintJobState state)
    {
        Printer = Guard.Text(printer, nameof(printer), 2048);
        if (jobId < 1)
            throw new ArgumentOutOfRangeException(nameof(jobId));
        if (!Enum.IsDefined(typeof(PdfPrintJobState), state))
            throw new ArgumentOutOfRangeException(nameof(state));
        JobId = jobId;
        State = state;
    }
    /// <summary>Gets exact printer identity.</summary>
    public string Printer
    {
        get;
    }
    /// <summary>Gets spooler-assigned job identity.</summary>
    public int JobId
    {
        get;
    }
    /// <summary>Gets observed state.</summary>
    public PdfPrintJobState State
    {
        get;
    }
}
/// <summary>Explicit optional print-system boundary. Implementations must reject unsupported choices, observe the context and never retry submission implicitly.</summary>
public interface IPdfPrintProvider : IPdfProvider
{
    /// <summary>Submits privately serialized PDF bytes exactly once, requiring all requested attributes. Borrowed streams expire on return.</summary>
    PdfPrintJob Submit(Stream pdf, int pageCount, PdfPrintOptions options, PdfOperationContext context);
    /// <summary>Gets one observation for the exact printer/job.</summary>
    PdfPrintJob GetStatus(string printer, int jobId, PdfOperationContext context);
    /// <summary>Requests cancellation of the exact job; does not promise that physical output can be recalled.</summary>
    PdfPrintJob Cancel(string printer, int jobId, PdfOperationContext context);
}
/// <summary>Explicit headless printing; inspection/opening never selects a printer or submits a job.</summary>
public static class PdfPrinting
{
    /// <summary>Serializes completely before the one explicit spool submission. A cancellation/transport failure after submission may leave an accepted job; use its spool identity where available and never blindly retry.</summary>
    public static PdfPrintJob Submit(PdfDocumentSnapshot source, PdfPrintOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || options == null || providers == null)
            throw new ArgumentNullException(source == null ? nameof(source) : options == null ? nameof(options) : nameof(providers));
        var provider = providers.GetRequired<IPdfPrintProvider>(PdfCapability.PrintDocument);
        PdfPrintJob? result = null;
        PdfFont.Run("SubmitPrint", provider.Descriptor.Id, policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            if (source.Pages.Count == 0 || options.FirstPage > source.Pages.Count || options.LastPage > source.Pages.Count)
                throw PdfWriterCore.Error(c, "Print.PageRange", "The explicit print range must identify captured pages.");
            using var buffer = new PdfCodecBufferStream(c, c.Available(PdfResourceKind.ExpandedBytes));
            c.Consume(PdfResourceKind.ExpandedBytes, source.Pages.Count * 128L + 256);
            var printCopy = new PdfDocumentSnapshot(source.Options, source.Pages.ToArray(), source.SourceDocument, source.OriginalOrder, new(allowSignatureInvalidation: true), source.PageNumbering);
            using (var output = new BorrowedStream(buffer, false, c))
                ManagedPdfProvider.Instance.Write(printCopy, output, c);
            var bytes = buffer.Finish();
            using var input = bytes.Open(0, bytes.Length);
            using var lease = c.BorrowExpandedInput(input);
            using var worker = c.EnterScope(PdfResourceKind.Workers);
            result = provider.Submit(lease, source.Pages.Count, options, c);
            Validate(result, options.Printer, null, c);
        }, PdfCapability.PrintDocument);
        return result!;
    }
    /// <summary>Queries only an exact caller-selected printer/job.</summary>
    public static PdfPrintJob GetStatus(string printer, int jobId, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Observe(printer, jobId, providers, false, policy, cancellationToken);
    /// <summary>Cancels only an exact caller-selected printer/job.</summary>
    public static PdfPrintJob Cancel(string printer, int jobId, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default) => Observe(printer, jobId, providers, true, policy, cancellationToken);
    private static PdfPrintJob Observe(string printer, int jobId, PdfProviderRegistry providers, bool cancel, PdfOperationPolicy? policy, CancellationToken token)
    {
        Guard.Text(printer, nameof(printer), 2048);
        if (jobId < 1)
            throw new ArgumentOutOfRangeException(nameof(jobId));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
        var provider = providers.GetRequired<IPdfPrintProvider>(PdfCapability.PrintDocument);
        PdfPrintJob? result = null;
        PdfFont.Run(cancel ? "CancelPrint" : "GetPrintStatus", provider.Descriptor.Id, policy, token, c =>
        {
            using var worker = c.EnterScope(PdfResourceKind.Workers);
            result = cancel ? provider.Cancel(printer, jobId, c) : provider.GetStatus(printer, jobId, c);
            Validate(result, printer, jobId, c);
        }, PdfCapability.PrintDocument);
        return result!;
    }
    private static void Validate(PdfPrintJob? job, string printer, int? jobId, PdfOperationContext c)
    {
        c.Checkpoint();
        if (job == null || job.Printer != printer || jobId.HasValue && job.JobId != jobId)
            throw PdfWriterCore.Error(c, "Print.JobIdentity", "The print provider returned a different printer/job identity.");
    }
}
