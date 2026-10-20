using System.Globalization;
using System.IO;
using System.Threading;

namespace Fluent.Pdf;
/// <summary>One passive script definition/attachment with trigger/path provenance.</summary>
public sealed class PdfScriptRecord
{
    internal PdfScriptRecord(string location, string source, bool attachment)
    {
        Location = location;
        Source = source;
        IsAttachment = attachment;
    }
    /// <summary>Gets exact object/attachment path, including trigger keys when known.</summary>
    public string Location
    {
        get;
    }
    /// <summary>Gets decoded source without executing it.</summary>
    public string Source
    {
        get;
    }
    /// <summary>Gets whether this observation is an attachment edge.</summary>
    public bool IsAttachment
    {
        get;
    }
}
/// <summary>One finite decimal result with resource accounting; applying values/appearances remains an explicit form operation.</summary>
public sealed class PdfCalculationResult
{
    internal PdfCalculationResult(decimal value, PdfOperationReport report)
    {
        Value = value;
        OperationReport = report;
    }
    /// <summary>Gets exact decimal result.</summary>
    public decimal Value
    {
        get;
    }
    /// <summary>Gets bounded evaluation report.</summary>
    public PdfOperationReport OperationReport
    {
        get;
    }
}
/// <summary>Passive script inventory/authoring and an opt-in closed arithmetic calculation subset with no host capabilities.</summary>
public static class PdfScripts
{
    /// <summary>Inspects all active JavaScript definitions and known trigger attachments without evaluating code.</summary>
    public static IReadOnlyList<PdfScriptRecord> Inspect(PdfDocumentSnapshot source, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        var result = new List<PdfScriptRecord>();
        PdfFont.Run("InspectScripts", "managed.scripts", policy ?? source.Options.OperationPolicy, cancellationToken, c =>
        {
            using var g = new PdfGraphEdit(source, new(), c);
            var reader = new PdfNavigationReader(g);
            foreach (var action in PdfActionInventory.Read(reader, new(allowJavaScript: true)))
            {
                if (action.Subtype != "JavaScript")
                    continue;
                var js = reader.Get(action.Definition, "JS");
                string text;
                if (js.Kind == PdfValueKind.String)
                    text = PdfTextStrings.Decode(js, c);
                else if (js.Kind == PdfValueKind.Stream)
                {
                    using var decoded = PdfFilters.Decode(PdfFilterDeclarations.Resolve(js, id => reader.Resolve(PdfValue.Reference(id)), c), c);
                    using var buffer = new PdfCodecBufferStream(c, 131072);
                    c.Consume(PdfResourceKind.ExpandedBytes, 4096);
                    var part = new byte[4096];
                    int n;
                    while ((n = decoded.Read(part, 0, part.Length)) > 0)
                    {
                        c.Checkpoint();
                        buffer.Write(part, 0, n);
                    }
                    var bytes = buffer.Finish();
                    c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 2);
                    var raw = new byte[(int)bytes.Length];
                    for (int i = 0; i < raw.Length; i++)
                        raw[i] = (byte)bytes.At(i);
                    text = PdfTextStrings.Decode(PdfValue.String(raw), c);
                }
                else
                    throw g.Error("Script.SourceProfile", "Script sources must be PDF text strings or bounded streams.");
                if (text.Length > 65536)
                    throw g.Error("Script.SourceExtent", "Script source exceeds the finite profile.");
                c.Consume(PdfResourceKind.Objects, 1);
                c.Consume(PdfResourceKind.ExpandedBytes, 256 + text.Length * 2L);
                result.Add(new(action.Location, text, action.IsAttachment));
            }
        }, PdfCapability.ReadDocument);
        return result.AsReadOnly();
    }
    /// <summary>Adds/replaces/removes one named document script without executing it. Mutation is explicit active-content authority; source null removes the named attachment. Removal is not secure sanitization.</summary>
    public static PdfDocument SetDocumentScript(PdfDocumentSnapshot document, string name, string? source, PdfOrganizationOptions? options = null, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        Guard.Text(name, nameof(name), 1024);
        if (source != null && source.Length > 65536)
            throw new ArgumentOutOfRangeException(nameof(source));
        return PdfGraphEdit.Run("SetDocumentScript", document, options, policy, cancellationToken, g =>
        {
            var names = g.Get(g.Catalog, "Names");
            var tree = g.Get(names, "JavaScript");
            var reader = new PdfNavigationReader(g);
            var pairs = tree.Kind == PdfValueKind.Null ? new List<(PdfValue Key, PdfValue Value)>() : reader.NameTree(tree);
            int matches = 0;
            for (int i = pairs.Count - 1; i >= 0; i--)
            if (PdfTextStrings.Decode(pairs[i].Key, g.Context) == name)
            {
                matches++;
                pairs.RemoveAt(i);
            }
            if (matches > 1)
                throw g.Error("Script.NameIdentity", "Decoded script names must be unambiguous.");
            if (source != null)
                pairs.Add((PdfTextStrings.Encode(name, g.Context), PdfValue.Reference(g.Edit.Add(PdfGraphicsNumbers.Dict(("S", PdfValue.Name("JavaScript")), ("JS", PdfTextStrings.Encode(source, g.Context)))))));
            g.Context.Consume(PdfResourceKind.ExpandedBytes, pairs.Count * 64L + 256);
            pairs.Sort((a, b) => { for (int i = 0; i < Math.Min(a.Key.StringBytes.Count, b.Key.StringBytes.Count); i++) { g.Context.Checkpoint(); int d = a.Key.StringBytes[i] - b.Key.StringBytes[i]; if (d != 0) return d; } return a.Key.StringBytes.Count.CompareTo(b.Key.StringBytes.Count); });
            var values = new List<PdfValue>();
            foreach (var pair in pairs)
            {
                g.Context.Checkpoint();
                values.Add(pair.Key);
                values.Add(pair.Value);
            }
            var dictionary = g.Copy(names);
            if (pairs.Count == 0)
                dictionary.Remove("JavaScript");
            else
                dictionary["JavaScript"] = PdfValue.Reference(g.Edit.Add(PdfGraphicsNumbers.Dict(("Names", PdfValue.OwnedArray(values)))));
            var catalog = g.Copy(g.Catalog);
            catalog["Names"] = PdfValue.OwnedDictionary(dictionary);
            g.SetCatalog(catalog);
        }, "1.3");
    }
    /// <summary>Evaluates only event.value = decimal arithmetic with parentheses and this.getField("name").value reads. Execution requires explicit opt-in. There are no loops, dynamic calls, objects, reflection, I/O or host APIs.</summary>
    public static PdfCalculationResult EvaluateCalculation(string source, IReadOnlyDictionary<string, decimal> values, bool enableExecution = false, PdfOperationPolicy? policy = null, CancellationToken cancellationToken = default)
    {
        if (source == null || values == null)
            throw new ArgumentNullException(source == null ? nameof(source) : nameof(values));
        decimal result = 0;
        var report = PdfFont.Run("EvaluateCalculation", "managed.closed-calculation", policy, cancellationToken, c =>
        {
            if (!enableExecution)
                throw PdfWriterCore.Error(c, "Script.ExecutionDisabled", "Calculation execution requires explicit opt-in.");
            if (source.Length > 65536)
                throw PdfWriterCore.Error(c, "Script.SourceExtent", "Calculation source exceeds the finite subset.");
            c.Consume(PdfResourceKind.ExpandedBytes, source.Length * 4L + 256);
            try
            {
                result = new CalculationParser(source, values, c).Run();
            }
            catch (OverflowException) { c.Checkpoint(); throw PdfWriterCore.Error(c, "Script.NumericRange", "Decimal calculation overflowed."); }
            catch (DivideByZeroException) { c.Checkpoint(); throw PdfWriterCore.Error(c, "Script.DivisionByZero", "Calculation divided by zero."); }
        }, PdfCapability.ReadDocument);
        return new(result, report);
    }
    private sealed class CalculationParser
    {
        private readonly string _source; private readonly IReadOnlyDictionary<string, decimal> _values; private readonly PdfOperationContext _c; private int _at;
        internal CalculationParser(string source, IReadOnlyDictionary<string, decimal> values, PdfOperationContext c)
        {
            _source = source;
            _values = values;
            _c = c;
        }
        internal decimal Run()
        {
            Require("event.value");
            Require("=");
            decimal value = Expression();
            Take(";");
            Space();
            if (_at != _source.Length)
                Unsupported();
            return value;
        }
        private decimal Expression()
        {
            decimal value = Term();
            while (true)
            {
                if (Take("+"))
                    value += Term();
                else if (Take("-"))
                    value -= Term();
                else
                    return value;
            }
        }
        private decimal Term()
        {
            decimal value = Atom();
            while (true)
            {
                if (Take("*"))
                    value *= Atom();
                else if (Take("/"))
                    value /= Atom();
                else
                    return value;
            }
        }
        private decimal Atom()
        {
            _c.Checkpoint();
            _c.Consume(PdfResourceKind.Objects, 1);
            _c.Consume(PdfResourceKind.ExpandedBytes, 64);
            using var depth = _c.EnterScope(PdfResourceKind.Depth);
            if (Take("+"))
                return Atom();
            if (Take("-"))
                return -Atom();
            if (Take("("))
            {
                decimal value = Expression();
                Require(")");
                return value;
            }
            if (Take("this.getField"))
            {
                Require("(");
                Require("\"");
                int start = _at;
                while (_at < _source.Length && _source[_at] != '"')
                {
                    _c.Checkpoint();
                    char ch = _source[_at++];
                    if (ch == '\\' || ch < 32 || _at - start > 1024)
                        Unsupported();
                }
                string name = _source.Substring(start, _at - start);
                Require("\"");
                Require(")");
                Require(".value");
                if (!_values.TryGetValue(name, out var value))
                    throw PdfWriterCore.Error(_c, "Script.FieldMissing", "An explicit calculation input is missing.");
                return value;
            }
            Space();
            int begin = _at;
            while (_at < _source.Length && (_source[_at] >= '0' && _source[_at] <= '9' || _source[_at] == '.'))
            {
                _c.Checkpoint();
                _at++;
            }
            if (begin == _at || !decimal.TryParse(_source.Substring(begin, _at - begin), NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var number))
            {
                Unsupported();
                return 0;
            }
            return number;
        }
        private void Space()
        {
            while (_at < _source.Length && char.IsWhiteSpace(_source[_at]))
            {
                _c.Checkpoint();
                _at++;
            }
        }
        private bool Take(string text)
        {
            Space();
            _c.Checkpoint();
            if (_source.Length - _at < text.Length || string.CompareOrdinal(_source, _at, text, 0, text.Length) != 0)
                return false;
            _at += text.Length;
            return true;
        }
        private void Require(string text)
        {
            if (!Take(text))
                Unsupported();
        }
        private void Unsupported() => throw PdfWriterCore.Error(_c, "Script.UnsupportedSubset", "The calculation uses unsupported syntax, host APIs or action triggers.");
    }
}
