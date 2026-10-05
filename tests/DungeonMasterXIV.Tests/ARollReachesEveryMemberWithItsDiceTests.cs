using System;
using System.Linq;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Rolls;
using Xunit;
using static DungeonMasterXIV.Tests.BaseChatFixture;

namespace DungeonMasterXIV.Tests;

/// <summary>A member's roll reaches a different member with every die the roller rolled.</summary>
public sealed class ARollReachesEveryMemberWithItsDiceTests
{
    [Fact]
    public void ARollFromOneMemberReachesADifferentMemberWithItsDice()
    {
        var host = Hosting(out var transport);
        using var roller = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        var rollerCode = Admitted(host, Speaker, roller);
        Admitted(host, Listener, listener);

        var outcome = new RollEvaluator(new ScriptedDieRoller(3, 6, 1, 4)).Evaluate("4d6+2");
        transport.Deliver(SealedBy(roller, host, new SessionContent { Rolling = SharedRoll.From("4d6+2", outcome) }));
        host.Tick(TimeSpan.Zero, Now);

        var line = Assert.Single(
            StampedLinesFor(listener, host, transport), sent => sent.Kind == StreamEventKind.Roll);

        Assert.Equal(rollerCode.Value, line.Peer);
        Assert.NotNull(line.Roll);
        Assert.Equal(new[] { 3, 6, 1, 4 }, line.Roll!.Dice.Select(die => die.Value));
        Assert.Equal(3 + 6 + 1 + 4 + 2, line.Roll.Total);
    }
}
