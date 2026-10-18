using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;
public sealed class PolicyTests
{
    [Fact]
    public void FailureDetailsSurviveLatchedRethrowAndOperationCompletion()
    {
        var details = new PdfArchivalValidation(PdfArchivalProfile.A2B, new string('0', 64), true, false, new[] { new PdfArchivalFinding("original.rule", "original.location", "Original bounded finding.") });
        PdfException? original = null;
        var failure = Assert.Throws<PdfException>(() => PdfFont.Run("FailureDetails", "fixture.details", null, default, context =>
        {
            original = Assert.Throws<PdfException>(() => context.ReportFailure(new("Fixture.Details", "Original bounded details."), "fixture.details", details));
            Assert.Same(details, original.Data["fixture.details"]);
            var repeated = Assert.Throws<PdfException>(context.Checkpoint);
            Assert.Same(original, repeated);
            Assert.Same(details, repeated.Data["fixture.details"]);
        }));
        Assert.Same(original, failure);
        Assert.Same(details, failure.Data["fixture.details"]);
    }

    [Fact]
    public void TemporaryReservationsReleaseMemoryAndAllowOverlappingObjectWork()
    {
        var report = Run(context =>
        {
            for (int i = 0; i < 50; i++)
                using (context.Reserve(PdfResourceKind.ExpandedBytes, 80))
                    context.Consume(PdfResourceKind.ExpandedBytes, 1);
            using var first = context.Reserve(PdfResourceKind.Objects, 2);
            System.Threading.Tasks.Task.Run(() =>
            {
                using var second = context.Reserve(PdfResourceKind.Objects, 2);
                context.Consume(PdfResourceKind.Objects, 1);
                second.Complete(1);
            }).GetAwaiter().GetResult();
            first.Complete(2);
        }, new PdfResourceLimits(maximumExpandedBytes: 130, maximumObjects: 5));
        Assert.Equal(50, report.Resources.Single(x => x.Resource == PdfResourceKind.ExpandedBytes).Consumed);
        Assert.Equal(130, report.Resources.Single(x => x.Resource == PdfResourceKind.ExpandedBytes).Peak);
        Assert.Equal(4, report.Resources.Single(x => x.Resource == PdfResourceKind.Objects).Consumed);
        Assert.Equal("Operation.ScopeLeaked", Assert.Throws<PdfException>(() => Run(c => c.Reserve(PdfResourceKind.ExpandedBytes, 1))).Diagnostic.Code);
        Assert.Equal("Limit.ExpandedBytes", Assert.Throws<PdfException>(() => Run(c =>
        {
            using var lease = c.Reserve(PdfResourceKind.ExpandedBytes, 3);
            try
            {
                c.Consume(PdfResourceKind.ExpandedBytes, 1);
            }
            catch (PdfException)
            {
            }
        }, new PdfResourceLimits(maximumExpandedBytes: 3))).Diagnostic.Code);
    }

    [Fact]
    public void IsolatedObjectReservationsReconcileUseAndDetectUnfinishedJobs()
    {
        var report = Run(context =>
        {
            context.Consume(PdfResourceKind.Objects, 2);
            using (var reservation = context.Reserve(PdfResourceKind.Objects, context.Available(PdfResourceKind.Objects)))
                reservation.Complete(1);
            context.Consume(PdfResourceKind.Objects, 2);
        }, new PdfResourceLimits(maximumObjects: 5));
        Assert.Equal(5, report.Resources.Single(x => x.Resource == PdfResourceKind.Objects).Consumed);
        Run(context =>
        {
            using (var abandoned = context.Reserve(PdfResourceKind.Objects, context.Available(PdfResourceKind.Objects)))
            {
            }

            context.Consume(PdfResourceKind.Objects, 5);
        }, new PdfResourceLimits(maximumObjects: 5));
        Assert.Equal("Operation.ScopeLeaked", Assert.Throws<PdfException>(() => Run(context => context.Reserve(PdfResourceKind.Objects, context.Available(PdfResourceKind.Objects)))).Diagnostic.Code);
        Assert.Equal("Limit.Objects", Assert.Throws<PdfException>(() => Run(context =>
        {
            using var reservation = context.Reserve(PdfResourceKind.Objects, context.Available(PdfResourceKind.Objects));
            try
            {
                context.Consume(PdfResourceKind.Objects, 1);
            }
            catch (PdfException)
            {
            }
        })).Diagnostic.Code);
    }

