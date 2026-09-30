using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Sessions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>The relay forwards a session payload to the other admitted members of its session.</summary>
public sealed class RelayRouterTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    [Fact]
    public void APayloadReachesTheOtherMembersOfItsSession()
    {
        var registry = new SessionRegistry();
        var router = new RelayRouter(registry);
        router.Route(WireEnvelope.ForCodeRequest(Code), "host-1");
        router.Route(WireEnvelope.ForJoinRequest(Code, [1, 2, 3]), "joiner-1");
        registry.TryAdmit(Code.Value, [1, 2, 3], out _);

        var decision = router.Route(
            WireEnvelope.ForSessionPayload(Code, SealedPayload.FromWire(new byte[12], [9, 9, 9])), "host-1");

        Assert.Equal(RelayAction.Forward, decision.Action);
        Assert.Equal(RelayOutcome.PayloadForwarded, decision.Outcome);
        Assert.Equal(["joiner-1"], decision.Recipients);
    }
}
