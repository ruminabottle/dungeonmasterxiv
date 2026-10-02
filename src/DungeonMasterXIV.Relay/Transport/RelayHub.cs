using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Diagnostics;
using DungeonMasterXIV.Relay.Sessions;

namespace DungeonMasterXIV.Relay.Transport;

/// <summary>Decodes and routes incoming messages, holds sessions whose host dropped, and clears up on disconnect and expiry.</summary>
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

                if (decision.FollowUps is { } followUps)
                {
                    foreach (var followUp in followUps)
                    {
                        await sender.SendAsync(EnvelopeCodec.Encode(followUp), cancellationToken).ConfigureAwait(false);
                    }
                }

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

        if (decision.Notice is { } notice && decision.NoticeRecipients is { } noticeRecipients)
        {
            await ForwardAsync(EnvelopeCodec.Encode(notice), noticeRecipients, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask ExpireHoldsAsync(TimeSpan hold, CancellationToken cancellationToken)
    {
        foreach (var departure in _registry.ExpireHolds(hold))
        {
            await CloseAsync(departure.OrphanedConnections, cancellationToken).ConfigureAwait(false);
        }
    }

    public async ValueTask DisconnectAsync(
        IRelayConnection connection,
        string reason,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);

        var removal = _registry.Remove(connection.Id);
        _directory.Remove(connection.Id);
        _log.ConnectionClosed(connection.Id, removal, reason);

        await TellHostsTheirMemberDroppedAsync(removal, cancellationToken).ConfigureAwait(false);
        await TellMembersTheHostIsAwayAsync(removal, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask TellMembersTheHostIsAwayAsync(ConnectionRemoval removal, CancellationToken cancellationToken)
    {
        foreach (var departure in removal.Departures)
        {
            if (departure.HeldMembers is not { } members || !SessionCode.TryParse(departure.Code, out var code))
            {
                continue;
            }

            await ForwardAsync(EnvelopeCodec.Encode(WireEnvelope.ForHostAway(code)), members, cancellationToken)
                .ConfigureAwait(false);
            await CloseAsync(departure.OrphanedConnections, cancellationToken).ConfigureAwait(false);
        }
    }

    private async ValueTask TellHostsTheirMemberDroppedAsync(
        ConnectionRemoval removal,
        CancellationToken cancellationToken)
    {
        foreach (var departure in removal.Departures)
        {
            if (departure.DepartedMemberKey is not { } memberKey
                || !SessionCode.TryParse(departure.Code, out var code)
                || !_directory.TryGet(departure.HostConnectionId, out var host))
            {
                continue;
            }

            var notice = WireEnvelope.ForConnectionDropped(code, Convert.FromBase64String(memberKey));
            await host.SendAsync(EnvelopeCodec.Encode(notice), cancellationToken).ConfigureAwait(false);
        }
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
