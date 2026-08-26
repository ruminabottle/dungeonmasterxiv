using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// D-13's None level, tested as a structural property rather than as a filtering step. The
/// distinction is the directive's enforcement line: filtering client-side passes every UI
/// inspection and still puts the data on the wire.
/// </summary>
public class SessionAudienceTests
{
    // Fails if: Recipients ever contains someone who was not admitted — which is the only way a
    // payload built for this audience could reach a client at None.
    [Fact]
    public void AClientThatWasNeverAdmittedIsNotAddressable()
    {
        var audience = new SessionAudience();
        audience.Admit("PEER-1");

        Assert.False(audience.IsAdmitted("PEER-2"));
        Assert.DoesNotContain(audience.Recipients, peer => peer.PeerCode == "PEER-2");
    }

    // Fails if: removal only hides a peer rather than dropping them. R-1.3 requires a removed
    // client to stop receiving state immediately, which here means ceasing to be addressable.
    [Fact]
    public void ARemovedClientStopsBeingAddressableImmediately()
    {
        var audience = new SessionAudience();
        audience.Admit("PEER-1");

        Assert.True(audience.Remove("PEER-1"));

        Assert.False(audience.IsAdmitted("PEER-1"));
        Assert.Empty(audience.Recipients);
    }

    // The inference half of D-13. Fails if: the host's count is derived from anything other than
    // the admitted set — a count that included pending or removed clients is precisely the number
    // D-13 says must not exist, because it would tell a recipient that someone else is there.
    [Fact]
    public void TheCountIsExactlyTheAddressableSetAndNothingElse()
    {
        var audience = new SessionAudience();
        audience.Admit("PEER-1");
        audience.Admit("PEER-2");
        audience.Remove("PEER-1");

        Assert.Equal(1, audience.Count);
        Assert.Equal(audience.Recipients.Count, audience.Count);
    }

    // Fails if: a retried admission adds a second entry. That would inflate the host's count and
    // duplicate a recipient, and the DM would see a player who is not there.
    [Fact]
    public void AdmittingTheSameParticipantTwiceDoesNotDuplicateThem()
    {
        var audience = new SessionAudience();

        var first = audience.Admit("PEER-1");
        var second = audience.Admit("PEER-1");

        Assert.Same(first, second);
        Assert.Equal(1, audience.Count);
    }

    // Fails if: ending a session leaves participants addressable.
    [Fact]
    public void ClearingLeavesNobodyAddressable()
    {
        var audience = new SessionAudience();
        audience.Admit("PEER-1");
        audience.Admit("PEER-2");

        audience.Clear();

        Assert.Empty(audience.Recipients);
        Assert.Equal(0, audience.Count);
    }

    // The structural claim, asserted rather than argued: every recipient came from Admit. There is
    // no public constructor for AdmittedPeer, so a payload cannot be addressed to a client at None
    // even by a caller trying to. Fails if: AdmittedPeer gains a public constructor.
    [Fact]
    public void AdmittedPeerCannotBeConstructedOutsideTheAudience()
    {
        var constructors = typeof(AdmittedPeer).GetConstructors();

        Assert.Empty(constructors);
    }
}
