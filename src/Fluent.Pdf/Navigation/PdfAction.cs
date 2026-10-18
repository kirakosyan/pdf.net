namespace Fluent.Pdf;
/// <summary>A passive immutable action description. Creating or inspecting it never executes it.</summary>
public sealed class PdfAction
{
    private readonly PdfDestination? _destination;
    private readonly string? _namedDestination;
    private readonly PdfValue _definition;
    private readonly PdfAction[] _next;
    private PdfAction(PdfValue definition, PdfDestination? destination = null, string? namedDestination = null, PdfAction[]? next = null)
    {
        _definition = definition;
        _destination = destination;
        _namedDestination = namedDestination;
        _next = next ?? System.Array.Empty<PdfAction>();
    }

    /// <summary>Creates a local page action.</summary>
    public static PdfAction GoTo(PdfDestination destination) => new(PdfGraphicsNumbers.Dict(("S", PdfValue.Name("GoTo"))), destination ?? throw new ArgumentNullException(nameof(destination)));
    /// <summary>Creates a local named destination action.</summary>
    public static PdfAction GoToNamed(string name) => new(PdfGraphicsNumbers.Dict(("S", PdfValue.Name("GoTo"))), namedDestination: string.IsNullOrEmpty(name) || name.Length > 8192 ? throw new ArgumentException("A nonempty bounded destination name is required.", nameof(name)) : name);
    /// <summary>Creates an absolute URI action; a separate policy controls allowed schemes.</summary>
    public static PdfAction Uri(string uri)
    {
        Text(uri, nameof(uri));
        if (!System.Uri.TryCreate(uri, UriKind.Absolute, out var parsed))
            throw new ArgumentException("An absolute URI is required.", nameof(uri));
        return new(PdfGraphicsNumbers.Dict(("S", PdfValue.Name("URI")), ("URI", Ascii(parsed.AbsoluteUri))));
    }

    /// <summary>Creates a remote-file named destination action, requiring explicit policy permission.</summary>
    public static PdfAction Remote(string fileName, string destinationName, bool newWindow = false) => new(PdfGraphicsNumbers.Dict(("S", PdfValue.Name("GoToR")), ("F", Unicode(Text(fileName, nameof(fileName)))), ("D", Unicode(Text(destinationName, nameof(destinationName)))), ("NewWindow", PdfValue.Boolean(newWindow))));
    /// <summary>Creates a launch-file action, requiring explicit policy permission. No process is started.</summary>
    public static PdfAction Launch(string fileName, bool newWindow = false) => new(PdfGraphicsNumbers.Dict(("S", PdfValue.Name("Launch")), ("F", Unicode(Text(fileName, nameof(fileName)))), ("NewWindow", PdfValue.Boolean(newWindow))));
    /// <summary>Captures an advanced detached action dictionary. It remains subject to policy and target validation.</summary>
    public static PdfAction Advanced(PdfValue definition)
    {
        if (definition == null)
            throw new ArgumentNullException(nameof(definition));
        if (definition.Kind != PdfValueKind.Dictionary || !definition.Entries.TryGetValue("S", out var subtype) || subtype.Kind != PdfValueKind.Name || definition.Entries.ContainsKey("Next"))
            throw new ArgumentException("A detached action dictionary with S and without Next is required.", nameof(definition));
        return new(definition);
    }

    /// <summary>Returns an action followed by a finite ordered batch. Instances are immutable.</summary>
    public PdfAction Then(params PdfAction[] actions)
    {
        if (actions == null || actions.Length > 4096 || actions.Any(a => a == null))
            throw new ArgumentException("Supply up to 4096 nonnull actions.", nameof(actions));
        return new(_definition, _destination, _namedDestination, (PdfAction[])actions.Clone());
    }

    internal PdfValue Materialize(PdfNavigationReader reader, PdfActionPolicy policy)
    {
        using var depth = reader.Context.EnterScope(PdfResourceKind.Depth);
        reader.Context.Checkpoint();
        reader.Context.Consume(PdfResourceKind.Objects, 1);
        PdfGraphImport.ValidateDetached(_definition, reader.Context);
        var values = PdfWriterCore.CopyDictionary(_definition.Entries, reader.Context);
        if (_destination != null)
            values["D"] = _destination.Materialize(reader.Pages, reader.Context);
        if (_namedDestination != null)
        {
            var matches = reader.NamedDestinations().Where(p => reader.Text(p.Key) == _namedDestination).ToArray();
            if (matches.Length != 1)
                throw reader.Error("Navigation.NamedDestination", "The named destination is missing or ambiguous.");
            values["D"] = matches[0].Key;
        }

        var definition = PdfValue.OwnedDictionary(values);
        if (!policy.Allows(definition, reader))
            throw reader.Error("Navigation.ActionPolicy", "The requested action is blocked by the explicit action policy.");
        if (_next.Length > 0)
        {
            reader.Context.Consume(PdfResourceKind.ExpandedBytes, _next.Length * 32L);
            values["Next"] = PdfValue.OwnedArray(_next.Select(a => a.Materialize(reader, policy)).ToList());
        }

        return new PdfResourceWriter(reader.Edit, reader.Context, new()).EmbedValue(PdfValue.OwnedDictionary(values));
    }

