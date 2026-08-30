using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.12's keep-or-lose choice: <b>the log survives until the choice resolves, the window ends, and
/// an ignored offer loses the log.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>THE PROPERTY IS THAT THE LOG OUTLIVES THE TEARDOWN, NOT THAT THE TEARDOWN WAITS.</b> SQ-115
/// ruled the offer does not block teardown and that what binds is <i>"a keep-or-lose choice
/// presented after the thing is gone is not a choice."</i> So the case below tears the session down
/// underneath an open offer and then reads the prompt — a build that held the entries by reference
/// into the session would go empty exactly there, and would pass every test that resolved the offer
/// before tearing down.
/// </para>
/// <para>
/// <b>DECLINING IS ASSERTED BY THE LOG BEING GONE, not by the outcome enum alone.</b> An enum set to
/// <c>Declined</c> beside a still-held log satisfies every assertion about the decision while
/// leaving the thing the decision was about sitting in memory, and R-2.12's actual sentence is that
/// the log dies.
/// </para>
/// </remarks>
public class TheSessionEndOfferHoldsOneLogUntilItResolvesTests
{
    private const long Closes = 1_000;
    private static readonly Guid Campaign = Guid.NewGuid();

    // ---- A-2.16: exactly one log, structurally.

    [Fact]
    public void TheOfferTakesExactlyOneLogAndNoConstructorTakesMore()
    {
        // Same shape-assertion as ARetainedLogIsDeletableAndNeverMerged uses for Write, and for the
        // same reason: a merging overload passes every behavioural test written against this one.
        var constructor = Assert.Single(typeof(SessionLogOffer).GetConstructors());

        Assert.Single(constructor.GetParameters(), p => p.ParameterType == typeof(RetainedLog));
    }

    [Fact]
    public void NoPublicMemberAnywhereAcceptsMoreThanOneLog()
    {
        // The other half of A-2.16 -- "or that REACHES FOR A SECOND". A constructor taking one log
        // proves nothing if some later method accepts a second, or a collection of them.
        var offer = typeof(SessionLogOffer);

        var takesMany = offer.GetMethods()
            .SelectMany(method => method.GetParameters())
            .Concat(offer.GetConstructors().SelectMany(c => c.GetParameters()))
            .Where(parameter => Mentions(parameter.ParameterType))
            .ToList();

        Assert.Empty(takesMany);
    }

    // A collection of logs, in any of the shapes one could arrive in.
    private static bool Mentions(Type type) =>
        type != typeof(RetainedLog)
        && (type.IsArray || type.IsGenericType)
        && type.GetGenericArguments().Concat([type.GetElementType() ?? typeof(void)])
            .Any(argument => argument == typeof(RetainedLog));

    // ---- The prompt.

    [Fact]
    public void ThePromptSaysHowLongItIsWhatIsInItAndWhoIsInIt()
    {
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"), Entry(2, "JKMNPR", "spoke"));

        Assert.Equal(2, offer.LineCount);
        Assert.True(offer.HasAnything);
        Assert.Equal(["BCDFGH", "JKMNPR"], offer.Participants);
    }

    [Fact]
    public void AnEmptyLogHasNothingToOffer()
    {
        // The control for HasAnything. Without it, a build returning a constant true passes above.
        var offer = OfferOver();

        Assert.False(offer.HasAnything);
        Assert.Equal(0, offer.LineCount);
    }

    // ---- SQ-115: the log outlives the teardown.

    [Fact]
    public void TheLogIsStillReadableAfterTheSessionItCameFromIsTornDown()
    {
        var host = Hosting();
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"));

        // GUARD: there must be a live session to tear down, or "it survived the teardown" is true of
        // nothing and this case cannot fail.
        Assert.True(host.InAHostedSession, "Not hosting, so there is no teardown for the log to outlive.");

        host.EndSessionForTeardown(new DateTimeOffset(2026, 8, 30, 16, 0, 0, TimeSpan.Zero));

        Assert.False(host.InAHostedSession, "Teardown did not stop hosting, so nothing was torn down.");
        Assert.True(offer.IsOpen);
        Assert.Equal(1, offer.LineCount);
    }

    // ---- Resolving.

    [Fact]
    public void KeepingHandsBackTheLogAndLeavesItReadable()
    {
        var log = LogOf(Entry(1, "BCDFGH", "rolled"));
        var offer = new SessionLogOffer(log, Closes);

        var kept = offer.Keep();

        Assert.Same(log, kept);
        Assert.Equal(SessionLogOfferOutcome.Kept, offer.Outcome);
        Assert.Equal(1, offer.LineCount);
    }

