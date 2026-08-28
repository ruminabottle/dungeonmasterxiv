using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Net;

/// <summary>
/// Makes a session key pair, reporting failure rather than throwing.
/// </summary>
/// <remarks>
/// <para>
/// <b>Its own type because BOTH entry points need it and neither owns it (DMXENG-31).</b> This was
/// <c>SessionCoordinator.TryMakeKeys</c>, called by <c>StartHosting</c> and by the join request.
/// When the join side moved to <see cref="JoinRequester"/> the helper had to go somewhere: moving it
/// with the join would have left hosting without it, and duplicating it was refused outright — two
/// copies of a catch is two places for BUG-61's guard to drift. So it sits where both can reach it
/// and neither is its owner.
/// </para>
/// <para>
/// <b>Only <see cref="CryptographicException"/>, deliberately.</b> That is what the reported
/// failure is, and a broader catch here would hide a genuine defect in this method's own
/// callers behind a message about keys. The exception is not logged or re-wrapped: the
/// user-facing answer is the failure value, and T-46 owns what gets logged.
/// </para>
/// </remarks>
public static class SessionKeyPair
{
    /// <summary>Makes a key pair, or reports that the machine could not.</summary>
    /// <param name="newKeys">How a pair is made. Injected so BUG-61's throw is drivable from a test.</param>
    /// <param name="keys">The new pair, or null on failure.</param>
    /// <returns>Whether a pair was made.</returns>
    public static bool TryMake(Func<SessionKeyExchange> newKeys, out SessionKeyExchange? keys)
    {
        ArgumentNullException.ThrowIfNull(newKeys);

        try
        {
            keys = newKeys();
            return true;
        }
        catch (CryptographicException)
        {
            keys = null;
            return false;
        }
    }
}
