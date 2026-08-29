using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.5 (kind), R-2.6 (target) and R-2.7 (speaker) — the three attributes a message carries.
/// </summary>
/// <remarks>
/// <b>EACH ROW HERE HAS A NEGATIVE, BECAUSE EACH REQUIREMENT IS FAKEABLE WITHOUT ONE.</b> A build
/// that always appends the parenthetical passes any test that only checks it is present; a build with
/// no targeting at all passes any test that only checks a message is delivered; a build encoding kind
/// as a colour passes any test that only checks the kind is set. The negatives are the requirements.
/// </remarks>
public class WhatAMessageCarriesTests
{
    private static DisplayName Person(string name)
    {
        Assert.True(DisplayName.TryParse(name, out var person), $"fixture name '{name}' must parse");
        return person;
    }

    // R-2.7: Character (Player).
    [Fact]
    public void ASpeakerDifferentFromThePersonRendersBoth()
    {
        Assert.Equal("Renn (Tuka)", MessageLine.Attribution("Renn", Person("Tuka")));
    }

    // R-2.7's NEGATIVE, AND IT IS THE ROW THAT MATTERS: speaking as yourself renders the person
    // ALONE, never Tuka (Tuka). A build that always appends the parenthetical passes the row above
    // and fails this one -- which is why the ticket says a test covering only the first is not a test.
    [Fact]
    public void SpeakingAsYourselfRendersThePersonAlone()
    {
        Assert.Equal("Tuka", MessageLine.Attribution("Tuka", Person("Tuka")));
    }

    // R-2.5: the three kinds are distinguishable WITHOUT COLOUR. Asserted on the rendered text with
    // no styling anywhere in the assertion -- if the kind were encoded as a colour, all three of
    // these strings would be identical and this reddens.
    [Fact]
    public void TheThreeKindsAreDistinguishableInPlainTextAlone()
    {
        var person = Person("Tuka");

        var rendered = new[] { MessageKind.InCharacter, MessageKind.OutOfCharacter, MessageKind.Emote }
            .Select(kind => MessageLine.Render(kind, MessageTarget.Everyone, "Renn", person, SessionRole.Player, "waits"))
            .ToList();

        Assert.Equal(rendered.Count, rendered.Distinct(StringComparer.Ordinal).Count());
    }

    // AND THE CENSUS HALF: every kind the enum declares must render. A kind added later without a
    // rendering reaches the default arm and throws, which is loud -- but only if something drives it.
    // This is what drives it.
    [Fact]
    public void EveryDeclaredKindRenders()
    {
        var person = Person("Tuka");

        foreach (var kind in Enum.GetValues<MessageKind>())
        {
            var line = MessageLine.Render(kind, MessageTarget.Everyone, "Renn", person, SessionRole.Player, "waits");
            Assert.False(string.IsNullOrWhiteSpace(line), $"{kind} rendered nothing");
        }
    }

    // R-2.6: a private message is marked as private in the text, so a reader can see the audience.
    [Fact]
    public void APrivateMessageSaysSoInPlainText()
    {
        var line = MessageLine.Render(
            MessageKind.InCharacter, MessageTarget.DungeonMasterOnly, "Renn", Person("Tuka"), SessionRole.Player, "quietly");

        Assert.Contains("(private)", line, StringComparison.Ordinal);
    }

    // R-2.6's NEGATIVE: an ordinary message must NOT carry the marker. Without this, a build that
    // marks everything private passes the row above -- and a reader who sees "private" on every line
    // learns nothing from it, which is the same failure as never marking it.
    [Fact]
    public void AnOrdinaryMessageDoesNotClaimToBePrivate()
    {
        var line = MessageLine.Render(
            MessageKind.InCharacter, MessageTarget.Everyone, "Renn", Person("Tuka"), SessionRole.Player, "aloud");

        Assert.DoesNotContain("(private)", line, StringComparison.Ordinal);
    }

    // R-2.6, THE PROOF OBLIGATION IN FULL: a DM-private message reaches the DM AND the sender, and
    // NOT a third participant. The ticket is explicit that THE NEGATIVE IS THE REQUIREMENT --
    // asserting only that it reaches the DM passes a build with no targeting at all, because a build
    // that shows everything to everyone also shows it to the DM.
    [Fact]
    public void APrivateMessageReachesTheDungeonMasterAndTheSenderAndNobodyElse()
    {
        Assert.True(PeerCode.TryParse("BCDFGH", out var sender));
        Assert.True(PeerCode.TryParse("JKMNPR", out var dm));
        Assert.True(PeerCode.TryParse("TVWXY2", out var bystander));

        Assert.True(
            MessageAudience.Includes(MessageTarget.DungeonMasterOnly, sender, dm, SessionRole.DungeonMaster),
            "the DM must receive a message addressed to them");
        Assert.True(
            MessageAudience.Includes(MessageTarget.DungeonMasterOnly, sender, sender, SessionRole.Player),
            "the sender must see their own message, or it looks like it failed to send");

        Assert.False(
            MessageAudience.Includes(MessageTarget.DungeonMasterOnly, sender, bystander, SessionRole.Player),
            "A THIRD PARTICIPANT MUST NOT RECEIVE DM-PRIVATE TRAFFIC. This is the requirement; the "
            + "two rows above pass a build with no targeting whatsoever.");
    }

    // AND AN ASSISTANT IS NOT THE HOST. R-2.7a is explicit that the privileged role is the host
    // specifically, not "anyone DM-ish" -- widening this hands private traffic to somebody the
    // requirement never named, and it is the plausible-looking mistake.
    [Fact]
    public void AnAssistantIsNotEntitledToDmPrivateTraffic()
    {
        Assert.True(PeerCode.TryParse("BCDFGH", out var sender));
        Assert.True(PeerCode.TryParse("JKMNPR", out var assistant));

        Assert.False(
            MessageAudience.Includes(MessageTarget.DungeonMasterOnly, sender, assistant, SessionRole.Assistant));
    }

    // THE CONTROL: an ordinary message reaches the bystander. Without it, every row above passes
    // against an Includes that returns false for everyone.
    [Fact]
    public void AnOrdinaryMessageReachesEveryone()
    {
        Assert.True(PeerCode.TryParse("BCDFGH", out var sender));
        Assert.True(PeerCode.TryParse("TVWXY2", out var bystander));

        Assert.True(MessageAudience.Includes(MessageTarget.Everyone, sender, bystander, SessionRole.Player));
    }

    // R-2.7 ACROSS PATHS: the parenthetical is in the rendered line itself, not added by a surface.
    // That is what makes "not dropped in the echo, the export, or a narrow window" a property of one
    // function rather than a promise repeated at three call sites.
    [Theory]
    [InlineData(MessageKind.InCharacter)]
    [InlineData(MessageKind.OutOfCharacter)]
    [InlineData(MessageKind.Emote)]
    public void TheParentheticalSurvivesEveryKind(MessageKind kind)
    {
        var line = MessageLine.Render(kind, MessageTarget.Everyone, "Renn", Person("Tuka"), SessionRole.Player, "waits");

        Assert.Contains("Renn (Tuka)", line, StringComparison.Ordinal);
    }
}
