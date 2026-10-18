namespace Fluent.Pdf;
// Original UAX #29 extended grapheme state machine, including Indic conjunct and emoji/RI rules.
internal static class PdfGraphemeBreaker
{
    internal static PdfGraphemeCluster[] Break(IReadOnlyList<PdfUnicodeScalar> scalars, IReadOnlyList<PdfUnicodeData.Property> properties, PdfOperationContext context)
    {
        if (scalars.Count == 0)
            return System.Array.Empty<PdfGraphemeCluster>();
        context.Consume(PdfResourceKind.ExpandedBytes, scalars.Count * 32L + 64);
        var result = new List<PdfGraphemeCluster>();
        int start = 0, regional = properties[0].Break == PdfUnicodeData.Grapheme.RegionalIndicator ? 1 : 0;
        bool pictographic = properties[0].Pictographic, previousZwjAfterPictographic = false, linker = properties[0].IndicConjunct == 3;
        for (int i = 1; i < scalars.Count; i++)
        {
            context.Checkpoint();
            var before = properties[i - 1];
            var current = properties[i];
            var a = before.Break;
            var b = current.Break;
            bool join;
            if (a == PdfUnicodeData.Grapheme.CR && b == PdfUnicodeData.Grapheme.LF)
                join = true;
            else if (Control(a) || Control(b))
                join = false;
            else if (a == PdfUnicodeData.Grapheme.L && (b == PdfUnicodeData.Grapheme.L || b == PdfUnicodeData.Grapheme.V || b == PdfUnicodeData.Grapheme.LV || b == PdfUnicodeData.Grapheme.LVT))
                join = true;
            else if ((a == PdfUnicodeData.Grapheme.LV || a == PdfUnicodeData.Grapheme.V) && (b == PdfUnicodeData.Grapheme.V || b == PdfUnicodeData.Grapheme.T))
                join = true;
            else if ((a == PdfUnicodeData.Grapheme.LVT || a == PdfUnicodeData.Grapheme.T) && b == PdfUnicodeData.Grapheme.T)
                join = true;
            else if (b == PdfUnicodeData.Grapheme.Extend || b == PdfUnicodeData.Grapheme.ZWJ || b == PdfUnicodeData.Grapheme.SpacingMark || a == PdfUnicodeData.Grapheme.Prepend)
                join = true;
            else if (current.IndicConjunct == 1 && linker)
                join = true;
            else if (current.Pictographic && a == PdfUnicodeData.Grapheme.ZWJ && previousZwjAfterPictographic)
                join = true;
            else
                join = a == PdfUnicodeData.Grapheme.RegionalIndicator && b == PdfUnicodeData.Grapheme.RegionalIndicator && regional % 2 == 1;
            if (!join)
            {
                Add(i);
                start = i;
            }

            previousZwjAfterPictographic = b == PdfUnicodeData.Grapheme.ZWJ && pictographic;
            if (current.Pictographic)
                pictographic = true;
            else if (b != PdfUnicodeData.Grapheme.Extend)
                pictographic = false;
            regional = b == PdfUnicodeData.Grapheme.RegionalIndicator ? regional + 1 : 0;
            if (current.IndicConjunct == 3)
                linker = true;
            else if (current.IndicConjunct != 2)
                linker = false;
        }

        Add(scalars.Count);
        return result.ToArray();
        void Add(int end)
        {
            context.Consume(PdfResourceKind.Objects, 1);
            var last = scalars[end - 1];
            result.Add(new PdfGraphemeCluster(start, end - start, scalars[start].Utf16Offset, last.Utf16Offset + last.Utf16Length - scalars[start].Utf16Offset));
        }
    }

    private static bool Control(PdfUnicodeData.Grapheme value) => value == PdfUnicodeData.Grapheme.Control || value == PdfUnicodeData.Grapheme.CR || value == PdfUnicodeData.Grapheme.LF;
}
