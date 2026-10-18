namespace Fluent.Pdf;
/// <summary>Standard PDF content-stream operators; unknown tokens require an explicit preservation policy.</summary>
public enum PdfOperator
{
    /// <summary>An explicitly preserved extension token.</summary>
    Unknown,
    /// <summary>Save graphics state: q.</summary>
    SaveState,
    /// <summary>Restore graphics state: Q.</summary>
    RestoreState,
    /// <summary>Concatenate a transformation: cm.</summary>
    Transform,
    /// <summary>Set line width: w.</summary>
    LineWidth,
    /// <summary>Set line cap: J.</summary>
    LineCap,
    /// <summary>Set line join: j.</summary>
    LineJoin,
    /// <summary>Set miter limit: M.</summary>
    MiterLimit,
    /// <summary>Set dash pattern: d.</summary>
    Dash,
    /// <summary>Set rendering intent: ri.</summary>
    RenderingIntent,
    /// <summary>Set flatness: i.</summary>
    Flatness,
    /// <summary>Apply named extended graphics state: gs.</summary>
    ExtendedState,
    /// <summary>Move the current path point: m.</summary>
    MoveTo,
    /// <summary>Add a line: l.</summary>
    LineTo,
    /// <summary>Add a cubic curve: c.</summary>
    CurveTo,
    /// <summary>Add a curve with its first control point at the current point: v.</summary>
    CurveFromCurrent,
    /// <summary>Add a curve with its final control point at its endpoint: y.</summary>
    CurveToEndpoint,
    /// <summary>Close the current subpath: h.</summary>
    ClosePath,
    /// <summary>Add a rectangle: re.</summary>
    Rectangle,
    /// <summary>Stroke the path: S.</summary>
    Stroke,
    /// <summary>Close and stroke: s.</summary>
    CloseStroke,
    /// <summary>Fill with nonzero winding: f.</summary>
    Fill,
    /// <summary>Legacy nonzero fill spelling: F.</summary>
    LegacyFill,
    /// <summary>Fill with even-odd winding: f*.</summary>
    FillEvenOdd,
    /// <summary>Fill and stroke: B.</summary>
    FillStroke,
    /// <summary>Even-odd fill and stroke: B*.</summary>
    FillStrokeEvenOdd,
    /// <summary>Close, fill and stroke: b.</summary>
    CloseFillStroke,
    /// <summary>Close, even-odd fill and stroke: b*.</summary>
    CloseFillStrokeEvenOdd,
    /// <summary>End the path without painting: n.</summary>
    EndPath,
    /// <summary>Apply nonzero clipping at path end: W.</summary>
    Clip,
    /// <summary>Apply even-odd clipping at path end: W*.</summary>
    ClipEvenOdd,
    /// <summary>Begin a text object: BT.</summary>
    BeginText,
    /// <summary>End a text object: ET.</summary>
    EndText,
    /// <summary>Set character spacing: Tc.</summary>
    CharacterSpacing,
    /// <summary>Set word spacing: Tw.</summary>
    WordSpacing,
    /// <summary>Set horizontal text scale: Tz.</summary>
    HorizontalScale,
    /// <summary>Set text leading: TL.</summary>
    Leading,
    /// <summary>Select a font and size: Tf.</summary>
    Font,
    /// <summary>Set text rendering mode: Tr.</summary>
    TextRenderingMode,
    /// <summary>Set text rise: Ts.</summary>
    TextRise,
    /// <summary>Move the text line origin: Td.</summary>
    MoveText,
    /// <summary>Move the text line origin and set leading: TD.</summary>
    MoveTextLeading,
    /// <summary>Set text and text-line matrices: Tm.</summary>
    TextMatrix,
    /// <summary>Move to the next text line: T*.</summary>
    NextTextLine,
    /// <summary>Show encoded glyph bytes: Tj.</summary>
    ShowText,
    /// <summary>Show bytes and positioning adjustments: TJ.</summary>
    ShowPositionedText,
    /// <summary>Move to the next line and show text: apostrophe.</summary>
    NextLineShowText,
    /// <summary>Set spacing, move to the next line and show text: quotation mark.</summary>
    SpacedNextLineShowText,
    /// <summary>Set stroke color space: CS.</summary>
    StrokeColorSpace,
    /// <summary>Set fill color space: cs.</summary>
    FillColorSpace,
    /// <summary>Set stroke components: SC.</summary>
    StrokeColor,
    /// <summary>Set extended stroke components/pattern: SCN.</summary>
    StrokeColorExtended,
    /// <summary>Set fill components: sc.</summary>
    FillColor,
    /// <summary>Set extended fill components/pattern: scn.</summary>
    FillColorExtended,
    /// <summary>Set stroke gray: G.</summary>
    StrokeGray,
    /// <summary>Set fill gray: g.</summary>
    FillGray,
    /// <summary>Set stroke RGB: RG.</summary>
    StrokeRgb,
    /// <summary>Set fill RGB: rg.</summary>
    FillRgb,
    /// <summary>Set stroke CMYK: K.</summary>
    StrokeCmyk,
    /// <summary>Set fill CMYK: k.</summary>
    FillCmyk,
    /// <summary>Paint a shading: sh.</summary>
    Shading,
    /// <summary>Paint a named external object: Do.</summary>
    DrawObject,
    /// <summary>Set Type 3 glyph width: d0.</summary>
    GlyphWidth,
    /// <summary>Set Type 3 glyph width and bounds: d1.</summary>
    GlyphBounds,
    /// <summary>Mark a content point: MP.</summary>
    MarkPoint,
    /// <summary>Mark a point with properties: DP.</summary>
    MarkPointProperties,
    /// <summary>Begin marked content: BMC.</summary>
    BeginMarkedContent,
    /// <summary>Begin marked content with properties: BDC.</summary>
    BeginMarkedContentProperties,
    /// <summary>End marked content: EMC.</summary>
    EndMarkedContent,
    /// <summary>Begin a compatibility section: BX.</summary>
    BeginCompatibility,
    /// <summary>End a compatibility section: EX.</summary>
    EndCompatibility,
    /// <summary>An opaque inline image with separately captured dictionary and data.</summary>
    InlineImage
}

/// <summary>How a content parser handles unknown operator tokens.</summary>
public enum PdfUnknownOperatorPolicy
{
    /// <summary>Reject unknown tokens.</summary>
    Reject,
    /// <summary>Preserve their exact token and operands without interpreting or executing them.</summary>
    Preserve
}
