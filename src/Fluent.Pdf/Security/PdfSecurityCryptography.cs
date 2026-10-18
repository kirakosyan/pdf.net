using System.Security.Cryptography;

namespace Fluent.Pdf;
// Original bounded composition of platform primitives and the standardized PDF key algorithms.
internal static class PdfSecurityCryptography
{
    internal static byte[] Random(int length, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.ExpandedBytes, length + 1024L);
        var result = new byte[length];
        try
        {
            using var random = RandomNumberGenerator.Create();
            random.GetBytes(result);
            context.Checkpoint();
            return result;
        }
        catch
        {
            Clear(result);
            throw;
        }
    }

    internal static byte[] Hash(byte[] input, int bits, PdfOperationContext context)
    {
        context.Checkpoint();
        context.Consume(PdfResourceKind.ExpandedBytes, bits / 8 + 4096L);
        using HashAlgorithm algorithm = bits switch
        {
            128 => MD5.Create(),
            160 => SHA1.Create(),
            256 => SHA256.Create(),
            384 => SHA384.Create(),
            512 => SHA512.Create(),
            _ => throw new ArgumentOutOfRangeException(nameof(bits))};
        var result = algorithm.ComputeHash(input);
        try
        {
            context.Checkpoint();
            return result;
        }
        catch
        {
            Clear(result);
            throw;
        }
    }

    internal static byte[] Aes(byte[] key, byte[] iv, byte[] input, bool encrypt, CipherMode mode, PaddingMode padding, PdfOperationContext context)
    {
        context.Checkpoint();
        if (key.Length != 16 && key.Length != 24 && key.Length != 32 || iv.Length != 16 || mode != CipherMode.CBC && mode != CipherMode.ECB || padding != PaddingMode.None && padding != PaddingMode.PKCS7)
            throw new ArgumentException("A supported AES key, IV, mode and padding selection are required.");
        if (padding == PaddingMode.None && input.Length % 16 != 0 || !encrypt && (input.Length == 0 && padding == PaddingMode.PKCS7 || input.Length % 16 != 0))
            throw PdfWriterCore.Error(context, "Security.CipherExtent", "The encrypted content does not have a valid AES block extent.");
        context.Consume(PdfResourceKind.ExpandedBytes, input.LongLength + 16 + key.Length + iv.Length + 16384L);
        try
        {
            using var aes = System.Security.Cryptography.Aes.Create();
            aes.Mode = mode;
            aes.Padding = padding;
            aes.Key = key;
            if (mode == CipherMode.CBC)
                aes.IV = iv;
            using var transform = encrypt ? aes.CreateEncryptor() : aes.CreateDecryptor();
            var result = transform.TransformFinalBlock(input, 0, input.Length);
            try
            {
                context.Checkpoint();
                return result;
            }
            catch
            {
                Clear(result);
                throw;
            }
        }
        catch (CryptographicException)
        {
            context.Checkpoint();
            throw PdfWriterCore.Error(context, "Security.CipherFailed", "The requested cryptographic operation failed or the encrypted content is malformed.");
        }
    }

    internal static byte[] Revision6Hash(byte[] preparedPassword, byte[] salt, byte[] userEntry, PdfOperationContext context)
    {
        if (preparedPassword.Length > 127 || salt.Length != 8 || userEntry.Length != 0 && userEntry.Length != 48)
            throw new ArgumentException("Invalid standardized password hash inputs.");
        byte[] seed = Concat(context, preparedPassword, salt, userEntry), key;
        try
        {
            key = Hash(seed, 256, context);
        }
        finally
        {
            Clear(seed);
        }

        try
        {
            int round = 0, last;
            do
            {
                context.Checkpoint();
                context.Consume(PdfResourceKind.Objects, 1);
                int part = preparedPassword.Length + key.Length + userEntry.Length;
                byte[]? repeated = null, encrypted = null, cipherKey = null, iv = null;
                try
                {
                    context.Consume(PdfResourceKind.ExpandedBytes, part * 64L);
                    repeated = new byte[part * 64];
                    cipherKey = Slice(key, 0, 16, context);
                    iv = Slice(key, 16, 16, context);
                    int at = 0;
                    for (int i = 0; i < 64; i++)
                    {
                        Buffer.BlockCopy(preparedPassword, 0, repeated, at, preparedPassword.Length);
                        at += preparedPassword.Length;
                        Buffer.BlockCopy(key, 0, repeated, at, key.Length);
                        at += key.Length;
                        Buffer.BlockCopy(userEntry, 0, repeated, at, userEntry.Length);
                        at += userEntry.Length;
                    }

                    encrypted = Aes(cipherKey, iv, repeated, true, CipherMode.CBC, PaddingMode.None, context);
                    // 256 is congruent to 1 modulo 3, so a byte sum has the required big-endian integer remainder.
                    int remainder = 0;
                    for (int i = 0; i < 16; i++)
                        remainder += encrypted[i];
                    var next = Hash(encrypted, remainder % 3 == 0 ? 256 : remainder % 3 == 1 ? 384 : 512, context);
                    Clear(key);
                    key = next;
                    last = encrypted[encrypted.Length - 1];
                    round++;
                }
                finally
                {
                    Clear(repeated);
                    Clear(encrypted);
                    Clear(cipherKey);
                    Clear(iv);
                }
            }
            while (round < 64 || last > round - 32);
            return Slice(key, 0, 32, context);
        }
        finally
        {
            Clear(key);
        }
    }

    internal static byte[] Arcfour(byte[] key, byte[] input, PdfOperationContext context)
    {
        if (key.Length < 1 || key.Length > 256)
            throw new ArgumentOutOfRangeException(nameof(key));
        context.Consume(PdfResourceKind.ExpandedBytes, input.LongLength + 256);
        var state = new byte[256];
        var output = new byte[input.Length];
        try
        {
            for (int i = 0; i < 256; i++)
                state[i] = (byte)i;
            int j = 0;
            for (int i = 0; i < 256; i++)
            {
                j = (j + state[i] + key[i % key.Length]) & 255;
                Swap(i, j);
            }

            int x = 0;
            j = 0;
            for (int i = 0; i < input.Length; i++)
            {
                if ((i & 4095) == 0)
                    context.Checkpoint();
                x = (x + 1) & 255;
                j = (j + state[x]) & 255;
                Swap(x, j);
                output[i] = (byte)(input[i] ^ state[(state[x] + state[j]) & 255]);
            }

            context.Checkpoint();
            return output;
            void Swap(int a, int b)
            {
                byte temporary = state[a];
                state[a] = state[b];
                state[b] = temporary;
            }
        }
        catch
        {
            Clear(output);
            throw;
        }
        finally
        {
            Clear(state);
        }
    }

    internal static byte[] Concat(PdfOperationContext context, params byte[][] chunks)
    {
        long length = 0;
        foreach (var chunk in chunks)
            length += chunk.LongLength;
        if (length > int.MaxValue)
            throw PdfWriterCore.Error(context, "Security.CipherExtent", "The cryptographic input exceeds the supported array extent.");
        context.Consume(PdfResourceKind.ExpandedBytes, length);
        var result = new byte[(int)length];
        int at = 0;
        try
        {
            foreach (var chunk in chunks)
            {
                context.Checkpoint();
                Buffer.BlockCopy(chunk, 0, result, at, chunk.Length);
                at += chunk.Length;
            }

            return result;
        }
        catch
        {
            Clear(result);
            throw;
        }
    }

    internal static byte[] Slice(byte[] input, int offset, int count, PdfOperationContext context)
    {
        context.Consume(PdfResourceKind.ExpandedBytes, count);
        var result = new byte[count];
        Buffer.BlockCopy(input, offset, result, 0, count);
        return result;
    }

    internal static bool Equal(byte[] left, byte[] right)
    {
        if (left.Length != right.Length)
            return false;
        int different = 0;
        for (int i = 0; i < left.Length; i++)
            different |= left[i] ^ right[i];
        return different == 0;
    }

    internal static void Clear(byte[]? bytes)
    {
        if (bytes != null)
            Array.Clear(bytes, 0, bytes.Length);
    }
}
