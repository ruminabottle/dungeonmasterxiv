using System;
using System.Linq;
using System.Reflection;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-1.5b: the joining client stores its participant UUID per session code, can see it, and can
/// delete it — and a join carries the claim when one is stored.
/// </summary>
/// <remarks>
/// <para>
/// <b>THE COMPLETION CONDITION IS A CONDITION, AND ITS ANTECEDENT ONLY BECAME SATISFIABLE HERE.</b>
/// DMXENG-1 asks for <i>"a join made from the UI carries a claim WHEN ONE IS STORED"</i>. Before this
/// chunk nothing was stored: DMXENG-47 conveyed an id into <c>JoinAttempt</c>, which
/// <c>Request()</c> clears and which is bound to no code. Editing the call site alone would have
/// carried null every time, or carried one campaign's id to another — the cross-campaign linkage D-8
/// refuses.
/// </para>
/// <para>
/// <b>What is covered here and what is not.</b> A-1.9b's storage half, A-1.9d and A-1.9e are machine
/// checks and are below. <b>A-1.9c is an IN-GAME criterion</b> — the friction of a two-step delete
/// lives in ImGui and no unit test sees it; what is testable is the wording, and that is asserted.
/// <b>A-1.9b's listing half is also marked in-game</b>, so the assertion here is that the data
/// reaches a caller, not that a human can read it on screen.
/// </para>
/// </remarks>
public sealed class TheJoinerRemembersWhoItIsTests
{
    private static readonly SessionCode Code = SessionCode.FromValid("BCDFGH");
    private static readonly SessionCode Another = SessionCode.FromValid("BKD7RM");

    // THE COMPLETION CONDITION. Fails if a join cannot carry a claim after one is stored, which is
    // the state the shipped build was in: the plumbing existed end to end and the UI passed two
    // arguments, so claimedParticipantId was null on every join ever made.
    [Fact]
    public void AStoredParticipantIsWhatAJoinWouldCarry()
    {
        var memory = new RelinkMemory();
        var me = Guid.NewGuid();

        Assert.Null(memory.IdFor(Code));

        memory.Remember(Code, me);

        Assert.Equal(me, memory.IdFor(Code));
    }

    // ONE CODE'S MEMORY IS NOT ANOTHER'S, and this is D-8 rather than tidiness: a client presenting
    // one campaign's participant to a different DM is exactly the cross-campaign linkage the
    // directive refuses, and it is the failure a single global slot would produce silently.
    [Fact]
    public void AClaimIsNeverCarriedToACodeItWasNotEarnedUnder()
    {
        var memory = new RelinkMemory();
        memory.Remember(Code, Guid.NewGuid());

        Assert.Null(memory.IdFor(Another));
    }

    // A-1.9b's DELETION half, and the assertion is about the ENTRY rather than the id. Fails if
    // Forget blanks the UUID and leaves the row: "after deletion no file on their disk contains that
    // UUID" is not satisfied by a record that still says this client was in that session.
    [Fact]
    public void ForgettingRemovesTheWholeRecordAndNotJustTheId()
    {
        var memory = new RelinkMemory();
        memory.Remember(Code, Guid.NewGuid());

        Assert.True(memory.Forget(Code));

        Assert.Empty(memory.All);
        Assert.DoesNotContain(memory.Remembered, entry => entry.SessionCode == Code.Value);
    }

    // DELETION ACTUALLY UNDOES THE RELINK. Fails if forgetting leaves a claim a later join would
    // still carry -- which is a deletion the player was shown and the product did not honour.
    [Fact]
    public void AfterForgettingAJoinCarriesNothingAgain()
    {
        var memory = new RelinkMemory();
        memory.Remember(Code, Guid.NewGuid());
        memory.Forget(Code);

        Assert.Null(memory.IdFor(Code));
    }

    // Forgetting what was never stored is not an error and changes nothing. Fails if a caller has to
    // check before asking -- and the UI would then have two paths where one is enough.
    [Fact]
    public void ForgettingSomethingUnknownReportsThatNothingChanged()
    {
        var memory = new RelinkMemory();

        Assert.False(memory.Forget(Code));
        Assert.Empty(memory.All);
    }

