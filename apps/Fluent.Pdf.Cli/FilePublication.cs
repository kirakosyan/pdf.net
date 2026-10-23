using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Runtime.Versioning;
using Fluent.Pdf;

namespace Fluent.Pdf.CommandLine;
internal sealed class Publication
{
    internal string? TemporaryPath;
    internal bool Published;
    internal bool StdoutStarted;
    internal string Cleanup = "not-needed";
    internal string? TemporaryName;
    internal bool PendingAdmission;
}

internal static class FilePublication
{
    internal static void Publish(string destination, Stream content, bool overwrite, Publication state, CancellationToken token)
    {
        string directory = Path.GetDirectoryName(destination)!;
        state.TemporaryName = ".fluent-pdf-" + Convert.ToHexString(RandomNumberGenerator.GetBytes(16)).ToLowerInvariant() + ".tmp";
        string temporary = Path.Combine(directory, state.TemporaryName);
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            Options = FileOptions.SequentialScan
        };
        if (!OperatingSystem.IsWindows())
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        using (var file = OperatingSystem.IsWindows() ? CreateWindows(temporary) : new FileStream(temporary, options))
        {
            state.TemporaryPath = temporary;
            state.Cleanup = "pending";
            content.Position = 0;
            byte[] buffer = new byte[65536];
            try
            {
                int count;
                while ((count = content.Read(buffer, 0, buffer.Length)) != 0)
                {
                    token.ThrowIfCancellationRequested();
                    file.Write(buffer, 0, count);
                }

                file.Flush(true);
            }
            finally
            {
                CryptographicOperations.ZeroMemory(buffer);
            }
        }

        token.ThrowIfCancellationRequested();
        File.Move(temporary, destination, overwrite);
        state.Published = true;
        state.TemporaryPath = null;
        state.TemporaryName = null;
        state.Cleanup = "removed";
    }

    internal static void Cleanup(Publication state)
    {
        if (state.TemporaryPath == null)
            return;
        try
        {
            File.Delete(state.TemporaryPath);
            state.TemporaryPath = null;
            state.TemporaryName = null;
            state.Cleanup = "removed";
        }
        catch
        {
            state.Cleanup = "failed";
        }
    }

    internal static async Task<PdfPassword> ReadPassword(string path, CancellationToken token)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new CliFailure("CLI.SecretPermissions", 2);
        await using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None, 4096, FileOptions.Asynchronous);
        if (OperatingSystem.IsWindows())
            CheckAcl(file);
        else if ((File.GetUnixFileMode(file.SafeFileHandle) & ~(UnixFileMode.UserRead | UnixFileMode.UserWrite)) != 0)
            throw new CliFailure("CLI.SecretPermissions", 2);
        var bytes = new byte[4097];
        byte[]? prepared = null;
        try
        {
            int length = 0;
            while (length < bytes.Length)
            {
                int count = await file.ReadAsync(bytes.AsMemory(length), token);
                if (count == 0)
                    break;
                length += count;
            }

            if (length > 4096)
                throw new CliFailure("CLI.SecretLimit", 4);
            prepared = new byte[length];
            Array.Copy(bytes, prepared, length);
            return new PdfPassword(prepared);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            if (prepared != null)
                CryptographicOperations.ZeroMemory(prepared);
        }
    }

    [SupportedOSPlatform("windows")]
    private static FileStream CreateWindows(string path)
    {
        var security = new FileSecurity();
        using var identity = WindowsIdentity.GetCurrent();
        var owner = identity.User!;
        security.SetOwner(owner);
        security.SetAccessRuleProtection(true, false);
        foreach (var sid in new[]
        {
            owner,
            new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
            new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null)
        }

        )
            security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, AccessControlType.Allow));
        return new FileInfo(path).Create(FileMode.CreateNew, FileSystemRights.FullControl, FileShare.None, 65536, FileOptions.SequentialScan, security);
    }

    [SupportedOSPlatform("windows")]
    private static void CheckAcl(FileStream file)
    {
        var security = file.GetAccessControl();
        using var identity = WindowsIdentity.GetCurrent();
        var owner = identity.User!;
        if (!owner.Equals(security.GetOwner(typeof(SecurityIdentifier))))
            throw new CliFailure("CLI.SecretPermissions", 2);
        foreach (FileSystemAccessRule rule in security.GetAccessRules(true, true, typeof(SecurityIdentifier)))
        {
            var sid = (SecurityIdentifier)rule.IdentityReference;
            if (rule.AccessControlType == AccessControlType.Allow && !sid.Equals(owner) && !sid.IsWellKnown(WellKnownSidType.LocalSystemSid) && !sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid))
                throw new CliFailure("CLI.SecretPermissions", 2);
        }
    }
}
