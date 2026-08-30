using System;
using System.Text.Json;
using DungeonMasterXIV.Net;
using Xunit;

using static DungeonMasterXIV.Tests.MemberContentWiring;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-2.40: the content the host refused is absent from what it stored, <b>in whole or in part</b>.
/// </summary>
/// <remarks>
/// <para>
/// <b>SPLIT OUT OF <see cref="AMemberCannotMakeTheHostRetainWhatItRefusedTests"/> BY DMXENG-145</b>,
/// which is a MOVE: no assertion changed meaning, and the count is unchanged. That class had reached
/// twenty-one lines of class margin and this method one line of method margin, so the next assertion
/// anybody added would have been refused with no explanation of where the budget went.
/// </para>
/// <para>
/// <b>THE POSITIVE CONTROL TRAVELS WITH ITS SUBJECT.</b>
/// <see cref="AssertTheRenderingWouldShowAStoredSentinel"/> is what makes the searches below mean
/// anything, and a split that left it in the other class would be a control that no longer runs with
/// the thing it controls.
/// </para>
/// </remarks>
public class TheRefusedContentIsAbsentFromEverythingStoredTests
{
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
    // run of q's, and each catches what the other misses. The table below has the measured
    // values.
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
}
