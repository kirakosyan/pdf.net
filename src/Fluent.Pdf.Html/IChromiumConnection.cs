namespace Fluent.Pdf.Html;

/// <summary>A caller-provisioned, privately owned DevTools connection for one supervised browser job. Creating a connection must never reuse a prior job's process/profile/credentials.</summary>
public interface IChromiumConnection : IDisposable
{
    /// <summary>Sends one complete JSON DevTools message. Implementations must bound/charge transport allocations and honor the supplied cancellation/deadline.</summary>
    void Send(string message, PdfOperationContext context);
    /// <summary>Receives one complete JSON DevTools message, at most 64 MiB of characters, charging before allocation. EOF/process failure must throw; never return an empty fabricated message.</summary>
    string Receive(PdfOperationContext context);
}

/// <summary>Opens one private, explicitly provisioned Chromium process/transport. The host must enforce offline network, read-only pinned assets/fonts, memory/process/time limits and termination within five seconds; Dispose must join all owned work even after a latched failure. No SDK engine download or host discovery occurs.</summary>
public delegate IChromiumConnection ChromiumConnectionFactory(PdfOperationContext context);
