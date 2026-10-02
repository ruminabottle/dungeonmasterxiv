using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>A host whose connection drops redials, reclaims its session, and hears its players again with nobody rejoining.</summary>
public sealed class AHostBlipIsRecoveredTests
{
    [Fact]
    public void TheHostComesBackAndThePlayerStaysIn()
    {
        var relay = new LoopbackRelay();
        var host = relay.Connect("host");
        var player = relay.Connect("player");

        host.StartHosting();
        relay.RunUntil(() => host.Host.Phase == HostingPhase.Hosting);
        player.RequestJoin(host.Host.Code!.Value, DisplayName.OrNone("Tuka"), null);
        relay.RunUntil(() => host.Admissions.Pending.Count == 1);
        host.Admit(host.Admissions.Pending[0].PeerCode);
        relay.RunUntil(() => player.Join.Phase == JoinPhase.Admitted);

        relay.Drop("host");
        relay.RunUntil(() => host.Grace.IsRunning);
        relay.RunUntil(() => !host.Grace.IsRunning, TimeSpan.FromSeconds(1));

        Assert.Equal(HostingPhase.Hosting, host.Host.Phase);
        Assert.Equal(JoinPhase.Admitted, player.Join.Phase);

        player.Membership.Say("still here");
        relay.RunUntil(() => host.Recorded.Any(entry => entry.Text == "still here"));
    }
}
