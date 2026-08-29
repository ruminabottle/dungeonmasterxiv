using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// PR #86's findings 4 and 5: the two places this client drops something and told nobody.
/// </summary>
/// <remarks>
/// <para>
/// <b>Survival and silence are separable, and only survival was ever tested.</b>
/// <c>AParticipantWithAnUnusableKeyCannotBreakTheBroadcast</c> proves the loop survives a peer whose
/// key will not import — and it passed on every build where that peer was then dropped from this and
/// every future broadcast without a word. The Deployment Manager's finding 5 is that second half:
/// <i>"a participant silently omitted from this and every future broadcast is a person sitting in a
/// session hearing nothing."</i>
/// </para>
/// <para>
/// <b>These tests assert the LINE FIRES, not that the code exists.</b> A log call somebody deletes
/// while refactoring is invisible to a test that only checks the session survived — which is exactly
/// how the silence lasted through #86 in the first place. Each test here drives the PRODUCTION path
/// and reads what reached the log.
/// </para>
/// <para>
/// <b>And each has a negative half, which is the part that keeps the line honest.</b> A warning that
/// also fires on the ordinary path is noise, and noise gets filtered out by whoever reads the log —
/// so proving it stays SILENT when nothing was dropped matters as much as proving it speaks when
/// something was.
/// </para>
/// </remarks>
public sealed class ASkippedParticipantIsReportedTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 2, 0, 0, TimeSpan.Zero);
    private const string Reachable = "PRBCD2";
    private const string Unreachable = "JNKBCD";

    // FINDING 5, and the DM called it the load-bearing one. Fails if: a participant whose key will
    // not import is dropped from the broadcast without the log naming them.
    //
    // The PEER CODE is what the assertion demands, because it is the only value that identifies
    // WHICH person vanished -- two participants may share a display name (A-1.2d), and D-8 keeps a
    // character name out of a log entirely. A line saying "a participant was skipped" would satisfy
    // the finding's letter and leave the DM unable to act on it.
    [Fact]
    public void AParticipantWhoseKeyWillNotImportIsNamedInTheLog()
    {
        var (coordinator, log) = Hosting();
        using var good = new SessionKeyExchange();

        coordinator.ReceiveJoinRequest(PeerCodes.Of(Unreachable), [1, 2, 3], Now);
        coordinator.ReceiveJoinRequest(PeerCodes.Of(Reachable), good.PublicKey, Now);
        coordinator.Admit(PeerCodes.Of(Unreachable));
        coordinator.Admit(PeerCodes.Of(Reachable));

        var lines = log.Warnings.Where(w => w.Contains(Unreachable, StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(lines);
        Assert.All(lines, line => Assert.Contains("hear nothing", line, StringComparison.OrdinalIgnoreCase));
    }

    // BUG-76. THE SIBLING OF THE TEST ABOVE, AND IT DID NOT EXIST. Every test in this file gave the
    // unreachable peer the key [1, 2, 3] -- MALFORMED BUT PRESENT -- so `peer.PublicKey is not { }`
    // was false in all of them and the no-public-key branch was never entered; the failure always
    // landed one branch later, at DeriveSharedKey. The measured contrast: deleting the no-public-key
    // warning left all 849 tests green, while deleting its sibling reddened two of them by name.
    //
    // A peer with an ABSENT key is produced by answering a peer code with NO PENDING REQUEST:
    // Desk.Decide returns null, so AdmissionControl.Admit passes `request?.JoinerPublicKey` as null.
    // That is the public API and not a constructor -- AdmittedPeer's constructor is internal and
    // unreachable from this project, which is precisely why the case had to be reached this way.
    //
    // ON REACHABILITY, and it is stated rather than assumed: qa-1 recorded it as UNKNOWN and I did
    // not resolve it either. The only production caller is AdmissionPromptView, which admits a code
    // taken from a live pending request, so the case looks closed from the UI today -- but that is an
    // argument about one call site, not a proof, and both Admit overloads are PUBLIC. The branch's
    // own comment already calls it "the guard that keeps a FUTURE CALLER from creating a participant
    // who is addressable and unreachable without anyone noticing". This test is what makes that guard
    // provable rather than asserted, which is the same standing PR #86's local guard has.
    [Fact]
    public void AParticipantWithNoPublicKeyAtAllIsNamedInTheLog()
    {
        var (coordinator, log) = Hosting();
        using var good = new SessionKeyExchange();

        coordinator.ReceiveJoinRequest(PeerCodes.Of(Reachable), good.PublicKey, Now);

        // No ReceiveJoinRequest for this one: nothing is pending under it, so it is admitted with no
        // key at all rather than with an unusable one.
        coordinator.Admit(PeerCodes.Of(Unreachable));
        coordinator.Admit(PeerCodes.Of(Reachable));

        // THE PREMISE, ASSERTED. If Admit ever starts supplying a key here, this test would otherwise
        // go on passing while silently exercising the sibling branch again -- which is exactly how the
        // gap this closes was created.
        Assert.Null(coordinator.Audience.Find(PeerCodes.Of(Unreachable))!.PublicKey);

        var lines = log.Warnings.Where(w => w.Contains(Unreachable, StringComparison.Ordinal)).ToList();

        Assert.NotEmpty(lines);

        // "no public key" rather than merely "skipped": it is what distinguishes this line from its
        // sibling, and a test that accepted either would pass with the wrong branch reporting.
        Assert.All(lines, line => Assert.Contains("no public key", line, StringComparison.OrdinalIgnoreCase));
        Assert.All(lines, line => Assert.Contains("hear nothing", line, StringComparison.OrdinalIgnoreCase));
    }

    // ONE LINE PER BROADCAST, NOT ONE PER PARTICIPANT, AND THAT IS THE DECISION RATHER THAN AN
    // ACCIDENT. My first version of the test above asserted a single line and FAILED against two:
    // admitting each participant publishes, so an unreachable peer is skipped by every publish. The
    // code was right and the assertion was wrong.
    //
    // Keeping the repetition is deliberate. The finding's own words are "omitted from this AND EVERY
    // FUTURE BROADCAST" -- each publish genuinely does omit them, so each is a true report, and
    // de-duplicating would need per-peer state that could itself go stale and start suppressing a
    // report that had become new again. A reviewer who would rather see it once per peer is making a
    // noise argument I would accept; it is a different design and not a defect in this one.
    //
    // Fails if: the second broadcast goes quiet, which would mean somebody added exactly that
    // suppression and a DM reading the log later would conclude the problem had resolved itself.
    [Fact]
    public void EveryBroadcastThatOmitsThemReportsItRatherThanOnlyTheFirst()
    {
        var (coordinator, log) = Hosting();
        using var good = new SessionKeyExchange();

        coordinator.ReceiveJoinRequest(PeerCodes.Of(Unreachable), [1, 2, 3], Now);
        coordinator.ReceiveJoinRequest(PeerCodes.Of(Reachable), good.PublicKey, Now);
        coordinator.Admit(PeerCodes.Of(Unreachable));

        var afterFirst = log.Warnings.Count(w => w.Contains(Unreachable, StringComparison.Ordinal));

        coordinator.Admit(PeerCodes.Of(Reachable));

        var afterSecond = log.Warnings.Count(w => w.Contains(Unreachable, StringComparison.Ordinal));

        Assert.True(
            afterSecond > afterFirst,
            $"The second broadcast omitted {Unreachable} and said nothing: {afterFirst} line(s) "
            + $"before it, {afterSecond} after. Silence on a repeat omission reads as resolved.");
    }

    // THE NEGATIVE HALF. Fails if: the warning fires when every participant was reachable. A line
    // that appears on the ordinary path is one a reader learns to skip, and then it is not a signal
    // when it matters.
    [Fact]
    public void NothingIsReportedWhenEveryParticipantIsReachable()
    {
        var (coordinator, log) = Hosting();
        using var good = new SessionKeyExchange();

        coordinator.ReceiveJoinRequest(PeerCodes.Of(Reachable), good.PublicKey, Now);
        coordinator.Admit(PeerCodes.Of(Reachable));

        Assert.Empty(log.Warnings);
    }

    // The reachable participant is still served. This is NOT a restatement of
    // AParticipantWithAnUnusableKeyCannotBreakTheBroadcast -- that test would pass with the log line
    // deleted, and this one is here so a future edit cannot buy silence back by making the whole
    // broadcast bail out early instead of skipping one peer.
    [Fact]
    public void SkippingTheUnreachableOneStillServesTheRest()
    {
        var (coordinator, _) = Hosting();
        using var good = new SessionKeyExchange();

        coordinator.ReceiveJoinRequest(PeerCodes.Of(Unreachable), [1, 2, 3], Now);
        coordinator.ReceiveJoinRequest(PeerCodes.Of(Reachable), good.PublicKey, Now);
        coordinator.Admit(PeerCodes.Of(Unreachable));
        coordinator.Admit(PeerCodes.Of(Reachable));

        Assert.Equal(2, coordinator.Audience.Recipients.Count);
    }

    // FINDING 4. Fails if: a payload that AUTHENTICATED and then failed to decode is discarded in
    // silence. Open succeeding means the AEAD verified, so a keyholder sealed this FOR US -- it can
    // never be "traffic for somebody else", which is the reading that makes silence correct one line
    // earlier. What is left is version skew or an encoding defect, and both are faults.
    //
    // The payload here is sealed with the REAL shared key and carries bytes that are not JSON, so it
    // travels the exact production path: decode the frame, open the seal, fail to decode the content.
    [Fact]
    public void APayloadThatAuthenticatesAndThenFailsToDecodeIsReported()
    {
        var (player, transport, log) = Joining(out var hostKeys, out var code);

        transport.Deliver(SealedGarbage(hostKeys, player, code));
        player.Tick(TimeSpan.Zero, Now);

        var line = Assert.Single(log.Warnings);
        Assert.Contains("authenticated", line, StringComparison.OrdinalIgnoreCase);
    }

    // THE NEGATIVE HALF, and for this line it matters more than for finding 5's. A payload sealed for
    // SOMEBODY ELSE is ORDINARY traffic -- keys are pairwise, so every client constantly receives
    // payloads it cannot open. If this line fired there it would scream on every normal broadcast,
    // and the fault it exists to report would be buried in its own noise.
    [Fact]
    public void APayloadSealedForSomebodyElseStaysSilent()
    {
        var (player, transport, log) = Joining(out _, out var code);
        using var somebodyElse = new SessionKeyExchange();

        transport.Deliver(SealedGarbage(somebodyElse, player, code));
        player.Tick(TimeSpan.Zero, Now);

        Assert.Empty(log.Warnings);
    }

    private static (SessionCoordinator Player, DeliveringTransport Transport, RecordingLog Log) Joining(
        out SessionKeyExchange hostKeys,
        out SessionCode code)
    {
        var log = new RecordingLog();
        var transport = new DeliveringTransport();
        var player = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, log: log, capabilities: SessionCapabilities.Default);

        code = SessionCode.FromValid("BCDFGH");
        hostKeys = new SessionKeyExchange();

        player.RequestJoin(code, DisplayName.OrNone("Bob"));
        player.SynchroniseTransport();
        player.Tick(TimeSpan.Zero, Now);
        transport.Deliver(WireEnvelope.ForJoinAccepted(code, player.Membership.Keys!.PublicKey, hostKeys.PublicKey));
        player.Tick(TimeSpan.Zero, Now);

        log.Warnings.Clear();
        return (player, transport, log);
    }

    // Sealed with a REAL key so the AEAD verifies, carrying bytes that are not JSON so the decode
    // fails after it. Building it any other way would test a different exit.
    private static WireEnvelope SealedGarbage(SessionKeyExchange from, SessionCoordinator player, SessionCode code)
    {
        var sealedPayload = SessionCipher.Seal(
            from.DeriveSharedKey(player.Membership.Keys!.PublicKey, code),
            System.Text.Encoding.UTF8.GetBytes("this is not json"),
            WireEnvelope.AssociatedDataFor(code, WireMessageType.SessionPayload));

        return WireEnvelope.ForSessionPayload(code, sealedPayload);
    }

    private static (SessionCoordinator Coordinator, RecordingLog Log) Hosting()
    {
        var log = new RecordingLog();
        // A MINTER IS SUPPLIED THOUGH THIS FILE IS NOT ABOUT MINTING, and leaving it out is what
        // made these two tests fail when R-1.5c landed: a host that admits without creating a
        // participant now WARNS about it (AdmissionControl.Admit), so the negative half below --
        // "nothing is reported when every participant is reachable" -- was reporting something.
        //
        // Arranged rather than filtered out of the assertion. The subject here is a peer the
        // broadcast skips, and a host with no campaign is not what this file means to describe;
        // weakening Assert.Empty to ignore a category of warning would blunt the only half that
        // proves the skip line is not noise.
        var coordinator = new SessionCoordinator(
            new FakeTransport(),
            () => RelayEndpoint.Default,
            GraceWindow.Default,
            log: log,
            capabilities: new SessionCapabilities(MintParticipant: static _ => Guid.NewGuid()));

        coordinator.StartHosting();
        coordinator.Host.Registered();
        coordinator.SynchroniseTransport();
        log.Warnings.Clear();
        return (coordinator, log);
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

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = new();

        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed { add { } remove { } }

        public event Action<byte[]>? Received { add { } remove { } }

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope) => Sent.Add(envelope);
    }
}
