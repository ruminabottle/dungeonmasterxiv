using System;
using System.Collections.Generic;
using System.Text;

namespace DungeonMasterXIV.Data;

/// <summary>
/// The export's on-disk text format (R-2.12). <b>THIS IS THE EXPORT.</b>
/// </summary>
/// <remarks>
/// <para>
/// <b>SEPARATE FROM <see cref="RetainedLogFormat"/> BY REQUIREMENT, NOT BY TASTE.</b> That type
/// writes the RETAINED log, where a peer code is permitted (A-1.11a-note: a retained log is not an
/// export, and D-8 lets local history hold real names). <b>Its <c>Write</c> is FORBIDDEN here</b> —
/// its line emits <c>entry.Peer</c>, and <b>A-1.11c</b> forbids a participant identifier in an
/// export. The two produce right-looking bytes from the same input, which is exactly why they are
/// two types and not one with a flag.
/// </para>
/// <para>
/// <b>WHAT AN ENTRY IS ATTRIBUTED BY (A-2.17a): a label local to this one file.</b> It is assigned
/// at write time, in order of first appearance, <b>from the file's own contents</b> — it is derived
/// from no stored and no transmitted value. <b>The discriminator D-20 gives, so it is not
/// re-argued: an identifier is a value that can be JOINED.</b> A peer code and a participant id
/// qualify because the system assigns and stores them; this label exists only inside its file, and
/// exporting the same session twice may produce different labels.
/// </para>
/// <para>
/// <b>THE CAMPAIGN ID IS DELIBERATELY ABSENT, and that is the one omission most likely to be read
/// as an oversight.</b> <see cref="RetainedLogFormat.Write"/> emits <c>campaign:</c> and it is
/// correct there. Here it would be <b>a field that persists across DIFFERENT sessions</b>, which is
/// the precise shape D-20 names as reopening it: the label is safe because it is joinable to
/// nothing, and a joinable neighbour would make it joinable BY COMBINATION. Two exports carrying one
/// campaign id can be joined on it and their labels aligned. <b>Do not add it back, and read D-20
/// before adding any field to this format.</b>
/// </para>
/// <para>
/// <b>Time IS carried, and that is not an oversight either.</b> A-2.17c settles it: the host
/// sequences and timestamps everything, so two exports of ONE session can be aligned on time and
/// their labels partially mapped. That discloses nothing — both sides are anonymous and it is the
/// same session — and it is why <see cref="LabelsAreFileLocal"/> is worded the way it is rather than
/// more strongly.
/// </para>
/// </remarks>
public static class SessionExportFormat
{
    /// <summary>The header line, so an exported file is identifiable as one.</summary>
    public const string Header = "# DungeonMasterXIV session export";

    /// <summary>The format's version, written into every file.</summary>
    /// <remarks>
    /// Same reasoning as <see cref="RetainedLogFormat.FormatVersion"/>: a written format without a
    /// version cannot be changed safely once a file exists on a user's machine. Numbered from 1 in
    /// its own right — this is not the retained log's version and the two are free to diverge.
    /// </remarks>
    public const int FormatVersion = 1;

    /// <summary>
    /// The sentence stating that this file's labels carry no meaning anywhere else (A-2.17c).
    /// </summary>
    /// <remarks>
    /// <b>THE STRENGTH OF THIS SENTENCE IS RULED, AND THE STRONGER ONE IS FALSE.</b> It says the
    /// labels mean nothing outside this file. It does <b>NOT</b> say <i>"these files cannot be
    /// related"</i> — the host timestamps everything, so two exports of one session CAN be aligned
    /// on time and their labels partially mapped. <b>R-1.7a forbids publishing a claim we cannot
    /// support</b>, so overclaiming fails A-2.17c exactly as omitting the sentence does. Both halves
    /// of that row fail separately.
    /// </remarks>
    public const string LabelsAreFileLocal = "these labels mean nothing outside this file";

    /// <summary>
    /// Writes <paramref name="log"/> out as an export. <b>One log, and deliberately no overload
    /// taking more than one</b> — A-2.16 fails a build that merges logs, and a merge must have no
    /// way to be expressed rather than be caught by a check.
    /// </summary>
    /// <param name="log">The owner's own log, and nobody else's.</param>
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

    /// <summary>
    /// Assigns each peer in <paramref name="log"/> a label local to this file, in order of first
    /// appearance.
    /// </summary>
    /// <remarks>
    /// <b>The ordering is taken from the file's own contents and nothing else</b> (A-2.17a). It does
    /// not sort by peer code, because sorting by the forbidden value would make the label a
    /// deterministic function OF that value — two exports naming the same participants would then
    /// agree on labels, and the label would be joinable after all without ever appearing.
    /// </remarks>
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

    /// <summary>One entry as one line: order, instant, kind, the file-local label, and what.</summary>
    /// <remarks>
    /// <b>The label, NEVER the peer code and never a display name</b> (A-2.17a, A-1.11c, D-8). The
    /// shape is otherwise the retained log's, so a reader of one can read the other — the difference
    /// is the one field that carries an identity.
    /// </remarks>
    private static string LineFor(LoggedEntry entry, string label) =>
        string.Join(
            '\t',
            entry.Stamp.Sequence,
            entry.Stamp.AtUtcTicks,
            entry.Kind,
            label,
            RetainedLogFormat.Escape(entry.Text));
}
