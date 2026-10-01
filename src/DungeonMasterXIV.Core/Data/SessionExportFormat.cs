using System;
using System.Collections.Generic;
using System.Text;

namespace DungeonMasterXIV.Data;

/// <summary>Writes a session log for export, replacing each peer with a file-local label such as participant 1.</summary>
public static class SessionExportFormat
{
    public const string Header = "# DungeonMasterXIV session export";

    public const int FormatVersion = 1;

    public const string LabelsAreFileLocal = "these labels mean nothing outside this file";

    public static string Write(RetainedLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        var text = new StringBuilder();
        text.AppendLine(Header);
        text.AppendLine($"version: {FormatVersion}");
        text.AppendLine($"# {LabelsAreFileLocal}");
        text.AppendLine($"ended: {log.EndedAtUtcTicks}");
        text.AppendLine();

        var labels = LabelsFor(log);

        foreach (var entry in log.Entries)
        {
            text.AppendLine(LineFor(entry, labels[entry.Peer]));
        }

        return text.ToString();
    }

    private static IReadOnlyDictionary<string, string> LabelsFor(RetainedLog log)
    {
        var labels = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var entry in log.Entries)
        {
            if (!labels.ContainsKey(entry.Peer))
            {
                labels[entry.Peer] = $"participant {labels.Count + 1}";
            }
        }

        return labels;
    }

    private static string LineFor(LoggedEntry entry, string label) =>
        string.Join(
            '\t',
            entry.Stamp.Sequence,
            entry.Stamp.AtUtcTicks,
            entry.Kind,
            label,
            RetainedLogFormat.Escape(entry.Text));
}
