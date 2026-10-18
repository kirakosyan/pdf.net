namespace Fluent.Pdf.Fonts;
// Original bounded Type 2 outline evaluator used only when expanding legacy composite accents.
internal sealed class CffGlyphMachine
{
    internal sealed class Outline
    {
        internal readonly List<(byte Kind, double[] Points)> Paths = new();
        internal double Width;
        internal bool Composite;
    }

    private readonly PdfOperationContext _context;
    private readonly Func<bool, int, (byte[] Bytes, int Offset, int Length)> _subroutine;
    private readonly Func<int, int, Outline> _accent;
    private readonly double _defaultWidth, _nominal;
    private readonly List<double> _stack = new();
    private readonly double[] _transient = new double[32];
    private readonly HashSet<(bool, int)> _active = new();
    private readonly Outline _outline = new();
    private double _x, _y;
    private int _hints;
    private bool _width, _ended;
    internal CffGlyphMachine(double defaultWidth, double nominal, Func<bool, int, (byte[] Bytes, int Offset, int Length)> subroutine, Func<int, int, Outline> accent, PdfOperationContext context)
    {
        _defaultWidth = defaultWidth;
        _nominal = nominal;
        _subroutine = subroutine;
        _accent = accent;
        _context = context;
        context.Consume(PdfResourceKind.ExpandedBytes, 1024);
    }

    internal Outline Evaluate(byte[] bytes, int offset, int length)
    {
        Execute(bytes, offset, length, false);
        if (!_ended)
            throw Error("The glyph has no endchar instruction.");
        return _outline;
    }

