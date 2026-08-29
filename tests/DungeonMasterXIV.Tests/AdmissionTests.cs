using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class AdmissionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 3, 0, 0, TimeSpan.Zero);

    private static PendingAdmission Request(string peerCode = "PRBCD2", bool relink = false) =>
        new(PeerCodes.Of(peerCode),
            "BKD-7RM-CDF-GH",
            AdmissionDeadline.DecidedByHost(Now),
            relink ? new RelinkClaim(true, "Ysera") : RelinkClaim.None);

    // R-1.3a's hardest clause. Fails if: confirmation can be defaulted, constructed or initialised
    // into. A pre-ticked box is forbidden in terms, and the way to make that unavailable is for
    // there to be no path to "confirmed" except a call.
    [Fact]
    public void AFreshRequestIsNeverAlreadyConfirmed()
    {
        var request = Request();

        Assert.False(request.FingerprintConfirmed);
        Assert.Equal(AdmissionVerification.NotCompared, request.Verification);
    }

    // Fails if: confirming stops being recorded, at which point every admission looks unverified and
    // a DM who did the work gets no credit for it.
    [Fact]
    public void ConfirmingTheFingerprintIsRecorded()
    {
        var request = Request();

        request.ConfirmFingerprintMatched();

        Assert.Equal(AdmissionVerification.Confirmed, request.Verification);
    }

    // R-1.3a: a DM may admit without comparing, but it is RECORDED as unverified. Fails if:
    // admission defaults to verified, which would let the UI describe an unchecked session as
    // protected against interception — the overclaim D-8 makes approve-blocking.
    [Fact]
    public void AdmittingWithoutComparingIsRecordedAsNotCompared()
    {
        var desk = new AdmissionDesk();
        var audience = new SessionAudience();
        desk.Receive(Request());

        var decided = desk.Decide(PeerCodes.Of("PRBCD2"));
        var peer = audience.Admit(PeerCodes.Of("PRBCD2"), SessionRole.Player, decided!.Verification);

        Assert.Equal(AdmissionVerification.NotCompared, peer.Verification);
        Assert.Equal(0, audience.ConfirmedCount);
        Assert.Equal(1, audience.Count);
    }

    // The other half, so the pair cannot both be satisfied by hard-coding one answer.
    [Fact]
    public void AdmittingAfterComparingIsRecordedAsConfirmed()
    {
        var desk = new AdmissionDesk();
        var audience = new SessionAudience();
        var request = Request();
        desk.Receive(request);
        request.ConfirmFingerprintMatched();

        var peer = audience.Admit(PeerCodes.Of("PRBCD2"), SessionRole.Player, desk.Decide(PeerCodes.Of("PRBCD2"))!.Verification);

        Assert.Equal(AdmissionVerification.Confirmed, peer.Verification);
        Assert.Equal(1, audience.ConfirmedCount);
    }

    // A-1.5h. Fails if: lapse and denial collapse. A lapsed player may reasonably ask again; a
    // denied one must not be invited to, and telling someone they were refused when nobody looked
    // is a different and worse message than telling them nobody looked.
    [Fact]
    public void ALapsedRequestIsReportedAsLapsedAndNeverAsDenied()
    {
        var attempt = new JoinAttempt();
        attempt.Request(SessionCode.FromValid("BKD7RM"));
        attempt.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));

        attempt.Lapsed();

        Assert.Equal(JoinPhase.Lapsed, attempt.Phase);
        Assert.NotEqual(JoinPhase.Denied, attempt.Phase);
        Assert.False(attempt.MayReceiveSessionState);
    }

    // A-1.5h's second clause: re-requestable without a new code. Fails if: a lapse is terminal, which
    // would make the player go and ask the DM for a code they already have.
    [Fact]
    public void ALapsedPlayerMayAskAgainButADeniedOneMayNot()
    {
        var lapsed = new JoinAttempt();
        lapsed.Request(SessionCode.FromValid("BKD7RM"));
        lapsed.AwaitDecision();
        lapsed.Lapsed();

        var denied = new JoinAttempt();
        denied.Request(SessionCode.FromValid("BKD7RM"));
        denied.AwaitDecision();
        denied.Denied();

        Assert.True(lapsed.MayRequestAgain);
        Assert.False(denied.MayRequestAgain);
    }

    // R-1.3c's harder half, and the one implementations drop. Fails if: the joiner has no deadline to
    // count toward, which is exactly what happens if this client starts its own clock instead of
    // using the one it was given. Being told after fifteen silent minutes is worse than knowing.
    [Fact]
    public void AWaitingJoinerCanSeeHowLongIsLeftWhileItIsStillRunning()
    {
        var attempt = new JoinAttempt();
        attempt.Request(SessionCode.FromValid("BKD7RM"));

        attempt.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));

        Assert.Equal(AdmissionDeadline.Window, attempt.RemainingAt(Now));
        Assert.Equal(TimeSpan.FromMinutes(5), attempt.RemainingAt(Now.AddMinutes(10)));
    }

    // Fails if: the deadline is re-derived locally rather than carried. Two clocks pretending to be
    // one drift into a player told it lapsed while the DM still holds a live prompt.
    [Fact]
    public void TheJoinersCountdownIsTheDeadlineItWasGiven()
    {
        var decidedByHost = AdmissionDeadline.DecidedByHost(Now);
        var attempt = new JoinAttempt();
        attempt.Request(SessionCode.FromValid("BKD7RM"));

        attempt.AwaitDecision(decidedByHost);

        Assert.Equal(decidedByHost, attempt.Deadline);
    }

    // Fails if: expiry stops removing requests, so a DM accumulates prompts for people who gave up
    // an hour ago.
    [Fact]
    public void RequestsPastTheirDeadlineAreExpiredAndReturned()
    {
        var desk = new AdmissionDesk();
        desk.Receive(Request("PRBCD2"));
        desk.Receive(Request("PRBCD3"));

        var lapsed = desk.ExpireLapsed(Now.Add(AdmissionDeadline.Window));

        Assert.Equal(2, lapsed.Count);
        Assert.Empty(desk.Pending);
    }

    // Fails if: expiry fires early and takes a live request with it.
    [Fact]
    public void RequestsInsideTheirDeadlineAreLeftAlone()
    {
        var desk = new AdmissionDesk();
        desk.Receive(Request("PRBCD2"));

        Assert.Empty(desk.ExpireLapsed(Now.AddMinutes(14)));
        Assert.Single(desk.Pending);
    }

    // R-1.5, D-8: relink is never silent and never automatic — the DM approves every one, every
    // session. Fails if: a relink stops being distinguishable, at which point the prompt cannot say
    // what it is approving.
    [Fact]
    public void ARelinkRequestIsDistinguishableFromAFirstJoin()
    {
        Assert.True(Request(relink: true).IsRelink);
        Assert.False(Request().IsRelink);
    }

    // E-11: an Assistant runs the table, only the DM controls who is at it. Fails if: the role stops
    // being recorded, which is what a roster needs before PRD-3 can let an Assistant drive combat.
    [Fact]
    public void ARolesIsRecordedOnTheAdmittedParticipant()
    {
        var audience = new SessionAudience();

        var assistant = audience.Admit(PeerCodes.Of("PRBCD2"), SessionRole.Assistant, AdmissionVerification.Confirmed);

        Assert.Equal(SessionRole.Assistant, assistant.Role);
        Assert.Equal(SessionRole.Player, audience.Admit(PeerCodes.Of("PRBCD3")).Role);
    }

    // R-1.3a-iii's forbidden half, asserted rather than trusted to a comment: what is carried is a
    // CAPABILITY. Reporting it must not by itself record that anybody compared anything -- an
    // acknowledgement of the human act is forgeable by the attacker it would defend against.
    [Fact]
    public void ReportingCapabilityIsNotItselfAConfirmation()
    {
        var request = Request();

        request.JoinerReportedItCanCompare();

        Assert.Equal(ComparabilityEvidence.EstablishedCapable, request.Comparability);
        Assert.False(request.FingerprintConfirmed);
        Assert.Equal(AdmissionVerification.NotCompared, request.Verification);
    }

    // A joiner that never reports stays exactly as it was -- an old build is not a refused one.
    // Fails if: absence of the signal is treated as a denial rather than as "unknown".
    //
    // THIS COMMENT WAS ALWAYS RIGHT AND THE ASSERTION COULD NOT SAY IT. Under the old bool the only
    // available assertion was Assert.False, and false meant BOTH "unknown" and "established
    // incapable" -- so the test could not distinguish the thing its own comment named. The second
    // assertion below is the one the bool made impossible to write (R-1.3a-iv, A-1.2q).
    [Fact]
    public void AJoinerThatNeverReportsIsSimplyNotConfirmable()
    {
        var request = Request();

        Assert.Equal(ComparabilityEvidence.NotEstablished, request.Comparability);
        Assert.NotEqual(ComparabilityEvidence.EstablishedIncapable, request.Comparability);
        Assert.Equal(AdmissionVerification.NotCompared, request.Verification);
    }

    // THE DEFAULT IS LOAD-BEARING AND ASSERTED SEPARATELY (A-1.2q). A zero value meaning "incapable"
    // fails BY CONSTRUCTION, so this pins the zero rather than the behaviour -- a future reordering
    // of the enum that made EstablishedIncapable the default would pass every other test here.
    [Fact]
    public void TheZeroValueMeansNotEstablishedRatherThanIncapable()
    {
        Assert.Equal(ComparabilityEvidence.NotEstablished, default(ComparabilityEvidence));
        Assert.Equal(0, (int)ComparabilityEvidence.NotEstablished);
    }

    // Fails if: a confirmation is refused on SILENCE. A-1.2o fails a build that suppresses the
    // control "on the grounds the joiner could not compare, on the strength of silence alone", and
    // NotEstablished is silence -- the ordinary case, since a fast admission decides before any
    // receipt could arrive. The old bool would have refused here, on every request.
    [Fact]
    public void ANotEstablishedRequestIsStillConfirmable()
    {
        var request = Request();

        request.ConfirmFingerprintMatched();

        Assert.True(request.FingerprintConfirmed);
        Assert.Equal(AdmissionVerification.Confirmed, request.Verification);
    }
}
