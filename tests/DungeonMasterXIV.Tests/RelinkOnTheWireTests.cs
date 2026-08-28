using System;
using System.Linq;
using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The optional participant-ID field on a join request, and the D-14 promise that adding it breaks
/// nobody.
/// </summary>
public class RelinkOnTheWireTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    private static readonly byte[] AKey = { 4, 5, 6 };

    private static readonly byte[] HostKey = { 7, 8, 9 };

    // The raw frames below hardcode Type:4 because a compatibility fixture has to pin the number an
    // older peer actually sent -- deriving it from the enum would make the fixture follow a renumber
    // instead of catching one. This asserts the two still agree, so a renumber fails loudly here
    // rather than leaving those frames quietly testing some other message type. (They were written
    // as Type:3 first, and this is how that was found.)
    [Fact]
    public void TheWireNumberForAJoinRequestIsStillFour()
    {
        Assert.Equal(4, (int)WireMessageType.JoinRequest);
    }

    [Fact]
    public void AClaimSurvivesTheRoundTrip()
    {
        var participantId = Guid.NewGuid();

        var frame = EnvelopeCodec.Encode(WireEnvelope.ForRelinkRequest(Code, AKey, participantId));

        Assert.True(EnvelopeCodec.TryDecode(frame, out var decoded));
        Assert.Equal(participantId.ToString("D"), decoded!.ClaimedParticipantId);
        Assert.Equal(WireMessageType.JoinRequest, decoded.Type);
    }

    // An ordinary join makes no claim, and "no claim" must be null rather than an empty string a
    // resolver might mistake for input.
    [Fact]
    public void AnOrdinaryJoinRequestCarriesNoClaim()
    {
        var frame = EnvelopeCodec.Encode(WireEnvelope.ForJoinRequest(Code, AKey));

        Assert.True(EnvelopeCodec.TryDecode(frame, out var decoded));
        Assert.Null(decoded!.ClaimedParticipantId);
    }

    // D-14, additive-only: a frame written by a peer that has never heard of this field still
    // decodes, and every field it DID send survives. Built as raw JSON deliberately -- generating it
    // with today's encoder could not produce a frame missing a field today's encoder always writes.
    [Fact]
    public void AFrameFromAPeerThatPredatesTheFieldStillDecodes()
    {
        var fromAnOlderPeer = Encoding.UTF8.GetBytes(
            "{\"Type\":4,\"SessionCode\":\"BKD7RM\",\"PublicKey\":\"BAUG\"}");

        Assert.True(EnvelopeCodec.TryDecode(fromAnOlderPeer, out var decoded));
        Assert.Equal(WireMessageType.JoinRequest, decoded!.Type);
        Assert.Equal("BKD7RM", decoded.SessionCode);
        Assert.Equal(AKey, decoded.PublicKey);
        Assert.Null(decoded.ClaimedParticipantId);
    }

    // The reverse direction: a peer that predates the field receives one carrying it and must not
    // choke. Decoding here stands in for that peer, since it is the same codec.
    [Fact]
    public void AFrameCarryingTheFieldDecodesEvenWhenTheClaimIsMeaningless()
    {
        var withNonsense = Encoding.UTF8.GetBytes(
            "{\"Type\":4,\"SessionCode\":\"BKD7RM\",\"PublicKey\":\"BAUG\",\"ClaimedParticipantId\":\"not-a-uuid\"}");

        Assert.True(EnvelopeCodec.TryDecode(withNonsense, out var decoded));
        Assert.Equal("not-a-uuid", decoded!.ClaimedParticipantId);
    }

    // Nothing was renamed, removed or repurposed -- the other half of D-14-additive. Fails if the
    // new field displaced any field an existing peer relies on.
    //
    // SPLIT IN TWO BY DMXENG-41, and the split is the honest version rather than a mechanical
    // repair. This used to assert all three fields on ONE JoinRequest frame, stamped with a
    // deadline through a ForJoinRequest overload that had NO PRODUCTION CALLER -- so the deadline
    // half was asserting that a field survived on a message no peer has ever received. Each field
    // is now checked on the message that actually carries it, which is what "a field an existing
    // peer relies on" has to mean.
    [Fact]
    public void TheFieldsAnExistingPeerRelesOnAreUntouched()
    {
        var frame = EnvelopeCodec.Encode(WireEnvelope.ForJoinRequest(Code, AKey));

        Assert.True(EnvelopeCodec.TryDecode(frame, out var decoded));
        Assert.Equal(AKey, decoded!.PublicKey);
        Assert.Equal("BKD7RM", decoded.SessionCode);
    }

    // The deadline half, on the message that carries it. Keeping this is the point: deleting the
    // dead overload must not delete the coverage that the deadline field itself still survives a
    // round trip beside the newly added claim field.
    [Fact]
    public void TheDeadlineFieldIsUntouchedOnTheMessageThatCarriesIt()
    {
        var deadline = AdmissionDeadline.DecidedByHost(new DateTimeOffset(2026, 8, 27, 3, 0, 0, TimeSpan.Zero));
        var frame = EnvelopeCodec.Encode(WireEnvelope.ForJoinPending(Code, AKey, HostKey, deadline));

        Assert.True(EnvelopeCodec.TryDecode(frame, out var decoded));
        Assert.Equal(AKey, decoded!.PublicKey);
        Assert.Equal(HostKey, decoded.HostPublicKey);
        Assert.Equal(deadline.UtcTicks, decoded.DeadlineUtcTicks);
        Assert.Equal("BKD7RM", decoded.SessionCode);
    }

    // The claim is unauthenticated text and must never reach the associated-data binding: doing so
    // would let a claim change which payloads authenticate.
    [Fact]
    public void TheClaimIsNotPartOfTheAssociatedDataBinding()
    {
        var withClaim = WireEnvelope.ForRelinkRequest(Code, AKey, Guid.NewGuid());
        var without = WireEnvelope.ForJoinRequest(Code, AKey);

        Assert.Equal(without.AssociatedData(), withClaim.AssociatedData());
    }
}