    // THE SAVE GUARD. Remember runs from the framework update, at frame rate: if it reported change
    // every time, the config file would be written sixty times a second. Fails if re-learning the
    // same id reports a change.
    [Fact]
    public void RelearningTheSameIdReportsNoChangeSoNothingIsWrittenEveryFrame()
    {
        var memory = new RelinkMemory();
        var me = Guid.NewGuid();

        Assert.True(memory.Remember(Code, me));
        Assert.False(memory.Remember(Code, me));
        Assert.Single(memory.All);
    }

    // The host is authoritative for who we are (D-3). Fails if a second, DIFFERENT id under a known
    // code is ignored or appended -- one leaves us stale against the DM, the other leaves two claims
    // for one code and no rule for choosing between them.
    [Fact]
    public void ANewIdUnderAKnownCodeReplacesTheOldOne()
    {
        var memory = new RelinkMemory();
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();

        memory.Remember(Code, first);

        Assert.True(memory.Remember(Code, second));
        Assert.Equal(second, memory.IdFor(Code));
        Assert.Single(memory.All);
    }

    // A-1.9d, AND IT IS ASSERTED STRUCTURALLY BECAUSE BEHAVIOUR CANNOT SHOW IT. "No participant UUID
    // has an expiry. A build that discards or ages out a stored UUID ON ANY TIMER fails." No test
    // can wait long enough to prove a clock is absent, and one that advanced a fake clock would only
    // prove THAT clock is not consulted.
    //
    // So this asserts the type has NOWHERE TO PUT ONE: no member of either persisted type is a date
    // or a duration. Derived by reflection rather than listed, so a field added next month fails BY
    // NAME rather than passing because nobody updated a list.
    [Theory]
    [InlineData(typeof(RelinkMemory))]
    [InlineData(typeof(RememberedParticipant))]
    public void NothingStoredHereCanBeAgedOut(Type stored)
    {
        var clocks = stored
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.PropertyType == typeof(DateTime)
                || p.PropertyType == typeof(DateTime?)
                || p.PropertyType == typeof(DateTimeOffset)
                || p.PropertyType == typeof(DateTimeOffset?)
                || p.PropertyType == typeof(TimeSpan)
                || p.PropertyType == typeof(TimeSpan?))
            .Select(p => p.Name)
            .ToList();

