using System;
using System.Collections.Generic;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// BUG-70, second half: the signal exists — this is whether it REACHES anyone.
/// </summary>
/// <remarks>
/// <para>
/// <b>Why this is a separate suite from <c>ADroppedRosterEntryIsObservableTests</c>.</b> That one
/// drives <see cref="SessionContentCodec.TryDecode"/> directly and proves the codec SAYS something.
/// It passes perfectly against a build where nothing production-side ever hands the codec a log —
/// which was exactly the state main shipped in between PR #120 and this change. <b>A signal nobody
/// is listening for is the same defect the bug was filed about</b>, one layer out.
/// </para>
/// <para>
/// So these tests refuse the shortcut of calling the codec. They drive a real
/// <see cref="SessionCoordinator"/> through a real join, seal a real payload with the key the host
/// would actually derive, and put it on the wire. <b>The only thing asserted is what came out of the
/// log</b> — because the conduit is the thing under test, and every part of it is somebody's
/// optional parameter that defaults to silence.
/// </para>
/// </remarks>
public class TheDropSignalIsWiredThroughTheCoordinatorTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 28, 7, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BKD7RM");

    /// <summary>
    /// A peer code the product could actually emit — derived from the same two constants the codec
    /// validates against, for the reason BUG-57 recorded: a typed fixture can be one the encoder
    /// can never produce, and nothing notices. The TAIL, because the head collides with the session
    /// code these fixtures use.
    /// </summary>
    private static readonly string Usable = SpeakableAlphabet.Characters[^SessionCode.Length..];

    // THE REGRESSION TEST for this half. Fails if: any link in the chain defaults back to silence --
    // the coordinator not taking a log, not keeping it, or not handing it to Drain; the inbox not
    // passing it to ApplyContent; ApplyContent not passing it to the codec. Each of those compiles,
    // ships, and leaves this the only thing that notices.
    [Fact]
    public void ADroppedEntryArrivingOverTheWireReachesTheLog()
    {
        var log = new RecordingLog();
        var (coordinator, transport, key) = AdmittedJoiner(log);

        Deliver(transport, key, $$"""
            { "Roster": [ { "PeerCode": "not-a-code", "DisplayName": "Mallory", "Role": 0 },
                          { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": 0 } ] }
            """);
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.NotEmpty(log.Warnings);
    }

    // THE VACUITY CONTROL, and it is the one that would have caught a lazier fix. Threading a log
    // and warning unconditionally satisfies the test above while making the signal worthless: a line
    // on every decode is exactly as useless as no line at all. This is the assertion that says the
    // conduit carries the DECISION and not just traffic.
    [Fact]
    public void ACleanRosterOverTheSameWireSaysNothing()
    {
        var log = new RecordingLog();
        var (coordinator, transport, key) = AdmittedJoiner(log);

        Deliver(transport, key, $$"""
            { "Roster": [ { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": 0 } ] }
            """);
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Empty(log.Warnings);
    }

    // The behaviour must not have moved. The drop is ruled correct and this change is about who
    // hears about it -- so the roster a user ends up seeing is identical either way. Passes before
    // and after the fix, which is precisely why it is not the regression test.
    [Fact]
    public void TheRosterTheJoinerEndsUpWithIsUnchanged()
    {
        var log = new RecordingLog();
        var (coordinator, transport, key) = AdmittedJoiner(log);

        Deliver(transport, key, $$"""
            { "Roster": [ { "PeerCode": "not-a-code", "DisplayName": "Mallory", "Role": 0 },
                          { "PeerCode": "{{Usable}}", "DisplayName": "Bob", "Role": 0 } ] }
            """);
        coordinator.Tick(TimeSpan.Zero, Now);

        var entry = Assert.Single(coordinator.Roster);
        Assert.Equal("Bob", entry.DisplayName);
    }

    /// <summary>
    /// Drives a joiner to an admitted session the way the wire does, and returns the key the host
    /// holds for it — so a payload can be sealed exactly as <c>RosterBroadcast</c> seals one.
    /// </summary>
    private static (SessionCoordinator Coordinator, FakeTransport Transport, byte[] Key) AdmittedJoiner(
        ISessionTransportLog log)
    {
        var transport = new FakeTransport();
        var coordinator = new SessionCoordinator(
            transport, () => RelayEndpoint.Default, GraceWindow.Default, log: log, capabilities: SessionCapabilities.Default);
        var host = new SessionKeyExchange();

        coordinator.RequestJoin(Code);
        coordinator.Join.AwaitDecision(AdmissionDeadline.DecidedByHost(Now));
        transport.Deliver(WireEnvelope.ForJoinAccepted(Code, coordinator.Membership.Keys!.PublicKey, host.PublicKey));
        coordinator.Tick(TimeSpan.Zero, Now);

        Assert.Equal(JoinPhase.Admitted, coordinator.Join.Phase);
        var key = host.DeriveSharedKey(coordinator.Membership.Keys!.PublicKey, Code);
        Assert.Equal(key, coordinator.Membership.SessionKey);

        return (coordinator, transport, key);
    }

    /// <summary>Seals a document and puts it on the wire, as <c>RosterBroadcast</c> would.</summary>
    private static void Deliver(FakeTransport transport, byte[] key, string json)
    {
        var sealedPayload = SessionCipher.Seal(
            key,
            System.Text.Encoding.UTF8.GetBytes(json),
            WireEnvelope.AssociatedDataFor(Code, WireMessageType.SessionPayload));

        transport.Deliver(WireEnvelope.ForSessionPayload(Code, sealedPayload));
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

    private sealed class FakeTransport : ISessionTransport
    {
        public List<byte[]> Sent { get; } = [];

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
