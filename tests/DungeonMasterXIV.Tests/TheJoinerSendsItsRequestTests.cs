using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.3 from the production entry point: asking to join must put a <c>JoinRequest</c> on the wire.
/// BUG-40 — it never did, so no player could join any session.
/// </summary>
/// <remarks>
/// <para>
/// <b>BUG-36's exact twin, one message along.</b> That was the host connecting and never sending
/// <c>ForCodeRequest</c>; this is the joiner connecting and never sending <c>ForJoinRequest</c>. The
/// host half was found and fixed and nobody asked the same question of the other side, which is why
/// <see cref="TheHostRegistersItsCodeTests"/> is this file's model rather than its neighbour.
/// </para>
/// <para>
/// <b>Nothing in this file may construct a <c>JoinRequest</c>.</b> A test that built one and handed
/// it to a relay passes on the broken build and proves nothing — that is exactly how
/// <c>JoinOverASocketTests.AJoinCompletesAcrossARealSocket</c> stayed green over this: it scripts the
/// server side and never asserts the client transmitted anything. These start at
/// <see cref="SessionCoordinator.RequestJoin"/> — what the plugin calls when a player presses the
/// button — and read only what reached the transport.
/// </para>
/// <para>
/// <b>The fake holds readiness apart from connection deliberately.</b> The real
/// <c>WebSocketSessionTransport</c> reports <c>IsConnected</c> while a connect is still in flight and
/// silently discards anything sent before the socket opens, so a fix that sent on the return from
/// <c>RequestJoin</c> would pass against a fake that conflated the two and fail in the product —
/// reproducing BUG-40 with a fix in place. See
/// <see cref="ARequestSentBeforeTheSocketOpensWouldBeLostSoItWaits"/>.
/// </para>
/// </remarks>
public class TheJoinerSendsItsRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 22, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    // THE CRITERION. Fails on the shipped build, where RequestJoin sets local state, opens a socket
    // and returns. The relay holds the connection open waiting for the client to speak, so silence
    // here is a player who is never seen by the DM and is told ten seconds later that the relay is
    // unreachable.
    [Fact]
    public void RequestingToJoinPutsAJoinRequestOnTheWire()
    {
        var joiner = new JoinerUnderTest();

        joiner.AsksToJoin(Code);

        var sent = joiner.Sent.Single();
        Assert.Equal(WireMessageType.JoinRequest, sent.Type);
    }

    // The request must name the code the player typed, or the relay routes the request into a
    // different session than the one they were invited to.
    [Fact]
    public void TheRequestNamesTheCodeThePlayerEntered()
    {
        var joiner = new JoinerUnderTest();

        joiner.AsksToJoin(Code);

        Assert.Equal(Code.Value, joiner.Sent.Single().SessionCode);
    }

    // D-11. Fails if: the request carries no key, which leaves the DM unable to compute a
    // fingerprint and the admitted player unable to derive the session key — routed and permanently
    // unable to read anything, which reads as an encryption bug rather than a missing field.
    [Fact]
    public void TheRequestCarriesTheJoinersOwnPublicKey()
    {
        var joiner = new JoinerUnderTest();

        joiner.AsksToJoin(Code);

        Assert.Equal(joiner.Coordinator.JoinerKeys!.PublicKey, joiner.Sent.Single().PublicKey);
    }

    // The hazard the real socket has and a naive fake does not. Fails if: the request is sent from
    // inside RequestJoin — which looks correct, and which Send silently discards, reproducing BUG-40
    // with a fix in place. BUG-36 already paid for this lesson on the host side.
    [Fact]
    public void ARequestSentBeforeTheSocketOpensWouldBeLostSoItWaits()
    {
        var joiner = new JoinerUnderTest();
        joiner.Transport.OpensImmediately = false;

        joiner.AsksToJoin(Code);

        Assert.Empty(joiner.Sent);

        joiner.Transport.SocketOpens();
        joiner.Coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(WireMessageType.JoinRequest, joiner.Sent.Single().Type);
    }

    // Fails if: the send is unguarded and fires every frame, which would post a fresh request to the
    // DM sixty times a second and turn one player into a wall of prompts.
    [Fact]
    public void TheRequestIsSentOnceRatherThanEveryFrame()
    {
        var joiner = new JoinerUnderTest();
        joiner.AsksToJoin(Code);

        joiner.Coordinator.Tick(TimeSpan.Zero, Now);
        joiner.Coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Single(joiner.Sent);
    }

    // R-1.3c allows asking again after a lapse, and the same code is the ordinary case — the DM was
    // mid-encounter, not absent. Fails if: the guard is "which code did we request", which would be
    // satisfied by the previous attempt and silently send nothing the second time.
    [Fact]
    public void AskingAgainAfterALapseSendsAnotherRequest()
    {
        var joiner = new JoinerUnderTest();
        joiner.AsksToJoin(Code);
        joiner.Coordinator.Join.Lapsed();

        joiner.AsksToJoin(Code);

        Assert.Equal(2, joiner.Sent.Count(e => e.Type == WireMessageType.JoinRequest));
    }

    // Starts where the plugin starts and reads only what left the machine.
    private sealed class JoinerUnderTest
    {
        public JoinerUnderTest() =>
            Coordinator = new SessionCoordinator(Transport, () => RelayEndpoint.Default, GraceWindow.Default);

        public SessionCoordinator Coordinator { get; }

        public ControllableTransport Transport { get; } = new();

        public IReadOnlyList<WireEnvelope> Sent => Transport.Sent
            .Select(bytes => EnvelopeCodec.TryDecode(bytes, out var e) ? e : null)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

        /// <summary>What the plugin does when a player presses the button, plus one frame.</summary>
        public void AsksToJoin(SessionCode code)
        {
            Coordinator.RequestJoin(code);
            Coordinator.Tick(TimeSpan.Zero, Now);
        }
    }

    // Separates "connected" from "able to send", which is the distinction the real socket makes and
    // the one BUG-40 hid — the same fake shape TheHostRegistersItsCodeTests needed for BUG-36.
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
