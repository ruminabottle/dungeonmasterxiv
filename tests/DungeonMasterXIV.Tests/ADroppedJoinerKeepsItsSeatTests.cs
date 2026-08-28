using System;
using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.17a and BUG-53: exclusivity ends when the SEAT ends, never when the link does.
/// </summary>
/// <remarks>
/// <para>
/// <b>An admitted joiner whose link drops was offered "Start session" while the DM was still holding
/// their place</b> — R-1.3h violated by the commonest failure there is, a network hiccup. A-1.17a is
/// the machine criterion for it and <b>nothing referenced it before this file</b>.
/// </para>
/// <para>
/// <b>Driven through the COORDINATOR, deliberately.</b> QA-3's repro drives <see cref="JoinAttempt"/>
/// in isolation, which is enough to demonstrate the bug and not enough to test the fix: at that level
/// the seat clock does not exist, so a build that keyed the decision on the coordinator would pass
/// while a build that keyed it on the attempt's phase would too. The predicate under test is the one
/// the window actually calls.
/// </para>
/// <para>
/// <b>Both halves, and the second is not optional.</b> Suppression alone locks a user out of hosting
/// forever, because nothing would ever expire the seat — a safe-looking partial that removes a
/// working control, which is worse than the bug.
/// </para>
/// </remarks>
public class ADroppedJoinerKeepsItsSeatTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 18, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    /// <summary>Longer than any window this client holds, so expiry is reached in one tick.</summary>
    private static readonly TimeSpan PastEveryWindow = TimeSpan.FromHours(1);

    // The bug. Fails on origin/main before this fix: the phase moves to Failed and the window offers
    // a host affordance while the seat is still resumable.
    [Fact]
    public void AnAdmittedJoinerWhoseLinkDropsIsStillInASession()
    {
        var (joiner, _) = Admitted();

        joiner.Fail(SessionFailure.ConnectionLost);

        Assert.Equal(JoinPhase.Failed, joiner.Join.Phase);
        Assert.True(joiner.InAJoinedSession, "The seat is still resumable, so hosting must not be offered.");
    }

    // The other half. Without it the suppression above never lifts and the user can never host again
    // — which is why this could not ship as a quick one-line hotfix.
    [Fact]
    public void OnceTheSeatWindowExpiresHostingIsOfferedAgain()
    {
        var (joiner, _) = Admitted();
        joiner.Fail(SessionFailure.ConnectionLost);

        joiner.Tick(PastEveryWindow, Now);

        Assert.False(joiner.InAJoinedSession, "The seat has lapsed, so hosting must be available again.");
    }

    // The discrimination the phase cannot make. A join that never succeeded holds no seat, so a
    // blanket "Failed means still in a session" would lock this user out of hosting for the window —
    // and would contradict MayRequestAgain, which treats Failed as retryable.
    [Fact]
    public void AJoinThatNeverSucceededHoldsNoSeat()
    {
        var transport = new FakeTransport();
        var joiner = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default);
        joiner.RequestJoin(Code, DisplayName.OrNone("Bob"));
        joiner.SynchroniseTransport();
        joiner.Tick(TimeSpan.Zero, Now);

        joiner.Fail(SessionFailure.ConnectionLost);

        Assert.Equal(JoinPhase.Failed, joiner.Join.Phase);
        Assert.False(joiner.InAJoinedSession, "Never admitted, so there is no seat to hold.");
        Assert.True(joiner.Join.MayRequestAgain);
    }

    // R-1.5a: a deliberate quit removes the seat immediately. Asking to join again is that, so the
    // suppression must lift at once rather than waiting out a window nobody is using.
    [Fact]
    public void AskingToJoinAgainReleasesTheSeatAtOnce()
    {
        var (joiner, _) = Admitted();
        joiner.Fail(SessionFailure.ConnectionLost);
        Assert.True(joiner.InAJoinedSession);

        joiner.RequestJoin(Code, DisplayName.OrNone("Bob"));

        Assert.False(joiner.Grace.IsRunning);
        Assert.True(joiner.InAJoinedSession, "Contacting again is itself being in a session.");

        joiner.Fail(SessionFailure.SessionCodeNotActive);
        Assert.False(joiner.InAJoinedSession, "The new attempt never reached Admitted, so no seat.");
    }

    // The seat clock is NOT the grace window, and starting one must not start the other. Grace is
    // what this client allows a lost HOST; the seat is how long its own place is worth waiting on.
    [Fact]
    public void ADroppedJoinerDoesNotStartTheHostsGraceWindow()
    {
        var (joiner, _) = Admitted();

        joiner.Fail(SessionFailure.ConnectionLost);

        Assert.False(joiner.Grace.IsRunning, "Grace is the host's window; a joiner dropping is not that.");
    }

    // THE SUPERSET PROPERTY, which is what the release ruling rests on and nothing pinned.
    //
    // BUG-53 ships without A-1.24 only because the new predicate is a strict SUPERSET of the old
    // one: the host affordance can never appear SOONER than it does today, so the missing host-side
    // clock cannot make anything worse than it already is. That holds because the first disjunct is
    // textually identical to the predicate it replaced — and "textually identical" is not a property
    // a test was watching.
    //
    // So: each live phase must satisfy InAJoinedSession ON ITS OWN, with the seat NOT running. A
    // future narrowing of the first disjunct that kept "|| Seat.IsRunning" would still pass every
    // other test in this file — the seat covers the dropped case — while silently breaking the
    // reasoning the release was approved on.
    [Fact]
    public void EachLivePhaseHoldsTheAffordanceWithoutTheSeat()
    {
        foreach (var (phase, interruption) in LivePhases())
        {
            Assert.False(interruption.Seat.IsRunning, $"{phase}: the seat must not be what carries this.");
            Assert.True(interruption.InAJoinedSession, $"{phase}: must suppress hosting on the phase alone.");
        }
    }

    /// <summary>Each phase a live join passes through, with a seat that has never started.</summary>
    private static IEnumerable<(JoinPhase Phase, SessionInterruption Interruption)> LivePhases()
    {
        foreach (var target in new[] { JoinPhase.Contacting, JoinPhase.AwaitingDecision, JoinPhase.Admitted })
        {
            var join = new JoinAttempt();
            var transport = new FakeTransport();
            var interruption = new SessionInterruption(
                new RelayLink(transport, () => RelayEndpoint.Default, _ => { }),
                new HostSession(),
                join,
                () => { },
                GraceWindow.Default);

            join.Request(Code);
            if (target != JoinPhase.Contacting)
            {
                join.AwaitDecision();
            }

            if (target == JoinPhase.Admitted)
            {
                join.Admitted();
            }

            Assert.Equal(target, join.Phase);
            yield return (target, interruption);
        }
    }

    /// <summary>A coordinator driven to Admitted the way a real joiner reaches it.</summary>
    private static (SessionCoordinator Joiner, SessionKeyExchange HostKeys) Admitted()
    {
        var transport = new FakeTransport();
        var joiner = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default);
        var hostKeys = new SessionKeyExchange();

        joiner.RequestJoin(Code, DisplayName.OrNone("Bob"));
        joiner.SynchroniseTransport();
        joiner.Tick(TimeSpan.Zero, Now);

        // The host answers the request before it decides (R-1.3a-i), which is what moves the joiner
        // to AwaitingDecision — Admitted is unreachable from Contacting.
        transport.Deliver(WireEnvelope.ForJoinPending(
            Code,
            joiner.JoinerKeys!.PublicKey,
            hostKeys.PublicKey,
            AdmissionDeadline.DecidedByHost(Now)));
        joiner.Tick(TimeSpan.Zero, Now);

        transport.Deliver(WireEnvelope.ForJoinAccepted(Code, joiner.JoinerKeys!.PublicKey, hostKeys.PublicKey));
        joiner.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.Admitted, joiner.Join.Phase);
        return (joiner, hostKeys);
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));
    }
}
