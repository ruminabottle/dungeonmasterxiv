using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-154: quitting the game is a DELIBERATE quit, so the joiner says so before the socket goes.
/// </summary>
/// <remarks>
/// <para>
/// <b>The teardown ran the host's half only.</b> <c>StopHosting</c>, <c>Detach</c>,
/// <c>Dispose</c> — the first is a no-op for a joiner and the other two drop the socket, which is
/// exactly what an ungraceful drop looks like from the relay's side. So a player who quit FFXIV
/// deliberately was indistinguishable from one whose machine died, and the host held the seat five
/// minutes under R-1.5a.
/// </para>
/// <para>
/// <b>R-1.3g draws the line this bug sits on.</b> An ungraceful drop is NOT a departure and holding
/// its seat is correct — removing vanished members to close that apparent gap is the false fix the
/// requirement names. Quitting cleanly is on the other side of it: the player said nothing because
/// the code never said anything, not because they crashed.
/// </para>
/// <para>
/// <b>ORDER IS THE WHOLE FIX AND IT IS ASSERTED, NOT ASSUMED.</b> After <c>Detach</c> there is
/// nothing to send on, so a departure placed after it compiles, returns false, sends nothing, and
/// looks correct in a diff. <see cref="RecordingTransport"/> keeps ONE ordered log of sends and of
/// the detach, so the test can say which came first rather than that both happened.
/// </para>
/// </remarks>
public class QuittingTheGameAnnouncesDepartureTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 16, 0, 0, TimeSpan.Zero);
    private const string Detached = "DETACHED";

    // THE DEFECT. Fails if teardown never announces: an admitted joiner quitting the game puts a
    // frame on the wire. Asserted on the FRAME, not on a bool -- a method returning true while
    // sending nothing is the shape this bug is made of.
    [Fact]
    public void AnAdmittedJoinerQuittingTheGameSendsADeparture()
    {
        var host = Hosting(out var hostTransport);
        var member = Joined(out var memberTransport, host, hostTransport);

        member.EndSessionForTeardown(Now);

        Assert.Contains(memberTransport.Log, entry => entry != Detached);
    }

    // AND IT IS SENT BEFORE THE TRANSPORT GOES. The ordering requirement, asserted directly: the
    // departure has to appear in the log ahead of the detach. A fix that announced afterwards would
    // satisfy the test above on a transport that still accepted sends, and ship nothing.
    [Fact]
    public void TheDepartureIsSentBeforeTheTransportIsDetached()
    {
        var host = Hosting(out var hostTransport);
        var member = Joined(out var memberTransport, host, hostTransport);

        member.EndSessionForTeardown(Now);

        var detachedAt = memberTransport.Log.IndexOf(Detached);
        var sentAt = memberTransport.Log.FindIndex(entry => entry != Detached);

        Assert.True(detachedAt >= 0, "The teardown never detached, so the ordering is untested.");
        Assert.True(sentAt >= 0, "Nothing was sent, so there is no ordering to check.");
        Assert.True(sentAt < detachedAt, $"Sent at {sentAt}, detached at {detachedAt}.");
    }

    // A HOST MUST NOT ANNOUNCE ONE. The doc on the departure path implies a host is already a no-op;
    // the bug report is explicit that this is a reading of a comment rather than a measurement, so it
    // is measured here.
    [Fact]
    public void AHostQuittingTheGameSendsNoDeparture()
    {
        var host = Hosting(out var hostTransport);
        hostTransport.Log.Clear();

        host.EndSessionForTeardown(Now);

        Assert.DoesNotContain(hostTransport.Log, entry => entry != Detached);
    }

    // AND THE HOST STILL PERFORMS THE R-1.1 CLOSE. Otherwise "sends no departure" would also be
    // satisfied by a teardown that does nothing at all for a host.
    [Fact]
    public void AHostQuittingTheGameStillEndsItsSession()
    {
        var host = Hosting(out _);

        host.EndSessionForTeardown(Now);

        Assert.False(host.InAHostedSession);
    }

    // A JOINER THAT WAS NEVER ADMITTED HAS NOTHING TO SAY, and must not throw on the way out. There
    // is no shared key, so an implementation that assumed one would fail exactly here.
    [Fact]
    public void AJoinerThatWasNeverAdmittedSendsNothingAndDoesNotThrow()
    {
        var transport = new RecordingTransport();
        var client = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default,
            SilentLog.Instance, SessionCapabilities.Default);
        transport.Log.Clear();

        client.EndSessionForTeardown(Now);

        Assert.DoesNotContain(transport.Log, entry => entry != Detached);
    }

    private static SessionCoordinator Hosting(out RecordingTransport transport)
    {
        transport = new RecordingTransport();
        var host = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default,
            SilentLog.Instance, SessionCapabilities.Default);

        host.StartHosting();
        host.Host.Registered();
        host.SynchroniseTransport();
        return host;
    }

    private static SessionCoordinator Joined(
        out RecordingTransport transport,
        SessionCoordinator host,
        RecordingTransport hostTransport)
    {
        transport = new RecordingTransport();
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
        hostTransport.Log.Clear();
        transport.Log.Clear();
        return member;
    }

    /// <summary>
    /// One ordered log of everything that happened to the transport — sends AND the detach.
    /// </summary>
    /// <remarks>
    /// Two separate lists would record that a send and a detach both occurred and could never say
    /// which was first, which is the only question this bug asks.
    /// </remarks>
    private sealed class RecordingTransport : ISessionTransport
    {
        public List<string> Log { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received
        {
            add => _received += value;
            remove
            {
                _received -= value;
                Log.Add(Detached);
            }
        }

        private Action<byte[]>? _received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Log.Add(Convert.ToBase64String(envelope));

        public void Deliver(byte[] envelope) => _received?.Invoke(envelope);
    }
}
