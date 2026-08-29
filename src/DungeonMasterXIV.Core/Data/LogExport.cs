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
    /// Writes <paramref name="log"/> out as text. <b>One log. There is deliberately no overload
    /// taking more than one.</b>
    /// </summary>
    /// <param name="log">The owner's own log, and nobody else's.</param>
    public static string Write(RetainedLog log)
    {
        ArgumentNullException.ThrowIfNull(log);

        var text = new StringBuilder();
        text.AppendLine(Header);
        text.AppendLine($"campaign: {log.CampaignId}");
        text.AppendLine($"ended: {log.EndedAtUtcTicks}");
        text.AppendLine();

        foreach (var entry in log.Entries)
        {
            text.AppendLine(LineFor(entry));
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
            entry.Text);

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
