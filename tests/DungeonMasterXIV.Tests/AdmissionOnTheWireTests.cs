using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The half PR #19 named as unreached: an admission decision that never leaves the machine is not a
/// decision the other party can act on.
/// </summary>
public class AdmissionOnTheWireTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 3, 0, 0, TimeSpan.Zero);

    // R-1.3b and A-1.4's positive half. Fails if: admitting updates local state only, which is what
    // shipped in PR #19 — the DM sees a player in the session and the player never hears anything.
    [Fact]
    public void AdmittingSendsAnAcceptanceCarryingTheHostsKey()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest("PRBCD2", joiner.PublicKey, Now);

        coordinator.Admit("PRBCD2");

        var sent = Decode(transport).Single(e => e.Type == WireMessageType.JoinAccepted);
        Assert.Equal(joiner.PublicKey, sent.PublicKey);
        Assert.Equal(coordinator.HostKeys!.PublicKey, sent.HostPublicKey);
    }

    // Proves the acceptance is usable rather than merely present: the joiner derives the same key
    // the host will seal with. Fails if the wrong key is echoed, which a null check would not catch.
    [Fact]
    public void TheAdmittedJoinerCanDeriveTheSessionKeyFromWhatItReceives()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest("PRBCD2", joiner.PublicKey, Now);
        coordinator.Admit("PRBCD2");

        var acceptance = Decode(transport).Single(e => e.Type == WireMessageType.JoinAccepted);
        var code = coordinator.Host.Code!.Value;

        var joinerKey = acceptance.TryGetAdmissionOutcome()!
            .Match(hostKey => joiner.DeriveSharedKey(hostKey, code), () => null!, () => null!);

        Assert.Equal(coordinator.HostKeys!.DeriveSharedKey(joiner.PublicKey, code), joinerKey);
    }

    // R-1.3b: denial is an explicit message, not silence. Fails if: denying is local-only, which
    // leaves the refused player unable to tell refusal from a broken relay, a wrong code, or a DM
    // who has not looked yet — R-1.8's ambiguity arriving through another door.
    [Fact]
    public void DenyingSendsAnExplicitRefusalRatherThanSilence()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest("PRBCD2", joiner.PublicKey, Now);

        coordinator.Deny("PRBCD2");

        Assert.Contains(Decode(transport), e => e.Type == WireMessageType.JoinDenied);
    }

    // R-1.3b's other half. Fails if: a denied client is left addressable, which would put session
    // traffic — even ciphertext they cannot read — in front of someone at D-13's None level.
    [Fact]
    public void ADeniedClientIsNotAddressableAndReceivesNoSessionTraffic()
    {
        var (coordinator, _) = Hosting();
        using var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest("PRBCD2", joiner.PublicKey, Now);

        coordinator.Deny("PRBCD2");

        Assert.False(coordinator.Audience.IsAdmitted(PeerCodes.Of("PRBCD2")));
        Assert.Empty(coordinator.Audience.Recipients);
    }

    // R-1.3c and A-1.5h on the wire. Fails if: a lapse is announced as a denial, or not announced at
    // all. Both leave the player worse off than the truth — one lies, the other is the fifteen
    // silent minutes the requirement exists to end.
    [Fact]
    public void ALapsedRequestIsAnnouncedAsLapsedAndNotAsDenied()
    {
        var (coordinator, transport) = Hosting();
        using var joiner = new SessionKeyExchange();
        coordinator.ReceiveJoinRequest("PRBCD2", joiner.PublicKey, Now);

        coordinator.Tick(TimeSpan.Zero, Now.Add(AdmissionDeadline.Window));

        var sent = Decode(transport);
        Assert.Contains(sent, e => e.Type == WireMessageType.JoinLapsed);
        Assert.DoesNotContain(sent, e => e.Type == WireMessageType.JoinDenied);
    }

    // Fails if: the fingerprint is taken from a caller rather than computed from the keys actually
    // exchanged. A fingerprint that does not match the keys is worse than none — the DM compares it,
    // it agrees, and they conclude the channel is clean.
    [Fact]
    public void TheFingerprintOnThePromptIsComputedFromBothRealKeys()
    {
        var (coordinator, _) = Hosting();
        using var joiner = new SessionKeyExchange();

        var request = coordinator.ReceiveJoinRequest("PRBCD2", joiner.PublicKey, Now);

        Assert.Equal(
            KeyFingerprint.Of(joiner.PublicKey, coordinator.HostKeys!.PublicKey),
            request!.Fingerprint);
    }

    // R-1.4: losing the relay starts the grace window, it does not end the session. Fails if: a drop
    // is treated as an immediate end, which is the instant kick the product decision rules out.
    [Fact]
    public void LosingTheRelayWhileHostingStartsTheGraceWindowRatherThanEndingTheSession()
    {
        var (coordinator, _) = Hosting();

        coordinator.Fail(SessionFailure.ConnectionLost);

        Assert.True(coordinator.Grace.IsRunning);
        Assert.Equal(HostingPhase.Hosting, coordinator.Host.Phase);
    }

    // Fails if: expiry does not end the session, leaving clients showing stale state as though live.
    [Fact]
    public void LettingTheGraceWindowExpireEndsTheSession()
    {
        var (coordinator, _) = Hosting();
        coordinator.Fail(SessionFailure.ConnectionLost);

        coordinator.Tick(GraceWindow.Default, Now);

        Assert.Equal(HostingPhase.NotHosting, coordinator.Host.Phase);
    }

    // Obligation 3, now reached. Fails if: a code taken during the window is swapped in silently.
    [Fact]
    public void ReconnectingToAStolenCodeTellsTheDmTheirPlayersHoldTheOldOne()
    {
        var (coordinator, _) = Hosting();
        var original = coordinator.Host.Code!.Value;
        coordinator.Fail(SessionFailure.ConnectionLost);

        coordinator.HostReconnectedWithNewCode();

        Assert.True(coordinator.Host.CodeChangedMidSession);
        Assert.Equal(original, coordinator.Host.SupersededCode);
        Assert.NotEqual(original, coordinator.Host.Code);
        Assert.False(coordinator.Grace.IsRunning);
    }

    // The ordinary case, so the pair cannot be satisfied by always reporting a change.
    [Fact]
    public void ReconnectingWithTheSameCodeReportsNoChange()
    {
        var (coordinator, _) = Hosting();
        coordinator.Fail(SessionFailure.ConnectionLost);

        coordinator.HostReconnected();

        Assert.False(coordinator.Host.CodeChangedMidSession);
        Assert.False(coordinator.Grace.IsRunning);
    }

    // D-8: the host's key pair is ephemeral per session. Fails if: it survives a session, which
    // would be an identifier linking a player across two session codes.
    [Fact]
    public void TheHostsKeysDoNotSurviveTheSession()
    {
        var (coordinator, _) = Hosting();
        Assert.NotNull(coordinator.HostKeys);

        coordinator.StopHosting();

        Assert.Null(coordinator.HostKeys);
    }

    private static (SessionCoordinator Coordinator, FakeTransport Transport) Hosting()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default);
        coordinator.StartHosting();
        coordinator.Host.Registered();
        transport.Sent.Clear();
        return (coordinator, transport);
    }

    private static List<WireEnvelope> Decode(FakeTransport transport) =>
        transport.Sent
            .Select(bytes => EnvelopeCodec.TryDecode(bytes, out var e) ? e : null)
            .Where(e => e is not null)
            .Select(e => e!)
            .ToList();

    private sealed class FakeTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));

        public void DeliverRaw(byte[] frame) => Received?.Invoke(frame);

        public bool IsConnected { get; private set; }

        // A fake socket is open the instant it connects, so readiness follows connection here.
        // The real WebSocket does not (BUG-36), which is why the coordinator asks this and not
        // IsConnected -- and why TheHostRegistersItsCodeTests drives the two apart deliberately.
        public bool IsReadyToSend => IsConnected;

        public List<byte[]> Sent { get; } = new();

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}

