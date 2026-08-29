using System;
using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.16 / R-1.3g: the DM's closing notice, sealed to each participant, carrying when it stops.
/// </summary>
/// <remarks>
/// <para>
/// <b>Driven through <see cref="RosterBroadcast"/> directly rather than the coordinator</b>, so the
/// sender is demonstrated on its own. It was written while the <c>StopHosting</c> wiring did not
/// exist and the window was still a product question; SQ-63 settled the window at sixty seconds and
/// the wiring has since landed.
/// </para>
/// <para>
/// <b>AND EVERY TEST HERE PASSED WITH THAT WIRING DELETED.</b> Not a criticism of these tests — they
/// are about the sender and they are right about it — but a statement of what they cannot reach:
/// <b>a test at this layer cannot fail on a call site that does not exist.</b> Removing
/// <c>PublishClosing</c> from <c>StopHosting</c>, and separately moving it after teardown so it
/// seals to an emptied audience, both left the whole suite green.
/// <see cref="EndingASessionAnnouncesItTests"/> is the coordinator-level file that fails on those,
/// and it exists because of that measurement rather than in anticipation of it.
/// </para>
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
            // OwnPeerCode is null because this fixture publishes CLOSING NOTICES, which carry no
            // roster. Supplying a peer code here would mean inventing one, which is the thing
            // HostIdentity's own doc refuses for production; the host's roster entry is
            // TheHostIsInItsOwnRosterTests' subject, not this file's.
            new RosterBroadcast(
                link,
                audience,
                new HostIdentity(() => hostKeys, () => Code, () => DisplayName.None, () => null),
                SilentLog.Instance),
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
            envelope!.AssociatedData());

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
        var closing = SessionClosing.DecidedByHost(Now);

        broadcast.PublishClosing(closing);

        var content = OpenAs(joiner, hostKeys, transport);
        Assert.Equal(closing.UtcTicks, content.ClosingAtUtcTicks);
        Assert.Equal(SessionClosing.Window, SessionClosing.TryFromWire(content.ClosingAtUtcTicks!.Value)!.Value.RemainingAt(Now));
    }

    // Sealed per participant like the roster — a relay operator forwarding the frame learns nothing,
    // and the instant is not readable off the wire (D-11).
    [Fact]
    public void TheNoticeIsCiphertextOnTheWire()
    {
        var (broadcast, transport, _, audience) = Hosting();
        using var joiner = new SessionKeyExchange();
        audience.Admit(PeerCodes.Of(PeerCode), publicKey: joiner.PublicKey);
        var closing = SessionClosing.DecidedByHost(Now);

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

        broadcast.PublishClosing(SessionClosing.DecidedByHost(Now));

        Assert.Equal(2, transport.Sent.Count);
    }

    // The roster returns early on an empty audience because a roster of nobody says nothing. A
    // closing notice must not carry that behaviour by accident — with no participants there is
    // simply nobody to send to, and it must not throw on the way to discovering that.
    [Fact]
    public void ClosingWithNoParticipantsSendsNothingAndDoesNotThrow()
    {
        var (broadcast, transport, _, _) = Hosting();

        broadcast.PublishClosing(SessionClosing.DecidedByHost(Now));

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

        broadcast.PublishClosing(SessionClosing.DecidedByHost(Now));

        Assert.Single(transport.Sent);
    }

    // >>> A-1.16b. THE CRITERION THAT EXISTS BECAUSE FIXING THE VALUE REMOVED A GUARD.
    //
    // Sixty seconds is now a constant. So a build with a sixty-second constant on the HOST and a
    // sixty-second constant on EACH CLIENT displays the right countdown everywhere and PASSES A-1.16
    // WHILE SENDING NOTHING AT ALL — and then drifts, because the two clocks start at different
    // instants. A CONFIGURABLE window had to travel in order to be observed, so the criterion would
    // have policed the mechanism by accident. A constant does not, so this test does it deliberately.
    //
    // The demonstration is that the participant's countdown FOLLOWS the host's rather than agreeing
    // with it: two sessions ended a minute apart must produce deadlines a minute apart on the wire.
    // A client computing sixty seconds locally produces the SAME remaining time for both.
    [Fact]
    public void EveryParticipantsCountdownFollowsTheHostsRatherThanAgreeingByCoincidence()
    {
        var (broadcast, transport, hostKeys, audience) = Hosting();
        using var joiner = new SessionKeyExchange();
        audience.Admit(PeerCodes.Of(PeerCode), publicKey: joiner.PublicKey);

        broadcast.PublishClosing(SessionClosing.DecidedByHost(Now));
        var early = Received(joiner, hostKeys, transport, 0);

        broadcast.PublishClosing(SessionClosing.DecidedByHost(Now.AddMinutes(1)));
        var late = Received(joiner, hostKeys, transport, 1);

        // The deadlines differ by exactly the difference in when the host ended. A locally computed
        // sixty seconds would make these identical.
        Assert.Equal(TimeSpan.FromMinutes(1), late.Instant - early.Instant);

        // And read at ONE instant, the two give different remaining times — which is what a
        // participant would actually see, and what a local constant cannot reproduce.
        Assert.NotEqual(early.RemainingAt(Now), late.RemainingAt(Now));
    }

    /// <summary>The closing instant as it arrived, opened from the nth frame this host sent.</summary>
    private static SessionClosing Received(
        SessionKeyExchange peer, SessionKeyExchange hostKeys, FakeTransport transport, int index)
    {
        Assert.True(EnvelopeCodec.TryDecode(transport.Sent[index], out var envelope));
        var plaintext = SessionCipher.Open(
            peer.DeriveSharedKey(hostKeys.PublicKey, Code),
            envelope!.TryGetSealedPayload()!,
            envelope!.AssociatedData());

        Assert.True(SessionContentCodec.TryDecode(plaintext, out var content));

        // Rebuilt through TryFromWire, never constructed here — a participant reads the instant it
        // was SENT. Constructing one on this side would be the very defect A-1.16b catches.
        return SessionClosing.TryFromWire(content!.ClosingAtUtcTicks!.Value)!.Value;
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