        Assert.True(
            clocks.Count == 0,
            $"{stored.Name} gained a time-shaped member: {string.Join(", ", clocks)}. A-1.9d fails a "
            + "build that ages a stored UUID out on ANY timer, and the field that does not exist is "
            + "the one that cannot be expired. If this is genuinely needed, it is a spec question.");
    }

    // A-1.9e, "assessed over what leaves the machine". Asserted structurally for the same reason:
    // this type has no transport, no coordinator and no way to reach one, so deleting is local BY
    // CONSTRUCTION rather than by a caller remembering not to announce it.
    //
    // Fails if anything network-shaped is given to the memory -- at which point a future deletion
    // path could notify the DM, which is the signal R-1.5b refuses to manufacture.
    [Fact]
    public void TheMemoryCannotSendAnythingBecauseItHasNothingToSendWith()
    {
        var reachable = typeof(RelinkMemory)
            .GetMembers(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
            .OfType<FieldInfo>()
            .Select(f => f.FieldType.Name)
            .Where(name => name.Contains("Transport", StringComparison.Ordinal)
                || name.Contains("Coordinator", StringComparison.Ordinal)
                || name.Contains("Announcer", StringComparison.Ordinal)
                || name.Contains("Link", StringComparison.Ordinal))
            .ToList();

        Assert.True(
            reachable.Count == 0,
            $"RelinkMemory can now reach {string.Join(", ", reachable)}. A-1.9e requires deleting to "
            + "send nothing to the DM or the relay, and the strongest form of that is having nothing "
            + "to send with.");
    }

    // A-1.9c's CONTENT. The criterion names two facts and this asserts both are present before the
    // deletion completes. The FRICTION -- that it takes two steps rather than one click -- is in
    // ImGui and is an in-game check; this is the half a test can hold.
    [Fact]
    public void TheWarningStatesBothFactsA19cRequires()
    {
        var warning = RelinkDisclosure.BeforeForgetting("BCDFGH");

        Assert.Contains("BCDFGH", warning, StringComparison.Ordinal);
        Assert.Contains("no longer recognise you", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("as a new player", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("approves you", warning, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("cannot be undone", warning, StringComparison.OrdinalIgnoreCase);
    }

    // R-1.7a's FORBIDDEN PHRASINGS, checked against the copy rather than trusted of it. Each is
    // false under D-8 and the last is false even with encryption. This copy is engineering-authored
    // UNDER those constraints (SQ-67), so the constraints are what a reviewer checks -- and a test
    // is a better place to keep them than a reviewer's memory.
    [Theory]
    [InlineData("anonymous")]
    [InlineData("private")]
    [InlineData("we can't see anything")]
    [InlineData("no one can see your session")]
    [InlineData("cannot be linked")]
    [InlineData("untraceable")]
    public void NoForbiddenPhrasingAppearsInAnyStringThisFileShips(string forbidden)
    {
        foreach (var copy in AllCopy())
        {
            Assert.DoesNotContain(forbidden, copy, StringComparison.OrdinalIgnoreCase);
        }
    }

    // THE CONTROL, and without it the theory above is worth nothing: if AllCopy returned an empty
    // set -- a rename, a refactor, a constant moved -- every forbidden phrasing would "not appear"
    // and the check would pass while inspecting nothing.
    [Fact]
    public void TheCopySweepIsActuallyLookingAtSomething()
    {
        var copy = AllCopy();

        Assert.NotEmpty(copy);
        Assert.All(copy, line => Assert.NotEqual(string.Empty, line));
        Assert.Contains(copy, line => line.Contains("participant", StringComparison.OrdinalIgnoreCase));
    }

    // THE SEAM Plugin.cs BRIDGES, driven through the REAL coordinator rather than asserted about.
    // A joiner is admitted, the host tells it which participant it is (DMXENG-47), and what the
    // plugin's framework tick does with that is exactly the two calls below.
    //
    // WHAT THIS DOES NOT COVER, said plainly: Plugin.OnFrameworkUpdate itself. That line needs
    // Dalamud's IFramework and no unit test reaches it -- so the pieces are proven to compose and
    // the ONE line joining them is in-game only. That is the same gap shape as DMXENG-47's "an arm
    // firing into a handler nobody wired", and naming it is the only honest thing available.
    [Fact]
    public void WhatTheJoinerIsToldIsWhatTheMemoryStoresAndWhatTheNextJoinWouldCarry()
    {
        var attempt = new JoinAttempt();
        attempt.Request(Code);
        attempt.AwaitDecision(AdmissionDeadline.DecidedByHost(
            new DateTimeOffset(2026, 8, 29, 2, 0, 0, TimeSpan.Zero)));
        attempt.Admitted();

        var told = Guid.NewGuid();
        attempt.ToldItIsParticipant(told);

        var memory = new RelinkMemory();

        // The two lines Plugin.OnFrameworkUpdate runs, in order, against real state.
        Assert.True(attempt.ParticipantId is { });
        memory.Remember(attempt.Code!.Value, attempt.ParticipantId!.Value);

        // And what a later join under the same code would put on the wire.
        Assert.Equal(told, memory.IdFor(Code));
    }

    // THE OTHER HALF OF THE SAME SEAM, and the one that would silently store nothing: a joiner that
    // was never told an id has none to remember. Fails if the guard is dropped and a null is
    // recorded, or if some default id is invented for an attempt nobody answered.
    [Fact]
    public void AJoinerNeverToldAnIdHasNothingToRemember()
    {
        var attempt = new JoinAttempt();
        attempt.Request(Code);

        Assert.Null(attempt.ParticipantId);
        Assert.Null(new RelinkMemory().IdFor(Code));
    }

    private static string[] AllCopy() =>
    [
        RelinkDisclosure.WhatIsStored,
        RelinkDisclosure.BeginForgetting,
        RelinkDisclosure.KeepIt,
        RelinkDisclosure.ConfirmForget,
        RelinkDisclosure.BeforeForgetting("BCDFGH"),
    ];
}
