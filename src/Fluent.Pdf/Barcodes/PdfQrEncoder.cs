using System.Text;

namespace Fluent.Pdf;
// Original QR Model2 byte/ECI encoder. Protocol layout records are kept separately from these algorithms.
internal static class PdfQrEncoder
{
    internal static PdfBarcodeGrid Encode(string text, PdfBarcodeOptions options, PdfOperationContext c)
    {
        var utf8 = new UTF8Encoding(false, true);
        int count;
        try
        {
            count = utf8.GetByteCount(text);
        }
        catch (EncoderFallbackException)
        {
            throw PdfBarcodes.Error(c, "PayloadEncoding", "QR text must contain valid Unicode scalars.");
        }

        if (count > 2953)
            throw PdfBarcodes.Error(c, "Capacity", "The QR UTF8 payload exceeds supported Model2 capacity.");
        int version = 0, capacity = 0;
        int[] layout = Array.Empty<int>();
        for (int candidate = options.QrVersion == 0 ? 1 : options.QrVersion; candidate <= (options.QrVersion == 0 ? 40 : options.QrVersion); candidate++)
        {
            c.Checkpoint();
            var blocks = PdfBarcodeTables.QrBlocks[(candidate - 1) * 4 + (int)options.ErrorCorrection - 1];
            int bytes = 0;
            for (int n = 0; n < blocks.Length; n += 3)
                bytes += blocks[n] * blocks[n + 2];
            long required = 12L + 4 + (candidate < 10 ? 8 : 16) + count * 8L;
            if (required <= bytes * 8L)
            {
                version = candidate;
                capacity = bytes;
                layout = blocks;
                break;
            }
        }

        if (version == 0)
            throw PdfBarcodes.Error(c, "Capacity", "The requested QR version and error correction cannot contain the complete UTF8 payload.");
        c.Consume(PdfResourceKind.ExpandedBytes, count + capacity + 256L);
        var data = new byte[capacity];
        byte[] payload = utf8.GetBytes(text);
        int bit = 0;
        void Add(int value, int bits)
        {
            c.Checkpoint();
            for (int n = bits - 1; n >= 0; n--)
            {
                if (bit >= data.Length * 8)
                    throw PdfBarcodes.Error(c, "Capacity", "QR bit packing exceeded the declared data capacity.");
                if (((value >> n) & 1) != 0)
                    data[bit / 8] |= (byte)(1 << (7 - bit % 8));
                bit++;
            }
        }

        Add(7, 4);
        Add(26, 8); // ECI UTF8.
        Add(4, 4);
        Add(count, version < 10 ? 8 : 16);
        foreach (byte value in payload)
            Add(value, 8);
        Add(0, Math.Min(4, data.Length * 8 - bit));
        while (bit % 8 != 0)
            Add(0, 1);
        int pad = 0;
        while (bit < data.Length * 8)
            Add(pad++ % 2 == 0 ? 0xec : 0x11, 8);
        var codewords = Interleave(data, layout, c);
        int size = version * 4 + 17;
        c.Consume(PdfResourceKind.ExpandedBytes, size * size * 2L + 1024);
        var raw = new bool[size * size];
        var function = new bool[raw.Length];
        void Set(int x, int y, bool black)
        {
            raw[y * size + x] = black;
            function[y * size + x] = true;
        }

        for (int n = 0; n < size; n++)
        {
            Set(6, n, n % 2 == 0);
            Set(n, 6, n % 2 == 0);
        }

        void Finder(int left, int top)
        {
            for (int y = -1; y <= 7; y++)
                for (int x = -1; x <= 7; x++)
                {
                    int px = left + x, py = top + y;
                    if (px < 0 || py < 0 || px >= size || py >= size)
                        continue;
                    bool inside = x >= 0 && x <= 6 && y >= 0 && y <= 6;
                    Set(px, py, inside && (x == 0 || x == 6 || y == 0 || y == 6 || x >= 2 && x <= 4 && y >= 2 && y <= 4));
                }
        }

        Finder(0, 0);
        Finder(size - 7, 0);
        Finder(0, size - 7);
        var centers = PdfBarcodeTables.QrAlignments[version - 1];
        foreach (int cy in centers)
            foreach (int cx in centers)
            {
                c.Checkpoint();
                if (cx == 6 && (cy == 6 || cy == size - 7) || cy == 6 && cx == size - 7)
                    continue;
                for (int y = -2; y <= 2; y++)
                    for (int x = -2; x <= 2; x++)
                        Set(cx + x, cy + y, Math.Max(Math.Abs(x), Math.Abs(y)) != 1);
            }

        void Format(bool[] target, int mask)
        {
            int level = options.ErrorCorrection == PdfBarcodeErrorCorrection.Low ? 1 : options.ErrorCorrection == PdfBarcodeErrorCorrection.Medium ? 0 : options.ErrorCorrection == PdfBarcodeErrorCorrection.Quartile ? 3 : 2;
            int value = (level << 3) | mask, remainder = value << 10;
            for (int shift = 14; shift >= 10; shift--)
                if ((remainder & (1 << shift)) != 0)
                    remainder ^= 0x537 << (shift - 10);
            int bits = ((value << 10) | remainder) ^ 0x5412;
            void F(int x, int y, int index)
            {
                target[y * size + x] = ((bits >> index) & 1) != 0;
                function[y * size + x] = true;
            }

            for (int n = 0; n < 6; n++)
                F(8, n, n);
            F(8, 7, 6);
            F(8, 8, 7);
            F(7, 8, 8);
            for (int n = 9; n < 15; n++)
                F(14 - n, 8, n);
            for (int n = 0; n < 8; n++)
                F(size - 1 - n, 8, n);
            for (int n = 8; n < 15; n++)
                F(8, size - 15 + n, n);
            target[(size - 8) * size + 8] = true;
            function[(size - 8) * size + 8] = true;
        }

        Format(raw, 0);
        if (version >= 7)
        {
            int remainder = version << 12;
            for (int shift = 17; shift >= 12; shift--)
                if ((remainder & (1 << shift)) != 0)
                    remainder ^= 0x1f25 << (shift - 12);
            int bits = (version << 12) | remainder;
            for (int n = 0; n < 18; n++)
            {
                bool black = ((bits >> n) & 1) != 0;
                Set(size - 11 + n % 3, n / 3, black);
                Set(n / 3, size - 11 + n % 3, black);
            }
        }

        int available = function.Count(v => !v);
        if (available / 8 != codewords.Length)
            throw PdfBarcodes.Error(c, "Layout", "QR protocol layout and codeword inventory disagree.");
        int ordinal = 0;
        bool up = true;
        for (int right = size - 1; right >= 1; right -= 2)
        {
            c.Checkpoint();
            if (right == 6)
                right--;
            for (int row = 0; row < size; row++)
                for (int dx = 0; dx < 2; dx++)
                {
                    int y = up ? size - row - 1 : row, x = right - dx, at = y * size + x;
                    if (function[at])
                        continue;
                    raw[at] = ordinal < codewords.Length * 8 && ((codewords[ordinal / 8] >> (7 - ordinal % 8)) & 1) != 0;
                    ordinal++;
                }

            up = !up;
        }

        bool[]? best = null;
        int bestScore = int.MaxValue, selected = 0;
        for (int mask = 0; mask < 8; mask++)
        {
            c.Checkpoint();
            c.Consume(PdfResourceKind.ExpandedBytes, raw.Length);
            var candidate = (bool[])raw.Clone();
            for (int y = 0; y < size; y++)
            {
                c.Checkpoint();
                for (int x = 0; x < size; x++)
                    if (!function[y * size + x] && Mask(mask, x, y))
                        candidate[y * size + x] = !candidate[y * size + x];
            }

            Format(candidate, mask);
            int score = Penalty(candidate, size, c);
            if (score < bestScore)
            {
                best = candidate;
                bestScore = score;
                selected = mask;
            }
        }

        return new(best!, size, size, version, selected);
    }

