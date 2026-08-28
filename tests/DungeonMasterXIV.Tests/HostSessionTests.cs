using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

public class HostSessionTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    // R-1.1's hardest sentence: "There is no circumstance in which the plugin holds a relay
    // connection while no session is running." Fails if: RequiresRelayConnection ever returns true
    // in a phase where no session is live. Enumerates every phase rather than the happy path, so a
    // new phase added later without thinking about the connection is caught here.
    [Fact]
    public void ARelayConnectionIsRequiredInExactlyTheTwoPhasesWhereASessionIsRunning()
    {
        var session = new HostSession();
        Assert.False(session.RequiresRelayConnection);   // NotHosting

        session.Start(Code);
        Assert.True(session.RequiresRelayConnection);    // Registering

        session.Registered();
        Assert.True(session.RequiresRelayConnection);    // Hosting

        session.Stop();
        Assert.False(session.RequiresRelayConnection);   // NotHosting again

        session.Start(Code);
        session.Fail(SessionFailure.RelayUnreachable);
        Assert.False(session.RequiresRelayConnection);   // Failed
    }

    // Fails if: the connection is opened before the DM starts a session. R-1.1 says the client
    // connects "at that moment, and not before".
    [Fact]
    public void AFreshSessionHoldsNoConnectionAndNoCode()
    {
        var session = new HostSession();

        Assert.Equal(HostingPhase.NotHosting, session.Phase);
        Assert.Null(session.Code);
        Assert.False(session.RequiresRelayConnection);
    }

    // Fails if: stopping leaves the code behind. R-1.1 treats ending, closing and unloading the
    // same, so there is one path and it must clear everything.
    [Fact]
    public void StoppingClearsTheCodeAndTheFailure()
    {
        var session = new HostSession();
        session.Start(Code);
        session.Fail(SessionFailure.ConnectionLost);

        session.Stop();

        Assert.Equal(HostingPhase.NotHosting, session.Phase);
        Assert.Null(session.Code);
        Assert.Equal(SessionFailure.None, session.Failure);
    }

    // A-1.5b, host half. Fails if: the timeout is removed and registering can run forever, which is
    // the indefinite spinner R-1.8 forbids by name.
    [Fact]
    public void RegisteringThatNeverCompletesBecomesAStatedFailure()
    {
        var session = new HostSession();
        session.Start(Code);

        Assert.True(session.ExpireIfRegistrationTimedOut(HostSession.RegistrationTimeout, requestWasSent: true));

        Assert.Equal(HostingPhase.Failed, session.Phase);

        // NOT RelayUnreachable (BUG-36) — but note this now says so because the CALLER states the
        // request went out, not because reaching a timeout implies it. The old comment here claimed
        // the latter as a guarantee of the code path, and it was false: a hung connect reached this
        // line having never connected (BUG-38). The sentence is gone rather than corrected, because
        // it is the sentence that misled three readers in one evening.
        Assert.Equal(SessionFailure.RegistrationNotAnswered, session.Failure);
        Assert.False(session.RequiresRelayConnection);
    }

    // BUG-38, at the unit the decision is made in. Fails if: a timeout reached WITHOUT the request
    // ever going out is reported as one the relay heard and ignored — which told a user whose
    // firewall was dropping the connection that their network was not the problem.
    [Fact]
    public void ATimeoutReachedWithoutSendingTheRequestIsNotAnUnansweredRegistration()
    {
        var session = new HostSession();
        session.Start(Code);

        Assert.True(session.ExpireIfRegistrationTimedOut(HostSession.RegistrationTimeout, requestWasSent: false));

        Assert.Equal(SessionFailure.ConnectionNeverOpened, session.Failure);
        Assert.NotEqual(SessionFailure.RegistrationNotAnswered, session.Failure);
    }

    // Fails if: the timeout fires early and kills a registration that was still in progress.
    [Fact]
    public void RegisteringWithinTheTimeoutIsLeftAlone()
    {
        var session = new HostSession();
        session.Start(Code);

        Assert.False(session.ExpireIfRegistrationTimedOut(HostSession.RegistrationTimeout - TimeSpan.FromSeconds(1), requestWasSent: true));

        Assert.Equal(HostingPhase.Registering, session.Phase);
    }

    // Fails if: the timeout applies to a live session. A DM hosting for three hours is not a
    // timeout, and this is the input that would prove it had become one.
    [Fact]
    public void ALiveSessionNeverTimesOutHoweverLongItRuns()
    {
        var session = new HostSession();
        session.Start(Code);
        session.Registered();

        Assert.False(session.ExpireIfRegistrationTimedOut(TimeSpan.FromHours(3), requestWasSent: true));

        Assert.Equal(HostingPhase.Hosting, session.Phase);
    }
}