    private PdfException Error(string message) => PdfWriterCore.Error(_context, "Font.CffCharString", message);
    private void Execute(byte[] bytes, int offset, int length, bool subroutine)
    {
        using var scope = _context.EnterScope(PdfResourceKind.Depth);
        int position = offset, end = checked(offset + length);
        while (position < end && !_ended)
        {
            _context.Checkpoint();
            int op = bytes[position++];
            if (op >= 32 || op == 28)
            {
                int needed = op == 28 ? 2 : op == 255 ? 4 : op >= 247 ? 1 : 0;
                if (position > end - needed)
                    throw Error("A numeric operand is truncated.");
                double value;
                if (op >= 32 && op <= 246)
                    value = op - 139;
                else if (op <= 250 && op >= 247)
                    value = (op - 247) * 256 + bytes[position++] + 108;
                else if (op <= 254 && op >= 251)
                    value = -(op - 251) * 256 - bytes[position++] - 108;
                else if (op == 28)
                {
                    value = (short)(bytes[position] * 256 + bytes[position + 1]);
                    position += 2;
                }
                else
                {
                    value = (bytes[position] << 24 | bytes[position + 1] << 16 | bytes[position + 2] << 8 | bytes[position + 3]) / 65536.0;
                    position += 4;
                }

                Push(value);
                continue;
            }

            if (op == 10 || op == 29)
            {
                int index = Integer(Pop());
                var key = (op == 29, index);
                if (!_active.Add(key))
                    throw Error("A subroutine call is cyclic.");
                var target = _subroutine(op == 29, index);
                try
                {
                    Execute(target.Bytes, target.Offset, target.Length, true);
                }
                finally
                {
                    _active.Remove(key);
                }

                continue;
            }

            if (op == 11)
            {
                if (!subroutine)
                    throw Error("return occurs outside a subroutine.");
                return;
            }

            if (op == 1 || op == 3 || op == 18 || op == 23 || op == 19 || op == 20)
            {
                Width(_stack.Count % 2 != 0);
                if (_stack.Count % 2 != 0)
                    throw Error("Stem hints require coordinate pairs.");
                _hints += _stack.Count / 2;
                if (_hints > 96)
                    throw Error("The glyph exceeds 96 stem hints.");
                _stack.Clear();
                if (op == 19 || op == 20)
                {
                    int count = (_hints + 7) / 8;
                    if (position > end - count)
                        throw Error("A hint mask is truncated.");
                    position += count;
                }

                continue;
            }

            if (op == 4 || op == 21 || op == 22)
            {
                int count = op == 21 ? 2 : 1;
                Width(_stack.Count > count);
                Count(count);
                Move(op == 4 ? 0 : _stack[0], op == 22 ? 0 : _stack[op == 21 ? 1 : 0]);
                _stack.Clear();
                continue;
            }

            if (op == 14)
            {
                Width(_stack.Count == 1 || _stack.Count == 5);
                if (_stack.Count == 4)
                {
                    int baseCode = Integer(_stack[2]), accentCode = Integer(_stack[3]);
                    var baseGlyph = _accent(baseCode, 0);
                    var accentGlyph = _accent(accentCode, 1);
                    Append(baseGlyph, 0, 0);
                    Append(accentGlyph, _stack[0], _stack[1]);
                    _outline.Composite = true;
                }
                else if (_stack.Count != 0)
                    throw Error("endchar has an invalid operand count.");
                _stack.Clear();
                _ended = true;
                return;
            }

            int escaped = -1;
            if (op == 12)
            {
                if (position >= end)
                    throw Error("An escaped instruction is truncated.");
                escaped = bytes[position++];
                if (Arithmetic(escaped))
                    continue;
                if (escaped == 0)
                {
                    Count(0);
                    continue;
                }
            }

            if (!_width)
                throw Error("A drawing instruction precedes the initial moveto/width.");
            if (op == 5)
            {
                if (_stack.Count < 2 || _stack.Count % 2 != 0)
                    throw Error("rlineto requires coordinate pairs.");
                for (int i = 0; i < _stack.Count; i += 2)
                    Line(_stack[i], _stack[i + 1]);
            }
            else if (op == 6 || op == 7)
            {
                if (_stack.Count == 0)
                    throw Error("Alternating lines require coordinates.");
                bool horizontal = op == 6;
                foreach (double value in _stack)
                {
                    Line(horizontal ? value : 0, horizontal ? 0 : value);
                    horizontal = !horizontal;
                }
            }
            else if (op == 8)
            {
                if (_stack.Count < 6 || _stack.Count % 6 != 0)
                    throw Error("rrcurveto requires cubic coordinate groups.");
                for (int i = 0; i < _stack.Count; i += 6)
                    Curve(_stack[i], _stack[i + 1], _stack[i + 2], _stack[i + 3], _stack[i + 4], _stack[i + 5]);
            }
            else if (op == 24)
            {
                if (_stack.Count < 8 || (_stack.Count - 2) % 6 != 0)
                    throw Error("rcurveline has an invalid operand count.");
                int i = 0;
                for (; i < _stack.Count - 2; i += 6)
                    Curve(_stack[i], _stack[i + 1], _stack[i + 2], _stack[i + 3], _stack[i + 4], _stack[i + 5]);
                Line(_stack[i], _stack[i + 1]);
            }
            else if (op == 25)
            {
                if (_stack.Count < 8 || (_stack.Count - 6) % 2 != 0)
                    throw Error("rlinecurve has an invalid operand count.");
                int i = 0;
                for (; i < _stack.Count - 6; i += 2)
                    Line(_stack[i], _stack[i + 1]);
                Curve(_stack[i], _stack[i + 1], _stack[i + 2], _stack[i + 3], _stack[i + 4], _stack[i + 5]);
            }
            else if (op == 26 || op == 27)
            {
                if (_stack.Count < 4 || _stack.Count % 4 > 1)
                    throw Error("Axis-aligned curves have an invalid operand count.");
                int i = 0;
                double extra = _stack.Count % 4 == 1 ? _stack[i++] : 0;
                bool vertical = op == 26;
                for (; i < _stack.Count; i += 4)
                {
                    Curve(vertical ? extra : _stack[i], vertical ? _stack[i] : extra, _stack[i + 1], _stack[i + 2], vertical ? 0 : _stack[i + 3], vertical ? _stack[i + 3] : 0);
                    extra = 0;
                }
            }
            else if (op == 30 || op == 31)
            {
                if (_stack.Count < 4 || _stack.Count % 4 > 1)
                    throw Error("Alternating curves have an invalid operand count.");
                bool horizontal = op == 31;
                int i = 0;
                while (i < _stack.Count)
                {
                    int remaining = _stack.Count - i;
                    double last = remaining == 5 ? _stack[i + 4] : 0;
                    if (horizontal)
                        Curve(_stack[i], 0, _stack[i + 1], _stack[i + 2], last, _stack[i + 3]);
                    else
                        Curve(0, _stack[i], _stack[i + 1], _stack[i + 2], _stack[i + 3], last);
                    i += remaining == 5 ? 5 : 4;
                    horizontal = !horizontal;
                }
            }
            else if (op == 12)
            {
                int escape = escaped;
                if (escape == 34)
                {
                    Count(7);
                    Curve(_stack[0], 0, _stack[1], _stack[2], _stack[3], 0);
                    Curve(_stack[4], 0, _stack[5], -_stack[2], _stack[6], 0);
                }
                else if (escape == 35)
                {
                    Count(13);
                    Curve(_stack[0], _stack[1], _stack[2], _stack[3], _stack[4], _stack[5]);
                    Curve(_stack[6], _stack[7], _stack[8], _stack[9], _stack[10], _stack[11]);
                }
                else if (escape == 36)
                {
                    Count(9);
                    Curve(_stack[0], _stack[1], _stack[2], _stack[3], _stack[4], 0);
                    Curve(_stack[5], 0, _stack[6], _stack[7], _stack[8], -(_stack[1] + _stack[3] + _stack[7]));
                }
                else if (escape == 37)
                {
                    Count(11);
                    double dx = _stack[0] + _stack[2] + _stack[4] + _stack[6] + _stack[8], dy = _stack[1] + _stack[3] + _stack[5] + _stack[7] + _stack[9];
                    Curve(_stack[0], _stack[1], _stack[2], _stack[3], _stack[4], _stack[5]);
                    Curve(_stack[6], _stack[7], _stack[8], _stack[9], Math.Abs(dx) > Math.Abs(dy) ? _stack[10] : -dx, Math.Abs(dx) > Math.Abs(dy) ? -dy : _stack[10]);
                }
                else
                    throw Error("An unsupported escaped Type 2 drawing instruction was encountered.");
            }
            else
                throw Error("A reserved/unsupported Type 2 instruction was encountered.");
            _stack.Clear();
        }

        if (subroutine && !_ended)
            throw Error("A subroutine has no return/endchar instruction.");
    }

