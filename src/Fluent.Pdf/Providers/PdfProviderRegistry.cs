using System.Collections.ObjectModel;

namespace Fluent.Pdf;

/// <summary>An immutable, explicit registry with at most 64 caller-owned provider instances.</summary>
/// <remarks>Duplicate identities/services are rejected. There is no global discovery, automatic download or implicit fallback.</remarks>
public sealed class PdfProviderRegistry
{
    private readonly Dictionary<PdfCapability, IPdfProvider> _providers = new();
    private readonly Dictionary<PdfCapability, string> _identifiers = new();

    /// <summary>Captures provider identities/capabilities; the supplied array is not retained.</summary>
    public PdfProviderRegistry(params IPdfProvider[] providers)
    {
        if (providers is null) throw new ArgumentNullException(nameof(providers));
        if (providers.Length > 64) throw new ArgumentException("At most 64 providers may be registered.", nameof(providers));
        var identities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var descriptors = new List<PdfProviderDescriptor>(providers.Length);
        foreach (var provider in providers)
        {
            if (provider is null) throw new ArgumentException("Providers cannot contain null.", nameof(providers));
            var descriptor = provider.Descriptor ?? throw new ArgumentException("A provider has no descriptor.", nameof(providers));
            if (!identities.Add(descriptor.Id)) throw new ArgumentException("Provider identities must be unique.", nameof(providers));
            foreach (var capability in descriptor.Capabilities)
            {
                if (_providers.ContainsKey(capability)) throw new ArgumentException("Only one provider may supply a capability.", nameof(providers));
                if (capability == PdfCapability.ReadDocument && provider is not IPdfDocumentReader ||
                    capability == PdfCapability.WriteDocument && provider is not IPdfDocumentWriter ||
                    capability == PdfCapability.ImageCodecs && provider is not IPdfImageCodecProvider ||
                    capability == PdfCapability.Fonts && provider is not IPdfFontProvider ||
                    capability == PdfCapability.ShapeText && provider is not IPdfTextShaper)
                    throw new ArgumentException("A provider does not implement the interface for its declared service.", nameof(providers));
                _providers.Add(capability, provider);
                _identifiers.Add(capability, descriptor.Id);
            }
            descriptors.Add(descriptor);
        }
        Descriptors = new ReadOnlyCollection<PdfProviderDescriptor>(descriptors);
        var capabilities = new List<PdfCapability>(_providers.Keys);
        capabilities.Sort();
        Capabilities = new ReadOnlyCollection<PdfCapability>(capabilities);
    }

    /// <summary>Gets the shared empty immutable registry.</summary>
    public static PdfProviderRegistry Empty { get; } = new();
    /// <summary>Gets the shared original managed reader/writer registry. Optional native/image services remain explicitly configured.</summary>
    public static PdfProviderRegistry Default{get;}=new(ManagedPdfProvider.Instance);
    /// <summary>Gets captured provider metadata in registration order.</summary>
    public IReadOnlyList<PdfProviderDescriptor> Descriptors { get; }
    /// <summary>Gets the declared capability inventory in enum order.</summary>
    public IReadOnlyList<PdfCapability> Capabilities { get; }

    internal string GetIdentifier(PdfCapability capability) => _identifiers[capability];

    /// <summary>Reports whether a capability has an explicitly registered provider.</summary>
    public bool Supports(PdfCapability capability)
    {
        Guard.Capability(capability);
        return _providers.ContainsKey(capability);
    }

    /// <summary>Resolves a declared capability and required interface, or throws a structured failure.</summary>
    /// <exception cref="PdfProviderUnavailableException">The capability has no registered provider.</exception>
    public T GetRequired<T>(PdfCapability capability) where T : class, IPdfProvider
    {
        Guard.Capability(capability);
        if (!_providers.TryGetValue(capability, out var provider)) throw new PdfProviderUnavailableException(capability);
        if (provider is T typed) return typed;
        throw new PdfException(new PdfDiagnostic("Provider.ContractMismatch",
            "The registered provider does not implement the requested service contract.", capability, _identifiers[capability]));
    }
}
