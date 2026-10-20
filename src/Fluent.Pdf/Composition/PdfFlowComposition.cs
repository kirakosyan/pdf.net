using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>Invariant list marker notation.</summary>
public enum PdfFlowListKind
{
    /// <summary>Use an explicitly supplied bullet.</summary>
    Bullet,
    /// <summary>Use invariant decimal counters.</summary>
    Decimal,
    /// <summary>Use lowercase alphabetic counters.</summary>
    LowerAlpha,
    /// <summary>Use uppercase alphabetic counters.</summary>
    UpperAlpha,
    /// <summary>Use lowercase Roman counters from 1 through 3999.</summary>
    LowerRoman,
    /// <summary>Use uppercase Roman counters from 1 through 3999.</summary>
    UpperRoman
}

/// <summary>Physical ordering of logical columns.</summary>
public enum PdfFlowColumnOrder
{
    /// <summary>Place successive logical columns from left to right.</summary>
    LeftToRight,
    /// <summary>Place successive logical columns from right to left.</summary>
    RightToLeft
}

/// <summary>An explicit section boundary without changing page geometry.</summary>
public enum PdfFlowSectionStart
{
    /// <summary>Continue on the current page.</summary>
    Continuous,
    /// <summary>Begin on the next available page.</summary>
    NextPage,
    /// <summary>Begin on an odd physical page, inserting a blank when needed.</summary>
    NextOddPage,
    /// <summary>Begin on an even physical page, inserting a blank when needed.</summary>
    NextEvenPage
}

/// <summary>Immutable content with an optional local counter restart.</summary>
public sealed class PdfFlowListItem
{
    /// <summary>Captures bounded item content and an optional counter restart.</summary>
    public PdfFlowListItem(IEnumerable<PdfFlowBlock> content, int? counterValue = null)
    {
        Content = PdfFlowCapture.Blocks(content);
        CounterValue = counterValue;
    }

    /// <summary>Gets immutable logical content blocks.</summary>
    public IReadOnlyList<PdfFlowBlock> Content { get; }
    /// <summary>Gets an explicit item counter restart, or null to continue.</summary>
    public int? CounterValue { get; }
}

/// <summary>An immutable list whose counters are fixed before pagination.</summary>
public sealed class PdfFlowList : PdfFlowBlock
{
    private PdfFlowList(PdfFlowListItem[] items, string[] markers, int[] counters, PdfFlowTextStyle style, double? width, double gap)
    {
        Items = new ReadOnlyCollection<PdfFlowListItem>(items);
        Markers = new ReadOnlyCollection<string>(markers);
        Counters = new ReadOnlyCollection<int>(counters);
        MarkerStyle = style;
        MarkerWidth = width;
        Gap = gap;
    }

    /// <summary>Gets immutable source items.</summary>
    public IReadOnlyList<PdfFlowListItem> Items { get; }
    /// <summary>Gets invariant markers fixed before pagination.</summary>
    public IReadOnlyList<string> Markers { get; }
    /// <summary>Gets stable item counter values.</summary>
    public IReadOnlyList<int> Counters { get; }
    /// <summary>Gets the explicit font/style for markers.</summary>
    public PdfFlowTextStyle MarkerStyle { get; }
    /// <summary>Gets fixed marker width, or null for measured width.</summary>
    public double? MarkerWidth { get; }
    /// <summary>Gets the physical inter-column or marker gap in points.</summary>
    public double Gap { get; }
    /// <summary>Gets the successful bounded construction report.</summary>
    public PdfOperationReport OperationReport { get; private set; } = null!;

