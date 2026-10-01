using System;
using DungeonMasterXIV.Chat;

namespace DungeonMasterXIV.Net;

/// <summary>Builds the host's inbound handlers, connecting join requests, member content and drops to admission.</summary>
internal sealed class InboundWiring(
    AdmissionControl admissions,
    SessionResources resources,
    Func<string?, RelinkClaim> resolveRelink,
    RosterBroadcast roster)
{
    public InboundHandlers For(
        DateTimeOffset now,
        byte[]? sessionKey,
        Action<SessionContent> onHostContent) =>
        new(
            Admission: new JoinerAdmission(
                OnJoinRequest: (key, name, claimed) =>
                    admissions.AdmitToTheQueue(key, now, name, resolveRelink(claimed)),
                OnComparabilityReceipt: admissions.RecordComparabilityReceipt),
            HostAuthored: new HostAuthoredContent(
                OpenWith: sessionKey,
                OnContent: onHostContent),
            MemberAuthored: new MemberAuthoredContent(
                OpenWith: resources.MemberKeys.Candidates,
                OnContent: (peer, content) =>
                {
                    resources.MemberContent.Record(peer, content);

                    Said(peer, content, now);

                    if (content.Leaving is true)
                    {
                        resources.Recording.RecordAsHost(StreamEventKind.Left, peer, string.Empty, now);

                        admissions.Departed(peer);
                    }
                }),
            Transport: new TransportNotices(
                OnConnectionDropped: key => admissions.RecordDrop(key, now)));

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

        if (resources.Recording.StampAsHost(StreamEventKind.Message, peer, said, now) is not { } entry)
        {
            return;
        }

        roster.PublishEntry(new StreamLine(
            entry.Stamp.Sequence, entry.Stamp.AtUtcTicks, entry.Kind, entry.Peer.Value, entry.Text));
    }
}
