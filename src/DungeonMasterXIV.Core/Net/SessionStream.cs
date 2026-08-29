using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The one time-ordered log every client shows: the host's order, the host's clock, the same on every
/// screen (R-2.3, R-2.4, A-2.5).
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS TYPE HAS NO CLOCK AND THAT ABSENCE IS THE MECHANISM, NOT AN OVERSIGHT.</b> A-2.5 fails a
/// build in which <i>any client's local clock reaches the log</i>. The usual way to satisfy that is a
/// rule everyone remembers; the way taken here is that <b>there is no clock in the receiving path to
/// forget about</b>. <see cref="Record"/> accepts a <see cref="StreamStamp"/> and cannot make one,
/// <see cref="HostSequencer"/> is the only minter, and a member's build constructs no sequencer. A
/// reviewer can check that by reading the constructor rather than by trusting the author.
/// </para>
/// <para>
/// <b>ORDER COMES FROM THE STAMP, NEVER FROM ARRIVAL.</b> Entries are inserted at their sequence
/// position, so two clients whose transports deliver in different orders still read the same log. A
/// stream that appended in arrival order would pass any test that fed both clients identically —
/// which is why A-2.5 says <i>fed the same events in different local timing</i>, and why the test
/// feeds them in different orders.
/// </para>
/// <para>
/// <b>A REPEATED SEQUENCE IS IGNORED RATHER THAN APPENDED.</b> A reconnecting client can be sent an
/// entry it already holds, and a log that showed it twice would disagree with a client that never
/// dropped. This is not replay — R-2.10 is a separate ticket — it is only the guarantee that
/// receiving something twice is indistinguishable from receiving it once.
/// </para>
/// <para>
/// <b>What this deliberately does NOT do:</b> it does not decide what may enter the stream, does not
/// render, and does not know what a roll means. It is the ordering and nothing else — a stream, an
/// ordering rule and a clock source are three concerns, and this type holds one of them.
/// </para>
/// </remarks>
public sealed class SessionStream
{
    private readonly List<StreamEntry> _entries = new();

    /// <summary>The log, in the host's order. The same list on every client that saw the same events.</summary>
    public IReadOnlyList<StreamEntry> Entries => _entries;

    /// <summary>
    /// Places <paramref name="entry"/> at its sequence position, reporting whether the log changed.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Reports false for a duplicate so a caller does not redraw on a no-op</b>, the same reason
    /// <c>RecordDisplayNameAlias</c> reports whether it changed anything.
    /// </para>
    /// <para>
    /// <b>Insertion is by sequence and the search is linear from the end</b>, because entries arrive
    /// in order almost always and out of order rarely. The rare case is correct rather than fast; the
    /// common case is both.
    /// </para>
    /// </remarks>
    /// <param name="entry">A stamped entry, from the host or decoded from the wire.</param>
    public bool Record(StreamEntry entry)
    {
        var at = _entries.Count;
        while (at > 0 && _entries[at - 1].Stamp.Sequence > entry.Stamp.Sequence)
        {
            at--;
        }

        if (at > 0 && _entries[at - 1].Stamp.Sequence == entry.Stamp.Sequence)
        {
            return false;
        }

        _entries.Insert(at, entry);
        return true;
    }
}
