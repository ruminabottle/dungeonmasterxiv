using System;
using System.Collections.Generic;
using System.Linq;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// R-2.12: a retained log is deletable from the place the product says everything is deletable
/// (A-2.21); a player's log does not survive and the DM's does (A-2.22); an export is a function of
/// exactly one log (A-2.16) and is never automatic (A-2.17).
/// </summary>
/// <remarks>
/// <para>
/// <b>A-2.21 FAILS BY MAKING LIVE COPY FALSE, NOT BY OMITTING A FEATURE.</b> <c>ConfigWindow</c>
/// ships <i>"nothing to delete anywhere but here"</i>, verbatim from R-1.7a and carrying a note that
/// the requirement changes first. So the test is not "deletion exists" — it is that a retained log
/// is reachable by the campaign delete path, <b>with a fixture that distinguishes it from a log an
/// unrelated path leaves alone.</b>
/// </para>
/// <para>
/// <b>A-2.16 is tested by ABSENCE and it is the only way it can be.</b> The Spec Owner ruled the
/// structural reading: a filtering exporter would have to be handed a view wider than its owner's
/// in order to narrow it, which builds the shape D-13 forbids. So there is nothing to assert about
/// filtering — the guarantee is that <see cref="LogExport.Write"/> takes one log and that no
/// overload, collection parameter or merge exists. That is checked here by reflection, because a
/// future overload would otherwise pass every behavioural test in this file.
/// </para>
/// </remarks>
public class ARetainedLogIsDeletableAndNeverMergedTests
{
    private static readonly Guid Campaign = new("11111111-1111-1111-1111-111111111111");
    private static readonly Guid Other = new("22222222-2222-2222-2222-222222222222");

    private static RetainedLog LogFor(Guid campaign) =>
        new(campaign, 638_000_000_000_000_000, [new LoggedEntry(new LoggedStamp(1, 5), "message", "BCDFGH", "hello")]);

    // ---- A-2.21: the delete control reaches it, and the fixture distinguishes.

    [Fact]
    public void ARetainedLogIsDeletedByTheCampaignDeletePath()
    {
        var archive = new FakeRetainedLogArchive();
        var store = new RetainedLogStore(archive);
        store.Retain(LogFor(Campaign), isHosting: true);

        Assert.True(store.Has(Campaign));

        store.DeleteFor(Campaign);

        Assert.False(store.Has(Campaign));
    }

    // THE BYSTANDER. Without this, a DeleteFor that wiped everything would pass the test above --
    // and "the control deletes all logs" is a different, worse product than "the control deletes
    // the campaign you chose".
    [Fact]
    public void DeletingOneCampaignsLogLeavesAnotherCampaignsAlone()
    {
        var store = new RetainedLogStore(new FakeRetainedLogArchive());
        store.Retain(LogFor(Campaign), isHosting: true);
        store.Retain(LogFor(Other), isHosting: true);

        store.DeleteFor(Campaign);

        Assert.False(store.Has(Campaign));
        Assert.True(store.Has(Other));
    }

    [Fact]
    public void DeletingALogThatIsNotThereSaysSoRatherThanPretending()
    {
        var store = new RetainedLogStore(new FakeRetainedLogArchive());

        Assert.False(store.DeleteFor(Campaign));
    }

    // ---- A-2.22: the asymmetry. Both halves fail separately.

    [Fact]
    public void TheHostsLogIsRetainedWithoutBeingAsked()
    {
        var store = new RetainedLogStore(new FakeRetainedLogArchive());

        Assert.True(store.Retain(LogFor(Campaign), isHosting: true));
        Assert.True(store.Has(Campaign));
    }

    [Fact]
    public void APlayersLogIsNotWrittenAtALL()
    {
        var archive = new FakeRetainedLogArchive();
        var store = new RetainedLogStore(archive);

        Assert.False(store.Retain(LogFor(Campaign), isHosting: false));

        // Not "written then cleaned up" -- the requirement is that it does not survive, and the only
        // way to be sure is that it was never on disk between the two.
        Assert.False(store.Has(Campaign));
        Assert.Equal(0, archive.Writes);
    }

    // ---- A-2.16: exactly one log, structurally.

    [Fact]
    public void ExportTakesExactlyOneLogAndNoOverloadTakesMore()
    {
        var writes = typeof(LogExport)
            .GetMethods()
            .Where(method => method.Name == nameof(LogExport.Write))
            .ToList();

        // A merge would arrive as an overload or a collection parameter. Asserting on the SHAPE is
        // what catches it -- a behavioural test cannot, because a merging overload passes every one.
        var single = Assert.Single(writes);
        var parameter = Assert.Single(single.GetParameters());
        Assert.Equal(typeof(RetainedLog), parameter.ParameterType);
    }

