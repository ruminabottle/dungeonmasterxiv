using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// DMXENG-137: <c>MemberContentReceipts.Record</c> bounds what it keeps from a member-authored
/// payload, so a member cannot make the host retain something the host refused.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE DEFECT WAS AN ORDERING.</b> <c>InboundWiring</c> recorded the arriving object and only
/// afterwards handed it to the bound in <c>Said</c>, so a payload the stream REFUSED was retained in
/// full — measured at ~80001 bytes, with the stream empty and the receipt intact.
/// </para>
/// <para>
/// <b>THE BOUND WAS ENVIRONMENTAL, WHICH IS NOT A BOUND THIS TYPE HAD.</b> The only thing limiting
/// the retained payload was the relay's 64 KiB <c>MaxMessageBytes</c> — a different assembly, a
/// different process, no project reference, and an <c>init</c> property an operator can raise.
/// Nothing between the socket and here caps anything: <c>WebSocketSessionTransport</c>'s receive
/// loop accumulates a frame with no length check at all.
/// </para>
/// <para>
/// <b>THREE FIELDS, THREE TESTS, ON PURPOSE.</b> <c>Saying</c>, <c>Roster</c> and <c>Entries</c>
/// were each retained unbounded, and a suite that pinned only the first would pass a fix that
/// bounded only the first — the same partial-fix trap one level up. Each bound is mutable alone and
/// reddens only its own row.
/// </para>
/// </remarks>
public class AMemberCannotMakeTheHostRetainWhatItRefusedTests
{
    private const int OverTheStreamsBound = 80001;

