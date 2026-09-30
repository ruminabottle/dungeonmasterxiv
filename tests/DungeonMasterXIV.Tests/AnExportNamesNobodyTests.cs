using System;
using System.Collections.Generic;
using DungeonMasterXIV.Data;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>An exported session log names nobody: no participant's peer code appears in it.</summary>
public class AnExportNamesNobodyTests
{
    private const string PeerA = "BCDFGH";
    private const string PeerB = "JKLMNP";

    [Fact]
    public void NoPeerCodeAppearsAnywhereInAnExport()
    {
        var log = new RetainedLog(
            Guid.Parse("11111111-2222-3333-4444-555555555555"),
            638_000_000_000_000_000L,
            new List<LoggedEntry>
            {
                new(new LoggedStamp(1, 100), "joined", PeerA, string.Empty),
                new(new LoggedStamp(2, 200), "message", PeerB, "hello"),
                new(new LoggedStamp(3, 300), "roll", PeerA, "4d6 = 14"),
            });

        var exported = SessionExportFormat.Write(log);

        // The export carries the session's content, so the searches below are over a real file.
        Assert.Contains("hello", exported, StringComparison.Ordinal);
        Assert.DoesNotContain(PeerA, exported, StringComparison.Ordinal);
        Assert.DoesNotContain(PeerB, exported, StringComparison.Ordinal);
    }
}
