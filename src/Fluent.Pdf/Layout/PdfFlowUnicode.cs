namespace Fluent.Pdf;
// Preserve paragraph resolution across wrapped lines, then apply line rules L1/L2 to the selected scalar interval.
internal static class PdfFlowUnicode
{
    internal static PdfUnicodeAnalysis Line(PdfUnicodeAnalysis paragraph, int start, int length, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, length * 160L + 512);
        var selected = paragraph.Scalars.Where(s => s.Utf16Offset >= start && s.Utf16Offset < start + length).ToArray();
        int first = paragraph.Scalars.Count;
        for (int i = 0; i < paragraph.Scalars.Count; i++)
            if (paragraph.Scalars[i].Utf16Offset >= start)
            {
                first = i;
                break;
            }

        int baseLevel = 0;
        foreach (var p in paragraph.Paragraphs)
            if (first >= p.FirstScalar && first < p.FirstScalar + p.ScalarCount)
            {
                baseLevel = p.EmbeddingLevel;
                break;
            }

        var levels = selected.Select(s => s.EmbeddingLevel).ToArray();
        for (int i = levels.Length - 1; i >= 0 && Reset(selected[i].BidiClass); i--)
            if (levels[i] >= 0)
                levels[i] = baseLevel;
        for (int i = 0; i < levels.Length; i++)
            if (selected[i].BidiClass == PdfBidiClass.S || selected[i].BidiClass == PdfBidiClass.B)
            {
                levels[i] = baseLevel;
                for (int j = i - 1; j >= 0 && Reset(selected[j].BidiClass); j--)
                    if (levels[j] >= 0)
                        levels[j] = baseLevel;
            }

        var order = Enumerable.Range(0, levels.Length).Where(i => levels[i] >= 0).ToArray();
        int maximum = 0, minimumOdd = int.MaxValue;
        foreach (int i in order)
        {
            maximum = Math.Max(maximum, levels[i]);
            if ((levels[i] & 1) != 0)
                minimumOdd = Math.Min(minimumOdd, levels[i]);
        }

        for (int level = maximum; level >= minimumOdd; level--)
        {
            for (int i = 0; i < order.Length;)
            {
                context.Checkpoint();
                if (levels[order[i]] < level)
                {
                    i++;
                    continue;
                }

                int begin = i++;
                while (i < order.Length && levels[order[i]] >= level)
                    i++;
                Array.Reverse(order, begin, i - begin);
            }
        }

        var scalars = selected.Select((s, i) => new PdfUnicodeScalar(s.Value, s.Utf16Offset - start, s.Utf16Length, s.BidiClass, s.ScriptTag, levels[i], s.DefaultIgnorable)).ToArray();
        var clusters = paragraph.Graphemes.Where(g => g.Utf16Offset >= start && g.Utf16Offset < start + length).Select(g => new PdfGraphemeCluster(g.FirstScalar - first, g.ScalarCount, g.Utf16Offset - start, g.Utf16Length)).ToArray();
        return new(paragraph.Text.Substring(start, length), scalars, clusters, new[] { new PdfBidiParagraph(0, scalars.Length, baseLevel) }, order);
    }

    private static bool Reset(PdfBidiClass c) => c == PdfBidiClass.WS || c == PdfBidiClass.S || c == PdfBidiClass.B || c == PdfBidiClass.BN || c == PdfBidiClass.LRI || c == PdfBidiClass.RLI || c == PdfBidiClass.FSI || c == PdfBidiClass.PDI || c == PdfBidiClass.LRE || c == PdfBidiClass.RLE || c == PdfBidiClass.LRO || c == PdfBidiClass.RLO || c == PdfBidiClass.PDF;
}
