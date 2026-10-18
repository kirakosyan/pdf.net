namespace Fluent.Pdf;
// Original feature facts. Opaque custom content must supply its explicit minimum version.
internal static class PdfFeatureVersion
{
    internal static int Dictionary(PdfValue value, Func<PdfValue, PdfValue> resolve)
    {
        int floor = 12;
        var entries = value.Entries;
        string Name(string key)
        {
            if (!entries.TryGetValue(key, out var item))
                return "";
            item = resolve(item);
            return item.Kind == PdfValueKind.Name ? item.NameValue : "";
        }

        string type = Name("Type"), subtype = Name("Subtype");
        if (subtype == "Image" && entries.ContainsKey("Mask")) floor = Math.Max(floor, 13);
        if (type == "Metadata") floor = Math.Max(floor, 14);
        if (subtype == "Image" && entries.TryGetValue("BitsPerComponent", out var precision) && resolve(precision).Kind == PdfValueKind.Number && resolve(precision).TryGetInteger(out long bits) && bits == 16) floor = Math.Max(floor, 15);
        if (type == "ObjStm" || type == "XRef" || type == "OCG" || type == "OCMD")
            floor = 15;
        if(entries.ContainsKey("ShadingType")||entries.ContainsKey("OPM")||entries.TryGetValue("PatternType",out var patternType)&&patternType.Kind==PdfValueKind.Number&&patternType.NumberValue==2)floor=Math.Max(floor,13);
        if(entries.ContainsKey("ActualText"))floor=Math.Max(floor,14);
        if (entries.ContainsKey("CA") || entries.ContainsKey("ca") || entries.ContainsKey("BM") || entries.ContainsKey("SMask"))
            floor = Math.Max(floor, 14);
        if (type == "Group" && Name("S") == "Transparency" || entries.ContainsKey("SMask"))
            floor = Math.Max(floor, 14);
        if (type == "Page" && entries.ContainsKey("UserUnit"))
            floor = Math.Max(floor, 16);
        if (value.Kind == PdfValueKind.Stream && subtype == "OpenType")
            floor = Math.Max(floor, 16);
        if (type == "Namespace" || type == "StructTreeRoot" && entries.ContainsKey("Namespaces") || entries.ContainsKey("UseBlackPtComp"))
            floor = 20;
        if (entries.TryGetValue("Filter", out var filters))
        {
            filters = resolve(filters);
            if (filters.Kind == PdfValueKind.Name)
                Filter(filters.NameValue);
            else if (filters.Kind == PdfValueKind.Array)
                foreach (var original in filters.Items)
                {
                    var item = resolve(original);
                    if (item.Kind == PdfValueKind.Name)
                        Filter(item.NameValue);
                }
        }

        return floor;
        void Filter(string name)
        {
            if (name == "JPXDecode" || name == "Crypt")
                floor = Math.Max(floor, 15);
            else if (name == "JBIG2Decode")
                floor = Math.Max(floor, 14);
        }
    }
    internal static int Array(PdfValue value,Func<PdfValue,PdfValue> resolve)
    {
        if(value.Items.Count==0)return 12;var first=resolve(value.Items[0]);if(first.Kind!=PdfValueKind.Name)return 12;
        if(first.NameValue=="ICCBased")return 13;
        if(first.NameValue=="DeviceN"){var names=value.Items.Count>1?resolve(value.Items[1]):PdfValue.Null;return value.Items.Count>4||names.Kind==PdfValueKind.Array&&names.Items.Count>8?16:13;}
        return 12;
    }
}
