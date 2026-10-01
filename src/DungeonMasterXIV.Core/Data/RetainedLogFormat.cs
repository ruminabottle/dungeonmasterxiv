using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DungeonMasterXIV.Data;

/// <summary>Writes a session log as a header and tab-separated escaped lines, and summarises its contents.</summary>
public static class RetainedLogFormat
{
    public const string Header = "# DungeonMasterXIV session log";

    public const int FormatVersion = 1;

    public static string Write(RetainedLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        var text = new StringBuilder();
        text.AppendLine(Header);
        text.AppendLine($"version: {FormatVersion}");
        text.AppendLine($"campaign: {log.CampaignId}");
        text.AppendLine($"ended: {log.EndedAtUtcTicks}");
        text.AppendLine();

        foreach (var entry in log.Entries)
        {
            text.AppendLine(LineFor(entry));
        }

        return text.ToString();
    }

    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    public static string Unescape(string field)
    {
        ArgumentNullException.ThrowIfNull(field);

        var text = new StringBuilder(field.Length);
        for (var i = 0; i < field.Length; i++)
        {
            if (field[i] != '\\' || i + 1 >= field.Length)
            {
                text.Append(field[i]);
                continue;
            }

            text.Append(field[++i] switch
            {
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                var other => other,
            });
        }

        return text.ToString();
    }

    private static string LineFor(LoggedEntry entry) =>
        string.Join(
            '\t',
            entry.Stamp.Sequence,
            entry.Stamp.AtUtcTicks,
            entry.Kind,
            entry.Peer,
            Escape(entry.Text));

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
