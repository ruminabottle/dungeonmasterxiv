namespace DungeonMasterXIV.Net;

/// <summary>Who a message is addressed to (R-2.6).</summary>
/// <remarks>
/// <para>
/// <b>TWO TARGETS, AND THE ABSENCE OF A THIRD IS A DECISION RATHER THAN AN OMISSION.</b>
/// Player-to-player privacy is deliberately not built: FFXIV's <c>/tell</c> already does private
/// player-to-player talk, and what only this product offers is private talk that is part of the
/// session record and can carry a roll. Building a worse <c>/tell</c> adds surface and competes with
/// the game.
/// </para>
/// <para>
/// <b>THIS IS LOAD-BEARING ON R-2.10 AND THE COUPLING MUST NOT BE FORGOTTEN.</b> Holding messages for
/// a dropped member is only safe because there is no player-to-player privacy — every message the
/// host queues is one the host is already a legitimate party to. <b>If a third target is ever added,
/// <see cref="MissedMessages"/> would be holding content it must not read, and R-2.10 must change in
/// the same breath.</b> Neither may be revisited alone.
/// </para>
/// </remarks>
public enum MessageTarget
{
    /// <summary>Everyone in the session.</summary>
    Everyone,

    /// <summary>The DM and the sender, and nobody else.</summary>
    DungeonMasterOnly,
}
