using System.Runtime.InteropServices;

namespace DungeonMasterXIV.Relay.Diagnostics;

/// <summary>
/// Turns a failure to load the TLS certificate into something an operator can act on.
/// </summary>
/// <remarks>
/// BUG-15: the platform crypto layer reports an unreadable certificate as a bare library error —
/// <c>error:10080002:BIO routines::system lib</c> on Linux — which names neither the file nor the
/// reason, so it reads like a corrupt or wrong-password PKCS#12 and sends the operator to inspect
/// the one thing that is not wrong. The container runs unprivileged and a bind-mounted secret keeps
/// its ownership from the host, so "the process may not read it" is the likely cause and is the
/// thing the message has to say out loud.
/// </remarks>
public static class CertificateLoadFailure
{
    /// <summary>
    /// The operator-facing message for a certificate at <paramref name="path"/> that the process
    /// running as <paramref name="identity"/> could not load, because of <paramref name="reason"/>.
    /// </summary>
    public static string Describe(string path, string identity, string reason) =>
        $"Could not load the TLS certificate at '{path}'. The relay runs as {identity}, and that "
        + "identity must be able to read that file. A bind-mounted secret keeps the ownership it "
        + "has on the host, so a key that is 0600 and owned by someone else is unreadable here "
        + "however correct it looks outside the container — give the file to that uid rather than "
        + "widening its mode, because a private key readable by everyone is the worse outcome. "
        + $"Underlying error: {reason}";

    /// <summary>
    /// Who this process is running as, as the message should name it.
    /// </summary>
    /// <remarks>
    /// Asks the kernel rather than reading <c>APP_UID</c>, which is what the image was <i>built</i>
    /// to run as and not necessarily what it is running as: an operator who passed <c>--user</c>
    /// would be told to grant the file to a uid that is not the one being refused, which is the
    /// same class of misdirection this whole fix exists to remove. Falls back to the user name
    /// where there is no <c>getuid</c> to ask.
    /// </remarks>
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
                // Not swallowing a fault: this runs while building the text of another exception,
                // and a runtime with no resolvable libc — musl, say — must not turn an unreadable
                // certificate into a missing-library error. Falls through to the name below.
            }
            catch (EntryPointNotFoundException)
            {
                // As above.
            }
        }

        return $"user '{Environment.UserName}'";
    }

    // DllImport rather than LibraryImport, and not as an oversight: the source generator emits
    // unsafe code, so LibraryImport here would mean AllowUnsafeBlocks on the whole relay project.
    // Turning unsafe on everywhere to read one integer is a poor trade; SYSLIB1054's advice does
    // not account for a project that is otherwise entirely safe.
    [DllImport("libc", EntryPoint = "getuid")]
    private static extern uint GetUserId();
}
