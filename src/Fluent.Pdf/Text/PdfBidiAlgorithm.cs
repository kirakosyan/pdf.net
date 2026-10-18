namespace Fluent.Pdf;
// Original UAX #9 implementation. Index arrays preserve logical provenance; no display text is rewritten.
internal static class PdfBidiAlgorithm
{
    internal sealed class Result
    {
        internal Result(int[] levels, PdfBidiParagraph[] paragraphs, int[] visual)
        {
            Levels = levels;
            Paragraphs = paragraphs;
            Visual = visual;
        }

        internal int[] Levels { get; }
        internal PdfBidiParagraph[] Paragraphs { get; }
        internal int[] Visual { get; }
    }

    internal static Result Resolve(int[] codepoints, PdfBidiClass[] original, PdfParagraphDirection direction, PdfUnicodeData data, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, original.Length * 128L + 512);
        var levels = new int[original.Length];
        var visual = new List<int>();
        var paragraphs = new List<PdfBidiParagraph>();
        int start = 0;
        while (start < original.Length)
        {
            context.Checkpoint();
            int end = start;
            while (end < original.Length && original[end] != PdfBidiClass.B)
                end++;
            if (end < original.Length)
            {
                end++;
                if (end < original.Length && codepoints[end - 1] == 13 && codepoints[end] == 10)
                    end++;
            }

            int[] points = new int[end - start];
            var types = new PdfBidiClass[end - start];
            System.Array.Copy(codepoints, start, points, 0, points.Length);
            System.Array.Copy(original, start, types, 0, types.Length);
            var paragraph = ResolveParagraph(points, types, direction, data, context);
            System.Array.Copy(paragraph.Levels, 0, levels, start, paragraph.Levels.Length);
            foreach (int index in paragraph.Visual)
                visual.Add(start + index);
            paragraphs.Add(new PdfBidiParagraph(start, end - start, paragraph.Paragraphs[0].EmbeddingLevel));
            start = end;
        }

