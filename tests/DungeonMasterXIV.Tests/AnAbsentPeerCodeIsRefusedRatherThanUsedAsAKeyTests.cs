using System;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// <c>MissedMessages</c> refuses an absent <see cref="PeerCode"/> at both writers (DMXENG-105).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DISPOSITION IS RULED BY THE TYPE, NOT CHOSEN HERE.</b> <c>PeerCode</c>'s own remarks say a
/// caller that defaults one <i>"has an absent code, not a valid one, and must treat it as a
/// refusal"</i>. <c>MissedMessages</c> keyed two collections on it and honoured that in neither, so
/// this is a precondition being kept rather than a new policy being invented.
/// </para>
/// <para>
/// <b>WHY AN ABSENT KEY IS WORSE THAN A MISSING ONE.</b> <c>default(PeerCode)</c> equals every other
/// default and hashes to 0, so every member whose code failed to parse is THE SAME KEY. Holding under
/// it does not lose traffic — it MERGES it, and hands one member what was kept for another. A gap
/// marker minted for one member arriving in another member's replay is the same fault: the assertion
/// passing IS the defect.
/// </para>
/// <para>
/// <b>TWO WRITERS, AND ONE TEST EACH ON PURPOSE.</b> <c>_held</c> and <c>_gapped</c> are separate
/// collections with separate writers, and a single test over <c>Hold</c> leaves <c>NoteGap</c>
/// unguarded and green — which is exactly how the first report of this bug would have been closed
/// with half of it live.
/// </para>
/// <para>
/// <b>Guarded at the WRITERS, so the readers are safe by construction.</b> <c>Replay</c> and
/// <c>IsHoldingFor</c> are deliberately NOT guarded: doing so would imply the bad state is still
/// representable when it is not. If nothing can be stored under an absent key, nothing can be read
/// back from one.
/// </para>
/// <para>
/// <b>The axis matters.</b> The existing suite covers this type well along "who is asking" — two
/// distinct PRESENT codes keep their traffic apart. The defect is not in who asks; it is in what the
/// key IS, and a test using present codes cannot see it. These use absent ones.
/// </para>
/// </remarks>
public class AnAbsentPeerCodeIsRefusedRatherThanUsedAsAKeyTests
{
    private static readonly PeerCode Present = PeerCodes.Of("PRBCD2");

    private static HostSequencer Host() =>
        new(() => new DateTimeOffset(2026, 8, 29, 21, 0, 0, TimeSpan.Zero));

    private static StreamEntry Said(HostSequencer host, string text) =>
        new(host.Next(), StreamEventKind.Message, Present, text);

    // WRITER 1 of 2. Fails if Hold accepts an absent code: two members whose codes did not parse
    // would then share one bucket, and the first to reconnect would receive both their entries.
    [Fact]
    public void HoldingForAMemberWhoseCodeIsAbsentIsRefused()
    {
        var host = Host();
        var missed = new MissedMessages();

        Assert.Throws<ArgumentException>(() => missed.Hold(default, Said(host, "not deliverable to anyone")));
    }

    // WRITER 2 of 2, AND THE ONE A FIX TO Hold ALONE LEAVES LIVE. _gapped is a separate collection
    // with its own writer; guarding only Hold leaves a gap marker minted for one member arriving in
    // another member's replay, with every existing test still green.
    [Fact]
    public void NotingAGapForAMemberWhoseCodeIsAbsentIsRefused()
    {
        var missed = new MissedMessages();

        Assert.Throws<ArgumentException>(() => missed.NoteGap(default));
    }

    // THE PROPERTY THE TWO REFUSALS BUY, stated as a fact about the collection rather than about the
    // calls. Two members with absent codes are indistinguishable as keys, so this is what "their
    // traffic cannot merge" has to mean: nothing is under that key at all.
    [Fact]
    public void NothingIsEverHeldOrMarkedUnderAnAbsentCode()
    {
        var host = Host();
        var missed = new MissedMessages();

        Assert.Throws<ArgumentException>(() => missed.Hold(default, Said(host, "first member")));
        Assert.Throws<ArgumentException>(() => missed.Hold(default, Said(host, "second member")));
        Assert.Throws<ArgumentException>(() => missed.NoteGap(default));

        Assert.False(missed.IsHoldingFor(default));
        Assert.Empty(missed.Replay(default, host.Next));
    }

    // THE CONTROL, AND WITHOUT IT THE THREE ABOVE ARE SATISFIED BY A Hold THAT REFUSES EVERYTHING.
    // A guard that rejects every code passes every refusal test written and breaks the feature.
    [Fact]
    public void APresentCodeIsStillHeldAndStillMarked()
    {
        var host = Host();
        var missed = new MissedMessages();

        missed.Hold(Present, Said(host, "the door opens"));
        missed.NoteGap(Present);

        Assert.True(missed.IsHoldingFor(Present));

        var replay = missed.Replay(Present, host.Next);

        Assert.Contains(replay, e => e.Kind == StreamEventKind.Gap);
        Assert.Contains(replay, e => e.Kind == StreamEventKind.Message && e.Text == "the door opens");
    }
}
