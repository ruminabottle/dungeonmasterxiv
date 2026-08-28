using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.3f's transport: the first content this product has ever sent, and it is sealed (D-11).
/// </summary>
/// <remarks>
/// <para>
/// <b>Until this chunk, <see cref="SessionCipher"/> and <see cref="WireEnvelope.ForSessionPayload"/>
/// had no production caller at all</b> — the relay routed a payload nobody sent. So these are not
/// regression tests for a feature that drifted; they are the first evidence the channel works.
/// </para>
/// <para>
/// <b>Every assertion opens the payload rather than trusting that one was sent.</b> A test that
/// counted <c>SessionPayload</c> envelopes would pass against a host sealing an empty document, or
/// the wrong document, or one sealed with a key nobody holds. What matters is what a participant can
/// actually read.
/// </para>
/// </remarks>
public class TheRosterTravelsSealedTests
{
    /// <summary>
    /// A peer code of the shape the product actually produces.
    /// </summary>
    /// <remarks>
    /// <b>Derived, not typed.</b> These fixtures used <c>"PEER-1"</c>, which
    /// <c>AdmissionControl.PeerCodeFor</c> can never emit — <c>E</c>, <c>-</c> and <c>1</c> are not
    /// in <see cref="SpeakableAlphabet.Characters"/>. That was invisible while nothing checked, and
    /// BUG-57 added the check. Built from the same two constants the codec validates against, so it
    /// cannot become impossible again if the alphabet or the length ever moves.
    /// <para>
    /// <b>The TAIL of the alphabet, not the head, and that is load-bearing.</b> The head is
    /// <c>"BCDFGH"</c>, which is also the session code these fixtures use — and a session code
    /// travels in the CLEAR, because the relay has to read it to route. A peer code equal to it
    /// makes "the roster is ciphertext" fail for a reason that has nothing to do with the roster.
    /// </para>
    /// </remarks>
    private static readonly string PeerCode = SpeakableAlphabet.Characters[^SessionCode.Length..];

    private static readonly DateTimeOffset Now = new(2026, 8, 28, 4, 0, 0, TimeSpan.Zero);

