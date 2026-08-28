using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.8 for the most common joiner error there is: a mistyped session code. BUG-43 — the relay
/// refused it correctly and promptly, and the joiner was told the relay was unreachable, fifteen
/// seconds later.
/// </summary>
/// <remarks>
/// <para>
/// <b>The fifth instance of one class in one evening</b> — a wire type with no production handler on
/// one side of a seam. BUG-36 (host never sent <c>CodeRequest</c>), BUG-40 (joiner never sent
/// <c>JoinRequest</c>), BUG-42 (host never received one), and now a <c>CodeRefused</c> the joiner
/// never handles.
/// </para>
/// <para>
/// <b>The mechanism is not simply "no arm exists".</b> <c>ApplyRegistration</c> is host-registration
/// logic, and it reported the frame HANDLED whether or not the host did anything with it — a joiner's
/// <c>CodeRefused</c> was matched by the host arm, silently discarded by
/// <c>HostSession.CodeAlreadyLive</c>'s own phase guard, and then swallowed by the <c>return true</c>.
/// So the joiner arm could not have been reached even if one had existed. The fix makes that return
/// value honest rather than widening <c>ApplyRegistration</c> to serve both sides.
/// </para>
/// <para>
/// <b>Asserted on the sentence, not only the enum.</b> BUG-37 set that bar: an enum swap that leaves
/// the prose claiming the relay is unreachable satisfies a weaker test and fixes nothing for the
/// person reading it.
/// </para>
/// </remarks>
public class TheJoinerIsToldTheCodeWasRefusedTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 23, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    // THE CRITERION. Fails on the shipped build, where the refusal is discarded and the player waits
    // out JoinAttempt.ContactTimeout before being told the wrong thing.
    [Fact]
    public void ARefusedCodeFailsTheAttemptImmediately()
    {
        var (coordinator, transport) = Joining();

        transport.Deliver(WireEnvelope.ForCodeRefused(Code));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.Failed, coordinator.Join.Phase);
        Assert.Equal(SessionFailure.SessionCodeNotActive, coordinator.Join.Failure);
    }

    // The half that reaches the person. Fails if: the enum changes and the prose still sends them to
    // check their network — which is the whole of BUG-43's cost, not the enum.
    [Fact]
    public void TheRefusalDoesNotBlameTheRelay()
    {
        var (coordinator, transport) = Joining();

        transport.Deliver(WireEnvelope.ForCodeRefused(Code));
        coordinator.Tick(TimeSpan.Zero, Now);

        var message = SessionFailureMessage.For(coordinator.Join.Failure);

        Assert.DoesNotContain("unreachable", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("not responding", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("your own network", message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("code", message, StringComparison.OrdinalIgnoreCase);
    }

    // It must not take the timeout to get there. Fails if: the refusal is still discarded and the
    // attempt only ends because ContactTimeout fired — which would satisfy a test that merely
    // checked the end state, and is exactly the fifteen seconds the bug is about.
    [Fact]
    public void TheAttemptEndsWithoutWaitingOutTheContactTimeout()
    {
        var (coordinator, transport) = Joining();

        transport.Deliver(WireEnvelope.ForCodeRefused(Code));
        coordinator.Tick(TimeSpan.Zero, Now);

        // No time has passed, so ExpireIfContactTimedOut cannot have been what ended this.
        Assert.False(coordinator.Join.ExpireIfContactTimedOut(TimeSpan.Zero));
        Assert.Equal(SessionFailure.SessionCodeNotActive, coordinator.Join.Failure);
    }

    private static (SessionCoordinator Coordinator, FakeTransport Transport) Joining()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance);
        coordinator.RequestJoin(Code);
        return (coordinator, transport);
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope)
        {
        }

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}
