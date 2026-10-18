using System.Collections;

namespace Fluent.Pdf;

/// <summary>A zero-based editable page collection. Document mutation is not thread-safe.</summary>
public sealed class PdfPageCollection : IReadOnlyList<PdfPage>
{
    private readonly PdfDocument _owner;
    private readonly List<PdfPage> _pages = new();
    private int _version;

    internal PdfPageCollection(PdfDocument owner) => _owner = owner;

    /// <summary>Gets the number of attached pages.</summary>
    public int Count { get { _owner.EnsureAlive(); return _pages.Count; } }
    /// <summary>Gets the page at a zero-based index.</summary>
    public PdfPage this[int index] { get { _owner.EnsureAlive(); return _pages[index]; } }

    /// <summary>Adds a page using the document's default size.</summary>
    public PdfPage Add() => Add(_owner.Options.DefaultPageSize);

    /// <summary>Adds a page with a valid size, within the document's maximum page count.</summary>
    public PdfPage Add(PdfPageSize size)
        =>AddCore(size,null);
    internal PdfPage Add(PdfPageSize size,PdfOperationContext context)=>AddCore(size,context);
    private PdfPage AddCore(PdfPageSize size,PdfOperationContext? context)
    {
        _owner.EnsureEditable();
        Guard.Size(size, nameof(size));
        if (_pages.Count >= _owner.Options.MaximumPages)
            throw _owner.PageLimit();
        _owner.BeforePageAllocation(context);
        var page = new PdfPage(_owner, size);
        _pages.Add(page);
        _version++;
        return page;
    }

    /// <summary>Removes a page and invalidates previously retained references to that page.</summary>
    public void RemoveAt(int index)
    {
        _owner.EnsureEditable();
        var page = _pages[index];
        _pages.RemoveAt(index);
        page.Detach();
        _version++;
    }

    /// <summary>Moves a page to its final zero-based index while retaining its identity.</summary>
    public void Move(int oldIndex, int newIndex)
    {
        _owner.EnsureEditable();
        if (oldIndex < 0 || oldIndex >= _pages.Count) throw new ArgumentOutOfRangeException(nameof(oldIndex));
        if (newIndex < 0 || newIndex >= _pages.Count) throw new ArgumentOutOfRangeException(nameof(newIndex));
        if (oldIndex == newIndex) return;
        var page = _pages[oldIndex];
        _pages.RemoveAt(oldIndex);
        _pages.Insert(newIndex, page);
        _version++;
    }

    /// <summary>Removes and invalidates all pages.</summary>
    public void Clear() { _owner.EnsureEditable(); ClearCore(); }

    internal void ClearCore()
    {
        foreach (var page in _pages) page.Detach();
        if (_pages.Count == 0) return;
        _pages.Clear();
        _version++;
    }

    /// <summary>Enumerates attached pages; edits invalidate enumeration and disposal invalidates further moves.</summary>
    public IEnumerator<PdfPage> GetEnumerator()
    {
        _owner.EnsureAlive();
        return Enumerate(_version).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private IEnumerable<PdfPage> Enumerate(int version)
    {
        for (int i = 0; ; i++)
        {
            _owner.EnsureAlive();
            if (_version != version) throw new InvalidOperationException("The page collection changed during enumeration.");
            if (i >= _pages.Count) yield break;
            yield return _pages[i];
        }
    }
}
