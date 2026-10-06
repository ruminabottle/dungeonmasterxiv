using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>Stamps a host stream entry and sends each admitted member its line in their own view, in the same step.</summary>
internal sealed class HostStream(SessionRecording recording, RosterBroadcast roster, SessionAudience audience)
{
    public StreamEntry? Announce(
        StreamEventKind kind,
        PeerCode peer,
        string text,
        DateTimeOffset at,
        SharedRoll? roll = null,
        EntryPrivacy? privacy = null)
    {
        if (recording.StampAsHost(kind, peer, text, at, roll, privacy) is not { } entry)
        {
            return null;
        }

        SendToEachMember(entry.Stamp.Sequence);
        return entry;
    }

    public bool Reveal(long sequence, PeerCode revealedBy, DateTimeOffset at)
    {
        if (recording.Reveal(sequence, revealedBy.Value, at) is null)
        {
            return false;
        }

        SendToEachMember(sequence);
        return true;
    }

    /// <summary>The lines a returning member missed, plus every revealed roll, so a reveal made while away fills in.</summary>
    public IReadOnlyList<StreamLine> MissedBy(AdmittedPeer peer, long lastSequence) =>
        ViewFor(peer)
            .Where(viewed => viewed.Line.Sequence > lastSequence || viewed.Line.RevealedBy is not null)
            .Select(viewed => viewed.Line)
            .ToList();

    private void SendToEachMember(long recordSequence)
    {
        foreach (var peer in audience.Recipients)
        {
            var viewed = ViewFor(peer).Find(candidate => candidate.RecordSequence == recordSequence);
            if (viewed.RecordSequence == recordSequence)
            {
                roster.PublishEntriesTo(peer.PeerCode, new[] { viewed.Line });
            }
        }
    }

    private List<ViewedLine> ViewFor(AdmittedPeer peer) =>
        MemberView.Of(recording.Entries, SeatKey.For(peer), audience.SupportsAudiences(peer.PeerCode));
}
