using System;
using DungeonMasterXIV.Net;
using Xunit;
using static DungeonMasterXIV.Tests.BaseChatFixture;

namespace DungeonMasterXIV.Tests;

/// <summary>A message from one member reaches a different member, opened with that member's own key.</summary>
public sealed class BaseChatReachesEveryMemberTests
{
    [Fact]
    public void AMessageFromOneMemberReachesADifferentMember()
    {
        var host = Hosting(out var transport);
        using var speaker = new SessionKeyExchange();
        using var listener = new SessionKeyExchange();
        var speakerCode = Admitted(host, Speaker, speaker);
        Admitted(host, Listener, listener);

        transport.Deliver(SealedBy(speaker, host, new SessionContent { Saying = "the door is trapped" }));
        host.Tick(TimeSpan.Zero, Now);

        var line = Assert.Single(
            StampedLinesFor(listener, host, transport), sent => sent.Kind == StreamEventKind.Message);

        Assert.Equal("the door is trapped", line.Text);
        Assert.Equal(speakerCode.Value, line.Peer);
        Assert.Equal(StreamEventKind.Message, line.Kind);
    }
}
