using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class JoinAttemptTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    // D-13 None, and R-1.3's "not a filtered view, nothing". Fails if: session state becomes
    // reachable in any phase but Admitted. Enumerates every phase, because the dangerous edit is a
    // new phase that forgets to be excluded rather than one of these being flipped.
    [Fact]
    public void OnlyAnAdmittedClientMayReceiveSessionState()
    {
        var attempt = new JoinAttempt();
        Assert.False(attempt.MayReceiveSessionState);    // Idle

        attempt.Request(Code);
        Assert.False(attempt.MayReceiveSessionState);    // Contacting

        attempt.AwaitDecision();
        Assert.False(attempt.MayReceiveSessionState);    // AwaitingDecision — the DM has not decided

        attempt.Admitted();
        Assert.True(attempt.MayReceiveSessionState);     // Admitted

        attempt.Denied();
        Assert.False(attempt.MayReceiveSessionState);    // removed mid-session
    }

    // R-1.3: the DM can remove an admitted player and that client immediately stops receiving
    // state. Fails if: removal is treated as a no-op once admitted.
    [Fact]
    public void RemovalAfterAdmissionStopsStateImmediately()
    {
        var attempt = new JoinAttempt();
        attempt.Request(Code);
        attempt.AwaitDecision();
        attempt.Admitted();

        attempt.Denied();

        Assert.Equal(JoinPhase.Denied, attempt.Phase);
        Assert.False(attempt.MayReceiveSessionState);
    }

    // Fails if: admission can be granted without the request having reached the DM. The guard is
    // what stops a forged accept from a relay short-circuiting the DM's decision, which is the only
    // thing protecting the session (R-1.2, R-1.3).
    [Fact]
    public void AdmissionOnlyFollowsARequestTheDmActuallyReceived()
    {
        var attempt = new JoinAttempt();

        attempt.Admitted();

        Assert.Equal(JoinPhase.Idle, attempt.Phase);
        Assert.False(attempt.MayReceiveSessionState);
    }

    // A-1.5b, joiner half. Fails if: the timeout is removed and the player watches a spinner that
    // never resolves — the exact thing R-1.8 forbids.
    [Fact]
    public void ContactingThatNeverCompletesBecomesAStatedFailure()
    {
        var attempt = new JoinAttempt();
        attempt.Request(Code);

        Assert.True(attempt.ExpireIfContactTimedOut(JoinAttempt.ContactTimeout));

        Assert.Equal(JoinPhase.Failed, attempt.Phase);
        Assert.Equal(SessionFailure.RelayUnreachable, attempt.Failure);
    }

    // The one that would be wrong to "fix". Fails if: waiting on the DM starts timing out. A DM
    // taking two minutes to decide is normal, and telling the player their attempt failed would be
    // false — R-1.3 requires them to know they are waiting on a person, not to be lied to.
    [Fact]
    public void WaitingOnTheDmNeverTimesOut()
    {
        var attempt = new JoinAttempt();
        attempt.Request(Code);
        attempt.AwaitDecision();

        Assert.False(attempt.ExpireIfContactTimedOut(TimeSpan.FromHours(1)));

        Assert.Equal(JoinPhase.AwaitingDecision, attempt.Phase);
    }

    // Fails if: a denied client is left in a state the UI cannot name. R-1.3 forbids leaving the
    // player looking at an ambiguous spinner, so every terminal state has to be distinguishable.
    [Theory]
    [InlineData(SessionFailure.RelayUnreachable)]
    [InlineData(SessionFailure.ConnectionLost)]
    [InlineData(SessionFailure.SessionCodeNotActive)]
    public void EveryFailureLeavesAPhaseAndAReasonTheUiCanState(SessionFailure failure)
    {
        var attempt = new JoinAttempt();
        attempt.Request(Code);

        attempt.Fail(failure);

        Assert.Equal(JoinPhase.Failed, attempt.Phase);
        Assert.Equal(failure, attempt.Failure);
        Assert.NotEmpty(SessionFailureMessage.For(failure));
    }
}
