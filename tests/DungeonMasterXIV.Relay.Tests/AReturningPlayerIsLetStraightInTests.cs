using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Relay.Tests;

/// <summary>With the DM's say-so, a returning player is admitted as their entry while a newcomer is still prompted.</summary>
public sealed class AReturningPlayerIsLetStraightInTests
{
    [Fact]
    public void AReturningPlayerSkipsThePromptAndANewcomerDoesNot()
    {
        var known = Guid.NewGuid();
        var relay = new LoopbackRelay();
        var host = relay.Connect("host", new SessionCapabilities(
            ResolveRelink: claimed => claimed == known.ToString("D")
                ? new RelinkClaim(true, "Ysera", known)
                : RelinkClaim.None,
            LetReturningPlayersIn: () => true));
        var returning = relay.Connect("returning");
        var newcomer = relay.Connect("newcomer");

        host.StartHosting();
        relay.RunUntil(() => host.Host.Phase == HostingPhase.Hosting);

        var code = host.Host.Code!.Value;
        returning.RequestJoin(code, DisplayName.OrNone("Ysera"), known);
        newcomer.RequestJoin(code, DisplayName.OrNone("Tuka"), null);
        relay.RunUntil(() => returning.Join.Phase == JoinPhase.Admitted && host.Admissions.Pending.Count == 1);

        Assert.Equal(known, returning.Join.ParticipantId);
        Assert.Equal("Tuka", host.Admissions.Pending[0].DisplayName.Value);
    }
}
