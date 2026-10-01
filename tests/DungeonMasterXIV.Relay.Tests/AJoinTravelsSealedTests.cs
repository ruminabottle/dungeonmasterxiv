using System.Text;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>A join crosses the relay with the name and participant ids sealed, and the DM admits by name.</summary>
public sealed class AJoinTravelsSealedTests
{
    [Fact]
    public void TheRelayNeverSeesTheNameOrTheParticipantIds()
    {
        var minted = Guid.NewGuid();
        var claimed = Guid.NewGuid();
        var relay = new LoopbackRelay();
        var host = relay.Connect("host", new SessionCapabilities(MintParticipant: _ => minted));
        var joiner = relay.Connect("joiner");

        host.StartHosting();
        relay.RunUntil(() => host.Host.Phase == HostingPhase.Hosting);

        joiner.RequestJoin(host.Host.Code!.Value, DisplayName.OrNone("Ysera Moonfall"), claimed);
        relay.RunUntil(() => host.Admissions.Pending.Count == 1);

        var request = host.Admissions.Pending[0];
        Assert.Equal("Ysera Moonfall", request.DisplayName.Value);

        host.Admit(request.PeerCode);
        relay.RunUntil(() => joiner.Join.Phase == JoinPhase.Admitted);

        Assert.Equal(minted, joiner.Join.ParticipantId);

        var seen = string.Join("\n", relay.Seen.Select(Encoding.UTF8.GetString));
        Assert.DoesNotContain("Ysera", seen);
        Assert.DoesNotContain(claimed.ToString("D"), seen);
        Assert.DoesNotContain(minted.ToString("D"), seen);
    }
}
