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
                        key, now, DisplayName.OrNone(details.DisplayName), resolveRelink(details.ParticipantId));

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

                    var missed = resources.Recording.Entries
                        .Where(entry => entry.Stamp.Sequence > resumed.LastSequence)
                        .Select(StreamLine.From)
                        .ToList();

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

        if (!draft.IsAccepted || draft.Text is not { } said)
        {
            return;
        }

        stream.Announce(StreamEventKind.Message, peer, said, now);
    }

    private void Rolled(PeerCode peer, SessionContent content, DateTimeOffset now)
    {
        if (content.Rolling is not { } roll || !roll.IsWithinBounds(RollLimits.Default))
        {
            return;
        }

        stream.Announce(StreamEventKind.Roll, peer, roll.Summary(), now, roll);
    }
}
