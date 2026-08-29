using System;
using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.16 / R-1.3g: the DM's closing notice, sealed to each participant, carrying when it stops.
/// </summary>
/// <remarks>
/// <b>Driven through <see cref="RosterBroadcast"/> directly rather than the coordinator</b>, because
/// the <c>StopHosting</c> wiring is deliberately not built yet — the window it would announce is a
/// product question still with the Spec Owner. Testing the sender now means the wiring lands against
/// something already demonstrated rather than both arriving unexamined.
/// </remarks>
public class TheClosingNoticeReachesParticipantsTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 1, 0, 0, TimeSpan.Zero);
    private static readonly string PeerCode = SpeakableAlphabet.Characters[^SessionCode.Length..];

    private static (RosterBroadcast Broadcast, FakeTransport Transport, SessionKeyExchange HostKeys, SessionAudience Audience)
        Hosting()
    {
        var transport = new FakeTransport();
        var link = new RelayLink(transport, () => RelayEndpoint.Default, _ => { });
        link.Synchronise(true);

        var audience = new SessionAudience();
        var hostKeys = new SessionKeyExchange();

        return (
            new RosterBroadcast(link, audience, () => hostKeys, () => Code, SilentLog.Instance),
            transport,
            hostKeys,
            audience);
    }

    private static SessionContent OpenAs(SessionKeyExchange peer, SessionKeyExchange hostKeys, FakeTransport transport)
    {
        Assert.True(EnvelopeCodec.TryDecode(Assert.Single(transport.Sent), out var envelope));
        var plaintext = SessionCipher.Open(
            peer.DeriveSharedKey(hostKeys.PublicKey, Code),
            envelope!.TryGetSealedPayload()!,
            envelope.AssociatedData());

        Assert.True(SessionContentCodec.TryDecode(plaintext, out var content));
        return content!;
    }

    // A-1.16's whole point: the notice carries WHEN it stops, so a participant can see the wait is
    // bounded. "Closing" with no remaining time is the indefinite wait R-1.3c and R-1.8 forbid.
    [Fact]
    public void TheNoticeCarriesWhenTheSessionStops()
    {
        var (broadcast, transport, hostKeys, audience) = Hosting();
        using var joiner = new SessionKeyExchange();
        audience.Admit(PeerCodes.Of(PeerCode), publicKey: joiner.PublicKey);
        var closing = SessionClosing.DecidedByHost(Now.AddMinutes(5));

        broadcast.PublishClosing(closing);

        var content = OpenAs(joiner, hostKeys, transport);
        Assert.Equal(closing.UtcTicks, content.ClosingAtUtcTicks);
        Assert.Equal(TimeSpan.FromMinutes(5), SessionClosing.TryFromWire(content.ClosingAtUtcTicks!.Value)!.Value.RemainingAt(Now));
    }

    // Sealed per participant like the roster — a relay operator forwarding the frame learns nothing,
    // and the instant is not readable off the wire (D-11).
    [Fact]
    public void TheNoticeIsCiphertextOnTheWire()
    {
        var (broadcast, transport, _, audience) = Hosting();
        using var joiner = new SessionKeyExchange();
        audience.Admit(PeerCodes.Of(PeerCode), publicKey: joiner.PublicKey);
        var closing = SessionClosing.DecidedByHost(Now.AddMinutes(5));

        broadcast.PublishClosing(closing);

        var onTheWire = System.Text.Encoding.UTF8.GetString(Assert.Single(transport.Sent));
        Assert.DoesNotContain(closing.UtcTicks.ToString(), onTheWire, StringComparison.Ordinal);
        Assert.DoesNotContain("Closing", onTheWire, StringComparison.OrdinalIgnoreCase);
    }

    // Every participant is told, not just the first. A closing notice that reached one member would
    // leave the rest waiting on a session that has already ended — the exact silence R-1.3g removes.
    [Fact]
    public void EveryParticipantIsTold()
    {
        var (broadcast, transport, _, audience) = Hosting();
        using var first = new SessionKeyExchange();
        using var second = new SessionKeyExchange();
        audience.Admit(PeerCodes.Of(PeerCode), publicKey: first.PublicKey);
        audience.Admit(PeerCodes.Of(SpeakableAlphabet.Characters[..SessionCode.Length]), publicKey: second.PublicKey);

        broadcast.PublishClosing(SessionClosing.DecidedByHost(Now.AddMinutes(5)));

        Assert.Equal(2, transport.Sent.Count);
    }

    // The roster returns early on an empty audience because a roster of nobody says nothing. A
    // closing notice must not carry that behaviour by accident — with no participants there is
    // simply nobody to send to, and it must not throw on the way to discovering that.
    [Fact]
    public void ClosingWithNoParticipantsSendsNothingAndDoesNotThrow()
    {
        var (broadcast, transport, _, _) = Hosting();

        broadcast.PublishClosing(SessionClosing.DecidedByHost(Now.AddMinutes(5)));

        Assert.Empty(transport.Sent);
    }

    // A participant whose key will not import is SKIPPED rather than taking the notice down for
    // everyone else — the same guard the roster has, and it matters more here: the others are being
    // told the session is ending.
    [Fact]
    public void AParticipantWithAnUnusableKeyDoesNotStopTheOthersBeingTold()
    {
        var (broadcast, transport, _, audience) = Hosting();
        using var good = new SessionKeyExchange();
        audience.Admit(PeerCodes.Of("JNKBCD"), publicKey: [1, 2, 3]);
        audience.Admit(PeerCodes.Of(PeerCode), publicKey: good.PublicKey);

        broadcast.PublishClosing(SessionClosing.DecidedByHost(Now.AddMinutes(5)));

        Assert.Single(transport.Sent);
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received { add { } remove { } }

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);
    }
}
