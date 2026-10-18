namespace Fluent.Pdf;
/// <summary>Explicit handling of navigation destinations excluded by a selection.</summary>
public enum PdfMissingDestinationPolicy
{
    /// <summary>Reject before producing an incomplete destination.</summary>
    Reject,
    /// <summary>Omit the affected navigation entry and report the loss.</summary>
    OmitWithDiagnostic
}

/// <summary>Explicit handling of colliding root form-field names.</summary>
public enum PdfFieldCollisionPolicy
{
    /// <summary>Reject ambiguous field identities.</summary>
    Reject,
    /// <summary>Prefix the colliding root partial name with a deterministic source namespace.</summary>
    Rename
}

/// <summary>Immutable organization choices. Encryption requires an explicit decryption provider before import.</summary>
public sealed class PdfOrganizationOptions
{
    /// <summary>Captures finite page limits and explicit signature, navigation and form-field collision policies.</summary>
    public PdfOrganizationOptions(bool allowSignatureInvalidation = false, PdfMissingDestinationPolicy missingDestinations = PdfMissingDestinationPolicy.Reject, PdfFieldCollisionPolicy fieldCollisions = PdfFieldCollisionPolicy.Reject, int maximumPages = 100000)
    {
        if (!Enum.IsDefined(typeof(PdfMissingDestinationPolicy), missingDestinations))
            throw new ArgumentOutOfRangeException(nameof(missingDestinations));
        if (!Enum.IsDefined(typeof(PdfFieldCollisionPolicy), fieldCollisions))
            throw new ArgumentOutOfRangeException(nameof(fieldCollisions));
        if (maximumPages < 1 || maximumPages > 1000000)
            throw new ArgumentOutOfRangeException(nameof(maximumPages));
        AllowSignatureInvalidation = allowSignatureInvalidation;
        MissingDestinations = missingDestinations;
        FieldCollisions = fieldCollisions;
        MaximumPages = maximumPages;
    }

    /// <summary>Gets explicit authorization to invalidate source signatures while rewriting.</summary>
    public bool AllowSignatureInvalidation { get; }
    /// <summary>Gets the omitted-target navigation policy.</summary>
    public PdfMissingDestinationPolicy MissingDestinations { get; }
    /// <summary>Gets the root form-field collision policy.</summary>
    public PdfFieldCollisionPolicy FieldCollisions { get; }
    /// <summary>Gets the finite output page ceiling.</summary>
    public int MaximumPages { get; }
}

/// <summary>A detached document snapshot and an optional ordered selection.</summary>
public sealed class PdfPageInput
{
    /// <summary>Captures a snapshot and optional selection; null selects all pages in source order.</summary>
    public PdfPageInput(PdfDocumentSnapshot document, PdfPageSelection? selection = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        if (document.Pages.Count == 0)
            throw new ArgumentException("An input document must have pages.", nameof(document));
        if (selection != null && selection.Indices.Any(i => i >= document.Pages.Count))
            throw new ArgumentException("The selection exceeds this document.", nameof(selection));
        Selection = selection;
    }

    /// <summary>Gets the detached input snapshot.</summary>
    public PdfDocumentSnapshot Document { get; }
    /// <summary>Gets the optional ordered physical selection.</summary>
    public PdfPageSelection? Selection { get; }
}
