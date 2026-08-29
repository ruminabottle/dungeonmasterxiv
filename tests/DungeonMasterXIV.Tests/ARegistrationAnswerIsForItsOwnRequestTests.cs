using System;
using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-89: the relay's answer to a code request is applied only if it names the code that was asked.
/// </summary>
/// <remarks>
/// <para>
/// <b><c>ApplyRegistration</c> checked only that this client was <c>Registering</c>.</b> It never
/// compared the code, though <c>ForCodeAccepted</c> puts one on the wire — so an answer left queued
/// from an earlier request would be applied to a later one, registering a new session under the
/// relay's answer about an old code.
/// </para>
/// <para>
/// <b>What was preventing it was <c>_inbox.Clear()</c> in <c>StopHosting</c>, and nothing else</b> —
/// a guard in one method protecting an unchecked assumption in another, which is a coupling nobody
/// declared. feature-engineer-3 deleted each of <c>StopHosting</c>'s five teardown steps in turn and
/// three left the suite green, that line among them. The mitigation is now tested; this pins the
/// defect instead.
/// </para>
/// <para>
/// <b>The stale answer is delivered while the host is registering a DIFFERENT code, rather than
/// across a teardown.</b> That reaches the same defect without depending on <c>StopHosting</c>
/// running first — which is the point, since the coupling is exactly what should not be relied on.
/// It is also the shape BUG-90 says is reachable by another path.
/// </para>
/// <para>
/// <b>Both arms, because both had the gap.</b> A stale <c>CodeRefused</c> is worse than useless: it
/// makes the host abandon a perfectly good code and regenerate, on the strength of an answer about a
/// code it is no longer asking about.
/// </para>
/// </remarks>
public class ARegistrationAnswerIsForItsOwnRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 6, 0, 0, TimeSpan.Zero);

    /// <summary>A valid code that is not the one the host will generate for itself.</summary>
    private static readonly SessionCode SomebodyElsesCode = SessionCode.FromValid("BKD7RM");

    // THE PRECONDITION, asserted rather than assumed. If the host were not Registering, every
    // negative below would pass because the phase guard refused the frame -- not because the code
    // was compared. That is the shape that made the first probe on BUG-85 worthless.
    [Fact]
    public void TheHostIsRegisteringADifferentCodeBeforeAnyAnswerArrives()
    {
        var host = new HostUnderTest();
        host.StartsASession();

        Assert.Equal(HostingPhase.Registering, host.Coordinator.Host.Phase);
        Assert.NotNull(host.Coordinator.Host.Code);
        Assert.NotEqual(SomebodyElsesCode, host.Coordinator.Host.Code!.Value);
    }

    // THE DEFECT. Fails if: an acceptance naming a code this host is not asking about completes its
    // registration -- a new session wearing an old session's code.
    [Fact]
    public void AnAcceptanceForAnotherCodeDoesNotCompleteRegistration()
    {
        var host = new HostUnderTest();
        host.StartsASession();
        var outstanding = host.Coordinator.Host.Code!.Value;

        host.RelaySays(WireEnvelope.ForCodeAccepted(SomebodyElsesCode));

        Assert.Equal(HostingPhase.Registering, host.Coordinator.Host.Phase);
        Assert.Equal(outstanding, host.Coordinator.Host.Code!.Value);
    }

    // THE SIBLING ARM, which had the identical gap. A stale refusal is worse than useless: it makes
    // the host abandon a code nobody refused, on the strength of an answer about a different one.
    [Fact]
    public void ARefusalForAnotherCodeDoesNotRegenerate()
    {
        var host = new HostUnderTest();
        host.StartsASession();
        var outstanding = host.Coordinator.Host.Code!.Value;

        host.RelaySays(WireEnvelope.ForCodeRefused(SomebodyElsesCode));

        Assert.Equal(outstanding, host.Coordinator.Host.Code!.Value);
        Assert.Equal(HostingPhase.Registering, host.Coordinator.Host.Phase);
    }

    // THE OTHER DIRECTION, and the half a compare-everything guard would break: the answer to the
    // request this host actually made must still register it.
    [Fact]
    public void TheOutstandingCodesAcceptanceStillRegisters()
    {
        var host = new HostUnderTest();
        host.StartsASession();

        host.RelaySays(WireEnvelope.ForCodeAccepted(host.Coordinator.Host.Code!.Value));

        Assert.Equal(HostingPhase.Hosting, host.Coordinator.Host.Phase);
    }

    // And a refusal of the outstanding code must still regenerate, or R-1.2a's retry is broken.
    [Fact]
    public void TheOutstandingCodesRefusalStillRegenerates()
    {
        var host = new HostUnderTest();
        host.StartsASession();
        var refused = host.Coordinator.Host.Code!.Value;

        host.RelaySays(WireEnvelope.ForCodeRefused(refused));

        Assert.NotEqual(refused, host.Coordinator.Host.Code!.Value);
        Assert.Equal(HostingPhase.Registering, host.Coordinator.Host.Phase);
    }

    private sealed class HostUnderTest
    {
        public HostUnderTest() =>
            Coordinator = new SessionCoordinator(
                Transport,
                () => RelayEndpoint.Default,
                GraceWindow.Default,
                log: SilentLog.Instance,
                capabilities: SessionCapabilities.Default);

        public SessionCoordinator Coordinator { get; }

        public FakeTransport Transport { get; } = new();

        public void StartsASession()
        {
            Coordinator.StartHosting();
            Coordinator.Tick(TimeSpan.Zero, Now);
        }

        public void RelaySays(WireEnvelope envelope)
        {
            Transport.Deliver(envelope);
            Coordinator.Tick(TimeSpan.Zero, Now);
        }
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = [];

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