    // The channel, end to end on the host side: the roster is sealed to the participant's own key
    // and carries what the roster is for.
    [Fact]
    public void TheHostSealsTheRosterToTheParticipantItIsFor()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest(PeerCode, joiner.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));

        coordinator.Admit(PeerCode);

        var content = OpenAs(joiner, coordinator, transport);
        var entry = Assert.Single(content.Roster!);
        Assert.Equal(PeerCode, entry.PeerCode);
        Assert.Equal("Ysera", entry.DisplayName);
    }

    // Fails if: the roster goes out in the clear. The published service policy says "everything you
    // say inside a session is sealed and the relay cannot open it. The joining name is the
    // exception, it is the only one" — so a plaintext roster would make shipped copy false, which is
    // a D-8 false-copy defect rather than a hardening preference.
    [Fact]
    public void TheRosterIsNotReadableWithoutTheKey()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest(PeerCode, joiner.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));

        coordinator.Admit(PeerCode);

        var payload = Payloads(transport).Single();
        Assert.DoesNotContain("Ysera", System.Text.Encoding.UTF8.GetString(payload.Payload!));
        Assert.DoesNotContain(PeerCode, System.Text.Encoding.UTF8.GetString(payload.Payload!));

        // And the seal is a real one: a stranger holding the session code still cannot open it.
        // ThrowsAny, not Throws: .NET raises AuthenticationTagMismatchException, a SUBCLASS of
        // CryptographicException. Worth stating because the production catches are written on the
        // base type — an exact-match catch would have let a tag mismatch escape as an unhandled
        // exception on the receive path, where ordinary traffic produces one constantly.
        using var stranger = new SessionKeyExchange();
        Assert.ThrowsAny<CryptographicException>(() => SessionCipher.Open(
            stranger.DeriveSharedKey(coordinator.HostKeys!.PublicKey, coordinator.Host.Code!.Value),
            payload.TryGetSealedPayload()!,
            payload.AssociatedData()));
    }

    // A-1.13a's host half. The push is driven by the ADMISSION, not by the membership changing, so a
    // client re-admitted after a reconnect is sent the CURRENT roster rather than nothing. Admitting
    // an already-admitted peer adds no entry — so a push keyed on "did the roster change" would send
    // nothing here, and the reconnecting client would sit looking at an empty list.
    [Fact]
    public void ReAdmittingAnExistingParticipantPublishesTheRosterAgain()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest(PeerCode, joiner.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));
        coordinator.Admit(PeerCode);
        var before = Payloads(transport).Count;

        coordinator.Admit(PeerCode);

        Assert.Single(coordinator.Audience.Recipients);
        Assert.True(Payloads(transport).Count > before, "Re-admitting sent no roster, so a reconnecting client would see nothing.");
    }

    // The T-30 lesson applied to my own change: a value carried by a defaulted parameter is a value
    // nothing proves arrived. The key and the name exist only on the request being answered, so this
    // is what stops AdmittedPeer quietly holding nulls.
    [Fact]
    public void AnAdmittedPeerKeepsTheKeyAndNameTheyArrivedWith()
    {
        var (coordinator, _) = Hosting();
        using var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest(PeerCode, joiner.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));

        var peer = coordinator.Admit(PeerCode);

        Assert.Equal(joiner.PublicKey, peer.PublicKey);
        Assert.Equal("Ysera", peer.DisplayName.Value);
    }

    // A joiner controls the bytes in its public key and NOTHING VALIDATES THEM as a well-formed SPKI
    // blob. Before the guard, admitting such a peer threw out of Admit — so any stranger could crash
    // the DM's admission by sending rubbish, through the one path that is open to strangers by
    // design. The session must keep serving everyone else.
    [Fact]
    public void AParticipantWithAnUnusableKeyCannotBreakTheBroadcast()
    {
        var (coordinator, transport) = Hosting();
        using var good = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest("PEER-JUNK", [1, 2, 3], Now, displayName: DisplayName.OrNone("Mallory"));
        coordinator.ReceiveJoinRequest(PeerCode, good.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));

        coordinator.Admit("PEER-JUNK");
        coordinator.Admit(PeerCode);

        // The good participant is still reachable, and the junk one is admitted but unaddressable.
        var content = OpenAs(good, coordinator, transport);
        Assert.Contains(content.Roster!, e => e.PeerCode == PeerCode);
        Assert.Equal(2, coordinator.Audience.Recipients.Count);
    }

    // THE RECEIVE ARM, and it is here because I added one. AdmissionInbox had NO arm for
    // SessionPayload — a payload arriving fell through to nothing, which is precisely the shape that
    // cost BUG-42 an entire feature: the consumer existed, was well tested, and nothing routed to it.
    // A host-side test proving the roster is SENT would pass against a client that drops it.
    [Fact]
    public void AnAdmittedPlayerAppliesTheRosterItReceives()
    {
        var (player, transport) = Joining(out var hostKeys, out var code);

        transport.Deliver(Sealed(hostKeys, player, code));
        player.Tick(TimeSpan.Zero, Now);

        var entry = Assert.Single(player.Roster);
        Assert.Equal(PeerCode, entry.PeerCode);
        Assert.Equal("Ysera", entry.DisplayName);
    }

    // The differential. A payload sealed for somebody else is ORDINARY traffic — keys are pairwise,
    // so the relay forwards every copy to every member and a client constantly receives payloads it
    // cannot open. Applying one would mean the client believed a roster it could not read; failing
    // loudly on one would make normal operation look like an attack.
    [Fact]
    public void APayloadSealedForSomebodyElseIsIgnoredInSilence()
    {
        var (player, transport) = Joining(out _, out var code);
        using var somebodyElse = new SessionKeyExchange();

        transport.Deliver(Sealed(somebodyElse, player, code));
        player.Tick(TimeSpan.Zero, Now);

        Assert.Empty(player.Roster);
    }

    /// <summary>A roster sealed by <paramref name="from"/> for <paramref name="player"/>.</summary>
    private static WireEnvelope Sealed(
        SessionKeyExchange from,
        SessionCoordinator player,
        SessionCode code,
        string name = "Ysera")
    {
        var plaintext = SessionContentCodec.Encode(new SessionContent
        {
            Roster = [new RosterEntry(PeerCode, name, SessionRole.Player)],
        });

        var sealedPayload = SessionCipher.Seal(
            from.DeriveSharedKey(player.JoinerKeys!.PublicKey, code),
            plaintext,
            WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));

        return WireEnvelope.ForSessionPayload(code, sealedPayload);
    }

    /// <summary>A coordinator that has asked to join and been admitted, so it holds a session key.</summary>
    private static (SessionCoordinator Player, FakeTransport Transport) Joining(
        out SessionKeyExchange hostKeys,
        out SessionCode code)
    {
        var transport = new FakeTransport();
        var player = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default);
        code = SessionCode.FromValid("BCDFGH");
        hostKeys = new SessionKeyExchange();

        player.RequestJoin(code, DisplayName.OrNone("Bob"));
        player.SynchroniseTransport();
        player.Tick(TimeSpan.Zero, Now);

        transport.Deliver(WireEnvelope.ForJoinAccepted(code, player.JoinerKeys!.PublicKey, hostKeys.PublicKey));
        player.Tick(TimeSpan.Zero, Now);

        return (player, transport);
    }

    // THE D-8 GATE ON THE CONTENT PATH. The seal authenticates the sender and says nothing about
    // what they sent, so a name arriving in a roster is exactly as untrusted as one arriving on a
    // join request — and the join path refuses this string. A multi-line name renders a SECOND,
    // FORGED "Code to compare" line: a name displacing the fingerprint, which the gate denies on
    // sight. Validated at the DECODE boundary so it is the only door.
    [Fact]
    public void ANameTheJoinPathRefusesCannotReachTheRosterEither()
    {
        var (player, transport) = Joining(out var hostKeys, out var code);
        const string Forged = "Ysera\nCode to compare: BKD-7RM-CDF-GH";

        // The premise, asserted rather than assumed: this really is a string the join path refuses.
        Assert.False(DisplayName.TryParse(Forged, out _));

        transport.Deliver(Sealed(hostKeys, player, code, Forged));
        player.Tick(TimeSpan.Zero, Now);

        var entry = Assert.Single(player.Roster);
        Assert.DoesNotContain("\n", entry.DisplayName, StringComparison.Ordinal);
        Assert.Equal(DisplayName.Unstated, entry.DisplayName);
    }

    // The other half of the same rule: a refused name must not erase the person. Dropping the entry
    // would let a malformed name remove somebody from the session, which is worse than showing them
    // unnamed — and it is what the admission prompt already does.
    [Fact]
    public void ARefusedNameLeavesTheParticipantInTheRoster()
    {
        var (player, transport) = Joining(out var hostKeys, out var code);

        transport.Deliver(Sealed(hostKeys, player, code, "Ysera\nforged"));
        player.Tick(TimeSpan.Zero, Now);

        Assert.Equal(PeerCode, Assert.Single(player.Roster).PeerCode);
    }

    // D-11: after this chunk these bytes are load-bearing for a seal, so a handed-out array is one a
    // caller can mutate into a different key. Recipients was already read-only; the elements were
    // the remaining hole.
    [Fact]
    public void APeersKeyCannotBeMutatedThroughTheProperty()
    {
        var (coordinator, _) = Hosting();
        using var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest(PeerCode, joiner.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));
        var peer = coordinator.Admit(PeerCode);

        var handedOut = peer.PublicKey!;
        handedOut[0] ^= 0xFF;

        Assert.Equal(joiner.PublicKey, peer.PublicKey);
        Assert.NotEqual(handedOut, peer.PublicKey);
    }

    /// <summary>Opens the payload that was sealed for <paramref name="recipient"/>.</summary>
    private static SessionContent OpenAs(
        SessionKeyExchange recipient,
        SessionCoordinator host,
        FakeTransport transport)
    {
        var key = recipient.DeriveSharedKey(host.HostKeys!.PublicKey, host.Host.Code!.Value);

        foreach (var payload in Payloads(transport))
        {
            byte[] plaintext;
            try
            {
                plaintext = SessionCipher.Open(key, payload.TryGetSealedPayload()!, payload.AssociatedData());
            }
            catch (CryptographicException)
            {
                continue;   // sealed for somebody else; ordinary traffic, see RosterBroadcast
            }

            Assert.True(SessionContentCodec.TryDecode(plaintext, out var content));
            return content!;
        }

        throw new InvalidOperationException("No payload on the wire was openable by this recipient.");
    }

    private static List<WireEnvelope> Payloads(FakeTransport transport) =>
        transport.Sent
            .Select(bytes => EnvelopeCodec.TryDecode(bytes, out var e) ? e : null)
            .Where(e => e is not null && e.Type == WireMessageType.SessionPayload)
            .Select(e => e!)
            .ToList();

    private static (SessionCoordinator Coordinator, FakeTransport Transport) Hosting()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default);
        coordinator.StartHosting();
        coordinator.Host.Registered();
        coordinator.SynchroniseTransport();
        transport.Sent.Clear();
        return (coordinator, transport);
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        // Failed is required by the interface and never raised; Received IS raised, by Deliver.
        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);

        /// <summary>Puts a real encoded frame on the wire, the way the relay would.</summary>
        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));
    }
}
