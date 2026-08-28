using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class AdmissionPromptTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 3, 0, 0, TimeSpan.Zero);

    private static PendingAdmission Request(RelinkClaim relink) =>
        Request(relink, DisplayName.OrNone("Bob"));

    private static PendingAdmission Request(RelinkClaim relink, DisplayName name) =>
        new("PEER-3", "BKD-7RM-CDF-GH", AdmissionDeadline.DecidedByHost(Now), relink, null, name);

    [Fact]
    public void AnOrdinaryRequestAsksToJoin()
    {
        Assert.Equal("Bob (PEER-3) is asking to join", AdmissionPrompt.Headline(Request(RelinkClaim.None)));
    }

    [Fact]
    public void AResolvedRelinkNamesTheParticipantItResolvedTo()
    {
        var headline = AdmissionPrompt.Headline(Request(new RelinkClaim(true, "Ysera")));

        Assert.Equal("Bob (PEER-3) is asking to relink as Ysera", headline);
    }

    // The prompt still identifies the REQUESTER by their session-scoped code. R-1.3e reversed the
    // old show-no-name rule, so a name now appears too - but it did NOT make the name an
    // identifier, and the relink label still does not replace the peer code. All three appear.
    [Fact]
    public void TheRequesterIsStillIdentifiedByPeerCodeOnARelink()
    {
        var headline = AdmissionPrompt.Headline(Request(new RelinkClaim(true, "Ysera")));

        Assert.Contains("PEER-3", headline);
    }

    // A claim that did not resolve reads exactly like an ordinary join, so a failed guess cannot be
    // told apart from a first-time joiner by watching the DM's screen.
    [Fact]
    public void AClaimThatDidNotResolveReadsAsAnOrdinaryJoin()
    {
        Assert.Equal(
            AdmissionPrompt.Headline(Request(RelinkClaim.None)),
            AdmissionPrompt.Headline(Request(new RelinkClaim(false, null))));
    }

    // Defence against a label that is present but empty: it would render "asking to relink as "
    // with nothing after it, which reads as a bug to a DM being asked to make a security decision.
    [Fact]
    public void ARelinkWithNoUsableLabelFallsBackRatherThanRenderingABlank()
    {
        var headline = AdmissionPrompt.Headline(Request(new RelinkClaim(true, string.Empty)));

        Assert.Equal("Bob (PEER-3) is asking to join", headline);
        Assert.DoesNotContain("relink as", headline);
    }

    // R-1.3e, and the criterion that would be missed: names are self-declared and nothing prevents
    // two requesters sending the same one (A-1.2d). Two identical names must still read as two
    // different requesters, and the peer code is the only thing in the headline that can do it.
    [Fact]
    public void TwoRequestersWithTheSameNameAreStillToldApart()
    {
        var name = DisplayName.OrNone("Bob");
        var first = new PendingAdmission("PEER-3", "BKD-7RM-CDF-GH", AdmissionDeadline.DecidedByHost(Now), default, null, name);
        var second = new PendingAdmission("PEER-9", "CDF-GHJ-KMN-PR", AdmissionDeadline.DecidedByHost(Now), default, null, name);

        Assert.NotEqual(AdmissionPrompt.Headline(first), AdmissionPrompt.Headline(second));
        Assert.Contains("PEER-3", AdmissionPrompt.Headline(first));
        Assert.Contains("PEER-9", AdmissionPrompt.Headline(second));
    }

    // A requester that sent no name - an older build, or one whose name was refused - still gets a
    // prompt, and it says so rather than rendering a gap where a name would be. A DM meeting a
    // blank would read it as a rendering fault and look past the fingerprint beside it.
    [Fact]
    public void ARequesterThatNamedItselfNothingIsStillNamedSomething()
    {
        var headline = AdmissionPrompt.Headline(Request(RelinkClaim.None, DisplayName.None));

        Assert.Contains(DisplayName.Unstated, headline);
        Assert.Contains("PEER-3", headline);
        Assert.DoesNotContain("()", headline);
    }
}