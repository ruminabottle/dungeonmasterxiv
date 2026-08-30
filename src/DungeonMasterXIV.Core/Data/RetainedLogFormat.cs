using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DungeonMasterXIV.Data;

/// <summary>
/// The retained log's on-disk text format (R-2.12). <b>THIS IS NOT THE EXPORT.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>IT WAS CALLED <c>LogExport</c> UNTIL SQ-114, AND THE NAME WAS THE HAZARD.</b> An export is
/// <b>AN ACT</b> — something a person asks for — not a path (A-2.17); a retained log is written
/// automatically, so it is not one. The old name would have led whoever builds R-2.12's real export
/// straight here, to a type whose <see cref="Write"/> already produces the right-looking output.
/// <b>Reusing it there would put a peer code into a genuine export, and nothing would complain:</b>
/// every test here is about the retained log, where the peer code is permitted (A-1.11a-note), and the
/// bytes are identical. Renamed rather than annotated, because a comment saying <i>"this name is
/// wrong"</i> leaves the attractor in place.
/// </para>
/// <para>
/// <b>ONE LOG. THERE IS NO OVERLOAD, NO COLLECTION PARAMETER AND NO MERGE</b>, and the absence is
/// deliberate rather than unfinished: A-2.16 fails a build that merges logs, and <b>a merge is not
/// something to be prevented by a check — it is something that must have no way to be expressed.</b>
/// That prohibition survives SQ-109 unchanged and is the live half of A-2.16.
/// </para>
/// <para>
/// <b>There is no owner filter, and SQ-109 ruled that is correct rather than missing.</b> A
/// participant who may not see a result never RECEIVES one under D-13 (A-2.15), so it was never in
/// this client's log to be removed — <i>"the old row implied a FILTER and there is nothing to
/// filter."</i> A filtering writer would have to be handed a view wider than its owner's in order to
/// narrow it, which is building the very shape D-13 forbids.
/// </para>
/// <para>
/// <b>THIS PARAGRAPH USED TO SAY "EXPORT IS NEVER AUTOMATIC. NOTHING HERE IS CALLED BY A SESSION
/// ENDING", AND THE RENAME IS WHAT EXPOSED IT AS FALSE.</b> This type is reached from
/// <c>RetainedLogStore.Retain</c>, which the session teardown drives — <b>the automatic path is the
/// only caller it has.</b> A-2.17's "never automatic" governs the EXPORT, which does not exist yet;
/// it never governed this. The sentence was true of the concept the old name named and false of the
/// code it sat on.
/// </para>
/// </remarks>
public static class RetainedLogFormat
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

    /// <summary>How many lines <paramref name="log"/> will occupy when written, for a caller's prompt.</summary>
    public static int LineCount(RetainedLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return log.Entries.Count;
    }

    /// <summary>
    /// Whether <paramref name="log"/> holds anything at all, for a caller deciding whether the
    /// session-end offer has anything to offer (R-2.12).
    /// </summary>
    public static bool HasAnything(RetainedLog log) => LineCount(log) > 0;

    /// <summary>The peer codes appearing in <paramref name="log"/>, for a caller that wants to say who is in it.</summary>
    public static IReadOnlyList<string> Participants(RetainedLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        return log.Entries.Select(entry => entry.Peer).Distinct().ToList();
    }
}
