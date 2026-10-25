using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Xml.Linq;

// Analysis-only metadata inspection: no inspected SDK code or attributes execute.
if (args.Length is < 2 or > 3 || args.Length == 3 && args[2] != "--check")
    throw new ArgumentException("Supply repository root, evidence directory and optional --check.");
string root = Path.GetFullPath(args[0]), evidence = Path.GetFullPath(args[1]);
bool check = args.Length == 3;
using var readiness = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "specs/release-readiness.json")));
string version = readiness.RootElement.GetProperty("packageVersion").GetString()!;
var projects = Directory.GetDirectories(Path.Combine(root, "src")).Where(p => File.Exists(Path.Combine(p, "PublicAPI.Shipped.txt"))).Order(StringComparer.Ordinal).ToArray();
var assemblyPaths = projects.Select(p => Path.Combine(p, "bin/Release/net10.0", Path.GetFileName(p) + ".dll")).ToArray();
if (assemblyPaths.Any(p => !File.Exists(p)))
    throw new InvalidOperationException("Build every SDK assembly before reference generation.");
var dependencies = projects.SelectMany(p => Directory.GetFiles(Path.Combine(p, "bin/Release/net10.0"), "*.dll"));
var paths = assemblyPaths.Concat(dependencies).Concat(Directory.GetFiles(RuntimeEnvironment.GetRuntimeDirectory(), "*.dll")).GroupBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(g => g.First()).ToArray();
using var metadata = new MetadataLoadContext(new PathAssemblyResolver(paths), "System.Private.CoreLib");
string destination = Path.Combine(root, "docs/reference");
Directory.CreateDirectory(destination);
Directory.CreateDirectory(evidence);
var rows = new List<object>();
var index = new StringBuilder("# Source API reference\n\nGenerated from the current public API baselines and compiled XML comments, using metadata-only inspection. All SDK contracts target net472, netstandard2.0, net8.0 and net10.0; assembly/provider applicability still applies. See the [developer guide](../developer-guide.md) and [API boundary contract](../api-contract.md). No package or production qualification is implied.\n\n| Assembly | Source version | Public declarations | Documented members |\n| --- | --- | --- | --- |\n");
foreach (string project in projects)
{
    string name = Path.GetFileName(project), binary = Path.Combine(project, "bin/Release/net10.0", name + ".dll"), xml = Path.ChangeExtension(binary, ".xml");
    var assembly = metadata.LoadFromAssemblyPath(binary);
    if (assembly.GetName().Version!.ToString(3) != version)
        throw new InvalidOperationException("Documentation/package version drift: " + name);
    var baselinePaths = new[]
    {
        Path.Combine(project, "PublicAPI.Shipped.txt"),
        Path.Combine(project, "PublicAPI.Unshipped.txt")
    };
    var signatures = baselinePaths.SelectMany(File.ReadAllLines).Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith('#')).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    var exported = assembly.GetExportedTypes();
    var ids = new HashSet<string>(StringComparer.Ordinal);
    foreach (var type in exported)
    {
        ids.Add("T:" + Owner(type));
        foreach (var field in type.GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(f => !f.IsSpecialName))
            ids.Add("F:" + Owner(type) + "." + field.Name);
        foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            ids.Add("P:" + Owner(type) + "." + property.Name + Parameters(property.GetIndexParameters()));
        foreach (var item in type.GetEvents(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            ids.Add("E:" + Owner(type) + "." + item.Name);
        if (type.BaseType?.FullName == "System.MulticastDelegate")
            continue; // Delegate signature is documented on its type declaration.
        foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly).Where(m => !m.IsSpecialName || m.Name.StartsWith("op_", StringComparison.Ordinal)))
        {
            string member = "M:" + Owner(type) + "." + method.Name + (method.IsGenericMethodDefinition ? "``" + method.GetGenericArguments().Length : "") + Parameters(method.GetParameters());
            if (method.Name is "op_Implicit" or "op_Explicit")
                member += "~" + ParameterType(method.ReturnType);
            ids.Add(member);
        }

        foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            ids.Add("M:" + Owner(type) + ".#ctor" + Parameters(constructor.GetParameters()));
    }

    using var input = System.Xml.XmlReader.Create(xml, new() { DtdProcessing = System.Xml.DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 16777216 });
    var comments = XDocument.Load(input).Descendants("member").ToDictionary(e => (string)e.Attribute("name")!, StringComparer.Ordinal);
    var synthesized = new List<string>();
    foreach (var type in exported)
    {
        string constructor = "M:" + Owner(type) + ".#ctor";
        if (ids.Contains(constructor) && !comments.ContainsKey(constructor) && comments.TryGetValue("T:" + Owner(type), out var typeComment))
        {
            comments.Add(constructor, new XElement("member", new XAttribute("name", constructor), new XElement("summary", "Public parameterless construction. " + Render(typeComment.Element("summary") ?? new XElement("summary")))));
            synthesized.Add(constructor);
        }
    }

    var missing = ids.Where(id => !comments.ContainsKey(id)).Order(StringComparer.Ordinal).ToArray();
    if (missing.Length != 0)
        throw new InvalidOperationException("Missing public XML documentation or unsupported ID mapping in " + name + ": " + string.Join(", ", missing.Take(10)));
    var text = new StringBuilder("# " + name + " source reference\n\nVersion `" + version + "`. These are source contracts; exact final packages and production qualification remain pending. Optional providers and platform prerequisites are declared in the [developer guide](../developer-guide.md). Unsupported/lossy behavior retains the [API boundary rules](../api-contract.md).\n\n## Public declarations\n\nThe baseline preserves exact nullability, parameter names, defaults and constant values.\n\n```text\n" + string.Join('\n', signatures) + "\n```\n\n## Member documentation\n\n");
    foreach (string id in ids.Order(StringComparer.Ordinal))
    {
        var comment = comments[id];
        text.Append("### <code>").Append(System.Net.WebUtility.HtmlEncode(id)).Append("</code>\n\n");
        foreach (var item in comment.Elements())
        {
            string value = Render(item).Trim();
            if (value.Length == 0)
                continue;
            if (item.Name == "param" || item.Name == "typeparam")
                text.Append("- `").Append((string? )item.Attribute("name")).Append("`: ");
            else if (item.Name == "returns" || item.Name == "remarks")
                text.Append(item.Name == "returns" ? "Returns: " : "Remarks: ");
            text.Append(value).Append("\n\n");
        }
    }

    string content = text.ToString();
    Write(name + ".md", content);
    index.Append("| [").Append(name).Append("](").Append(name).Append(".md) | ").Append(version).Append(" | ").Append(signatures.Length).Append(" | ").Append(ids.Count).Append(" |\n");
    rows.Add(new { assembly = name, version, publicDeclarations = signatures.Length, documentedMembers = ids.Count, implicitConstructorDescriptions = synthesized, assemblySha256 = Hash(binary), xmlSha256 = Hash(xml), baselines = baselinePaths.Select(p => new { path = Path.GetRelativePath(root, p).Replace('\\', '/'), sha256 = Hash(p) }), outputSha256 = HashText(content) });
}

