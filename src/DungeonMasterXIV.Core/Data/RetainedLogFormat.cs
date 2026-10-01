using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Data;

/// <summary>Escapes log text for a tab-separated line and summarises a session log's entries.</summary>
public static class RetainedLogFormat
{
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    public static int LineCount(RetainedLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return log.Entries.Count;
    }

    public static bool HasAnything(RetainedLog log) => LineCount(log) > 0;

    public static IReadOnlyList<string> Participants(RetainedLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return log.Entries.Select(entry => entry.Peer).Distinct().ToList();
    }
}
