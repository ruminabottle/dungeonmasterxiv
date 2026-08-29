using System;
using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-87. An admission answer addressed to a different client is dropped — correctly — and said
/// nothing about it, so the one place D-11's addressing rule actually refuses something left no
/// trace of having done so.
/// </summary>
/// <remarks>
/// <para>
/// <b>The drop is right and is not what this tests.</b> Acting on somebody else's admission answer
/// is the failure D-11 exists to prevent. What was missing is that a client could be handed another
/// client's answer — by a routing fault, or by something hand-rolled putting traffic on the wire —
/// and nobody would ever know it had happened.
/// </para>
/// <para>
/// <b>DRIVEN THROUGH THE PUBLIC SURFACE, WHICH IS WHAT KEEPS THE PROOF ALIVE.</b> A coordinator, a
/// real envelope on a real transport, a recording log. The alternative — reaching into
/// <c>Drain</c> — would pin the proof to an internal shape, and this method was restructured twice
/// in one evening. That is also the argument that retired this ticket's deferrability hold: a proof
/// through the public surface survives a behaviour-preserving restructure.
/// </para>
/// <para>
/// <b>The signal is driven by an ACTUALLY-DROPPED answer, not asserted alongside one.</b> Every case
/// below checks the join state as well as the log, so a line recorded while the answer was quietly
/// applied would fail rather than pass.
/// </para>
/// </remarks>
public class AMisaddressedAdmissionAnswerIsObservableTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    private static readonly DateTimeOffset Now = new(2026, 8, 29, 21, 0, 0, TimeSpan.Zero);

    // THE CRITERION. Fails before the fix, where the drop is silent.
    [Fact]
    public void AnAnswerForSomebodyElseTellsTheDeveloperItWasDropped()
    {
        var (coordinator, transport, log) = JoinerAwaitingADecision();

        transport.Deliver(WireEnvelope.ForJoinAccepted(Code, Stranger(), new SessionKeyExchange().PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.NotEmpty(log.Warnings);
    }

    // THE PREMISE, and for this fix it carries as much weight as the criterion. A line saying an
    // answer was discarded, written while the answer was in fact APPLIED, would be worse than
    // silence -- it would be a false record of a refusal that never happened. So the same delivery
    // is checked for having changed nothing.
    [Fact]
    public void AndTheAnswerWasGenuinelyDroppedRatherThanApplied()
    {
        var (coordinator, transport, _) = JoinerAwaitingADecision();

        transport.Deliver(WireEnvelope.ForJoinAccepted(Code, Stranger(), new SessionKeyExchange().PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.AwaitingDecision, coordinator.Join.Phase);
        Assert.Null(coordinator.Membership.SessionKey);
    }

    // THE VACUITY CONTROL, and the one that would catch the lazier fix. Warning on every null from
    // TryGetAdmissionOutcome satisfies the criterion above and makes the signal worthless: the null
    // that means "not an admission answer at all" arrives on ordinary traffic, so a line there fires
    // constantly. THE CLIENT'S OWN ANSWER MUST BE SILENT.
    [Fact]
    public void TheClientsOwnAnswerOverTheSameWireSaysNothing()
    {
        var (coordinator, transport, log) = JoinerAwaitingADecision();

        transport.Deliver(WireEnvelope.ForJoinAccepted(
            Code, coordinator.Membership.Keys!.PublicKey, new SessionKeyExchange().PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.Admitted, coordinator.Join.Phase);
        Assert.Empty(log.Warnings);
    }

    // THE SECOND VACUITY CONTROL, aimed at the OTHER null -- and it has to reach the same line to
    // mean anything. A fix that warned on every null from TryGetAdmissionOutcome satisfies the
    // criterion above and makes the signal worthless, because that null is the ordinary case.
    //
    // CodeAccepted RATHER THAN JoinPending, AND THE DIFFERENCE IS THE WHOLE TEST. ApplyOutcome is a
    // FALLTHROUGH -- six Try* arms get first refusal, and TryPendingNotice consumes JoinPending, so
    // a JoinPending envelope never reaches the line under test. That was this test's first version:
    // it passed, it looked like a control, and it could not have failed. Measured, not reasoned --
    // the always-warn mutation produced no reds until this row pointed at a type that gets there.
    [Fact]
    public void AnEnvelopeThatIsNotAnAdmissionAnswerSaysNothing()
    {
        var (coordinator, transport, log) = JoinerAwaitingADecision();

        transport.Deliver(WireEnvelope.ForCodeAccepted(Code));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Empty(log.Warnings);
    }

    // EVERY ANSWER TYPE, not the one that exposed it. Accepted, denied and lapsed are three arms of
    // one switch, and a fix that reached only the arm in the bug report would leave two drops silent
    // behind a suite reporting success.
    [Theory]
    [InlineData(WireMessageType.JoinDenied)]
    [InlineData(WireMessageType.JoinLapsed)]
    public void EveryKindOfAnswerForSomebodyElseIsObserved(WireMessageType type)
    {
        var (coordinator, transport, log) = JoinerAwaitingADecision();

        transport.Deliver(type is WireMessageType.JoinDenied
            ? WireEnvelope.ForJoinDenied(Code, Stranger())
            : WireEnvelope.ForJoinLapsed(Code, Stranger()));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.NotEmpty(log.Warnings);
        Assert.Equal(JoinPhase.AwaitingDecision, coordinator.Join.Phase);
    }

    // D-8, and it is a requirement rather than tidiness: the addressee IS a public key, which is the
    // cross-session identifier BUG-117 established the relay must not be able to link a person by.
    // A log line is the artifact most likely to be pasted into a bug report.
    [Fact]
    public void TheLineNamesNoKey()
    {
        var (coordinator, transport, log) = JoinerAwaitingADecision();
        var stranger = Stranger();

        transport.Deliver(WireEnvelope.ForJoinAccepted(Code, stranger, new SessionKeyExchange().PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        var line = Assert.Single(log.Warnings);
        Assert.DoesNotContain(Convert.ToBase64String(stranger), line, StringComparison.Ordinal);
        Assert.DoesNotContain(
            Convert.ToBase64String(coordinator.Membership.Keys!.PublicKey), line, StringComparison.Ordinal);
    }

    /// <summary>Some other client's join key — a real one, minted the way the product mints them.</summary>
    private static byte[] Stranger() => new SessionKeyExchange().PublicKey;

    /// <summary>
    /// A joiner that has asked to join and is waiting to be told — the only state in which an
    /// admission answer is meaningful, and therefore the only one where dropping another client's
    /// is worth remarking on.
    /// </summary>
    private static (SessionCoordinator Coordinator, FakeTransport Transport, RecordingLog Log)
        JoinerAwaitingADecision()
    {
        var log = new RecordingLog();
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, log: log,
            capabilities: SessionCapabilities.Default);

        coordinator.RequestJoin(Code);
        coordinator.Join.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));

        Assert.Equal(JoinPhase.AwaitingDecision, coordinator.Join.Phase);

        return (coordinator, transport, log);
    }

    // The per-file transport double this assembly uses. Copied in rather than shared, which is the
    // convention here -- twenty test files carry their own.
    private sealed class FakeTransport : ISessionTransport
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

    private sealed class RecordingLog : ISessionTransportLog
    {
        public List<string> Warnings { get; } = [];

        public void Information(string message)
        {
        }

        public void Warning(string message) => Warnings.Add(message);

        public void Warning(Exception exception, string message) => Warnings.Add(message);
    }
}
