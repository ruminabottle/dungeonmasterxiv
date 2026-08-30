using System;
using System.Collections.Generic;

namespace DungeonMasterXIV.Data;

/// <summary>
/// A session's log as the DM's client keeps it: which campaign it belonged to, when it happened,
/// and what was said (R-2.12).
/// </summary>
/// <param name="CampaignId">The campaign the session was run under. The key deletion uses.</param>
/// <param name="EndedAtUtcTicks">When the session ended, so logs can be listed newest first.</param>
/// <param name="Entries">The lines, in the order the host sequenced them.</param>
/// <remarks>
/// <para>
/// <b>A ROLL LOG IS NOT CAMPAIGN DATA, AND THIS TYPE KEEPS THAT TRUE.</b> R-2.12 draws the line: a
/// campaign persists a <i>roster</i>, which is metadata; <b>a log is what people said and did.</b>
/// So a log is stored beside campaigns rather than inside <c>CampaignDocument</c> — it carries a
/// campaign id so the delete control can reach it, and nothing more of the campaign than that.
/// </para>
/// <para>
/// <b>Only the DM's client builds one.</b> A player's log dies with the session unless exported,
/// which is a rule about who retains rather than about the shape of a log — see
/// <see cref="LogRetention"/>, where that decision lives and is testable.
/// </para>
/// <para>
/// <b>One log, one owner, always.</b> There is no constructor, method or codec here that takes two
/// logs or merges them. That absence is load-bearing: A-2.16 fails a build that merges logs, and
/// the Spec Owner's ruling is that a filtered export would <i>build the shape D-13 forbids</i> —
/// so the guarantee is that a log only ever contains what its own client received, and an export
/// is a function of exactly one of them.
/// </para>
/// </remarks>
public sealed record RetainedLog(Guid CampaignId, long EndedAtUtcTicks, IReadOnlyList<LoggedEntry> Entries);
