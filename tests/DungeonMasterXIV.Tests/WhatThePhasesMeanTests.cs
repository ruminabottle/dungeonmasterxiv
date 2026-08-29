using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The rules <see cref="SessionLiveness"/> owns, pinned per phase rather than per outcome.
/// </summary>
/// <remarks>
/// <para>
/// <b>This file exists because a mutation survived.</b> DMXENG-69 moved these predicates out of
/// <c>SessionCoordinator</c>, and mutating them there was the first time anyone asked what covered
/// them. Removing <c>Registering</c> from <c>InAHostedSession</c> killed one test; dropping the
/// whole join half of <c>RequiresRelayConnection</c> killed nineteen. But narrowing that join half
/// to <c>Contacting</c> alone — deleting <c>AwaitingDecision</c> and <c>Admitted</c> — left all
/// 1,122 tests green.
/// </para>
/// <para>
/// <b>Nineteen tests and none of them watched the phase.</b> They drive a join end to end, and a
/// connection opened at <c>Contacting</c> is never asked to close, so it stays up through the
/// phases that follow and every one of them passes on a predicate that no longer names them. The
/// case the gap actually costs is the one no end-to-end test builds: a client that reaches
/// <c>AwaitingDecision</c> or <c>Admitted</c> and then calls <c>SynchroniseTransport</c> — a
/// reconnect, a resumed seat — where the narrowed rule hangs up on a player mid-admission.
/// </para>
/// <para>
/// <b>The gap predates the extraction.</b> The predicate was a private method with the same three
/// phases; moving it gave it a name to test, it did not create the hole.
/// </para>
/// </remarks>
public class WhatThePhasesMeanTests
{
    // THE MUTATION THIS CLOSES: JoinRequiresRelayConnection => Join.Phase is JoinPhase.Contacting.
    //
    // Asserted ON ITS OWN, phase by phase, with a HostSession that is not hosting -- so the host
    // disjunct cannot be what carries any of them. An assertion over a driven join would pass on the
    // narrowed rule, which is exactly how nineteen tests missed it.
    [Fact]
    public void EveryPhaseOfALiveJoinHoldsTheConnectionOnItsOwn()
    {
        foreach (var (phase, join) in LiveJoinPhases())
        {
            var liveness = new SessionLiveness(new HostSession(), join);

            Assert.False(liveness.Host.RequiresRelayConnection, $"{phase}: the host must not be what carries this.");
            Assert.True(liveness.JoinRequiresRelayConnection, $"{phase}: the join alone must hold the connection.");
            Assert.True(liveness.RequiresRelayConnection, $"{phase}: and so must the composed answer.");
        }
    }

    // The other direction, or the predicate above is satisfied by returning true. Idle is the state
    // R-1.1 is actually about: no session is running, so nothing may hold a relay connection.
    [Fact]
    public void AJoinThatIsOverDoesNotHoldTheConnection()
    {
        foreach (var (phase, join) in FinishedJoinPhases())
        {
            var liveness = new SessionLiveness(new HostSession(), join);

            Assert.False(liveness.JoinRequiresRelayConnection, $"{phase}: the attempt is over.");
            Assert.False(liveness.RequiresRelayConnection, $"{phase}: and nothing else is running.");
        }
    }

    // Registering counts. This is BUG-115's rule, re-pinned on the type that now owns it rather than
    // only through the coordinator, so a future narrowing is caught where the enumeration lives.
    [Fact]
    public void HostingIsLiveFromTheMomentACodeIsClaimed()
    {
        var host = new HostSession();
        Assert.False(
            new SessionLiveness(host, new JoinAttempt()).InAHostedSession,
            "NotHosting: there is no session to protect.");

        host.Start(Code);
        Assert.True(new SessionLiveness(host, new JoinAttempt()).InAHostedSession, "Registering is already losable.");

        host.Registered();
        Assert.True(new SessionLiveness(host, new JoinAttempt()).InAHostedSession, "Hosting is the case it exists for.");
    }

    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    private static IEnumerable<(JoinPhase Phase, JoinAttempt Join)> LiveJoinPhases()
    {
        var contacting = new JoinAttempt();
        contacting.Request(Code);
        yield return (JoinPhase.Contacting, contacting);

        var awaiting = new JoinAttempt();
        awaiting.Request(Code);
        awaiting.AwaitDecision();
        yield return (JoinPhase.AwaitingDecision, awaiting);

        var admitted = new JoinAttempt();
        admitted.Request(Code);
        admitted.AwaitDecision();
        admitted.Admitted();
        yield return (JoinPhase.Admitted, admitted);
    }

    private static IEnumerable<(JoinPhase Phase, JoinAttempt Join)> FinishedJoinPhases()
    {
        yield return (JoinPhase.Idle, new JoinAttempt());

        var denied = new JoinAttempt();
        denied.Request(Code);
        denied.AwaitDecision();
        denied.Denied();
        yield return (JoinPhase.Denied, denied);

        var lapsed = new JoinAttempt();
        lapsed.Request(Code);
        lapsed.AwaitDecision();
        lapsed.Lapsed();
        yield return (JoinPhase.Lapsed, lapsed);
    }
}
