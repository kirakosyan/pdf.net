using System.Collections;
using System.Collections.Generic;
using Xunit;

namespace Fluent.Pdf.Contract.Tests;

public sealed class ModelTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(-1, 1)]
    [InlineData(1, 0)]
    [InlineData(double.NaN, 1)]
    [InlineData(1, double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity, 1)]
    public void SizesRejectInvalidDimensions(double width, double height) =>
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfPageSize(width, height));

    [Fact]
    public void SizesHaveValueSemanticsAndDefaultIsRejected()
    {
        Assert.Equal(new PdfPageSize(612, 792), PdfPageSize.Letter);
        Assert.True(new PdfPageSize(612, 792) == PdfPageSize.Letter);
        Assert.True(PdfPageSize.A4 != PdfPageSize.Letter);
        Assert.Equal(new PdfPageSize(612, 792).GetHashCode(), PdfPageSize.Letter.GetHashCode());
        Assert.False(default(PdfPageSize).IsValid);
        Assert.Throws<ArgumentException>(() => new PdfOptions(default(PdfPageSize)));
        using var document = Pdf.Create();
        Assert.Throws<ArgumentException>(() => document.Pages.Add(default(PdfPageSize)));
        Assert.Empty(document.Pages);
    }

    [Fact]
    public void OptionsAreSharedImmutableValuesAndPageLimitFailsWithoutMutation()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new PdfOptions(maximumPages: 0));
        var options = new PdfOptions(PdfPageSize.Letter, 1);
        using var document = Pdf.Create(options);
        Assert.Same(options, document.Options);
        var page = document.Pages.Add();
        Assert.Equal(PdfPageSize.Letter, page.Size);
        var failure = Assert.Throws<PdfException>(() => document.AddPage());
        Assert.Equal("Limit.Pages", failure.Diagnostic.Code);
        Assert.Single(document.Pages);
        Assert.Same(page, document.Pages[0]);
        document.Pages.RemoveAt(0);
        document.Pages.Add();
        Assert.Single(document.Pages);
    }

    [Fact]
    public void FluentEditsPreservePageIdentityAndValidateBeforeMutation()
    {
        using var document = Pdf.Create();
        Assert.Same(document, document.AddPage(PdfPageSize.A4).AddPage(PdfPageSize.Letter));
        var first = document.Pages[0];
        var second = document.Pages[1];
        first.Rotation = PdfPageRotation.Clockwise90;
        document.Pages.Move(0, 1);
        Assert.Same(first, document.Pages[1]);
        Assert.Same(second, document.Pages[0]);
        Assert.Throws<ArgumentOutOfRangeException>(() => document.Pages.Move(0, 2));
        Assert.Throws<ArgumentOutOfRangeException>(() => first.Rotation = (PdfPageRotation)1);
        Assert.Throws<ArgumentException>(() => first.Size = default);
        Assert.Equal(PdfPageSize.A4, first.Size);
        Assert.Equal(PdfPageRotation.Clockwise90, first.Rotation);
        Assert.Same(second, document.Pages[0]);
    }

    [Fact]
    public void RemovedPagesAndClearedPagesCannotBeReused()
    {
        using var document = Pdf.Create().AddPage().AddPage();
        var removed = document.Pages[0];
        var cleared = document.Pages[1];
        document.Pages.RemoveAt(0);
        Assert.Throws<InvalidOperationException>(() => removed.Size);
        document.Pages.Clear();
        Assert.Throws<InvalidOperationException>(() => cleared.Rotation = PdfPageRotation.None);
        Assert.Empty(document.Pages);
    }

    [Fact]
    public void SnapshotIsDetachedReadOnlyAndSurvivesDisposal()
    {
        var document = Pdf.Create().AddPage(PdfPageSize.Letter);
        document.Pages[0].Rotation = PdfPageRotation.Clockwise270;
        var snapshot = document.CreateSnapshot();
        document.Pages[0].Size = PdfPageSize.A4;
        document.Pages.Clear();
        document.Dispose();
        Assert.Single(snapshot.Pages);
        Assert.Equal(PdfPageSize.Letter, snapshot.Pages[0].Size);
        Assert.Equal(PdfPageRotation.Clockwise270, snapshot.Pages[0].Rotation);
        Assert.Throws<NotSupportedException>(() => ((IList<PdfPageSnapshot>)snapshot.Pages).Clear());
    }

    [Fact]
    public void DisposalInvalidatesRetainedHandlesAndIsIdempotent()
    {
        var document = Pdf.Create().AddPage();
        var pages = document.Pages;
        var page = pages[0];
        using var enumerator = pages.GetEnumerator();
        Assert.True(enumerator.MoveNext());
        document.Dispose();
        document.Dispose();
        Assert.True(document.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => document.Options);
        Assert.Throws<ObjectDisposedException>(() => document.Providers);
        Assert.Throws<ObjectDisposedException>(() => document.Pages);
        Assert.Throws<ObjectDisposedException>(() => document.CreateSnapshot());
        Assert.Throws<ObjectDisposedException>(() => document.AddPage());
        Assert.Throws<ObjectDisposedException>(() => pages.Count);
        Assert.Throws<ObjectDisposedException>(() => pages[0]);
        Assert.Throws<ObjectDisposedException>(() => pages.Add());
        Assert.Throws<ObjectDisposedException>(() => pages.Clear());
        Assert.Throws<ObjectDisposedException>(() => pages.Move(0, 0));
        Assert.Throws<ObjectDisposedException>(() => pages.RemoveAt(0));
        Assert.Throws<ObjectDisposedException>(() => pages.GetEnumerator());
        Assert.Throws<ObjectDisposedException>(() => page.Size);
        Assert.Throws<ObjectDisposedException>(() => page.Rotation);
        Assert.Throws<ObjectDisposedException>(() => enumerator.MoveNext());
        Assert.Throws<ObjectDisposedException>(() => ((IEnumerable)pages).GetEnumerator());
    }

    [Fact]
    public void EnumerationDetectsChangesBeforeAndAfterItsFirstMove()
    {
        using var document = Pdf.Create().AddPage();
        using var before = document.Pages.GetEnumerator();
        document.AddPage();
        Assert.Throws<InvalidOperationException>(() => before.MoveNext());
        using var after = document.Pages.GetEnumerator();
        Assert.True(after.MoveNext());
        document.Pages.RemoveAt(0);
        Assert.Throws<InvalidOperationException>(() => after.MoveNext());
    }
}
