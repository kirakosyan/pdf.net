using System.Text.Json;

namespace Fluent.Pdf.CommandLine;
internal sealed class CliFailure(string code, int exitCode) : Exception
{
    internal string Code { get; } = code;
    internal int ExitCode { get; } = exitCode;
}

internal sealed record Job(string Id, string Operation, string? Input, string Output, string Pages, string View, string? Target, string? PasswordFile, bool Overwrite, bool AllowSignatureInvalidation);
internal sealed record Settings(Job[] Jobs, int Parallelism, int MaximumInputBytes, int MaximumOutputBytes, int TimeoutMilliseconds, string? Report, bool OverwriteReport, bool Batch);
internal static class Configuration
{
    private static readonly HashSet<string> Operations = new(StringComparer.Ordinal)
    {
        "text",
        "select",
        "rewrite",
        "preflight",
        "detect",
        "convert",
        "decrypt",
        "formats"
    };
    private static readonly string[] JobKeys = ["id", "operation", "input", "output", "pages", "view", "target", "passwordFile", "overwrite", "allowSignatureInvalidation"];
    internal static Settings Parse(string[] args)
    {
        if (args.Length == 0 || args.Length > 32 || args.Any(a => a.Length > 4096))
            throw Invalid();
        string command = args[0];
        if (command is "run" or "batch")
        {
            if (args.Length != 3 || args[1] != "--config")
                throw Invalid();
            return Read(args[2], command == "batch");
        }

        if (!Operations.Contains(command))
            throw Invalid();
        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["id"] = "single",
            ["operation"] = command
        };
        int input = 16777216, output = 16777216, timeout = 30000;
        for (int i = 1; i < args.Length; i++)
        {
            string key = args[i] switch
            {
                "--input" => "input",
                "--output" => "output",
                "--pages" => "pages",
                "--view" => "view",
                "--target" => "target",
                "--password-file" => "passwordFile",
                "--overwrite" => "overwrite",
                "--allow-signature-invalidation" => "allowSignatureInvalidation",
                "--max-input-bytes" => "maximumInputBytes",
                "--max-output-bytes" => "maximumOutputBytes",
                "--timeout-ms" => "timeoutMilliseconds",
                _ => throw Invalid()};
            string value = key is "overwrite" or "allowSignatureInvalidation" ? "true" : ++i < args.Length ? args[i] : throw Invalid();
            if (!values.TryAdd(key, value))
                throw Invalid();
        }

