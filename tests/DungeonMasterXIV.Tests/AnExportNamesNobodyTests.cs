using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonMasterXIV.Data;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// D-20's two approve-blocking bounds on the export: <b>it names nobody, it offers no legend, and it
/// says its labels are file-local at the ruled strength</b> (A-2.17a, A-2.17b, A-2.17c).
/// </summary>
/// <remarks>
/// <para>
/// <b>EVERY ABSENCE ASSERTED HERE IS PAIRED WITH A PLANTED POSITIVE, BECAUSE A-2.17a SAYS IN TERMS
/// THAT IT MUST BE.</b> A search for forbidden values over an export that is empty, or that never
/// had one in it, returns the same clean zero as a conforming build — so a green here would mean
/// nothing on its own.
/// </para>
/// <para>
/// <b>The planted positive is <see cref="RetainedLogFormat.Write"/> on the SAME log.</b> That is a
/// deliberately non-conforming artefact — it emits the peer code, correctly, because a retained log
/// is not an export (A-1.11a-note). Running the identical search over it and requiring a HIT proves
/// the search can fail before any test rests on it passing. It also pins the exact confusion the
/// split exists to prevent: two writers, one input, and only one of them may be an export.
/// </para>
/// </remarks>
public class AnExportNamesNobodyTests
{
    private const string PeerA = "BCDFGH";
    private const string PeerB = "JKLMNP";

    private static RetainedLog LogWithTwoParticipants() =>
        new(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            638_000_000_000_000_000L,
            new List<LoggedEntry>
            {
                new(new LoggedStamp(1, 100), "joined", PeerA, string.Empty),
                new(new LoggedStamp(2, 200), "message", PeerB, "hello"),
                new(new LoggedStamp(3, 300), "roll", PeerA, "4d6 = 14"),
            });

    // ---- A-2.17a: an export names nobody, and the search is shown to be able to fail.

    [Theory]
    [InlineData(PeerA)]
    [InlineData(PeerB)]
    public void NoPeerCodeAppearsAnywhereInAnExport(string peer)
    {
        var exported = SessionExportFormat.Write(LogWithTwoParticipants());

        Assert.DoesNotContain(peer, exported, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(PeerA)]
    [InlineData(PeerB)]
    public void ThePlantedPositive_TheSameSearchFindsThePeerCodeInTheRetainedLog(string peer)
    {
        // The control for the test above. Same log, same search, an artefact that DOES carry the
        // value -- so a green up there is a fact about the export and not about the search.
        var retained = RetainedLogFormat.Write(LogWithTwoParticipants());

        Assert.Contains(peer, retained, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCampaignIdDoesNotAppearInAnExport()
    {
        var log = LogWithTwoParticipants();

        var exported = SessionExportFormat.Write(log);

        // A field that persists across DIFFERENT sessions is what reopens D-20: the label is safe
        // because it is joinable to nothing, and a joinable neighbour makes it joinable by
        // combination.
        Assert.DoesNotContain(log.CampaignId.ToString(), exported, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThePlantedPositive_TheSameSearchFindsTheCampaignIdInTheRetainedLog()
    {
        var log = LogWithTwoParticipants();

        var retained = RetainedLogFormat.Write(log);

        Assert.Contains(log.CampaignId.ToString(), retained, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EntriesAreAttributedByAFileLocalLabelInOrderOfFirstAppearance()
    {
        var exported = SessionExportFormat.Write(LogWithTwoParticipants());

        // PeerA speaks first, so it is participant 1 -- assigned from the file's own contents.
        Assert.Contains("participant 1", exported, StringComparison.Ordinal);
        Assert.Contains("participant 2", exported, StringComparison.Ordinal);
    }

    [Fact]
    public void TheLabelIsNotAFunctionOfThePeerCode()
    {
        // Two logs with the SAME participants in the OPPOSITE order of first appearance. If the
        // label were derived from the peer code -- by sorting, hashing, anything -- both files would
        // agree, and the label would be joinable without ever appearing.
        var first = new RetainedLog(Guid.NewGuid(), 1, new List<LoggedEntry>
        {
            new(new LoggedStamp(1, 100), "message", PeerA, "a"),
            new(new LoggedStamp(2, 200), "message", PeerB, "b"),
        });
        var second = new RetainedLog(Guid.NewGuid(), 1, new List<LoggedEntry>
        {
            new(new LoggedStamp(1, 100), "message", PeerB, "b"),
            new(new LoggedStamp(2, 200), "message", PeerA, "a"),
        });

        var firstLabelOf = LabelOnLineFor(SessionExportFormat.Write(first), "a");
        var secondLabelOf = LabelOnLineFor(SessionExportFormat.Write(second), "a");

        Assert.NotEqual(firstLabelOf, secondLabelOf);
    }

    // ---- A-2.17b: no legend, ever. The format carries no field for a name.

    [Fact]
    public void TheFormatCarriesNoFieldForANameOrAnIdentifier()
    {
        var members = typeof(SessionExportFormat)
            .GetMembers(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Select(member => member.Name)
            .ToList();

        Assert.DoesNotContain(members, name =>
            name.Contains("Name", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Legend", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Participant", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void TheExportWriterTakesNothingButOneLog()
    {
        // A-2.16: one log, and a merge must have no way to be EXPRESSED rather than be refused.
        var writes = typeof(SessionExportFormat)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(method => method.Name == nameof(SessionExportFormat.Write))
            .ToList();

        var only = Assert.Single(writes);
        var parameter = Assert.Single(only.GetParameters());
        Assert.Equal(typeof(RetainedLog), parameter.ParameterType);
    }

    // ---- A-2.17c: the file-local sentence, at the ruled strength. Both halves fail separately.

    [Fact]
    public void TheFileSaysItsLabelsAreFileLocal()
    {
        var exported = SessionExportFormat.Write(LogWithTwoParticipants());

        Assert.Contains("these labels mean nothing outside this file", exported, StringComparison.Ordinal);
    }

    [Fact]
    public void TheFileDoesNotOVERCLAIM_ItNeverSaysTheFilesCannotBeRelated()
    {
        // The half a reasonable build gets wrong. The strong sentence is FALSE -- the host
        // timestamps everything, so two exports of one session can be aligned on time -- and
        // R-1.7a forbids publishing a claim we cannot support.
        var exported = SessionExportFormat.Write(LogWithTwoParticipants());

        Assert.DoesNotContain("cannot be related", exported, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("unrelated", exported, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ThePlantedPositive_TheSentenceSearchCanFail()
    {
        // The negative control the description asks for: assert the sentence is present, AND show
        // that the same search over an artefact without it goes red. The retained log does not
        // carry the sentence, and should not -- its labels are peer codes, not file-local ordinals.
        var retained = RetainedLogFormat.Write(LogWithTwoParticipants());

        Assert.DoesNotContain("these labels mean nothing outside this file", retained, StringComparison.Ordinal);
    }

    private static string LabelOnLineFor(string exported, string text)
    {
        var line = exported
            .Split('\n')
            .Single(candidate => candidate.EndsWith(text, StringComparison.Ordinal));

        return line.Split('\t')[3];
    }
}
