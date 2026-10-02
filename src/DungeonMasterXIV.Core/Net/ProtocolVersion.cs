using System;

namespace DungeonMasterXIV.Net;

/// <summary>The relay protocol version: adds it to the relay address and classifies version refusals.</summary>
public static class ProtocolVersion
{
    public const int Current = 2;

    public const string QueryParameter = "v";

    public const string Header = "X-DMX-Protocol-Version";

    public static Uri AppendTo(Uri relay)
    {
        ArgumentNullException.ThrowIfNull(relay);

        var existing = relay.Query.TrimStart('?');
        var query = existing.Length == 0
            ? $"{QueryParameter}={Current}"
            : $"{existing}&{QueryParameter}={Current}";

        return new UriBuilder(relay) { Query = query }.Uri;
    }

    public static int? Parse(string? stated) =>
        int.TryParse(stated, out var version) && version > 0 ? version : null;

    public static SessionFailure ClassifyRefusal(bool upgradeRefused, string? statedByRelay)
    {
        if (!upgradeRefused || Parse(statedByRelay) is not { } relayVersion)
        {
            return SessionFailure.RelayUnreachable;
        }

        if (relayVersion > Current)
        {
            return SessionFailure.PluginBehindRelay;
        }

        return relayVersion < Current ? SessionFailure.RelayBehindPlugin : SessionFailure.RelayUnreachable;
    }
}
