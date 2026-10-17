using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;

internal static unsafe class Exports
{
    private static readonly nint Abi = Marshal.StringToCoTaskMemUTF8("fluent-pdf-fixture-1");
    private static readonly nint Version = Marshal.StringToCoTaskMemUTF8("1.0.0");
    [UnmanagedCallersOnly(EntryPoint = "fluent_pdf_bridge_abi_v1", CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static nint GetAbi() => Abi;
    [UnmanagedCallersOnly(EntryPoint = "fluent_pdf_bridge_version_v1", CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static nint GetVersion() => Version;
    [UnmanagedCallersOnly(EntryPoint = "fluent_pdf_bridge_execute_v1", CallConvs = new[] { typeof(CallConvCdecl) })]
    internal static int Execute(byte* input, nuint length, byte* output, nuint capacity, nuint* written)
    {
        if (length > int.MaxValue || capacity > int.MaxValue || written == null)
            return 99;
        try
        {
            var source = new ReadOnlySpan<byte>(input, (int)length);
            *written = 0;
            if (source.Length > 0 && source[0] == 7)
            {
                if (capacity < 2)
                    return 99;
                string path = Encoding.UTF8.GetString(source.Slice(1));
                int reached = 0, opened = 0;
                var thread = new Thread(() =>
                {
                    reached = 42;
                    try
                    {
                        using var file = File.OpenRead(path);
                        opened = 1;
                    }
                    catch (IOException)
                    {
                    }
                    catch (UnauthorizedAccessException)
                    {
                    }
                });
                thread.Start();
                thread.Join();
                output[0] = (byte)reached;
                output[1] = (byte)opened;
                *written = 2;
                return 0;
            }

            int count = Math.Max(0, source.Length - 1);
            if ((nuint)count > capacity)
                return 99;
            if (count > 0)
                source.Slice(1).CopyTo(new Span<byte>(output, count));
            *written = (nuint)count;
            return 0;
        }
        catch
        {
            return 99;
        }
    }
}
