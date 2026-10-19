namespace Fluent.Pdf;
// Original streaming PDF RunLength encoder. Only already-masked RGB samples enter its bounded literal/repeat buffers.
internal sealed class PdfRedactionRle
{
    private readonly PdfByteBuilder _output;
    private readonly byte[] _literal;
    private int _length, _repeat;
    private byte _value;
    internal PdfRedactionRle(PdfOperationContext c)
    {
        c.Consume(PdfResourceKind.ExpandedBytes, 512);
        _literal = new byte[128];
        _output = new(c);
    }

    internal void Add(byte value)
    {
        if (_repeat != 0)
        {
            if (value == _value && _repeat < 128)
            {
                _repeat++;
                return;
            }

            FlushRepeat();
        }

        _literal[_length++] = value;
        if (_length >= 3 && _literal[_length - 1] == _literal[_length - 2] && _literal[_length - 2] == _literal[_length - 3])
        {
            int prefix = _length - 3;
            if (prefix != 0)
                WriteLiteral(prefix);
            _length = 0;
            _value = value;
            _repeat = 3;
        }
        else if (_length == 128)
        {
            WriteLiteral(_length);
            _length = 0;
        }
    }

    internal PdfByteStore Finish()
    {
        if (_repeat != 0)
            FlushRepeat();
        if (_length != 0)
            WriteLiteral(_length);
        _output.Add(128);
        return _output.Build();
    }

    private void WriteLiteral(int count)
    {
        _output.Add((byte)(count - 1));
        for (int i = 0; i < count; i++)
            _output.Add(_literal[i]);
    }

    private void FlushRepeat()
    {
        _output.Add((byte)(257 - _repeat));
        _output.Add(_value);
        _repeat = 0;
    }
}
