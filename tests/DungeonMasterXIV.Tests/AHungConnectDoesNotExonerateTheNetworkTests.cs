using System;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-38. A connect that hangs — a firewall that DROPS rather than refuses — left the host in
/// <see cref="HostingPhase.Registering"/> until the clock ran out, and it was then reported as
/// <see cref="SessionFailure.RegistrationNotAnswered"/>: "the relay accepted the connection… the
/// relay is reachable, so this is not your network."
/// </summary>
/// <remarks>
/// <para>
/// <b>Both halves of that sentence are false here, and the second is the harmful one</b> — a
/// dropping firewall <i>is</i> the user's network, and the message rules it out by name. That is
/// BUG-37's class in its worst form: not merely blaming a third party, but exonerating the actual
/// cause.
/// </para>
/// <para>
/// <b>The fake holds readiness apart from connection, and that is the whole test.</b> A refused port
/// fails in about a millisecond and never reaches here; a dropped one hangs the full timeout. The
/// two are indistinguishable to any test whose fake opens instantly, which is why this one does not.
/// </para>
/// </remarks>
public class AHungConnectDoesNotExonerateTheNetworkTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 1, 0, 0, TimeSpan.Zero);

    // THE CRITERION. Fails on the shipped build, which reports a registration the relay never
    // received as one the relay received and ignored.
    [Fact]
    public void AConnectThatNeverOpensIsNotReportedAsAnUnansweredRegistration()
    {
        var host = HostingWithASocketThatNeverOpens();

        Assert.NotEqual(SessionFailure.RegistrationNotAnswered, host.Failure);
        Assert.NotEqual(SessionFailure.None, host.Failure);
    }

    // The half that reaches the person. Fails if: the sentence tells someone whose firewall is
    // dropping the connection that their network is not the problem.
    [Fact]
    public void TheMessageDoesNotRuleOutTheUsersOwnNetwork()
    {
        var host = HostingWithASocketThatNeverOpens();

        var message = SessionFailureMessage.For(host.Failure);

        Assert.NotEmpty(message);
        Assert.DoesNotContain("this is not your network", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("accepted the connection", message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("relay is reachable", message, StringComparison.OrdinalIgnoreCase);
    }

    // The ACCEPT side, and it is what stops this fix swallowing the case BUG-36 created. A socket
    // that DID open and then heard nothing is still RegistrationNotAnswered, whose sentence is
    // correct for it.
    [Fact]
    public void ASocketThatOpenedAndWasNeverAnsweredIsStillAnUnansweredRegistration()
    {
        var transport = new ControllableTransport();
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance);

        coordinator.StartHosting();
        coordinator.Tick(TimeSpan.Zero, Now);
        coordinator.Tick(HostSession.RegistrationTimeout, Now);

        Assert.Equal(SessionFailure.RegistrationNotAnswered, coordinator.Host.Failure);
    }

    private static HostSession HostingWithASocketThatNeverOpens()
    {
        var transport = new ControllableTransport { OpensImmediately = false };
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance);

        coordinator.StartHosting();
        coordinator.Tick(TimeSpan.Zero, Now);
        coordinator.Tick(HostSession.RegistrationTimeout, Now);

        return coordinator.Host;
    }

    // Separates "connected" from "able to send", which is the distinction a dropping firewall makes
    // and the one BUG-38 hid.
    private sealed class ControllableTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public bool OpensImmediately { get; init; } = true;

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend { get; private set; }

        public void Connect(Uri relay)
        {
            IsConnected = true;
            IsReadyToSend = OpensImmediately;
        }

        public void Disconnect()
        {
            IsConnected = false;
            IsReadyToSend = false;
        }

        public void Send(byte[] envelope)
        {
        }

        // Present so the interface's event is used rather than suppressed, and shaped like the
        // other transport fakes in this suite. Nothing here calls it: the whole case is a socket
        // that never opens far enough to carry a frame.
        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}
