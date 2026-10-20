using System.Globalization;
using System.IO;
using System.Text;

namespace Fluent.Pdf.Word;
// Original bounded RTF source profile. External fields, objects and file destinations are never resolved.
internal static class WordRtf
{
    internal static WordModel Read(PdfByteStore bytes, PdfWordConversionOptions options, PdfOperationContext c) => new Reader(bytes, options, c).Read();
    internal static void Write(WordModel model, Stream output, string family, PdfOperationContext c)
    {
        WordErrors.Node(c, 8192);
        using var writer = new StreamWriter(output, new UTF8Encoding(false, true), 1024, true);
        writer.Write(@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil ");
        Text(family);
        writer.Write(";}}\n");
        for (int p = 0; p < model.Sections.Count; p++)
        {
            var section = model.Sections[p];
            if (p > 0)
                writer.Write(@"\sect ");
            writer.Write(@"\sectd\paperw");
            Number(section.Width);
            writer.Write(@"\paperh");
            Number(section.Height);
            writer.Write(@"\margl0\margr0\margt0\margb0 ");
            foreach (var block in section.Blocks)
            {
                c.Checkpoint();
                if (block is WordParagraph paragraph)
                    Paragraph(paragraph, false);
                else if (block is WordTable table)
                {
                    foreach (var row in table.Rows)
                    {
                        writer.Write(@"\trowd");
                        double right = 0;
                        for (int i = 0; i < row.Count; i++)
                        {
                            right += table.Widths.Count == row.Count ? table.Widths[i] : 100;
                            writer.Write(@"\cellx");
                            Number(right);
                        }

                        foreach (var cell in row)
                        {
                            writer.Write(@"\intbl ");
                            Paragraph(cell, true);
                            writer.Write(@"\cell ");
                        }

                        writer.Write(@"\row ");
                    }
                }
                else if (block is WordPicture picture)
                {
                    writer.Write(@"{\pict\pngblip\picwgoal");
                    Number(picture.Width);
                    writer.Write(@"\pichgoal");
                    Number(picture.Height);
                    writer.Write(' ');
                    const string hex = "0123456789abcdef";
                    for (long i = 0; i < picture.Bytes.Length; i++)
                    {
                        if ((i & 4095) == 0)
                            c.Checkpoint();
                        int value = picture.Bytes.At(i);
                        writer.Write(hex[value >> 4]);
                        writer.Write(hex[value & 15]);
                    }

                    writer.Write("}\\par\n");
                }
            }
        }

        writer.Write('}');
        writer.Flush();
        c.Checkpoint();
        void Number(double value)
        {
            double n = Math.Round(value * 20);
            if (n < 0 || n > int.MaxValue || double.IsNaN(n) || double.IsInfinity(n))
                throw WordErrors.Error(c, "RtfGeometry", "Word geometry exceeds its bounded twip extent.");
            writer.Write(((int)n).ToString(CultureInfo.InvariantCulture));
        }

        void Paragraph(WordParagraph paragraph, bool cell)
        {
            writer.Write(@"\pard");
            if (cell)
                writer.Write(@"\intbl");
            writer.Write(' ');
            foreach (var run in paragraph.Runs)
            {
                c.Checkpoint();
                if (!string.Equals(run.Family, family, StringComparison.Ordinal))
                    WordErrors.Loss(c, "RtfFontSubstitution", "The explicit RTF source profile uses the selected fallback font for this run.");
                writer.Write(@"{\f0\fs");
                writer.Write(Math.Round(run.Size * 2).ToString(CultureInfo.InvariantCulture));
                if (run.Bold)
                    writer.Write(@"\b");
                if (run.Italic)
                    writer.Write(@"\i");
                writer.Write(' ');
                Text(run.Text);
                writer.Write('}');
            }

            if (!cell)
                writer.Write("\\par\n");
        }

        void Text(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                c.Checkpoint();
                char ch = text[i];
                if (ch == '\\' || ch == '{' || ch == '}')
                {
                    writer.Write('\\');
                    writer.Write(ch);
                }
                else if (ch == '\r')
                {
                    if (i + 1 < text.Length && text[i + 1] == '\n')
                        i++;
                    writer.Write(@"\line ");
                }
                else if (ch == '\n')
                    writer.Write(@"\line ");
                else if (ch == '\t')
                    writer.Write(@"\tab ");
                else if (ch < 32 || ch > 126)
                {
                    writer.Write(@"\u");
                    writer.Write(unchecked((short)ch).ToString(CultureInfo.InvariantCulture));
                    writer.Write('?');
                }
                else
                    writer.Write(ch);
            }
        }
    }

