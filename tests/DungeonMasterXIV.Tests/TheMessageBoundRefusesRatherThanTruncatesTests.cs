using System;
using DungeonMasterXIV.Chat;
using DungeonMasterXIV.Net;
using Xunit;
using static DungeonMasterXIV.Tests.BaseChatFixture;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.19 / A-2.35: an over-long message is REFUSED WITH THE FAULT NAMED — not truncated, not
/// dropped.
/// </summary>
/// <remarks>
/// <para>
/// <b>THREE OUTCOMES AND ONLY ONE PASSES.</b> Silent truncation fails, because the sender believes
/// they said something they did not. A silent drop fails for the same reason one layer along.
/// <b>And a freeze or an out-of-memory is a failure of THIS requirement rather than a performance
/// issue</b> — which is why the bound is not only on characters: see
/// <see cref="AMessageInsideTheCharacterBoundCanStillBeTooLarge"/>, the test that discharges that
/// clause, because a character bound alone leaves what the wire carries unbounded and an unbounded
/// arrival is the thing that exhausts memory.
/// </para>
/// <para>
/// <b>THE CRITERION ASSERTS NO NUMBER AND NEITHER DOES ANY TEST HERE.</b> The bound value is
/// engineering's (R-2.19), so every fixture builds its over-long input RELATIVE to the configured
/// bound. A test hard-coding one would fail the day somebody tunes it, for no reason the criterion
/// recognises.
/// </para>
/// <para>
/// <b>BOTH ENDS, AND THEY ARE NOT REDUNDANT.</b> The sender refuses so the person who typed it is
/// TOLD; the host refuses because <b>a peer is not obliged to run our sending code</b>. A build with
/// only the first is bounded against its friends.
/// </para>
/// <para>
/// <b>A-2.34 IS NOT HERE. It is <see cref="BaseChatReachesEveryMemberTests"/>.</b> Split under
/// DMXENG-133.
/// </para>
/// </remarks>
public sealed class TheMessageBoundRefusesRatherThanTruncatesTests
{
    // A-2.35, THE SENDER'S HALF. Fails if: an over-long message is truncated, or dropped in silence.
    //
    // THREE OUTCOMES AND ONLY ONE PASSES. This asserts the refusal NAMES ITS FAULT and that the
    // text was not quietly shortened -- the failure mode where the sender believes they said
    // something they did not.
    //
    // NO NUMBER IS PINNED. A-2.35 asserts none and says the value is engineering's, so the fixture
    // builds its over-long input RELATIVE to the configured bound. A test hard-coding 2000 would
    // fail the day somebody tunes it, for no reason the criterion recognises.
    [Fact]
    public void AnOverLongMessageIsRefusedWithTheFaultNamed()
    {
        var limits = MessageLimits.Default;
        var tooLong = new string('a', limits.MaxLength + 1);

        var draft = MessageDraft.Compose(tooLong, limits);

        Assert.False(draft.IsAccepted);
        Assert.Equal(MessageFault.TooLong, draft.Fault);
        Assert.NotNull(draft.Reason);
        Assert.Null(draft.Text);
    }

    // THE TRUNCATION ARM, SEPARATELY. Fails if: a refusal hands back a shortened body that a caller
    // would then send. Asserting only the fault above would pass a build that refused AND supplied
    // text, which is the silent-truncation outcome wearing a receipt.
    [Fact]
    public void ARefusedMessageCarriesNoTextToSendInstead()
    {
        var limits = MessageLimits.Default;

        var draft = MessageDraft.Compose(new string('a', limits.MaxLength + 1), limits);

        Assert.Null(draft.Text);
        Assert.NotEqual(limits.MaxLength, draft.Text?.Length ?? -1);
    }

    // THE BOUND THE CHARACTER COUNT CANNOT EXPRESS. Fails if: a message inside the character bound
    // but far outside the byte bound is accepted.
    //
    // A grapheme cluster carries arbitrarily many combining marks, so N characters has no finite
    // byte ceiling -- DisplayName records the same measurement for names. A build bounded only on
    // characters is bounded against what a person perceives and not against what the wire carries,
    // which is the hostile case R-2.19 names.
    [Fact]
    public void AMessageInsideTheCharacterBoundCanStillBeTooLarge()
    {
        var limits = new MessageLimits { MaxLength = 8, BytesPerCharacter = 2 };
        var heavy = new string('e', 4) + new string('́', 40);

        var draft = MessageDraft.Compose(heavy, limits);

        Assert.False(draft.IsAccepted);
        Assert.Equal(MessageFault.TooLarge, draft.Fault);
    }

    // A-2.35 AT THE HOST, WHICH THE SENDER'S CHECK CANNOT STAND IN FOR. Fails if: an over-long
    // message that arrives from a peer running its own client is stamped and rebroadcast.
    //
    // A PEER IS NOT OBLIGED TO RUN OUR SENDING CODE. This fixture builds the payload directly,
    // exactly as a hostile client would, and asserts nothing goes out.
    [Fact]
    public void AnOverLongArrivalIsNotStampedOrRebroadcast()
    {
        var host = Hosting(out var transport);
        using var speaker = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        Admitted(host, Speaker, speaker);
        Admitted(host, Listener, listener);

        var tooLong = new string('a', MessageLimits.Default.MaxLength + 1);
        transport.Deliver(SealedBy(speaker, host, new SessionContent { Saying = tooLong }));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Empty(StampedLinesFor(listener, host, transport));
    }

    // THE ANTI-VACUITY CONTROL FOR THE TEST ABOVE, AND WITHOUT IT THAT ONE PROVES LESS THAN IT
    // LOOKS. Fails if: nothing is ever rebroadcast, which would make the empty assertion above pass
    // for a reason unrelated to the bound.
    [Fact]
    public void AMessageInsideTheBoundIsRebroadcast()
    {
        var host = Hosting(out var transport);
        using var speaker = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        Admitted(host, Speaker, speaker);
        Admitted(host, Listener, listener);

        transport.Deliver(SealedBy(speaker, host, new SessionContent { Saying = "short enough" }));
        host.Tick(TimeSpan.Zero, Now);

        Assert.NotEmpty(StampedLinesFor(listener, host, transport));
    }
}
