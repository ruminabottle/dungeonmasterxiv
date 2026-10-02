using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>A message a player sends while the DM is briefly away waits and arrives once the DM is back.</summary>
public sealed class AMessageSentDuringABlipArrivesTests
{
    [Fact]
    public void TheHeldMessageIsDeliveredAfterTheBlip()
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
        relay.RunUntil(() => player.Grace.IsRunning);
        Assert.Null(player.ReconnectingLine);

        player.Membership.Say("held for you");
        relay.RunUntil(() => host.Recorded.Any(entry => entry.Text == "held for you"), TimeSpan.FromSeconds(1));
    }
}
