using System.Collections.ObjectModel;
using System.IO;

namespace Fluent.Pdf;
/// <summary>Immutable web-optimization request. Full rewrites discard existing linearization and must rebuild it for the exact resulting bytes.</summary>
public sealed class PdfLinearizationOptions
{
    /// <summary>Selects the first displayed page and explicit authority for signature invalidation.</summary>
    public PdfLinearizationOptions(int firstPageIndex = 0, bool allowSignatureInvalidation = false)
    {
        if (firstPageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(firstPageIndex));
        FirstPageIndex = firstPageIndex;
        AllowSignatureInvalidation = allowSignatureInvalidation;
    }

    /// <summary>Gets the zero-based first displayed page.</summary>
    public int FirstPageIndex { get; }
    /// <summary>Gets explicit full-rewrite signature invalidation authority.</summary>
    public bool AllowSignatureInvalidation { get; }
}

/// <summary>Structural linearization dictionary observations. These bounds alone do not establish hint-table validity or web optimization.</summary>
public sealed class PdfLinearizationInfo
{
    internal PdfLinearizationInfo(long length, long hintOffset, long hintLength, long firstPageEnd, int firstObject, int pages, long mainXref, int firstPage)
    {
        FileLength = length;
        HintOffset = hintOffset;
        HintLength = hintLength;
        FirstPageEnd = firstPageEnd;
        FirstPageObjectNumber = firstObject;
        PageCount = pages;
        MainCrossReferenceOffset = mainXref;
        FirstPageIndex = firstPage;
    }

    /// <summary>Gets the exact declared file length.</summary>
    public long FileLength { get; }
    /// <summary>Gets the primary hint stream object's byte offset.</summary>
    public long HintOffset { get; }
    /// <summary>Gets the primary hint stream object's declared byte extent.</summary>
    public long HintLength { get; }
    /// <summary>Gets the declared end of the first-page section.</summary>
    public long FirstPageEnd { get; }
    /// <summary>Gets the first displayed page object number.</summary>
    public int FirstPageObjectNumber { get; }
    /// <summary>Gets the declared page count.</summary>
    public int PageCount { get; }
    /// <summary>Gets the declared main cross-reference entry offset.</summary>
    public long MainCrossReferenceOffset { get; }
    /// <summary>Gets the declared zero-based first displayed page index.</summary>
    public int FirstPageIndex { get; }
}

/// <summary>Independent complete linearization verdict bound to exact candidate and request bytes.</summary>
public sealed class PdfLinearizationValidation
{
    /// <summary>Captures bounded findings. A valid observation requires completed checking and zero findings; this does not qualify a production engine.</summary>
    public PdfLinearizationValidation(string sourceSha256, string requestSha256, bool completed, bool isValid, IEnumerable<string> findings)
    {
        Hash(sourceSha256, nameof(sourceSha256));
        Hash(requestSha256, nameof(requestSha256));
        if (findings == null)
            throw new ArgumentNullException(nameof(findings));
        var items = new List<string>();
        foreach (string item in findings)
        {
            if (items.Count == 10000)
                throw new ArgumentException("At most10000 findings are supported.", nameof(findings));
            Guard.Text(item, nameof(findings), 8192);
            items.Add(item);
        }

        if (isValid && (!completed || items.Count != 0))
            throw new ArgumentException("Validity requires complete checking and zero findings.", nameof(isValid));
        SourceSha256 = sourceSha256;
        RequestSha256 = requestSha256;
        Completed = completed;
        IsValid = isValid;
        Findings = new ReadOnlyCollection<string>(items);
    }

    /// <summary>Gets the exact validated PDF digest.</summary>
    public string SourceSha256 { get; }
    /// <summary>Gets the SDK-supplied request digest.</summary>
    public string RequestSha256 { get; }
    /// <summary>Gets whether every applicable offset and hint rule was checked.</summary>
    public bool Completed { get; }
    /// <summary>Gets the independent validity observation.</summary>
    public bool IsValid { get; }
    /// <summary>Gets the immutable rule findings.</summary>
    public IReadOnlyList<string> Findings { get; }
    /// <summary>Gets the completed public operation report, or null for a provider-only result.</summary>
    public PdfOperationReport? OperationReport { get; internal set; }

    private static void Hash(string value, string name)
    {
        if (value == null || value.Length != 64 || value.Any(ch => !(ch >= '0' && ch <= '9' || ch >= 'a' && ch <= 'f')))
            throw new ArgumentException("An exact lowercase SHA256 is required.", name);
    }
}

/// <summary>Explicit offline caller-owned linearization engine with a pinned identity and qualified bounded isolation for native implementations.</summary>
public interface IPdfLinearizationProducer
{
    /// <summary>Gets the nonsecret pinned ASCII engine/configuration identity.</summary>
    string EngineId { get; }

    /// <summary>Checks exact settings and explicit local assets before input capture.</summary>
    bool Supports(PdfLinearizationOptions options);
    /// <summary>Rebuilds all linearization offsets and hints for exact output bytes, retaining document semantics. Uses the mandatory shared context; borrowed leases expire on return.</summary>
    void Linearize(Stream source, Stream candidate, PdfLinearizationOptions options, PdfOperationContext context);
}

/// <summary>Separately configured complete independent hint/offset validator. A marker check or the producer under another name is insufficient.</summary>
public interface IPdfLinearizationValidator
{
    /// <summary>Gets a pinned nonsecret identity distinct from the producer.</summary>
    string EngineId { get; }

    /// <summary>Checks validation availability before source capture.</summary>
    bool Supports(PdfLinearizationOptions options);
    /// <summary>Validates all linearization rules and binds its report to exact bytes and the supplied request digest, without downloads or implicit network access.</summary>
    PdfLinearizationValidation Validate(Stream candidate, PdfLinearizationOptions options, string requestSha256, PdfOperationContext context);
}
