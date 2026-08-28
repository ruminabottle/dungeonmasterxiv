using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-1.3f-1 and R-1.3a-i: the joining client can compute the combined fingerprint <b>before</b> it
/// has been admitted.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every assertion here is about ordering, and that is deliberate.</b> The struck A-1.3f said the
/// same fingerprint appears on both screens and never said when, so a build that showed both parties
/// the same value after admission met it word for word — which is the build BUG-31 was filed
/// against. A presence test ("the joiner has a fingerprint") passes on that build too, because the
/// host's key arrives in the acceptance envelope: by the time anyone asks, it is there.
/// </para>
/// <para>
/// <b>So the positive test drives the real host and the real joiner</b> rather than hand-feeding
/// frames. On a build where the host sends its key only on acceptance, no pending notice is emitted,
/// the joiner holds nothing at the decision, and
/// <see cref="AJoinerIsGivenTheCodeBeforeTheDmDecides"/> fails. That is the run A-1.3f-1 requires to
/// fail.
/// </para>
/// <para>
/// <b>And the negative control is not optional.</b>
/// <see cref="AJoinerSentOnlyAnAcceptanceIsAdmittedAndToldItCouldNotCompare"/> exists so the positive
/// assertion cannot be vacuous: it shows the property can be <i>false</i>, on the one path that used
/// to be the only path. A check that has never been observed failing is not evidence.
/// </para>
/// <para>
/// This says nothing about the computation itself — that is A-1.3f-2, discharged by
/// <c>CombinedKeyFingerprintTests</c>, and nothing here duplicates it. Nor does it discharge
/// A-1.3f-4, which is two screens in a running game and cannot be reached from a test host.
/// </para>
/// </remarks>
public class TheJoinerCanCompareBeforeAdmissionTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 19, 0, 0, TimeSpan.Zero);

    // A-1.3f-1, the criterion itself. Fails if: the host's key travels no earlier than the
    // acceptance envelope, which is the state BUG-31 describes and the state this chunk changes.
    [Fact]
    public void AJoinerIsGivenTheCodeBeforeTheDmDecides()
    {
        var session = new TwoClients();

        session.JoinerRequests();

        Assert.NotNull(session.Joiner.Join.Fingerprint);
        Assert.NotEqual(JoinPhase.Admitted, session.Joiner.Join.Phase);
    }

    // The same ordering stated as the snapshot the UI reads. Fails if: the flag is derived from
    // "is there a fingerprint now" rather than captured before the transition — a derived property
    // is true a moment after admission on every build, including the broken one.
    [Fact]
    public void ThatCodeWasAvailableAtTheMomentOfAdmission()
    {
        var session = new TwoClients();
        session.JoinerRequests();

        session.HostAdmits();

        Assert.Equal(JoinPhase.Admitted, session.Joiner.Join.Phase);
        Assert.True(session.Joiner.Join.FingerprintWasComparableAtDecision);
    }

    // The cross-screen half that a test of the function cannot reach: the value the DM is shown and
    // the value the joiner is shown are the same string. Fails if: either side computes from the
    // wrong pair of keys, or the two are computed in different orders and only happen to agree.
    [Fact]
    public void BothScreensShowTheSameCode()
    {
        var session = new TwoClients();
        session.JoinerRequests();

        var onTheDmsScreen = session.Host.Admissions.Pending.Single().Fingerprint;

        Assert.Equal(onTheDmsScreen, session.Joiner.Join.Fingerprint);
    }

    // NEGATIVE CONTROL, and the D-14 interoperability proof in the same run. A host too old to send
    // a pending notice sends only an acceptance. Two things must both hold: the joiner is still
    // admitted (an additive change must not strand an old host), and it is NOT told it compared
    // anything. Fails if: the ordering flag is derived rather than snapshotted, or if the new
    // client refuses to join an old host.
    [Fact]
    public void AJoinerSentOnlyAnAcceptanceIsAdmittedAndToldItCouldNotCompare()
    {
        var session = new TwoClients();
        session.JoinerRequestsWithoutAPendingNotice();

        session.HostAdmits();

        Assert.Equal(JoinPhase.Admitted, session.Joiner.Join.Phase);
        Assert.False(session.Joiner.Join.FingerprintWasComparableAtDecision);
    }

    // The trap named explicitly, because it is the one a future refactor will walk into. The host's
    // key arrives again in the acceptance envelope, so anything recomputed afterwards can report a
    // comparison that never happened. Fails if: a late key backfills the fingerprint.
    [Fact]
    public void AHostKeyArrivingAfterTheDecisionCannotMakeTheCheckLookPossible()
    {
        var session = new TwoClients();
        session.JoinerRequestsWithoutAPendingNotice();
        session.HostAdmits();

        session.DeliverToJoiner(WireEnvelope.ForJoinPending(
            session.Code,
            session.Joiner.JoinerKeys!.PublicKey,
            session.Host.HostKeys!.PublicKey,
            AdmissionDeadline.DecidedByHost(Now)));
        session.Joiner.Tick(TimeSpan.Zero, Now);

        Assert.False(session.Joiner.Join.FingerprintWasComparableAtDecision);
        Assert.Null(session.Joiner.Join.Fingerprint);
    }

    // A pending notice is not an answer. Fails if: it is folded into the admission vocabulary, which
    // would let "the DM is looking at your request" admit somebody nobody decided about.
    [Fact]
    public void APendingNoticeAdmitsNobody()
    {
        var session = new TwoClients();

        session.JoinerRequests();

        Assert.Equal(JoinPhase.AwaitingDecision, session.Joiner.Join.Phase);
        Assert.False(session.Joiner.Join.MayReceiveSessionState);
    }

    // Two real coordinators with the host's outbound frames piped to the joiner, so the ordering
    // under test is the product's own rather than one the test arranged.
    private sealed class TwoClients
    {
        private const string PeerCode = "PEER-1";

        private readonly FakeTransport _hostTransport = new();
        private readonly FakeTransport _joinerTransport = new();

        public TwoClients()
        {
            Host = new SessionCoordinator(_hostTransport, () => RelayEndpoint.Default, GraceWindow.Default);
            Host.StartHosting();
            Host.Host.Registered();
            _hostTransport.Sent.Clear();

            Joiner = new SessionCoordinator(_joinerTransport, () => RelayEndpoint.Default, GraceWindow.Default);
            Code = Host.Host.Code!.Value;
        }

        public SessionCoordinator Host { get; }

        public SessionCoordinator Joiner { get; }

        public SessionCode Code { get; }

        /// <summary>The whole join up to the decision, with every host frame delivered.</summary>
        public void JoinerRequests()
        {
            Joiner.RequestJoin(Code);
            Host.ReceiveJoinRequest(PeerCode, Joiner.JoinerKeys!.PublicKey, Now);
            PumpHostToJoiner();
        }

        /// <summary>
        /// The same join against a host too old to send a pending notice — the frames are dropped
        /// rather than the sender changed, so the joiner sees exactly what an old host would send.
        /// </summary>
        public void JoinerRequestsWithoutAPendingNotice()
        {
            Joiner.RequestJoin(Code);
            Host.ReceiveJoinRequest(PeerCode, Joiner.JoinerKeys!.PublicKey, Now);
            _hostTransport.Sent.Clear();
            Joiner.Join.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));
        }

        public void HostAdmits()
        {
            Host.Admit(PeerCode);
            PumpHostToJoiner();
        }

        public void DeliverToJoiner(WireEnvelope envelope) => _joinerTransport.Deliver(envelope);

        private void PumpHostToJoiner()
        {
            foreach (var frame in _hostTransport.Sent.ToArray())
            {
                _joinerTransport.DeliverRaw(frame);
            }

            _hostTransport.Sent.Clear();
            Joiner.Tick(TimeSpan.Zero, Now);
        }
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        // A fake socket is open the instant it connects, so readiness follows connection here.
        // The real WebSocket does not (BUG-36), which is why the coordinator asks this and not
        // IsConnected -- and why TheHostRegistersItsCodeTests drives the two apart deliberately.
        public bool IsReadyToSend => IsConnected;

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));

        public void DeliverRaw(byte[] frame) => Received?.Invoke(frame);

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}
