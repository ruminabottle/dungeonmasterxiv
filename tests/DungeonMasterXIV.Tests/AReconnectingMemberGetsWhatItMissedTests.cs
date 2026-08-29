using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.10: what a dropped member missed is delivered on reconnect, and a gap is MARKED (A-2.6, A-2.6a).
/// </summary>
/// <remarks>
/// <para>
/// <b>A-2.6a HAS TWO HALVES AND THEY FAIL SEPARATELY.</b> Delivering nothing fails; delivering
/// silently past a gap fails. <b>A suite that only proves delivery passes a build that swallows
/// gaps</b> — and that build looks correct, because a stream with a hole in it renders exactly like
/// a stream with nothing in the hole. Each half has its own test here, and the marking half has a
/// control beside it.
/// </para>
/// <para>
/// <b>A-2.6 IS A DIFFERENT CASE and is not the reconnect one.</b> A client never admitted receives
/// nothing from before its admission. It is asserted here because the two are one sentence apart in
/// the PRD and the mechanism that satisfies one could plausibly leak the other.
/// </para>
/// <para>
/// <b>RE-SENDING IS REQUIRED.</b> A-2.6a's clause <i>"a build that restores the log by re-sending
/// fails"</i> was STRUCK on 2026-08-29 — decision 7 requires exactly that re-send. Nothing here
/// treats re-sending as a defect, and a reading that does is a reading of the struck version.
/// </para>
/// </remarks>
public class AReconnectingMemberGetsWhatItMissedTests
{
    private static readonly PeerCode Member = PeerCodes.Of("PRBCD2");
    private static readonly PeerCode Bystander = PeerCodes.Of("BKD7RM");

    private static HostSequencer Host() =>
        new(() => new DateTimeOffset(2026, 8, 29, 21, 0, 0, TimeSpan.Zero));

    // A-2.6a, FIRST HALF: delivering nothing fails.
    [Fact]
    public void AMemberThatReconnectsReceivesWhatItMissed()
    {
        var host = Host();
        var missed = new MissedMessages();
        missed.Hold(Member, Said(host, "the door opens"));
        missed.Hold(Member, Said(host, "something moves"));

        var replay = missed.Replay(Member, host.Next);

        Assert.Equal(
            ["the door opens", "something moves"],
            replay.Where(e => e.Kind == StreamEventKind.Message).Select(e => e.Text));
    }

    // >>> A-2.6a, SECOND HALF: delivering SILENTLY PAST A GAP fails. <<<
    //
    // This is the half a build fails invisibly. The entries that survived are all present and in
    // order, so the stream looks whole; the only thing distinguishing it from a complete one is the
    // marker that is not there.
    [Fact]
    public void AGapThatCouldNotBeHeldIsMarked()
    {
        var host = Host();
        var missed = new MissedMessages();
        missed.Hold(Member, Said(host, "something moves"));
        missed.NoteGap(Member);

        var replay = missed.Replay(Member, host.Next);

        Assert.Contains(replay, entry => entry.Kind == StreamEventKind.Gap);
    }

    // THE CONTROL THAT MAKES THE TEST ABOVE MEAN SOMETHING. Same shape, same held entry, no loss
    // reported -- and no marker. Without this, a build that marked EVERY replay would pass the gap
    // test while telling every returning member that something was missing.
    [Fact]
    public void AReplayThatLostNothingCarriesNoMarker()
    {
        var host = Host();
        var missed = new MissedMessages();
        missed.Hold(Member, Said(host, "something moves"));

        var replay = missed.Replay(Member, host.Next);

        Assert.DoesNotContain(replay, entry => entry.Kind == StreamEventKind.Gap);
    }

    // The marker describes what is missing, so it precedes what survived -- the order that makes the
    // hole legible to someone reading their stream top to bottom.
    [Fact]
    public void TheMarkerComesBeforeWhatSurvived()
    {
        var host = Host();
        var missed = new MissedMessages();
        missed.NoteGap(Member);
        missed.Hold(Member, Said(host, "something moves"));

        var replay = missed.Replay(Member, host.Next);

        Assert.Equal(StreamEventKind.Gap, replay[0].Kind);
    }

    // R-2.3/R-2.4: the host sequences and timestamps. A marker minting its own stamp would be a
    // second sequencer, which is the drift those requirements exist to prevent.
    [Fact]
    public void TheMarkerCarriesAStampFromTheHostRatherThanOneItInvented()
    {
        var host = Host();
        var missed = new MissedMessages();
        missed.Hold(Member, Said(host, "something moves"));   // sequence 1
        missed.NoteGap(Member);

        var marker = missed.Replay(Member, host.Next).Single(e => e.Kind == StreamEventKind.Gap);

        Assert.Equal(2, marker.Stamp.Sequence);
    }

    // >>> A-2.6, WHICH IS THE OTHER CASE <<<
    //
    // A client never admitted receives NOTHING from before its admission. Nothing was ever held for
    // it, so there is nothing to give -- the separation is structural rather than a check that could
    // be forgotten. The bystander is here so the assertion has something to distinguish: a build
    // replaying the whole log to anyone would hand this peer somebody else's traffic.
    [Fact]
    public void AClientNeverAdmittedReceivesNothing()
    {
        var host = Host();
        var missed = new MissedMessages();
        missed.Hold(Member, Said(host, "something moves"));

        Assert.Empty(missed.Replay(Bystander, host.Next));
        Assert.False(missed.IsHoldingFor(Bystander));
    }

    // What has been given back is no longer missed. A member that drops again starts a new hold
    // rather than receiving the old one a second time.
    [Fact]
    public void ReplayingForgetsWhatItGaveBack()
    {
        var host = Host();
        var missed = new MissedMessages();
        missed.Hold(Member, Said(host, "something moves"));
        missed.NoteGap(Member);

        missed.Replay(Member, host.Next);

        Assert.Empty(missed.Replay(Member, host.Next));
    }

    // The seat ended, so there is nobody to replay to -- and the hold must not survive to be handed
    // to whoever next presents that peer code.
    [Fact]
    public void ForgettingASeatDropsTheHoldWithoutReplayingIt()
    {
        var host = Host();
        var missed = new MissedMessages();
        missed.Hold(Member, Said(host, "something moves"));
        missed.NoteGap(Member);

        missed.Forget(Member);

        Assert.False(missed.IsHoldingFor(Member));
        Assert.Empty(missed.Replay(Member, host.Next));
    }

    private static StreamEntry Said(HostSequencer host, string text) =>
        new(host.Next(), StreamEventKind.Message, Member, text);
}
