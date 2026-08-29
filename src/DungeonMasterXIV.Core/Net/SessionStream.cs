using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Net;

/// <summary>
/// The one time-ordered log every client shows: the host's order, the host's clock, the same on every
/// screen (R-2.3, R-2.4, A-2.5).
/// </summary>
/// <remarks>
/// <para>
/// <b>THIS TYPE READS NO CLOCK, AND THE GUARANTEE IS EXACTLY THAT — NO MORE.</b> A-2.5 fails a build
/// in which <i>any client's local clock reaches the log</i>. <see cref="Record"/> accepts a
/// <see cref="StreamStamp"/> and cannot make one; <see cref="HostSequencer"/> is the only minter.
/// <para>
/// <b>WHAT IS NOT GUARANTEED, STATED BECAUSE THE FIRST DRAFT CLAIMED IT WAS.</b> A reviewer planted a
/// clock FACTORY into this type and both test arms stayed green, so <i>"there is no clock in the
/// receiving path to forget about"</i> was stronger than the code supports. <b>Nothing prevents a
/// future edit injecting one</b> — what the tests now do is refuse a direct clock read AND a clock
/// factory in this file, which is a checked property rather than an architectural impossibility.</para>
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
        // BUG-161. AN UNMINTED STAMP IS REFUSED HERE, BECAUSE THE TYPE SYSTEM DOES NOT REFUSE IT.
        // StreamStamp is a readonly record struct, so new StreamEntry(default, ...) compiles and
        // carries Sequence 0 -- and 0 sorts to the FRONT of a populated log, which is the original
        // hazard. HostSequencer issues from 1, so Sequence < 1 is definitionally not host-issued.
        //
        // A refusal rather than a throw: Record already reports whether the log changed, and a
        // caller that cannot distinguish "duplicate" from "never stamped" is not made safer by an
        // exception it will not catch on a draw path.
        if (entry.Stamp.Sequence < 1)
        {
            return false;
        }

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