    private sealed class Reader
    {
        private readonly string _text;
        private readonly PdfWordConversionOptions _options;
        private readonly PdfOperationContext _c;
        private readonly WordModel _model = new();
        private readonly Dictionary<int, string> _fonts = new();
        private readonly Stack<(State State, IDisposable Scope)> _groups = new();
        private State _state = new();
        private WordSection _section = new();
        private WordParagraph _paragraph = new();
        private WordTable? _table;
        private List<WordParagraph>? _row;
        private readonly List<double> _widths = new();
        private int _at, _characters, _fallback, _defaultFont;
        private readonly StringBuilder _pending = new();
        private WordRun? _currentRun;
        private bool _header, _closed;
        private sealed class Picture
        {
            internal readonly PdfByteBuilder Bytes;
            internal int High = -1;
            internal double Width, Height;
            internal bool Png;
            internal Picture(PdfOperationContext c)
            {
                Bytes = new(c);
            }
        }

        private sealed class State
        {
            internal bool Skip, FontTable, PictureOwner;
            internal int Font, UnicodeFallback = 1;
            internal string Family = "Noto Sans";
            internal double Size = 12;
            internal bool Bold, Italic;
            internal string FontName = "";
            internal Picture? Picture;
            internal State Copy()
            {
                var value = (State)MemberwiseClone();
                value.PictureOwner = false;
                value.FontName = "";
                return value;
            }
        }

        internal Reader(PdfByteStore bytes, PdfWordConversionOptions options, PdfOperationContext c)
        {
            _options = options;
            _c = c;
            if (bytes.Length > options.MaximumInputBytes || bytes.Length > int.MaxValue)
                throw Error("InputLimit", "The RTF source exceeds its explicit byte ceiling.");
            c.Consume(PdfResourceKind.ExpandedBytes, bytes.Length * 4 + 256);
            var raw = new byte[(int)bytes.Length];
            using var stream = bytes.Open(0, bytes.Length);
            for (int at = 0; at < raw.Length;)
            {
                c.Checkpoint();
                int n = stream.Read(raw, at, raw.Length - at);
                if (n == 0)
                    throw Error("Rtf", "The captured RTF input ended unexpectedly.");
                at += n;
            }

            _text = Encoding.GetEncoding(28591).GetString(raw);
            WordErrors.Node(c);
            _model.Sections.Add(_section);
        }

