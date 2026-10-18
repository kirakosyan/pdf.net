namespace Fluent.Pdf;
/// <summary>Standard passive page transition styles.</summary>
public enum PdfTransitionStyle
{
    /// <summary>Immediate replacement.</summary>
    Replace,
    /// <summary>Split.</summary>
    Split,
    /// <summary>Blinds.</summary>
    Blinds,
    /// <summary>Box.</summary>
    Box,
    /// <summary>Wipe.</summary>
    Wipe,
    /// <summary>Dissolve.</summary>
    Dissolve,
    /// <summary>Glitter.</summary>
    Glitter,
    /// <summary>Fly.</summary>
    Fly,
    /// <summary>Push.</summary>
    Push,
    /// <summary>Cover.</summary>
    Cover,
    /// <summary>Uncover.</summary>
    Uncover,
    /// <summary>Fade.</summary>
    Fade
}

/// <summary>Transition split/blind dimension.</summary>
public enum PdfTransitionDimension
{
    /// <summary>Horizontal.</summary>
    Horizontal,
    /// <summary>Vertical.</summary>
    Vertical
}

/// <summary>Transition motion.</summary>
public enum PdfTransitionMotion
{
    /// <summary>Inward.</summary>
    Inward,
    /// <summary>Outward.</summary>
    Outward
}

/// <summary>An immutable transition request; authoring never runs an animation.</summary>
public sealed class PdfPageTransition
{
    /// <summary>Creates a finite standard transition. Direction -1 denotes nondirectional Fly. Optional parameters must apply to the chosen style.</summary>
    public PdfPageTransition(PdfTransitionStyle style, double duration = 1, PdfTransitionDimension? dimension = null, PdfTransitionMotion? motion = null, int? direction = null, double? scale = null, bool? rectangular = null)
    {
        if (!Enum.IsDefined(typeof(PdfTransitionStyle), style))
            throw new ArgumentOutOfRangeException(nameof(style));
        if (double.IsNaN(duration) || double.IsInfinity(duration) || duration < 0 || duration > 86400)
            throw new ArgumentOutOfRangeException(nameof(duration));
        if (dimension.HasValue && (!Enum.IsDefined(typeof(PdfTransitionDimension), dimension.Value) || style != PdfTransitionStyle.Split && style != PdfTransitionStyle.Blinds))
            throw new ArgumentException("Dimension applies only to Split/Blinds.", nameof(dimension));
        if (motion.HasValue && (!Enum.IsDefined(typeof(PdfTransitionMotion), motion.Value) || style != PdfTransitionStyle.Split && style != PdfTransitionStyle.Box && style != PdfTransitionStyle.Fly))
            throw new ArgumentException("Motion applies only to Split/Box/Fly.", nameof(motion));
        if (direction.HasValue)
        {
            bool standard = direction == 0 || direction == 270;
            bool wipe = standard || direction == 90 || direction == 180;
            bool valid = style == PdfTransitionStyle.Glitter ? direction == 0 || direction == 270 || direction == 315 : style == PdfTransitionStyle.Fly ? standard || direction == -1 : style == PdfTransitionStyle.Wipe ? wipe : (style == PdfTransitionStyle.Push || style == PdfTransitionStyle.Cover || style == PdfTransitionStyle.Uncover) && standard;
            if (!valid)
                throw new ArgumentException("Direction must be a standard angle applicable to the style.", nameof(direction));
        }

        if (scale.HasValue && (style != PdfTransitionStyle.Fly || double.IsNaN(scale.Value) || double.IsInfinity(scale.Value) || scale < 0))
            throw new ArgumentOutOfRangeException(nameof(scale));
        if (rectangular.HasValue && style != PdfTransitionStyle.Fly)
            throw new ArgumentException("Rectangular applies only to Fly.", nameof(rectangular));
        Style = style;
        Duration = duration;
        Dimension = dimension;
        Motion = motion;
        Direction = direction;
        Scale = scale;
        Rectangular = rectangular;
    }

    /// <summary>Gets the standard transition style.</summary>
    public PdfTransitionStyle Style { get; }
    /// <summary>Gets the finite transition duration in seconds.</summary>
    public double Duration { get; }
    /// <summary>Gets the optional dimension.</summary>
    public PdfTransitionDimension? Dimension { get; }
    /// <summary>Gets the optional inward/outward motion.</summary>
    public PdfTransitionMotion? Motion { get; }
    /// <summary>Gets the optional angle or nondirectional Fly marker.</summary>
    public int? Direction { get; }
    /// <summary>Gets the optional Fly scale.</summary>
    public double? Scale { get; }
    /// <summary>Gets the optional Fly rectangular-area setting.</summary>
    public bool? Rectangular { get; }

    internal PdfValue Value()
    {
        var values = new Dictionary<string, PdfValue>(StringComparer.Ordinal)
        {
            ["Type"] = PdfValue.Name("Trans"),
            ["S"] = PdfValue.Name(Style == PdfTransitionStyle.Replace ? "R" : Style.ToString()),
            ["D"] = PdfValues.Real(Duration)
        };
        if (Dimension.HasValue)
            values["Dm"] = PdfValue.Name(Dimension == PdfTransitionDimension.Horizontal ? "H" : "V");
        if (Motion.HasValue)
            values["M"] = PdfValue.Name(Motion == PdfTransitionMotion.Inward ? "I" : "O");
        if (Direction.HasValue)
            values["Di"] = Direction == -1 ? PdfValue.Name("None") : PdfValue.Integer(Direction.Value);
        if (Scale.HasValue)
            values["SS"] = PdfValues.Real(Scale.Value);
        if (Rectangular.HasValue)
            values["B"] = PdfValue.Boolean(Rectangular.Value);
        return PdfValue.OwnedDictionary(values);
    }
}

/// <summary>Standard catalog page layouts.</summary>
public enum PdfPageLayout
{
    /// <summary>Single page.</summary>
    SinglePage,
    /// <summary>One continuous column.</summary>
    OneColumn,
    /// <summary>Two columns with odd pages left.</summary>
    TwoColumnLeft,
    /// <summary>Two columns with odd pages right.</summary>
    TwoColumnRight,
    /// <summary>Two page spreads with odd pages left.</summary>
    TwoPageLeft,
    /// <summary>Two page spreads with odd pages right.</summary>
    TwoPageRight
}

/// <summary>Standard initial viewer page modes.</summary>
public enum PdfPageMode
{
    /// <summary>No navigation panel.</summary>
    UseNone,
    /// <summary>Outline panel.</summary>
    UseOutlines,
    /// <summary>Thumbnail panel.</summary>
    UseThumbs,
    /// <summary>Full screen.</summary>
    FullScreen,
    /// <summary>Optional content panel.</summary>
    UseOC,
    /// <summary>Attachments panel.</summary>
    UseAttachments
}
