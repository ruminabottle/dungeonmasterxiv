using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DungeonMasterXIV.Data;

/// <summary>
/// A log written out for the person who owns it (R-2.12).
/// </summary>
/// <remarks>
/// <para>
/// <b>AN EXPORT IS A FUNCTION OF EXACTLY ONE LOG, AND THAT IS THE WHOLE OF A-2.16.</b> There is no
/// overload, no collection parameter and no merge here, and the absence is deliberate rather than
/// unfinished: the criterion fails a build that merges logs, and a merge is not something to be
/// prevented by a check — it is something that must have no way to be expressed.
/// </para>
/// <para>
/// <b>The Spec Owner's ruling is why there is no filter either.</b> A client's log holds only what
/// that client received, so <i>"contains only what its owner could see"</i> holds by construction —
/// and a filtering exporter would have to be given a view wider than its owner's in order to narrow
/// it, which is <b>building the very shape D-13 forbids</b>. The safe design is the one that never
/// has the wider view in its hands.
/// </para>
/// <para>
/// <b>Export is never automatic</b> (A-2.17). Nothing here is called by a session ending; producing
/// text is a deliberate act by a caller, and this type has no clock, no file access and no
/// subscription.
/// </para>
/// </remarks>
public static class LogExport
{
    /// <summary>The header line, so an exported file is identifiable as one.</summary>
    public const string Header = "# DungeonMasterXIV session log";

    /// <summary>
    /// The format's version, written into every file.
    /// </summary>
    /// <remarks>
    /// <b>A written format without a version cannot be changed safely once a file exists on a
    /// user's machine</b> — a reader meeting an unfamiliar layout has no way to tell "written by a
    /// newer build" from "corrupt", and must guess. Costing one line now buys the ability to know
    /// later, which is the whole reason the Deployment Manager held this PR for the format rather
    /// than for the wiring.
    /// </remarks>
    public const int FormatVersion = 1;

    /// <summary>
    /// Writes <paramref name="log"/> out as text. <b>One log. There is deliberately no overload
    /// taking more than one.</b>
    /// </summary>
    /// <param name="log">The owner's own log, and nobody else's.</param>
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

    /// <summary>
    /// Makes free text safe to put in a line-and-tab record: <b>one entry stays one line, and no
    /// typed character can invent a field.</b>
    /// </summary>
    /// <remarks>
    /// <b>WITHOUT THIS, A USER CAN FORGE A LOG ENTRY BY TYPING ONE.</b> The first version of this
    /// file escaped nothing, so a message containing a newline followed by tab-separated fields
    /// produced MORE LINES THAN THERE WERE ENTRIES — and anything reading the file back saw an entry
    /// carrying a sequence number and an author the host never issued. That is the R-2.7
    /// impersonation surface arriving through the export instead of the panel, and it was found by
    /// the code reviewer rather than by me.
    /// <para>
    /// <b>The backslash is escaped FIRST and unescaped LAST.</b> Any other order lets a typed
    /// <c>\n</c> survive the round trip as a real newline, which reopens the hole through the escape
    /// mechanism itself.
    /// </para>
    /// </remarks>
    /// <param name="text">Free text as the user typed it.</param>
    public static string Escape(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        return text
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\t", "\\t", StringComparison.Ordinal)
            .Replace("\r", "\\r", StringComparison.Ordinal)
            .Replace("\n", "\\n", StringComparison.Ordinal);
    }

    /// <summary>Reverses <see cref="Escape"/>, so a log's content is not lost to being made safe.</summary>
    /// <param name="field">One escaped field, as written.</param>
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

            // Read the pair as one unit, so a literal backslash cannot combine with the character
            // after it to produce a control character the user never typed.
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

    /// <summary>One entry as one line: order, instant, kind, who, and what.</summary>
    /// <remarks>
    /// <b>The peer code, never a display name</b> (A-2.31, D-8). A name in an exported file is a
    /// portable identifier that has left the campaign holding it, which is the thing D-8 exists to
    /// prevent — and an export is precisely the moment data stops being governed by the store.
    /// </remarks>
    private static string LineFor(LoggedEntry entry) =>
        string.Join(
            '\t',
            entry.Stamp.Sequence,
            entry.Stamp.AtUtcTicks,
            entry.Kind,
            entry.Peer,
            Escape(entry.Text));

    /// <summary>How many lines an export of <paramref name="log"/> will carry, for a caller's prompt.</summary>
    public static int LineCount(RetainedLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return log.Entries.Count;
    }

    /// <summary>Whether an export would contain anything at all.</summary>
    public static bool HasAnything(RetainedLog log) => LineCount(log) > 0;

    /// <summary>The peer codes appearing in <paramref name="log"/>, for a caller that wants to say who is in it.</summary>
    public static IReadOnlyList<string> Participants(RetainedLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return log.Entries.Select(entry => entry.Peer).Distinct().ToList();
    }
}
