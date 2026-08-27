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
/// its ownership from the host, so "the process may not read it" is a likely cause and is a thing
/// the message has to be able to say out loud.
/// <para>
/// BUG-17: <b>and it must say it only when it is true.</b> The first version of this said it for
/// every load failure, so a wrong <c>CERT_PASSWORD</c> on a perfectly readable file produced sixty-
/// five words instructing the operator to <c>chown</c> it — BUG-15 pointing the other way, sending
/// them to inspect the one thing that was not wrong. The underlying cause now leads, and the
/// permissions advice is a suffix conditioned on <see cref="CannotBeRead"/>.
/// </para>
/// </remarks>
public static class CertificateLoadFailure
{
    /// <summary>
    /// The operator-facing message for a certificate at <paramref name="path"/> that could not be
    /// loaded, because of <paramref name="reason"/>.
    /// </summary>
    public static string Describe(string path, string reason) =>
        Compose(path, CurrentIdentity(), reason, CannotBeRead(path));

    /// <summary>
    /// The message itself, with nothing measured — <paramref name="cannotBeRead"/> is the caller's
    /// finding rather than this method's, so the wording can be tested without a fixture that has
    /// to be made unreadable first.
    /// </summary>
    /// <remarks>
    /// <b>The underlying reason comes first and unconditionally.</b> It is the only clause backed by
    /// evidence in every case, and an operator reads top-down: whatever leads is what they act on.
    /// </remarks>
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

    /// <summary>
    /// Whether this process is refused read access to <paramref name="path"/>.
    /// </summary>
    /// <remarks>
    /// <b>Asks the filesystem rather than reading the crypto layer's message.</b> Matching on
    /// "BIO routines" or "Permission denied" would put the platform-specific string back in, one
    /// layer along, and defeat the reason <see cref="RelayApp"/> catches broadly in the first place
    /// — the failure that matters is the one nobody would think to name. Opening the file settles it
    /// in one syscall, on every platform, in the same process and therefore as the same uid.
    /// <para>
    /// A file that is missing, locked or a directory is not a permissions finding and must not be
    /// reported as one; only an outright refusal counts. The directory case is checked explicitly
    /// rather than left to the exception type, which cannot tell it apart from a refusal — this
    /// paragraph described an intention the code did not have until BUG-21.
    /// </para>
    /// </remarks>
    public static bool CannotBeRead(string path)
    {
        // Asked before the open, because the open cannot answer it. On Unix File.OpenRead throws
        // UnauthorizedAccessException for a DIRECTORY as well as for a refusal, so the exception
        // type cannot separate "you may not read this" from "this is not a file" and a directory
        // reached the permissions arm. An operator who wrote /run/secrets where they meant
        // /run/secrets/relay-certificate was told to chown a directory whose ownership was fine.
        // BUG-21.
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
            // Missing, locked, or otherwise unopenable for a reason that is not permission. The
            // underlying error already leads the message and says what it was.
            return false;
        }
    }

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
