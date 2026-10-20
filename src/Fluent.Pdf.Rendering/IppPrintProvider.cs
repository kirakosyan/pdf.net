using System.IO;
using System.Text;

namespace Fluent.Pdf.Rendering;

/// <summary>Caller-owned authenticated IPP transport. It must bind the exact printer endpoint, enforce its network/credential policy and terminate on context cancellation/deadline. No redirects or implicit retries are permitted.</summary>
public interface IPdfIppTransport
{
    /// <summary>Exchanges exactly one application/ipp request/response over an explicit local or authenticated remote connection. Leases expire on return; SDK response storage is bounded.</summary>
    void Exchange(Uri printer, Stream request, Stream response, PdfOperationContext context);
}
/// <summary>Original IPP 2.0 PDF print-job adapter for explicit CUPS/IPP endpoints. No printer discovery, host process launch or runtime download occurs.</summary>
public sealed class IppPrintProvider : IPdfPrintProvider
{
    private readonly IPdfIppTransport _transport;
    /// <summary>Captures an explicitly configured authenticated transport. The caller retains ownership.</summary>
    public IppPrintProvider(IPdfIppTransport transport) => _transport = transport ?? throw new ArgumentNullException(nameof(transport));
    /// <summary>Gets the source IPP adapter identity, without hardware/platform qualification claims.</summary>
    public PdfProviderDescriptor Descriptor { get; } = new("managed.ipp-print", "1", PdfCapability.PrintDocument);
    /// <summary>Submits a PDF once with attribute fidelity required. A rejected/ambiguous response never triggers a retry.</summary>
    public PdfPrintJob Submit(Stream pdf, int pageCount, PdfPrintOptions options, PdfOperationContext context)
    {
        if (pdf == null || options == null || context == null)
            throw new ArgumentNullException(pdf == null ? nameof(pdf) : options == null ? nameof(options) : nameof(context));
        if (pageCount < 1 || options.FirstPage > pageCount || options.LastPage > pageCount)
            throw new ArgumentOutOfRangeException(nameof(pageCount));
        return Exchange(options.Printer, 2, null, pdf, options, pageCount, context);
    }
    /// <summary>Queries one spool job; completion is only a spool observation.</summary>
    public PdfPrintJob GetStatus(string printer, int jobId, PdfOperationContext context) => Exchange(printer, 9, jobId, null, null, 0, context);
    /// <summary>Requests cancellation once. An acknowledgment cannot recall physical output.</summary>
    public PdfPrintJob Cancel(string printer, int jobId, PdfOperationContext context) => Exchange(printer, 8, jobId, null, null, 0, context);
    private PdfPrintJob Exchange(string printer, ushort operation, int? jobId, Stream? pdf, PdfPrintOptions? options, int pages, PdfOperationContext c)
    {
        if (c == null)
            throw new ArgumentNullException(nameof(c));
        c.Checkpoint();
        if (!Uri.TryCreate(printer, UriKind.Absolute, out var endpoint) || (endpoint.Scheme != "ipp" && endpoint.Scheme != "ipps") || endpoint.UserInfo.Length != 0 || endpoint.Fragment.Length != 0 || printer.Length > 2048)
            throw PdfWriterCore.Error(c, "Print.PrinterUri", "An explicit credential-free IPP/IPPS printer URI is required.");
        if (jobId.HasValue && jobId.Value < 1)
            throw new ArgumentOutOfRangeException(nameof(jobId));
        using var request = new PdfCodecBufferStream(c, c.Available(PdfResourceKind.ExpandedBytes));
        request.WriteByte(2);
        request.WriteByte(0);
        U16(request, operation);
        I32(request, 1);
        request.WriteByte(1);
        Attribute(request, 0x47, "attributes-charset", "utf-8", c);
        Attribute(request, 0x48, "attributes-natural-language", "en", c);
        Attribute(request, 0x45, "printer-uri", printer, c);
        if (jobId.HasValue)
            Integer(request, 0x21, "job-id", jobId.Value, c);
        if (options != null)
        {
            Attribute(request, 0x49, "document-format", "application/pdf", c);
            Header(request, 0x22, "ipp-attribute-fidelity", 1, c);
            request.WriteByte(1);
            request.WriteByte(2);
            Integer(request, 0x21, "copies", options.Copies, c);
            Integer(request, 0x21, "number-up", options.NumberUp, c);
            Attribute(request, 0x44, "sides", options.Duplex == PdfPrintDuplex.Simplex ? "one-sided" : options.Duplex == PdfPrintDuplex.LongEdge ? "two-sided-long-edge" : "two-sided-short-edge", c);
            Attribute(request, 0x44, "print-scaling", options.Scaling == PdfPrintScaling.Fit ? "fit" : "none", c);
            Header(request, 0x33, "page-ranges", 8, c);
            I32(request, options.FirstPage);
            I32(request, options.LastPage == 0 ? pages : options.LastPage);
        }
        request.WriteByte(3);
        if (pdf != null)
        {
            c.Consume(PdfResourceKind.ExpandedBytes, 8192);
            var chunk = new byte[8192];
            int n;
            using var input = c.BorrowExpandedInput(pdf);
            while ((n = input.Read(chunk, 0, chunk.Length)) > 0)
            {
                c.Checkpoint();
                request.Write(chunk, 0, n);
            }
        }
        var bytes = request.Finish();
        using var source = bytes.Open(0, bytes.Length);
        using var response = new PdfCodecBufferStream(c, 1048576);
        try
        {
            using var input = c.BorrowExpandedInput(source);
            using var output = new BorrowedStream(response, false, c);
            _transport.Exchange(endpoint, input, output, c);
        }
        catch (Exception error) when (!(error is PdfException) && !(error is OperationCanceledException))
        {
            c.Checkpoint();
            throw PdfWriterCore.Error(c, "Print.Transport", "The explicitly configured IPP transport failed; inspect its credential-safe host diagnostics. Submission is never retried.");
        }
        c.Checkpoint();
        var result = Parse(response.Finish(), printer, jobId, operation == 8, c);
        c.Checkpoint();
        return result;
    }
    private static PdfPrintJob Parse(PdfByteStore bytes, string printer, int? expectedId, bool cancel, PdfOperationContext c)
    {
        long at = 0;
        int Byte()
        {
            if (at >= bytes.Length)
                throw PdfWriterCore.Error(c, "Print.Protocol", "The IPP response is truncated.");
            return bytes.At(at++);
        }
        int Short() => Byte() * 256 + Byte();
        int Int()
        {
            long value = (long)Byte() << 24 | (long)Byte() << 16 | (long)Byte() << 8 | (uint)Byte();
            if (value > int.MaxValue)
                throw PdfWriterCore.Error(c, "Print.Protocol", "The IPP integer is outside this adapter's positive extent.");
            return (int)value;
        }
        if (Byte() != 2 || Byte() != 0)
            throw PdfWriterCore.Error(c, "Print.Protocol", "IPP 2.0 response is required.");
        int status = Short();
        if (Int() != 1)
            throw PdfWriterCore.Error(c, "Print.Protocol", "The IPP request identifier does not match.");
        if (status != 0)
            throw PdfWriterCore.Error(c, status == 0x0401 || status == 0x0402 || status == 0x0403 ? "Print.Permission" : status < 0x0400 ? "Print.Substitution" : "Print.SpoolRejected", "The spooler rejected the request or substituted requested attributes; inspect the configured spooler for details. Submission is never retried.");
        int group = 0, id = 0, state = 0;
        bool hasId = false, hasState = false, ended = false;
        string name = "";
        while (at < bytes.Length)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            int tag = Byte();
            if (tag == 3)
            {
                ended = true;
                break;
            }
            if (tag < 16)
            {
                group = tag;
                name = "";
                continue;
            }
            int length = Short();
            if (length > 0)
            {
                if (length > bytes.Length - at || length > 1024)
                    throw PdfWriterCore.Error(c, "Print.Protocol", "An IPP attribute name exceeds its extent.");
                c.Consume(PdfResourceKind.ExpandedBytes, length * 4L + 32);
                var raw = new byte[length];
                for (int i = 0; i < raw.Length; i++)
                    raw[i] = (byte)Byte();
                try
                {
                    name = new UTF8Encoding(false, true).GetString(raw);
                }
                catch (DecoderFallbackException) { throw PdfWriterCore.Error(c, "Print.Protocol", "An IPP attribute name has invalid UTF8."); }
            }
            if (name.Length == 0)
                throw PdfWriterCore.Error(c, "Print.Protocol", "IPP continuation attributes need a preceding name.");
            int valueLength = Short();
            if (valueLength > bytes.Length - at)
                throw PdfWriterCore.Error(c, "Print.Protocol", "An IPP attribute value exceeds its extent.");
            if (name == "job-id" || name == "job-state")
            {
                bool identity = name == "job-id";
                if (group != 2 || valueLength != 4 || tag != (identity ? 0x21 : 0x23) || (identity ? hasId : hasState))
                    throw PdfWriterCore.Error(c, "Print.Protocol", "Ambiguous or mistyped IPP job attributes reject.");
                int value = Int();
                if (identity)
                {
                    id = value;
                    hasId = true;
                }
                else
                {
                    state = value;
                    hasState = true;
                }
            }
            else
                at += valueLength;
        }
        if (!ended || at != bytes.Length || !expectedId.HasValue && id < 1 || expectedId.HasValue && hasId && id != expectedId.Value)
            throw PdfWriterCore.Error(c, "Print.Protocol", "A complete matching IPP job response is required.");
        var observed = cancel ? PdfPrintJobState.CancellationRequested : state switch
        {
            3 => PdfPrintJobState.Pending,
            4 => PdfPrintJobState.Held,
            5 => PdfPrintJobState.Processing,
            6 => PdfPrintJobState.Stopped,
            7 => PdfPrintJobState.Canceled,
            8 => PdfPrintJobState.Aborted,
            9 => PdfPrintJobState.Completed,
            _ => PdfPrintJobState.Unknown
        };
        return new(printer, expectedId ?? id, observed);
    }
    private static void Attribute(Stream output, byte tag, string name, string value, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, value.Length * 4L + 32);
        var encoded = new UTF8Encoding(false, true).GetBytes(value);
        Header(output, tag, name, encoded.Length, c);
        output.Write(encoded, 0, encoded.Length);
    }
    private static void Integer(Stream output, byte tag, string name, int value, PdfOperationContext c)
    {
        Header(output, tag, name, 4, c);
        I32(output, value);
    }
    private static void Header(Stream output, byte tag, string name, int length, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, name.Length + 32);
        var encoded = Encoding.ASCII.GetBytes(name);
        output.WriteByte(tag);
        U16(output, encoded.Length);
        output.Write(encoded, 0, encoded.Length);
        U16(output, length);
    }
    private static void U16(Stream output, int value)
    {
        output.WriteByte((byte)(value >> 8));
        output.WriteByte((byte)value);
    }
    private static void I32(Stream output, int value)
    {
        for (int shift = 24; shift >= 0; shift -= 8)
            output.WriteByte((byte)(value >> shift));
    }
}
