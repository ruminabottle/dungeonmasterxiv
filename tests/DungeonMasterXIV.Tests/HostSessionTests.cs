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

        Assert.True(session.ExpireIfRegistrationTimedOut(HostSession.RegistrationTimeout));

        Assert.Equal(HostingPhase.Failed, session.Phase);
        Assert.Equal(SessionFailure.RelayUnreachable, session.Failure);
        Assert.False(session.RequiresRelayConnection);
    }

    // Fails if: the timeout fires early and kills a registration that was still in progress.
    [Fact]
    public void RegisteringWithinTheTimeoutIsLeftAlone()
    {
        var session = new HostSession();
        session.Start(Code);

        Assert.False(session.ExpireIfRegistrationTimedOut(HostSession.RegistrationTimeout - TimeSpan.FromSeconds(1)));

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

        Assert.False(session.ExpireIfRegistrationTimedOut(TimeSpan.FromHours(3)));

        Assert.Equal(HostingPhase.Hosting, session.Phase);
    }
}
