using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// How a <see cref="SessionRole"/> is written in the roster, including one that is not a role.
/// </summary>
/// <remarks>
/// <para>
/// <b>An unknown role is a rendering decision, and it is made here rather than met at draw time.</b>
/// <c>SessionRole</c> is an enum over the wire, so it can carry an int matching no defined case —
/// BUG-57 closed the text case, not this one. A newer client, a future role, or a corrupted value
/// all arrive the same way, and a <c>switch</c> with no arm for them throws inside a draw call.
/// </para>
/// <para>
/// <b>The rule: an unrecognised role renders NO label, and the participant still appears.</b> Both
/// halves matter and both are the conservative choice over an obvious alternative.
/// </para>
/// <list type="bullet">
/// <item>
/// <b>Not "Player".</b> Defaulting an unknown value to the lowest role states something we do not
/// know. If a later version adds a role above Player, every client older than it would quietly
/// describe those participants as ordinary players — a wrong claim about what someone may do, which
/// is worse than saying nothing.
/// </item>
/// <item>
/// <b>Not hidden.</b> Dropping the entry would misrepresent MEMBERSHIP, and membership is what
/// R-1.3f is about — the DM would see a session smaller than it is. The name is the roster's
/// purpose; the role qualifies it. Losing the qualifier costs less than losing the participant.
/// </item>
/// </list>
/// </remarks>
public static class SessionRoleLabel
{
    /// <summary>The label for a role, or <c>null</c> when the value is not a role we know.</summary>
    /// <remarks>
    /// <c>null</c> rather than an empty string or a placeholder, so a caller has to decide what
    /// absence looks like instead of concatenating something meaningless into a line.
    /// </remarks>
    public static string? For(SessionRole role) => role switch
    {
        SessionRole.Player => "Player",
        SessionRole.Assistant => "Assistant",
        SessionRole.DungeonMaster => "DM",

        // Not a default that guesses. Every value NOT above is unknown by construction, so adding a
        // case to the enum without adding it here yields no label rather than a wrong one -- and
        // EveryDefinedRoleHasALabel fails, so it cannot ship that way unnoticed.
        _ => null,
    };

    /// <summary>Whether this value is a role this build knows.</summary>
    public static bool IsKnown(SessionRole role) => For(role) is not null;
}
