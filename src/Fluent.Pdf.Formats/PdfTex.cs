using System.IO;
using System.Text;
using System.Threading;

namespace Fluent.Pdf.Formats;
/// <summary>Explicit TeX engine/format identities. Availability never implies permission for shell escape or ambient file/network access.</summary>
public enum PdfTexProfile
{
    /// <summary>pdfTeX with the LaTeX format.</summary>
    PdfLatex,
    /// <summary>LuaTeX with the LaTeX format; embedded Lua must remain isolated by the host.</summary>
    LuaLatex,
    /// <summary>XeTeX with the LaTeX format.</summary>
    XeLatex
}

/// <summary>Immutable, explicit engine and local package prerequisites. No package installation/discovery is permitted.</summary>
public sealed class PdfTexOptions
{
    /// <summary>Captures at most128 distinct ASCII package identities and an exact engine profile.</summary>
    public PdfTexOptions(PdfTexProfile profile, IEnumerable<string>? requiredPackages = null, int maximumPages = 1000)
    {
        if (!Enum.IsDefined(typeof(PdfTexProfile), profile))
            throw new ArgumentOutOfRangeException(nameof(profile));
        if (maximumPages < 1 || maximumPages > 10000)
            throw new ArgumentOutOfRangeException(nameof(maximumPages));
        var packages = new List<string>();
        if (requiredPackages != null)
            foreach (string name in requiredPackages)
            {
                if (packages.Count == 128)
                    throw new ArgumentException("At most128 packages may be requested.", nameof(requiredPackages));
                Guard.Text(name, nameof(requiredPackages), 80);
                if (name.Any(ch => !(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || "-_".IndexOf(ch) >= 0)) || packages.Contains(name, StringComparer.Ordinal))
                    throw new ArgumentException("Distinct literal ASCII package identities are required.", nameof(requiredPackages));
                packages.Add(name);
            }

        Profile = profile;
        RequiredPackages = packages.AsReadOnly();
        MaximumPages = maximumPages;
    }

    /// <summary>Gets exact engine/format profile.</summary>
    public PdfTexProfile Profile { get; }
    /// <summary>Gets required locally pinned packages. Font, format and transitive package assets are explicit host prerequisites too.</summary>
    public IReadOnlyList<string> RequiredPackages { get; }
    /// <summary>Gets the nonempty output page bound.</summary>
    public int MaximumPages { get; }
}

/// <summary>Trusted caller-owned host for a separately qualified offline OS-isolated typesetter. This contract alone does not supply or qualify a sandbox.</summary>
public interface IPdfIsolatedTexTypesetter
{
    /// <summary>Gets a stable nonsecret ASCII pinned engine/configuration identity.</summary>
    string EngineId { get; }

    /// <summary>Checks the exact engine and all local package/font/format prerequisites before source input is read.</summary>
    bool Supports(PdfTexOptions options);
    /// <summary>Typesets in a fresh bounded OS-isolated job with shell escape, ambient files/network, child processes and runtime installation denied. Lua and TeX file primitives need the same isolation. Leases expire at return; all phases use this context and actual worker scopes.</summary>
    void Typeset(Stream source, PdfTexOptions options, Stream pdfOutput, PdfOperationContext context);
}