    /// <summary>Captures a finite item source and fixes invariant markers/restarts under one operation budget.</summary>
    public static PdfFlowList Create(IEnumerable<PdfFlowListItem> items, PdfFlowTextStyle markerStyle, PdfFlowListKind kind = PdfFlowListKind.Decimal, int start = 1, string prefix = "", string suffix = ".", string bullet = "\u2022", double? markerWidth = null, double gap = 6, int maximumItems = 100000, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (items == null)
            throw new ArgumentNullException(nameof(items));
        if (markerStyle == null)
            throw new ArgumentNullException(nameof(markerStyle));
        if (!Enum.IsDefined(typeof(PdfFlowListKind), kind))
            throw new ArgumentOutOfRangeException(nameof(kind));
        if (prefix == null || suffix == null || bullet == null || prefix.Length > 32 || suffix.Length > 32 || bullet.Length < 1 || bullet.Length > 128)
            throw new ArgumentException("Bounded explicit marker text is required.");
        PdfGraphicsNumbers.Finite(gap);
        if (gap < 0 || maximumItems < 1 || maximumItems > 1000000)
            throw new ArgumentOutOfRangeException(nameof(gap));
        if (markerWidth.HasValue)
        {
            PdfGraphicsNumbers.Finite(markerWidth.Value);
            if (markerWidth <= 0)
                throw new ArgumentOutOfRangeException(nameof(markerWidth));
        }

        PdfFlowList? result = null;
        var report = PdfFont.Run("CreateList", "managed.composition", policy, cancellationToken, context =>
        {
            result = CreateCore(items, markerStyle, kind, start, prefix, suffix, bullet, markerWidth, gap, maximumItems, context);
        }, PdfCapability.WriteDocument);
        result!.OperationReport = report;
        return result;
    }

    internal static PdfFlowList CreateCore(IEnumerable<PdfFlowListItem> items, PdfFlowTextStyle markerStyle, PdfFlowListKind kind, int start, string prefix, string suffix, string bullet, double? markerWidth, double gap, int maximumItems, PdfOperationContext context)
    {
        var copy = new List<PdfFlowListItem>();
        foreach (var item in items)
        {
            context.Checkpoint();
            context.Consume(PdfResourceKind.Objects, 1);
            context.Consume(PdfResourceKind.ExpandedBytes, 256);
            if (copy.Count == maximumItems)
                throw PdfWriterCore.Error(context, "Composition.ListLimit", "The list exceeds its explicit finite item limit.");
            copy.Add(item ?? throw new ArgumentException("List items cannot contain null.", nameof(items)));
        }

        var labels = new string[copy.Count];
        var values = new int[copy.Count];
        int counter = start;
        for (int i = 0; i < copy.Count; i++)
        {
            counter = copy[i].CounterValue ?? counter;
            values[i] = counter;
            labels[i] = kind == PdfFlowListKind.Bullet ? bullet : prefix + Marker(counter, kind, context) + suffix;
            if (i + 1 < copy.Count)
            {
                if (counter == int.MaxValue)
                    throw PdfWriterCore.Error(context, "Composition.CounterOverflow", "The list counter cannot advance within its integer range.");
                counter++;
            }
        }

        return new(copy.ToArray(), labels, values, markerStyle, markerWidth, gap);
    }

    internal PdfFlowList WithContent(Func<IReadOnlyList<PdfFlowBlock>, IReadOnlyList<PdfFlowBlock>> map)
    {
        var value = new PdfFlowList(Items.Select(item => new PdfFlowListItem(map(item.Content), item.CounterValue)).ToArray(), Markers.ToArray(), Counters.ToArray(), MarkerStyle, MarkerWidth, Gap);
        value.OperationReport = OperationReport;
        return value;
    }