        private PdfException Error(string code, string message) => WordErrors.Error(_c, code, message);
        internal WordModel Read()
        {
            try
            {
                if (!_text.TrimStart().StartsWith("{\\rtf1", StringComparison.Ordinal))
                    throw Error("RtfVersion", "A version-one RTF root header must begin the source.");
                while (_at < _text.Length)
                {
                    _c.Checkpoint();
                    char ch = _text[_at++];
                    if (_closed)
                    {
                        if (!char.IsWhiteSpace(ch))
                            throw Error("Rtf", "Trailing RTF content is not allowed.");
                        continue;
                    }

                    if (ch == '{')
                    {
                        if (_groups.Count == _options.MaximumDepth)
                            throw Error("RtfDepth", "The explicit RTF nesting ceiling was exceeded.");
                        WordErrors.Node(_c);
                        _groups.Push((_state, _c.EnterScope(PdfResourceKind.Depth)));
                        _state = _state.Copy();
                    }
                    else if (ch == '}')
                    {
                        if (_groups.Count == 0)
                            throw Error("Rtf", "An RTF group closed without an opener.");
                        if (_state.PictureOwner)
                            PictureComplete();
                        var frame = _groups.Pop();
                        _state = frame.State;
                        frame.Scope.Dispose();
                        if (_groups.Count == 0)
                            _closed = true;
                    }
                    else if (_groups.Count == 0)
                    {
                        if (!char.IsWhiteSpace(ch))
                            throw Error("Rtf", "An RTF document must have one root group.");
                    }
                    else if (ch == '\\')
                        Control();
                    else if (ch != '\r' && ch != '\n')
                        Literal(ch);
                }

                if (!_closed || _groups.Count != 0 || !_header || _fallback != 0)
                    throw Error("Rtf", "The RTF document is incomplete or its Unicode fallback is truncated.");
                if (_row != null)
                    throw Error("RtfTable", "An RTF table row is incomplete.");
                FlushParagraph();
                return _model;
            }
            finally
            {
                while (_groups.Count > 0)
                    _groups.Pop().Scope.Dispose();
            }
        }