/// <summary>
/// The joiner's half of R-1.3b and R-1.3c: what happens when an answer actually arrives.
/// </summary>
public class AdmissionReceivedTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 27, 3, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    // A-1.3. Fails if: an acceptance never reaches the joiner's state — which is where this stood
    // before the receive path existed, with the host sending and nobody listening.
    [Fact]
    public void AnArrivingAcceptancePutsTheJoinerInTheSession()
    {
        var (coordinator, transport) = Joining();
        using var host = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinAccepted(Code, coordinator.JoinerKeys!.PublicKey, host.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.Admitted, coordinator.Join.Phase);
        Assert.True(coordinator.Join.MayReceiveSessionState);
    }

    // The acceptance is only useful if the key it carries is. Fails if: the host key is ignored,
    // leaving an admitted joiner routed and unable to decrypt anything.
    [Fact]
    public void AnAdmittedJoinerDerivesTheSameKeyTheHostWillSealWith()
    {
        var (coordinator, transport) = Joining();
        using var host = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinAccepted(Code, coordinator.JoinerKeys!.PublicKey, host.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(
            host.DeriveSharedKey(coordinator.JoinerKeys!.PublicKey, Code),
            coordinator.SessionKey);
    }

    // A-1.4. Fails if: a denial is dropped, leaving the player on an indefinite spinner — R-1.8's
    // ambiguity arriving through R-1.3b's door.
    [Fact]
    public void AnArrivingDenialIsShownAsRefusalAndNoStateFlows()
    {
        var (coordinator, transport) = Joining();
        using var host = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinDenied(Code, coordinator.JoinerKeys!.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.Denied, coordinator.Join.Phase);
        Assert.False(coordinator.Join.MayReceiveSessionState);
        Assert.Null(coordinator.SessionKey);
    }

    // A-1.5h across the wire. Fails if: a lapse arrives as a denial, which tells someone they were
    // turned away when nobody looked — and stops them re-requesting, which they are entitled to do.
    [Fact]
    public void AnArrivingLapseIsShownAsLapsedAndStaysReRequestable()
    {
        var (coordinator, transport) = Joining();

        transport.Deliver(WireEnvelope.ForJoinLapsed(Code, coordinator.JoinerKeys!.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.Lapsed, coordinator.Join.Phase);
        Assert.NotEqual(JoinPhase.Denied, coordinator.Join.Phase);
        Assert.True(coordinator.Join.MayRequestAgain);
    }

    // D-14 at the consumer. Fails if: an unrecognised type reaches a handler, or takes the client
    // down. A newer relay adding a message must not break an installed plugin.
    [Fact]
    public void AFrameFromANewerRelayIsIgnoredRatherThanCrashing()
    {
        var (coordinator, transport) = Joining();

        transport.DeliverRaw(System.Text.Encoding.UTF8.GetBytes("{\"Type\":9999,\"SessionCode\":\"BKD7RM\"}"));

        Assert.Null(Record.Exception(() => coordinator.Tick(TimeSpan.Zero, Now)));
        Assert.Equal(JoinPhase.AwaitingDecision, coordinator.Join.Phase);
    }

    // Fails if: a malformed frame throws on the receive path. Anything can arrive from a relay, and
    // a frame that does not parse must not take the game client down.
    [Fact]
    public void AMalformedFrameIsDroppedWithoutThrowing()
    {
        var (coordinator, transport) = Joining();

        transport.DeliverRaw(System.Text.Encoding.UTF8.GetBytes("not json at all"));

        Assert.Null(Record.Exception(() => coordinator.Tick(TimeSpan.Zero, Now)));
    }

    // Fails if: frames are applied on the socket thread. Mutating session state from a receive
    // callback races the draw, which is why arrival and application are separated by the tick.
    [Fact]
    public void AnArrivingFrameChangesNothingUntilTheNextTick()
    {
        var (coordinator, transport) = Joining();
        using var host = new SessionKeyExchange();

        transport.Deliver(WireEnvelope.ForJoinAccepted(Code, coordinator.JoinerKeys!.PublicKey, host.PublicKey));

        Assert.Equal(JoinPhase.AwaitingDecision, coordinator.Join.Phase);
    }

    private static (SessionCoordinator Coordinator, FakeTransport Transport) Joining()
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(transport, () => RelayEndpoint.Default, GraceWindow.Default);
        coordinator.RequestJoin(Code);
        coordinator.Join.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));
        return (coordinator, transport);
    }

    private sealed class FakeTransport : ISessionTransport
    {
        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public bool IsConnected { get; private set; }

        // A fake socket is open the instant it connects, so readiness follows connection here.
        // The real WebSocket does not (BUG-36), which is why the coordinator asks this and not
        // IsConnected -- and why TheHostRegistersItsCodeTests drives the two apart deliberately.
        public bool IsReadyToSend => IsConnected;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope)
        {
        }

        public void Deliver(WireEnvelope envelope) => Received?.Invoke(EnvelopeCodec.Encode(envelope));

        public void DeliverRaw(byte[] frame) => Received?.Invoke(frame);

        public void RaiseFailure(SessionFailure failure) => Failed?.Invoke(failure);
    }
}
