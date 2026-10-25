using System.IO;
using System.Threading;

namespace Fluent.Pdf.Formats;

/// <summary>Explicit read-only source format for a separately provisioned import host.</summary>
public enum PdfLegacyFormat
{
    /// <summary>DjVu scanned documents.</summary>
    DjVu,
    /// <summary>CDR vector documents.</summary>
    Cdr
}

/// <summary>Stable import outcomes. A completed decode must still return a nonempty valid PDF.</summary>
public enum PdfLegacyImportStatus
{
    /// <summary>The requested exact profile was decoded.</summary>
    Completed,
    /// <summary>The source version does not match the requested supported profile.</summary>
    UnsupportedVersion,
    /// <summary>A required explicitly provisioned codec is absent.</summary>
    MissingCodec,
    /// <summary>A required explicitly provisioned font is absent.</summary>
    MissingFont,
    /// <summary>A source record cannot be represented by this host.</summary>
    UnsupportedRecord,
    /// <summary>The source is damaged.</summary>
    DamagedInput
}

/// <summary>Immutable exact decoder profile and finite source, output and page bounds.</summary>
public sealed class PdfLegacyImportOptions
{
    /// <summary>Requires a literal host-defined version/profile identity; no format version is inferred or advertised by the SDK wrapper.</summary>
    public PdfLegacyImportOptions(PdfLegacyFormat format, string versionProfile, int maximumPages = 1000, int maximumInputBytes = 16777216, int maximumOutputBytes = 67108864)
    {
        if (!Enum.IsDefined(typeof(PdfLegacyFormat), format)) throw new ArgumentOutOfRangeException(nameof(format));
        Identity(versionProfile, nameof(versionProfile));
        if (maximumPages < 1 || maximumPages > 10000) throw new ArgumentOutOfRangeException(nameof(maximumPages));
        if (maximumInputBytes < 1 || maximumInputBytes > 67108864) throw new ArgumentOutOfRangeException(nameof(maximumInputBytes));
        if (maximumOutputBytes < 1 || maximumOutputBytes > 67108864) throw new ArgumentOutOfRangeException(nameof(maximumOutputBytes));
        Format = format;
        VersionProfile = versionProfile;
        MaximumPages = maximumPages;
        MaximumInputBytes = maximumInputBytes;
        MaximumOutputBytes = maximumOutputBytes;
    }
    /// <summary>Gets the explicit source format.</summary>
    public PdfLegacyFormat Format { get; }
    /// <summary>Gets the exact separately documented host version/compression/font profile.</summary>
    public string VersionProfile { get; }
    /// <summary>Gets the maximum nonempty output page count.</summary>
    public int MaximumPages { get; }
    /// <summary>Gets the source capture byte limit.</summary>
    public int MaximumInputBytes { get; }
    /// <summary>Gets the staged PDF byte limit.</summary>
    public int MaximumOutputBytes { get; }

    internal static void Identity(string value, string parameter)
    {
        Guard.Text(value, parameter, 80);
        if (value.Any(ch => !(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || "._-".IndexOf(ch) >= 0)))
            throw new ArgumentException("A bounded literal ASCII identity is required.", parameter);
    }
}

/// <summary>Caller-owned trusted adapter for an independently qualified offline isolated decoder. Implementing this interface does not supply a sandbox or codec.</summary>
public interface IPdfIsolatedLegacyImporter
{
    /// <summary>Gets the pinned nonsecret engine/configuration identity.</summary>
    string EngineId { get; }
    /// <summary>Checks the exact format/version and local codec/font prerequisites before input capture. No downloads or ambient discovery are allowed.</summary>
    bool Supports(PdfLegacyImportOptions options);
    /// <summary>Imports in a fresh bounded OS-isolated job. Deny ambient file/network access and external document references; charge resources, use actual worker scopes and report every fidelity loss. Borrowed leases expire at return. No source actions may execute.</summary>
    PdfLegacyImportStatus Import(Stream source, PdfLegacyImportOptions options, Stream pdfOutput, PdfOperationContext context);
}

/// <summary>Bounded, explicit DjVu/CDR host integration. No built-in decoder or supported source version is supplied.</summary>
public static class PdfLegacyImport
{
    /// <summary>Captures source and stages/reparses all PDF output under one operation context. Failure returns no partial document; caller streams and host remain owned by the caller.</summary>
    public static PdfDocument Read(Stream source, PdfLegacyImportOptions options, IPdfIsolatedLegacyImporter? importer = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || !source.CanRead) throw new ArgumentException("Readable borrowed input is required.", nameof(source));
        if (options == null) throw new ArgumentNullException(nameof(options));
        string identity = importer?.EngineId ?? "unavailable";
        PdfLegacyImportOptions.Identity(identity, nameof(importer));
        PdfDocument? document = null;
        try
        {
            var report = PdfFont.Run("ImportLegacyFormat", "legacy." + identity, policy, cancellationToken, c =>
            {
                if (importer == null) throw Error(c, "HostUnavailable");
                bool supported;
                try { supported = importer.Supports(options); }
                catch { c.Checkpoint(); throw Error(c, "HostFailed"); }
                if (!supported) throw Error(c, "ProfileUnavailable");
                var bytes = FormatIo.Capture(source, c, options.MaximumInputBytes);
                using var input = bytes.Open(0, bytes.Length);
                using var staged = new PdfCodecBufferStream(c, options.MaximumOutputBytes);
                PdfLegacyImportStatus status;
                try
                {
                    using var inputLease = new BorrowedStream(input, true, c);
                    using var outputLease = new BorrowedStream(staged, false, c);
                    status = importer.Import(inputLease, options, outputLease, c);
                    c.Checkpoint();
                }
                catch { c.Checkpoint(); throw Error(c, "HostFailed"); }
                if (!Enum.IsDefined(typeof(PdfLegacyImportStatus), status)) throw Error(c, "InvalidStatus");
                if (status != PdfLegacyImportStatus.Completed) throw Error(c, status.ToString());
                document = PdfPageOrganization.Read(staged.Finish(), options.MaximumPages, c);
                if (document.Pages.Count == 0) throw Error(c, "EmptyOutput");
            }, PdfCapability.WriteDocument);
            document!.SetCompositionReport(report);
            return document;
        }
        catch { document?.Dispose(); throw; }
    }

    private static PdfException Error(PdfOperationContext c, string code) => PdfWriterCore.Error(c, "LegacyFormat." + code, "The explicit isolated format import did not complete the requested profile.");
}
