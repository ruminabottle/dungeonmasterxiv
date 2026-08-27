using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Diagnostics;

namespace DungeonMasterXIV.Relay.Transport;

/// <summary>
/// Refuses a connect request whose protocol version is not this relay's, before a WebSocket exists
/// (R-1.7b, A-1.5i).
/// </summary>
/// <remarks>
/// <para>
/// <b>Placed on the upgrade request rather than on a message, and that is the requirement rather
/// than a preference.</b> R-1.7b forbids a partial connection, not just a confusing one. A version
/// carried in an envelope could only be read after the socket was established, so a mismatched
/// client would be connected — routed, counted, holding a slot — and then told no. Here there is
/// nothing to be partway into.
/// </para>
/// <para>
/// The relay states <b>its own</b> version and no verdict. Which side is behind is a subtraction the
/// client can do, and doing it there keeps the user-facing sentence on the side that has a user —
/// this relay would otherwise have to describe a client build it has never heard of.
/// </para>
/// <para>
/// A request stating no version is refused too. It cannot be shown to be compatible, and a build old
/// enough not to send one predates this check and could not render the answer anyway.
/// </para>
/// </remarks>
public sealed class ProtocolVersionGate(RelayLog log)
{
    private readonly RelayLog _log = log;

    /// <summary>
    /// Whether this request may proceed to a WebSocket. Writes the refusal itself when it may not.
    /// </summary>
    public bool Admits(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var stated = ProtocolVersion.Parse(context.Request.Query[ProtocolVersion.QueryParameter]);
        if (stated == ProtocolVersion.Current)
        {
            return true;
        }

        // Stated on every refusal so the client can say which side is behind. Set before the status
        // code because a response that has begun cannot gain headers afterwards.
        context.Response.Headers[ProtocolVersion.Header] = ProtocolVersion.Current.ToString();
        context.Response.StatusCode = StatusCodes.Status426UpgradeRequired;

        _log.ConnectionRejected(
            "pre-connect",
            $"protocol version mismatch: client stated {stated?.ToString() ?? "none"}, relay speaks {ProtocolVersion.Current}");

        return false;
    }
}
