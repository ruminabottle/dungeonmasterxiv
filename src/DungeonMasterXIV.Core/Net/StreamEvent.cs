namespace DungeonMasterXIV.Net;

/// <summary>What one stream entry records (R-2.3).</summary>
/// <remarks>
/// <para>
/// <b>Messages, rolls and MEMBERSHIP EVENTS, because R-2.3 names all three.</b> The roster shows who
/// is here NOW; the stream shows WHEN it changed. Neither replaces the other, so a build that only
/// logs speech satisfies half a requirement.
/// </para>
/// <para>
/// <b>ADMISSION MECHANICS ARE NOT HERE AND THEIR ABSENCE IS DELIBERATE.</b> Who asked, who was
/// refused and what a fingerprint said belong to PRD-1's admission flow. <b>The stream records THAT
/// membership changed, not HOW it was negotiated</b> — so there is no Refused, no Asked, and no
/// fingerprint anywhere in this enum, and adding one later is a scope decision rather than a detail.
/// </para>
/// </remarks>
public enum StreamEventKind
{
    /// <summary>Someone said something.</summary>
    Message,

    /// <summary>Someone rolled. The evaluator stays a leaf; this only records that it happened.</summary>
    Roll,

    /// <summary>Someone joined the session.</summary>
    Joined,

    /// <summary>Someone left deliberately.</summary>
    Left,

    /// <summary>Someone stopped answering and the host recorded a drop.</summary>
    Dropped,

    /// <summary>Someone who had dropped came back.</summary>
    Reconnected,

    /// <summary>
    /// Something this member missed could not be held, so their stream is incomplete here (R-2.10).
    /// </summary>
    /// <remarks>
    /// <b>The marker exists so the omission is VISIBLE rather than silent.</b> R-2.10: a member is
    /// "told that something is missing rather than shown a stream that silently omits it". A replay
    /// that quietly skips what it could not hold satisfies the delivery half of A-2.6a and fails the
    /// marking half — and it fails invisibly, because a stream with a hole in it looks exactly like a
    /// stream with nothing in the hole.
    /// </remarks>
    Gap,
}
