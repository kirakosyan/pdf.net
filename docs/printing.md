# Explicit printing

FP-043 / queue #61 has an original bounded print orchestration and IPP 2.0 source profile. Complete Windows spool-system adapters, controlled CUPS/IPP and virtual/physical-printer integration, minimum runtimes and production qualification remain mandatory. The tracking requirement stays Planned.

`PdfPrinting.Submit` requires an exact printer identity and explicit provider. It serializes the complete immutable PDF snapshot under the operation context before making one spool submission. No opening, inspection or construction operation selects a printer or prints. Copies, one-based inclusive ranges, duplex, fit/physical scaling and N-up are immutable choices; invalid ranges fail before submission. Caller documents, providers and transports remain owned by the caller. Private print serialization permits rewriting the print copy, including signed inputs; it never changes the original bytes or asserts cryptographic validity of the spooled copy.

The optional `Fluent.Pdf.Rendering.IppPrintProvider` implements original IPP 2.0 Print-Job, Get-Job-Attributes and Cancel-Job framing with `application/pdf`, UTF-8, exact request identity, a 1 MiB response limit and bounded attribute parsing. It requires attribute fidelity and rejects success-with-substitution as `Print.Substitution`, permission failures as `Print.Permission`, other spool rejection as `Print.SpoolRejected`, malformed/mismatched responses as `Print.Protocol`, and non-IPP targets as `Print.PrinterUri`. It never retries Print-Job. The caller supplies `IPdfIppTransport`, whose authenticated endpoint, network permission, credentials, TLS/local connection and cancellation behavior are explicit host responsibilities. The SDK does not discover a daemon, shell out, resolve document URLs or download binaries.

```csharp
var providers = new PdfProviderRegistry(new IppPrintProvider(configuredTransport));
var job = PdfPrinting.Submit(snapshot,
    new PdfPrintOptions("ipps://printer.example/printers/accounts", copies: 2,
        duplex: PdfPrintDuplex.LongEdge, numberUp: 2), providers);
var observed = PdfPrinting.GetStatus(job.Printer, job.JobId, providers);
```

The IPP protocol source is headless and has no GUI or Windows graphics dependency; actual printer acceptance on each operating system must be qualified with the configured transport and printer. Windows native spooler/dialog support is not supplied by this profile. Core types expose the same contract across the four target frameworks; compilation does not qualify printing on a platform.

Spool acceptance/completion is an observation, not proof of physical output. Cancellation acknowledgment cannot recall pages already printed. A transport error or cancellation after submission may leave an accepted job whose identifier was not returned; blindly retrying can print duplicates. Use spool-system reconciliation and explicit job identities. Host authentication/permissions errors must remain actionable and must not expose credentials or document text in logs.

Contract fixtures inspect exact IPP bytes and PDF payload, observe state/cancellation, verify stream-lease expiry, and reject permission/substitution/malformed identity, invalid ranges, cancellation and resource limits. They do not stand in for virtual/physical-printer evidence.
