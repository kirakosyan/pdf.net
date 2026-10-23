using System.IO;
using System.Threading;

namespace Fluent.Pdf.Formats;
/// <summary>Exact caller-selected language/version. No language or engine substitution occurs.</summary>
public enum PdfPrinterLanguageProfile
{
    /// <summary>PostScript language level2.</summary>
    PostScriptLevel2,
    /// <summary>PostScript language level3.</summary>
    PostScriptLevel3,
    /// <summary>Encapsulated PostScript level2.</summary>
    EpsLevel2,
    /// <summary>Encapsulated PostScript level3.</summary>
    EpsLevel3,
    /// <summary>PCL5 monochrome.</summary>
    Pcl5,
    /// <summary>PCL5e.</summary>
    Pcl5e,
    /// <summary>PCL5c color.</summary>
    Pcl5c,
    /// <summary>PCL XL.</summary>
    PclXl
}

/// <summary>Trusted optional host contract for a separately qualified, offline, OS-isolated printer-language interpreter. Merely implementing this interface is not isolation/engine qualification.</summary>
public interface IPdfIsolatedPrinterInterpreter
{
    /// <summary>Gets a stable nonsecret ASCII engine/configuration identity. Do not return payloads, paths or credentials.</summary>
    string EngineId { get; }

    /// <summary>Reports exact language/version availability before input capture; no automatic installation or fallback is permitted.</summary>
    bool Supports(PdfPrinterLanguageProfile profile);
    /// <summary>Converts in a fresh bounded OS-isolated job using explicit local engine/font/procset assets and the shared context. Arbitrary host files, processes, network, shell execution and discovery must be denied. Caller leases expire at return. Strict unsupported commands/substitutions must report through the context.</summary>
    void Convert(Stream input, PdfPrinterLanguageProfile profile, int maximumPages, Stream pdfOutput, PdfOperationContext context);
}

/// <summary>Explicit bounded import through a caller-owned optional isolated interpreter. The managed static EPS subset has a separate non-executing API.</summary>
public static class PdfPrinterLanguages
{
    /// <summary>Requires exact interpreter support before reading; captures at most8 MiB input, stages at most64 MiB complete PDF and imports under the same context. Missing/failed/empty output cannot return a substitute document.</summary>
    public static PdfDocument Import(Stream input, PdfPrinterLanguageProfile profile, IPdfIsolatedPrinterInterpreter? interpreter = null, int maximumPages = 1000, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable borrowed language stream is required.", nameof(input));
        if (!Enum.IsDefined(typeof(PdfPrinterLanguageProfile), profile))
            throw new ArgumentOutOfRangeException(nameof(profile));
        if (maximumPages < 1 || maximumPages > 10000)
            throw new ArgumentOutOfRangeException(nameof(maximumPages));
        string identity = interpreter?.EngineId ?? "unavailable";
        Guard.Text(identity, nameof(interpreter), 80);
        if (identity.Any(ch => !(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || "._-".IndexOf(ch) >= 0)))
            throw new ArgumentException("A stable ASCII engine identity is required.", nameof(interpreter));
        PdfDocument? result = null;
        try
        {
            var report = PdfFont.Run("ImportPrinterLanguage", "printer." + identity, policy, cancellationToken, c =>
            {
                if (interpreter == null)
                    throw Error(c, "InterpreterUnavailable", "This language needs an explicitly configured, separately qualified isolated interpreter.");
                bool supported;
                try
                {
                    supported = interpreter.Supports(profile);
                }
                catch
                {
                    c.Checkpoint();
                    throw Error(c, "InterpreterFailed", "The configured interpreter capability check failed.");
                }

                if (!supported)
                    throw Error(c, "ProfileUnsupported", "The configured interpreter does not supply this exact language/version.");
                c.Checkpoint();
                var bytes = FormatIo.Capture(input, c, 8388608);
                using var source = bytes.Open(0, bytes.Length);
                using var buffer = new PdfCodecBufferStream(c, 67108864);
                try
                {
                    using var inputLease = new BorrowedStream(source, true, c);
                    using var outputLease = new BorrowedStream(buffer, false, c);
                    interpreter.Convert(inputLease, profile, maximumPages, outputLease, c);
                    c.Checkpoint();
                }
                catch
                {
                    c.Checkpoint();
                    throw Error(c, "InterpreterFailed", "The explicitly configured interpreter failed; no partial PDF was exposed.");
                }

                result = PdfPageOrganization.Read(buffer.Finish(), maximumPages, c);
                if (result.Pages.Count == 0)
                {
                    result.Dispose();
                    result = null;
                    throw Error(c, "EmptyOutput", "The interpreter returned no valid nonempty page document.");
                }
            }, PdfCapability.WriteDocument);
            result!.SetCompositionReport(report);
            return result;
        }
        catch
        {
            result?.Dispose();
            throw;
        }
    }

    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Printer." + code, message);
}