    internal static PdfOperationReport Run(Action<PdfOperationContext> action, PdfResourceLimits? limits = null, PdfProcessingMode mode = PdfProcessingMode.Strict)
    {
        var provider = new ContractProvider
        {
            OnWriteContext = (_, _, context) => action(context)
        };
        using var document = Pdf.Create(new PdfOptions(operationPolicy: new PdfOperationPolicy(mode, limits)), new PdfProviderRegistry(provider));
        using var output = new MemoryStream();
        var report = document.Save(output);
        Assert.Same(report, document.LastOperationReport);
        return report;
    }

    [Fact]
    public void PoliciesAndDiagnosticConfigurationRejectInvalidBounds()
    {
        Action[] invalid =
        {
            () => new PdfResourceLimits(maximumInputBytes: 0),
            () => new PdfResourceLimits(maximumExpandedBytes: -1),
            () => new PdfResourceLimits(maximumOutputBytes: 0),
            () => new PdfResourceLimits(maximumObjects: -1),
            () => new PdfResourceLimits(maximumPixels: 0),
            () => new PdfResourceLimits(maximumDepth: 0),
            () => new PdfResourceLimits(maximumWorkers: -1),
            () => new PdfResourceLimits(maximumDiagnostics: 0),
            () => new PdfResourceLimits(maximumElapsedTime: TimeSpan.Zero),
            () => new PdfResourceLimits(maximumElapsedTime: TimeSpan.FromDays(2)),
            () => new PdfOperationPolicy((PdfProcessingMode)999),
            () => new PdfDiagnostic("code", "message", severity: (PdfDiagnosticSeverity)999),
            () => new PdfDiagnostic("code", "message", lossCategory: (PdfLossCategory)999),
            () => new PdfDiagnostic("code", "message", pageIndex: -1),
            () => new PdfDiagnostic("code", "message", objectId: -1)
        };
        foreach (var action in invalid)
            Assert.Throws<ArgumentOutOfRangeException>(action);
        Assert.Equal(PdfProcessingMode.Strict, PdfOperationPolicy.Default.Mode);
        var limits = new PdfResourceLimits();
        var policy = new PdfOperationPolicy(limits: limits);
        Assert.Same(limits, policy.Limits);
        Assert.Same(policy, new PdfOptions(operationPolicy: policy).OperationPolicy);
    }

    public static IEnumerable<object[]> ResourceBoundaries()
    {
        foreach (PdfResourceKind resource in Enum.GetValues(typeof(PdfResourceKind)))
            foreach (int amount in new[]
            {
                1,
                2,
                3
            }

            )
                yield return new object[]
                {
                    resource,
                    amount
                };
    }

    [Theory]
    [MemberData(nameof(ResourceBoundaries))]
    public void EveryResourceHasBelowAtAndAboveLimitBehavior(PdfResourceKind resource, int amount)
    {
        var limits = new PdfResourceLimits(2, 2, 2, 2, 2, 2, 2);
        Action<PdfOperationContext> work = context =>
        {
            if (resource == PdfResourceKind.Depth || resource == PdfResourceKind.Workers)
            {
                var scopes = new Stack<IDisposable>();
                try
                {
                    for (int i = 0; i < amount; i++)
                        scopes.Push(context.EnterScope(resource));
                }
                finally
                {
                    while (scopes.Count > 0)
                    {
                        var scope = scopes.Pop();
                        scope.Dispose();
                        scope.Dispose();
                    }
                }
            }
            else
                context.Consume(resource, amount);
        };
        if (amount > 2)
        {
            var error = Assert.Throws<PdfException>(() => Run(work, limits));
            Assert.Equal("Limit." + resource, error.Diagnostic.Code);
        }
        else
        {
            var report = Run(work, limits);
            var usage = Assert.Single(report.Resources, item => item.Resource == resource);
            Assert.Equal(amount, usage.Peak);
            Assert.Equal(resource == PdfResourceKind.Depth || resource == PdfResourceKind.Workers ? 0 : amount, usage.Consumed);
        }
    }

