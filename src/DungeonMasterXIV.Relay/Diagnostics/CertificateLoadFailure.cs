using System.Runtime.InteropServices;

namespace DungeonMasterXIV.Relay.Diagnostics;

/// <summary>Explains a failed TLS certificate load, adding a permissions hint when the file cannot be read.</summary>
public static class CertificateLoadFailure
{
    public static string Describe(string path, string reason) =>
        Compose(path, CurrentIdentity(), reason, CannotBeRead(path));

    public static string Compose(string path, string identity, string reason, bool cannotBeRead)
    {
        var cause = $"Could not load the TLS certificate at '{path}': {reason}";

        if (!cannotBeRead)
        {
            return cause;
        }

        return cause
            + $" The relay runs as {identity}, and that identity must be able to read that file. "
            + "A bind-mounted secret keeps the ownership it has on the host, so a key that is 0600 "
            + "and owned by someone else is unreadable here however correct it looks outside the "
            + "container — give the file to that uid rather than widening its mode, because a "
            + "private key readable by everyone is the worse outcome.";
    }

    public static bool CannotBeRead(string path)
    {
        if (Directory.Exists(path))
        {
            return false;
        }

        try
        {
            using var stream = File.OpenRead(path);
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return true;
        }
        catch (IOException)
        {
            return false;
        }
    }

    public static string CurrentIdentity()
    {
        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS())
        {
            try
            {
                return $"uid {GetUserId()}";
            }
            catch (DllNotFoundException)
            {
            }
            catch (EntryPointNotFoundException)
            {
            }
        }

        return $"user '{Environment.UserName}'";
    }

    [DllImport("libc", EntryPoint = "getuid")]
    private static extern uint GetUserId();
}
