using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Explicit provider-backed Word interchange, with shared-budget PDF import and atomic file export.</summary>
public static class PdfWordConversion
{
    /// <summary>Converts Word input at its borrowed current position into a caller-owned editable PDF model. Input remains open.</summary>
    public static PdfDocument Import(Stream input, PdfWordFormat format, PdfWordConversionOptions options, PdfProviderRegistry providers, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Validate(format, options, providers);
        if (input == null || !input.CanRead)
            throw new ArgumentException("A readable borrowed source is required.", nameof(input));
        var converter = providers.GetRequired<IPdfWordConverter>(PdfCapability.ConvertWord);
        if (!converter.Supports(format, true))
            throw new PdfException(new("Word.FormatUnsupported", "The selected provider does not support this import direction/format."));
        PdfDocument? document = null;
        try
        {
            var report = PdfFont.Run("ImportWord", providers.GetIdentifier(PdfCapability.ConvertWord), policy, cancellationToken, c =>
            {
                var captured = Capture(input, options.MaximumInputBytes, c);
                using var output = new PdfCodecBufferStream(c, options.MaximumOutputBytes);
                using (var source = captured.Open(0, captured.Length))
                using (var lease = new BorrowedStream(source, true, c))
                using (var target = new BorrowedStream(output, false, c))
                    converter.Import(lease, format, target, options, c);
                c.Checkpoint();
                document = PdfPageOrganization.Read(output.Finish(), options.MaximumPages, c);
            }, PdfCapability.ConvertWord);
            document!.SetCompositionReport(report);
            return document;
        }
        catch
        {
            document?.Dispose();
            throw;
        }
    }

    /// <summary>Exports at a borrowed output's current position; source is unchanged and the stream stays open. Failure may leave a prefix.</summary>
    public static PdfOperationReport Write(PdfDocumentSnapshot source, PdfWordFormat format, PdfWordConversionOptions options, PdfProviderRegistry providers, Stream output, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed output is required.", nameof(output));
        return Run(source, format, options, providers, output, null, policy, cancellationToken);
    }

    /// <summary>Exports to a private destination-directory file before atomic replacement. Failures preserve an existing destination.</summary>
    public static PdfOperationReport WriteFile(PdfDocumentSnapshot source, PdfWordFormat format, PdfWordConversionOptions options, PdfProviderRegistry providers, string path, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(path, nameof(path), 32767);
        return Run(source, format, options, providers, null, path, policy, cancellationToken);
    }

    private static PdfOperationReport Run(PdfDocumentSnapshot source, PdfWordFormat format, PdfWordConversionOptions options, PdfProviderRegistry providers, Stream? output, string? path, PdfOperationPolicy? policy, CancellationToken token)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        Validate(format, options, providers);
        var converter = providers.GetRequired<IPdfWordConverter>(PdfCapability.ConvertWord);
        if (!converter.Supports(format, false))
            throw new PdfException(new("Word.FormatUnsupported", "The selected provider does not support this export direction/format."));
        return PdfFont.Run(path == null ? "ExportWord" : "ExportWordFile", providers.GetIdentifier(PdfCapability.ConvertWord), policy ?? source.Options.OperationPolicy, token, c =>
        {
            if (source.Pages.Count > options.MaximumPages)
                throw PdfWriterCore.Error(c, "Word.PageLimit", "The explicit conversion page ceiling was exceeded.");
            void Export(Stream target)
            {
                using var buffer = new PdfCodecBufferStream(c, options.MaximumOutputBytes);
                using (var destination = new BorrowedStream(buffer, false, c))
                    converter.Export(source, format, destination, options, providers, c);
                c.Checkpoint();
                var bytes = buffer.Finish();
                using var captured = bytes.Open(0, bytes.Length);
                using var lease = new BorrowedStream(target, false, c);
                Copy(captured, lease, bytes.Length, c);
            }

            if (path == null)
                Export(output!);
            else
                PdfAtomicFile.Write(path, c, Export);
        }, PdfCapability.ConvertWord);
    }

    internal static PdfByteStore Capture(Stream input, int maximumBytes, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var chunk = new byte[8192];
        using var source = new BorrowedStream(input, true, c);
        using var output = new PdfCodecBufferStream(c, maximumBytes);
        while (true)
        {
            c.Checkpoint();
            int n = source.Read(chunk, 0, (int)Math.Min(chunk.Length, maximumBytes - output.BufferedLength + 1));
            if (n == 0)
                break;
            if (output.BufferedLength + n > maximumBytes)
                throw PdfWriterCore.Error(c, "Word.InputLimit", "The explicit Word source-byte ceiling was exceeded.");
            output.Write(chunk, 0, n);
        }

        return output.Finish();
    }

    internal static void Copy(Stream source, Stream output, long count, PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 8192);
        var chunk = new byte[8192];
        while (count > 0)
        {
            c.Checkpoint();
            int n = source.Read(chunk, 0, (int)Math.Min(chunk.Length, count));
            if (n == 0)
                throw PdfWriterCore.Error(c, "Word.OutputLength", "A private conversion buffer ended unexpectedly.");
            output.Write(chunk, 0, n);
            count -= n;
        }
    }

    private static void Validate(PdfWordFormat format, PdfWordConversionOptions options, PdfProviderRegistry providers)
    {
        if (!Enum.IsDefined(typeof(PdfWordFormat), format))
            throw new ArgumentOutOfRangeException(nameof(format));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        if (providers == null)
            throw new ArgumentNullException(nameof(providers));
    }
}
