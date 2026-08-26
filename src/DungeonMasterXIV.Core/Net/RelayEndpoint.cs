using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The relay a client dials. R-1.8 requires this to be swappable, so it is validated here rather
/// than trusted from settings.
/// </summary>
/// <remarks>
/// D-2 permits exactly two network destinations: a configured session relay and a session peer.
/// Validation therefore rejects anything that is not an absolute wss:// or ws:// URL — not because
/// other schemes would fail to connect, but because a setting that accepted <c>https://</c> or a
/// bare hostname would be a route to a destination D-2 does not permit.
/// </remarks>
public static class RelayEndpoint
{
    /// <summary>The relay used when the user has not chosen one. A default, not a dependency.</summary>
    public const string Default = "wss://relay.dungeonmasterxiv.invalid/session";

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

        if (parsed.Scheme != "wss" && parsed.Scheme != "ws")
        {
            return false;
        }

        endpoint = parsed;
        return true;
    }

    /// <summary>
    /// Whether this address is encrypted in transit. <c>ws://</c> parses so that a self-hosted relay
    /// on a trusted network is reachable, but the UI states which one the user is on — D-11's
    /// payload encryption is unaffected either way, and claiming otherwise would be the kind of
    /// overstatement R-1.9 forbids in both directions.
    /// </summary>
    public static bool IsEncryptedTransport(Uri endpoint) => endpoint.Scheme == "wss";
}