    internal PdfTable ToTable(PdfFlowOptions options, IPdfTextShaper shaper, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, Items.Count * 512L + 256);
        double width = MarkerWidth ?? 0;
        if (!MarkerWidth.HasValue)
            foreach (string marker in Markers)
                width = Math.Max(width, new PdfFlowParagraphData(new(marker, MarkerStyle), options, shaper, context).Get(0, marker.Length).Width);
        width = Math.Max(width, .000001);
        var none = PdfTableBorder.None;
        var markerCell = new PdfTableCellStyle(new(0, 0, Gap, 0), left: none, top: none, right: none, bottom: none);
        var bodyCell = new PdfTableCellStyle(PdfMargins.Uniform(0), left: none, top: none, right: none, bottom: none);
        var rows = Items.Select((item, i) => new PdfTableRow(new[] { PdfTableCell.Text(Markers[i], MarkerStyle, style: markerCell), new PdfTableCell(item.Content, style: bodyCell) }));
        var table = PdfTable.CreateCore(new[] { PdfTableColumn.Fixed(width + Gap), PdfTableColumn.Auto() }, rows, 0, true, Math.Max(1, Items.Count), context);
        table.ListSource = this;
        return table;
    }

    private static string Marker(int value, PdfFlowListKind kind, PdfOperationContext context)
    {
        if (kind == PdfFlowListKind.Decimal)
            return value.ToString(CultureInfo.InvariantCulture);
        if (value <= 0)
            throw PdfWriterCore.Error(context, "Composition.CounterRange", "Alphabetic and Roman markers require positive counters.");
        var builder = new StringBuilder();
        if (kind == PdfFlowListKind.LowerAlpha || kind == PdfFlowListKind.UpperAlpha)
        {
            while (value > 0)
            {
                value--;
                builder.Insert(0, (char)('A' + value % 26));
                value /= 26;
            }
        }
        else
        {
            if (value > 3999)
                throw PdfWriterCore.Error(context, "Composition.RomanRange", "The explicit Roman marker profile supports 1 through 3999.");
            int[] values =
            {
                1000,
                900,
                500,
                400,
                100,
                90,
                50,
                40,
                10,
                9,
                5,
                4,
                1
            };
            string[] symbols =
            {
                "M",
                "CM",
                "D",
                "CD",
                "C",
                "XC",
                "L",
                "XL",
                "X",
                "IX",
                "V",
                "IV",
                "I"
            };
            for (int i = 0; i < values.Length; i++)
                while (value >= values[i])
                {
                    builder.Append(symbols[i]);
                    value -= values[i];
                }
        }

        string text = builder.ToString();
        return kind == PdfFlowListKind.LowerAlpha || kind == PdfFlowListKind.LowerRoman ? text.ToLowerInvariant() : text;
    }
}

/// <summary>A fixed-height column region; logical content fills columns in the explicit physical order.</summary>
public sealed class PdfFlowColumns : PdfFlowBlock
{
    /// <summary>Captures bounded logical content and a finite fixed-height column region.</summary>
    public PdfFlowColumns(IEnumerable<PdfFlowBlock> content, int count = 2, double gap = 18, double? height = null, PdfFlowColumnOrder order = PdfFlowColumnOrder.LeftToRight)
    {
        Content = PdfFlowCapture.Blocks(content);
        if (count < 2 || count > 16)
            throw new ArgumentOutOfRangeException(nameof(count));
        PdfGraphicsNumbers.Finite(gap);
        if (gap < 0)
            throw new ArgumentOutOfRangeException(nameof(gap));
        if (height.HasValue)
        {
            PdfGraphicsNumbers.Finite(height.Value);
            if (height <= 0)
                throw new ArgumentOutOfRangeException(nameof(height));
        }

        if (!Enum.IsDefined(typeof(PdfFlowColumnOrder), order))
            throw new ArgumentOutOfRangeException(nameof(order));
        Count = count;
        Gap = gap;
        Height = height;
        Order = order;
    }

    /// <summary>Gets immutable logical content blocks.</summary>
    public IReadOnlyList<PdfFlowBlock> Content { get; }
    /// <summary>Gets the explicit physical column count.</summary>
    public int Count { get; }
    /// <summary>Gets the physical inter-column or marker gap in points.</summary>
    public double Gap { get; }
    /// <summary>Gets fixed physical region height, or null for a full page frame.</summary>
    public double? Height { get; }
    /// <summary>Gets physical column ordering for logical content.</summary>
    public PdfFlowColumnOrder Order { get; }
}

