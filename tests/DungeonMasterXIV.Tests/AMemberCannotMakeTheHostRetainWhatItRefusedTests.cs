using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
        var (handlers, resources, peer, _) = Wired();

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
        var (handlers, resources, peer, _) = Wired();
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
        var (handlers, resources, peer, _) = Wired();
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
        var (handlers, resources, peer, _) = Wired();

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = "hello table" });

        Assert.Equal("hello table", Assert.Single(resources.Recording.Entries).Text);
        Assert.Equal("hello table", Assert.Single(resources.MemberContent.Latest).Content.Saying);
    }

    // THE FIELD WITH A REAL CONSUMER. A-1.16a's departure trace is the reason this receipt exists, so
    // a bound that quietly dropped it would break the feature while passing every row above.
    [Fact]
    public void ADepartureIsStillRecorded()
    {
        var (handlers, resources, peer, _) = Wired();

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Leaving = true });

        Assert.True(Assert.Single(resources.MemberContent.Latest).Content.Leaving);
    }

    // qa-1's probe F, preserved. Retention is REPLACED, not ACCUMULATED — a change that made it
    // accumulate would be worse than the defect this fixes.
    [Fact]
    public void RetentionIsStillReplacedRatherThanAccumulated()
    {
        var (handlers, resources, peer, _) = Wired();

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
        var (handlers, resources, peer, _) = Wired();
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
        var (handlers, resources, peer, _) = Wired();
        var oversize = new string('x', OverTheStreamsBound);

        for (var i = 0; i < 500; i++)
        {
            handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = oversize });
        }

        // 500 refusals leave ONE retained receipt and a COUNT -- not 500 of anything.
        Assert.Single(resources.MemberContent.Latest);
        Assert.Equal(500, resources.MemberContent.RefusedSayings);
    }

    // >>> THE DISCRIMINATOR THAT SEPARATES A REFUSAL RECORD FROM AN ARRIVAL COUNTER.
    //
    // An ACCEPTED message and a REFUSED one must leave DIFFERENT records. If both produce the same
    // artefact then the "record" counts arrivals, and asserting on it is a check that cannot fail --
    // which is the exact failure A-2.36 exists to prevent. <c>Received</c> is precisely that:
    // <c>_received++</c> is the first statement of <c>Record</c>, which <c>:98</c> calls
    // unconditionally, so it is identical in a refusing and a non-refusing build.
    [Fact]
    public void AnAcceptedMessageLeavesTheRefusalRecordUntouched()
    {
        var (handlers, resources, peer, _) = Wired();

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = "hello table" });

        Assert.Equal(0, resources.MemberContent.RefusedSayings);
        Assert.Equal(0, resources.MemberContent.RefusedRosters);
        Assert.Equal(0, resources.MemberContent.RefusedEntries);

        // THE PREMISE, and without it the three zeros above are satisfied by a build where nothing
        // arrived at all: the arrival WAS counted. So the two records genuinely DIFFER -- one moved
        // and the other did not, on the same call.
        Assert.Equal(1, resources.MemberContent.Received);
    }

    // >>> A-2.38: NOTHING REACHES A SESSION SURFACE -- ASSERTED TOGETHER WITH A-2.36's RECORD.
    //
    // The criterion is explicit that BOTH HALVES are needed: an unchanged stream is satisfied
    // trivially by a build where the refusal never happened. So the record and the non-movement are
    // asserted in ONE test -- the refusal demonstrably occurred AND nothing moved.
    [Fact]
    public void ARefusalMovesNoSessionSurfaceAndIsStillRecorded()
    {
        var (handlers, resources, peer, roster) = Wired();
        var rosterBefore = roster.Count;

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = new string('x', OverTheStreamsBound) });

        // HALF ONE -- the refusal demonstrably HAPPENED. Without this the rest is a check that
        // cannot fail.
        Assert.Equal(1, resources.MemberContent.RefusedSayings);

        // HALF TWO -- and no session surface moved. No entry, no marker, no gap indicator.
        Assert.Empty(resources.Recording.Entries);
        Assert.Equal(rosterBefore, roster.Count);
    }

    // >>> A-2.40: THE REFUSED CONTENT IS ABSENT, IN WHOLE OR IN PART.
    //
    // A distinctive SENTINEL is planted at the END of the refused payload -- refused is
    // ('q' x bound) + Sentinel -- and "in part" is the half that bites, which a whole-payload
    // equality check would not catch.
    //
    // THE SENTINEL IS A SUFFIX, which is the fact an earlier version of this note got wrong: it
    // said a truncated PREFIX kept "for diagnostics" would fail this test. It would not -- a prefix
    // carries no sentinel and trips the "qqq" search instead (BUG-188). So the two searches in the
    // body are not redundant: a PREFIX mutation carries no sentinel, a SUFFIX mutation carries no
    // run of q's, and each catches what the other misses. The table in the body has the measured
    // values.
    [Fact]
    public void TheRefusedContentIsAbsentInWholeOrInPart()
    {
        const string Sentinel = "Zq7-SENTINEL-4vX";
        var (handlers, resources, peer, _) = Wired();
        var refused = new string('q', OverTheStreamsBound) + Sentinel;

        // THE POSITIVE CONTROL IS THE SENTINEL ITSELF: the search finds it in the payload that was
        // refused, so its absence below is a fact about the STORE and not about the search.
        Assert.Contains(Sentinel, refused, StringComparison.Ordinal);

        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = refused });

        // >>> THESE TWO GROUPS ARE NOT REDUNDANT, AND THE SECOND IS SHADOWED BY THE FIRST. <<<
        //
        // THREE LAYERS, NOT TWO, AND EACH IS SEPARATELY PROVABLE. Mutate Retainable to keep part of
        // the refused payload and delete the Null trio to unshadow; the surviving assert NAMES what
        // it caught. All three rows measured on DMXENG-140, not quoted:
        //
        //     keep a 40-char PREFIX     -> Found:  "qqq"                the qqq assert alone
        //     keep the LAST 16 (SUFFIX) -> Found:  "Zq7-SENTINEL-4vX"   the sentinel alone
        //     retain the WHOLE saying   -> Found:  "Zq7-SENTINEL-4vX"   both violated; the sentinel
        //                                                               is named only because it is
        //                                                               asserted first
        //
        // WITH the Null trio in place all three give "Assert.Null() Failure: Value is not null" --
        // the trio runs FIRST and neither search executes. SHADOWED, NOT DECORATIVE.
        //
        // ALWAYS QUOTE THE `Found:` LINE. Both searches are Assert.DoesNotContain, so the failure
        // line is byte-identical either way; the recipe that stood here cited a PREFIX mutation as
        // proof of the SENTINEL and survived three confirmations because nobody read which value
        // fired (BUG-188 -- qa-2 caught it by ISOLATING, which reproducing could not).
        //
        // WHAT EACH ONE BUYS, which is the part a reader cannot get from looking at them:
        //   the Null asserts  -- the fast, direct check that the three retained fields are empty.
        //   the sentinel      -- the coverage of refused content leaking into some OTHER stored
        //                        field, and DMXENG-140 is what MADE that true rather than claimed.
        //                        It used to read "the ONLY coverage" while the surface it searched
        //                        was a hand-written list that omitted Roster and Entries entirely --
        //                        claiming reach it did not have. The surface is now DERIVED from the
        //                        store rather than listed, so it reaches every stored field,
        //                        including those two and any added later. A Null check on
        //                        Saying/Roster/Entries cannot see a fragment that ended up somewhere
        //                        else, and "in part" is the half A-2.40 names as the one that bites.
        //
        // DELETING EITHER LOSES REAL COVERAGE. Two independent assertions catching one predicted
        // violation is durability, and it reads as duplication to anyone who has not run the above.
        var receipt = Assert.Single(resources.MemberContent.Latest);
        Assert.Null(receipt.Content.Saying);
        Assert.Null(receipt.Content.Roster);
        Assert.Null(receipt.Content.Entries);

        // Nowhere in anything the STORE can render as text, not merely absent from Saying.
        var everythingStored = EverythingStoredIn(resources.MemberContent);

        AssertTheRenderingWouldShowAStoredSentinel(Sentinel);

        Assert.DoesNotContain(Sentinel, everythingStored, StringComparison.Ordinal);
        Assert.DoesNotContain("qqq", everythingStored, StringComparison.Ordinal);
    }

    // D-22: the record dies with the session, like the receipts it sits beside. A count that survived
    // would describe a session nobody is in.
    [Fact]
    public void TheRefusalRecordResetsWithTheSession()
    {
        var (handlers, resources, peer, _) = Wired();
        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = new string('x', OverTheStreamsBound) });
        Assert.Equal(1, resources.MemberContent.RefusedSayings);

        resources.Release();

        Assert.Equal(0, resources.MemberContent.RefusedSayings);
    }

    /// <summary>
    /// The derivation's own positive control: a saying the host DOES retain must appear in
    /// <see cref="EverythingStoredIn"/>'s rendering.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>This is not the same control as the one on the refused payload.</b> That one proves the
    /// sentinel is in the PAYLOAD. This proves the RENDERING WOULD SHOW IT IF IT WERE STORED —
    /// without which <see cref="EverythingStoredIn"/> could return an empty string and the
    /// <c>DoesNotContain</c> assertions would pass while measuring nothing.
    /// </para>
    /// <para>
    /// <b>It also catches the derivation that looks right and is not.</b> Measured on DMXENG-140:
    /// a reflect-then-<c>ToString</c> surface renders each container as its TYPE NAME, so no stored
    /// string reaches the search at all — and this control goes red on it, while the
    /// <c>DoesNotContain</c> assertions stay green and read as coverage.
    /// </para>
    /// <para>
    /// <b>A SEPARATE store, so the control cannot disturb the subject:</b> receipts are keyed by
    /// peer, so recording against the same one would replace the receipt under test.
    /// </para>
    /// </remarks>
    private static void AssertTheRenderingWouldShowAStoredSentinel(string sentinel)
    {
        var (handlers, resources, peer, _) = Wired();
        handlers.MemberAuthored.OnContent!(peer, new SessionContent { Saying = sentinel });
        Assert.Contains(sentinel, EverythingStoredIn(resources.MemberContent), StringComparison.Ordinal);
    }

    /// <summary>
    /// Everything the store can render as text, <b>derived from the object</b> rather than listed by
    /// hand.
    /// </summary>
    /// <remarks>
    /// <para>
    /// DMXENG-140. This was a <c>string.Join</c> over six hand-named fields, which could only ever
    /// cover what its author knew about: <b>a stored field added later was silently not searched and
    /// the guard stayed green while covering less.</b> It had already drifted — carrying
    /// <c>RefusedSayings</c> while omitting <c>RefusedRosters</c> and <c>RefusedEntries</c>, and
    /// omitting <c>Content.Roster</c> and <c>Content.Entries</c> altogether, which between them carry
    /// four string fields (<c>RosterEntry.PeerCode</c>, <c>RosterEntry.DisplayName</c>,
    /// <c>StreamLine.Peer</c>, <c>StreamLine.Text</c>).
    /// </para>
    /// <para>
    /// <b>Serialised rather than given a longer list, because naming more fields keeps the
    /// mechanism.</b> It also renders NESTED content, which a list could not: <c>Roster?.ToString()</c>
    /// yields <c>System.Collections.Generic.List`1[…]</c> — a type name with no element content, which
    /// would read as coverage and supply none.
    /// </para>
    /// <para>
    /// <b>The limit, stated rather than left implied:</b> a sentinel containing <c>"</c> or a
    /// backslash would be JSON-escaped, and a substring search could then miss it. The sentinel here
    /// is plain ASCII, and the positive control at the call site pins that this rendering does carry
    /// a stored one.
    /// </para>
    /// </remarks>
    private static string EverythingStoredIn(MemberContentReceipts store) =>
        JsonSerializer.Serialize(store);

    private static readonly DateTimeOffset Now = new(2026, 8, 30, 1, 0, 0, TimeSpan.Zero);
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");

    // Drives the PRODUCTION handler rather than constructing a receipts object: the defect was the
    // ORDER of two calls in InboundWiring, and a test that called Record directly could not see it.
    private static (InboundHandlers Handlers, SessionResources Resources, PeerCode Peer, SessionAudience Roster) Wired()
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

        return (wiring.For(Now, sessionKey: null, onHostContent: _ => { }), resources, peer, admissions.Audience);
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
