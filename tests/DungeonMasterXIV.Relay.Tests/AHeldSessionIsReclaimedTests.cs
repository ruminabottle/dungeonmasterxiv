using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Relay.Sessions;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>A dropped host's session is held for its players and handed back only to the holder of the secret.</summary>
public sealed class AHeldSessionIsReclaimedTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");
    private static readonly byte[] JoinerKey = [1, 2, 3];

    [Fact]
    public void TheSessionWaitsForTheHostAndOnlyTheSecretReclaimsIt()
    {
        var secret = RandomNumberGenerator.GetBytes(32);
        var registry = new SessionRegistry();
        var router = new RelayRouter(registry);
        router.Route(WireEnvelope.ForCodeRequest(Code, SHA256.HashData(secret)), "host-1");
        router.Route(WireEnvelope.ForJoinHello(Code, JoinerKey), "joiner-1");
        registry.TryAdmit(Code.Value, JoinerKey, out _);

        var departure = Assert.Single(registry.Remove("host-1").Departures);
        Assert.False(departure.EndedSession);
        Assert.Equal(["joiner-1"], departure.HeldMembers);

        var payload = WireEnvelope.ForSessionPayload(Code, SealedPayload.FromWire(new byte[12], [9]));
        Assert.Equal(RelayOutcome.HostAway, router.Route(payload, "joiner-1").Outcome);
        Assert.Equal(
            RelayOutcome.ReclaimRefused,
            router.Route(WireEnvelope.ForReclaim(Code, RandomNumberGenerator.GetBytes(32)), "host-2").Outcome);

        var back = router.Route(WireEnvelope.ForReclaim(Code, secret), "host-2");
        Assert.Equal(RelayOutcome.Reclaimed, back.Outcome);
        Assert.Equal(["joiner-1"], back.NoticeRecipients);
        Assert.Equal(["host-2"], router.Route(payload, "joiner-1").Recipients);
    }
}
