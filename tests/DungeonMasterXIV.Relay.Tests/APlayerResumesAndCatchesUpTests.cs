using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>A player who drops resumes without a prompt and receives the lines said while they were away.</summary>
public sealed class APlayerResumesAndCatchesUpTests
{
    [Fact]
    public void TheReturningPlayerGetsWhatTheyMissed()
    {
        var relay = new LoopbackRelay();
        var host = relay.Connect("host");
        var alice = relay.Connect("alice");
        var bob = relay.Connect("bob");

        host.StartHosting();
        relay.RunUntil(() => host.Host.Phase == HostingPhase.Hosting);
        var code = host.Host.Code!.Value;
        alice.RequestJoin(code, DisplayName.OrNone("Alice"), null);
        bob.RequestJoin(code, DisplayName.OrNone("Bob"), null);
        relay.RunUntil(() => host.Admissions.Pending.Count == 2);
        foreach (var request in host.Admissions.Pending.ToArray())
        {
            host.Admit(request.PeerCode);
        }

        relay.RunUntil(() => alice.Join.Phase == JoinPhase.Admitted && bob.Join.Phase == JoinPhase.Admitted);

        relay.Drop("bob");
        relay.RunUntil(() => host.Drops.Count == 1);
        alice.Membership.Say("one");
        alice.Membership.Say("two");
        relay.RunUntil(() => host.Recorded.Count(entry => entry.Kind == StreamEventKind.Message) == 2);

        relay.RunUntil(
            () => !bob.Join.Resuming && bob.Received.Any(line => line.Text == "two"),
            TimeSpan.FromSeconds(1));

        Assert.Equal(JoinPhase.Admitted, bob.Join.Phase);
        Assert.Empty(host.Admissions.Pending);
        Assert.Contains(bob.Received, line => line.Text == "one");
    }
}