    private static byte[] Interleave(byte[] data, int[] layout, PdfOperationContext c)
    {
        int total = 0, blocks = 0, largest = 0, ecc = layout[1] - layout[2];
        for (int n = 0; n < layout.Length; n += 3)
        {
            total += layout[n] * layout[n + 1];
            blocks += layout[n];
            largest = Math.Max(largest, layout[n + 2]);
        }

        c.Consume(PdfResourceKind.ExpandedBytes, total * 4L + blocks * 64L + ecc * ecc * 2L + 1024);
        var generator = new byte[]
        {
            1
        };
        int root = 1;
        for (int n = 0; n < ecc; n++)
        {
            c.Checkpoint();
            var next = new byte[generator.Length + 1];
            for (int j = 0; j < generator.Length; j++)
            {
                next[j] ^= generator[j];
                next[j + 1] ^= Multiply(generator[j], root);
            }

            generator = next;
            root = Multiply(root, 2);
        }

        var payloads = new List<byte[]>();
        var corrections = new List<byte[]>();
        int source = 0;
        for (int group = 0; group < layout.Length; group += 3)
            for (int b = 0; b < layout[group]; b++)
            {
                c.Checkpoint();
                var payload = new byte[layout[group + 2]];
                Buffer.BlockCopy(data, source, payload, 0, payload.Length);
                source += payload.Length;
                var remainder = new byte[ecc];
                foreach (byte value in payload)
                {
                    c.Checkpoint();
                    int factor = value ^ remainder[0];
                    for (int n = 0; n < ecc - 1; n++)
                        remainder[n] = remainder[n + 1];
                    remainder[ecc - 1] = 0;
                    for (int n = 0; n < ecc; n++)
                        remainder[n] ^= Multiply(factor, generator[n + 1]);
                }

                payloads.Add(payload);
                corrections.Add(remainder);
            }

        var output = new byte[total];
        int at = 0;
        for (int n = 0; n < largest; n++)
            foreach (var payload in payloads)
                if (n < payload.Length)
                    output[at++] = payload[n];
        for (int n = 0; n < ecc; n++)
            foreach (var correction in corrections)
                output[at++] = correction[n];
        return output;
    }