        private void Control()
        {
            if (_at == _text.Length)
                throw Error("Rtf", "A control escape is truncated.");
            char first = _text[_at++];
            if (first == '\\' || first == '{' || first == '}')
            {
                Literal(first);
                return;
            }

            if (first == '\'')
            {
                if (_at > _text.Length - 2)
                    throw Error("Rtf", "A hexadecimal character is truncated.");
                int value = Hex(_text[_at++]) * 16 + Hex(_text[_at++]);
                Literal(Ansi(value));
                return;
            }

            if (first == '*')
            {
                _state.Skip = true;
                WordErrors.Loss(_c, "RtfDestination", "An unrecognized ignorable RTF destination is omitted.");
                return;
            }

            if (first == '~')
            {
                Literal('\u00a0');
                return;
            }

            if (first == '_')
            {
                Literal('\u2011');
                return;
            }

            if (first == '-')
            {
                WordErrors.Loss(_c, "RtfOptionalHyphen", "Optional hyphen placement is outside this explicit source profile.");
                return;
            }

            if (!Letter(first))
                throw Error("Rtf", "An RTF control symbol is invalid.");
            int start = _at - 1;
            while (_at < _text.Length && Letter(_text[_at]))
            {
                _c.Checkpoint();
                _at++;
                if (_at - start > 64)
                    throw Error("Rtf", "An RTF control word exceeds its bounded extent.");
            }

            string word = _text.Substring(start, _at - start);
            int sign = 1;
            if (_at < _text.Length && _text[_at] == '-')
            {
                sign = -1;
                _at++;
            }

            int number = 0, digits = 0;
            while (_at < _text.Length && _text[_at] >= '0' && _text[_at] <= '9')
            {
                if (++digits > 9)
                    throw Error("Rtf", "An RTF numeric control exceeds its bounded extent.");
                number = number * 10 + _text[_at++] - '0';
            }

            if (sign < 0 && digits == 0)
                throw Error("Rtf", "An RTF numeric sign requires digits.");
            number *= sign;
            if (_at < _text.Length && _text[_at] == ' ')
                _at++;
            if (word == "field" || word == "object" || word == "objdata" || word == "filetbl" || word == "fldinst")
                throw Error("RtfActiveContent", "RTF external fields, embedded objects and file destinations are prohibited.");
            if (word == "bin")
                throw Error("RtfBinary", "Binary RTF destinations require an explicit bounded binary profile.");
            if (_state.Skip)
                return;
            if (word == "rtf")
            {
                if (_header || number != 1 || _groups.Count != 1)
                    throw Error("RtfVersion", "Exactly one version-one RTF root header is required.");
                _header = true;
                return;
            }

            if (word == "ansi" || word == "fnil" || word == "froman" || word == "fswiss" || word == "fmodern" || word == "fcharset" || word == "lang" || word == "viewkind" || word == "sectd" || word == "intbl" || word == "pard")
                return;
            if (word == "ansicpg")
            {
                if (number != 1252)
                    throw Error("RtfCodepage", "This source profile requires explicit Unicode escapes or Windows1252 ANSI characters.");
                return;
            }

            if (word == "deff")
            {
                if (number < 0)
                    throw Error("RtfFont", "The default font index must be nonnegative.");
                _defaultFont = _state.Font = number;
                return;
            }

            if (word == "fonttbl")
            {
                _state.FontTable = true;
                return;
            }

            if (word == "f")
            {
                if (number < 0)
                    throw Error("RtfFont", "Font indices must be nonnegative.");
                _state.Font = number;
                _state.FontName = "";
                if (!_state.FontTable)
                {
                    if (!_fonts.TryGetValue(number, out var family))
                        throw Error("RtfFont", "The selected RTF font has no declaration.");
                    _state.Family = family;
                }

                return;
            }

            if (word == "info" || word == "stylesheet" || word == "colortbl" || word == "listtable" || word == "listoverridetable")
            {
                _state.Skip = true;
                WordErrors.Loss(_c, "RtfDestination", "An unsupported RTF metadata/style/color/list destination is omitted.");
                return;
            }

            if (word == "uc")
            {
                if (number < 0 || number > 16)
                    throw Error("RtfUnicode", "Unicode fallback must be between zero and16characters.");
                _state.UnicodeFallback = number;
                return;
            }

            if (word == "u")
            {
                if (_fallback != 0)
                    throw Error("RtfUnicode", "A Unicode fallback was interrupted by another code unit.");
                if (number < short.MinValue || number > ushort.MaxValue)
                    throw Error("RtfUnicode", "A Unicode code unit exceeds its16-bit extent.");
                char value = unchecked((char)(ushort)number);
                if (_state.FontTable)
                    FontCharacter(value);
                else
                    Append(value);
                _fallback = _state.UnicodeFallback;
                return;
            }

            if (_state.FontTable)
            {
                WordErrors.Loss(_c, "RtfFontProperty", "An unsupported font-table property is omitted.");
                return;
            }

            if (word == "pict")
            {
                WordErrors.Node(_c);
                _state.Picture = new(_c);
                _state.PictureOwner = true;
                return;
            }

            if (_state.Picture != null)
            {
                if (word == "pngblip")
                    _state.Picture.Png = true;
                else if (word == "picwgoal")
                    _state.Picture.Width = number / 20.0;
                else if (word == "pichgoal")
                    _state.Picture.Height = number / 20.0;
                else if (word != "picw" && word != "pich")
                    throw Error("RtfPicture", "Only explicit PNG picture controls are supported.");
                return;
            }

            if (word == "b")
            {
                _state.Bold = digits == 0 || number != 0;
                return;
            }

            if (word == "i")
            {
                _state.Italic = digits == 0 || number != 0;
                return;
            }

            if (word == "fs")
            {
                if (number < 1 || number > 576)
                    throw Error("RtfFontSize", "The RTF font size exceeds this source profile.");
                _state.Size = number / 2.0;
                return;
            }

            if (word == "plain")
            {
                _state.Bold = _state.Italic = false;
                _state.Size = 12;
                _state.Font = _defaultFont;
                return;
            }

            if (word == "line")
            {
                Literal('\n');
                return;
            }

            if (word == "tab")
            {
                Literal('\t');
                return;
            }

            if (word == "par")
            {
                FlushParagraph(true);
                return;
            }

            if (word == "paperw" || word == "paperh")
            {
                if (number <= 0 || number > 288000)
                    throw Error("RtfGeometry", "RTF page geometry must be positive and bounded.");
                if (word == "paperw")
                    _section.Width = number / 20.0;
                else
                    _section.Height = number / 20.0;
                return;
            }

            if (word == "margl" || word == "margr" || word == "margt" || word == "margb")
            {
                if (number != 0)
                    WordErrors.Loss(_c, "RtfMargins", "Nonzero RTF margin reconstruction is outside this source profile.");
                return;
            }

            if (word == "page" || word == "sect")
            {
                FlushParagraph();
                if (_row != null)
                    throw Error("RtfTable", "A page cannot end inside an unfinished table row.");
                if (_model.Sections.Count == _options.MaximumPages)
                    throw Error("PageLimit", "The explicit source page ceiling was exceeded.");
                WordErrors.Node(_c);
                _section = new()
                {
                    Width = _section.Width,
                    Height = _section.Height
                };
                _model.Sections.Add(_section);
                _table = null;
                return;
            }

            if (word == "trowd")
            {
                FlushParagraph();
                if (_row != null)
                    throw Error("RtfTable", "Nested or unfinished table rows require a separate profile.");
                if (_table == null)
                {
                    WordErrors.Node(_c);
                    _table = new();
                    _section.Blocks.Add(_table);
                }

                WordErrors.Node(_c);
                _row = new();
                _widths.Clear();
                return;
            }

            if (word == "cellx")
            {
                if (_row == null || number <= 0 || _widths.Count == 256)
                    throw Error("RtfTable", "Cell boundaries require a bounded active row.");
                double width = number / 20.0 - _widths.Sum();
                if (width <= 0)
                    throw Error("RtfTable", "Cell boundaries must increase.");
                WordErrors.Node(_c, 64);
                _widths.Add(width);
                return;
            }

            if (word == "cell")
            {
                if (_row == null)
                    throw Error("RtfTable", "A cell ended outside an active row.");
                CommitRun();
                WordErrors.Node(_c);
                _row.Add(_paragraph);
                _paragraph = new();
                return;
            }

            if (word == "row")
            {
                if (_row == null || _row.Count == 0 || _paragraph.Runs.Count != 0 || _widths.Count != _row.Count)
                    throw Error("RtfTable", "A complete rectangular row requires one boundary per cell.");
                if (_table!.Rows.Count > 0 && _table.Rows[0].Count != _row.Count)
                    throw Error("RtfTable", "Merged/ragged RTF tables require another source profile.");
                if (_table.Rows.Count == 0)
                    _table.Widths.AddRange(_widths);
                else if (!_table.Widths.SequenceEqual(_widths))
                    throw Error("RtfTable", "Changing table grid widths requires another source profile.");
                _table.Rows.Add(_row);
                _row = null;
                return;
            }

            WordErrors.Loss(_c, "RtfControl", "An unsupported RTF control and its requested behavior are omitted.");
        }

