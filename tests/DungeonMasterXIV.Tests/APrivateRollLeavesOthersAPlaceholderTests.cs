using System;
using System.Linq;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Rolls;
using Xunit;
using static DungeonMasterXIV.Tests.BaseChatFixture;

namespace DungeonMasterXIV.Tests;

/// <summary>A private roll reaches others only as a placeholder, a whisper not at all, and a reveal fills the placeholder in.</summary>
public sealed class APrivateRollLeavesOthersAPlaceholderTests
{
    [Fact]
    public void OthersGetAPlaceholderAndNoWhisperUntilTheDmReveals()
    {
        var host = Hosting(out var transport);
        using var eli = new SessionKeyExchange();
        using var mara = new SessionKeyExchange();
        using var old = new SessionKeyExchange();
        AdmittedWithAudiences(host, Speaker, eli);
        AdmittedWithAudiences(host, Listener, mara);
        var oldCode = Admitted(host, "MNPRTV", old);

        host.Say("before", Now);
        var outcome = new RollEvaluator(new ScriptedDieRoller(17)).Evaluate("1d20");
        transport.Deliver(SealedBy(eli, host, new SessionContent
        {
            Rolling = SharedRoll.From("1d20", outcome),
            Audience = MessageAudience.DmSide,
        }));
        transport.Deliver(SealedBy(eli, host, new SessionContent
        {
            Saying = "the key is under the mat",
            Audience = MessageAudience.DmSide,
        }));
        host.Tick(TimeSpan.Zero, Now);
        var dmRoll = new RollEvaluator(new ScriptedDieRoller(4)).Evaluate("1d6");
        Assert.Null(host.ShareRoll(SharedRoll.From("1d6", dmRoll), Now, MessageAudience.DmSide));
        host.Say("after", Now);

        Assert.False(host.Say("psst", Now, MessageAudience.ToPlayer(oldCode.Value)).IsAccepted);

        var seen = StampedLinesFor(mara, host, transport);
        var numbers = seen.Select(line => line.Sequence).ToList();
        Assert.Equal(Enumerable.Range((int)numbers[0], numbers.Count).Select(n => (long)n), numbers);
        Assert.DoesNotContain(seen, line => line.Text.Contains("mat", StringComparison.Ordinal));
        Assert.All(seen, line => Assert.Null(line.Roll));

        var placeholders = seen.Where(line => line.Withheld == true).ToList();
        Assert.Equal(2, placeholders.Count);
        Assert.Equal(AudienceRules.ForTheDm, placeholders[0].Text);
        Assert.Equal(AudienceRules.ByTheDm, placeholders[1].Text);

        var eliRoll = Assert.Single(StampedLinesFor(eli, host, transport), line => line.Roll is not null);
        Assert.Equal(AudienceKind.DmSide, eliRoll.Audience);

        var recorded = host.Recorded.First(entry => entry.Kind == StreamEventKind.Roll);
        Assert.True(host.Reveal(recorded.Stamp.Sequence, Now));
        Assert.False(host.Reveal(recorded.Stamp.Sequence, Now));

        var revealed = StampedLinesFor(mara, host, transport).Last(line => line.Sequence == placeholders[0].Sequence);
        Assert.Equal(17, revealed.Roll!.Total);
        Assert.NotNull(revealed.RevealedBy);
    }

    private static PeerCode AdmittedWithAudiences(SessionCoordinator host, string code, SessionKeyExchange keys)
    {
        var peerCode = PeerCodes.Of(code);
        host.ReceiveJoinRequest(peerCode, keys.PublicKey, Now, supportsAudiences: true);
        host.Admit(peerCode);
        return peerCode;
    }
}
