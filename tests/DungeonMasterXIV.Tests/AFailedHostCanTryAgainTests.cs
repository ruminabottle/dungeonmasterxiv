using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A DM whose hosting attempt failed can actually start another one (DMXENG-68, BUG-120).
/// </summary>
/// <remarks>
/// <para>
/// <b>The sibling of <c>AFailedAttemptStillOffersTheAction</c>, and the half that was missing.</b>
/// That test pins the start-session control being OFFERED after a failure — which is why the
/// hosting-exclusivity guard treats <c>Failed</c> as "not hosting". This one pins that pressing it
/// WORKS. They are two halves of one requirement and neither implies the other: an action can be
/// offered and do nothing, which is a worse outcome than hiding it, because the DM now has evidence
/// they tried.
/// </para>
/// <para>
/// <b>Measured, and this is why the ticket exists:</b> refusing to restart after a failure —
/// <c>if (Phase == HostingPhase.Failed) { return; }</c> at the top of <c>HostSession.Start</c> —
/// left all 1489 tests passing. A bool was pinned and a recovery was not.
/// </para>
/// <para>
/// Driven through <c>SessionCoordinator</c> rather than by calling <c>HostSession</c> directly,
/// because the thing under test is what a DM pressing the button gets, and the button goes through
/// the coordinator.
/// </para>
/// </remarks>
public class AFailedHostCanTryAgainTests
{
    // THE RECOVERY ITSELF. Reaches Failed the way a DM does, then starts again and requires the new
    // attempt to be genuinely under way.
    [Fact]
    public void AnAttemptAfterAFailureReachesRegistering()
    {
        var coordinator = Coordinator();

        coordinator.StartHosting();
        coordinator.Fail(SessionFailure.RelayUnreachable);
        Assert.Equal(HostingPhase.Failed, coordinator.Host.Phase);

        coordinator.StartHosting();

        Assert.Equal(HostingPhase.Registering, coordinator.Host.Phase);
    }

    // A recovery that still reports the old failure has not recovered — the DM is looking at a live
    // attempt and a reason it did not work. Separate from the phase because they can disagree: the
    // phase is what the code does next, the failure is what the DM is told.
    [Fact]
    public void TheOldFailureIsNotStillShowingOnTheNewAttempt()
    {
        var coordinator = Coordinator();

        coordinator.StartHosting();
        coordinator.Fail(SessionFailure.RelayUnreachable);
        Assert.Equal(SessionFailure.RelayUnreachable, coordinator.Host.Failure);

        coordinator.StartHosting();

        Assert.Equal(SessionFailure.None, coordinator.Host.Failure);
    }

    // THE CONTROL, and without it the two above are satisfied by a coordinator that never fails at
    // all. This proves the rig can actually reach Failed, so "it recovered" is a recovery rather
    // than a state the host never left.
    [Fact]
    public void TheRigCanActuallyReachFailed()
    {
        var coordinator = Coordinator();

        coordinator.StartHosting();
        Assert.Equal(HostingPhase.Registering, coordinator.Host.Phase);

        coordinator.Fail(SessionFailure.RelayUnreachable);

        Assert.Equal(HostingPhase.Failed, coordinator.Host.Phase);
    }

    private static SessionCoordinator Coordinator() =>
        new(new SilentTransport(), () => RelayEndpoint.Default, GraceWindow.Default,
            log: SilentLog.Instance, capabilities: SessionCapabilities.Default);

    private sealed class SilentTransport : ISessionTransport
    {
        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) { _ = Failed; _ = Received; }
    }
}
