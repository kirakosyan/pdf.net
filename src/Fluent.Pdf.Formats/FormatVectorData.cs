using System.Globalization;

namespace Fluent.Pdf.Formats;
// Closed data emitted by our vector-page bridge. No SVG stylesheet/resource interpretation occurs here.
internal static class FormatVectorData
{
    internal static List<PdfPathSegment> Path(string text, PdfOperationContext c)
    {
        int at = 0;
        var result = new List<PdfPathSegment>();
        while (true)
        {
            Space();
            if (at == text.Length)
                return result;
            c.Checkpoint();
            c.Consume(PdfResourceKind.Objects, 1);
            c.Consume(PdfResourceKind.ExpandedBytes, 1024);
            char command = text[at++];
            result.Add(command switch
            {
                'M' => PdfPathSegment.Move(N(), N()),
                'L' => PdfPathSegment.Line(N(), N()),
                'C' => PdfPathSegment.Curve(N(), N(), N(), N(), N(), N()),
                'Z' => PdfPathSegment.Close(),
                _ => throw FormatIo.Error(c, "VectorBridge", "Unexpected command in the closed vector bridge.")});
        }

        void Space()
        {
            while (at < text.Length && (char.IsWhiteSpace(text[at]) || text[at] == ','))
                at++;
        }

        double N()
        {
            Space();
            int start = at;
            while (at < text.Length && !char.IsWhiteSpace(text[at]) && text[at] != ',')
            {
                if (++at - start > 128)
                    throw FormatIo.Error(c, "VectorBridge", "A vector scalar exceeds its finite extent.");
            }

            if (at == start)
                throw FormatIo.Error(c, "VectorBridge", "A vector coordinate is missing.");
            return FormatIo.Number(text.Substring(start, at - start), c);
        }
    }

    internal static PdfMatrix Matrix(string text, PdfOperationContext c)
    {
        if (!text.StartsWith("matrix(", StringComparison.Ordinal) || !text.EndsWith(")", StringComparison.Ordinal) || text.Length > 1024)
            throw FormatIo.Error(c, "VectorBridge", "An explicit six-component affine transform is required.");
        c.Consume(PdfResourceKind.ExpandedBytes, 4096);
        var values = text.Substring(7, text.Length - 8).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
        if (values.Length != 6)
            throw FormatIo.Error(c, "VectorBridge", "An exact affine transform is required.");
        var a = values.Select(v => FormatIo.Number(v, c)).ToArray();
        return new(a[0], a[1], a[2], a[3], a[4], a[5]);
    }

    internal static double[] Color(string text, PdfOperationContext c)
    {
        if (!text.StartsWith("rgb(", StringComparison.Ordinal) || !text.EndsWith(")", StringComparison.Ordinal) || text.Length > 512)
            throw FormatIo.Error(c, "VectorBridge", "A closed RGB percentage paint is required.");
        c.Consume(PdfResourceKind.ExpandedBytes, 2048);
        var values = text.Substring(4, text.Length - 5).Split(',');
        if (values.Length != 3)
            throw FormatIo.Error(c, "VectorBridge", "Three explicit paint components are required.");
        return values.Select(v =>
        {
            if (!v.EndsWith("%", StringComparison.Ordinal))
                throw FormatIo.Error(c, "VectorBridge", "Percentage paint components are required.");
            double value = FormatIo.Number(v.Substring(0, v.Length - 1), c) / 100;
            if (value < 0 || value > 1)
                throw FormatIo.Error(c, "VectorBridge", "Unit-range paint components are required.");
            return value;
        }).ToArray();
    }
}
