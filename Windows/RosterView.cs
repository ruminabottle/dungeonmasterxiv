using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// Renders who is in the session, for whichever side is asking.
/// </summary>
/// <remarks>
/// <para>
/// <b>One renderer for both views on purpose.</b> The DM reads its own audience and a player
/// reads the roster the host sent, so the two arrive as different types from different places —
/// but what a participant LOOKS like must not depend on which side is drawing them. Two
/// renderers would be two places for the unknown-role rule to drift.
/// </para>
/// <para>
/// <b>Its own type is what keeps that true across the split (DMXENG-15).</b> This was a private
/// method on <see cref="SessionWindow"/> while both callers were also on
/// <see cref="SessionWindow"/>. Moving the joiner's surface into <see cref="JoinFlowView"/> put the
/// two call sites in two files, and a private method cannot serve both — so the choice was one
/// shared renderer with a home, or a second copy. The paragraph above says which.
/// </para>
/// <para>
/// <b>An unrecognised role renders no label and the participant still appears</b>, per
/// <see cref="SessionRoleLabel"/>. The reasoning lives there because it is a decision about
/// meaning rather than about drawing.
/// </para>
/// </remarks>
internal static class RosterView
{
    /// <summary>Draws one line per participant, in the order given.</summary>
    /// <param name="participants">Who to draw, and what each may do.</param>
    public static void Draw(IEnumerable<(string Name, SessionRole Role)> participants)
    {
        foreach (var (name, role) in participants)
        {
            // The name is a label and never an identity: names are self-declared and two people may
            // hold the same one (A-1.2d), so nothing here keys on it or de-duplicates by it.
            ImGui.TextUnformatted(
                SessionRoleLabel.For(role) is { } label ? $"  {name} ({label})" : $"  {name}");
        }
    }
}
