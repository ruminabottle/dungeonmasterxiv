using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>Stamps and records the host's session stream entries, starting afresh when released.</summary>
internal sealed class SessionRecording
{
    private SessionStream _stream = new();
    private HostSequencer _sequencer;
    private DateTimeOffset _at;

    public SessionRecording() => _sequencer = NewSequencer();

    public IReadOnlyList<StreamEntry> Entries => _stream.Entries;

    public bool RecordAsHost(StreamEventKind kind, PeerCode peer, string text, DateTimeOffset at) =>
        StampAsHost(kind, peer, text, at) is not null;

    public StreamEntry? StampAsHost(
        StreamEventKind kind,
        PeerCode peer,
        string text,
        DateTimeOffset at,
        SharedRoll? roll = null,
        EntryPrivacy? privacy = null)
    {
        _at = at;
        var entry = new StreamEntry(_sequencer.Next(), kind, peer, text, roll, privacy);
        return Record(entry) ? entry : null;
    }

    public bool Record(StreamEntry entry) => _stream.Record(entry);

    /// <summary>Marks a private roll revealed; returns the revealed entry, or null when there is nothing to reveal.</summary>
    public StreamEntry? Reveal(long sequence, string revealedBy)
    {
        var entry = _stream.Entries.FirstOrDefault(candidate => candidate.Stamp.Sequence == sequence);
        if (entry is not { Kind: StreamEventKind.Roll, Privacy: { IsRevealed: false } privacy })
        {
            return null;
        }

        var revealed = entry with { Privacy = privacy with { RevealedBy = revealedBy } };
        return _stream.Replace(revealed) ? revealed : null;
    }

    public void Release()
    {
        _stream = new SessionStream();
        _sequencer = NewSequencer();
    }

    private HostSequencer NewSequencer() => new(() => _at);
}