    private static string Text(string text, string parameter) => string.IsNullOrEmpty(text) || text.Length > 8192 || text.Any(c => c == 0 || char.IsControl(c)) ? throw new ArgumentException("A nonempty bounded string without controls is required.", parameter) : text;
    private static PdfValue Ascii(string text) => PdfValue.String(System.Text.Encoding.ASCII.GetBytes(text));
    private static PdfValue Unicode(string text) => PdfValue.String(new byte[] { 254, 255 }.Concat(System.Text.Encoding.BigEndianUnicode.GetBytes(text)).ToArray());
}

/// <summary>Explicit permission for emitted and retained actions. Permissions never execute actions or fetch resources.</summary>
public sealed class PdfActionPolicy
{
    /// <summary>Creates a policy. Default permissions allow local navigation and absolute HTTP/HTTPS URI actions only.</summary>
    public PdfActionPolicy(bool allowLocalGoTo = true, bool allowRemote = false, bool allowLaunch = false, bool allowJavaScript = false, bool allowOtherActions = false, bool allowUri = true, params string[] allowedUriSchemes)
    {
        if (allowedUriSchemes == null || allowedUriSchemes.Length > 64 || allowedUriSchemes.Any(s => string.IsNullOrEmpty(s) || !System.Uri.CheckSchemeName(s)))
            throw new ArgumentException("Supply up to 64 URI schemes.", nameof(allowedUriSchemes));
        AllowLocalGoTo = allowLocalGoTo;
        AllowRemote = allowRemote;
        AllowLaunch = allowLaunch;
        AllowJavaScript = allowJavaScript;
        AllowOtherActions = allowOtherActions;
        AllowUri = allowUri;
        AllowedUriSchemes = System.Array.AsReadOnly((allowedUriSchemes.Length == 0 ? new[] { "http", "https" } : allowedUriSchemes).Select(s => s.ToLowerInvariant()).Distinct(StringComparer.Ordinal).ToArray());
    }

    /// <summary>Gets local navigation permission.</summary>
    public bool AllowLocalGoTo { get; }
    /// <summary>Gets remote-file navigation permission.</summary>
    public bool AllowRemote { get; }
    /// <summary>Gets file launch permission.</summary>
    public bool AllowLaunch { get; }
    /// <summary>Gets passive script action retention permission.</summary>
    public bool AllowJavaScript { get; }
    /// <summary>Gets retention permission for other action types and opaque active content.</summary>
    public bool AllowOtherActions { get; }
    /// <summary>Gets absolute URI action permission.</summary>
    public bool AllowUri { get; }
    /// <summary>Gets the allowed absolute URI schemes. Relative URIs are rejected.</summary>
    public IReadOnlyList<string> AllowedUriSchemes { get; }

    internal bool Allows(PdfValue value, PdfNavigationReader reader)
    {
        value = reader.Resolve(value);
        if (value.Kind != PdfValueKind.Dictionary)
            return false;
        var s = reader.Get(value, "S");
        if (s.Kind != PdfValueKind.Name)
            return false;
        switch (s.NameValue)
        {
            case "GoTo":
                return AllowLocalGoTo && value.Entries.ContainsKey("D");
            case "GoToR":
            case "GoToE":
                return AllowRemote && value.Entries.ContainsKey("F") && value.Entries.ContainsKey("D");
            case "Launch":
                return AllowLaunch && value.Entries.ContainsKey("F");
            case "JavaScript":
                return AllowJavaScript && value.Entries.ContainsKey("JS");
            case "URI":
                var uri = reader.Get(value, "URI");
                if (!AllowUri || uri.Kind != PdfValueKind.String || uri.StringBytes.Count > 8192 || uri.StringBytes.Any(b => b < 32 || b > 126))
                    return false;
                reader.Context.Consume(PdfResourceKind.ExpandedBytes, uri.StringBytes.Count * 4L + 32);
                string text = System.Text.Encoding.ASCII.GetString(uri.StringBytes.ToArray());
                return System.Uri.TryCreate(text, UriKind.Absolute, out var parsed) && AllowedUriSchemes.Contains(parsed.Scheme, StringComparer.OrdinalIgnoreCase);
            default:
                return AllowOtherActions;
        }
    }
}
