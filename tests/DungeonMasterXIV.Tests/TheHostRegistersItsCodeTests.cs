using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.2a from the production entry point: starting a session must put a <c>CodeRequest</c> on the
/// wire. BUG-36 — it never did.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every one of the twenty-six existing references to <c>ForCodeRequest</c> is in a test, and
/// every one of them constructs the envelope itself and hands it to the relay.</b> The relay's side
/// of the arbitration is covered thoroughly and the plugin's state machine is covered thoroughly, and
/// between them sat the step that builds and sends the thing. Nothing failed, because nothing asked.
/// </para>
/// <para>
/// <b>So nothing in this file may construct a <c>CodeRequest</c>.</b> A test that built one and
/// asserted the relay accepts it passes on the broken build and is worthless here. These start at
/// <see cref="SessionCoordinator.StartHosting"/> — what the plugin calls when a DM starts a session —
/// and look at what reached the transport.
/// </para>
/// <para>
/// <b>The fake drives readiness apart from connection deliberately.</b> The real
/// <c>WebSocketSessionTransport</c> reports <c>IsConnected</c> while a connect is still in flight and
/// silently discards anything sent before the socket opens. A fix that sent on the return from
/// <c>Connect</c> would pass a test whose fake conflated the two, and fail in the product exactly as
/// BUG-36 did — so <see cref="ARegistrationSentBeforeTheSocketOpensWouldBeLostSoItWaits"/> holds the
/// two apart on purpose.
/// </para>
/// </remarks>
public class TheHostRegistersItsCodeTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 21, 0, 0, TimeSpan.Zero);

    // THE CRITERION. Fails on the shipped build, where SynchroniseTransport connects and returns and
    // nothing ever speaks. The relay holds the connection open waiting for the client, so silence
    // here is a session that never starts.
    [Fact]
    public void StartingASessionPutsACodeRequestOnTheWire()
    {
        var host = new HostUnderTest();

        host.StartsASession();

        var sent = host.Sent.Single();
        Assert.Equal(WireMessageType.CodeRequest, sent.Type);
    }

    // The request must name the code the DM is actually shown, or the relay claims one code while the
    // DM reads out another. Fails if: the envelope is built from anything but the live session code.
    [Fact]
    public void TheRequestClaimsTheCodeTheDmIsShowing()
    {
        var host = new HostUnderTest();

        host.StartsASession();

        Assert.Equal(host.Coordinator.Host.Code!.Value.Value, host.Sent.Single().SessionCode);
    }

    // The hazard the real socket has and a naive fake does not. Fails if: the request is sent on the
    // return from Connect — which looks correct, and which Send would silently discard, reproducing
    // BUG-36 with a fix in place.
    [Fact]
    public void ARegistrationSentBeforeTheSocketOpensWouldBeLostSoItWaits()
    {
        var host = new HostUnderTest();
        host.Transport.OpensImmediately = false;

        host.StartsASession();

        Assert.Empty(host.Sent);

        host.Transport.SocketOpens();
        host.Coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(WireMessageType.CodeRequest, host.Sent.Single().Type);
    }

    // Fails if: the guard is a "have we sent one" boolean. Ticking repeatedly must not re-claim a
    // code the relay is already arbitrating.
    [Fact]
    public void TheRequestIsSentOnceRatherThanEveryFrame()
    {
        var host = new HostUnderTest();
        host.StartsASession();

        host.Coordinator.Tick(TimeSpan.Zero, Now);
        host.Coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Single(host.Sent);
    }

    // The relay's half of R-1.2a, consumed. Fails if: CodeAccepted arrives and nothing applies it,
    // which leaves a registered host sitting in Registering until it times out — BUG-36's symptom
    // surviving its cause.
    [Fact]
    public void TheRelayAcceptingTheCodeCompletesRegistration()
    {
        var host = new HostUnderTest();
        host.StartsASession();

        host.RelaySays(WireEnvelope.ForCodeAccepted(host.Coordinator.Host.Code!.Value));

        Assert.Equal(HostingPhase.Hosting, host.Coordinator.Host.Phase);
        Assert.Equal(SessionFailure.None, host.Coordinator.Host.Failure);
    }

    // R-1.2a: the host proposes, the relay arbitrates, a refusal means regenerate and ask again.
    // Fails if: a refusal is surfaced to the DM, who did not choose the code, or if the replacement
    // is never claimed because the send is guarded by a boolean rather than by which code is held.
    [Fact]
    public void ARefusedCodeIsRegeneratedAndClaimedAgainWithoutTellingTheDm()
    {
        var host = new HostUnderTest();
        host.StartsASession();
        var refused = host.Coordinator.Host.Code!.Value.Value;

        host.RelaySays(WireEnvelope.ForCodeRefused(host.Coordinator.Host.Code!.Value));

        Assert.Equal(HostingPhase.Registering, host.Coordinator.Host.Phase);
        Assert.Equal(SessionFailure.None, host.Coordinator.Host.Failure);

        var replacement = host.Coordinator.Host.Code!.Value.Value;
        Assert.NotEqual(refused, replacement);

        var claims = host.Sent.Where(e => e.Type == WireMessageType.CodeRequest).ToList();
        Assert.Equal(2, claims.Count);
        Assert.Equal(replacement, claims[1].SessionCode);
    }

    // A-1.5b, and the second half of BUG-36. Fails if: a registration timeout still reports
    // RelayUnreachable — a relay that answered, upgraded the connection and held it open was
    // reached, and saying otherwise sends the reader to check DNS and certificates.
    [Fact]
    public void ARegistrationThatIsNeverAnsweredDoesNotBlameTheRelaysReachability()
    {
        var host = new HostUnderTest();
        host.StartsASession();

        host.Coordinator.Tick(TimeSpan.Zero, Now);
        host.Coordinator.Tick(HostSession.RegistrationTimeout, Now);

        Assert.Equal(SessionFailure.RegistrationNotAnswered, host.Coordinator.Host.Failure);
        Assert.NotEqual(SessionFailure.RelayUnreachable, host.Coordinator.Host.Failure);

        var told = SessionFailureMessage.For(host.Coordinator.Host.Failure);
        Assert.NotEmpty(told);
        Assert.DoesNotContain("unreachable", told, StringComparison.OrdinalIgnoreCase);
    }

    // Starts where the plugin starts and reads only what left the machine.
    private sealed class HostUnderTest
    {
        public HostUnderTest() =>
            Coordinator = new SessionCoordinator(Transport, () => RelayEndpoint.Default);

        public SessionCoordinator Coordinator { get; }

        public ControllableTransport Transport { get; } = new();

        public IReadOnlyList<WireEnvelope> Sent => Transport.Sent
            .Select(bytes => EnvelopeCodec.TryDecode(bytes, out var e) ? e : null)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        /// <summary>What the plugin does when a DM presses the button, plus one frame.</summary>
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

    // Separates "connected" from "able to send", which is the distinction the real socket makes and
    // the one BUG-36 hid.
    private sealed class ControllableTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public List<byte[]> Sent { get; } = new();

        public bool OpensImmediately { get; set; } = true;

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend { get; private set; }

        public void SocketOpens() => IsReadyToSend = IsConnected;

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));

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

        // Mirrors the real transport: a frame sent before the socket is open is discarded, silently.
        public void Send(byte[] envelope)
        {
            if (!IsReadyToSend)
            {
                return;
            }

            Sent.Add(envelope);
        }

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}
