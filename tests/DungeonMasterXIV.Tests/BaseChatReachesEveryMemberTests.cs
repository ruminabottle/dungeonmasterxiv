using System;
using DungeonMasterXIV.Net;
using Xunit;
using static DungeonMasterXIV.Tests.BaseChatFixture;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.19 / A-2.34: a NON-HOST member says something and every OTHER admitted member receives it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE TWO LOAD-BEARING WORDS ARE NON-HOST AND DIFFERENT, and the criterion says why in its own
/// terms.</b> <i>"Local echo passes a build that never reached the wire"</i>, and <i>"a build where
/// only the host can originate satisfies every other message requirement in this document"</i> —
/// kinds, targets, speaker, ordering, retention. So no test here asserts on the sender's own log,
/// and the load-bearing ones decrypt the host's outbound envelope <b>with a THIRD party's key</b>.
/// </para>
/// <para>
/// <b>A-2.35 IS NOT HERE. It is <see cref="TheMessageBoundRefusesRatherThanTruncatesTests"/>.</b>
/// DMXENG-133: this class covered two criteria, which are two reasons to change, and was born over
/// both size flags — a delta gate compares a new file against nothing, so it crossed them silently.
/// </para>
/// <para>
/// <b>WHAT THIS FILE DOES NOT COVER, stated rather than left to be discovered.</b> The compose
/// surface is not drawn here — that is UI. These tests reach the host's inbound door directly, which
/// is honest for a Core test and is not a claim that a player can type into a box today.
/// </para>
/// </remarks>
public sealed class BaseChatReachesEveryMemberTests
{
    // THE BAR, AND IT IS A-2.34 ENTIRE. Fails if: a member's message reaches the host and stops
    // there -- which is every build before this one, since a member seals to the host and to nobody
    // else, so without a rebroadcast the message reaches exactly one machine.
    //
    // The assertion opens the host's OUTBOUND envelope with the LISTENER's key. A build that
    // recorded the message in the host's own log and sent nothing passes every other message
    // requirement and fails here.
    [Fact]
    public void AMessageFromOneMemberReachesADifferentMember()
    {
        var host = Hosting(out var transport);
        using var speaker = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        var speakerCode = Admitted(host, Speaker, speaker);
        Admitted(host, Listener, listener);

        transport.Deliver(SealedBy(speaker, host, new SessionContent { Saying = "the door is trapped" }));
        host.Tick(TimeSpan.Zero, Now);

        var line = Assert.Single(StampedLinesFor(listener, host, transport));

        Assert.Equal("the door is trapped", line.Text);
        Assert.Equal(speakerCode.Value, line.Peer);
        Assert.Equal(StreamEventKind.Message, line.Kind);
    }

    // NON-HOST ORIGINATION, PINNED AS ITS OWN ROW. Fails if: the speaker on the rebroadcast is the
    // host rather than the member who said it.
    //
    // A-2.34's whole point is that a host-only origination build looks correct everywhere else. The
    // peer code here comes from the KEY THE PAYLOAD OPENED UNDER, never from the payload -- there is
    // no speaker field in what a member sends -- so this also pins that a member cannot speak as
    // somebody else.
    [Fact]
    public void TheSpeakerIsTheMemberAndNotTheHost()
    {
        var host = Hosting(out var transport);
        using var speaker = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        var speakerCode = Admitted(host, Speaker, speaker);
        var listenerCode = Admitted(host, Listener, listener);

        transport.Deliver(SealedBy(speaker, host, new SessionContent { Saying = "I check for traps" }));
        host.Tick(TimeSpan.Zero, Now);

        var line = Assert.Single(StampedLinesFor(listener, host, transport));

        Assert.Equal(speakerCode.Value, line.Peer);
        Assert.NotEqual(listenerCode.Value, line.Peer);
        // NOT THE HOST: the host is not in its own audience -- RosterBroadcast inserts its
        // roster entry separately for exactly that reason -- so membership here IS the
        // non-host property A-2.34 is about.
        Assert.Contains(host.Audience.Recipients, peer => peer.PeerCode.Value == line.Peer);
    }

    // ORDER IS THE HOST'S (R-2.4). Fails if: a member can choose where its line lands, or the host
    // mints a sequence below 1 -- which the decode door refuses, so a zero would make the line
    // vanish at every receiver while looking sent.
    [Fact]
    public void TheHostMintsTheSequenceAndItIsUsable()
    {
        var host = Hosting(out var transport);
        using var speaker = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        Admitted(host, Speaker, speaker);
        Admitted(host, Listener, listener);

        transport.Deliver(SealedBy(speaker, host, new SessionContent { Saying = "first" }));
        host.Tick(TimeSpan.Zero, Now);

        var line = Assert.Single(StampedLinesFor(listener, host, transport));

        Assert.True(line.Sequence >= 1, "the host is the sole minter and issues from 1");
        Assert.True(line.TryToEntry(out _), "a line the decode door refuses reaches nobody");
    }

    // THE SECTION GUARD, REQUIRED BY SessionContentCodec's own comment. Delete `Saying` from
    // Vetted's rebuild and this is what reddens.
    //
    // Vetted REBUILDS the document from an enumerated member list, so a section added to
    // SessionContent and forgotten there is silently dropped on decode -- sender sets it, wire
    // carries it, receiver never sees it, and nothing fails. Measured on DMXENG-118: there is no
    // general guard for deletion, every section needs its own.
    //
    // ASSERTED WITH NO ROSTER PRESENT, for the reason the Entries guard is: a member's message
    // carries no roster, so a guard that only held when one was present would not hold on the case
    // that actually travels.
    [Fact]
    public void ASayingSectionSurvivesVettingWithNoRosterPresent()
    {
        var encoded = SessionContentCodec.Encode(new SessionContent { Saying = "still here" });

        Assert.True(SessionContentCodec.TryDecode(encoded, out var decoded));
        Assert.Equal("still here", decoded!.Saying);
        Assert.Null(decoded.Roster);
    }
}
