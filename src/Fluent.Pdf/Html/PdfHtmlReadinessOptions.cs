using System.Collections.ObjectModel;

namespace Fluent.Pdf;
/// <summary>Explicit CSS media for browser document preparation and printing.</summary>
public enum PdfHtmlMedia
{
    /// <summary>Use print media.</summary>
    Print,
    /// <summary>Use screen media.</summary>
    Screen
}

/// <summary>Immutable bounded main-document readiness policy. Caller JavaScript is explicit and requires source script execution to be enabled.</summary>
public sealed class PdfHtmlReadinessOptions
{
    /// <summary>Captures finite timeout, explicit CSS selectors/promise, native asset checks and observed network quietness. Operation limits can reject earlier.</summary>
    public PdfHtmlReadinessOptions(TimeSpan? timeout = null, IEnumerable<string>? selectors = null, string? javaScriptPromise = null, bool waitForDocumentComplete = true, bool waitForFonts = true, bool waitForImages = true, TimeSpan? networkQuietPeriod = null, int maximumInFlightRequests = 0, TimeSpan? pollingInterval = null)
    {
        Timeout = timeout ?? TimeSpan.FromSeconds(30);
        NetworkQuietPeriod = networkQuietPeriod ?? TimeSpan.FromMilliseconds(500);
        PollingInterval = pollingInterval ?? TimeSpan.FromMilliseconds(50);
        if (Timeout < TimeSpan.FromMilliseconds(1) || Timeout > TimeSpan.FromMinutes(5))
            throw new ArgumentOutOfRangeException(nameof(timeout));
        if (NetworkQuietPeriod < TimeSpan.Zero || NetworkQuietPeriod > TimeSpan.FromSeconds(5))
            throw new ArgumentOutOfRangeException(nameof(networkQuietPeriod));
        if (PollingInterval < TimeSpan.FromMilliseconds(10) || PollingInterval > TimeSpan.FromSeconds(1))
            throw new ArgumentOutOfRangeException(nameof(pollingInterval));
        if (maximumInFlightRequests < 0 || maximumInFlightRequests > 64)
            throw new ArgumentOutOfRangeException(nameof(maximumInFlightRequests));
        if (javaScriptPromise != null && (javaScriptPromise.Length == 0 || javaScriptPromise.Length > 16384 || !Unicode(javaScriptPromise)))
            throw new ArgumentException("An explicit bounded well-formed JavaScript promise expression is required.", nameof(javaScriptPromise));
        var captured = new List<string>();
        int length = 0;
        if (selectors != null)
            foreach (string selector in selectors)
            {
                if (captured.Count == 64 || string.IsNullOrEmpty(selector) || selector.Length > 1024 || !Unicode(selector) || (length += selector.Length) > 16384)
                    throw new ArgumentException("At most 64 bounded nonempty selectors with at most 16 Ki UTF-16 units total are permitted.", nameof(selectors));
                captured.Add(selector);
            }

        Selectors = new ReadOnlyCollection<string>(captured);
        JavaScriptPromise = javaScriptPromise;
        WaitForDocumentComplete = waitForDocumentComplete;
        WaitForFonts = waitForFonts;
        WaitForImages = waitForImages;
        MaximumInFlightRequests = maximumInFlightRequests;
    }

    /// <summary>Gets the shared immutable finite default.</summary>
    public static PdfHtmlReadinessOptions Default { get; } = new();
    /// <summary>Gets the phase timeout, further bounded by the operation/outer deadlines.</summary>
    public TimeSpan Timeout { get; }
    /// <summary>Gets selectors which must each match a main-document element. Selector errors reject without echoing their source.</summary>
    public IReadOnlyList<string> Selectors { get; }
    /// <summary>Gets an explicitly trusted main-world JavaScript expression whose promise must resolve, or null. No document expression is discovered or executed implicitly.</summary>
    public string? JavaScriptPromise { get; }
    /// <summary>Gets whether the main document must reach complete readyState.</summary>
    public bool WaitForDocumentComplete { get; }
    /// <summary>Gets whether declared fonts must stop loading. Known failures or explicitly skipped pending fonts remain reported losses.</summary>
    public bool WaitForFonts { get; }
    /// <summary>Gets whether main-document images must complete. Known failures or explicitly skipped pending images remain reported losses.</summary>
    public bool WaitForImages { get; }
    /// <summary>Gets how long observed HTTP activity must remain at or below the configured in-flight threshold; zero disables this additional wait.</summary>
    public TimeSpan NetworkQuietPeriod { get; }
    /// <summary>Gets the permitted number of observed pending requests during the quiet interval.</summary>
    public int MaximumInFlightRequests { get; }
    /// <summary>Gets the cooperative DOM/asset inspection interval.</summary>
    public TimeSpan PollingInterval { get; }

    private static bool Unicode(string text)
    {
        for (int i = 0; i < text.Length; i++)
            if (char.IsHighSurrogate(text[i]))
            {
                if (++i == text.Length || !char.IsLowSurrogate(text[i]))
                    return false;
            }
            else if (char.IsLowSurrogate(text[i]) || text[i] == '\0')
                return false;
        return true;
    }
}
