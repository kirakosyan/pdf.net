using System.Globalization;
using System.Runtime.CompilerServices;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>A callback-thread generation builder. Mutation is permitted only inside an active template invocation; completed contexts cannot be reused.</summary>
public sealed class PdfGenerationContext
{
    private readonly PdfGenerationOptions _options;
    private readonly PdfOperationContext _context;
    private readonly int _thread = Thread.CurrentThread.ManagedThreadId;
    private readonly List<Action<PdfOperationReport>> _finish;
    private readonly List<PdfFlowBlock> _blocks = new();
    private readonly HashSet<(object Template, object? Record)> _activeRecords = new(IdentityComparer.Instance);
    private bool _active = true, _dispatching;
    private int _depth, _events;
    private string? _template;
    private IReadOnlyList<PdfGenerationPage> _pages = Array.Empty<PdfGenerationPage>();
    private sealed class IdentityComparer : IEqualityComparer<(object Template, object? Record)>
    {
        internal static readonly IdentityComparer Instance = new();
        public bool Equals((object Template, object? Record) x, (object Template, object? Record) y) => ReferenceEquals(x.Template, y.Template) && ReferenceEquals(x.Record, y.Record);
        public int GetHashCode((object Template, object? Record) value) => unchecked(RuntimeHelpers.GetHashCode(value.Template) * 397 ^ (value.Record == null ? 0 : RuntimeHelpers.GetHashCode(value.Record)));
    }

    internal PdfGenerationContext(PdfGenerationOptions options, PdfProviderRegistry providers, PdfOperationContext context, List<Action<PdfOperationReport>> finish)
    {
        _options = options;
        Providers = providers;
        _context = context;
        _finish = finish;
    }

    /// <summary>Gets the captured immutable standard formatting culture.</summary>
    public CultureInfo Culture => _options.Culture;
    /// <summary>Gets the explicitly supplied immutable provider registry; no discovery or resource loading occurs.</summary>
    public PdfProviderRegistry Providers { get; }
    /// <summary>Gets the shared context for cooperative provider work.</summary>
    public PdfOperationContext OperationContext => _context;
    /// <summary>Gets the current zero-based source record, or -1 before enumeration.</summary>
    public int RecordIndex { get; internal set; } = -1;
    /// <summary>Gets fully rendered source records.</summary>
    public int RecordsPrepared { get; internal set; }
    /// <summary>Gets captured top-level blocks.</summary>
    public int BlockCount => _blocks.Count;
    internal IReadOnlyList<PdfFlowBlock> Blocks => _blocks;

    /// <summary>Captures one immutable block during template rendering. Layout remains a later shared-budget phase.</summary>
    public void Add(PdfFlowBlock block)
    {
        Mutable();
        if (block == null)
            throw new ArgumentNullException(nameof(block));
        if (_blocks.Count == _options.MaximumBlocks)
            Fail("BlockLimit", "The explicit generation block ceiling was reached.");
        _context.Consume(PdfResourceKind.Objects, 1);
        _context.Consume(PdfResourceKind.ExpandedBytes, 96);
        _blocks.Add(block);
    }

    /// <summary>Captures a finite block source with cooperative checkpoints and the same block/object ceilings.</summary>
    public void Add(IEnumerable<PdfFlowBlock> blocks)
    {
        Mutable();
        if (blocks == null)
            throw new ArgumentNullException(nameof(blocks));
        using var enumerator = blocks.GetEnumerator();
        while (true)
        {
            Check();
            if (!enumerator.MoveNext())
            {
                Check();
                break;
            }

            Check();
            Add(enumerator.Current);
        }
    }

