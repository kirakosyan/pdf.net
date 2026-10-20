namespace Fluent.Pdf;
/// <summary>The explicitly captured source of an owned page-content stamp.</summary>
public enum PdfStampContentKind
{
    /// <summary>An original drawing.</summary>
    Drawing,
    /// <summary>Explicitly shaped text and its embedded fonts.</summary>
    Text,
    /// <summary>An immutable image, retaining encoded samples and orientation.</summary>
    Image,
    /// <summary>The passive visible content of a PDF page.</summary>
    PdfPage,
    /// <summary>An explicitly rendered HTML page.</summary>
    Html
}

/// <summary>The owned stamp's position relative to existing page content.</summary>
public enum PdfStampLayer
{
    /// <summary>Paint behind existing page content.</summary>
    Background,
    /// <summary>Paint in front of existing page content.</summary>
    Foreground
}

/// <summary>Immutable explicit placement in displayed CropBox points, with a top-left origin.</summary>
public sealed class PdfStampOptions
{
    /// <summary>Captures a positive destination, opacity0–1, clockwise rotation, bounded repetition and explicit decoration classification. Repetition advances by destination size plus the supplied gap.</summary>
    public PdfStampOptions(PdfRect destination, PdfStampLayer layer = PdfStampLayer.Foreground, double opacity = 1, double rotation = 0, bool artifact = true, int columns = 1, int rows = 1, double horizontalGap = 0, double verticalGap = 0, PdfCanvasFit fit = PdfCanvasFit.Contain)
    {
        PdfGraphicsNumbers.Unit(opacity);
        foreach (double number in new[]
        {
            rotation,
            horizontalGap,
            verticalGap
        }

        )
            PdfGraphicsNumbers.Finite(number);
        if (destination.Width <= 0 || destination.Height <= 0 || horizontalGap < 0 || verticalGap < 0 || Math.Abs(rotation) > 360000 || columns < 1 || columns > 100 || rows < 1 || rows > 100)
            throw new ArgumentOutOfRangeException(nameof(destination));
        if (!Enum.IsDefined(typeof(PdfStampLayer), layer) || !Enum.IsDefined(typeof(PdfCanvasFit), fit))
            throw new ArgumentOutOfRangeException(nameof(layer));
        // Validate the last repeated frame before any operation can allocate placements.
        _ = new PdfRect(destination.X + (columns - 1) * (destination.Width + horizontalGap), destination.Y + (rows - 1) * (destination.Height + verticalGap), destination.Width, destination.Height);
        Destination = destination;
        Layer = layer;
        Opacity = opacity;
        Rotation = rotation;
        Artifact = artifact;
        Columns = columns;
        Rows = rows;
        HorizontalGap = horizontalGap;
        VerticalGap = verticalGap;
        Fit = fit;
    }

    /// <summary>Gets the first displayed destination frame.</summary>
    public PdfRect Destination { get; }
    /// <summary>Gets foreground/background placement.</summary>
    public PdfStampLayer Layer { get; }
    /// <summary>Gets explicit fill and stroke opacity.</summary>
    public double Opacity { get; }
    /// <summary>Gets clockwise rotation about each destination's center.</summary>
    public double Rotation { get; }
    /// <summary>Gets whether the complete stamp invocation is decoration marked as Artifact.</summary>
    public bool Artifact { get; }
    /// <summary>Gets the horizontal repetition count.</summary>
    public int Columns { get; }
    /// <summary>Gets the vertical repetition count.</summary>
    public int Rows { get; }
    /// <summary>Gets additional horizontal spacing in points.</summary>
    public double HorizontalGap { get; }
    /// <summary>Gets additional vertical spacing in points.</summary>
    public double VerticalGap { get; }
    /// <summary>Gets explicit content fitting.</summary>
    public PdfCanvasFit Fit { get; }
}

/// <summary>A passively inspected stamp invocation with a validated owned-stream marker. Ownership metadata is document provenance, not authentication.</summary>
public sealed class PdfStampInfo
{
    internal PdfStampInfo(string identifier, int page, PdfStampContentKind kind, PdfStampOptions options)
    {
        Identifier = identifier;
        PageIndex = page;
        ContentKind = kind;
        Options = options;
    }

    /// <summary>Gets the exact caller-supplied ASCII identifier.</summary>
    public string Identifier { get; }
    /// <summary>Gets the current zero-based page index.</summary>
    public int PageIndex { get; }
    /// <summary>Gets the recorded source classification.</summary>
    public PdfStampContentKind ContentKind { get; }
    /// <summary>Gets the captured requested placement. Subsequent page geometry edits can change its displayed position.</summary>
    public PdfStampOptions Options { get; }
}

/// <summary>A detached passive inventory of recognized owned stamp streams.</summary>
public sealed class PdfStampInspection
{
    internal PdfStampInspection(IReadOnlyList<PdfStampInfo> stamps, PdfOperationReport report)
    {
        Stamps = stamps;
        OperationReport = report;
    }

    /// <summary>Gets stamps in page/content order.</summary>
    public IReadOnlyList<PdfStampInfo> Stamps { get; }
    /// <summary>Gets the complete bounded inspection report.</summary>
    public PdfOperationReport OperationReport { get; }
}
