using System.Collections.Generic;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class ProviderTests
{
    [Fact]
    public void DescriptorsAndRegistryDefensivelySnapshotConfiguration()
    {
        var capabilities = new[] { PdfCapability.RenderHtml };
        var descriptor = new PdfProviderDescriptor("web.local", "1.0", capabilities);
        capabilities[0] = PdfCapability.RecognizeText;
        var provider = new ServiceProvider(descriptor);
        IPdfProvider[] array = { provider };
        var registry = new PdfProviderRegistry(array);
        array[0] = new ServiceProvider(new PdfProviderDescriptor("different", "2", PdfCapability.RecognizeText));
        Assert.True(registry.Supports(PdfCapability.RenderHtml));
        Assert.False(registry.Supports(PdfCapability.RecognizeText));
        Assert.Same(provider, registry.GetRequired<IPdfProvider>(PdfCapability.RenderHtml));
        Assert.Equal("web.local", Assert.Single(registry.Descriptors).Id);
        Assert.Throws<NotSupportedException>(() => ((IList<PdfCapability>)descriptor.Capabilities).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PdfCapability>)registry.Capabilities).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PdfProviderDescriptor>)registry.Descriptors).Clear());
    }

    [Fact]
    public void InvalidRegistrationsAreRejectedRatherThanSelectingAnImplicitFallback()
    {
        Assert.Throws<ArgumentNullException>(() => new PdfProviderRegistry(null!));
        Assert.Throws<ArgumentException>(() => new PdfProviderRegistry(new IPdfProvider[] { null! }));
        Assert.Throws<ArgumentException>(() => new PdfProviderRegistry(new IPdfProvider[65]));
        Assert.Throws<ArgumentException>(() => new PdfProviderDescriptor("", "1", PdfCapability.RenderHtml));
        Assert.Throws<ArgumentException>(() => new PdfProviderDescriptor("web", " "));
        Assert.Throws<ArgumentException>(() => new PdfProviderDescriptor("web", "1"));
        Assert.Throws<ArgumentNullException>(() => new PdfProviderDescriptor("web", "1", null!));
        Assert.Throws<ArgumentException>(() => new PdfProviderDescriptor("web", "1", PdfCapability.RenderHtml, PdfCapability.RenderHtml));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfProviderDescriptor("web", "1", (PdfCapability)999));
        Assert.Throws<ArgumentException>(() => new PdfProviderRegistry(new ServiceProvider(new PdfProviderDescriptor("invalid", "1", PdfCapability.ReadDocument))));
        var first = new ServiceProvider(new PdfProviderDescriptor("web", "1", PdfCapability.RenderHtml));
        var duplicateIdentity = new ServiceProvider(new PdfProviderDescriptor("WEB", "2", PdfCapability.RecognizeText));
        var duplicateService = new ServiceProvider(new PdfProviderDescriptor("other", "1", PdfCapability.RenderHtml));
        Assert.Throws<ArgumentException>(() => new PdfProviderRegistry(first, duplicateIdentity));
        Assert.Throws<ArgumentException>(() => new PdfProviderRegistry(first, duplicateService));
    }

    [Fact]
    public void MissingAndMismatchedServicesHaveStructuredDiagnostics()
    {
        var absent = Assert.Throws<PdfProviderUnavailableException>(() => PdfProviderRegistry.Empty.GetRequired<IPdfProvider>(PdfCapability.RecognizeText));
        Assert.Equal("Provider.Unavailable", absent.Diagnostic.Code);
        Assert.Equal(PdfCapability.RecognizeText, absent.Diagnostic.Capability);
        Assert.Null(absent.Diagnostic.ProviderId);
        var registry = new PdfProviderRegistry(new ServiceProvider(new PdfProviderDescriptor("web", "1", PdfCapability.RenderHtml)));
        var mismatch = Assert.Throws<PdfException>(() => registry.GetRequired<IPdfDocumentReader>(PdfCapability.RenderHtml));
        Assert.Equal("Provider.ContractMismatch", mismatch.Diagnostic.Code);
        Assert.Equal("web", mismatch.Diagnostic.ProviderId);
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.Supports((PdfCapability)(-1)));
        Assert.Throws<ArgumentOutOfRangeException>(() => registry.GetRequired<IPdfProvider>((PdfCapability)99));
    }

    [Fact]
    public void DiagnosticsRetainIdentityAndInnerFailureWithoutGlobalState()
    {
        var diagnostic = new PdfDiagnostic("Input.Invalid", "Invalid contract fixture.", PdfCapability.ReadDocument, "local");
        var inner = new InvalidOperationException("test failure");
        var failure = new PdfException(diagnostic, inner);
        Assert.Same(diagnostic, failure.Diagnostic);
        Assert.Same(inner, failure.InnerException);
        Assert.Equal(diagnostic.Message, failure.Message);
        Assert.Throws<ArgumentNullException>(() => new PdfException(null!));
        Assert.Throws<ArgumentException>(() => new PdfDiagnostic(" ", "message"));
        Assert.Throws<ArgumentException>(() => new PdfDiagnostic("code", new string('x', 2049)));
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfProviderUnavailableException((PdfCapability)99));
    }

    [Fact]
    public void DocumentsDoNotOwnSharedProviders()
    {
        using var provider = new ContractProvider();
        var registry = new PdfProviderRegistry(provider);
        var first = Pdf.Create(providers: registry);
        var second = Pdf.Create(providers: registry);
        first.Dispose();
        Assert.False(provider.Disposed);
        second.AddPage();
        Assert.Single(second.Pages);
        second.Dispose();
        Assert.False(provider.Disposed);
    }
}
