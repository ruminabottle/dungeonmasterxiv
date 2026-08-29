using System;
using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// What <c>StopHosting</c> tears down, for the steps of it that nothing was watching.
/// </summary>
/// <remarks>
/// <para>
/// <b>THESE EXIST BECAUSE DMXENG-51 MOVED THAT CODE AND "THE WHOLE SUITE PASSES" TURNED OUT TO BE A
/// WEAKER CLAIM THAN IT LOOKS.</b> The extraction is a pure move; the way to find out whether a
/// green suite could tell was to break it on purpose. Deleting each teardown step in turn, against
/// the suite as it stood before this file existed:
/// </para>
/// <code>
/// _admissions.Clear()               -> 1 test fails    covered
/// _inbox.Clear()                    -> SUITE GREEN     not covered
/// Grace.Reset()                     -> SUITE GREEN     not covered
/// _handshake.ForgetHostRegistration -> SUITE GREEN     not covered
/// SynchroniseTransport()            -> 2 tests fail    covered
/// </code>
/// <para>
/// With this file in place the same five deletions give four failures and one silence, and each
/// failure is the test named for that step rather than some distant assertion catching it by
/// accident:
/// </para>
/// <code>
/// _admissions.Clear()               -> SessionCoordinatorTests.EndingTheSessionEmptiesTheAudience
/// _inbox.Clear()                    -> AnAnswerToTheLastSessionsCodeCannotRegisterTheNextOne
/// Grace.Reset()                     -> EndingTheSessionStopsAGraceWindowTheLostConnectionStarted
/// _handshake.ForgetHostRegistration -> still silent, and deliberately so; see below
/// SynchroniseTransport()            -> SessionCoordinatorTests, two of them
/// </code>
/// <para>
/// <b>The gap is pre-existing rather than introduced, and that was measured rather than assumed</b> —
/// the same three deletions made directly to <c>SessionCoordinator.StopHosting</c> on
/// <c>origin/main</c> also leave the suite green. A move of this code was resting on a suite that
/// could not see three fifths of it.
/// </para>
/// <para>
/// <b>AND MY FIRST TWO TESTS DID NOT CATCH THEIR OWN MUTATIONS, WHICH IS THE PART WORTH KEEPING.</b>
/// The first pair asserted that a stale <c>JoinRequest</c> could not reach the admission prompt and
/// that a stale registration receipt could not misreport a timeout. Both passed. Both also passed
/// with the step they claimed to pin <i>deleted</i> — so both were green for a reason unrelated to
/// their names, which is the exact failure this file's own opening paragraph is about. They were
/// replaced rather than kept: <b>a test that cannot fail is not evidence, however well it reads.</b>
/// </para>
/// <para>
/// <b>One step is not covered here and is not covered anywhere, deliberately.</b>
/// <c>_handshake.ForgetHostRegistration()</c> in the stop path is <b>redundant on every path I could
/// find</b> — <c>Start</c> calls it too, nothing reads <c>RegistrationWasSent</c> while not
/// Registering, and no session can begin except through <c>Start</c>. It is left in place because
/// DMXENG-51 is a pure move and removing a line is a behaviour change; it is reported on the ticket
/// rather than quietly deleted or covered by a test that would only be asserting that <c>Start</c>
/// works.
/// </para>
/// </remarks>
public sealed class EndingASessionReleasesWhatItHeldTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 3, 0, 0, TimeSpan.Zero);

    // Fails if: _inbox.Clear() is removed from the stop path.
    //
    // THE DRAIN NEVER ASKS WHICH SESSION A FRAME CAME FROM, AND ApplyRegistration DOES NOT COMPARE
    // THE CODE -- it checks only that this client is Registering. So a CodeAccepted still sitting in
    // the queue when the DM ends a session is an answer to the OLD code request that will be applied
    // to the NEW one: session two goes Hosting on the strength of the relay accepting session one's
    // code, without the relay ever having answered session two.
    //
    // That is worse than a stale frame. The DM is shown a live session under a code the relay may
    // have given to somebody else, which is precisely the collision R-1.2a's arbitration exists to
    // settle.
    [Fact]
    public void AnAnswerToTheLastSessionsCodeCannotRegisterTheNextOne()
    {
        var host = Hosting(out var transport);
        var answeredCode = host.Host.Code!.Value;

        // Arrives before the session ends and is never drained -- the DM stops hosting first.
        transport.Deliver(WireEnvelope.ForCodeAccepted(answeredCode));
        host.StopHosting();

        host.StartHosting();
        Assert.NotEqual(answeredCode.Value, host.Host.Code!.Value.Value);
        host.Tick(TimeSpan.Zero, Now);

        Assert.Equal(HostingPhase.Registering, host.Host.Phase);
    }

    // The positive control. Without it the assertion above would pass just as well against a build
    // where CodeAccepted was never parsed, never routed, or never applied at all -- and would be
    // proving that nothing works rather than that the queue was emptied.
    [Fact]
    public void TheSameAnswerDoesRegisterTheSessionItWasAnswering()
    {
        var host = Hosting(out var transport);

        transport.Deliver(WireEnvelope.ForCodeAccepted(host.Host.Code!.Value));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Equal(HostingPhase.Hosting, host.Host.Phase);
    }

    // Fails if: Grace.Reset() is removed from the stop path.
    //
    // A host that loses its connection starts a grace window rather than ending (R-1.4). If the DM
    // then ends the session deliberately, that window is about a session nobody is in -- and it is
    // read by InAJoinedSession, which is what the window uses to decide whether this client is
    // still in something. A stale one says yes.
    [Fact]
    public void EndingTheSessionStopsAGraceWindowTheLostConnectionStarted()
    {
        var host = Hosting(out _);

        // R-1.4's window is for losing a session that was RUNNING, so the relay has to have
        // accepted the code first. Registering is a different failure and takes a different path.
        host.Host.Registered();
        host.Fail(SessionFailure.ConnectionLost);
        Assert.True(host.Grace.IsRunning, "arrangement failed: the lost connection did not start a window");

        host.StopHosting();

        Assert.False(host.Grace.IsRunning);
    }

    // Fails if: _handshake.ForgetHostRegistration() is removed from the stop path.
    //
    // THIS IS THE ONE I EXPECTED TO BE MERELY DEFENSIVE AND IT IS NOT. The registration receipt is
    // a single field -- "did our code request leave?" -- and BUG-38 exists because the answer
    // separates two failures a DM must be told apart: THE RELAY HEARD US AND SAID NOTHING, versus
    // WE NEVER REACHED THE RELAY.
    //
    // Not forgetting it leaks that receipt across the session boundary. The next session mints a
    // fresh code, so registration is re-attempted and re-sending is unaffected -- which is why this
    // looks harmless. But if the next session cannot reach the relay AT ALL, the request never
    // leaves, the stale receipt is still non-null, and the timeout reports RegistrationNotAnswered:
    // the DM is told the relay ignored them when nothing was ever sent.
    //
    // This is the first test to reach either of those classifications through the coordinator --
    // HostSessionTests covers them on the state machine alone.
    [Fact]
    public void TheNextSessionIsNotToldTheRelayAnsweredWhenNothingWasEverSent()
    {
        var host = Hosting(out var transport);

        // Session one reached the relay and its code request went out.
        Assert.NotEmpty(transport.Sent);

        host.StopHosting();

        // Session two cannot reach the relay at all.
        transport.RefuseToConnect();
        host.StartHosting();
        host.Tick(TimeSpan.Zero, Now);
        host.Tick(HostSession.RegistrationTimeout + TimeSpan.FromSeconds(1), Now);

        Assert.Equal(SessionFailure.ConnectionNeverOpened, host.Host.Failure);
    }

    private static SessionCoordinator Hosting(out ConnectableTransport transport)
    {
        transport = new ConnectableTransport();
        var host = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, log: new SilentLog());

        host.StartHosting();
        host.SynchroniseTransport();
        host.Tick(TimeSpan.Zero, Now);
        return host;
    }

    private sealed class SilentLog : ISessionTransportLog
    {
        public void Information(string message)
        {
        }

        public void Warning(string message)
        {
        }

        public void Warning(Exception exception, string message)
        {
        }
    }

    /// <summary>A transport that can be told the relay is unreachable, which is BUG-38's case.</summary>
    private sealed class ConnectableTransport : ISessionTransport
    {
        private bool _reachable = true;

        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received;

        public void RefuseToConnect()
        {
            _reachable = false;
            IsConnected = false;
        }

        public void Connect(Uri relay) => IsConnected = _reachable;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));
    }
}