    /// <summary>Captures a paragraph using an explicit immutable text style; no font selection occurs implicitly.</summary>
    public void Paragraph(string text, PdfFlowTextStyle style, PdfFlowParagraphStyle? paragraphStyle = null)
    {
        Mutable();
        if (text == null)
            throw new ArgumentNullException(nameof(text));
        if (style == null)
            throw new ArgumentNullException(nameof(style));
        _context.Consume(PdfResourceKind.InputBytes, text.Length * 2L);
        _context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 4L + 256);
        Add(new PdfFlowParagraph(text, style, paragraphStyle));
    }

    /// <summary>Formats only supported scalar types with the captured culture. Arbitrary object ToString and ambient culture are never used.</summary>
    public string Format(object? value, string? format = null, string nullText = "")
    {
        Check();
        if (nullText == null)
            throw new ArgumentNullException(nameof(nullText));
        if (format?.Length > 256 || nullText.Length > 1000000)
            throw new ArgumentException("Formatting inputs must be bounded.");
        string text = PdfTableData.Format(value, format, nullText, Culture, _context);
        _context.Consume(PdfResourceKind.InputBytes, text.Length * 2L);
        _context.Consume(PdfResourceKind.ExpandedBytes, text.Length * 4L + 64);
        return text;
    }

    /// <summary>Binds and captures a typed finite table under this operation and culture without exposing unfinished model/report objects.</summary>
    public void AddTable<T>(IEnumerable<T> source, PdfTableDataColumn<T>[] columns, int maximumRows = 100000, PdfTableRow[]? headerRows = null, bool allowRowSplit = true)
    {
        Mutable();
        var table = PdfTableData.BindCore(source, columns, Culture, maximumRows, headerRows, allowRowSplit, _context);
        _context.Consume(PdfResourceKind.ExpandedBytes, 128);
        _finish.Add(report => table.OperationReport = report);
        Add(table);
    }

    /// <summary>Invokes a reusable typed template within the same operation. Reference-record invocation cycles reject; every invocation uses bounded depth, including value records.</summary>
    public void Apply<T>(PdfGenerationTemplate<T> template, T record)
    {
        Mutable();
        ApplyCore(template, record);
    }

    internal void ApplyRoot<T>(PdfGenerationTemplate<T> template, T record)
    {
        Check();
        if (_depth != 0 || _dispatching)
            Fail("ReentrantMutation", "Root record rendering cannot reenter active work.");
        ApplyCore(template, record);
    }

    private void ApplyCore<T>(PdfGenerationTemplate<T> template, T record)
    {
        if (template == null)
            throw new ArgumentNullException(nameof(template));
        Check();
        using var scope = _context.EnterScope(PdfResourceKind.Depth);
        _context.Consume(PdfResourceKind.Objects, 1);
        _context.Consume(PdfResourceKind.ExpandedBytes, 256);
        bool referenceRecord = !typeof(T).IsValueType;
        object? identity = referenceRecord ? (object? )record : null;
        var key = ((object)template, identity);
        if (referenceRecord && !_activeRecords.Add(key))
            Fail("TemplateCycle", "The same template/reference-record pair is already active.");
        string? previous = _template;
        _template = template.Identifier;
        _depth++;
        try
        {
            Emit(PdfGenerationEventKind.TemplateStarting);
            template.Render(this, record);
            Check();
            Emit(PdfGenerationEventKind.TemplatePrepared);
        }
        finally
        {
            _depth--;
            _template = previous;
            if (referenceRecord)
                _activeRecords.Remove(key);
        }
    }

    internal void SetLayout(PdfFlowResult layout)
    {
        _context.Consume(PdfResourceKind.ExpandedBytes, layout.Pages.Count * 256L + 64);
        _context.Consume(PdfResourceKind.Objects, layout.Pages.Count);
        var pages = new PdfGenerationPage[layout.Pages.Count];
        for (int i = 0; i < pages.Length; i++)
        {
            _context.Checkpoint();
            pages[i] = new(i, layout.Options.Geometry, layout.Pages[i]);
        }

        _pages = Array.AsReadOnly(pages);
    }

    internal void Emit(PdfGenerationEventKind kind, int page = -1)
    {
        Check();
        var observer = _options.Observer;
        if (observer == null)
            return;
        if (_events == _options.MaximumEvents)
            Fail("EventLimit", "The explicit observer invocation ceiling was reached.");
        _context.Consume(PdfResourceKind.Objects, 1);
        _context.Consume(PdfResourceKind.ExpandedBytes, 256);
        var view = new PdfGenerationEvent(kind, _events++, RecordIndex, RecordsPrepared, _blocks.Count, _template, _depth, _pages, page, _context);
        _dispatching = true;
        try
        {
            observer(view);
        }
        finally
        {
            _dispatching = false;
        }

        Check();
    }

    internal void Expire()
    {
        _active = false;
    }

    private void Mutable()
    {
        Check();
        if (_dispatching)
            Fail("ReentrantMutation", "Generation hooks cannot mutate or reenter the observed generation plan.");
        if (_depth == 0)
            Fail("MutationPhase", "Add, bind and nested apply are available only during template rendering.");
    }

    private void Check()
    {
        if (!_active)
            throw new PdfException(new("Generation.ContextExpired", "The synchronous generation callback context is no longer active."));
        if (Thread.CurrentThread.ManagedThreadId != _thread)
            Fail("CallbackThread", "Use generation callbacks on their owning synchronous thread.");
        _context.Checkpoint();
    }

    private void Fail(string code, string message)
    {
        _context.Report(new("Generation." + code, message));
        throw new InvalidOperationException("A fatal generation diagnostic unexpectedly returned.");
    }
}