    private static byte Multiply(int a, int b)
    {
        int result = 0;
        while (b != 0)
        {
            if ((b & 1) != 0)
                result ^= a;
            a <<= 1;
            if ((a & 256) != 0)
                a ^= 0x11d;
            b >>= 1;
        }

        return (byte)result;
    }

    private static bool Mask(int mask, int x, int y) => mask switch
    {
        0 => (x + y) % 2 == 0,
        1 => y % 2 == 0,
        2 => x % 3 == 0,
        3 => (x + y) % 3 == 0,
        4 => (y / 2 + x / 3) % 2 == 0,
        5 => x * y % 2 + x * y % 3 == 0,
        6 => (x * y % 2 + x * y % 3) % 2 == 0,
        7 => ((x + y) % 2 + x * y % 3) % 2 == 0,
        _ => false
    };
    private static int Penalty(bool[] grid, int size, PdfOperationContext c)
    {
        int score = 0;
        for (int axis = 0; axis < 2; axis++)
            for (int line = 0; line < size; line++)
            {
                c.Checkpoint();
                bool Read(int n) => n >= 0 && n < size && grid[axis == 0 ? line * size + n : n * size + line];
                int run = 1;
                for (int n = 1; n < size; n++)
                {
                    if (Read(n) == Read(n - 1))
                        run++;
                    else
                    {
                        if (run >= 5)
                            score += run - 2;
                        run = 1;
                    }
                }

                if (run >= 5)
                    score += run - 2;
                int window = 0;
                for (int n = -4; n < size + 4; n++)
                {
                    window = ((window << 1) | (Read(n) ? 1 : 0)) & 2047;
                    if (n >= 6 && (window == 0x5d0 || window == 0x05d))
                        score += 40;
                }
            }

        for (int y = 0; y < size - 1; y++)
            for (int x = 0; x < size - 1; x++)
            {
                bool value = grid[y * size + x];
                if (value == grid[y * size + x + 1] && value == grid[(y + 1) * size + x] && value == grid[(y + 1) * size + x + 1])
                    score += 3;
            }

        int dark = grid.Count(v => v);
        return score + (int)(Math.Abs(dark * 20L - size * size * 10L) / (size * size)) * 10;
    }
}
