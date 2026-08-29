using System;

namespace DungeonMasterXIV.Net;

/// <summary>
/// One rendered line of the session stream: who spoke, in what register, to whom (R-2.5, R-2.6,
/// R-2.7, R-2.7a).
/// </summary>
/// <remarks>
/// <para>
/// <b>ONE EXPRESSION, RENDERED ONCE.</b> R-2.7 requires the speaker's parenthetical in every path —
/// panel, export, echo, narrow window — so there is one function producing it and the surfaces call
/// it. Two expressions meant to agree drift; one cannot disagree with itself. That is the same
/// argument <c>SessionRoleLabel</c> already makes for the roster, and this uses <c>SessionRoleLabel</c>
/// rather than restating it.
/// </para>
/// <para>
/// <b>THE HOST IS MARKED FROM <see cref="SessionRole"/>, WHICH THE SENDER CANNOT SET (R-2.7a).</b>
/// The role is assigned by the session. An earlier reading held that the speaker parenthetical was
/// itself the defence against someone labelling themselves "DM" — <b>the Product Owner withdrew that
/// as false</b>, because a member could set speaker <c>Renn</c> and display name <c>DM</c> and the
/// panel would render <c>Renn (DM)</c>: the defence rendering the impersonation.
/// </para>
/// <para>
/// <b>WHAT THIS ACTUALLY GUARANTEES, AT ITS TRUE STRENGTH AND NOT ONE NOTCH STRONGER.</b> The exact
/// authority token cannot appear in a display name: <c>DisplayName.TryParse</c> refuses <c>DM</c> and
/// <c>dm</c> (R-1.3j.6). <b>Measured, not assumed.</b> So a member cannot produce a line IDENTICAL to
/// a host-authored one.
/// <b>They can produce an adjacent one</b> — <c>the DM</c>, <c>D.M.</c> and <c>D M</c> all parse as
/// display names, exactly as R-1.3j.6's own leak list says. <b>So the property delivered is
/// non-identity, not non-resemblance</b>, and anything relying on resemblance needs its own mechanism.
/// </para>
/// <para>
/// <b>AND THAT GUARANTEE IS A COUPLING BETWEEN TWO FILES THAT DO NOT REFERENCE EACH OTHER.</b> It
/// holds only while <see cref="SessionRoleLabel"/>'s authority token is a word
/// <c>DisplayName</c> reserves. Neither file mentions the other, so either could move and break it
/// silently — which is why <c>TheAuthorityTokenCannotBeWornByAMemberTests</c> asserts the coupling
/// directly rather than leaving it to hold by luck.
/// </para>
/// </remarks>
public static class MessageLine
{
    /// <summary>
    /// Renders the speaker attribution: <c>Character (Player)</c>, or the person alone when they
    /// speak as themselves (R-2.7).
    /// </summary>
    /// <remarks>
    /// <b>Never <c>Tuka (Tuka)</c>.</b> Speaking as yourself renders the person alone — the
    /// parenthetical exists to show that the character and the person differ, so repeating the name
    /// says nothing and reads as a defect.
    /// </remarks>
    /// <param name="speaker">The character being spoken as, or the person's own name.</param>
    /// <param name="person">Who the session knows the sender as.</param>
    public static string Attribution(string speaker, DisplayName person) =>
        string.Equals(speaker, person.Value, StringComparison.Ordinal)
            ? person.Value
            : $"{speaker} ({person.Value})";

    /// <summary>
    /// The whole line: register affix, speaker attribution, authority marker, and privacy marker.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Every marker here is TEXTUAL and survives the loss of colour (R-2.5) and the loss of width
    /// (R-2.7a's echo case).</b> A one-line plain-text echo carries the same string this returns, so
    /// nothing is dropped for a narrow surface — which is the surface R-2.7a names as where a build
    /// will fail.
    /// </para>
    /// <para>
    /// <b>The authority marker comes from <paramref name="role"/> and never from the message.</b>
    /// A sender supplies speaker, kind and target; the session supplies the role.
    /// </para>
    /// </remarks>
    /// <param name="kind">The register (R-2.5).</param>
    /// <param name="target">Who it is addressed to (R-2.6).</param>
    /// <param name="speaker">The character being spoken as.</param>
    /// <param name="person">Who the session knows the sender as.</param>
    /// <param name="role">The sender's session-assigned role (R-2.7a).</param>
    /// <param name="text">What was said.</param>
    public static string Render(
        MessageKind kind,
        MessageTarget target,
        string speaker,
        DisplayName person,
        SessionRole role,
        string text)
    {
        var who = Attribution(speaker, person);
        var authority = role is SessionRole.DungeonMaster ? $"[{SessionRoleLabel.For(role)}] " : string.Empty;
        var privacy = target is MessageTarget.DungeonMasterOnly ? "(private) " : string.Empty;

        return kind switch
        {
            MessageKind.Emote => $"{privacy}{authority}* {who} {text}",
            MessageKind.OutOfCharacter => $"{privacy}{authority}(OOC) {who}: {text}",
            MessageKind.InCharacter => $"{privacy}{authority}{who}: {text}",

            // A switch over a closed set is exhaustive only against the set at the time it was
            // written. A kind added later reaches here rather than rendering as in-character, which
            // would silently mislabel a register the reader is relying on to be distinguishable.
            _ => throw new ArgumentOutOfRangeException(
                nameof(kind), kind, "Unknown message kind — R-2.5 requires each to be distinguishable."),
        };
    }
}