        private void Literal(char value)
        {
            if (_state.Skip)
                return;
            if (_fallback > 0)
            {
                _fallback--;
                return;
            }

            if (_state.FontTable)
            {
                if (value == ';')
                {
                    string name = _state.FontName.Trim();
                    if (name.Length == 0 || name.Length > 128 || _fonts.ContainsKey(_state.Font))
                        throw Error("RtfFont", "Font declarations must be bounded and unique.");
                    WordErrors.Node(_c);
                    _fonts.Add(_state.Font, name);
                    _state.FontName = "";
                }
                else
                    FontCharacter(value);
                return;
            }

            if (_state.Picture != null)
            {
                if (char.IsWhiteSpace(value))
                    return;
                int n = Hex(value);
                if (_state.Picture.High < 0)
                    _state.Picture.High = n;
                else
                {
                    _state.Picture.Bytes.Add((byte)(_state.Picture.High * 16 + n));
                    _state.Picture.High = -1;
                }

                return;
            }

            Append(value >= 128 && value <= 255 ? Ansi(value) : value);
        }

        private void FontCharacter(char value)
        {
            if (_state.FontName.Length == 128)
                throw Error("RtfFont", "A font family exceeds its name ceiling.");
            _c.Consume(PdfResourceKind.ExpandedBytes, _state.FontName.Length * 2L + 64);
            _state.FontName += value;
        }

