using System;

namespace DungeonMasterXIV.Net;

public static class RelayEndpoint
{
    public const string Default = "wss://relay.ruminabottle.com" + SessionPath;

    public const string SessionPath = "/session";

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

    private static bool IsPermittedScheme(Uri candidate) =>
        candidate.Scheme == "wss" || (candidate.Scheme == "ws" && candidate.IsLoopback);
}
