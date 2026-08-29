using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.5c: the host creates a participant on admission and tells the admitted joiner which one it
/// is — only that joiner, and only after the decision.
/// </summary>
/// <remarks>
/// <para>
/// <b>WHAT THIS DOES NOT DELIVER, FIRST, BECAUSE IT IS THE THING MOST LIKELY TO BE MISREAD.</b> The
/// receipt is <b>in memory</b> and dies with the process, so <b>relink is still impossible and
/// A-1.9g stays RED</b> — that criterion was tightened the same day to <i>retains it across a plugin
/// restart</i> precisely because <i>"and stores it"</i> passes on a receipt that is forgotten at
/// exit. <b>If A-1.9g goes green on this, something is wrong.</b> SQ-53 ruled the in-memory cut
/// conforming, and the Engineering Lead chose it.
/// </para>
/// <para>
/// <b>And the roster still counts admissions rather than people.</b> Every admission mints, so the
/// same human joining on Monday and Tuesday is two participants, and because
/// <c>AddParticipant</c> <b>saves</b>, those duplicates reach the DM's disk permanently — no later
/// migration can disentangle them, since nothing records which duplicates were one person.
/// De-duplication needs the key this chunk <i>tells</i> the joiner, presented back through DMXENG-1
/// and DMXENG-8. <b>This must not merge as though it delivers a truthful roster.</b>
/// </para>
/// <para>
/// <b>The demonstration the ticket named as the one that matters</b> is
/// <see cref="ASecondJoinerNeverLearnsTheFirstsParticipantId"/> — <i>"the constraint a working
/// implementation is most likely to satisfy by accident today and lose later"</i>. It passes here by
/// construction rather than by accident: the relay forwards an acceptance to a single-element
/// recipient list, so a carrier on that envelope reaches nobody else.
/// </para>
/// </remarks>
public sealed class TheJoinerIsToldWhichParticipantItIsTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 29, 1, 0, 0, TimeSpan.Zero);

    // THE WHOLE HOP, over a real transport and the real codec. Fails if the host mints nothing, the
    // envelope drops the field, the joiner ignores it, or any of the three is wired to a different
    // one of the others. Every one of those is R-1.5c's own defect: a wire whose middle is missing.
    [Fact]
    public void AnAdmittedJoinerIsToldWhichParticipantItIs()
    {
        var session = new OneHostTwoJoiners();

        session.Requests(Joiner.First);
        Assert.Null(session.First.Join.ParticipantId);

        session.Admits(Joiner.First);

        Assert.NotNull(session.First.Join.ParticipantId);
        Assert.Equal(session.Minted.Single(), session.First.Join.ParticipantId);
    }

    // THE DEMONSTRATION THE TICKET CALLED THE ONE THAT MATTERS. The UUID *is* the relink claim, so a
    // participant who learns another's can present it and the DM sees a plausible returning player
    // -- D-13 and R-1.3f both put a participant's own credential outside other participants' reach.
    //
    // Fails if: the id is broadcast, put on the roster, or addressed by anything other than the
    // joiner's own key. It holds BY CONSTRUCTION rather than by a filter -- the relay forwards an
    // acceptance as Forward(JoinerAdmitted, [admitted]), a single-element list -- which is why the
    // carrier is this envelope and not a new message type whose routing would need maintaining.
    [Fact]
    public void ASecondJoinerNeverLearnsTheFirstsParticipantId()
    {
        var session = new OneHostTwoJoiners();

        session.Requests(Joiner.First);
        session.Admits(Joiner.First);
        session.Requests(Joiner.Second);
        session.Admits(Joiner.Second);

        var first = session.First.Join.ParticipantId;
        var second = session.Second.Join.ParticipantId;

        Assert.NotNull(first);
        Assert.NotNull(second);
        Assert.NotEqual(first, second);
        Assert.Equal(2, session.Minted.Distinct().Count());
    }

    // R-1.3b: an unadmitted client receives no session traffic at all, so an id arriving before the
    // decision is one this client was never entitled to. Fails if the receiver reads the field off
    // any envelope carrying it rather than off an acceptance -- which is what a "just read the
    // field" implementation does, and it would pass every test above.
    [Fact]
    public void AParticipantIdOfferedBeforeTheDecisionIsIgnored()
    {
        var session = new OneHostTwoJoiners();
        session.Requests(Joiner.First);

        session.DeliverToFirst(WireEnvelope.ForJoinPending(
            session.Code,
            session.First.JoinerKeys!.PublicKey,
            session.HostKey,
            AdmissionDeadline.DecidedByHost(Now)));

        Assert.Null(session.First.Join.ParticipantId);
    }

    // THE SAME RULE ASSERTED AT THE MODEL, because the wire test above would pass with the guard
    // removed as long as nothing happened to send one early. Fails if ToldItIsParticipant records an
    // identity for a client that is not admitted.
    [Fact]
    public void TheRecordRefusesAnIdWhileTheDecisionIsStillOpen()
    {
        var attempt = new JoinAttempt();
        attempt.Request(SessionCode.FromValid("BCDFGH"));
        attempt.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));

        attempt.ToldItIsParticipant(Guid.NewGuid());

        Assert.Null(attempt.ParticipantId);
    }

    // D-8, AND THIS IS THE ONE THAT WOULD DO REAL DAMAGE IF IT WERE WRONG. A new attempt may be to a
    // DIFFERENT session under a different host; R-1.5b binds a stored UUID under a session code, so
    // carrying the previous host's answer forward would let this client present one campaign's
    // participant to another -- the cross-campaign linkage D-8 exists to refuse.
    //
    // Fails if: Request() clears the phase and the fingerprint and leaves this behind, which is what
    // adding a field to a reset method looks like when the reset is not revisited.
    [Fact]
    public void AskingToJoinAgainDoesNotCarryTheLastSessionsParticipant()
    {
        var session = new OneHostTwoJoiners();
        session.Requests(Joiner.First);
        session.Admits(Joiner.First);
        Assert.NotNull(session.First.Join.ParticipantId);

        session.First.RequestJoin(SessionCode.FromValid("BKD7RM"), DisplayName.OrNone("Bob"));

        Assert.Null(session.First.Join.ParticipantId);
    }

    // THE MINT ITSELF, which had ZERO production callers before this chunk. Fails if the tell is
    // wired and the create is not -- a carrier with nothing to carry, which is half of exactly the
    // defect this ticket was created to fix and which reads as working from the joiner's side only
    // because null travels perfectly well.
    [Fact]
    public void AdmittingCreatesAParticipantRatherThanOnlyAnnouncingOne()
    {
        var session = new OneHostTwoJoiners();

        session.Requests(Joiner.First);
        session.Admits(Joiner.First);

        Assert.Single(session.Minted);
    }

    // THE SILENCE, CLOSED. A host with no campaign admits nobody into anything: that player can
    // never relink, next session the DM sees a stranger and approves them fresh, and NOTHING would
    // have said why. PR #86's finding 5 in a new place.
    //
    // Fails if: the warning is dropped, or names no peer. The peer code is what the assertion
    // demands because two joiners may share a display name (A-1.2d) and D-8 keeps a character name
    // out of a log -- "a participant was not created" would satisfy the letter and leave the DM
    // unable to act on it.
    [Fact]
    public void AHostWithNoCampaignSaysSoRatherThanAdmittingIntoNothing()
    {
        var log = new RecordingLog();
        var host = new SessionCoordinator(
            new CollectingTransport(), () => RelayEndpoint.Default, GraceWindow.Default, log: log);
        host.StartHosting();
        host.Host.Registered();

        using var joiner = new SessionKeyExchange();
        host.ReceiveJoinRequest(PeerCodes.Of("PRBCD2"), joiner.PublicKey, Now);
        host.Admit(PeerCodes.Of("PRBCD2"));

        var line = Assert.Single(log.Warnings, w => w.Contains("participant", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("PRBCD2", line, StringComparison.Ordinal);
        Assert.Contains("relink", line, StringComparison.OrdinalIgnoreCase);
    }

    // THE NEGATIVE HALF of the line above, and it matters as much: a warning that also fires on the
    // ordinary path is one a DM learns to skip past, and then it is not a signal when it matters.
    [Fact]
    public void NothingIsReportedWhenAParticipantWasCreated()
    {
        var session = new OneHostTwoJoiners();

        session.Requests(Joiner.First);
        session.Admits(Joiner.First);

        Assert.DoesNotContain(
            session.HostWarnings, w => w.Contains("participant", StringComparison.OrdinalIgnoreCase));
    }

    // PARSED RATHER THAN TRUSTED, at the one door it arrives through. A host controls these
    // characters and a value that is not a GUID is not a participant. Fails if it is carried inward
    // as a string for something further in to choke on -- BUG-56's shape, on a different field.
    [Fact]
    public void AParticipantIdThatIsNotAGuidIsDroppedAtTheDoor()
    {
        using var us = new SessionKeyExchange();

        Assert.True(EnvelopeCodec.TryDecode(EnvelopeCodec.Encode(Malformed(us.PublicKey)), out var envelope));

        // ADDRESSED TO US ON PURPOSE, so the PARSE is the only reason this can return null. With a
        // stranger's key the accessor would refuse it for the wrong reason and the test would pass
        // against a build that had no parsing at all -- a probe succeeding for a reason unrelated to
        // what it claims to check.
        Assert.Equal("not-a-guid", envelope!.ParticipantId);
        Assert.Null(ParticipantReceipt.TryRead(envelope, us.PublicKey));
    }

    // D-11, AND THE RELAY DOES NOT DISCHARGE IT. An honest relay forwards an acceptance to a single
    // recipient, so this can only arrive at its owner -- but D-11 assumes an attacker may control
    // the relay, which is exactly the position from which one would hand this client somebody else's
    // acceptance. The UUID IS the relink claim, so taking one addressed to another participant is
    // how a client comes to hold a credential it can present later.
    //
    // Fails if: the addressee check is dropped. THIS TEST WAS WRITTEN BECAUSE THE HARNESS FOUND IT
    // -- delivering every host frame to both joiners made the second joiner take the first's id, and
    // the first version of this feature had no such check at all.
    [Fact]
    public void AnAcceptanceAddressedToSomebodyElseTellsUsNothing()
    {
        using var us = new SessionKeyExchange();
        using var them = new SessionKeyExchange();
        using var host = new SessionKeyExchange();

        var theirs = WireEnvelope.ForJoinAccepted(
            SessionCode.FromValid("BCDFGH"), them.PublicKey, host.PublicKey, Guid.NewGuid());

        Assert.NotNull(ParticipantReceipt.TryRead(theirs, them.PublicKey));
        Assert.Null(ParticipantReceipt.TryRead(theirs, us.PublicKey));
    }

    // D-14. Fails if: the new field makes an acceptance unreadable to a build that has never heard
    // of it. Asserted by decoding an envelope that carries the field and checking that everything
    // ELSE about it still arrives -- which is what an older peer's decode amounts to.
    [Fact]
    public void AnAcceptanceCarryingTheFieldStillDecodesEverythingElse()
    {
        using var joiner = new SessionKeyExchange();
        using var host = new SessionKeyExchange();
        var sent = WireEnvelope.ForJoinAccepted(
            SessionCode.FromValid("BCDFGH"), joiner.PublicKey, host.PublicKey, Guid.NewGuid());

        Assert.True(EnvelopeCodec.TryDecode(EnvelopeCodec.Encode(sent), out var arrived));

        Assert.Equal(WireMessageType.JoinAccepted, arrived!.Type);
        Assert.Equal(joiner.PublicKey, arrived.PublicKey);
        Assert.Equal(host.PublicKey, arrived.HostPublicKey);
        // ADDRESSED TO joiner, so this asserts that it still reads as an OUTCOME rather than that
        // BUG-85's addressee check happens to let it through. Passing a stranger's key here would
        // return null for the right reason and prove nothing about D-14, which is what this test is
        // for.
        Assert.NotNull(arrived.TryGetAdmissionOutcome(joiner.PublicKey));
    }

    // A claim and an answer are DIFFERENT FACTS travelling in OPPOSITE DIRECTIONS, and they are
    // separate fields for that reason. Fails if one field is made to serve both -- at which point
    // direction is the only thing telling them apart, and the wire does not carry direction.
    [Fact]
    public void TheHostsAnswerIsNotTheJoinersClaim()
    {
        using var joiner = new SessionKeyExchange();
        using var host = new SessionKeyExchange();
        var claimed = Guid.NewGuid();

        var request = WireEnvelope.ForRelinkRequest(SessionCode.FromValid("BCDFGH"), joiner.PublicKey, claimed);
        var answer = WireEnvelope.ForJoinAccepted(
            SessionCode.FromValid("BCDFGH"), joiner.PublicKey, host.PublicKey, Guid.NewGuid());

        Assert.Null(request.ParticipantId);
        Assert.Null(ParticipantReceipt.TryRead(request, joiner.PublicKey));
        Assert.Null(answer.ClaimedParticipantId);
        Assert.NotNull(ParticipantReceipt.TryRead(answer, joiner.PublicKey));
    }

    private static WireEnvelope Malformed(byte[] joinerPublicKey)
    {
        using var host = new SessionKeyExchange();

        // Built through the codec's own internal shape, because the FACTORY WILL NOT EMIT A BAD
        // GUID -- which is correct of the factory and exactly why a hostile host has to be modelled
        // this way. Reachable only because Core makes its internals visible to this assembly; no
        // test-only door was added to production to make this expressible.
        return WireEnvelope.FromWire(
            WireMessageType.JoinAccepted,
            "BCDFGH",
            new WireShape
            {
                PublicKey = joinerPublicKey,
                HostPublicKey = host.PublicKey,
                ParticipantId = "not-a-guid",
            });
    }

    private enum Joiner
    {
        First,
        Second,
    }

    /// <summary>One host and two joiners, so "only the joiner it belongs to" is observable.</summary>
    private sealed class OneHostTwoJoiners
    {
        private readonly CollectingTransport _hostTransport = new();
        private readonly CollectingTransport _firstTransport = new();
        private readonly CollectingTransport _secondTransport = new();
        private readonly RecordingLog _hostLog = new();

        public OneHostTwoJoiners()
        {
            Host = new SessionCoordinator(
                _hostTransport,
                () => RelayEndpoint.Default,
                GraceWindow.Default,
                log: _hostLog,
                mintParticipant: _ =>
                {
                    var id = Guid.NewGuid();
                    Minted.Add(id);
                    return id;
                });

            Host.StartHosting();
            Host.Host.Registered();
            _hostTransport.Sent.Clear();

            First = new SessionCoordinator(
                _firstTransport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance);
            Second = new SessionCoordinator(
                _secondTransport, () => RelayEndpoint.Default, GraceWindow.Default, log: SilentLog.Instance);

            Code = Host.Host.Code!.Value;
            HostKey = Host.HostKeys!.PublicKey;
        }

        public SessionCoordinator Host { get; }

        public SessionCoordinator First { get; }

        public SessionCoordinator Second { get; }

        public SessionCode Code { get; }

        public byte[] HostKey { get; }

        public List<Guid> Minted { get; } = new();

        public IReadOnlyList<string> HostWarnings => _hostLog.Warnings;

        public void Requests(Joiner who)
        {
            var (client, _) = Sides(who);
            client.RequestJoin(Code, DisplayName.OrNone(who.ToString()));
            Host.ReceiveJoinRequest(CodeOf(who), client.JoinerKeys!.PublicKey, Now);
            Pump();
        }

        public void Admits(Joiner who)
        {
            Host.Admit(CodeOf(who));
            Pump();
        }

        public void DeliverToFirst(WireEnvelope envelope) => _firstTransport.Deliver(envelope);

        // EVERY host frame reaches BOTH joiners, deliberately. The relay addresses an acceptance to
        // one recipient, so a faithful harness would hide the very property under test behind its
        // own routing -- and a bug where the id went to everyone would look identical to a pass.
        // Delivering to both makes the receiver's own gate the thing being asserted.
        private void Pump()
        {
            foreach (var frame in _hostTransport.Sent.ToArray())
            {
                _firstTransport.DeliverRaw(frame);
                _secondTransport.DeliverRaw(frame);
            }

            _hostTransport.Sent.Clear();
            First.Tick(TimeSpan.Zero, Now);
            Second.Tick(TimeSpan.Zero, Now);
        }

        private (SessionCoordinator Client, CollectingTransport Transport) Sides(Joiner who) =>
            who == Joiner.First ? (First, _firstTransport) : (Second, _secondTransport);

        private static PeerCode CodeOf(Joiner who) =>
            PeerCodes.Of(who == Joiner.First ? "PRBCD2" : "JNKBCD");
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

    private sealed class CollectingTransport : ISessionTransport
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

        public void DeliverRaw(byte[] frame) => Received?.Invoke(frame);
    }
}