/// <summary>Explicit optional typesetting import and passive ASCII LaTeX text reconstruction.</summary>
public static class PdfTex
{
    /// <summary>Requires exact prerequisites before capture; stages at most8 MiB source and64 MiB PDF, validates nonempty PDF and preserves caller ownership.</summary>
    public static PdfDocument Import(Stream source, PdfTexOptions options, IPdfIsolatedTexTypesetter? typesetter = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || !source.CanRead)
            throw new ArgumentException("Readable borrowed TeX source is required.", nameof(source));
        if (options == null)
            throw new ArgumentNullException(nameof(options));
        string identity = typesetter?.EngineId ?? "unavailable";
        Guard.Text(identity, nameof(typesetter), 80);
        if (identity.Any(ch => !(ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z' || ch >= '0' && ch <= '9' || "._-".IndexOf(ch) >= 0)))
            throw new ArgumentException("A stable ASCII engine identity is required.", nameof(typesetter));
        PdfDocument? result = null;
        try
        {
            var report = PdfFont.Run("ImportTex", "tex." + identity, policy, cancellationToken, c =>
            {
                if (typesetter == null)
                    throw Error(c, "TypesetterUnavailable", "Configure an explicitly pinned, separately qualified isolated TeX host.");
                bool supported;
                try
                {
                    supported = typesetter.Supports(options);
                }
                catch
                {
                    c.Checkpoint();
                    throw Error(c, "TypesetterFailed", "The configured typesetter prerequisite check failed.");
                }

                if (!supported)
                    throw Error(c, "PrerequisiteUnavailable", "The exact engine/format or required local package/font assets are unavailable.");
                var bytes = FormatIo.Capture(source, c, 8388608);
                using var input = bytes.Open(0, bytes.Length);
                using var staged = new PdfCodecBufferStream(c, 67108864);
                try
                {
                    using var inputLease = new BorrowedStream(input, true, c);
                    using var outputLease = new BorrowedStream(staged, false, c);
                    typesetter.Typeset(inputLease, options, outputLease, c);
                    c.Checkpoint();
                }
                catch
                {
                    c.Checkpoint();
                    throw Error(c, "TypesetterFailed", "The configured typesetter failed without exposing partial output.");
                }

                result = PdfPageOrganization.Read(staged.Finish(), options.MaximumPages, c);
                if (result.Pages.Count == 0)
                {
                    result.Dispose();
                    result = null;
                    throw Error(c, "EmptyOutput", "A nonempty valid PDF is required from the typesetter.");
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

    /// <summary>Stages an escaped ASCII LaTeX article in selected page order. Explicit permissive authority is required for text/layout/math reconstruction loss. No source string can introduce a TeX command.</summary>
    public static PdfOperationReport WriteInferredText(PdfDocumentSnapshot source, IEnumerable<int> pages, Stream output, PdfTextExtractionOptions? text = null, PdfProviderRegistry? providers = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || pages == null)
            throw new ArgumentNullException(source == null ? nameof(source) : nameof(pages));
        if (output == null || !output.CanWrite)
            throw new ArgumentException("A writable borrowed stream is required.", nameof(output));
        providers ??= PdfProviderRegistry.Empty;
        text ??= new(includeInvisibleText: false);
        return PdfFont.Run("ExportTexText", "managed.tex-text", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            using var graph = new PdfGraphEdit(source, new(allowSignatureInvalidation: true), c);
            using var staged = new PdfCodecBufferStream(c, 8388608);
            using (var writer = new StreamWriter(staged, new UTF8Encoding(false, true), 1024, true))
            {
                c.Consume(PdfResourceKind.ExpandedBytes, 4096);
                writer.Write("% Inferred text only; no mathematical or source layout semantics asserted.\n\\documentclass{article}\n\\usepackage[T1]{fontenc}\n\\begin{document}\n\\ttfamily\\raggedright\n");
                int count = 0;
                foreach (int page in pages)
                {
                    c.Checkpoint();
                    c.Consume(PdfResourceKind.Objects, 1);
                    if (++count > 512 || page < 0 || page >= source.Pages.Count)
                        throw Error(c, "PageSelection", "Select1–512 existing pages in explicit order.");
                    c.Report(new("Tex.InferredText", "Only inferred literal text is retained; original fonts, geometry, graphics, accessibility and mathematical semantics are not reconstructed.", lossCategory: PdfLossCategory.FidelityReduction, pageIndex: page));
                    string value = PdfHtmlExport.ReadText(graph, page, text, providers, c);
                    if (count > 1)
                        writer.Write("\n\\newpage\n");
                    foreach (char ch in value)
                    {
                        c.Checkpoint();
                        if (ch > 126 || ch < 32 && ch != '\n' && ch != '\r' && ch != '\t')
                            throw Error(c, "TextEncodingUnsupported", "The closed source export profile requires printable ASCII text and line whitespace.");
                        c.Consume(PdfResourceKind.ExpandedBytes, 64);
                        switch (ch)
                        {
                            case '\\':
                                writer.Write("\\textbackslash{}");
                                break;
                            case '{':
                            case '}':
                            case '#':
                            case '$':
                            case '%':
                            case '&':
                            case '_':
                                writer.Write('\\');
                                writer.Write(ch);
                                break;
                            case '~':
                                writer.Write("\\textasciitilde{}");
                                break;
                            case '^':
                                writer.Write("\\textasciicircum{}");
                                break;
                            case '<':
                                writer.Write("\\textless{}");
                                break;
                            case '>':
                                writer.Write("\\textgreater{}");
                                break;
                            case '\n':
                                writer.Write("\\par\n");
                                break;
                            case '\r':
                                break;
                            case '\t':
                                writer.Write("    ");
                                break;
                            default:
                                writer.Write(ch);
                                break;
                        }
                    }

                    writer.Write("\n\\par\n");
                }

                if (count == 0)
                    throw Error(c, "PageSelection", "A nonempty page selection is required.");
                writer.Write("\\end{document}\n");
                writer.Flush();
            }

            FormatIo.Copy(staged.Finish(), output, c);
        }, PdfCapability.ReadDocument);
    }

    private static PdfException Error(PdfOperationContext c, string code, string message) => PdfWriterCore.Error(c, "Tex." + code, message);
}