    // FIELD 1 of 3. A payload the stream refuses is not retained either.
    [Fact]
    public void AnOversizeSayingIsNotRetained()
    {
        var (handlers, resources, peer) = Wired();

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = new string('x', OverTheStreamsBound) });

        // THE PREMISE: the stream really did refuse it. Without this the row below is satisfied by a
        // build where nothing happened at all.
        Assert.Empty(resources.Recording.Entries);

        Assert.Null(Assert.Single(resources.MemberContent.Latest).Content.Saying);
    }

    // FIELD 2 of 3. A member supplying a roster is trying to make the host keep one it did not author.
    [Fact]
    public void AMemberSuppliedRosterIsNotRetained()
    {
        var (handlers, resources, peer) = Wired();
        var roster = Enumerable.Range(0, 50000)
            .Select(i => new RosterEntry($"P{i}", new string('n', 40), SessionRole.Player))
            .ToList();

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Roster = roster });

        Assert.Null(Assert.Single(resources.MemberContent.Latest).Content.Roster);
    }

    // FIELD 3 of 3. Same for a member-supplied log.
    [Fact]
    public void MemberSuppliedEntriesAreNotRetained()
    {
        var (handlers, resources, peer) = Wired();
        var entries = Enumerable.Range(0, 50000)
            .Select(i => new StreamLine(i, 0L, StreamEventKind.Message, "PRBCD2", new string('e', 40)))
            .ToList();

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Entries = entries });

        Assert.Null(Assert.Single(resources.MemberContent.Latest).Content.Entries);
    }

    // THE NEGATIVE CONTROL, and without it all three rows above are satisfied by a Record that keeps
    // nothing. An ordinary message must still be retained AND still reach the stream.
    [Fact]
    public void AnOrdinaryMessageIsStillRetainedInFullAndStillReachesTheStream()
    {
        var (handlers, resources, peer) = Wired();

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = "hello table" });

        Assert.Equal("hello table", Assert.Single(resources.Recording.Entries).Text);
        Assert.Equal("hello table", Assert.Single(resources.MemberContent.Latest).Content.Saying);
    }

    // THE FIELD WITH A REAL CONSUMER. A-1.16a's departure trace is the reason this receipt exists, so
    // a bound that quietly dropped it would break the feature while passing every row above.
    [Fact]
    public void ADepartureIsStillRecorded()
    {
        var (handlers, resources, peer) = Wired();

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Leaving = true });

        Assert.True(Assert.Single(resources.MemberContent.Latest).Content.Leaving);
    }

    // qa-1's probe F, preserved. Retention is REPLACED, not ACCUMULATED — a change that made it
    // accumulate would be worse than the defect this fixes.
    [Fact]
    public void RetentionIsStillReplacedRatherThanAccumulated()
    {
        var (handlers, resources, peer) = Wired();

        for (var i = 0; i < 25; i++)
        {
            handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = new string('x', OverTheStreamsBound) });
        }

        Assert.Single(resources.MemberContent.Latest);
        Assert.Equal(25, resources.MemberContent.Received);
    }

    // >>> D-22: A REFUSAL IS RECORDED. Three discard paths, three counters, each pinned alone so a
    // fix that recorded only one cannot pass this.
    [Fact]
    public void EachRefusalIsRecordedAgainstItsOwnBoundary()
    {
        var (handlers, resources, peer) = Wired();
        var content = resources.MemberContent;

        // THE PREMISE: nothing refused yet, so each row below is a CHANGE and not a starting value.
        Assert.Equal(0, content.RefusedSayings);
        Assert.Equal(0, content.RefusedRosters);
        Assert.Equal(0, content.RefusedEntries);

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = new string('x', OverTheStreamsBound) });
        Assert.Equal(1, content.RefusedSayings);
        Assert.Equal(0, content.RefusedRosters);
        Assert.Equal(0, content.RefusedEntries);

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Roster = [new RosterEntry("P1", "n", SessionRole.Player)] });
        Assert.Equal(1, content.RefusedRosters);
        Assert.Equal(1, content.RefusedSayings);

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Entries = [new StreamLine(1, 0L, StreamEventKind.Message, "PRBCD2", "e")] });
        Assert.Equal(1, content.RefusedEntries);
        Assert.Equal(1, content.RefusedRosters);
    }

    // D-22(c), AND IT IS THE TRAP: the obvious implementation of "record every refusal" is a list,
    // which lets a peer grow the record without limit -- recreating the very defect being fixed. A
    // counter occupies the same space at 1 refusal and at 500. Probe F's discipline, applied to the
    // RECORD rather than to the retention.
    [Fact]
    public void TheRefusalRecordCannotBeGrownByWhoeverCausedIt()
    {
        var (handlers, resources, peer) = Wired();
        var oversize = new string('x', OverTheStreamsBound);

        for (var i = 0; i < 500; i++)
        {
            handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = oversize });
        }

        // 500 refusals leave ONE retained receipt and a COUNT -- not 500 of anything.
        Assert.Single(resources.MemberContent.Latest);
        Assert.Equal(500, resources.MemberContent.RefusedSayings);
    }

    // D-22: "the refused content itself is never displayed, STORED, or echoed, in whole or in part."
    // The count is a number; it carries no fragment of what arrived.
    [Fact]
    public void TheRefusedContentIsNotStoredAnywhereInTheReceipt()
    {
        var (handlers, resources, peer) = Wired();
        var secret = new string('q', OverTheStreamsBound);

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = secret });

        var receipt = Assert.Single(resources.MemberContent.Latest);
        Assert.Null(receipt.Content.Saying);
        Assert.Null(receipt.Content.Roster);
        Assert.Null(receipt.Content.Entries);
    }

    // D-22: the record dies with the session, like the receipts it sits beside. A count that survived
    // would describe a session nobody is in.
    [Fact]
    public void TheRefusalRecordResetsWithTheSession()
    {
        var (handlers, resources, peer) = Wired();
        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = new string('x', OverTheStreamsBound) });
        Assert.Equal(1, resources.MemberContent.RefusedSayings);

        resources.Release();

        Assert.Equal(0, resources.MemberContent.RefusedSayings);
    }

    private static readonly DateTimeOffset Now = new(2026, 8, 30, 1, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    // Drives the PRODUCTION handler rather than constructing a receipts object: the defect was the
    // ORDER of two calls in InboundWiring, and a test that called Record directly could not see it.
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

        var broadcast = new RosterBroadcast(
            new RelayLink(new SilentTransport(), () => RelayEndpoint.Default, static _ => { }),
            admissions.Audience,
            new HostIdentity(() => hostKeys, () => host.Code, () => DisplayName.None, () => null),
            SilentLog.Instance);

        var wiring = new InboundWiring(admissions, resources, static _ => RelinkClaim.None, broadcast);

        return (wiring.For(Now, sessionKey: null, onHostContent: _ => { }), resources, peer);
    }

    private sealed class SilentTransport : ISessionTransport
    {
        public bool IsConnected => true;
        public bool IsReadyToSend => true;
        public event Action<SessionFailure>? Failed { add { } remove { } }
        public event Action<byte[]>? Received { add { } remove { } }
        public void Connect(Uri relay) { }
        public void Disconnect() { }
        public void Send(byte[] envelope) { }
    }
}
