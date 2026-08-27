using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class AdmissionPromptTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 3, 0, 0, TimeSpan.Zero);

    private static PendingAdmission Request(RelinkClaim relink) =>
        new("PEER-3", "BKD-7RM-CDF-GH", AdmissionDeadline.DecidedByHost(Now), relink);

    [Fact]
    public void AnOrdinaryRequestAsksToJoin()
    {
        Assert.Equal("PEER-3 is asking to join", AdmissionPrompt.Headline(Request(RelinkClaim.None)));
    }

    [Fact]
    public void AResolvedRelinkNamesTheParticipantItResolvedTo()
    {
        var headline = AdmissionPrompt.Headline(Request(new RelinkClaim(true, "Ysera")));

        Assert.Equal("PEER-3 is asking to relink as Ysera", headline);
    }

    // The prompt still identifies the REQUESTER by their session-scoped code, never by a character
    // name (R-1.3, D-8). The relink label names who they claim to be within this campaign; it does
    // not replace the peer code, and both appear.
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

        Assert.Equal("PEER-3 is asking to join", headline);
        Assert.DoesNotContain("relink as", headline);
    }
}
