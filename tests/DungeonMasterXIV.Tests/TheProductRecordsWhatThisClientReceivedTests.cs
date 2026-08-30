using System;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// DMXENG-116: something in the PRODUCT constructs a stream and records into it (R-2.12, SQ-116).
/// </summary>
/// <remarks>
/// <para>
/// <b>THE SENTENCE THE R-2.12 SUITE COULD NOT SAY.</b> Nine types were built, merged and unit-tested
/// against retention and export, and <b>nothing ever constructed a <see cref="SessionStream"/> in
/// production</b> — so retention would have written an empty file every hosted session. Every one of
/// those tests passed, because a unit test constructs its own subject.
/// </para>
/// <para>
/// <b>So these drive the PRODUCTION handler.</b> <c>InboundWiring.For</c> is what the coordinator
/// hands to the inbound path; the handler invoked below is the one the product would run. A test
/// that constructed a recorder and called it would prove the thing that is already proven.
/// </para>
/// </remarks>
public class TheProductRecordsWhatThisClientReceivedTests
{
    private static readonly DateTimeOffset Now = new(2026, 8, 30, 1, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    // >>> OBLIGATION 1: A PRODUCTION CALLER RECORDS A RECEIVED ENTRY <<<
    //
    // The peer code is the one the payload OPENED UNDER, never one the payload claimed -- so what is
    // written down cannot name somebody the sender is not.
    [Fact]
    public void TheWiringRecordsAMemberDepartureItReceived()
    {
        var (handlers, resources, peer) = Wired();

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Leaving = true });

        var entry = Assert.Single(resources.Recording.Entries);
        Assert.Equal(StreamEventKind.Left, entry.Kind);
        Assert.Equal(peer, entry.Peer);
        Assert.Equal(Now.UtcTicks, entry.Stamp.AtUtcTicks);
        Assert.True(entry.Stamp.Sequence >= 1, "an unminted stamp would have been refused by the stream");
    }

    // THE CONTROL. Member content that is NOT a departure must record nothing. Without this row, a
    // build that recorded every inbound payload passes the test above.
    [Fact]
    public void MemberContentThatIsNotADepartureRecordsNothing()
    {
        var (handlers, resources, peer) = Wired();

        handlers.MemberAuthored.OnContent!(peer, new SessionContent());

        Assert.Empty(resources.Recording.Entries);
    }

    // OBLIGATION 4, first half: no session, no recording. Asserted as the starting state so the row
    // above is a CHANGE rather than a coincidence.
    [Fact]
    public void AClientThatHasReceivedNothingHasRecordedNothing()
    {
        var (_, resources, _) = Wired();

        Assert.Empty(resources.Recording.Entries);
    }

    // OBLIGATION 4, second half. R-2.12: a log DIES WITH THE SESSION. Release is what HostRunner.Stop
    // already calls, so this is the production teardown rather than a method invented for the test.
    [Fact]
    public void TheLogDiesWithTheSessionAndTheNextOneStartsAtOne()
    {
        var (handlers, resources, peer) = Wired();
        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Leaving = true });
        Assert.NotEmpty(resources.Recording.Entries);

        resources.Release();
        Assert.Empty(resources.Recording.Entries);

        // AND THE SEQUENCE RESTARTS. Carrying the counter across would number a fresh log from where
        // the last one stopped, and an export would then claim an order it never had.
        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Leaving = true });
        Assert.Equal(1, Assert.Single(resources.Recording.Entries).Stamp.Sequence);
    }

    // >>> AMENDED OBLIGATION 3: NO HOST-ONLY ASSUMPTION IS BAKED IN <<<
    //
    // The original obligation -- non-host clients record too -- is UNMET and cannot be met: no stamp
    // crosses the wire (DMXENG-118), and minting one on the member would be a second sequencer, the
    // exact drift R-2.4 exists to prevent.
    //
    // What IS achievable is that recording does not REQUIRE minting. This records an entry stamped
    // ELSEWHERE -- as an arriving one would be -- so admitting the member path later is a wiring
    // change rather than taking this type apart.
    [Fact]
    public void AnEntryStampedElsewhereIsRecordedWithoutAnySequencerHere()
    {
        var recording = new SessionRecording();
        var elsewhere = new HostSequencer(() => Now);
        elsewhere.Next();
        var arrived = new StreamEntry(elsewhere.Next(), StreamEventKind.Message, PeerCodes.Of("PRBCD2"), "the door opens");

        Assert.True(recording.Record(arrived));

        var entry = Assert.Single(recording.Entries);
        Assert.Equal("the door opens", entry.Text);

        // THE PREMISE: the stamp really came from somewhere else. Sequence 2 is the foreign
        // sequencer's second mint -- a stamp this recording could not have produced, since its own
        // first mint would be 1.
        Assert.Equal(2, entry.Stamp.Sequence);
    }

    // AND THE STREAM'S REFUSAL SURVIVES THE NEW DOOR. BUG-161: an unminted stamp sorts to the FRONT
    // of a populated log. A recorder that accepted arriving entries would be the obvious place to
    // lose that guard, so it is pinned THROUGH this type rather than only on the stream.
    [Fact]
    public void AnEntryThatWasNeverStampedIsStillRefused()
    {
        var recording = new SessionRecording();

        Assert.False(recording.Record(new StreamEntry(default, StreamEventKind.Message, PeerCodes.Of("PRBCD2"), "never stamped")));
        Assert.Empty(recording.Entries);
    }

    private static (InboundHandlers Handlers, SessionResources Resources, PeerCode Peer) Wired()
    {
        var host = new HostSession();
        host.Start(Code);
        var hostKeys = new SessionKeyExchange();

        var admissions = new AdmissionControl(
            new AdmissionAnnouncer(new SilentTransport()),
            () => host.Code,
            () => hostKeys,
            static _ => null,
            SilentLog.Instance);

        var joiner = new SessionKeyExchange();
        var peer = admissions.PeerCodeFor(joiner.PublicKey);
        admissions.Receive(peer, joiner.PublicKey, Now, displayName: DisplayName.OrNone("Ysera"));
        admissions.Admit(peer);

        var resources = new SessionResources(
            admissions,
            new AdmissionInbox(),
            () => new GraceWindow(),
            new MemberContentKeys(admissions.Audience, () => hostKeys, () => host.Code, SilentLog.Instance),
            new MemberContentReceipts());

        var wiring = new InboundWiring(admissions, resources, static _ => RelinkClaim.None);

        return (wiring.For(Now, sessionKey: null, onHostContent: _ => { }), resources, peer);
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