    [Fact]
    public void DecliningDropsTheLogAndNotOnlyTheOutcome()
    {
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"));

        offer.Decline();

        Assert.Equal(SessionLogOfferOutcome.Declined, offer.Outcome);
        Assert.False(offer.IsOpen);

        // The half that an outcome-only assertion cannot see: the entries are actually gone.
        Assert.Throws<InvalidOperationException>(() => offer.LineCount);
    }

    [Fact]
    public void AnIgnoredOfferLapsesIntoTheSameDeclineAndLosesTheLog()
    {
        // Decision 4: declining by inaction is declining. A lapse is not a third outcome.
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"));

        Assert.True(offer.ElapseTo(Closes));

        Assert.Equal(SessionLogOfferOutcome.Declined, offer.Outcome);
        Assert.Throws<InvalidOperationException>(() => offer.LineCount);
    }

    [Fact]
    public void TheWindowIsStillOpenTheTickBeforeItCloses()
    {
        // The control for the lapse. Without it, an offer that closed on construction passes above.
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"));

        Assert.False(offer.ElapseTo(Closes - 1));

        Assert.True(offer.IsOpen);
        Assert.Equal(1, offer.LineCount);
    }

    [Fact]
    public void TheRemainingTimeIsReadableWhileItRunsAndZeroOnceItHasNotRun()
    {
        // R-1.3c: the bound is shown WHILE the wait happens, not only announced when it ends.
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"));

        Assert.Equal(TimeSpan.FromTicks(400), offer.RemainingAt(Closes - 400));
        Assert.Equal(TimeSpan.Zero, offer.RemainingAt(Closes));
        Assert.Equal(TimeSpan.Zero, offer.RemainingAt(Closes + 5_000));
    }

    [Fact]
    public void AResolvedOfferCannotBeAnsweredASecondTime()
    {
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"));

        offer.Decline();

        Assert.Throws<InvalidOperationException>(() => offer.Keep());
        Assert.Throws<InvalidOperationException>(offer.Decline);
    }

    [Fact]
    public void LapsingAfterAKeepDoesNotTakeTheKeptLogBack()
    {
        var offer = OfferOver(Entry(1, "BCDFGH", "rolled"));

        offer.Keep();

        Assert.False(offer.ElapseTo(Closes + 1_000));
        Assert.Equal(SessionLogOfferOutcome.Kept, offer.Outcome);
        Assert.Equal(1, offer.LineCount);
    }

    // ---- The supply: what this client recorded becomes the offer's log.

    [Fact]
    public void TheOfferIsBuiltFromWhatThisClientRecorded()
    {
        // The production pipeline, in the order Plugin.cs runs it: recorded entries -> projection ->
        // one log -> the offer. Every client records its own log (R-2.12, SQ-116), so this is what
        // THIS client received and never an assembled superset.
        IReadOnlyList<StreamEntry> recorded =
        [
            new(new StreamStamp(1, 100), StreamEventKind.Roll, PeerCodes.Of("BCDFGH"), "1d20"),
            new(new StreamStamp(2, 200), StreamEventKind.Message, PeerCodes.Of("JKMNPR"), "nice"),
        ];

        var offer = new SessionLogOffer(
            new RetainedLog(Campaign, 200, StreamLogProjection.From(recorded)), Closes);

        Assert.Equal(2, offer.LineCount);
        Assert.Equal(["BCDFGH", "JKMNPR"], offer.Participants);
    }

    [Fact]
    public void ACoordinatorThatHasRecordedNothingOffersNothing()
    {
        // The starting state, so the row above is a CHANGE rather than a coincidence. This also
        // pins the accessor added to SessionCoordinator for the composition root.
        var host = Hosting();

        Assert.Empty(host.Recorded);
    }

    private static SessionCoordinator Hosting()
    {
        var host = new SessionCoordinator(
            new QuietTransport(), () => RelayEndpoint.Default, GraceWindow.Default,
            SilentLog.Instance, SessionCapabilities.Default);

        host.StartHosting();
        host.Host.Registered();
        host.SynchroniseTransport();
        return host;
    }

    // ---- A-2.23a: an accept that reads as having written something.
    //
    // The sentence-test that stood beside this one is GONE with the constant it pinned (DMXENG-123
    // shipped the writer). It asserted only that SOME string existed, under a name claiming the
    // sentence was pinned -- so it would have passed a build that set the constant to "x". No
    // replacement is owed: writing one to make the removal look thorough would reproduce exactly
    // the defect being removed.

