using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.3g: a participant sees that the session is closing, and how long remains.
/// </summary>
/// <remarks>
/// <para>
/// <b>This is the receiving half of a notice the product has sent since DMXENG-58 and nobody read.</b>
/// Measured before this file: <c>SessionClosing</c> had zero occurrences under <c>Windows/</c> or
/// <c>Plugin.cs</c>, so the host published a closing instant into silence. The countdown is a
/// REQUIREMENT and not a courtesy (PRD-1:698) — "the session is closing" without "how long remains"
/// is the indefinite wait R-1.3c and R-1.8 both forbid.
/// </para>
/// <para>
/// <b>The rules under test are the two ways a notice can be LOST after it arrives</b>, both of which
/// return a participant to that silence: an ordinary payload with no closing field clearing it, and
/// a malformed value being read as a retraction.
/// </para>
/// </remarks>
public class AParticipantHearsTheSessionClosingTests
{
    private static readonly DateTimeOffset Ended = new(2026, 8, 29, 14, 0, 0, TimeSpan.Zero);

    // The instant the host would put on the wire, derived here rather than read back from the type
    // under test -- asserting a value equals itself is the vacuity A-1.2u-oracle names.
    private static long WhatTheHostSends => Ended.Add(SessionClosing.Window).UtcTicks;

    [Fact]
    public void AClosingInstantThatArrivesIsHeard()
    {
        var received = new ReceivedClosing();

        received.Apply(WhatTheHostSends);

        Assert.NotNull(received.Notice);
        Assert.Equal(SessionClosing.Window, received.Notice!.Value.RemainingAt(Ended));
    }

    // >>> THE MUTATION THIS CLOSES: Apply(null) clearing the notice. <<<
    //
    // The closing instant is ONE optional field on SessionContent and most payloads carry none --
    // every ordinary roster push is a payload with no closing. A build that cleared on null would
    // forget the notice on the very next message, so the participant sees a countdown flicker once
    // and then nothing.
    [Fact]
    public void AnOrdinaryPayloadWithNoClosingDoesNotRetractOne()
    {
        var received = new ReceivedClosing();
        received.Apply(WhatTheHostSends);

        received.Apply(null);

        Assert.NotNull(received.Notice);
        Assert.Equal(WhatTheHostSends, received.Notice!.Value.UtcTicks);
    }

    // A malformed number is refused, and refusing it is NOT the same as being told the session is no
    // longer closing. Otherwise any client able to put a bad long on the wire could retract the
    // host's notice -- and under D-3 only the host decides.
    [Fact]
    public void AnOutOfRangeValueDoesNotRetractANoticeTheHostSent()
    {
        var received = new ReceivedClosing();
        received.Apply(WhatTheHostSends);

        received.Apply(-1);

        Assert.Equal(WhatTheHostSends, received.Notice!.Value.UtcTicks);
    }

    // And it is not recorded either, which is the half that would crash a draw path: RemainingAt
    // reads the instant in front of a participant watching a countdown.
    [Fact]
    public void AnOutOfRangeValueIsNeverRecorded()
    {
        var received = new ReceivedClosing();

        received.Apply(long.MinValue);

        Assert.Null(received.Notice);
    }

    // A later notice from the host wins. D-3 makes the closing deadline the host's to decide, so a
    // build that kept the first and ignored a correction would show a countdown the DM had changed.
    [Fact]
    public void AHostThatSaysADifferentInstantIsBelieved()
    {
        var received = new ReceivedClosing();
        received.Apply(WhatTheHostSends);

        var later = Ended.AddMinutes(5).Add(SessionClosing.Window).UtcTicks;
        received.Apply(later);

        Assert.Equal(later, received.Notice!.Value.UtcTicks);
    }

    // Leaving forgets it. A notice outliving its session shows a countdown for a session the user is
    // no longer in, or closes the next one they join.
    [Fact]
    public void LeavingForgetsTheNotice()
    {
        var received = new ReceivedClosing();
        received.Apply(WhatTheHostSends);

        received.Clear();

        Assert.Null(received.Notice);
    }

    // Nothing has arrived, so there is nothing to show -- and the absence is expressible rather than
    // being some sentinel instant a draw path would render as a countdown.
    [Fact]
    public void BeforeAnyNoticeThereIsNone()
    {
        Assert.Null(new ReceivedClosing().Notice);
    }
}
