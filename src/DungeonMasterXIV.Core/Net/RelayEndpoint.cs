using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The relay a client dials. R-1.8 requires this to be swappable, so it is validated here rather
/// than trusted from settings.
/// </summary>
/// <remarks>
/// <para>
/// D-2 permits exactly two network destinations: a configured session relay and a session peer.
/// Validation therefore rejects anything that is not an absolute WebSocket URL — not because other
/// schemes would fail to connect, but because a setting that accepted <c>https://</c> or a bare
/// hostname would be a route to a destination D-2 does not permit.
/// </para>
/// <para>
/// <b>TLS is required, and D-11 is not the reason.</b> D-11 encrypts payloads, so content survives
/// an unencrypted transport intact — but anyone on the path still sees the session code, the
/// timing, the message sizes and the cadence, which is the cross-session correlation D-8 forbids.
/// The same argument makes a TLS-terminating proxy in front of the relay approve-blocking, and
/// allowing it here would have been the same exposure by a different route.
/// </para>
/// <para>
/// A warning was the obvious alternative and is not one: the person who clicks through it is not
/// the person who pays. The exposed metadata belongs to every other participant in the session, and
/// none of them saw the dialog.
/// </para>
/// <para>
/// Loopback is the one exception, and it is principled rather than a compromise — there is no
/// observable network path, so none of the above applies.
/// </para>
/// </remarks>
public static class RelayEndpoint
{
    /// <summary>The relay used when the user has not chosen one. A default, not a dependency.</summary>
    public const string Default = "wss://relay.dungeonmasterxiv.invalid" + SessionPath;

    /// <summary>
    /// The path a session connection is made on. <b>The one place this value exists.</b>
    /// </summary>
    /// <remarks>
    /// <para>
    /// The relay serves this and the client dials it, so they must agree — and they did not, twice.
    /// A relay on a different path answers the upgrade with a 400 and the plugin reports it
    /// unreachable, which is a correctly deployed relay and a correctly configured client both
    /// looking broken, with the two-machine A-1.5a chain spent chasing NAT before anyone reads a
    /// path string.
    /// </para>
    /// <para>
    /// It is a shared constant rather than two values an assertion compares because the assertion
    /// only ever caught the case somebody had already thought to write down. One value cannot
    /// disagree with itself.
    /// </para>
    /// </remarks>
    public const string SessionPath = "/session";

    /// <summary>
    /// Whether <paramref name="candidate"/> is a usable relay address.
    /// </summary>
    public static bool TryParse(string? candidate, out Uri? endpoint)
    {
        endpoint = null;
        if (string.IsNullOrWhiteSpace(candidate)
            || !Uri.TryCreate(candidate.Trim(), UriKind.Absolute, out var parsed))
        {
            return false;
        }

        if (!IsPermittedScheme(parsed))
        {
            return false;
        }

        endpoint = parsed;
        return true;
    }

    // wss:// anywhere; ws:// only where there is no network path to observe. Uri.IsLoopback is the
    // BCL's own answer rather than a hostname comparison of ours — a string check would accept
    // "localhost.example.org", which is a perfectly ordinary remote host.
    private static bool IsPermittedScheme(Uri candidate) =>
        candidate.Scheme == "wss" || (candidate.Scheme == "ws" && candidate.IsLoopback);
}
