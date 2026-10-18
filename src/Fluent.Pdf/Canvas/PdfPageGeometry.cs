namespace Fluent.Pdf;
/// <summary>The five standard page box selections.</summary>
public enum PdfPageBox
{
    /// <summary>The physical medium.</summary>
    Media,
    /// <summary>The default visible/printable region.</summary>
    Crop,
    /// <summary>The production bleed region.</summary>
    Bleed,
    /// <summary>The intended finished-page region.</summary>
    Trim,
    /// <summary>The meaningful artwork region.</summary>
    Art
}

/// <summary>Immutable finite standard page boxes in native PDF user coordinates.</summary>
public sealed class PdfPageBoxes : IEquatable<PdfPageBoxes>
{
    /// <summary>Captures positive media and optional boxes within it. Each omitted secondary box defaults to Crop, as specified by PDF.</summary>
    public PdfPageBoxes(PdfRect media, PdfRect? crop = null, PdfRect? bleed = null, PdfRect? trim = null, PdfRect? art = null)
    {
        if (media.Width <= 0 || media.Height <= 0)
            throw new ArgumentException("MediaBox must have positive finite extent.", nameof(media));
        Media = media;
        Crop = crop ?? media;
        Bleed = bleed ?? Crop;
        Trim = trim ?? Crop;
        Art = art ?? Crop;
        foreach (var box in new[]
        {
            Crop,
            Bleed,
            Trim,
            Art
        }

        )
            if (!Contains(media, box))
                throw new ArgumentException("Every secondary page box must lie within MediaBox.");
    }

    /// <summary>Gets MediaBox.</summary>
    public PdfRect Media { get; }
    /// <summary>Gets effective CropBox.</summary>
    public PdfRect Crop { get; }
    /// <summary>Gets effective BleedBox.</summary>
    public PdfRect Bleed { get; }
    /// <summary>Gets effective TrimBox.</summary>
    public PdfRect Trim { get; }
    /// <summary>Gets effective ArtBox.</summary>
    public PdfRect Art { get; }

    /// <summary>Gets an explicitly selected standard box.</summary>
    public PdfRect this[PdfPageBox box] => box switch
    {
        PdfPageBox.Media => Media,
        PdfPageBox.Crop => Crop,
        PdfPageBox.Bleed => Bleed,
        PdfPageBox.Trim => Trim,
        PdfPageBox.Art => Art,
        _ => throw new ArgumentOutOfRangeException(nameof(box))};
    /// <inheritdoc/>
    public bool Equals(PdfPageBoxes? other) => other != null && Same(Media, other.Media) && Same(Crop, other.Crop) && Same(Bleed, other.Bleed) && Same(Trim, other.Trim) && Same(Art, other.Art);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfPageBoxes other && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => unchecked(Media.X.GetHashCode() * 397 ^ Media.Y.GetHashCode() ^ Media.Width.GetHashCode() ^ Media.Height.GetHashCode());
    internal static bool Same(PdfRect a, PdfRect b) => a.X == b.X && a.Y == b.Y && a.Width == b.Width && a.Height == b.Height;
    internal static bool Contains(PdfRect outer, PdfRect inner) => inner.X >= outer.X && inner.Y >= outer.Y && inner.Right <= outer.Right && inner.Top <= outer.Top;
    internal PdfPageBoxes Resize(double width, double height)
    {
        var media = new PdfRect(Media.X, Media.Y, width, height);
        double sx = width / Media.Width, sy = height / Media.Height;
        PdfRect Map(PdfRect box)
        {
            if (Same(box, Media))
                return media;
            double x = box.X == Media.X ? media.X : Math.Max(media.X, Math.Min(media.Right, media.X + (box.X - Media.X) * sx));
            double y = box.Y == Media.Y ? media.Y : Math.Max(media.Y, Math.Min(media.Top, media.Y + (box.Y - Media.Y) * sy));
            double right = box.Right == Media.Right ? media.Right : Math.Max(x, Math.Min(media.Right, media.X + (box.Right - Media.X) * sx));
            double top = box.Top == Media.Top ? media.Top : Math.Max(y, Math.Min(media.Top, media.Y + (box.Top - Media.Y) * sy));
            double w = right - x, h = top - y;
            for (int i = 0; i < 4 && x + w > media.Right; i++)
                w = Previous(w);
            for (int i = 0; i < 4 && y + h > media.Top; i++)
                h = Previous(h);
            return new(x, y, w, h);
        }

        return new(media, Map(Crop), Map(Bleed), Map(Trim), Map(Art));
        double Previous(double value) => value > 0 ? BitConverter.Int64BitsToDouble(BitConverter.DoubleToInt64Bits(value) - 1) : 0;
    }
}

/// <summary>Immutable page boxes, native user-unit scale and clockwise viewing rotation.</summary>
public sealed class PdfPageGeometry : IEquatable<PdfPageGeometry>
{
    /// <summary>Captures page geometry. UserUnit is positive and at most 75000 physical points per native PDF unit.</summary>
    public PdfPageGeometry(PdfPageBoxes boxes, PdfPageRotation rotation = PdfPageRotation.None, double userUnit = 1)
    {
        Boxes = boxes ?? throw new ArgumentNullException(nameof(boxes));
        if (!Enum.IsDefined(typeof(PdfPageRotation), rotation))
            throw new ArgumentOutOfRangeException(nameof(rotation));
        PdfGraphicsNumbers.Finite(userUnit);
        if (userUnit <= 0 || userUnit > 75000)
            throw new ArgumentOutOfRangeException(nameof(userUnit));
        Rotation = rotation;
        UserUnit = userUnit;
        Size = new(boxes.Media.Width * userUnit, boxes.Media.Height * userUnit);
    }