        private void Append(char value)
        {
            if (_characters++ == _options.MaximumCharacters)
                throw Error("CharacterLimit", "The explicit RTF character ceiling was exceeded.");
            _c.Consume(PdfResourceKind.ExpandedBytes, 8);
            if (_fonts.TryGetValue(_state.Font, out var declaredFamily))
                _state.Family = declaredFamily;
            var run = _currentRun;
            if (run == null || run.Family != _state.Family || run.Size != _state.Size || run.Bold != _state.Bold || run.Italic != _state.Italic)
            {
                CommitRun();
                WordErrors.Node(_c);
                run = new()
                {
                    Family = _state.Family,
                    Size = _state.Size,
                    Bold = _state.Bold,
                    Italic = _state.Italic
                };
                _paragraph.Runs.Add(run);
                _currentRun = run;
            }

            _pending.Append(value);
        }

        private void CommitRun()
        {
            if (_currentRun == null)
                return;
            _c.Consume(PdfResourceKind.ExpandedBytes, _pending.Length * 2L + 32);
            _currentRun.Text = _pending.ToString();
            _currentRun = null;
            _pending.Clear();
        }

        private void FlushParagraph(bool preserveEmpty = false)
        {
            CommitRun();
            if (_paragraph.Runs.Count == 0 && !preserveEmpty)
                return;
            if (_row != null)
                throw Error("RtfTable", "Multiple paragraphs in an RTF cell require an explicit cell-content profile.");
            WordErrors.Node(_c);
            _section.Blocks.Add(_paragraph);
            _paragraph = new();
            _table = null;
        }

        private void PictureComplete()
        {
            var picture = _state.Picture!;
            if (!picture.Png || picture.High >= 0 || picture.Width <= 0 || picture.Height <= 0)
                throw Error("RtfPicture", "An RTF picture requires complete PNG bytes and explicit positive twip geometry.");
            var bytes = picture.Bytes.Build();
            byte[] signature =
            {
                137,
                80,
                78,
                71,
                13,
                10,
                26,
                10
            };
            if (bytes.Length < signature.Length || signature.Where((value, index) => bytes.At(index) != value).Any())
                throw Error("RtfPicture", "An RTF PNG picture has an invalid file signature.");
            _ = PdfImagePng.Read(bytes, new(), _c);
            FlushParagraph();
            WordErrors.Node(_c);
            _section.Blocks.Add(new WordPicture { Bytes = bytes, Width = picture.Width, Height = picture.Height });
            _table = null;
        }

        private int Hex(char ch) => ch >= '0' && ch <= '9' ? ch - '0' : ch >= 'a' && ch <= 'f' ? ch - 'a' + 10 : ch >= 'A' && ch <= 'F' ? ch - 'A' + 10 : throw Error("RtfHex", "An RTF hexadecimal digit is invalid.");
        private static bool Letter(char ch) => ch >= 'a' && ch <= 'z' || ch >= 'A' && ch <= 'Z';
        private char Ansi(int value)
        {
            if (value < 128 || value >= 160)
                return (char)value;
            const string high = "\u20ac\0\u201a\u0192\u201e\u2026\u2020\u2021\u02c6\u2030\u0160\u2039\u0152\0\u017d\0\0\u2018\u2019\u201c\u201d\u2022\u2013\u2014\u02dc\u2122\u0161\u203a\u0153\0\u017e\u0178";
            char result = high[value - 128];
            if (result == 0)
                throw Error("RtfCodepage", "An undefined Windows1252 byte cannot be mapped faithfully.");
            return result;
        }
    }
}
