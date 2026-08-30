using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Data;

/// <summary>
/// Turns the live stream's entries into the form a log is written in. <b>The single point of
/// contact between retention and <c>Core/Net</c>.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>ONE FILE TOUCHES THE STREAM TYPES ON PURPOSE.</b> The ticket's boundary is a WRITE separation
/// — I may not edit <c>Net/</c> — and <b>a write separation does not make a read safe.</b> Two live
/// branches are editing <c>StreamEntry</c> and <c>StreamEvent</c> right now. Confining the
/// dependency here means that when their shape moves, exactly one file fails to compile, loudly,
/// instead of an exporter failing at export time on a real log.
/// </para>
/// <para>
/// <b>AND THE ENUM HAS ALREADY MOVED.</b> Measured rather than assumed: <c>StreamEventKind</c> is
/// <b>six members on <c>origin/main</c> and seven on the open PR #210</b>, which adds <c>Gap</c> —
/// the marker for a stretch the host could not hold. <b>A switch over a closed set is exhaustive
/// only against the set it was written against</b>, and this one would otherwise break at EXPORT
/// time, on a stream that recorded a real gap, which is the worst moment to discover it.
/// </para>
/// <para>
/// <b>So the default arm THROWS rather than guessing.</b> An unmapped kind is a new stream event
/// this code has never seen; writing it as "Unknown" would put a silent lie in an archival record,
/// and dropping it would lose a line the log is supposed to hold — and a dropped <c>Gap</c> is the
/// worst case of all, because a stream with a hole in it would then look identical to a stream with
/// nothing in the hole, which is the exact failure that marker exists to prevent.
/// </para>
/// <para>
/// <b>AND ADDING A KIND WILL NOT BREAK THE BUILD, SO A TEST IS THE TRIPWIRE.</b> A new enum member
/// compiles perfectly against this switch and falls to the default <i>at run time</i> — which for an
/// exporter means at export time, on a DM's real log. <c>EveryKindTheStreamHasTodayIsMapped</c>
/// iterates <c>Enum.GetValues</c> and fails the moment a kind exists that this file has not been
/// taught, so the surprise lands in CI rather than on a user.
/// </para>
/// <para>
/// <b>AND THE REMEDY IS A MERGE ORDER, NOT A CROSS-BOUNDARY EDIT.</b> This file and its tripwire
/// exist only here; neither is on <c>main</c>. So if the PR adding a kind lands <i>first</i>, the
/// kind is simply present when this switch is written and nothing ever goes red. If <i>this</i>
/// lands first, the tripwire is green on the kinds of the day, the next PR turns CI red, and fixing
/// it would require <b>that</b> PR to edit <b>this</b> file — a cross-boundary edit that both
/// boundaries were drawn to prevent, manufactured by the ordering rather than by anyone's mistake.
/// <b>The ordering is the Deployment Manager's to hold; two engineers agreeing it between their own
/// PRs is a decision neither of them owns.</b>
/// </para>
/// </remarks>
public static class StreamLogProjection
{
    /// <summary>Projects one live entry into its written form.</summary>
    /// <exception cref="NotSupportedException">The kind is one this projection has not been taught.</exception>
    public static LoggedEntry From(StreamEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return new LoggedEntry(
            new LoggedStamp(entry.Stamp.Sequence, entry.Stamp.AtUtcTicks),
            NameOf(entry.Kind),
            entry.Peer.Value,
            entry.Text);
    }

    /// <summary>Projects a whole stream, in order.</summary>
    public static IReadOnlyList<LoggedEntry> From(IEnumerable<StreamEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        return entries.Select(From).ToList();
    }

    private static string NameOf(StreamEventKind kind) => kind switch
    {
        StreamEventKind.Message => "message",
        StreamEventKind.Roll => "roll",
        StreamEventKind.Joined => "joined",
        StreamEventKind.Left => "left",
        StreamEventKind.Dropped => "dropped",
        StreamEventKind.Reconnected => "reconnected",

        // ADDED WHEN #210 MERGED, WHICH IS THE TRIPWIRE ABOVE DOING ITS JOB: the moment Gap reached
        // main, EveryKindTheStreamHasTodayIsMapped went red on this branch. In CI, at merge time,
        // on a developer -- not at export time on a DM's real log.
        //
        // R-2.12 rulings, mine: retention COUNTS a gap, because a retained log that omitted it would
        // assert a continuity the session did not have; and an export RENDERS it, never drops it,
        // because a stream with a hole must not look identical to a stream with nothing in the hole.
        // That is the same trade #210 makes one layer in.
        StreamEventKind.Gap => "gap",

        // NOT a catch-all for convenience. A kind that reaches here is one the stream gained after
        // this file was written -- Gap is already queued on PR #210 -- and both silent options are
        // wrong: naming it "unknown" writes a falsehood into an archive, dropping it loses a line
        // the log exists to hold. This throws where a developer sees it, not where a DM does.
        _ => throw new NotSupportedException(
            $"The log projection has not been taught the stream event kind '{kind}'. "
            + "The stream gained a kind after this file was written; teach it here rather than "
            + "letting an export invent or discard the line."),
    };
}
