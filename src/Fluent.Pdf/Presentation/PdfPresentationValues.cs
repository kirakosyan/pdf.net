namespace Fluent.Pdf;
internal static class PdfPresentationValues
{
    private static readonly HashSet<string> Booleans = new(StringComparer.Ordinal)
    {
        "HideToolbar",
        "HideMenubar",
        "HideWindowUI",
        "FitWindow",
        "CenterWindow",
        "DisplayDocTitle",
        "PickTrayByPDFSize"
    };
    private static readonly string[] Boxes =
    {
        "MediaBox",
        "CropBox",
        "BleedBox",
        "TrimBox",
        "ArtBox"
    };
    internal static void Warning(PdfNavigationReader reader, string code, string message) => reader.Context.Report(new PdfDiagnostic(code, message, severity: PdfDiagnosticSeverity.Warning));
    internal static void Preferences(PdfNavigationReader reader, PdfValue preferences, bool authoring)
    {
        if (preferences.Kind == PdfValueKind.Null && !authoring)
            return;
        if (preferences.Kind != PdfValueKind.Dictionary)
        {
            Invalid("Presentation.ViewerPreferences", "ViewerPreferences must be a dictionary.");
            return;
        }

        foreach (var pair in preferences.Entries)
        {
            reader.Context.Checkpoint();
            reader.Context.Consume(PdfResourceKind.Objects, 1);
            var value = reader.Resolve(pair.Value);
            bool known = true, valid;
            bool Name(params string[] choices) => value.Kind == PdfValueKind.Name && choices.Contains(value.NameValue, StringComparer.Ordinal);
            if (Booleans.Contains(pair.Key))
                valid = value.Kind == PdfValueKind.Boolean;
            else
                switch (pair.Key)
                {
                    case "NonFullScreenPageMode":
                        valid = Name("UseNone", "UseOutlines", "UseThumbs", "UseOC");
                        break;
                    case "Direction":
                        valid = Name("L2R", "R2L");
                        break;
                    case "ViewArea":
                    case "ViewClip":
                    case "PrintArea":
                    case "PrintClip":
                        valid = Name(Boxes);
                        break;
                    case "PrintScaling":
                        valid = Name("None", "AppDefault");
                        break;
                    case "Duplex":
                        valid = Name("Simplex", "DuplexFlipShortEdge", "DuplexFlipLongEdge");
                        break;
                    case "NumCopies":
                        valid = value.TryGetInteger(out long copies) && copies >= 1 && copies <= 100000;
                        break;
                    case "PrintPageRange":
                        valid = value.Kind == PdfValueKind.Array && (value.Items.Count & 1) == 0;
                        if (valid)
                            for (int i = 0; i < value.Items.Count; i += 2)
                            {
                                reader.Context.Checkpoint();
                                reader.Context.Consume(PdfResourceKind.Objects, 1);
                                if (!reader.Resolve(value.Items[i]).TryGetInteger(out long a) || !reader.Resolve(value.Items[i + 1]).TryGetInteger(out long b) || a < 1 || b < a || b > reader.Pages.Count)
                                {
                                    valid = false;
                                    break;
                                }
                            }

                        break;
                    case "Enforce":
                        valid = value.Kind == PdfValueKind.Array && value.Items.Count == 1 && reader.Resolve(value.Items[0]).Kind == PdfValueKind.Name && reader.Resolve(value.Items[0]).NameValue == "PrintScaling" && reader.Name(preferences, "PrintScaling") == "None";
                        break;
                    default:
                        valid = false;
                        known = false;
                        break;
                }

            if (!known)
                Invalid("Presentation.UnsupportedPreference", "An unsupported viewer preference is retained for explicit inspection.");
            else if (!valid)
                Invalid("Presentation.PreferenceValue", "A viewer preference has an unsupported or malformed value.");
        }

        void Invalid(string code, string message)
        {
            if (authoring)
                throw reader.Error(code, message);
            Warning(reader, code, message);
        }
    }

