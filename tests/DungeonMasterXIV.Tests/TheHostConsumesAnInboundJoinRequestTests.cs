using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-42: the host turns a JoinRequest that actually arrived into a prompt the DM can answer.
/// </summary>
/// <remarks>
/// <para>
/// <b>Every assertion here is about what the host did with a frame it RECEIVED.</b> The frame is a
/// real <see cref="WireEnvelope"/> put through <see cref="EnvelopeCodec.Encode"/> and handed to the
/// transport's <c>Received</c> event, so the production
/// <see cref="EnvelopeCodec.TryDecode"/> is what parses it. Nothing here scripts the host side.
/// </para>
/// <para>
/// <b>That distinction is the whole reason this bug survived.</b>
/// <c>JoinOverASocketTests.AJoinCompletesAcrossARealSocket</c> passed throughout BUG-40 while the
/// joiner sent nothing at all: it drives a double, calls the decision path by hand, and never
/// asserts anything crossed the wire. A test written the same way on this side would pass
/// throughout BUG-42 — the consumer exists and is well tested, and nothing routed to it.
/// </para>
/// </remarks>
public class TheHostConsumesAnInboundJoinRequestTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 3, 0, 0, TimeSpan.Zero);

    // The bug itself. Fails on origin/main because AdmissionInbox has no arm for JoinRequest: the
    // frame decodes, matches nothing, and is dropped, so Pending stays empty and no prompt appears.
    [Fact]
    public void AJoinRequestThatArrivesBecomesAPendingAdmission()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, joiner.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        var pending = Assert.Single(coordinator.Admissions.Pending);
        Assert.Equal(joiner.PublicKey, pending.JoinerPublicKey);
    }

    // The prompt is only useful if the DM and the joiner read the SAME string, so the fingerprint
    // has to be the one computed from both keys rather than anything the host invented.
    [Fact]
    public void ThePromptShowsTheFingerprintOfBothKeys()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, joiner.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(
            KeyFingerprint.Of(joiner.PublicKey, coordinator.HostKeys!.PublicKey),
            Assert.Single(coordinator.Admissions.Pending).Fingerprint);
    }

    // R-1.3a-i: the host's key goes back while the decision is still open, or the joiner cannot
    // compare anything in time. Asserted on what left the transport, not on host state.
    [Fact]
    public void TheHostAnswersWithItsOwnKeyWhileTheDecisionIsStillOpen()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, joiner.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        var pending = Sent(transport).Single(e => e.Type == WireMessageType.JoinPending);
        Assert.Equal(coordinator.HostKeys!.PublicKey, pending.HostPublicKey);
        Assert.Equal(joiner.PublicKey, pending.PublicKey);
    }

    // The admission has to be answerable by the code the prompt shows, or the DM sees a request they
    // cannot act on -- which would satisfy "Pending is non-empty" while joining stayed broken.
    [Fact]
    public void TheDmCanAdmitTheRequestTheyWereShown()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, joiner.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        coordinator.Admit(Assert.Single(coordinator.Admissions.Pending).PeerCode);

        var accepted = Sent(transport).Single(e => e.Type == WireMessageType.JoinAccepted);
        Assert.Equal(joiner.PublicKey, accepted.PublicKey);
    }

    // Two joiners must not collapse into one prompt, which a peer code that ignored who was asking
    // would do -- and the DM would admit one person believing they had admitted the other.
    [Fact]
    public void TwoJoinersProduceTwoDistinctPrompts()
    {
        var (coordinator, transport) = Hosting();
        using var first = new SessionKeyExchange();
        using var second = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, first.PublicKey));
        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, second.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(2, coordinator.Admissions.Pending.Count);
        Assert.Equal(2, coordinator.Admissions.Pending.Select(p => p.PeerCode).Distinct().Count());
    }

    // A-1.2a, added by the Spec Owner when they answered SQ-16: the SAME joiner presenting to two
    // different sessions must get two different peer codes. "A value that is identical across two
    // session codes fails, even though it renders correctly in both prompts" -- so the failing input
    // is a derivation that ignores the session, and one key is reused across both hosts to make that
    // the only thing that can vary. D-8: the same person must not present the same value twice.
    [Fact]
    public void TheSameJoinerGetsADifferentPeerCodeInEachSession()
    {
        using var joiner = new SessionKeyExchange();
        var (first, firstTransport) = Hosting();
        var (second, secondTransport) = Hosting();

        Assert.NotEqual(first.Host.Code!.Value, second.Host.Code!.Value);

        firstTransport.Deliver(WireEnvelope.ForJoinRequest(first.Host.Code!.Value, joiner.PublicKey));
        secondTransport.Deliver(WireEnvelope.ForJoinRequest(second.Host.Code!.Value, joiner.PublicKey));
        first.Tick(TimeSpan.Zero, Now);
        second.Tick(TimeSpan.Zero, Now);

        Assert.NotEqual(
            Assert.Single(first.Admissions.Pending).PeerCode,
            Assert.Single(second.Admissions.Pending).PeerCode);
    }

    // The other half of A-1.2a's first clause, so the pair cannot be satisfied by a value that is
    // simply random: within ONE session the same joiner is the same requester, and a code that
    // changed per frame would make the DM's prompt unanswerable.
    [Fact]
    public void TheSameJoinerKeepsOnePeerCodeWithinASession()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, joiner.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);
        var first = Assert.Single(coordinator.Admissions.Pending).PeerCode;

        coordinator.Admissions.Clear();
        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, joiner.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(first, Assert.Single(coordinator.Admissions.Pending).PeerCode);
    }

    // A-1.2d, the half that is reachable today. The criterion is "two concurrent requesters sending
    // the SAME display name remain distinguishable, and admitting one does not admit the other" --
    // a case which WILL occur, because a name is self-declared and nothing verifies it.
    //
    // The display name does not exist yet: nothing in production carries one and putting it on the
    // wire is outside this fix's boundary. What IS testable now is the half that makes the criterion
    // satisfiable at all -- the requesters are told apart by something a duplicate name cannot
    // collapse, and the DM's decision lands on exactly the one they chose.
    //
    // BOTH POSITIONS, and that is the whole point of the Theory. The first version of this test
    // admitted whichever requester arrived FIRST, and it passed with the peer-code lookup replaced
    // by `_pending.FirstOrDefault()` -- ignoring the code entirely. Admitting the head of the list
    // is the right answer for the wrong reason, so the test was order-dependent where A-1.2d is
    // entirely about the code. Running both positions makes that blindness unrepresentable rather
    // than fixed once: a future change to _pending's ordering cannot quietly restore it.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    public void AdmittingOneRequesterDoesNotAdmitTheOther(int admitted)
    {
        var (coordinator, transport) = Hosting();
        using var first = new SessionKeyExchange();
        using var second = new SessionKeyExchange();
        var keys = new[] { first.PublicKey, second.PublicKey };

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, first.PublicKey));
        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, second.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        var chosen = coordinator.Admissions.Pending
            .Single(p => p.JoinerPublicKey!.SequenceEqual(keys[admitted]));
        coordinator.Admit(chosen.PeerCode);

        // The other is still waiting on the DM, not silently let in alongside.
        var stillPending = Assert.Single(coordinator.Admissions.Pending);
        Assert.Equal(keys[1 - admitted], stillPending.JoinerPublicKey);

        // And exactly one acceptance went out, addressed to the one the DM chose -- not merely to
        // someone. A lookup that ignores the peer code fails here on admitted: 1.
        var accepted = Sent(transport).Where(e => e.Type == WireMessageType.JoinAccepted).ToList();
        Assert.Equal(keys[admitted], Assert.Single(accepted).PublicKey);
    }

    // A client that is not hosting must not build prompts out of traffic addressed to a host.
    [Fact]
    public void AClientThatIsNotHostingIgnoresAJoinRequest()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance);

        transport.Deliver(WireEnvelope.ForJoinRequest(SessionCodeGenerator.Next(), new byte[] { 1, 2, 3 }));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Empty(coordinator.Admissions.Pending);
    }

    // R-1.3e end to end, and it crosses the seam rather than testing either side of it: the name is
    // put on a real envelope, encoded, decoded by the production codec, and read back out of the
    // headline the DM is shown. A test that asserted ForJoinRequest carried the name, plus one that
    // asserted Headline rendered a PendingAdmission's name, would both pass while nothing joined
    // the two -- which is the shape that let BUG-40 and BUG-42 through on this exact path.
    [Fact]
    public void ANameSentOnTheWireReachesThePromptTheDmIsShown()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(
            coordinator.Host.Code!.Value, joiner.PublicKey, DisplayName.OrNone("Ysera Nightsong")));
        coordinator.Tick(TimeSpan.Zero, Now);

        var request = Assert.Single(coordinator.Admissions.Pending);
        Assert.Equal("Ysera Nightsong", request.DisplayName.Value);
        Assert.Contains("Ysera Nightsong", AdmissionPrompt.Headline(request));
    }

    // A-1.2b's other half, asserted where it can be: the prompt must carry the fingerprint as well
    // as the name. The fingerprint is rendered by the window, but it comes from here -- so if this
    // ever arrives empty, the window has nothing to show and D-8's approve-blocking rule is broken
    // by the model rather than by the drawing.
    [Fact]
    public void ARequestCarriesAFingerprintAsWellAsAName()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(
            coordinator.Host.Code!.Value, joiner.PublicKey, DisplayName.OrNone("Bob")));
        coordinator.Tick(TimeSpan.Zero, Now);

        var request = Assert.Single(coordinator.Admissions.Pending);
        Assert.NotEmpty(request.Fingerprint);
        Assert.True(request.DisplayName.WasStated);
    }

    // A hostile name must not be able to SUPPRESS a prompt. Refusing the name and dropping the
    // request would hand any joiner a way to make themselves invisible to the DM -- a worse outcome
    // than the spoofing the refusal exists to prevent. The request arrives; only the name is lost.
    [Fact]
    public void ANameTheHostRefusesDoesNotCostTheJoinerTheirPrompt()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(
            coordinator.Host.Code!.Value,
            joiner.PublicKey,
            DisplayName.OrNone("Bob" + (char)10 + "Code to compare: forged")));
        coordinator.Tick(TimeSpan.Zero, Now);

        var request = Assert.Single(coordinator.Admissions.Pending);
        Assert.False(request.DisplayName.WasStated);
        Assert.DoesNotContain("forged", AdmissionPrompt.Headline(request));
        Assert.NotEmpty(request.Fingerprint);
    }

    // An older build sends no name at all (D-14: the wire only grows, and a peer that omits a field
    // is not making a statement). It must still be admittable, with a prompt that reads as a person
    // rather than as a fault.
    [Fact]
    public void AJoinerFromAnOlderBuildStillGetsAPrompt()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinRequest(coordinator.Host.Code!.Value, joiner.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        var request = Assert.Single(coordinator.Admissions.Pending);
        Assert.False(request.DisplayName.WasStated);
        Assert.Contains(DisplayName.Unstated, AdmissionPrompt.Headline(request));
    }

    private static (SessionCoordinator Coordinator, FakeTransport Transport) Hosting()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance);
        coordinator.StartHosting();
        coordinator.Host.Registered();
        transport.Sent.Clear();
        return (coordinator, transport);
    }

    private static List<WireEnvelope> Sent(FakeTransport transport) =>
        transport.Sent
            .Select(bytes => EnvelopeCodec.TryDecode(bytes, out var e) ? e : null)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);

        /// <summary>Puts a real encoded frame on the wire, the way the relay would.</summary>
        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}
