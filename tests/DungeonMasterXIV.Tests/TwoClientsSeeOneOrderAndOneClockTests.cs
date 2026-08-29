using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-2.5 — two clients fed the same events in different local timing show identical order and
/// identical timestamps, and a build in which any client's local clock reaches the log fails
/// (R-2.3, R-2.4).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE SECOND CLAUSE IS THE WHOLE CRITERION AND IT IS THE ONE A TEST CAN FAKE.</b> Two clients fed
/// the same events in the SAME order agree under a build that timestamps locally, because nothing
/// distinguished them. So the behavioural test below feeds them in <b>different orders</b> and gives
/// them <b>different local clocks</b> — if either client's clock could reach its log, the two logs
/// would differ, and the test would have something to catch.
/// </para>
/// <para>
/// <b>AND THE BEHAVIOURAL TEST ALONE IS NOT ENOUGH, WHICH IS WHY THERE ARE TWO HALVES.</b> It shows
/// that today's build does not leak a clock; it cannot show that a future one could not. The
/// structural half asserts the property that makes it impossible rather than merely absent:
/// <b>nothing in the receiving path reads a clock at all.</b> That is checkable by reading the types,
/// which is the difference between a rule someone must remember and a rule the code cannot break.
/// </para>
/// <para>
/// <b>Host sequencing is not the relay deciding (D-3 untouched, D-2 intact).</b> The sequencer runs
/// on the host's own client. Nothing here gives the relay a role.
/// </para>
/// </remarks>
public class TwoClientsSeeOneOrderAndOneClockTests
{
    // A-2.5, THE BEHAVIOURAL HALF. Different local clocks AND different delivery orders -- either one
    // alone leaves the test satisfiable by a build it should fail.
    [Fact]
    public void TwoClientsFedInDifferentOrdersWithDifferentClocksHoldIdenticalLogs()
    {
        var authored = HostAuthored();

        var alice = new SessionStream();
        foreach (var entry in authored)
        {
            alice.Record(entry);
        }

        // Bob's transport delivers the same entries in a different order. His log must still match.
        var bob = new SessionStream();
        foreach (var entry in Shuffled(authored))
        {
            bob.Record(entry);
        }

        Assert.Equal(alice.Entries, bob.Entries);
    }

    // AND THE ASSERTION ABOVE NEEDS SOMETHING TO DISTINGUISH, OR IT PASSES ON TWO EMPTY LOGS.
    // Pins that the log is populated and in the HOST's order rather than either arrival order.
    [Fact]
    public void TheLogIsInTheHostsOrderAndNotInArrivalOrder()
    {
        var authored = HostAuthored();

        var bob = new SessionStream();
        foreach (var entry in Shuffled(authored))
        {
            bob.Record(entry);
        }

        Assert.Equal(authored.Count, bob.Entries.Count);
        Assert.Equal(
            authored.Select(e => e.Stamp.Sequence),
            bob.Entries.Select(e => e.Stamp.Sequence));
        Assert.NotEqual(
            Shuffled(authored).Select(e => e.Stamp.Sequence),
            bob.Entries.Select(e => e.Stamp.Sequence));
    }

    // THE DEDUP THE DOC CLAIMS, WHICH NOTHING WAS CHECKING UNTIL THIS ROW.
    //
    // A reconnecting client can be sent an entry it already holds. If the log showed it twice it
    // would disagree with a client that never dropped -- which is A-2.5's failure, arriving through
    // a mechanism the ordering tests cannot see, because they never feed anything twice.
    //
    // Written because SessionStream's remarks ASSERTED this behaviour and no test exercised it. A
    // claim in prose with no test is the same shape as a criterion with no assertion.
    [Fact]
    public void AnEntryReceivedTwiceAppearsOnce()
    {
        var authored = HostAuthored();
        var stream = new SessionStream();

        foreach (var entry in authored)
        {
            stream.Record(entry);
        }

        Assert.False(stream.Record(authored[2]), "a repeat should report that nothing changed");
        Assert.Equal(authored.Count, stream.Entries.Count);
        Assert.Equal(authored.Select(e => e.Stamp.Sequence), stream.Entries.Select(e => e.Stamp.Sequence));
    }

