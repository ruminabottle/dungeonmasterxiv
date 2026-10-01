using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Diagnostics;

namespace DungeonMasterXIV.Relay.Transport;

/// <summary>Refuses, with a 426 answer, a connection whose stated protocol version differs from the relay's.</summary>
public sealed class ProtocolVersionGate(RelayLog log)
{
    private readonly RelayLog _log = log;

    public bool Admits(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stated = ProtocolVersion.Parse(context.Request.Query[ProtocolVersion.QueryParameter]);
        if (stated == ProtocolVersion.Current)
        {
            return true;
        }

        context.Response.Headers[ProtocolVersion.Header] = ProtocolVersion.Current.ToString();
        context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;

        _log.ConnectionRejected(
            "pre-connect",
            $"protocol version mismatch: client stated {stated?.ToString() ?? "none"}, relay speaks {ProtocolVersion.Current}");

        return false;
    }
}