/// <summary>Reusable immutable content. Kept components use an unsplittable borderless cell.</summary>
public sealed class PdfFlowComponent : PdfFlowBlock
{
    /// <summary>Captures reusable immutable content and explicit keep behavior.</summary>
    public PdfFlowComponent(IEnumerable<PdfFlowBlock> content, bool keepTogether = false)
    {
        Content = PdfFlowCapture.Blocks(content);
        KeepTogether = keepTogether;
    }

    /// <summary>Gets immutable logical content blocks.</summary>
    public IReadOnlyList<PdfFlowBlock> Content { get; }
    /// <summary>Gets whether this component must fit an empty page as a whole.</summary>
    public bool KeepTogether { get; }
}

/// <summary>An explicit continuous/new/odd/even section boundary.</summary>
public sealed class PdfFlowSectionBreak : PdfFlowBlock
{
    /// <summary>Captures an explicit continuous/new/odd/even section boundary.</summary>
    public PdfFlowSectionBreak(PdfFlowSectionStart start = PdfFlowSectionStart.NextPage)
    {
        if (!Enum.IsDefined(typeof(PdfFlowSectionStart), start))
            throw new ArgumentOutOfRangeException(nameof(start));
        Start = start;
    }

    /// <summary>Gets the section boundary policy.</summary>
    public PdfFlowSectionStart Start { get; }
}

/// <summary>An immutable note definition, reusable by multiple explicit references.</summary>
public sealed class PdfFlowNote
{
    /// <summary>Captures a bounded note ID/label, immutable body and explicit marker style.</summary>
    public PdfFlowNote(string id, string label, IEnumerable<PdfFlowBlock> content, PdfFlowTextStyle markerStyle)
    {
        Id = Guard.Text(id, nameof(id), 128);
        Label = Guard.Text(label, nameof(label), 128);
        Content = PdfFlowCapture.Blocks(content);
        MarkerStyle = markerStyle ?? throw new ArgumentNullException(nameof(markerStyle));
    }

    /// <summary>Gets the stable bounded note definition ID.</summary>
    public string Id { get; }
    /// <summary>Gets its explicit visible label.</summary>
    public string Label { get; }
    /// <summary>Gets immutable logical content blocks.</summary>
    public IReadOnlyList<PdfFlowBlock> Content { get; }
    /// <summary>Gets the explicit font/style for markers.</summary>
    public PdfFlowTextStyle MarkerStyle { get; }
}

/// <summary>A paragraph reference with an automatically appended raised note label.</summary>
public sealed class PdfFlowNoteReference : PdfFlowBlock
{
    /// <summary>Captures a paragraph reference and explicit footnote/endnote placement.</summary>
    public PdfFlowNoteReference(PdfFlowParagraph paragraph, PdfFlowNote note, bool endnote = false)
    {
        Paragraph = paragraph ?? throw new ArgumentNullException(nameof(paragraph));
        Note = note ?? throw new ArgumentNullException(nameof(note));
        IsEndnote = endnote;
    }

    /// <summary>Gets the source reference paragraph.</summary>
    public PdfFlowParagraph Paragraph { get; }
    /// <summary>Gets its immutable note definition.</summary>
    public PdfFlowNote Note { get; }
    /// <summary>Gets whether the note is appended after body content.</summary>
    public bool IsEndnote { get; }
}

internal static class PdfFlowCapture
{
    internal static IReadOnlyList<PdfFlowBlock> Blocks(IEnumerable<PdfFlowBlock> blocks)
    {
        if (blocks == null)
            throw new ArgumentNullException(nameof(blocks));
        var copy = new List<PdfFlowBlock>();
        foreach (var block in blocks)
        {
            if (block == null || copy.Count == 4096)
                throw new ArgumentException("Supply up to 4096 nonnull component blocks.", nameof(blocks));
            copy.Add(block);
        }

        return copy.AsReadOnly();
    }
}