    /// <summary>Gets the exact effective standard boxes in native user coordinates.</summary>
    public PdfPageBoxes Boxes { get; }
    /// <summary>Gets clockwise viewer rotation.</summary>
    public PdfPageRotation Rotation { get; }
    /// <summary>Gets physical points per native user unit.</summary>
    public double UserUnit { get; }
    /// <summary>Gets unrotated physical MediaBox dimensions in points.</summary>
    public PdfPageSize Size { get; }

    /// <summary>Returns raw PDF user coordinates to displayed top-left coordinates in physical points for the selected box.</summary>
    public PdfMatrix UserToDisplay(PdfPageBox box = PdfPageBox.Crop)
    {
        var b = Boxes[box];
        double u = UserUnit;
        return Rotation switch
        {
            PdfPageRotation.None => new(u, 0, 0, -u, -b.X * u, b.Top * u),
            PdfPageRotation.Clockwise90 => new(0, u, u, 0, -b.Y * u, -b.X * u),
            PdfPageRotation.Clockwise180 => new(-u, 0, 0, u, b.Right * u, -b.Y * u),
            PdfPageRotation.Clockwise270 => new(0, -u, -u, 0, b.Top * u, b.Right * u),
            _ => throw new InvalidOperationException()};
    }

    /// <summary>Returns displayed top-left point coordinates to native PDF user coordinates.</summary>
    public PdfMatrix DisplayToUser(PdfPageBox box = PdfPageBox.Crop) => UserToDisplay(box).Invert();
    /// <summary>Gets rotated displayed extents in physical points. An empty crop can have zero extent.</summary>
    public PdfRect DisplayBounds(PdfPageBox box = PdfPageBox.Crop)
    {
        var b = Boxes[box];
        return Rotation == PdfPageRotation.Clockwise90 || Rotation == PdfPageRotation.Clockwise270 ? new(0, 0, b.Height * UserUnit, b.Width * UserUnit) : new(0, 0, b.Width * UserUnit, b.Height * UserUnit);
    }

    /// <summary>Returns the same boxes and scale with another explicit rotation.</summary>
    public PdfPageGeometry WithRotation(PdfPageRotation rotation) => new(Boxes, rotation, UserUnit);
    /// <summary>Resizes MediaBox and proportionally maps every secondary box, retaining origin, rotation and UserUnit.</summary>
    public PdfPageGeometry Resize(PdfPageSize size)
    {
        Guard.Size(size, nameof(size));
        return new(Boxes.Resize(size.Width / UserUnit, size.Height / UserUnit), Rotation, UserUnit);
    }

    /// <inheritdoc/>
    public bool Equals(PdfPageGeometry? other) => other != null && Rotation == other.Rotation && UserUnit == other.UserUnit && Boxes.Equals(other.Boxes);
    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfPageGeometry other && Equals(other);
    /// <inheritdoc/>
    public override int GetHashCode() => unchecked(Boxes.GetHashCode() * 397 ^ UserUnit.GetHashCode() ^ (int)Rotation);
    internal static PdfPageGeometry Read(PdfSyntaxDocument source, PdfValue dictionary, PdfValue inherited, PdfRect media, PdfPageRotation rotation, double unit, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, 2048);
        var crop = Box("CropBox", media, true);
        return new(new PdfPageBoxes(media, crop, Box("BleedBox", crop, false), Box("TrimBox", crop, false), Box("ArtBox", crop, false)), rotation, unit);
        PdfRect Box(string key, PdfRect fallback, bool inheritable)
        {
            context.Checkpoint();
            var entries = inheritable ? inherited.Entries : dictionary.Entries;
            if (!entries.TryGetValue(key, out var raw))
                return fallback;
            var value = ManagedPdfProvider.Resolve(source, raw, context);
            if (value.Kind == PdfValueKind.Null)
                return fallback;
            if (value.Kind != PdfValueKind.Array || value.Items.Count != 4)
                throw Error();
            var c = new double[4];
            for (int i = 0; i < 4; i++)
            {
                var number = ManagedPdfProvider.Resolve(source, value.Items[i], context);
                if (number.Kind != PdfValueKind.Number)
                    throw Error();
                try
                {
                    c[i] = number.NumberValue;
                }
                catch (InvalidOperationException)
                {
                    throw Error();
                }
            }

            double x = Math.Min(c[0], c[2]), y = Math.Min(c[1], c[3]), right = Math.Max(c[0], c[2]), top = Math.Max(c[1], c[3]);
            PdfRect parsed;
            try
            {
                parsed = new(x, y, right - x, top - y);
            }
            catch (ArgumentOutOfRangeException)
            {
                throw Error();
            }

            if (PdfPageBoxes.Contains(media, parsed))
                return parsed;
            double left = Math.Max(media.X, parsed.X), bottom = Math.Max(media.Y, parsed.Y), endX = Math.Min(media.Right, parsed.Right), endY = Math.Min(media.Top, parsed.Top);
            if (endX < left)
                left = Math.Max(media.X, Math.Min(media.Right, left));
            if (endY < bottom)
                bottom = Math.Max(media.Y, Math.Min(media.Top, bottom));
            var effective = new PdfRect(left, bottom, Math.Max(0, endX - left), Math.Max(0, endY - bottom));
            context.Report(new PdfDiagnostic("Geometry.EffectivePageBox", "The standard effective page-box intersection with MediaBox is used; original syntax remains preserved.", severity: PdfDiagnosticSeverity.Information));
            return effective;
        }

        PdfException Error() => PdfWriterCore.Error(context, "Geometry.PageBox", "A page box must resolve to four finite numeric bounds.");
    }
}