    [Fact]
    public void TheOfferHoldsNothingThatCouldWriteAFile()
    {
        // The OTHER half of A-2.23a, and the one a behavioural test cannot reach: a future "fix"
        // that satisfies the first half by reaching for RetainedLogFormat would put a peer code
        // into a genuine export and fail A-1.11a. It cannot, because there is nothing here to
        // reach with -- no store, no archive, no formatter, in a parameter or a field.
        var offer = typeof(SessionLogOffer);

        var surface = offer.GetConstructors()
            .SelectMany(c => c.GetParameters().Select(p => p.ParameterType))
            .Concat(offer
                .GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
                .Select(field => field.FieldType))
            .ToList();

        // VACUITY CONTROL: the offer really does hold a RetainedLog, so an empty list below is a
        // fact about the TYPES and not about the reflection having read nothing. Without it, wrong
        // BindingFlags or a renamed type would empty the surface and pass.
        Assert.Contains(typeof(RetainedLog), surface);

        var unrecognised = surface.Where(type => !MayBeHeld(type)).Select(type => type.Name).Distinct().ToList();

        Assert.Empty(unrecognised);
    }

    /// <summary>What the offer is ALLOWED to hold. Anything else fails, including a type nobody has
    /// written yet.</summary>
    /// <remarks>
    /// <para>
    /// DMXENG-146. This was <c>CouldWrite</c>, a hand-list of THREE write-capable types, and it was
    /// written before an export writer existed. PR #237 added four — <c>ISessionExportDestination</c>,
    /// <c>SessionExportFileDestination</c>, <c>SessionExport</c>, <c>SessionExportFormat</c> — and
    /// none were added here, so the guard <b>failed open on exactly the types the PR that needed it
    /// introduced</b>.
    /// </para>
    /// <para>
    /// <b>INVERTED RATHER THAN EXTENDED, because the two populations grow differently.</b> The
    /// write-capable set grows whenever anyone anywhere adds a writer, and an omission there is
    /// silent and passes. The set this offer may HOLD is small, stable, and can only change by
    /// editing <c>SessionLogOffer</c> itself — and an omission HERE fails, loudly, naming the type.
    /// Adding the four names would have closed today's gap, kept the mechanism, and made the next
    /// omission look like it had been considered.
    /// </para>
    /// <para>
    /// <b>A positive derivation of "can write" is not available, measured rather than assumed.</b>
    /// Namespace cannot discriminate: <c>RetainedLog</c>, which the offer legitimately holds, shares
    /// <c>DungeonMasterXIV.Data</c> with <c>SessionExport</c> and <c>RetainedLogStore</c>. Nor can a
    /// System.IO reference: of the four types #237 added, only
    /// <c>SessionExportFileDestination</c> mentions System.IO at all, so that rule would have missed
    /// three of the four — under-covering silently, which is this defect exactly. Reflection cannot
    /// read method bodies, so transitive reachability is not observable here either.
    /// </para>
    /// <para>
    /// <b>To extend it:</b> when the offer legitimately gains a field, add that type here as a
    /// deliberate decision. That edit is the point — it is a person saying "the offer may hold this",
    /// in the same commit that makes it true.
    /// </para>
    /// </remarks>
    private static bool MayBeHeld(Type type) =>
        type.IsPrimitive
        || type == typeof(RetainedLog)
        || type == typeof(SessionLogOfferOutcome);

    /// <summary>Inert wire: the session's own state is what these cases observe.</summary>
    private sealed class QuietTransport : ISessionTransport
    {
        public bool IsConnected { get; private set; }

        public bool IsReadyToSend => IsConnected;

        public event Action<SessionFailure>? Failed;

        public event Action<byte[]>? Received;

        public void Connect(Uri relay) => IsConnected = true;

        public void Disconnect() => IsConnected = false;

        public void Send(byte[] envelope)
        {
            _ = Failed;
            _ = Received;
        }
    }

    private static LoggedEntry Entry(int sequence, string peer, string text) =>
        new(new LoggedStamp(sequence, 100 + sequence), "message", peer, text);

    private static RetainedLog LogOf(params LoggedEntry[] entries) => new(Campaign, 500, entries);

    private static SessionLogOffer OfferOver(params LoggedEntry[] entries) =>
        new(LogOf(entries), Closes);
}
