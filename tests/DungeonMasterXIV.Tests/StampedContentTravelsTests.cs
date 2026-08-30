using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.12 — stamped content crosses the wire, so a client can record what it RECEIVED (SQ-116).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE SECTION GUARD IS <see cref="AnEntrySectionSurvivesVettingWithNoRosterPresent"/> AND IT IS
/// REQUIRED RATHER THAN TIDY.</b> <c>Vetted</c> REBUILDS the document from an enumerated member
/// list, so a section added to <c>SessionContent</c> and forgotten there is silently dropped on
/// decode — sender sets it, wire carries it, receiver never sees it, nothing fails. Measured
/// (DMXENG-118): there is NO general guard, every section has its own, so this one is what stands
/// between a future edit and a silent loss.
/// </para>
/// <para>
/// <b>AND IT ASSERTS WITH NO ROSTER PRESENT ON PURPOSE.</b> The previous <c>Vetted</c> returned the
/// document untouched when <c>Roster</c> was null — which is why the departure guard is named
/// <i>WhenARosterIsPresent</i>. A stamped broadcast ordinarily carries no roster, so a guard that
/// only held when one was present would not hold on the common case.
/// </para>
/// </remarks>
public class StampedContentTravelsTests
{
    private static readonly string Code = "BCDFGH";

    private static StreamLine Line(long sequence = 1, string peer = "BCDFGH", string text = "rolls") =>
        new(sequence, 637_000_000_000_000_000, StreamEventKind.Message, peer, text);

    // THE SECTION GUARD. Delete Entries from Vetted's rebuild and this is what reddens.
    [Fact]
    public void AnEntrySectionSurvivesVettingWithNoRosterPresent()
    {
        var encoded = SessionContentCodec.Encode(new SessionContent { Entries = [Line()] });

        Assert.True(SessionContentCodec.TryDecode(encoded, out var decoded));
        Assert.NotNull(decoded!.Entries);
        Assert.Single(decoded.Entries!);
        Assert.Equal("rolls", decoded.Entries![0].Text);
        Assert.Null(decoded.Roster);
    }

    // R-2.4: the host is the sole minter, so an unminted sequence is refused AT THE DOOR. Sequence 0
    // is the specific hazard — it sorts to the FRONT of a populated log (BUG-161).
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void AnUnmintedSequenceIsDroppedAtDecode(long sequence)
    {
        var encoded = SessionContentCodec.Encode(
            new SessionContent { Entries = [Line(sequence: sequence)] });

        Assert.True(SessionContentCodec.TryDecode(encoded, out var decoded));
        Assert.Empty(decoded!.Entries!);
    }

    // A peer code the product could not have produced DROPS the line — the code is the identity, so
    // keeping it would attribute content to nobody. Same answer as a roster entry, same reason.
    [Fact]
    public void ALineWhosePeerCodeThisProductCannotProduceIsDropped()
    {
        var encoded = SessionContentCodec.Encode(
            new SessionContent { Entries = [Line(peer: "not-a-code")] });

        Assert.True(SessionContentCodec.TryDecode(encoded, out var decoded));
        Assert.Empty(decoded!.Entries!);
    }

    // THE CONTROL, or every row above passes against a decoder that drops everything.
    [Fact]
    public void AWellFormedLineIsKept()
    {
        Assert.True(PeerCode.TryParse(Code, out _), "fixture premise: the code parses");

        var encoded = SessionContentCodec.Encode(new SessionContent { Entries = [Line()] });

        Assert.True(SessionContentCodec.TryDecode(encoded, out var decoded));
        Assert.Single(decoded!.Entries!);
    }

    // THE POINT OF THE WHOLE SECTION: what arrives can become a domain entry that SessionStream
    // will actually accept. A wire member nothing can consume is the gap this ticket exists to close.
    [Fact]
    public void AReceivedLineBecomesAnEntryTheStreamAccepts()
    {
        var encoded = SessionContentCodec.Encode(new SessionContent { Entries = [Line(sequence: 7)] });
        Assert.True(SessionContentCodec.TryDecode(encoded, out var decoded));

        Assert.True(decoded!.Entries![0].TryToEntry(out var entry));
        Assert.Equal(7, entry.Stamp.Sequence);
        Assert.True(entry.Peer.IsPresent);

        Assert.True(new SessionStream().Record(entry), "SessionStream must accept a decoded entry");
    }

    // AND THE PEER CODE SURVIVES AS AN IDENTITY, NOT AS AN ABSENT ONE. This is the measured reason
    // StreamLine carries a string: a PeerCode placed on this wire serialises to
    // {"Value":"BCDFGH","IsPresent":true} and deserialises to default — absent, and equal to every
    // other absent code (DMXENG-105). This row fails if anyone "simplifies" the DTO to the struct.
    [Fact]
    public void ThePeerCodeArrivesPresentRatherThanAsTheAbsentDefault()
    {
        var encoded = SessionContentCodec.Encode(new SessionContent { Entries = [Line()] });
        Assert.True(SessionContentCodec.TryDecode(encoded, out var decoded));

        Assert.True(decoded!.Entries![0].TryToEntry(out var entry));
        Assert.True(entry.Peer.IsPresent, "the code must arrive PRESENT");
        Assert.False(entry.Peer.Equals(default(PeerCode)), "and must not be the colliding default");
        Assert.Equal(Code, entry.Peer.Value);
    }
}
