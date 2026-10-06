using System;
using System.Linq;
using DungeonMasterXIV.Chat;
using DungeonMasterXIV.Rolls;

namespace DungeonMasterXIV.Net;

/// <summary>Builds each tick's inbound handlers: join requests, host and member content, and drop notices.</summary>
internal sealed class InboundWiring(
    AdmissionControl admissions,
    SessionResources resources,
    Func<string?, RelinkClaim> resolveRelink,
    RosterBroadcast roster,
    HostStream stream,
    ISessionTransportLog log,
    Action onReclaimed,
    Action onReclaimRefused,
    Action onHostAway,
    Action onHostBack)
{
    public InboundHandlers For(
        DateTimeOffset now,
        byte[]? sessionKey,
        Action<SessionContent> onHostContent) =>
        new(
            Admission: new JoinerAdmission(
                OnHello: admissions.OfferHostKey,
                OnJoinRequest: (key, envelope) =>
                {
                    if (admissions.OpenJoinRequest(key, envelope) is not { } details)
                    {
                        return;
                    }

                    var request = admissions.AdmitToTheQueue(
                        key,
                        now,
                        DisplayName.OrNone(details.DisplayName),
                        resolveRelink(details.ParticipantId),
                        details.Audiences ?? false);

                    if (request is not null && admissions.LetsInAutomatically(request))
                    {
                        admissions.Admit(request.PeerCode, asClaimed: true);
                        roster.Publish();
                        stream.Announce(StreamEventKind.Joined, request.PeerCode, string.Empty, now);
                    }
                },
                OnResume: (key, envelope) =>
                {
                    if (admissions.Resume(key, envelope) is not { } resumed)
                    {
                        return;
                    }

                    var missed = admissions.Audience.Find(resumed.Peer) is { } member
                        ? stream.MissedBy(member, resumed.LastSequence)
                        : Array.Empty<StreamLine>();

                    roster.Publish();
                    roster.PublishEntriesTo(resumed.Peer, missed);
                    if (admissions.DropLines.Returned(resumed.Peer))
                    {
                        stream.Announce(StreamEventKind.Reconnected, resumed.Peer, string.Empty, now);
                    }
                }),
            HostAuthored: new HostAuthoredContent(
                OpenWith: sessionKey,
                OnContent: onHostContent),
            MemberAuthored: new MemberAuthoredContent(
                OpenWith: resources.MemberKeys.Candidates,
                OnContent: (peer, content) =>
                {
                    resources.MemberContent.Record(peer, content);

                    Said(peer, content, now);
                    Rolled(peer, content, now);

                    if (content.Leaving is true)
                    {
                        stream.Announce(StreamEventKind.Left, peer, string.Empty, now);

                        admissions.Departed(peer);
                    }
                }),
            Transport: new TransportNotices(
                OnConnectionDropped: key => admissions.RecordDrop(key, now),
                OnReclaimed: onReclaimed,
                OnReclaimRefused: onReclaimRefused,
                OnHostAway: onHostAway,
                OnHostBack: onHostBack));

    private void Said(PeerCode peer, SessionContent content, DateTimeOffset now)
    {
        if (content.Saying is not { } text)
        {
            return;
        }

        var draft = MessageDraft.Compose(text, MessageLimits.Default);

        if (!draft.IsAccepted || draft.Text is not { } said || !Addressed(peer, content, isRoll: false, out var privacy))
        {
            return;
        }

        stream.Announce(StreamEventKind.Message, peer, said, now, privacy: privacy);
    }

    private void Rolled(PeerCode peer, SessionContent content, DateTimeOffset now)
    {
        if (content.Rolling is not { } roll
            || !roll.IsWithinBounds(RollLimits.Default)
            || !Addressed(peer, content, isRoll: true, out var privacy))
        {
            return;
        }

        stream.Announce(StreamEventKind.Roll, peer, roll.Summary(), now, roll, privacy);
    }

    private bool Addressed(PeerCode peer, SessionContent content, bool isRoll, out EntryPrivacy? privacy)
    {
        privacy = null;
        if (admissions.Audience.Find(peer) is not { } sender)
        {
            return false;
        }

        if (AudienceRules.TryResolve(content.Audience, isRoll, sender, admissions.Audience, out privacy))
        {
            return true;
        }

        log.Warning(
            $"Dropped a send from {peer.Value} addressed to an audience it may not use. Nothing was "
            + "relayed. The content is deliberately not recorded here.");
        return false;
    }
}
