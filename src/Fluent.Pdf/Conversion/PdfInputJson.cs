using System.Globalization;
using System.Numerics;
using System.Text;

namespace Fluent.Pdf;
// Original finite recursive-descent JSON reader. Duplicate members and malformed scalar grammar reject.
internal sealed class PdfInputJson
{
    private readonly string _source;
    private readonly PdfInputParser _owner;
    private int _at;
    internal PdfInputJson(string source, PdfInputParser owner)
    {
        _source = source;
        _owner = owner;
    }

    private PdfOperationContext C => _owner.Context;

    private PdfException Error() => PdfInputConversion.Error(C, "Json", "The JSON syntax or scalar extent is invalid.");
    internal object? Read()
    {
        var result = Value();
        Space();
        if (_at != _source.Length)
            throw Error();
        return result;
    }

    private void Space()
    {
        while (_at < _source.Length && " \t\r\n".IndexOf(_source[_at]) >= 0)
        {
            C.Checkpoint();
            _at++;
        }
    }

    private bool Take(char value)
    {
        Space();
        if (_at < _source.Length && _source[_at] == value)
        {
            _at++;
            return true;
        }

        return false;
    }

    private void Need(char value)
    {
        if (!Take(value))
            throw Error();
    }

    private object? Value()
    {
        _owner.Node();
        using var depth = C.EnterScope(PdfResourceKind.Depth);
        Space();
        if (_at == _source.Length)
            throw Error();
        if (_source[_at] == '"')
            return Text();
        if (Take('{'))
        {
            var result = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (!Take('}'))
            {
                do
                {
                    var key = Text();
                    Need(':');
                    if (result.ContainsKey(key))
                        throw Error();
                    result.Add(key, Value());
                }
                while (Take(','));
                Need('}');
            }

            return result;
        }

        if (Take('['))
        {
            var result = new List<object?>();
            if (!Take(']'))
            {
                do
                {
                    result.Add(Value());
                }
                while (Take(','));
                Need(']');
            }

            return result;
        }

        foreach (var literal in new[]
        {
            "true",
            "false",
            "null"
        }

        )
        {
            if (_at + literal.Length <= _source.Length && string.CompareOrdinal(_source, _at, literal, 0, literal.Length) == 0)
            {
                _at += literal.Length;
                return literal == "null" ? null : (object)(literal == "true");
            }
        }

        int start = _at;
        if (_source[_at] == '-')
            _at++;
        if (_at == _source.Length)
            throw Error();
        if (_source[_at] == '0')
            _at++;
        else
        {
            if (_source[_at] < '1' || _source[_at] > '9')
                throw Error();
            Digits();
        }

        if (_at < _source.Length && _source[_at] == '.')
        {
            _at++;
            int first = _at;
            Digits();
            if (_at == first)
                throw Error();
        }

        if (_at < _source.Length && (_source[_at] == 'e' || _source[_at] == 'E'))
        {
            _at++;
            if (_at < _source.Length && (_source[_at] == '+' || _source[_at] == '-'))
                _at++;
            int first = _at;
            Digits();
            if (_at == first)
                throw Error();
        }

        int count = _at - start;
        if (count > 128 || !decimal.TryParse(_source.Substring(start, count), NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
            throw Error();
        if (!ExactNumber(_source.Substring(start, count), number))
            throw Error();
        return number;
    }

    private bool ExactNumber(string source, decimal value)
    {
        int exponentAt = source.IndexOfAny(new[] { 'e', 'E' });
        int exponent = 0;
        if (exponentAt >= 0 && !int.TryParse(source.Substring(exponentAt + 1), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent))
            return false;
        string mantissa = exponentAt < 0 ? source : source.Substring(0, exponentAt);
        bool negative = mantissa[0] == '-';
        if (negative)
            mantissa = mantissa.Substring(1);
        int dot = mantissa.IndexOf('.');
        long scale = (dot < 0 ? 0 : mantissa.Length - dot - 1L) - exponent;
        BigInteger coefficient = BigInteger.Parse(mantissa.Replace(".", ""), CultureInfo.InvariantCulture);
        if (negative)
            coefficient = -coefficient;
        if (coefficient.IsZero)
            return value == 0;
        while (coefficient % 10 == 0)
        {
            C.Checkpoint();
            coefficient /= 10;
            scale--;
        }

        int[] bits = decimal.GetBits(value);
        BigInteger actual = (new BigInteger((uint)bits[2]) << 64) | (new BigInteger((uint)bits[1]) << 32) | new BigInteger((uint)bits[0]);
        if ((bits[3] & int.MinValue) != 0)
            actual = -actual;
        int actualScale = (bits[3] >> 16) & 255;
        if (actual.IsZero)
            return false;
        while (actual % 10 == 0)
        {
            C.Checkpoint();
            actual /= 10;
            actualScale--;
        }

        return actual == coefficient && actualScale == scale;
    }

    private void Digits()
    {
        while (_at < _source.Length && _source[_at] >= '0' && _source[_at] <= '9')
        {
            C.Checkpoint();
            _at++;
        }
    }

    private string Text()
    {
        Need('"');
        var result = new StringBuilder();
        while (_at < _source.Length)
        {
            C.Checkpoint();
            char value = _source[_at++];
            if (value == '"')
            {
                var text = result.ToString();
                for (int i = 0; i < text.Length; i++)
                {
                    C.Checkpoint();
                    if (char.IsHighSurrogate(text[i]))
                    {
                        if (++i == text.Length || !char.IsLowSurrogate(text[i]))
                            throw Error();
                    }
                    else if (char.IsLowSurrogate(text[i]))
                        throw Error();
                }

                return text;
            }

            if (value < 32)
                throw Error();
            if (value == '\\')
            {
                if (_at == _source.Length)
                    throw Error();
                char escaped = _source[_at++];
                switch (escaped)
                {
                    case '"':
                    case '\\':
                    case '/':
                        value = escaped;
                        break;
                    case 'b':
                        value = '\b';
                        break;
                    case 'f':
                        value = '\f';
                        break;
                    case 'n':
                        value = '\n';
                        break;
                    case 'r':
                        value = '\r';
                        break;
                    case 't':
                        value = '\t';
                        break;
                    case 'u':
                        int code = 0;
                        for (int i = 0; i < 4; i++)
                        {
                            if (_at == _source.Length)
                                throw Error();
                            char digit = _source[_at++];
                            int n = digit >= '0' && digit <= '9' ? digit - '0' : digit >= 'a' && digit <= 'f' ? digit - 'a' + 10 : digit >= 'A' && digit <= 'F' ? digit - 'A' + 10 : -1;
                            if (n < 0)
                                throw Error();
                            code = code * 16 + n;
                        }

                        value = (char)code;
                        break;
                    default:
                        throw Error();
                }
            }

            C.Consume(PdfResourceKind.ExpandedBytes, 8);
            if (result.Length == 1000000)
                throw Error();
            result.Append(value);
        }

        throw Error();
    }
}