    private void Width(bool present)
    {
        if (_width)
            return;
        _outline.Width = present ? PopFirst() + _nominal : _defaultWidth;
        _width = true;
    }

    private double PopFirst()
    {
        if (_stack.Count == 0)
            throw Error("An initial width is missing.");
        double value = _stack[0];
        _stack.RemoveAt(0);
        return value;
    }

    private void Count(int count)
    {
        if (_stack.Count != count)
            throw Error("A drawing instruction has an incorrect operand count.");
    }

    private void Push(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            throw Error("Glyph arithmetic is not finite.");
        if (_stack.Count == 48)
            throw Error("The Type 2 operand stack exceeds 48 values.");
        _context.Consume(PdfResourceKind.ExpandedBytes, 16);
        _stack.Add(value);
    }

    private double Pop()
    {
        if (_stack.Count == 0)
            throw Error("The Type 2 operand stack underflows.");
        double value = _stack[_stack.Count - 1];
        _stack.RemoveAt(_stack.Count - 1);
        return value;
    }

    private int Integer(double value)
    {
        if (value < int.MinValue || value > int.MaxValue || value != Math.Floor(value))
            throw Error("An instruction requires a bounded integer.");
        return (int)value;
    }

    private void Path(byte kind, params double[] points)
    {
        _context.Consume(PdfResourceKind.Objects, 1);
        _context.Consume(PdfResourceKind.ExpandedBytes, points.Length * 8L + 128);
        if (points.Any(x => double.IsNaN(x) || double.IsInfinity(x)))
            throw Error("Outline coordinates are not finite.");
        _outline.Paths.Add((kind, points));
    }

    private void Move(double dx, double dy)
    {
        _x += dx;
        _y += dy;
        Path(21, _x, _y);
    }

    private void Line(double dx, double dy)
    {
        _x += dx;
        _y += dy;
        Path(5, _x, _y);
    }

    private void Curve(double dx1, double dy1, double dx2, double dy2, double dx3, double dy3)
    {
        double x1 = _x + dx1, y1 = _y + dy1, x2 = x1 + dx2, y2 = y1 + dy2;
        _x = x2 + dx3;
        _y = y2 + dy3;
        Path(8, x1, y1, x2, y2, _x, _y);
    }

    private void Append(Outline outline, double dx, double dy)
    {
        foreach (var path in outline.Paths)
        {
            _context.Checkpoint();
            _context.Consume(PdfResourceKind.ExpandedBytes, path.Points.Length * 8L);
            var points = (double[])path.Points.Clone();
            for (int i = 0; i < points.Length; i += 2)
            {
                points[i] += dx;
                points[i + 1] += dy;
            }

            Path(path.Kind, points);
        }
    }