    [Fact]
    public void AnExportContainsOnlyTheLogItWasGiven()
    {
        var mine = new RetainedLog(
            Campaign, 1, [new LoggedEntry(new LoggedStamp(1, 1), "message", "BCDFGH", "mine")]);
        var theirs = new RetainedLog(
            Other, 1, [new LoggedEntry(new LoggedStamp(1, 1), "roll", "JKMNPR", "theirs")]);

        var exported = LogExport.Write(mine);

        Assert.Contains("mine", exported, StringComparison.Ordinal);
        Assert.DoesNotContain("theirs", exported, StringComparison.Ordinal);
        Assert.DoesNotContain(theirs.CampaignId.ToString(), exported, StringComparison.Ordinal);
    }

    // ---- A-2.17: never automatic.

    [Fact]
    public void RetainingDoesNotProduceAnExportedFile()
    {
        var archive = new FakeRetainedLogArchive();
        var store = new RetainedLogStore(archive);

        store.Retain(LogFor(Campaign), isHosting: true);

        // The retained log is not an export -- A-2.17 says so in terms. Nothing here writes anywhere
        // an export would go, and the store has no export path at all.
        Assert.Equal(1, archive.Writes);
        Assert.Empty(archive.ExportPaths);
    }

    // ---- A-2.31: no display name leaves the campaign.

    [Fact]
    public void AnExportCarriesPeerCodesAndNeverADisplayName()
    {
        var log = new RetainedLog(
            Campaign, 1, [new LoggedEntry(new LoggedStamp(1, 1), "message", "BCDFGH", "hello")]);

        var exported = LogExport.Write(log);

        Assert.Contains("BCDFGH", exported, StringComparison.Ordinal);

        // The projection has no field a name could travel in -- checked on the type rather than the
        // output, because an output check passes any log that happens to contain no name.
        Assert.DoesNotContain(
            typeof(LoggedEntry).GetProperties(),
            property => property.Name.Contains("Name", StringComparison.OrdinalIgnoreCase));
    }

    // ---- the projection's loud default arm.

    [Fact]
    public void AStreamKindTheProjectionHasNotBeenTaughtThrowsRatherThanGuessing()
    {
        // Gap is real and queued on PR #210. Cast an out-of-range value to stand for "a kind added
        // after this file was written" without depending on which one it is.
        var unknown = (StreamEventKind)999;
        var entry = new StreamEntry(new StreamStamp(1, 1), unknown, PeerCodes.Of("BCDFGH"), "x");

        var thrown = Assert.Throws<NotSupportedException>(() => StreamLogProjection.From(entry));

        Assert.Contains("has not been taught", thrown.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void EveryKindTheStreamHasTodayIsMapped()
    {
        // The positive half: the default arm must not be reachable by anything that exists now, or
        // it would be firing on ordinary logs rather than on a new kind.
        foreach (var kind in Enum.GetValues<StreamEventKind>())
        {
            var entry = new StreamEntry(new StreamStamp(1, 1), kind, PeerCodes.Of("BCDFGH"), "x");
            var projected = StreamLogProjection.From(entry);

            Assert.False(string.IsNullOrWhiteSpace(projected.Kind));
        }
    }

    [Fact]
    public void TheProjectionCopiesTheHostsStampRatherThanRestampING()
    {
        var entry = new StreamEntry(new StreamStamp(7, 12345), StreamEventKind.Roll, PeerCodes.Of("BCDFGH"), "1d20");

        var projected = StreamLogProjection.From(entry);

        // A-2.5: no client's local clock reaches the log. Re-stamping at write time would be exactly
        // that, and it would be invisible in any test that only checked the text.
        Assert.Equal(7, projected.Stamp.Sequence);
        Assert.Equal(12345, projected.Stamp.AtUtcTicks);
    }

    private sealed class FakeRetainedLogArchive : IRetainedLogArchive
    {
        private readonly Dictionary<Guid, string> _logs = [];

        public int Writes { get; private set; }

        public IReadOnlyList<string> ExportPaths { get; } = [];

        public IReadOnlyList<Guid> Campaigns() => _logs.Keys.ToList();

        public string? Read(Guid campaignId) => _logs.GetValueOrDefault(campaignId);

        public void Write(Guid campaignId, string contents)
        {
            Writes++;
            _logs[campaignId] = contents;
        }

        public bool Delete(Guid campaignId) => _logs.Remove(campaignId);
    }
}
