namespace Fluent.Pdf;
/// <summary>An immutable original page binding, including effective standard inheritable attributes.</summary>
public sealed class PdfPageSource
{
    internal PdfPageSource(PdfSyntaxDocument document, PdfObjectId id, PdfValue dictionary, PdfValue attributes, PdfPageSize size, PdfPageRotation rotation, double left, double bottom, double unit,PdfPageGeometry geometry)
    {
        Document = document;
        ObjectId = id;
        Dictionary = dictionary;
        EffectiveAttributes = attributes;
        Size = size;
        Rotation = rotation;
        Left = left;
        Bottom = bottom;
        UserUnit = unit;Geometry=geometry;
    }

    /// <summary>Gets the immutable source object graph.</summary>
    public PdfSyntaxDocument Document { get; }
    /// <summary>Gets the original page object identity.</summary>
    public PdfObjectId ObjectId { get; }
    /// <summary>Gets the original leaf dictionary with unknown entries preserved.</summary>
    public PdfValue Dictionary { get; }
    /// <summary>Gets effective MediaBox/CropBox/Resources/Rotate entries, retaining references into Document.</summary>
    public PdfValue EffectiveAttributes { get; }
    /// <summary>Gets the original unrotated physical size in points.</summary>
    public PdfPageSize Size { get; }
    /// <summary>Gets the normalized original display rotation.</summary>
    public PdfPageRotation Rotation { get; }
    /// <summary>Gets original effective page boxes, UserUnit and viewing rotation.</summary>
    public PdfPageGeometry Geometry{get;}
    internal double Left { get; }
    internal double Bottom { get; }
    internal double UserUnit { get; }
}