Write("index.md", index.ToString());
using var requirements = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(root, "specs/requirements.json")));
var deferredRequirements = readiness.RootElement.TryGetProperty("requirementExceptions", out var releaseExceptions)
    ? releaseExceptions.EnumerateArray().Select(e => e.GetProperty("requirement").GetString()!).ToHashSet(StringComparer.Ordinal)
    : new HashSet<string>(StringComparer.Ordinal);
var matrix = new StringBuilder("# Required capability/source matrix\n\nThis is declared requirement/source progress, not runtime capability or qualification. The catalogue retains 111 SDK requirements; the independent Reader is excluded. Explicit version-specific deferrals are shown below and documented in the [release exception](../release-exceptions.md). A deferral is not qualification. Exact provider/format applicability is in the linked feature documentation.\n\n| Requirement | SDK release | Status | Source progress |\n| --- | --- | --- | --- |\n");
foreach (var feature in requirements.RootElement.GetProperty("features").EnumerateArray())
{
    string id = feature.GetProperty("id").GetString()!, title = feature.GetProperty("title").GetString()!;
    string progress = "Full source/qualification pending";
    if (feature.TryGetProperty("implementationProgress", out var implementation) && implementation.TryGetProperty("sourceImplementation", out var source))
    {
        string path = source.GetString()!;
        if (!path.StartsWith("docs/", StringComparison.Ordinal) || !File.Exists(Path.Combine(root, path)))
            throw new InvalidOperationException("Invalid source progress link.");
        progress = "[Declared source profile](../" + path[5..] + ")";
    }

    string releaseScope = !feature.GetProperty("releaseRequired").GetBoolean() ? "Excluded Reader"
        : deferredRequirements.Contains(id) ? "Deferred for " + version : "Required";
    matrix.Append("| [").Append(id).Append(" ").Append(title.Replace("|", "\\|", StringComparison.Ordinal)).Append("](../requirements/").Append(id).Append(".md) | ").Append(releaseScope).Append(" | ").Append(feature.GetProperty("status").GetString()).Append(" | ").Append(progress).Append(" |\n");
}