    internal static SortedDictionary<int, PdfValue> Labels(PdfNavigationReader reader)
    {
        var result = new SortedDictionary<int, PdfValue>();
        var seen = new HashSet<PdfObjectId>();
        int previous = -1;
        void Visit(PdfValue value)
        {
            using var depth = reader.Context.EnterScope(PdfResourceKind.Depth);
            reader.Context.Checkpoint();
            reader.Context.Consume(PdfResourceKind.Objects, 1);
            if (value.Kind == PdfValueKind.Reference)
            {
                reader.Context.Consume(PdfResourceKind.ExpandedBytes, 48);
                if (!seen.Add(value.ReferenceId))
                    throw reader.Error("Presentation.LabelCycle", "A page-label number tree repeats a node.");
            }

            value = reader.Resolve(value);
            if (value.Kind != PdfValueKind.Dictionary)
                throw reader.Error("Presentation.PageLabels", "Page-label number-tree nodes must be dictionaries.");
            var nums = reader.Get(value, "Nums");
            var kids = reader.Get(value, "Kids");
            if (nums.Kind != PdfValueKind.Null && kids.Kind != PdfValueKind.Null)
                throw reader.Error("Presentation.PageLabels", "Number-tree nodes cannot contain both Nums and Kids.");
            if (nums.Kind != PdfValueKind.Null)
            {
                if (nums.Kind != PdfValueKind.Array || (nums.Items.Count & 1) != 0)
                    throw reader.Error("Presentation.PageLabels", "Page labels require integer/dictionary pairs.");
                for (int i = 0; i < nums.Items.Count; i += 2)
                {
                    reader.Context.Checkpoint();
                    reader.Context.Consume(PdfResourceKind.Objects, 1);
                    reader.Context.Consume(PdfResourceKind.ExpandedBytes, 128);
                    if (!reader.Resolve(nums.Items[i]).TryGetInteger(out long n) || n < 0 || n >= reader.Pages.Count || n <= previous)
                        throw reader.Error("Presentation.PageLabels", "Page-label starts must be sorted distinct existing physical indices.");
                    var definition = reader.Resolve(nums.Items[i + 1]);
                    if (definition.Kind != PdfValueKind.Dictionary)
                        throw reader.Error("Presentation.PageLabels", "A label range must be a dictionary.");
                    previous = (int)n;
                    result.Add(previous, definition);
                }
            }

            if (kids.Kind != PdfValueKind.Null)
            {
                if (kids.Kind != PdfValueKind.Array || kids.Items.Count == 0)
                    throw reader.Error("Presentation.PageLabels", "Number-tree Kids must be a nonempty array.");
                foreach (var child in kids.Items)
                    Visit(child);
            }
        }

        var labels = reader.Get(reader.Catalog, "PageLabels");
        if (labels.Kind != PdfValueKind.Null)
            Visit(reader.Catalog.Entries["PageLabels"]);
        if (result.Count > 0 && result.First().Key != 0)
            throw reader.Error("Presentation.PageLabels", "Explicit page labels must begin at page zero.");
        return result;
    }

    internal static PdfPageNumbering? Numbering(PdfNavigationReader reader, SortedDictionary<int, PdfValue> labels)
    {
        if (labels.Count == 0)
            return new();
        var ranges = new List<PdfPageNumberRange>();
        bool supported = labels.Count <= 4096;
        foreach (var pair in labels)
        {
            reader.Context.Checkpoint();
            reader.Context.Consume(PdfResourceKind.ExpandedBytes, 256);
            string style = reader.Name(pair.Value, "S");
            var first = reader.Get(pair.Value, "St");
            var prefixValue = reader.Get(pair.Value, "P");
            var labelStyle = style switch
            {
                "D" => PdfPageLabelStyle.Decimal,
                "r" => PdfPageLabelStyle.LowerRoman,
                "R" => PdfPageLabelStyle.UpperRoman,
                "a" => PdfPageLabelStyle.LowerLetters,
                "A" => PdfPageLabelStyle.UpperLetters,
                _ => PdfPageLabelStyle.None
            };
            string prefix = prefixValue.Kind == PdfValueKind.String ? PdfTextStrings.Decode(prefixValue, reader.Context) : "";
            long number = 1;
            bool valid = first.Kind == PdfValueKind.Null || first.TryGetInteger(out number) && number >= 1 && number <= int.MaxValue;
            valid &= prefixValue.Kind == PdfValueKind.Null || prefixValue.Kind == PdfValueKind.String;
            valid &= style.Length == 0 && reader.Get(pair.Value, "S").Kind == PdfValueKind.Null || new[]
            {
                "D",
                "r",
                "R",
                "a",
                "A"
            }.Contains(style);
            if (!valid || prefix.Length > 128 || !supported)
            {
                supported = false;
                continue;
            }

            ranges.Add(new(pair.Key, labelStyle, (int)number, prefix));
        }

        if (supported)
        {
            var numbering = new PdfPageNumbering(ranges.ToArray());
            // Formatting has an explicit finite Roman/repeated-letter profile; preserve other ranges raw.
            for (int i = 0; i < ranges.Count; i++)
            {
                var range = ranges[i];
                long end = (long)range.FirstNumber + (i + 1 < ranges.Count ? ranges[i + 1].StartPageIndex : reader.Pages.Count) - 1 - range.StartPageIndex;
                if (end > int.MaxValue || (range.Style == PdfPageLabelStyle.LowerRoman || range.Style == PdfPageLabelStyle.UpperRoman) && end > 3999 || (range.Style == PdfPageLabelStyle.LowerLetters || range.Style == PdfPageLabelStyle.UpperLetters) && end > 3328)
                    supported = false;
            }

            if (supported)
                return numbering;
        }

        Warning(reader, "Presentation.UnsupportedPageLabels", "Label ranges outside the bounded formatter profile are preserved in the raw range inventory.");
        return null;
    }

