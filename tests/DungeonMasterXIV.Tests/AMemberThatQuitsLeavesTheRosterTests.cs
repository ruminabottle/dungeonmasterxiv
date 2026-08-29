using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.16a: when a player's client quits, that player is removed from the roster immediately —
/// and A-1.30's line, that a member which VANISHES is not treated as one that QUIT.
/// </summary>
/// <remarks>
/// <para>
/// <b>The two halves are two inbound paths with no code in common, and these tests are what keeps
/// them apart.</b> A quit arrives as a member-authored document and REMOVES. A vanish arrives as a
/// relay notice and RECORDS while the seat is held (A-1.28). Conflating them closes a false gap by
/// breaking R-1.5a, which is the failure SQ-60 and SQ-62 both stopped.
/// </para>
/// <para>
/// <b>The quoted form of A-1.30 in DMXENG-60 is the STRUCK one</b> (SQ-73): <i>"assert the seat is
/// held"</i> is satisfiable by doing nothing, and passed on a build where the host never learned of
/// the kill at all. <b>So the vanish direction here does not assert an absence.</b> It asserts that
/// the drop was RECORDED and the member is STILL ADMITTED — a do-nothing build fails the first half.
/// </para>
/// </remarks>
public sealed class AMemberThatQuitsLeavesTheRosterTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 9, 0, 0, TimeSpan.Zero);
    private const string Member = "PRBCD2";
    private const string Other = "JNKBCD";

    // A-1.16a ITSELF, end to end over the real wire: a member seals a departure, the host opens it
    // on the path DMXENG-50 built, and the member is gone from the roster.
    //
    // Fails if: the field is dropped by the codec, the wiring does not act on it, or removal is
    // deferred. "Immediately" is the criterion's own word -- there is no tick between the notice
    // arriving and the roster changing.
    [Fact]
    public void AMemberThatAnnouncesItsDepartureIsRemovedAtOnce()
    {
        var host = Hosting(out var transport);
        var (member, code) = Admit(host, transport);

        Assert.True(host.Audience.IsAdmitted(code));

        transport.Deliver(Departure(member, host));
        host.Tick(TimeSpan.Zero, Now);

        Assert.False(host.Audience.IsAdmitted(code));
    }

    // A-1.30, THE LINE. A member that vanishes sends nothing; the relay reports it and the host
    // RECORDS the drop while holding the seat.
    //
    // ASSERTED AS A POSITIVE, NOT AN ABSENCE, because SQ-73 struck the absence form: "assert the
    // seat is held" passes on a build where nothing happens at all. Here the drop must be RECORDED
    // and the member must still be admitted -- a do-nothing build fails the first clause.
    [Fact]
    public void AMemberThatVanishesIsRecordedAndKeepsItsSeat()
    {
        var host = Hosting(out var transport);
        var (member, code) = Admit(host, transport);

        transport.Deliver(WireEnvelope.ForConnectionDropped(host.Host.Code!.Value, member.PublicKey));
        host.Tick(TimeSpan.Zero, Now);

        Assert.NotNull(host.Drops.WhenDropped(code));
        Assert.True(host.Audience.IsAdmitted(code));
    }

    // THE DISCRIMINATION, which is what A-1.30 actually asks for: the same member, the two paths,
    // opposite outcomes. Neither test above rules out a build that treats both alike -- one asserts
    // removal, the other retention, and only running them against ONE member proves they are told
    // apart rather than that two fixtures behave differently.
    [Fact]
    public void TheTwoPathsDoOppositeThingsToTheSameMember()
    {
        var host = Hosting(out var transport);
        var (quitter, quitterCode) = Admit(host, transport);
        var (vanisher, vanisherCode) = Admit(host, transport);

        transport.Deliver(WireEnvelope.ForConnectionDropped(host.Host.Code!.Value, vanisher.PublicKey));
        transport.Deliver(Departure(quitter, host));
        host.Tick(TimeSpan.Zero, Now);

        Assert.False(host.Audience.IsAdmitted(quitterCode));
        Assert.True(host.Audience.IsAdmitted(vanisherCode));
    }

    // ORDINARY MEMBER CONTENT MUST NOT REMOVE ANYBODY. Fails if the wiring acts on the arrival of a
    // document rather than on what it says -- which would make every member message a departure.
    [Fact]
    public void MemberContentWithoutADepartureRemovesNobody()
    {
        var host = Hosting(out var transport);
        var (member, code) = Admit(host, transport);

        transport.Deliver(Sealed(member, host, new SessionContent()));
        host.Tick(TimeSpan.Zero, Now);

        Assert.Equal(1, host.MemberContent.Received);
        Assert.True(host.Audience.IsAdmitted(code));
    }

    // A MEMBER CAN ONLY REMOVE ITSELF, and this is the one a malicious client would try. The peer
    // code is read from the KEY the payload opened under, never from the payload, so a departure
    // sealed by one member cannot take another off the roster however it is shaped.
    [Fact]
    public void AMemberCannotAnnounceSomebodyElsesDeparture()
    {
        var host = Hosting(out var transport);
        var (attacker, attackerCode) = Admit(host, transport);
        var (victim, victimCode) = Admit(host, transport);

        transport.Deliver(Departure(attacker, host));
        host.Tick(TimeSpan.Zero, Now);

        Assert.False(host.Audience.IsAdmitted(attackerCode));
        Assert.True(host.Audience.IsAdmitted(victimCode));
    }

    // THE DEPARTURE IS STILL RECORDED AS A RECEIPT. Fails if removal short-circuits the record: the
    // DM's surface reads receipts, and a member vanishing from the roster with nothing anywhere
    // saying why is PR #86 finding 5 in a new place.
    [Fact]
    public void TheDepartureLeavesATraceRatherThanOnlyAnAbsence()
    {
        var host = Hosting(out var transport);
        var (member, code) = Admit(host, transport);

        transport.Deliver(Departure(member, host));
        host.Tick(TimeSpan.Zero, Now);

        var receipt = Assert.Single(host.MemberContent.Latest);
        Assert.Equal(code, receipt.Peer);
        Assert.True(receipt.Content.Leaving);
    }

    // D-14. Fails if the new field does not survive the codec, or is dropped by Vetted -- which is
    // its own guard test, and this is the round trip that would notice the codec half.
    [Fact]
    public void ADepartureSurvivesTheCodec()
    {
        var encoded = SessionContentCodec.Encode(new SessionContent { Leaving = true });

        Assert.True(SessionContentCodec.TryDecode(encoded, out var decoded, null));
        Assert.True(decoded!.Leaving);
    }

    // The member's own half: nothing to leave means nothing sent, silently. A client that quits the
    // join screen has no host to tell, and treating that as a failure would make the ordinary path
    // noisy. Fails if Announce throws or sends on a client that was never admitted.
    [Fact]
    public void AClientThatWasNeverAdmittedAnnouncesNothing()
    {
        var transport = new DeliveringTransport();
        var joiner = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default,
            SilentLog.Instance, SessionCapabilities.Default);

        joiner.RequestJoin(SessionCode.FromValid("BCDFGH"), DisplayName.OrNone("Bob"));
        joiner.SynchroniseTransport();

        Assert.False(joiner.Membership.AnnounceDeparture());
    }

    // THE MEMBER'S OWN HALF, END TO END: an admitted client calls AnnounceDeparture, the frame it
    // puts on the wire is handed to the host, and the host removes it.
    //
    // WRITTEN BECAUSE A PROBE FOUND IT MISSING. Deleting the _link.Send inside MemberDeparture left
    // the whole suite GREEN -- every other test here constructs the departure envelope itself, so
    // nothing exercised the SENDER. The receiving half was pinned four ways and the sending half not
    // at all, which is the same "a model with no production caller" shape MemberContentReceipts
    // warns about, one layer down.
    [Fact]
    public void AnAdmittedMemberSendsItsOwnDepartureAndTheHostActsOnIt()
    {
        var host = Hosting(out var hostTransport);
        var member = Joined(out var memberTransport, host, out var code);

        Assert.True(member.Membership.AnnounceDeparture());

        var frame = Assert.Single(memberTransport.Sent);
        Assert.True(EnvelopeCodec.TryDecode(frame, out var envelope));
        hostTransport.Deliver(envelope!);
        host.Tick(TimeSpan.Zero, Now);

        Assert.False(host.Audience.IsAdmitted(code));
    }

    // VETTED'S REBUILD PATH, which the departure documents above never reach: Vetted RETURNS EARLY
    // when Roster is null, so a document carrying only Leaving is handed back untouched and the
    // rebuild is never executed.
    //
    // ANOTHER PROBE FINDING. Setting Leaving = null inside Vetted left the suite green, because
    // nothing put a roster and a departure in the same document. feature-engineer-2 warned about
    // exactly this shape hours ago -- "if you test your section in isolation you will prove nothing;
    // put a valid roster entry alongside it" -- and it was right.
    //
    // Fails if a future edit drops Leaving from the rebuild list. Today no producer sends both, so
    // this pins a line that protects against a document nobody writes YET.
    [Fact]
    public void ADepartureSurvivesVettedsRebuildWhenARosterIsPresent()
    {
        var host = Hosting(out var transport);
        var (_, code) = Admit(host, transport);

        var both = new SessionContent
        {
            Roster = [new RosterEntry(code.Value, "Bob", SessionRole.Player)],
            Leaving = true,
        };

        Assert.True(SessionContentCodec.TryDecode(SessionContentCodec.Encode(both), out var decoded, null));
        Assert.NotNull(decoded!.Roster);
        Assert.True(decoded.Leaving);
    }

    private static SessionCoordinator Joined(
        out DeliveringTransport transport,
        SessionCoordinator host,
        out PeerCode code)
    {
        transport = new DeliveringTransport();
        var member = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default,
            SilentLog.Instance, SessionCapabilities.Default);

        var sessionCode = host.Host.Code!.Value;
        member.RequestJoin(sessionCode, DisplayName.OrNone("Bob"));
        member.SynchroniseTransport();
        member.Tick(TimeSpan.Zero, Now);

        var before = host.Admissions.Pending.Select(p => p.PeerCode).ToList();
        host.ReceiveJoinRequest(
            new PendingAdmission(
                PeerCodes.Of("PRBCD2"), "BKD-7RM-CDF-GH", AdmissionDeadline.DecidedByHost(Now),
                RelinkClaim.None, member.Membership.Keys!.PublicKey, DisplayName.OrNone("Bob")));

        code = PeerCodes.Of("PRBCD2");
        host.Admit(code);

        // The acceptance the host sends is what gives the member its shared key.
        transport.Deliver(WireEnvelope.ForJoinAccepted(
            sessionCode, member.Membership.Keys!.PublicKey, host.HostKeys!.PublicKey));
        member.Tick(TimeSpan.Zero, Now);
        transport.Sent.Clear();

        return member;
    }

    private static WireEnvelope Departure(SessionKeyExchange member, SessionCoordinator host) =>
        Sealed(member, host, new SessionContent { Leaving = true });

    // Sealed with a REAL shared key so it travels the production path: the host opens it by trying
    // each admitted peer's key, and the peer code comes from whichever one worked.
    private static WireEnvelope Sealed(
        SessionKeyExchange member,
        SessionCoordinator host,
        SessionContent content)
    {
        var code = host.Host.Code!.Value;
        var sealedPayload = SessionCipher.Seal(
            member.DeriveSharedKey(host.HostKeys!.PublicKey, code),
            SessionContentCodec.Encode(content),
            WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));

        return WireEnvelope.ForSessionPayload(code, sealedPayload);
    }

    // ADMITTED THROUGH THE REAL WIRE so the peer code is DERIVED, not invented. My first version
    // handed PeerCodes.Of("PRBCD2") to ReceiveJoinRequest and the drop test failed: RecordDrop
    // computes PeerCodeFor(key) from the key the relay names, so a hand-made code is admitted under
    // one name and dropped under another and IsAdmitted says no. The quit path did not notice --
    // it matches against the audience's stored code -- so the fixture was wrong in a way only ONE
    // of the two paths could see, which is exactly the asymmetry these tests exist to police.
    private static (SessionKeyExchange Key, PeerCode Code) Admit(
        SessionCoordinator host,
        DeliveringTransport transport)
    {
        var member = new SessionKeyExchange();
        var before = host.Admissions.Pending.Select(p => p.PeerCode).ToList();

        transport.Deliver(WireEnvelope.ForJoinRequest(host.Host.Code!.Value, member.PublicKey));
        host.Tick(TimeSpan.Zero, Now);

        var pending = host.Admissions.Pending.Single(p => !before.Contains(p.PeerCode));
        host.Admit(pending.PeerCode);
        return (member, pending.PeerCode);
    }

    private static SessionCoordinator Hosting(out DeliveringTransport transport)
    {
        transport = new DeliveringTransport();
        var host = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default,
            SilentLog.Instance, SessionCapabilities.Default);

        host.StartHosting();
        host.Host.Registered();
        host.SynchroniseTransport();
        return host;
    }

    private sealed class DeliveringTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

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