        return new Result(levels, paragraphs.ToArray(), visual.ToArray());
    }

    internal static Result ResolveParagraph(int[] codepoints, PdfBidiClass[] original, PdfParagraphDirection direction, PdfUnicodeData data, PdfOperationContext context)
    {
        int count = original.Length;
        context.Consume(PdfResourceKind.ExpandedBytes, count * 160L + 1024);
        var types = (PdfBidiClass[])original.Clone();
        var levels = new int[count];
        var matching = Enumerable.Repeat(-1, count).ToArray();
        var firstStrong = new PdfBidiClass[count];
        var isolates = new Stack<(int Index, PdfBidiClass Strong)>();
        PdfBidiClass paragraphStrong = PdfBidiClass.ON;
        for (int i = 0; i < count; i++)
        {
            context.Checkpoint();
            var type = original[i];
            if (Isolate(type))
            {
                isolates.Push((i, PdfBidiClass.ON));
                continue;
            }

            if (type == PdfBidiClass.PDI)
            {
                if (isolates.Count > 0)
                {
                    var frame = isolates.Pop();
                    matching[frame.Index] = i;
                    firstStrong[frame.Index] = frame.Strong;
                }

                continue;
            }

            if (type != PdfBidiClass.L && type != PdfBidiClass.R && type != PdfBidiClass.AL)
                continue;
            if (isolates.Count == 0)
            {
                if (paragraphStrong == PdfBidiClass.ON)
                    paragraphStrong = type;
            }
            else
            {
                var frame = isolates.Pop();
                if (frame.Strong == PdfBidiClass.ON)
                    frame.Strong = type;
                isolates.Push(frame);
            }
        }

        while (isolates.Count > 0)
        {
            var frame = isolates.Pop();
            firstStrong[frame.Index] = frame.Strong;
        }

        int paragraphLevel = direction == PdfParagraphDirection.RightToLeft ? 1 : direction == PdfParagraphDirection.LeftToRight ? 0 : paragraphStrong == PdfBidiClass.R || paragraphStrong == PdfBidiClass.AL ? 1 : 0;
        var embeddings = new Stack<(int Level, PdfBidiClass Override, bool Isolate)>();
        embeddings.Push((paragraphLevel, PdfBidiClass.ON, false));
        int overflowIsolates = 0, overflowEmbeddings = 0, validIsolates = 0;
        for (int i = 0; i < count; i++)
        {
            context.Checkpoint();
            var type = original[i];
            var current = embeddings.Peek();
            levels[i] = current.Level;
            if (type == PdfBidiClass.RLE || type == PdfBidiClass.LRE || type == PdfBidiClass.RLO || type == PdfBidiClass.LRO)
            {
                bool right = type == PdfBidiClass.RLE || type == PdfBidiClass.RLO;
                int next = right ? (current.Level + 1) | 1 : (current.Level + 2) & ~1;
                if (next <= 125 && overflowIsolates == 0 && overflowEmbeddings == 0)
                    embeddings.Push((next, type == PdfBidiClass.RLO ? PdfBidiClass.R : type == PdfBidiClass.LRO ? PdfBidiClass.L : PdfBidiClass.ON, false));
                else if (overflowIsolates == 0)
                    overflowEmbeddings++;
                continue;
            }

            if (Isolate(type))
            {
                if (current.Override != PdfBidiClass.ON)
                    types[i] = current.Override;
                bool right = type == PdfBidiClass.RLI || type == PdfBidiClass.FSI && (firstStrong[i] == PdfBidiClass.R || firstStrong[i] == PdfBidiClass.AL);
                int next = right ? (current.Level + 1) | 1 : (current.Level + 2) & ~1;
                if (next <= 125 && overflowIsolates == 0 && overflowEmbeddings == 0)
                {
                    embeddings.Push((next, PdfBidiClass.ON, true));
                    validIsolates++;
                }
                else
                    overflowIsolates++;
                continue;
            }

            if (type == PdfBidiClass.PDI)
            {
                if (overflowIsolates > 0)
                    overflowIsolates--;
                else if (validIsolates > 0)
                {
                    overflowEmbeddings = 0;
                    while (embeddings.Count > 1 && !embeddings.Peek().Isolate)
                        embeddings.Pop();
                    if (embeddings.Count > 1)
                        embeddings.Pop();
                    validIsolates--;
                }

                current = embeddings.Peek();
                levels[i] = current.Level;
                if (current.Override != PdfBidiClass.ON)
                    types[i] = current.Override;
                continue;
            }

            if (type == PdfBidiClass.PDF)
            {
                if (overflowIsolates == 0)
                {
                    if (overflowEmbeddings > 0)
                        overflowEmbeddings--;
                    else if (embeddings.Count > 1 && !embeddings.Peek().Isolate)
                        embeddings.Pop();
                }

                continue;
            }

            if (type == PdfBidiClass.B)
            {
                levels[i] = paragraphLevel;
                continue;
            }

            if (type != PdfBidiClass.BN && current.Override != PdfBidiClass.ON)
                types[i] = current.Override;
        }

        var active = new List<int>();
        var activePosition = Enumerable.Repeat(-1, count).ToArray();
        for (int i = 0; i < count; i++)
            if (!Removed(original[i]))
            {
                activePosition[i] = active.Count;
                active.Add(i);
            }

        var runs = new List<List<int>>();
        var runOf = Enumerable.Repeat(-1, count).ToArray();
        int previousLevel = -1;
        foreach (int index in active)
        {
            if (levels[index] != previousLevel)
            {
                runs.Add(new List<int>());
                previousLevel = levels[index];
            }

            runs[runs.Count - 1].Add(index);
            runOf[index] = runs.Count - 1;
        }

        var nextRun = Enumerable.Repeat(-1, runs.Count).ToArray();
        var predecessor = new bool[runs.Count];
        for (int i = 0; i < runs.Count; i++)
        {
            int last = runs[i][runs[i].Count - 1];
            if (Isolate(original[last]) && matching[last] >= 0)
            {
                int next = runOf[matching[last]];
                if (next >= 0 && next != i)
                {
                    nextRun[i] = next;
                    predecessor[next] = true;
                }
            }
        }

        context.Consume(PdfResourceKind.ExpandedBytes, count * 4L);
        var explicitLevels = (int[])levels.Clone();
        for (int root = 0; root < runs.Count; root++)
        {
            if (predecessor[root])
                continue;
            context.Checkpoint();
            var sequence = new List<int>();
            int run = root;
            while (run >= 0)
            {
                sequence.AddRange(runs[run]);
                run = nextRun[run];
            }

            if (sequence.Count == 0)
                continue;
            int first = sequence[0], last = sequence[sequence.Count - 1], before = activePosition[first] > 0 ? explicitLevels[active[activePosition[first] - 1]] : paragraphLevel, after = Isolate(original[last]) ? paragraphLevel : activePosition[last] + 1 < active.Count ? explicitLevels[active[activePosition[last] + 1]] : paragraphLevel;
            var sos = Direction(Math.Max(levels[first], before));
            var eos = Direction(Math.Max(levels[last], after));
            ResolveSequence(sequence, types, original, codepoints, levels, sos, eos, data, context);
        }

        for (int i = 0; i < count; i++)
        {
            context.Checkpoint();
            if (original[i] != PdfBidiClass.B && original[i] != PdfBidiClass.S)
                continue;
            levels[i] = paragraphLevel;
            for (int j = i - 1; j >= 0 && Reset(original[j]); j--)
                levels[j] = paragraphLevel;
        }

        for (int i = count - 1; i >= 0 && Reset(original[i]); i--)
            levels[i] = paragraphLevel;
        foreach (int index in Enumerable.Range(0, count))
            if (Removed(original[index]))
                levels[index] = -1;
        int[] order = active.ToArray();
        int maximum = 0, minimumOdd = int.MaxValue;
        foreach (int index in order)
        {
            maximum = Math.Max(maximum, levels[index]);
            if ((levels[index] & 1) != 0)
                minimumOdd = Math.Min(minimumOdd, levels[index]);
        }

        for (int level = maximum; level >= minimumOdd; level--)
        {
            int i = 0;
            while (i < order.Length)
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
                System.Array.Reverse(order, begin, i - begin);
            }
        }

        return new Result(levels, new[] { new PdfBidiParagraph(0, count, paragraphLevel) }, order);
    }

    private static void ResolveSequence(List<int> sequence, PdfBidiClass[] types, PdfBidiClass[] original, int[] codepoints, int[] levels, PdfBidiClass sos, PdfBidiClass eos, PdfUnicodeData data, PdfOperationContext context)
    {
        var previous = sos;
        foreach (int index in sequence)
        {
            context.Checkpoint();
            if (types[index] == PdfBidiClass.NSM)
                types[index] = Isolate(previous) || previous == PdfBidiClass.PDI ? PdfBidiClass.ON : previous;
            previous = types[index];
        }

        var strong = sos;
        foreach (int index in sequence)
        {
            if (types[index] == PdfBidiClass.EN && strong == PdfBidiClass.AL)
                types[index] = PdfBidiClass.AN;
            if (types[index] == PdfBidiClass.L || types[index] == PdfBidiClass.R || types[index] == PdfBidiClass.AL)
                strong = types[index];
        }

        foreach (int index in sequence)
            if (types[index] == PdfBidiClass.AL)
                types[index] = PdfBidiClass.R;
        for (int i = 1; i + 1 < sequence.Count; i++)
        {
            int index = sequence[i];
            var a = types[sequence[i - 1]];
            var b = types[sequence[i + 1]];
            if (types[index] == PdfBidiClass.ES && a == PdfBidiClass.EN && b == PdfBidiClass.EN)
                types[index] = PdfBidiClass.EN;
            else if (types[index] == PdfBidiClass.CS && a == b && (a == PdfBidiClass.EN || a == PdfBidiClass.AN))
                types[index] = a;
        }

        for (int i = 0; i < sequence.Count;)
        {
            context.Checkpoint();
            if (types[sequence[i]] != PdfBidiClass.ET)
            {
                i++;
                continue;
            }

            int start = i++;
            while (i < sequence.Count && types[sequence[i]] == PdfBidiClass.ET)
                i++;
            bool number = start > 0 && types[sequence[start - 1]] == PdfBidiClass.EN || i < sequence.Count && types[sequence[i]] == PdfBidiClass.EN;
            if (number)
                for (int j = start; j < i; j++)
                    types[sequence[j]] = PdfBidiClass.EN;
        }

        foreach (int index in sequence)
            if (types[index] == PdfBidiClass.ES || types[index] == PdfBidiClass.ET || types[index] == PdfBidiClass.CS)
                types[index] = PdfBidiClass.ON;
        strong = sos;
        foreach (int index in sequence)
        {
            if (types[index] == PdfBidiClass.EN && strong == PdfBidiClass.L)
                types[index] = PdfBidiClass.L;
            if (types[index] == PdfBidiClass.L || types[index] == PdfBidiClass.R)
                strong = types[index];
        }

        var brackets = new List<(int Position, int Partner)>();
        var pairs = new List<(int Open, int Close)>();
        for (int i = 0; i < sequence.Count; i++)
        {
            context.Checkpoint();
            int index = sequence[i];
            if (types[index] != PdfBidiClass.ON || !data.Bracket(codepoints[index], out int partner, out bool open))
                continue;
            if (open)
            {
                if (brackets.Count == 63)
                    break;
                brackets.Add((i, Canonical(partner)));
            }
            else
            {
                int expected = Canonical(codepoints[index]);
                for (int j = brackets.Count - 1; j >= 0; j--)
                    if (brackets[j].Partner == expected)
                    {
                        pairs.Add((brackets[j].Position, i));
                        brackets.RemoveRange(j, brackets.Count - j);
                        break;
                    }
            }
        }

        pairs.Sort((a, b) => a.Open.CompareTo(b.Open));
        foreach (var pair in pairs)
        {
            context.Checkpoint();
            var embedding = Direction(levels[sequence[pair.Open]]);
            var opposite = embedding == PdfBidiClass.L ? PdfBidiClass.R : PdfBidiClass.L;
            bool same = false, other = false;
            for (int i = pair.Open + 1; i < pair.Close; i++)
            {
                var value = Strong(types[sequence[i]]);
                same |= value == embedding;
                other |= value == opposite;
            }

            PdfBidiClass resolved;
            if (same)
                resolved = embedding;
            else if (other)
            {
                var preceding = sos;
                for (int i = pair.Open - 1; i >= 0; i--)
                {
                    var value = Strong(types[sequence[i]]);
                    if (value == PdfBidiClass.L || value == PdfBidiClass.R)
                    {
                        preceding = value;
                        break;
                    }
                }

                resolved = preceding == opposite ? opposite : embedding;
            }
            else
                continue;
            Set(pair.Open, resolved);
            Set(pair.Close, resolved);
        }

        for (int i = 0; i < sequence.Count;)
        {
            context.Checkpoint();
            if (!Neutral(types[sequence[i]]))
            {
                i++;
                continue;
            }

            int start = i++;
            while (i < sequence.Count && Neutral(types[sequence[i]]))
                i++;
            var before = start == 0 ? sos : Strong(types[sequence[start - 1]]);
            var after = i == sequence.Count ? eos : Strong(types[sequence[i]]);
            var resolved = before == after ? before : Direction(levels[sequence[start]]);
            for (int j = start; j < i; j++)
                types[sequence[j]] = resolved;
        }

        foreach (int index in sequence)
        {
            context.Checkpoint();
            if ((levels[index] & 1) == 0)
            {
                if (types[index] == PdfBidiClass.R)
                    levels[index]++;
                else if (types[index] == PdfBidiClass.EN || types[index] == PdfBidiClass.AN)
                    levels[index] += 2;
            }
            else if (types[index] == PdfBidiClass.L || types[index] == PdfBidiClass.EN || types[index] == PdfBidiClass.AN)
                levels[index]++;
        }

        void Set(int position, PdfBidiClass value)
        {
            types[sequence[position]] = value;
            for (int i = position + 1; i < sequence.Count && original[sequence[i]] == PdfBidiClass.NSM; i++)
                types[sequence[i]] = value;
        }
    }

    private static PdfBidiClass Strong(PdfBidiClass value) => value == PdfBidiClass.EN || value == PdfBidiClass.AN ? PdfBidiClass.R : value;
    private static PdfBidiClass Direction(int level) => (level & 1) == 0 ? PdfBidiClass.L : PdfBidiClass.R;
    private static bool Isolate(PdfBidiClass value) => value == PdfBidiClass.LRI || value == PdfBidiClass.RLI || value == PdfBidiClass.FSI;
    internal static bool Removed(PdfBidiClass value) => value == PdfBidiClass.LRE || value == PdfBidiClass.RLE || value == PdfBidiClass.LRO || value == PdfBidiClass.RLO || value == PdfBidiClass.PDF || value == PdfBidiClass.BN;
    private static bool Neutral(PdfBidiClass value) => value == PdfBidiClass.B || value == PdfBidiClass.S || value == PdfBidiClass.WS || value == PdfBidiClass.ON || Isolate(value) || value == PdfBidiClass.PDI;
    private static bool Reset(PdfBidiClass value) => value == PdfBidiClass.WS || Isolate(value) || value == PdfBidiClass.PDI || Removed(value);
    private static int Canonical(int scalar) => scalar == 0x2329 ? 0x3008 : scalar == 0x232a ? 0x3009 : scalar;
}
