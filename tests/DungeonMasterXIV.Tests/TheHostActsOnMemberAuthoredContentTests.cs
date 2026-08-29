using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.3k / A-1.13c: a host opens content authored by an admitted member, and acts on it.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE CRITERION IS WRITTEN OVER THE ACTION AND NOT THE ARRIVAL, AND THAT PHRASING IS THE WHOLE
/// REASON THIS FILE LOOKS THE WAY IT DOES.</b> A test asserting that a member's payload REACHED the
/// host would have been green against every build this product has ever had. The relay routed it
/// correctly the entire time — <c>RelayRouter.ForwardPayload</c> forwards from any admitted member
/// to <c>MembersExcept(sender)</c>, and
/// <c>DungeonMasterXIV.Relay.Tests.AdmissionGateIsReachedTests</c> proves it live — while the host
/// held a single key that was null when hosting and dropped every payload unopened. <b>Routing is
/// availability; decryption is capability.</b>
/// </para>
/// <para>
/// <b>So every assertion here is on state that CANNOT MOVE unless a payload was decrypted.</b> The
/// load-bearing one is the peer code on a receipt: nothing on the wire names a sender —
/// <see cref="WireEnvelope.ForSessionPayload"/> sets only the nonce and the ciphertext — so the only
/// way to know who sent something is to find the key that opens it. <b>A host that merely received
/// the frame could not name anybody.</b>
/// </para>
/// <para>
/// <b>The counterfactual is asserted rather than described.</b>
/// <c>TheOldSingleKeyPathHadNothingToTryAndItStillOpens</c> pins <c>SessionKey</c> as null on a pure
/// host in the same arrangement that succeeds — so the test states, in the run rather than in a
/// comment, that the pre-DMXENG-50 path had literally no key to attempt and the payload opens
/// regardless.
/// </para>
/// <para>
/// <b>NOTHING IN THE SHIPPED PRODUCT SENDS THESE PAYLOADS YET.</b>
/// <see cref="WireEnvelope.ForSessionPayload"/> has exactly one production caller,
/// <c>RosterBroadcast</c>, which is the host. The sending half is <b>DMXENG-11 / A-1.15</b>, a live
/// ticket held by another engineer and blocked on this one. These fixtures therefore build the
/// member's payload directly, which is honest for a Core test and is <b>not</b> a claim that a
/// member client can do this today.
/// </para>
/// </remarks>
public sealed class TheHostActsOnMemberAuthoredContentTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 2, 0, 0, TimeSpan.Zero);
    private const string Speaker = "PRBCD2";
    private const string Listener = "JNKBCD";

    // THE BAR. Fails if: a host drops content authored by an admitted member unopened.
    //
    // The assertion is on MemberContentReceived and on the PEER NAMED IN THE RECEIPT, not on the
    // frame having arrived. Both are unreachable without decryption.
    [Fact]
    public void AHostOpensContentAuthoredByAnAdmittedMember()
    {
        var host = Hosting(out var transport);
        using var member = new SessionKeyExchange();
        var speaker = Admitted(host, Speaker, member);

        transport.Deliver(SealedBy(member, host));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Equal(1, host.MemberContent.Received);
        var receipt = Assert.Single(host.MemberContent.Latest);
        Assert.Equal(speaker, receipt.Peer);
    }

    // THE COUNTERFACTUAL, ASSERTED. Fails if: the arrangement above starts passing for a reason
    // other than the one under test.
    //
    // SessionKey is the ONLY key the pre-DMXENG-50 inbox had -- SessionCoordinator:SessionKey is the
    // JOINER's key, and this client never joined anything. Pinning it null in the same arrangement
    // that succeeds is the statement that the old path had nothing to try, made as a run rather than
    // as a comment somebody can leave behind when they change the code.
    [Fact]
    public void TheOldSingleKeyPathHadNothingToTryAndItStillOpens()
    {
        var host = Hosting(out var transport);
        using var member = new SessionKeyExchange();
        var speaker = Admitted(host, Speaker, member);

        Assert.Null(host.SessionKey);

        transport.Deliver(SealedBy(member, host));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Equal(1, host.MemberContent.Received);
    }

    // INSTRUMENT CHECK, AND WITHOUT IT THE TEST ABOVE PROVES LESS THAN IT LOOKS.
    // Fails if: a receipt appears for a payload the host could not have opened. If the drain recorded
    // on arrival rather than on decryption, every test in this file would pass and the feature would
    // not exist -- which is precisely the failure A-1.13c is phrased to catch.
    [Fact]
    public void ContentFromSomebodyNotAdmittedProducesNoReceipt()
    {
        var host = Hosting(out var transport);
        using var member = new SessionKeyExchange();
        using var stranger = new SessionKeyExchange();
        var speaker = Admitted(host, Speaker, member);

        // Sealed with a real key, correctly framed, and addressed to a host that shares no key with
        // this sender. Every candidate must fail.
        transport.Deliver(SealedBy(stranger, host));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Equal(0, host.MemberContent.Received);
        Assert.Empty(host.MemberContent.Latest);
    }

    // D-3, AND THIS IS THE ONE THAT WOULD HAVE BEEN MISSED. Fails if: a member becomes the author of
    // what the HOST believes the roster is.
    //
    // SessionCoordinator.Roster documents that on a host it stays empty because "the host authors
    // the roster and never receives one" -- an invariant that held ONLY because a host had no key to
    // open anything with. The moment R-1.3k hands it keys, wiring member content into the same
    // handler would invert D-3: shared state would have two authors, one of them a player.
    //
    // The payload here carries a full, well-formed roster naming somebody who is not in the session.
    // If it lands in Roster, a member has authored the host's view of the room.
    [Fact]
    public void MemberContentDoesNotBecomeTheHostsRoster()
    {
        var host = Hosting(out var transport);
        using var member = new SessionKeyExchange();
        var speaker = Admitted(host, Speaker, member);

        var forged = new SessionContent
        {
            Roster = [new RosterEntry(Listener, "Somebody Who Is Not Here", SessionRole.DungeonMaster)],
        };

        transport.Deliver(SealedBy(member, host, forged));
        host.Tick(TimeSpan.Zero, Now);

        // It was OPENED -- so this is not passing because the payload was dropped.
        Assert.Equal(1, host.MemberContent.Received);

        // And it went nowhere near the host's own view of the room.
        Assert.Empty(host.Roster);
    }

    // Fails if: two members cannot be told apart. Identity comes from WHICH KEY OPENED THE PAYLOAD,
    // so this is the assertion that the mechanism identifies rather than merely decrypts -- and it is
    // what A-1.15 needs, since "a player left" is useless without which player.
    [Fact]
    public void TwoMembersAreToldApartByTheKeyThatOpenedTheirContent()
    {
        var host = Hosting(out var transport);
        using var first = new SessionKeyExchange();
        using var second = new SessionKeyExchange();
        var one = Admitted(host, Speaker, first);
        var two = Admitted(host, Listener, second);

        transport.Deliver(SealedBy(second, host));
        transport.Deliver(SealedBy(first, host));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Equal(2, host.MemberContent.Received);
        Assert.Equal([two, one], host.MemberContent.Latest.Select(receipt => receipt.Peer));
    }

    // HOST RECEIPT ORDER, WHICH A-2.5 NAMES AND NO MEMBER CAN CLAIM. Fails if: the order is taken
    // from anything a sender controls rather than assigned when the host reads it.
    [Fact]
    public void TheOrderIsAssignedByTheHostWhenItReadsThem()
    {
        var host = Hosting(out var transport);
        using var first = new SessionKeyExchange();
        using var second = new SessionKeyExchange();
        var one = Admitted(host, Speaker, first);
        var two = Admitted(host, Listener, second);

        transport.Deliver(SealedBy(first, host));
        host.Tick(TimeSpan.Zero, Now);
        transport.Deliver(SealedBy(second, host));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Equal([1, 2], host.MemberContent.Latest.Select(receipt => receipt.Order));
    }

    // Fails if: a frame from a finished session can be replayed into the next one.
    //
    // NAMED FOR WHAT IT ACTUALLY PINS, WHICH IS NOT WHAT I FIRST CALLED IT. This was
    // "KeysDoNotSurviveTheSessionTheyWereDerivedFor" until I mutated the cache invalidation out and
    // it stayed GREEN -- StopHosting clears the audience, so there are no candidates left to try and
    // the assertion never reaches the cache at all. It passed for a reason unrelated to its name.
    // The real invalidation test is the one below; this one keeps the replay property, which is
    // worth having and is genuinely what it tests.
    [Fact]
    public void AFrameFromAFinishedSessionCannotBeReplayedIntoTheNext()
    {
        var host = Hosting(out var transport);
        using var member = new SessionKeyExchange();
        Admitted(host, Speaker, member);
        var fromTheOldSession = SealedBy(member, host);

        host.StopHosting(Now);
        host.StartHosting();
        host.Host.Registered();
        host.SynchroniseTransport();

        transport.Deliver(fromTheOldSession);
        host.Tick(TimeSpan.Zero, Now);

        Assert.Equal(0, host.MemberContent.Received);
    }

    // THE CACHE INVALIDATION, TESTED ON THE PATH THAT ACTUALLY REACHES IT.
    // Fails if: a key derived under the previous host key pair is served under the new one.
    //
    // StartHosting can be called WITHOUT StopHosting -- it disposes the old key pair and makes a new
    // one, and it does NOT clear the audience. So the peers stay admitted, their cached keys were
    // derived from a key pair that no longer exists, and a cache that did not notice would hand back
    // a key that can no longer open anything. The member here re-derives against the NEW host key,
    // exactly as a real one would after a rehost, and must be readable.
    //
    // This is a POSITIVE assertion on purpose. The negative version -- old payload does not open --
    // is satisfied by the AAD binding to the session code and would pass with the invalidation
    // deleted, which is the trap the test above fell into.
    [Fact]
    public void RehostingWithoutStoppingDoesNotServeKeysFromTheOldKeyPair()
    {
        var host = Hosting(out var transport);
        using var member = new SessionKeyExchange();
        var speaker = Admitted(host, Speaker, member);

        // Populate the cache under the first key pair.
        transport.Deliver(SealedBy(member, host));
        host.Tick(TimeSpan.Zero, Now);
        Assert.Equal(1, host.MemberContent.Received);

        // New key pair, new code, SAME audience -- no StopHosting anywhere.
        host.StartHosting();
        host.Host.Registered();
        host.SynchroniseTransport();
        Assert.Contains(host.Audience.Recipients, peer => peer.PeerCode == speaker);

        transport.Deliver(SealedBy(member, host));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Equal(2, host.MemberContent.Received);
        Assert.Equal(speaker, host.MemberContent.Latest[^1].Peer);
    }

    // Fails if: the record is left holding the previous session's traffic. "Since the session began"
    // has to mean the session somebody is in.
    [Fact]
    public void EndingTheSessionEmptiesWhatTheHostHeard()
    {
        var host = Hosting(out var transport);
        using var member = new SessionKeyExchange();
        var speaker = Admitted(host, Speaker, member);

        transport.Deliver(SealedBy(member, host));
        host.Tick(TimeSpan.Zero, Now);
        Assert.Equal(1, host.MemberContent.Received);

        host.StopHosting(Now);

        Assert.Equal(0, host.MemberContent.Received);
        Assert.Empty(host.MemberContent.Latest);
    }

    // A JOINER IS UNAFFECTED, WHICH IS THE OTHER HALF OF THE D-3 SPLIT. Fails if: the member arm
    // starts consuming host-authored content, or the host arm starts firing on a client that has no
    // members. A joiner has no audience, so it has no candidates and the arm is inert.
    [Fact]
    public void AJoinerStillReadsTheHostsRosterAndRecordsNoMemberContent()
    {
        var player = Joining(out var transport, out var hostKeys, out var code);

        var roster = new SessionContent
        {
            Roster = [new RosterEntry(Speaker, "Bob", SessionRole.Player)],
        };

        transport.Deliver(SealedFor(hostKeys, player.JoinerKeys!.PublicKey, code, roster));
        player.Tick(TimeSpan.Zero, Now);

        Assert.Single(player.Roster);
        Assert.Equal(0, player.MemberContent.Received);
    }

    // D-8, AND THIS IS A UNIT TEST BECAUSE THE CALL SITE CANNOT BE REACHED FROM ONE.
    // Fails if: Forget stops zeroing, i.e. drops the keys instead of erasing them.
    //
    // MemberContentKeys.Forget() is called from SessionResources.Release() when hosting ends, and
    // THAT CALL SITE IS NOT COVERED BY ANYTHING -- deleting it leaves the whole suite green, which I
    // measured rather than assumed. It cannot be otherwise: the only difference the call makes is
    // the contents of memory nobody can observe from a test, since a cleared audience yields no
    // candidates either way.
    //
    // So this pins the MECHANISM instead of the wiring, and the gap in the wiring is stated rather
    // than papered over. What it does catch is the version of this that would look correct and be
    // wrong: clearing the dictionary and leaving the arrays intact in the heap, which is exactly
    // what "dropped, not zeroed" means and exactly what D-8 is about.
    [Fact]
    public void ForgettingTheKeysErasesThemRatherThanDroppingThem()
    {
        var log = new RecordingLog();
        var audience = new SessionAudience();
        using var hostKeys = new SessionKeyExchange();
        using var member = new SessionKeyExchange();
        var code = SessionCode.FromValid("BCDFGH");

        audience.Admit(PeerCodes.Of(Speaker), SessionRole.Player, AdmissionVerification.NotCompared, member.PublicKey);

        var keys = new MemberContentKeys(audience, () => hostKeys, () => code, log);
        var derived = keys.Candidates().Single().Key;

        Assert.Contains(derived, b => b != 0);

        keys.Forget();

        Assert.All(derived, b => Assert.Equal(0, b));
    }

    private static SessionCoordinator Hosting(out DeliveringTransport transport)
    {
        transport = new DeliveringTransport();
        var host = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, log: new RecordingLog(), capabilities: SessionCapabilities.Default);

        host.StartHosting();
        host.Host.Registered();
        host.SynchroniseTransport();
        return host;
    }

    private static SessionCoordinator Joining(
        out DeliveringTransport transport,
        out SessionKeyExchange hostKeys,
        out SessionCode code)
    {
        transport = new DeliveringTransport();
        var player = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, log: new RecordingLog(), capabilities: SessionCapabilities.Default);

        code = SessionCode.FromValid("BCDFGH");
        hostKeys = new SessionKeyExchange();

        player.RequestJoin(code, DisplayName.OrNone("Bob"));
        player.SynchroniseTransport();
        player.Tick(TimeSpan.Zero, Now);
        transport.Deliver(WireEnvelope.ForJoinAccepted(code, player.JoinerKeys!.PublicKey, hostKeys.PublicKey));
        player.Tick(TimeSpan.Zero, Now);
        return player;
    }

    private static PeerCode Admitted(SessionCoordinator host, string code, SessionKeyExchange keys)
    {
        var peerCode = PeerCodes.Of(code);
        host.ReceiveJoinRequest(peerCode, keys.PublicKey, Now);
        host.Admit(peerCode);
        return peerCode;
    }

    // Sealed the way a MEMBER would seal it: with the key that member derives from the HOST's public
    // key and the session code. Not with anything the host handed out -- there is no such thing, and
    // a fixture that took a shortcut here would be testing a path the product does not have.
    private static WireEnvelope SealedBy(
        SessionKeyExchange member,
        SessionCoordinator host,
        SessionContent? content = null)
    {
        var code = host.Host.Code!.Value;
        return SealedFor(member, host.HostKeys!.PublicKey, code, content);
    }

    private static WireEnvelope SealedFor(
        SessionKeyExchange from,
        byte[] toPublicKey,
        SessionCode code,
        SessionContent? content = null)
    {
        var sealedPayload = SessionCipher.Seal(
            from.DeriveSharedKey(toPublicKey, code),
            SessionContentCodec.Encode(content ?? new SessionContent()),
            WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));

        return WireEnvelope.ForSessionPayload(code, sealedPayload);
    }

    private sealed class RecordingLog : ISessionTransportLog
    {
        public List<string> Warnings { get; } = new();

        public void Information(string message)
        {
        }

        public void Warning(string message) => Warnings.Add(message);

        public void Warning(Exception exception, string message) => Warnings.Add(message);
    }

    private sealed class DeliveringTransport : ISessionTransport
    {
        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope)
        {
        }

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));
    }
}