        if (values.Remove("maximumInputBytes", out var a))
            input = Number(a, 1, 67108864);
        if (values.Remove("maximumOutputBytes", out var b))
            output = Number(b, 1, 67108864);
        if (values.Remove("timeoutMilliseconds", out var c))
            timeout = Number(c, 1, 300000);
        return Validate(new([MakeJob(values, Environment.CurrentDirectory)], 1, input, output, timeout, null, false, false));
    }

    private static Settings Read(string path, bool batch)
    {
        try
        {
            return ReadCore(path, batch);
        }
        catch (Exception error)when (error is JsonException or InvalidOperationException or FormatException)
        {
            throw Invalid();
        }
    }

    private static Settings ReadCore(string path, bool batch)
    {
        string absolute = Path.GetFullPath(path), directory = Path.GetDirectoryName(absolute)!;
        using var file = File.OpenRead(absolute);
        if (file.Length > 1048576)
            throw Invalid();
        using var content = new MemoryStream();
        var buffer = new byte[8192];
        while (true)
        {
            int count = file.Read(buffer, 0, (int)Math.Min(buffer.Length, 1048577 - content.Length));
            if (count == 0)
                break;
            content.Write(buffer, 0, count);
            if (content.Length > 1048576)
                throw Invalid();
        }

        using var json = JsonDocument.Parse(content.ToArray(), new() { MaxDepth = 16 });
        var root = Object(json.RootElement, ["schemaVersion", "parallelism", "maximumInputBytes", "maximumOutputBytes", "timeoutMilliseconds", "report", "overwriteReport", "jobs"]);
        if (Integer(root, "schemaVersion", 0, 1, 1) != 1 || !root.TryGetValue("jobs", out var list) || list.ValueKind != JsonValueKind.Array || list.GetArrayLength()is < 1 or > 256 || !batch && list.GetArrayLength() != 1)
            throw Invalid();
        var jobs = new List<Job>();
        foreach (var item in list.EnumerateArray())
        {
            var fields = Object(item, JobKeys);
            var values = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in fields)
            {
                if (pair.Key is "overwrite" or "allowSignatureInvalidation")
                    values[pair.Key] = Boolean(pair.Value) ? "true" : "false";
                else
                {
                    if (pair.Value.ValueKind != JsonValueKind.String)
                        throw Invalid();
                    values[pair.Key] = pair.Value.GetString()!;
                }
            }

            jobs.Add(MakeJob(values, directory));
        }

        string? report = root.TryGetValue("report", out var reportValue) && reportValue.ValueKind == JsonValueKind.String ? Resolve(reportValue.GetString()!, directory) : null;
        if (root.ContainsKey("report") && report == null || batch && report == null)
            throw Invalid();
        return Validate(new(jobs.ToArray(), Integer(root, "parallelism", 1, 1, batch ? 4 : 1), Integer(root, "maximumInputBytes", 16777216, 1, 67108864), Integer(root, "maximumOutputBytes", 16777216, 1, 67108864), Integer(root, "timeoutMilliseconds", 30000, 1, 300000), report, root.TryGetValue("overwriteReport", out var overwrite) && Boolean(overwrite), batch));
    }

    private static Dictionary<string, JsonElement> Object(JsonElement element, string[] allowed)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Invalid();
        var result = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
            if (!allowed.Contains(property.Name, StringComparer.Ordinal) || !result.TryAdd(property.Name, property.Value))
                throw Invalid();
        return result;
    }

    private static Job MakeJob(Dictionary<string, string> values, string directory)
    {
        string Text(string name, string? fallback = null)
        {
            string? value = values.TryGetValue(name, out var supplied) ? supplied : fallback;
            if (string.IsNullOrEmpty(value) || value.Length > 4096 || value.Contains('\0'))
                throw Invalid();
            return value;
        }

        string id = Text("id"), operation = Text("operation");
        if (id.Length > 64 || id.Any(ch => !(ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '_' or '-')) || !Operations.Contains(operation))
            throw Invalid();
        string? input = operation == "formats" ? null : Resolve(Text("input"), directory);
        if (operation == "formats" && values.ContainsKey("input"))
            throw Invalid();
        string output = Resolve(Text("output", "-"), directory), pages = Text("pages", "all"), view = Text("view", "raw");
        string? target = values.GetValueOrDefault("target"), password = values.GetValueOrDefault("passwordFile");
        if (view is not ("raw" or "semantic" or "layout") || values.ContainsKey("view") && operation != "text" || values.ContainsKey("pages") && operation is not ("text" or "select" or "convert"))
            throw Invalid();
        if (operation == "convert" ? string.IsNullOrEmpty(target) : target != null)
            throw Invalid();
        if (operation == "decrypt" ? string.IsNullOrEmpty(password) : password != null)
            throw Invalid();
        bool overwrite = values.GetValueOrDefault("overwrite") == "true", signatures = values.GetValueOrDefault("allowSignatureInvalidation") == "true";
        if (overwrite && output == "-" || signatures && operation is not ("select" or "rewrite" or "decrypt"))
            throw Invalid();
        return new(id, operation, input, output, pages, view, target, password == null ? null : Resolve(password, directory), overwrite, signatures);
    }

    private static Settings Validate(Settings settings)
    {
        // Conservatively reject case/canonical-Unicode aliases even on hosts
        // whose filesystem would allow distinct files with those spellings.
        var comparer = StringComparer.OrdinalIgnoreCase;
        static string Key(string path) => path.Normalize(System.Text.NormalizationForm.FormC);
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var inputs = new HashSet<string>(comparer);
        var outputs = new HashSet<string>(comparer);
        foreach (var job in settings.Jobs)
        {
            if (!ids.Add(job.Id) || settings.Batch && (job.Input == "-" || job.Output == "-"))
                throw Invalid();
            if (job.Input != null && job.Input != "-")
                inputs.Add(Key(job.Input));
            if (job.PasswordFile != null)
            {
                if (job.PasswordFile == "-")
                    throw Invalid();
                inputs.Add(Key(job.PasswordFile));
            }

            if (job.Output != "-" && !outputs.Add(Key(job.Output)))
                throw Invalid();
        }

        if (settings.Report != null && (!outputs.Add(Key(settings.Report)) || settings.Report == "-" || File.Exists(settings.Report) && !settings.OverwriteReport))
            throw Invalid();
        if (inputs.Overlaps(outputs))
            throw Invalid();
        return settings;
    }

    private static string Resolve(string value, string directory) => value == "-" ? "-" : value.Length is> 0 and <= 4096 ? Path.GetFullPath(value, directory) : throw Invalid();
    private static int Number(string text, int minimum, int maximum) => int.TryParse(text, System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out int value) && value >= minimum && value <= maximum ? value : throw Invalid();
    private static int Integer(Dictionary<string, JsonElement> value, string key, int fallback, int minimum, int maximum) => !value.TryGetValue(key, out var item) ? fallback : item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out int number) && number >= minimum && number <= maximum ? number : throw Invalid();
    private static bool Boolean(JsonElement value) => value.ValueKind is JsonValueKind.True or JsonValueKind.False ? value.GetBoolean() : throw Invalid();
    private static CliFailure Invalid() => new("CLI.Configuration", 2);
}
