using System.IO;

namespace Fluent.Pdf;
internal static class PdfAtomicFile
{
    internal static void Write(string path, PdfOperationContext context, Action<Stream> write)
    {
        string full = Path.GetFullPath(path), directory = Path.GetDirectoryName(full)!;
        string temporary = Path.Combine(directory, ".fluent-pdf-" + Guid.NewGuid().ToString("N") + ".tmp");
        bool created = false;
        Exception? failure = null;
        try
        {
            context.Checkpoint();
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                created = true;
                write(output);
                output.Flush(true);
            }

            context.EnsureComplete();
            if (File.Exists(full))
                File.Replace(temporary, full, null);
            else
                File.Move(temporary, full);
        }
        catch (Exception error)
        {
            failure = error;
            throw;
        }
        finally
        {
            if (created)
                try
                {
                    File.Delete(temporary);
                }
                catch (Exception cleanup)when (failure != null && (cleanup is IOException || cleanup is UnauthorizedAccessException))
                {
                    failure.Data["Fluent.Pdf.CleanupFailure"] = cleanup;
                }
        }
    }
}