    [Fact]
    public void CumulativeAccountingCannotOverflowOrRefundACharge()
    {
        var error = Assert.Throws<PdfException>(() => Run(context =>
        {
            context.Consume(PdfResourceKind.ExpandedBytes, long.MaxValue - 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 1);
        }, new PdfResourceLimits(maximumExpandedBytes: long.MaxValue)));
        Assert.Equal("Limit.ExpandedBytes", error.Diagnostic.Code);
        Run(context =>
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => context.Consume(PdfResourceKind.Objects, -1));
            Assert.Throws<ArgumentOutOfRangeException>(() => context.Consume((PdfResourceKind)999, 1));
            Assert.Throws<ArgumentException>(() => context.Consume(PdfResourceKind.Depth, 1));
            Assert.Throws<ArgumentException>(() => context.EnterScope(PdfResourceKind.Objects));
            context.Consume(PdfResourceKind.Objects, 1);
        });
    }

    [Fact]
    public void SwallowingAResourceFailureCannotReturnSuccess()
    {
        var error = Assert.Throws<PdfException>(() => Run(context =>
        {
            try
            {
                context.Consume(PdfResourceKind.Objects, 3);
            }
            catch (PdfException)
            {
            }
        }, new PdfResourceLimits(maximumObjects: 2)));
        Assert.Equal("Limit.Objects", error.Diagnostic.Code);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 1)]
    [InlineData(3, 1)]
    [InlineData(long.MaxValue, long.MaxValue)]
    public void PixelDimensionMultiplicationIsBoundedBeforeAllocation(long width, long height)
    {
        if (width > 2 || height > 2)
            Assert.Equal("Limit.Pixels", Assert.Throws<PdfException>(() => Run(context => context.ConsumePixels(width, height), new PdfResourceLimits(maximumPixels: 2))).Diagnostic.Code);
        else
            Assert.Equal(width * height, Run(context => context.ConsumePixels(width, height), new PdfResourceLimits(maximumPixels: 2)).Resources.Single(item => item.Resource == PdfResourceKind.Pixels).Consumed);
    }

    [Fact]
    public void StrictLossIsLatchedEvenIfProviderCatchesIt()
    {
        var error = Assert.Throws<PdfException>(() => Run(context =>
        {
            try
            {
                context.Report(Loss(0, 42));
            }
            catch (PdfException)
            {
            }
        }));
        Assert.Equal("Content.Unsupported", error.Diagnostic.Code);
        Assert.Equal(PdfDiagnosticSeverity.Error, error.Diagnostic.Severity);
        Assert.Equal(0, error.Diagnostic.PageIndex);
        Assert.Equal(42, error.Diagnostic.ObjectId);
        Assert.Equal("Save", error.Diagnostic.Operation);
        Assert.Equal("test.contract", error.Diagnostic.ProviderId);
    }

    [Fact]
    public void ReplacingAPolicyFailureCannotHideItAndRetainsTheUnderlyingError()
    {
        var replacement = new IOException("Replacement fixture failure.");
        var error = Assert.Throws<PdfException>(() => Run(context =>
        {
            try
            {
                context.Report(Loss(0, 42));
            }
            catch (PdfException)
            {
                throw replacement;
            }
        }));
        Assert.Equal("Content.Unsupported", error.Diagnostic.Code);
        Assert.Same(replacement, error.Data["Fluent.Pdf.UnderlyingFailure"]);
    }

    [Fact]
    public void PermissiveResultRetainsEveryLossAndOwnsItsProvenance()
    {
        PdfOperationContext? retained = null;
        var report = Run(context =>
        {
            retained = context;
            context.Report(Loss(0, 42));
            context.Report(Loss(1, 43));
        }, mode: PdfProcessingMode.Permissive);
        Assert.True(report.Succeeded);
        Assert.Equal(2, report.Diagnostics.Count);
        Assert.Equal(new int? [] { 0, 1 }, report.Diagnostics.Select(item => item.PageIndex));
        Assert.All(report.Diagnostics, item =>
        {
            Assert.Equal(PdfDiagnosticSeverity.Warning, item.Severity);
            Assert.Equal(PdfLossCategory.UnsupportedContent, item.LossCategory);
            Assert.Equal("Save", item.Operation);
            Assert.Equal("test.contract", item.ProviderId);
            Assert.Equal(PdfCapability.WriteDocument, item.Capability);
            Assert.Equal("Provide supported content.", item.Remediation);
        });
        Assert.Throws<NotSupportedException>(() => ((IList<PdfDiagnostic>)report.Diagnostics).Clear());
        Assert.Throws<NotSupportedException>(() => ((IList<PdfResourceUsage>)report.Resources).Clear());
        Assert.Throws<ObjectDisposedException>(() => retained!.Checkpoint());
        Assert.Throws<ObjectDisposedException>(() => retained!.Consume(PdfResourceKind.Objects, 0));
        Assert.Throws<ObjectDisposedException>(() => retained!.Report(Loss(0, 1)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public void DiagnosticCapacityNeverSilentlyDropsARecord(int count)
    {
        Action<PdfOperationContext> work = context =>
        {
            for (int i = 0; i < count; i++)
                context.Report(new PdfDiagnostic("Input.Notice", "Fixture notice.", severity: PdfDiagnosticSeverity.Information));
        };
        if (count > 2)
            Assert.Equal("Limit.Diagnostics", Assert.Throws<PdfException>(() => Run(work, new PdfResourceLimits(maximumDiagnostics: 2))).Diagnostic.Code);
        else
            Assert.Equal(count, Run(work, new PdfResourceLimits(maximumDiagnostics: 2)).Diagnostics.Count);
    }

    [Theory]
    [InlineData(PdfProcessingMode.Strict)]
    [InlineData(PdfProcessingMode.Permissive)]
    public void ErrorDiagnosticsRemainFatalInEveryMode(PdfProcessingMode mode)
    {
        var error = Assert.Throws<PdfException>(() => Run(context => context.Report(new PdfDiagnostic("Input.Invalid", "Invalid fixture.")), mode: mode));
        Assert.Equal("Input.Invalid", error.Diagnostic.Code);
    }

    [Theory]
    [InlineData(PdfResourceKind.Depth)]
    [InlineData(PdfResourceKind.Workers)]
    public void UnreleasedScopesBlockSuccessfulReturn(PdfResourceKind resource)
    {
        IDisposable? retained = null;
        var error = Assert.Throws<PdfException>(() => Run(context => retained = context.EnterScope(resource)));
        Assert.Equal("Operation.ScopeLeaked", error.Diagnostic.Code);
        retained!.Dispose();
        retained.Dispose();
    }

    [Fact]
    public void ParallelChargesAreSynchronizedAndScopedWorkersAreReleased()
    {
        var report = Run(context =>
        {
            Parallel.For(0, 64, _ =>
            {
                using var worker = context.EnterScope(PdfResourceKind.Workers);
                for (int i = 0; i < 200; i++)
                    context.Consume(PdfResourceKind.Objects, 1);
            });
        }, new PdfResourceLimits(maximumObjects: 12800, maximumWorkers: 64));
        Assert.Equal(12800, report.Resources.Single(item => item.Resource == PdfResourceKind.Objects).Consumed);
        Assert.Equal(0, report.Resources.Single(item => item.Resource == PdfResourceKind.Workers).Consumed);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(1)]
    public void DeadlineUsesMonotonicBelowAtAndAboveBoundary(long tickOffset)
    {
        var limit = TimeSpan.FromSeconds(30);
        TimeSpan elapsed = TimeSpan.Zero;
        var context = new PdfOperationContext("Fixture", "local", PdfCapability.ReadDocument, new PdfOperationPolicy(limits: new PdfResourceLimits(maximumElapsedTime: limit)), CancellationToken.None, () => elapsed);
        try
        {
            elapsed = limit + TimeSpan.FromTicks(tickOffset);
            if (tickOffset < 0)
                context.Checkpoint();
            else
                Assert.Equal("Limit.ElapsedTime", Assert.Throws<PdfException>(context.Checkpoint).Diagnostic.Code);
        }
        finally
        {
            context.Finish(false);
            context.DisposeSources();
        }
    }

    [Fact]
    public void DeadlineCancelsTheProviderTokenAndCannotBeSwallowed()
    {
        var error = Assert.Throws<PdfException>(() => Run(context =>
        {
            Assert.True(context.CancellationToken.WaitHandle.WaitOne(TimeSpan.FromSeconds(5)));
            try
            {
                context.CancellationToken.ThrowIfCancellationRequested();
            }
            catch (OperationCanceledException)
            {
            }
        }, new PdfResourceLimits(maximumElapsedTime: TimeSpan.FromMilliseconds(50))));
        Assert.Equal("Limit.ElapsedTime", error.Diagnostic.Code);
    }

    internal static PdfDiagnostic Loss(int page, long objectId) => new("Content.Unsupported", "Fixture behavior is unsupported.", providerId: "untrusted.provenance", lossCategory: PdfLossCategory.UnsupportedContent, operation: "different", pageIndex: page, objectId: objectId, remediation: "Provide supported content.");
}
