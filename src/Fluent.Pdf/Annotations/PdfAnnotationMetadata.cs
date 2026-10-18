using System.Globalization;

namespace Fluent.Pdf;
/// <summary>Immutable comment metadata and stable reply identity. Dates retain their explicit offsets.</summary>
public sealed class PdfAnnotationMetadata
{
    /// <summary>Captures bounded Unicode fields, dates and an optional reply target identifier in the destination document.</summary>
    public PdfAnnotationMetadata(string contents = "", string author = "", string subject = "", DateTimeOffset? created = null, DateTimeOffset? modified = null, string? state = null, string? stateModel = null, string? replyTo = null, bool groupReply = false)
    {
        foreach (string? value in new[]
        {
            contents,
            author,
            subject,
            state,
            stateModel,
            replyTo
        }

        )
            if (value != null && value.Length > 1000000)
                throw new ArgumentOutOfRangeException(nameof(contents));
        Contents = contents ?? throw new ArgumentNullException(nameof(contents));
        Author = author ?? throw new ArgumentNullException(nameof(author));
        Subject = subject ?? throw new ArgumentNullException(nameof(subject));
        Created = created;
        Modified = modified;
        State = state;
        StateModel = stateModel;
        ReplyTo = replyTo;
        GroupReply = groupReply;
        if (state != null && stateModel == null || groupReply && replyTo == null)
            throw new ArgumentException("States and grouped replies require their associated model or reply identity.");
        if (replyTo != null && (replyTo.Length == 0 || replyTo.Length > 4096))
            throw new ArgumentException("Reply identifiers must be bounded and nonempty.", nameof(replyTo));
    }

    /// <summary>Gets logical comment contents.</summary>
    public string Contents { get; }
    /// <summary>Gets the author label.</summary>
    public string Author { get; }
    /// <summary>Gets the subject.</summary>
    public string Subject { get; }
    /// <summary>Gets the creation instant and offset.</summary>
    public DateTimeOffset? Created { get; }
    /// <summary>Gets the modification instant and offset.</summary>
    public DateTimeOffset? Modified { get; }
    /// <summary>Gets the optional state text.</summary>
    public string? State { get; }
    /// <summary>Gets the optional state-model text.</summary>
    public string? StateModel { get; }
    /// <summary>Gets an existing or same-batch target's stable annotation identifier.</summary>
    public string? ReplyTo { get; }
    /// <summary>Gets whether the reply is grouped rather than an ordinary reply.</summary>
    public bool GroupReply { get; }

    internal void Apply(Dictionary<string, PdfValue> entries, PdfOperationContext context)
    {
        entries["Contents"] = PdfTextStrings.Encode(Contents, context);
        entries["T"] = PdfTextStrings.Encode(Author, context);
        entries["Subj"] = PdfTextStrings.Encode(Subject, context);
        if (Created.HasValue)
            entries["CreationDate"] = PdfTextStrings.Encode(Date(Created.Value), context);
        if (Modified.HasValue)
            entries["M"] = PdfTextStrings.Encode(Date(Modified.Value), context);
        if (State != null)
            entries["State"] = PdfTextStrings.Encode(State, context);
        if (StateModel != null)
            entries["StateModel"] = PdfTextStrings.Encode(StateModel, context);
        if (ReplyTo != null)
            entries["RT"] = PdfValue.Name(GroupReply ? "Group" : "R");
    }

    private static string Date(DateTimeOffset date)
    {
        int minutes = (int)date.Offset.TotalMinutes;
        string zone = minutes == 0 ? "Z" : (minutes < 0 ? "-" : "+") + (Math.Abs(minutes) / 60).ToString("00", CultureInfo.InvariantCulture) + "'" + (Math.Abs(minutes) % 60).ToString("00", CultureInfo.InvariantCulture) + "'";
        return "D:" + date.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture) + zone;
    }
}
