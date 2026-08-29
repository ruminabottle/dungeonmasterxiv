using System;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.2o: where the host has not established whether the joiner could compare, the prompt says so
/// and asserts neither direction. A-1.2f: the control is suppressed only on the FACT.
/// </summary>
/// <remarks>
/// <para>
/// <b>Silence is the ordinary case, not an edge, and that is what makes this criterion sharp.</b>
/// qa-2 measured a 171ms admission producing zero receipts from a joiner that could compare
/// perfectly well. So the state these tests spend most of their assertions on —
/// <see cref="ComparabilityEvidence.NotEstablished"/> — is the one most real sessions decide in, and
/// a build that treats it as "could not compare" gets the common case wrong rather than a corner.
/// </para>
/// <para>
/// <b>Two ways to fail A-1.2o, and only one of them looks like a bug.</b> Suppressing the control on
/// silence is the loud failure. Saying NOTHING is the quiet one, and it is what shipped: a bare
/// tickbox reads to a DM as an ordinary comparison, which is the false record BUG-33 produced. Both
/// are asserted below.
/// </para>
/// </remarks>
public sealed class ThePromptAssertsNeitherDirectionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 0, 0, 0, TimeSpan.Zero);

    // A-1.2o's positive half. Fails if: the prompt is silent about a joiner whose comparability
    // nothing has established -- which is what the build did before this, and the failure a reviewer
    // would never see because a silent prompt looks exactly like a correct one.
    [Fact]
    public void AnUnestablishedJoinerIsSaidToBeUnestablishedRatherThanPassedOverInSilence()
    {
        var request = Request();

        Assert.Equal(ComparabilityEvidence.NotEstablished, request.Comparability);
        Assert.NotEqual(string.Empty, AdmissionPrompt.ComparabilityNote(request));
    }

    // THE CRITERION'S ACTUAL WORDS, checked against the copy rather than around it: it "asserts
    // NEITHER direction". Fails if: the sentence tells the DM the joiner cannot compare. A note that
    // said so would satisfy the test above -- non-empty is not the same as non-asserting -- and
    // would be A-1.2o failed in the copy while passing in the shape.
    [Fact]
    public void TheUnestablishedNoteClaimsNeitherThatTheyCanNorThatTheyCannot()
    {
        var note = AdmissionPrompt.ComparabilityNote(Request());

        Assert.Contains("neither a yes nor a no", note, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cannot show", note, StringComparison.OrdinalIgnoreCase);
    }

    // A-1.2o's HARD prohibition, and the one that would have failed the build the ticket originally
    // described. Fails if: the control is suppressed on the strength of silence alone. Following
    // DMXENG-9's own description faithfully would have landed exactly this, firing on every fast
    // admission -- the common path, not an edge.
    [Fact]
    public void TheControlIsStillOfferedWhenNothingHasBeenEstablished()
    {
        Assert.True(AdmissionPrompt.OffersConfirmation(Request()));
    }

    // THE NEGATIVE HALF, and it is what stops the note becoming wallpaper. A sentence beside every
    // request is one a DM stops reading, and then it says nothing on the day it matters. Fails if:
    // a joiner who demonstrably CAN compare is qualified anyway.
    [Fact]
    public void AJoinerKnownToBeAbleToCompareIsNotQualifiedAtAll()
    {
        var request = Request();
        request.JoinerReportedItCanCompare();

        Assert.Equal(string.Empty, AdmissionPrompt.ComparabilityNote(request));
        Assert.True(AdmissionPrompt.OffersConfirmation(request));
    }

    // A-1.2f, on the one state that discharges it. UNREACHABLE IN PRODUCTION TODAY and asserted
    // anyway: D-14 makes the pending notice additive, so a client that ignores it carries the same
    // version and nothing refuses it -- there is no producer for this state. The branch is here so
    // that the day a signal exists, the suppression is already correct and already pinned, rather
    // than being written under time pressure against a criterion nobody re-reads.
    //
    // This test drives the enum directly BECAUSE nothing else can. That is the honest way to cover
    // an unreachable state: say it is unreachable, cover it at the level where it is reachable, and
    // do not dress a direct poke up as an end-to-end proof.
    [Fact]
    public void TheControlIsSuppressedOnPositiveEvidenceThatTheyCannotCompare()
    {
        Assert.False(AdmissionPrompt.OffersConfirmation(Incapable()));
        Assert.NotEqual(string.Empty, AdmissionPrompt.ComparabilityNote(Incapable()));
    }

    // The refusal and the prompt agree. Fails if: the UI suppresses the control on a state the model
    // would still accept a confirmation for, or offers it on one the model refuses -- either way the
    // DM's screen and the record disagree, and A-1.2f is discharged in one of them only.
    [Fact]
    public void WhateverThePromptOffersIsWhatTheRecordWillAccept()
    {
        foreach (var request in new[] { Request(), Capable(), Incapable() })
        {
            request.ConfirmFingerprintMatched();

            Assert.Equal(AdmissionPrompt.OffersConfirmation(request), request.FingerprintConfirmed);
        }
    }

    // The three states are covered above and this is what says so out loud. Fails BY NAME if a
    // fourth is added and nobody decides what the prompt does with it -- the same property the
    // completeness test asserts of the wire, applied to the one enum a DM's screen depends on.
    [Fact]
    public void EveryComparabilityStateHasARuledAnswer()
    {
        var covered = new[]
        {
            ComparabilityEvidence.NotEstablished,
            ComparabilityEvidence.EstablishedCapable,
            ComparabilityEvidence.EstablishedIncapable,
        };

        var uncovered = Enum.GetValues<ComparabilityEvidence>().Where(e => !covered.Contains(e)).ToList();

        Assert.True(
            uncovered.Count == 0,
            $"ComparabilityEvidence gained {string.Join(", ", uncovered)} and no test in this file "
            + "says what the DM's prompt does with it. A-1.2o is a rule about every state, not the "
            + "three that existed when it was written.");
    }

    private static PendingAdmission Capable()
    {
        var request = Request();
        request.JoinerReportedItCanCompare();
        return request;
    }

    // The one state with no production producer, reached the only way it can be. If a producer ever
    // exists, this helper is where the test suite should stop needing reflection.
    private static PendingAdmission Incapable()
    {
        var request = Request();

        typeof(PendingAdmission)
            .GetProperty(nameof(PendingAdmission.Comparability))!
            .SetValue(request, ComparabilityEvidence.EstablishedIncapable);

        Assert.Equal(ComparabilityEvidence.EstablishedIncapable, request.Comparability);
        return request;
    }

    private static PendingAdmission Request() =>
        new(
            PeerCodes.Of("PRBCD4"),
            "BKD-7RM-CDF-GH",
            AdmissionDeadline.DecidedByHost(Now),
            RelinkClaim.None,
            null,
            DisplayName.OrNone("Bob"));
}