    private bool Arithmetic(int op)
    {
        if (op == 3 || op == 4 || op == 10 || op == 11 || op == 12 || op == 15 || op == 24)
        {
            double b = Pop(), a = Pop();
            Push(op == 3 ? (a != 0 && b != 0 ? 1 : 0) : op == 4 ? (a != 0 || b != 0 ? 1 : 0) : op == 10 ? a + b : op == 11 ? a - b : op == 12 ? a / b : op == 15 ? (a == b ? 1 : 0) : a * b);
            return true;
        }

        if (op == 5 || op == 9 || op == 14 || op == 26)
        {
            double value = Pop();
            Push(op == 5 ? (value == 0 ? 1 : 0) : op == 9 ? Math.Abs(value) : op == 14 ? -value : Math.Sqrt(value));
            return true;
        }

        if (op == 18)
        {
            Pop();
            return true;
        }

        if (op == 20)
        {
            int index = Integer(Pop());
            double value = Pop();
            if (index < 0 || index >= 32)
                throw Error("A transient array index is outside range.");
            _transient[index] = value;
            return true;
        }

        if (op == 21)
        {
            int index = Integer(Pop());
            if (index < 0 || index >= 32)
                throw Error("A transient array index is outside range.");
            Push(_transient[index]);
            return true;
        }

        if (op == 22)
        {
            double b = Pop(), a = Pop(), v2 = Pop(), v1 = Pop();
            Push(a <= b ? v1 : v2);
            return true;
        }

        if (op == 23)
            throw PdfWriterCore.Error(_context, "Font.CffRandomUnsupported", "Nondeterministic random glyph programs require an explicit seeded outline provider.");
        if (op == 27)
        {
            double value = Pop();
            Push(value);
            Push(value);
            return true;
        }

        if (op == 28)
        {
            double b = Pop(), a = Pop();
            Push(b);
            Push(a);
            return true;
        }

        if (op == 29)
        {
            int index = Integer(Pop());
            if (_stack.Count == 0)
                throw Error("index requires an operand.");
            index = Math.Max(0, Math.Min(index, _stack.Count - 1));
            Push(_stack[_stack.Count - 1 - index]);
            return true;
        }

        if (op == 30)
        {
            int shift = Integer(Pop()), count = Integer(Pop());
            if (count < 0 || count > _stack.Count)
                throw Error("roll exceeds the operand stack.");
            if (count == 0)
                return true;
            shift = (shift % count + count) % count;
            _context.Consume(PdfResourceKind.ExpandedBytes, count * 8L);
            var values = _stack.Skip(_stack.Count - count).ToArray();
            for (int i = 0; i < count; i++)
                _stack[_stack.Count - count + (i + shift) % count] = values[i];
            return true;
        }

        return false;
    }

    internal static byte[] Encode(Outline outline, double nominal, PdfOperationContext context)
    {
        var output = new PdfByteBuilder(context);
        Number(outline.Width - nominal);
        double x = 0, y = 0;
        foreach (var path in outline.Paths)
        {
            context.Checkpoint();
            for (int i = 0; i < path.Points.Length; i += 2)
            {
                Number(path.Points[i] - x);
                Number(path.Points[i + 1] - y);
                x = path.Points[i];
                y = path.Points[i + 1];
            }

            output.Add(path.Kind);
        }

        output.Add(14);
        var bytes = output.Build();
        context.Consume(PdfResourceKind.ExpandedBytes, bytes.Length);
        var result = new byte[(int)bytes.Length];
        using var input = bytes.Open(0, bytes.Length);
        int offset = 0;
        while (offset < result.Length)
        {
            context.Checkpoint();
            offset += input.Read(result, offset, result.Length - offset);
        }

        return result;
        void Number(double value)
        {
            double scaled = value * 65536;
            if (scaled < int.MinValue || scaled > int.MaxValue || double.IsInfinity(scaled) || double.IsNaN(scaled))
                throw PdfWriterCore.Error(context, "Font.CffCoordinateRange", "A normalized Type 2 coordinate exceeds 16.16 representation.");
            long exact = (long)Math.Round(scaled);
            if (Math.Abs(scaled - exact) > 0.000001)
                context.Report(new PdfDiagnostic("Font.CffCoordinatePrecision", "Expanding a legacy composite requires rounding a coordinate to the CFF 16.16 grid.", severity: PdfDiagnosticSeverity.Warning, lossCategory: PdfLossCategory.FidelityReduction));
            if (exact % 65536 == 0 && exact / 65536 >= short.MinValue && exact / 65536 <= short.MaxValue)
            {
                output.Add(28);
                output.Add((byte)(exact / 65536 >> 8));
                output.Add((byte)(exact / 65536));
            }
            else
            {
                output.Add(255);
                uint bits = (uint)(int)exact;
                for (int i = 3; i >= 0; i--)
                    output.Add((byte)(bits >> (8 * i)));
            }
        }
    }
}
