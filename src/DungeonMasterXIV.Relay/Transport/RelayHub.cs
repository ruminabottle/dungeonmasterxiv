using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Diagnostics;
using DungeonMasterXIV.Relay.Sessions;

namespace DungeonMasterXIV.Relay.Transport;

/// <summary>
/// Applies the router's decisions to real connections: decode what arrived, ask
/// <see cref="RelayRouter"/> what to do with it, do that, and record it.
/// </summary>
/// <remarks>
/// The one place a routing decision meets a socket. It re-encodes nothing on the forwarding path —
/// the bytes a member receives are the bytes the sender sent, so the relay cannot alter a payload
/// even in principle, and a re-framed one would fail its authentication tag anyway because
/// <see cref="SessionCipher"/> binds the envelope's type and session code into the tag.
/// </remarks>
public sealed class RelayHub(
    RelayRouter router,
    SessionRegistry registry,
    ConnectionDirectory directory,
    RelayLog log)
{
    private readonly RelayRouter _router = router;
    private readonly SessionRegistry _registry = registry;
    private readonly ConnectionDirectory _directory = directory;
    private readonly RelayLog _log = log;

    /// <summary>Handles one complete message received from <paramref name="sender"/>.</summary>
    public async ValueTask ReceiveAsync(IRelayConnection sender, byte[] bytes, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(sender);

        if (!EnvelopeCodec.TryDecode(bytes, out var envelope) || envelope is null)
        {
            _log.Routed(sender.Id, "none", RelayDecision.Drop(RelayOutcome.MalformedEnvelope));
            return;
        }

        var decision = _router.Route(envelope, sender.Id);
        _log.Routed(sender.Id, envelope.SessionCode, decision);

        switch (decision.Action)
        {
            case RelayAction.ReplyToSender when decision.Reply is not null:
                await sender.SendAsync(EnvelopeCodec.Encode(decision.Reply), cancellationToken).ConfigureAwait(false);
                break;

            case RelayAction.Forward:
                await ForwardAsync(bytes, decision.Recipients, cancellationToken).ConfigureAwait(false);

                if (decision.CloseRecipients)
                {
                    await CloseAsync(decision.Recipients, cancellationToken).ConfigureAwait(false);
                }

                break;

            case RelayAction.Drop:
            default:
                break;
        }
    }

    /// <summary>Drops a connection from its session and from the directory.</summary>
    public void Disconnect(IRelayConnection connection, string reason)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var removal = _registry.Remove(connection.Id);
        _directory.Remove(connection.Id);
        _log.ConnectionClosed(connection.Id, removal, reason);
    }

    private async ValueTask CloseAsync(IReadOnlyList<string> recipients, CancellationToken cancellationToken)
    {
        foreach (var recipientId in recipients)
        {
            if (_directory.TryGet(recipientId, out var recipient))
            {
                await recipient.CloseAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async ValueTask ForwardAsync(
        byte[] bytes,
        IReadOnlyList<string> recipients,
        CancellationToken cancellationToken)
    {
        foreach (var recipientId in recipients)
        {
            if (_directory.TryGet(recipientId, out var recipient))
            {
                await recipient.SendAsync(bytes, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