Write("requirements.md", matrix.ToString());
File.WriteAllText(Path.Combine(evidence, "api-reference.json"), JsonSerializer.Serialize(new { schemaVersion = 1, purpose = "source-documentation", metadataOnly = true, libraries = rows, productionQualified = false }, new JsonSerializerOptions { WriteIndented = true }) + "\n");
Console.WriteLine("SOURCE_REFERENCE_PASS;" + projects.Length + "-libraries;metadata-only");
void Write(string name, string content)
{
    string path = Path.Combine(destination, name);
    if (check)
    {
        if (!File.Exists(path) || File.ReadAllText(path) != content)
            throw new InvalidOperationException("Generated reference drift: " + name);
    }
    else
        File.WriteAllText(path, content, new UTF8Encoding(false));
}

static string Hash(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))).ToLowerInvariant();
static string HashText(string text) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text))).ToLowerInvariant();
static string Owner(Type type) => type.FullName!.Replace('+', '.');
static string Parameters(ParameterInfo[] values) => values.Length == 0 ? "" : "(" + string.Join(',', values.Select(v => ParameterType(v.ParameterType))) + ")";
static string ParameterType(Type type)
{
    if (type.IsByRef)
        return ParameterType(type.GetElementType()!) + "@";
    if (type.IsPointer)
        return ParameterType(type.GetElementType()!) + "*";
    if (type.IsArray)
        return ParameterType(type.GetElementType()!) + (type.IsSZArray ? "[]" : "[" + string.Join(',', Enumerable.Repeat("0:", type.GetArrayRank())) + "]");
    if (type.IsGenericParameter)
        return (type.DeclaringMethod == null ? "`" : "``") + type.GenericParameterPosition;
    if (type.IsGenericType)
        return Regex.Replace(Owner(type.GetGenericTypeDefinition()), @"`\d+", "") + "{" + string.Join(',', type.GetGenericArguments().Select(ParameterType)) + "}";
    return Owner(type);
}

static string Render(XElement element)
{
    var text = new StringBuilder();
    foreach (var node in element.Nodes())
    {
        if (node is XText value)
            text.Append(value.Value);
        else if (node is XElement child)
        {
            string? literal = (string? )child.Attribute("cref") ?? (string? )child.Attribute("name") ?? (string? )child.Attribute("langword");
            text.Append(literal == null ? Render(child) : "`" + literal.Replace("`", "&#96;", StringComparison.Ordinal) + "`");
        }
    }

    return Regex.Replace(text.ToString(), @"\s+", " ");
}
