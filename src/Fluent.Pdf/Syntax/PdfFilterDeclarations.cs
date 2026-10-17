namespace Fluent.Pdf;
internal static class PdfFilterDeclarations
{
    private static readonly HashSet<string> KnownParameters = new(StringComparer.Ordinal)
    {
        "Predictor",
        "Colors",
        "BitsPerComponent",
        "Columns",
        "EarlyChange",
        "K",
        "Rows",
        "EndOfLine",
        "EncodedByteAlign",
        "EndOfBlock",
        "BlackIs1",
        "DamagedRowsBeforeError",
        "ColorTransform",
        "JBIG2Globals"
    };
    internal static PdfValue Resolve(PdfValue stream, Func<PdfObjectId, PdfValue> resolver, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.Objects, 1);
        var entries = stream.Entries.ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
        foreach (string key in new[]
        {
            "Filter",
            "DecodeParms"
        }

        )
            if (entries.TryGetValue(key, out var value))
                entries[key] = Value(value, resolver, context, new HashSet<PdfObjectId>());
        return stream.WithDictionary(PdfValue.OwnedDictionary(entries));
    }

    private static PdfValue Value(PdfValue value, Func<PdfObjectId, PdfValue> resolver, PdfOperationContext context, HashSet<PdfObjectId> path)
    {
        context.Checkpoint();
        using var scope = context.EnterScope(PdfResourceKind.Depth);
        if (value.Kind == PdfValueKind.Reference)
        {
            var id = value.ReferenceId;
            if (!path.Add(id))
                throw PdfStreamFilters.Error(context, "Filter.ParameterCycle", "Indirect filter parameters contain a cycle.");
            try
            {
                return Value(resolver(id), resolver, context, path);
            }
            finally
            {
                path.Remove(id);
            }
        }

        if (value.Kind == PdfValueKind.Array)
        {
            context.Consume(PdfResourceKind.Objects, 1);
            var result = new List<PdfValue>();
            foreach (var item in value.Items)
                result.Add(Value(item, resolver, context, path));
            return PdfValue.OwnedArray(result);
        }

        if (value.Kind == PdfValueKind.Dictionary)
        {
            context.Consume(PdfResourceKind.Objects, 1);
            var entries = value.Entries.ToDictionary(x => x.Key, x => KnownParameters.Contains(x.Key) ? Value(x.Value, resolver, context, path) : x.Value, StringComparer.Ordinal);
            return PdfValue.OwnedDictionary(entries);
        }

        return value;
    }
}
