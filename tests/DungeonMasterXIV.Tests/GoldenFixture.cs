using System;
using DungeonMasterXIV.Data;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// The fixture the byte-identity proof runs on. <b>Deliberately exercises every moving part of the
/// format</b> — header, version line, campaign, ended stamp, several entries, and text carrying a
/// newline, a tab and a backslash. A golden captured on a log with no escaping in it would prove
/// byte-identity of a case with no format in it.
/// </summary>
internal static class GoldenFixture
{
    internal static readonly Guid Campaign = new("7f3a1c88-0d2e-4b6a-9c11-5e8d2f40a913");

    internal static RetainedLog Log() =>
        new(
            Campaign,
            638_000_000_000_000_000L,
            [
                new LoggedEntry(new LoggedStamp(1, 638_000_000_000_000_001L), "message", "BCDFGH", "Renn swings at the troll"),
                new LoggedEntry(new LoggedStamp(2, 638_000_000_000_000_002L), "roll", "JKMNPR", "4d6dl1+2"),
                new LoggedEntry(new LoggedStamp(3, 638_000_000_000_000_003L), "message", "BCDFGH", "a\ttab, a\nnewline and a \\ backslash"),
                new LoggedEntry(new LoggedStamp(4, 638_000_000_000_000_004L), "left", "JKMNPR", string.Empty),
            ]);
}
