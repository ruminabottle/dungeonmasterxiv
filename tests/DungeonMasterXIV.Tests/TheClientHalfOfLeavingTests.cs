using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.3g from the CLIENT's side: hearing the session close, and leaving it (brief.md:391).
/// </summary>
/// <remarks>
/// <para>
/// <b>Both halves were built and neither could be reached.</b> The host has published a closing
/// instant since DMXENG-58 and no client read it; <c>AnnounceDeparture</c> existed with no
/// production caller; and the local teardown was inlined in <c>JoinRequester.Request</c>, under a
/// comment reading "a deliberate quit removes the seat immediately, AND ASKING TO JOIN AGAIN IS
/// THAT" — the comment naming its only trigger. <b>The plugin could already do all of this and
/// nobody could ask it to.</b>
/// </para>
/// <para>
/// <b>Driven end to end through two coordinators, deliberately.</b> A test that set the closing on
/// the member directly would pass on a build where the host's payload never reaches the reader,
/// which is the exact state this chunk exists to end.
/// </para>
/// </remarks>
public class TheClientHalfOfLeavingTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 16, 0, 0, TimeSpan.Zero);

    // R-1.3g: every participant sees the session is closing AND how long remains.
    [Fact]
    public void AParticipantIsToldTheDungeonMasterHasEndedTheSession()
    {
        var host = Hosting(out var hostTransport);
        var member = Joined(out var memberTransport, host, hostTransport);

        EndTheSessionAndDeliverTheNotice(host, hostTransport, memberTransport, member);

        Assert.NotNull(member.Membership.Closing);
    }

    // >>> THE HALF THAT IS A REQUIREMENT RATHER THAN A COURTESY <<<
    //
    // "The session is closing" without "how long remains" is the indefinite wait R-1.3c and R-1.8
    // both forbid. Compared against the window the HOST applied, derived here rather than read back
    // off the value under test -- asserting it equals itself is the vacuity A-1.2u-oracle names.
    [Fact]
    public void AndHowLongRemainsBeforeItDoes()
    {
        var host = Hosting(out var hostTransport);
        var member = Joined(out var memberTransport, host, hostTransport);

        EndTheSessionAndDeliverTheNotice(host, hostTransport, memberTransport, member);

        Assert.Equal(SessionClosing.Window, member.Membership.Closing!.Value.RemainingAt(Now));
    }

    // A-1.16a's client half: the member tells the host, so the roster can drop it at once.
    [Fact]
    public void LeavingTellsTheHost()
    {
        var host = Hosting(out var hostTransport);
        var member = Joined(out var memberTransport, host, hostTransport);
        memberTransport.Sent.Clear();

        member.Membership.Leave();

        Assert.NotEmpty(memberTransport.Sent);
    }

    // >>> THE PRODUCT OWNER'S CONSTRAINT, AND THE ONE A BUILD IS MOST LIKELY TO GET WRONG <<<
    //
    // Nothing is delivered back. The host never answers, never acknowledges, and the relay says
    // nothing -- the wrong-code-typed-into-a-stranger's-session case. The player must still be out.
    // A leave conditional on an ack is a user held in a session by a host that is not listening.
    [Fact]
    public void LeavingDoesNotWaitForTheHostToAnswer()
    {
        var host = Hosting(out var hostTransport);
        var member = Joined(out var memberTransport, host, hostTransport);

        member.Membership.Leave();

        Assert.False(member.InAJoinedSession, "Nothing came back, and the player has still left.");
    }

    // R-1.3h: exclusivity lasts for the life of the session, and a deliberate quit ends it. A player
    // who left and cannot host or join anywhere else has been released from nothing.
    [Fact]
    public void AfterLeavingTheClientMayHostOrJoinAgain()
    {
        var host = Hosting(out var hostTransport);
        var member = Joined(out var memberTransport, host, hostTransport);

        member.Membership.Leave();

        Assert.True(member.Join.MayRequestAgain, "R-1.3g: asking again is the point of leaving.");
        Assert.False(member.InAHostedSession);
    }

    // >>> FOUND BY MUTATION, NOT BY DESIGN, AND IT IS THE SECURITY HALF <<<
    //
    // Cutting the seat-and-keys release from the teardown left every other test in this file GREEN.
    // The phase moves to Idle either way, so "am I in a session" answers correctly while the client
    // still holds the key it derived on admission -- able to open payloads from a session it has
    // left, and holding a key pair across session codes, which is the linkage D-8 refuses.
    //
    // A LEAVE THAT KEEPS THE KEY IS NOT A LEAVE. Asserted on the key material rather than on the
    // phase, because the phase is the half that was already covered.
    [Fact]
    public void LeavingDropsTheKeyItWasAdmittedWith()
    {
        var host = Hosting(out var hostTransport);
        var member = Joined(out _, host, hostTransport);
        Assert.NotNull(member.Membership.SessionKey);
        Assert.NotNull(member.Membership.Keys);

        member.Membership.Leave();

        Assert.Null(member.Membership.SessionKey);
        Assert.Null(member.Membership.Keys);
    }

    // A notice outliving its session would show a countdown for a session the player is no longer
    // in, or close the next one they join.
    [Fact]
    public void LeavingForgetsAClosingNoticeAlreadyHeard()
    {
        var host = Hosting(out var hostTransport);
        var member = Joined(out var memberTransport, host, hostTransport);
        EndTheSessionAndDeliverTheNotice(host, hostTransport, memberTransport, member);
        Assert.NotNull(member.Membership.Closing);

        member.Membership.Leave();

        Assert.Null(member.Membership.Closing);
    }

    // The bystander: an ordinary roster push carries no closing, and most payloads are that. A build
    // that cleared on absence would forget the notice on the very next message.
    [Fact]
    public void AnOrdinaryPayloadAfterTheNoticeDoesNotEraseIt()
    {
        var host = Hosting(out var hostTransport);
        var member = Joined(out var memberTransport, host, hostTransport);
        EndTheSessionAndDeliverTheNotice(host, hostTransport, memberTransport, member);

        host.Admit(PeerCodes.Of("PRBCD2"));                 // republishes the roster, no closing
        DeliverHostPayloadsTo(hostTransport, memberTransport, member);

        Assert.NotNull(member.Membership.Closing);
    }

    private static void EndTheSessionAndDeliverTheNotice(
        SessionCoordinator host,
        DeliveringTransport hostTransport,
        DeliveringTransport memberTransport,
        SessionCoordinator member)
    {
        hostTransport.Sent.Clear();
        host.StopHosting(Now);
        DeliverHostPayloadsTo(hostTransport, memberTransport, member);
    }

    private static void DeliverHostPayloadsTo(
        DeliveringTransport hostTransport,
        DeliveringTransport memberTransport,
        SessionCoordinator member)
    {
        foreach (var sent in hostTransport.Sent.ToList())
        {
            memberTransport.Deliver(sent);
        }

        member.Tick(TimeSpan.Zero, Now);
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

    private static SessionCoordinator Joined(
        out DeliveringTransport transport,
        SessionCoordinator host,
        DeliveringTransport hostTransport)
    {
        transport = new DeliveringTransport();
        var member = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default,
            SilentLog.Instance, SessionCapabilities.Default);

        var sessionCode = host.Host.Code!.Value;
        member.RequestJoin(sessionCode, DisplayName.OrNone("Bob"));
        member.SynchroniseTransport();
        member.Tick(TimeSpan.Zero, Now);

        host.ReceiveJoinRequest(
            new PendingAdmission(
                PeerCodes.Of("PRBCD2"), "BKD-7RM-CDF-GH", AdmissionDeadline.DecidedByHost(Now),
                RelinkClaim.None, member.Membership.Keys!.PublicKey, DisplayName.OrNone("Bob")));
        host.Admit(PeerCodes.Of("PRBCD2"));

        transport.Deliver(EnvelopeCodec.Encode(WireEnvelope.ForJoinAccepted(
            sessionCode, member.Membership.Keys!.PublicKey, host.HostKeys!.PublicKey)));
        member.Tick(TimeSpan.Zero, Now);
        hostTransport.Sent.Clear();
        transport.Sent.Clear();
        return member;
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

        public void Deliver(byte[] envelope) => Received?.Invoke(envelope);
    }
}