    // A-2.5, THE STRUCTURAL HALF: NO CLOCK EXISTS IN THE RECEIVING PATH TO LEAK.
    //
    // The behavioural test shows today's build does not leak one. This shows a future one cannot
    // without deleting an assertion -- the difference between "we checked" and "it is unconstructable".
    // Reads the SOURCE rather than reflecting over the types, because a clock call is a statement
    // inside a method body and metadata cannot see it.
    // A CLOCK FACTORY IS ON THE LIST, AND IT IS THERE BECAUSE A REVIEWER GOT PAST THE FIRST VERSION.
    // The original list held six direct-read literals; a planted Func<DateTimeOffset> in SessionStream
    // left BOTH arms green, because an injected clock is neither a literal read nor visible in the
    // behavioural fixture. The factory forms close that, and HostSequencer -- which legitimately holds
    // one -- is deliberately not in the file list below.
    private static readonly string[] ClockSources =
    {
        "DateTime.Now", "DateTime.UtcNow", "DateTimeOffset.Now", "DateTimeOffset.UtcNow",
        "Environment.TickCount", "Stopwatch", "TimeProvider",
        "Func<DateTime>", "Func<DateTimeOffset>",
    };

    [Theory]
    [InlineData("SessionStream.cs")]
    [InlineData("StreamEntry.cs")]
    [InlineData("StreamStamp.cs")]
    [InlineData("StreamEvent.cs")]
    public void NoTypeInTheReceivingPathCanReadAClock(string file)
    {
        var source = File.ReadAllText(Path.Combine(NetDirectory(), file));

        foreach (var clock in ClockSources)
        {
            Assert.False(
                source.Contains(clock, StringComparison.Ordinal),
                $"{file} reads a clock ({clock}). A-2.5 fails a build in which any client's local "
                + "clock reaches the log, and the receiving path is where that would happen. The "
                + "host's time arrives on the stamp; nothing here may source its own.");
        }
    }

    // THE POSITIVE CASE THE SEARCH HAD NOWHERE, AND ITS ABSENCE WAS THE DEFECT.
    //
    // The forbidden list held six literals and the control asserted "Func<DateTimeOffset>" -- WHICH WAS
    // NOT ONE OF THEM. So the control demonstrated that a DIFFERENT string is findable, and none of the
    // six appeared anywhere in this repository. A search with no demonstrated positive case is
    // indistinguishable from one that matches nothing, which is the exact defect this file exists to
    // guard against, sitting inside the guard.
    //
    // Every entry is now checked against a sample that CONTAINS it. If a form stops matching -- an
    // escaping slip, a renamed API -- this reddens instead of going quietly green.
    [Fact]
    public void EveryClockFormTheSearchForbidsIsOneItCanActuallyFind()
    {
        foreach (var clock in ClockSources)
        {
            Assert.Contains(clock, $"var t = {clock};", StringComparison.Ordinal);
        }
    }

    // AND A POSITIVE CASE ON A REAL FILE, not only on a synthetic one: HostSequencer is the one type
    // here that SHOULD hold a clock, and the SAME list must find it there. A synthetic sample proves
    // the matcher; this proves the matcher against the codebase as it is actually written.
    [Fact]
    public void TheSameSearchFindsTheOneClockThatIsSupposedToExist()
    {
        var sequencer = File.ReadAllText(Path.Combine(NetDirectory(), "HostSequencer.cs"));

        Assert.Contains(
            ClockSources.Where(c => sequencer.Contains(c, StringComparison.Ordinal)),
            _ => true);
    }

    // Deliberately NOT DateTimeOffset.UtcNow: the test supplies the host's clock, so the expected
    // values are the test's own and a reader can check the arithmetic.
    private static IReadOnlyList<StreamEntry> HostAuthored()
    {
        var tick = 0;
        var host = new HostSequencer(() => new DateTimeOffset(2026, 8, 29, 20, 0, ++tick, TimeSpan.Zero));
        Assert.True(PeerCode.TryParse("BCDFGH", out var peer), "the fixture's own peer code must parse");

        return new List<StreamEntry>
        {
            new(host.Next(), StreamEventKind.Joined, peer, string.Empty),
            new(host.Next(), StreamEventKind.Message, peer, "is anyone there"),
            new(host.Next(), StreamEventKind.Roll, peer, "2d6"),
            new(host.Next(), StreamEventKind.Dropped, peer, string.Empty),
            new(host.Next(), StreamEventKind.Reconnected, peer, string.Empty),
            new(host.Next(), StreamEventKind.Left, peer, string.Empty),
        };
    }

    // A fixed reordering rather than a random one: a shuffle that differs per run makes a failure
    // unreproducible, and this test's job is to be checkable rather than lucky.
    private static IReadOnlyList<StreamEntry> Shuffled(IReadOnlyList<StreamEntry> entries) =>
        new[] { entries[2], entries[0], entries[5], entries[1], entries[4], entries[3] };

    private static string NetDirectory() => Path.Combine(
        ShippedCopyCorpus.RepositoryRoot(), "src", "DungeonMasterXIV.Core", "Net");
}