    internal static void Transition(PdfNavigationReader reader, PdfValue value)
    {
        if (value.Kind == PdfValueKind.Null)
            return;
        string styleName = reader.Name(value, "S");
        if (styleName == nameof(PdfTransitionStyle.Replace))
        {
            Warning(reader, "Presentation.UnsupportedTransition", "The source transition name is not a standard PDF style and is preserved.");
            return;
        }

        if (styleName == "R" || styleName.Length == 0 && reader.Get(value, "S").Kind == PdfValueKind.Null)
            styleName = nameof(PdfTransitionStyle.Replace);
        if (value.Kind != PdfValueKind.Dictionary || !Enum.TryParse<PdfTransitionStyle>(styleName, out var style) || !Enum.IsDefined(typeof(PdfTransitionStyle), style))
        {
            Warning(reader, "Presentation.UnsupportedTransition", "An unsupported transition remains available in the raw page settings.");
            return;
        }

        var known = new HashSet<string>(StringComparer.Ordinal)
        {
            "Type",
            "S",
            "D",
            "Dm",
            "M",
            "Di",
            "SS",
            "B"
        };
        if (value.Entries.Keys.Any(k => !known.Contains(k)))
            Warning(reader, "Presentation.UnsupportedTransition", "Unknown transition keys remain available in the raw page settings.");
        foreach (var key in value.Entries.Keys)
        {
            var v = reader.Get(value, key);
            bool valid = true;
            if (key == "D" || key == "SS")
                valid = v.Kind == PdfValueKind.Number && Finite(v) && v.NumberValue >= 0;
            if (key == "Dm")
                valid = v.Kind == PdfValueKind.Name && (v.NameValue == "H" || v.NameValue == "V") && (style == PdfTransitionStyle.Split || style == PdfTransitionStyle.Blinds);
            if (key == "M")
                valid = v.Kind == PdfValueKind.Name && (v.NameValue == "I" || v.NameValue == "O") && (style == PdfTransitionStyle.Split || style == PdfTransitionStyle.Box || style == PdfTransitionStyle.Fly);
            if (key == "B")
                valid = v.Kind == PdfValueKind.Boolean && style == PdfTransitionStyle.Fly;
            if (key == "SS")
                valid &= style == PdfTransitionStyle.Fly;
            if (key == "Type")
                valid = v.Kind == PdfValueKind.Name && v.NameValue == "Trans";
            if (key == "Di")
            {
                double angle = v.Kind == PdfValueKind.Number && Finite(v) ? v.NumberValue : double.NaN;
                bool standard = angle == 0 || angle == 270;
                valid = style == PdfTransitionStyle.Wipe ? standard || angle == 90 || angle == 180 : style == PdfTransitionStyle.Glitter ? standard || angle == 315 : style == PdfTransitionStyle.Fly ? standard || v.Kind == PdfValueKind.Name && v.NameValue == "None" : (style == PdfTransitionStyle.Push || style == PdfTransitionStyle.Cover || style == PdfTransitionStyle.Uncover) && standard;
            }

            if (!valid)
                Warning(reader, "Presentation.UnsupportedTransition", "A transition parameter is outside its supported style/value profile and is preserved.");
        }

        static bool Finite(PdfValue v)
        {
            try
            {
                _ = v.NumberValue;
                return true;
            }
            catch (InvalidOperationException)
            {
                return false;
            }
        }
    }
}
