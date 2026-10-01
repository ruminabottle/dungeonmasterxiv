using System;
using System.Collections.Generic;

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

    public StreamEntry? StampAsHost(StreamEventKind kind, PeerCode peer, string text, DateTimeOffset at)
    {
        _at = at;
        var entry = new StreamEntry(_sequencer.Next(), kind, peer, text);
        return Record(entry) ? entry : null;
    }

    public bool Record(StreamEntry entry) => _stream.Record(entry);

    public void Release()
    {
        _stream = new SessionStream();
        _sequencer = NewSequencer();
    }

    private HostSequencer NewSequencer() => new(() => _at);
}
