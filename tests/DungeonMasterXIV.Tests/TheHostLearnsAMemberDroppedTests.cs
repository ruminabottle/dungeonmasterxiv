using System;
using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.28 and A-1.29: the host learns a member's connection dropped, and learning it changes
/// nothing about who is in the session.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE LIMIT, FIRST, BECAUSE A-1.28 CONTAINS AN ABSENCE AND NO TEST HERE REACHES IT.</b> The
/// criterion says the host learns of a drop <i>"without inferring it from silence"</i>. What this
/// file demonstrates is the POSITIVE half — that a notice exists, arrives, and is recorded. <b>It
/// cannot demonstrate that no code path anywhere infers a drop from quiet.</b> A timer elsewhere
/// that started a seat clock on an absence of traffic would need no field any of these tests can
/// see, and would leave every one of them green. That gap closes by being stated, not by better
/// reflection.
/// </para>
/// <para>
/// <b>A-1.30 IS NOT DISCHARGED HERE AND NOT BECAUSE IT IS HARD.</b> It was STRUCK — its assertion
/// (<i>"kill a client without a leave notice and assert the seat is held"</i>) passes on a build
/// where the host never learns of the kill at all, because nothing removes anybody. A criterion is
/// met when the build EXHIBITS the property, not when nothing contradicts it. Recorded here so
/// nobody adds a test that would pass vacuously and reads as coverage.
/// </para>
/// <para>
/// <b>A RECORDED INSTANT, NOT A CLOCK.</b> R-1.5a constrains the decision taken when a client
/// RETURNS; it says nothing about the roster while nobody is looking. So there is nothing here that
/// ticks, expires or sweeps, and a test asserting a seat visibly lapsing would be testing a
/// requirement that was explicitly not made.
/// </para>
/// </remarks>
public class TheHostLearnsAMemberDroppedTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 6, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    // A-1.28's positive half, through the real inbound path rather than by calling RecordDrop.
    // Fails on origin/main before this change: there is no message type to deliver.
    [Fact]
    public void ADropNoticeReachesTheHostAndIsRecorded()
    {
        var (admissions, inbox, joinerKey, peerCode) = HostWithOneAdmittedMember();

        Deliver(inbox, admissions, WireEnvelope.ForConnectionDropped(Code, joinerKey));

        Assert.Equal(Now, admissions.Drops.WhenDropped(peerCode));
    }

    // A-1.29, and it fails TWICE if broken -- D-3 and R-1.5a. The relay reports a transport fact;
    // the host decides. A build that removed the member here would be taking its roster from a
    // party D-2 says is not authoritative over the session.
    [Fact]
    public void ADropNoticeDoesNotChangeTheRoster()
    {
        var (admissions, inbox, joinerKey, peerCode) = HostWithOneAdmittedMember();
        var before = admissions.Audience.Count;

        Deliver(inbox, admissions, WireEnvelope.ForConnectionDropped(Code, joinerKey));

        Assert.Equal(before, admissions.Audience.Count);
        Assert.True(admissions.Audience.IsAdmitted(peerCode), "R-1.5a HOLDS the seat; it does not free it.");
        Assert.NotNull(admissions.Audience.Find(peerCode));
    }

    // The outer guard is the relay's -- RelayRouter refuses a client-sent notice. This is the INNER
    // one, and it is the reason the inbound arm needs no sender check of its own: a notice naming
    // somebody this host never admitted records nothing at all.
    [Fact]
    public void ANoticeNamingAStrangerRecordsNothing()
    {
        var (admissions, inbox, _, _) = HostWithOneAdmittedMember();
        using var stranger = new SessionKeyExchange();

        Deliver(inbox, admissions, WireEnvelope.ForConnectionDropped(Code, stranger.PublicKey));

        Assert.Equal(0, admissions.Drops.Count);
    }

    // The other direction, and it is what makes the record usable rather than accumulating. R-1.5a's
    // decision is taken at ADMISSION, so that is where a spent instant is forgotten -- not when
    // traffic arrives, which would be inferring presence from noise.
    [Fact]
    public void ReturningClearsTheRecordedDrop()
    {
        var (admissions, inbox, joinerKey, peerCode) = HostWithOneAdmittedMember();
        Deliver(inbox, admissions, WireEnvelope.ForConnectionDropped(Code, joinerKey));
        Assert.NotNull(admissions.Drops.WhenDropped(peerCode));

        admissions.Receive(peerCode, joinerKey, Now, displayName: DisplayName.OrNone("Ysera"));
        admissions.Admit(peerCode);

        Assert.Null(admissions.Drops.WhenDropped(peerCode));
    }

    // A second drop after a return measures the SECOND absence. Keeping the first would age a member
    // out on the strength of a gap they have already come back from.
    [Fact]
    public void ALaterDropReplacesTheEarlierOne()
    {
        var (admissions, inbox, joinerKey, peerCode) = HostWithOneAdmittedMember();
        var later = Now.AddMinutes(7);

        Deliver(inbox, admissions, WireEnvelope.ForConnectionDropped(Code, joinerKey));
        admissions.Drops.Record(peerCode, later);

        Assert.Equal(later, admissions.Drops.WhenDropped(peerCode));
    }

    // Ending the session forgets them, like everything else the host was holding.
    [Fact]
    public void ClearingTheSessionForgetsEveryDrop()
    {
        var (admissions, inbox, joinerKey, _) = HostWithOneAdmittedMember();
        Deliver(inbox, admissions, WireEnvelope.ForConnectionDropped(Code, joinerKey));

        admissions.Clear();

        Assert.Equal(0, admissions.Drops.Count);
    }

    /// <summary>Drives the notice through the real drain, not through RecordDrop directly.</summary>
    private static void Deliver(AdmissionInbox inbox, AdmissionControl admissions, WireEnvelope notice)
    {
        inbox.Receive(EnvelopeCodec.Encode(notice));
        inbox.Drain(
            new JoinAttempt(),
            null,
            new HostSession(),
            new InboundHandlers(
                Transport: new TransportNotices(
                    OnConnectionDropped: key => admissions.RecordDrop(key, Now))));
    }

    private static (AdmissionControl Admissions, AdmissionInbox Inbox, byte[] JoinerKey, PeerCode Code)
        HostWithOneAdmittedMember()
    {
        var host = new HostSession();
        host.Start(Code);
        using var hostKeys = new SessionKeyExchange();
        var admissions = new AdmissionControl(
            new AdmissionAnnouncer(new SilentTransport()),
            () => host.Code,
            () => hostKeys,
            static _ => null,
            SilentLog.Instance);

        var joiner = new SessionKeyExchange();
        var peerCode = admissions.PeerCodeFor(joiner.PublicKey);
        admissions.Receive(peerCode, joiner.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));
        admissions.Admit(peerCode);

        return (admissions, new AdmissionInbox(), joiner.PublicKey, peerCode);
    }

    private sealed class SilentTransport : ISessionTransport
    {
        public bool IsConnected => true;

        public bool IsReadyToSend => true;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received { add { } remove { } }

        public void Connect(Uri relay)
        {
        }

        public void Disconnect()
        {
        }

        public void Send(byte[] envelope)
        {
        }
    }
}
