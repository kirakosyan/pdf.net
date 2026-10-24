using Xunit;

namespace Fluent.Pdf.Contract.Tests;

// Keep unrelated process-heavy classes out of this fixture's bounded native deadlines.
// Tests still exercise their explicit concurrent workers and retain every timeout assertion.
[CollectionDefinition("Native process stress", DisableParallelization = true)]
public sealed class NativeProcessStressCollection
{
}
