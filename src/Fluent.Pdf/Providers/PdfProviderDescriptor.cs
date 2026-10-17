using System.Collections.ObjectModel;

namespace Fluent.Pdf;

/// <summary>Immutable identity and advertised services captured at registration.</summary>
public sealed class PdfProviderDescriptor
{
    /// <summary>Creates a descriptor with identifiers of at most 128 characters and a unique, nonempty capability list.</summary>
    public PdfProviderDescriptor(string id, string version, params PdfCapability[] capabilities)
    {
        Id = Guard.Text(id, nameof(id), 128);
        Version = Guard.Text(version, nameof(version), 128);
        if (capabilities is null) throw new ArgumentNullException(nameof(capabilities));
        if (capabilities.Length == 0 || capabilities.Length > Enum.GetValues(typeof(PdfCapability)).Length)
            throw new ArgumentException("Specify a nonempty unique capability list.", nameof(capabilities));
        var seen = new HashSet<PdfCapability>();
        var copy = new PdfCapability[capabilities.Length];
        for (int i = 0; i < capabilities.Length; i++)
        {
            Guard.Capability(capabilities[i]);
            if (!seen.Add(capabilities[i])) throw new ArgumentException("A capability is repeated.", nameof(capabilities));
            copy[i] = capabilities[i];
        }
        Capabilities = new ReadOnlyCollection<PdfCapability>(copy);
    }

    /// <summary>Gets the provider identifier; registration compares identifiers ordinally without case.</summary>
    public string Id { get; }
    /// <summary>Gets the explicitly supplied provider version, without resolving or downloading an engine.</summary>
    public string Version { get; }
    /// <summary>Gets a defensive, read-only capability snapshot.</summary>
    public IReadOnlyList<PdfCapability> Capabilities { get; }
}
