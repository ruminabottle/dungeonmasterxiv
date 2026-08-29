using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Net;

/// <summary>
/// What the host holds for a member that dropped, and gives back when it returns (R-2.10).
/// </summary>
/// <remarks>
/// <para>
/// <b>Holding a member's messages is only safe because there is no player-to-player privacy</b>, and
/// R-2.6 states that coupling as load-bearing: every message the host queues here is one the host is
/// already a legitimate party to. <b>If player-to-player privacy is ever added, this type is holding
/// content it must not read</b> — R-2.6 and R-2.10 may not be revisited alone, and this remark is
/// here so whoever adds that privacy meets the coupling at the code as well as in the PRD.
/// </para>
/// <para>
/// <b>THIS IS NOT HISTORY FOR NEWCOMERS.</b> A client that was never admitted receives nothing
/// (A-2.6). Nothing is held for a peer that never dropped, so a never-admitted client has no
/// entry here to replay — the two cases are kept apart by there being nothing to give rather than
/// by a check that could be forgotten.
/// </para>
/// <para>
/// <b>Re-sending is REQUIRED, not forbidden.</b> A-2.6a's clause "a build that restores the log by
/// re-sending fails" was STRUCK on 2026-08-29 because decision 7 requires exactly that re-send. A
/// reading that makes re-sending a failure is a reading of the struck version.
/// </para>
/// <para>
/// <b>IT DECIDES NO CAPACITY, AND THAT IS DELIBERATE.</b> R-2.10 says a gap "that could not be held"
/// is marked; it does not say what makes something unholdable, and the PRD states no bound anywhere.
/// So the caller — which owns whatever limit exists — reports the loss through
/// <see cref="NoteGap"/>, and this type guarantees only that a reported loss is MARKED. Choosing a
/// number here would settle a product question by building a mechanism for it.
/// </para>
/// </remarks>
internal sealed class MissedMessages
{
    private readonly Dictionary<PeerCode, List<StreamEntry>> _held = new();
    private readonly HashSet<PeerCode> _gapped = new();

    /// <summary>Whether anything is being held for <paramref name="member"/>.</summary>
    public bool IsHoldingFor(PeerCode member) => _held.ContainsKey(member) || _gapped.Contains(member);

    /// <summary>
    /// Holds one entry that <paramref name="member"/> was not there to receive.
    /// </summary>
    /// <param name="member">The dropped member this entry is being kept for.</param>
    /// <param name="entry">What they missed.</param>
    public void Hold(PeerCode member, StreamEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        if (!_held.TryGetValue(member, out var entries))
        {
            _held[member] = entries = new List<StreamEntry>();
        }

        entries.Add(entry);
    }

    /// <summary>
    /// Records that something for <paramref name="member"/> could not be held (R-2.10).
    /// </summary>
    /// <remarks>
    /// <b>The caller owns the reason and this type owns the consequence.</b> Whatever bound applies —
    /// a capacity, an expiry, a message too large — the decision is not made here; what is guaranteed
    /// here is that once a loss is reported, the replay says so.
    /// </remarks>
    /// <param name="member">The member whose stream is now incomplete.</param>
    public void NoteGap(PeerCode member) => _gapped.Add(member);

    /// <summary>
    /// Gives back what <paramref name="member"/> missed, marked if any of it was lost (R-2.10, A-2.6a).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>The marker comes FIRST, because it describes what is missing before what survived.</b> A
    /// member reading their stream top to bottom meets "something is missing here" and then the
    /// entries that follow it, which is the order that makes the hole legible.
    /// </para>
    /// <para>
    /// <b>The stamp is the HOST's, supplied rather than invented.</b> R-2.3/R-2.4 make sequencing and
    /// timestamping the host's, and <see cref="HostSequencer"/> is the one place that does it. A
    /// marker minting its own stamp would be a second sequencer, which is the drift R-2.4 exists to
    /// prevent.
    /// </para>
    /// <para>
    /// <b>Replaying forgets.</b> What has been given back is no longer missed, and a member that
    /// dropped again starts a new hold rather than receiving the old one twice.
    /// </para>
    /// </remarks>
    /// <param name="member">The returning member.</param>
    /// <param name="stamp">The host's sequencer, used only if a gap must be marked.</param>
    public IReadOnlyList<StreamEntry> Replay(PeerCode member, Func<StreamStamp> stamp)
    {
        ArgumentNullException.ThrowIfNull(stamp);

        var held = _held.TryGetValue(member, out var entries) ? entries : new List<StreamEntry>();
        var marker = _gapped.Contains(member)
            ? new[] { new StreamEntry(stamp(), StreamEventKind.Gap, member, string.Empty) }
            : [];

        _held.Remove(member);
        _gapped.Remove(member);

        return marker.Concat(held).ToList();
    }

    /// <summary>Drops the hold without replaying it — the seat is gone, so there is nobody to give it to.</summary>
    /// <param name="member">The member whose seat ended.</param>
    public void Forget(PeerCode member)
    {
        _held.Remove(member);
        _gapped.Remove(member);
    }
}
