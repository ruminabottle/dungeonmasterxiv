using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// Whether the joiner got to check who the DM is, and being told when they did not (R-1.3a-i, A-1.3f-1).
/// </summary>
/// <remarks>
/// <para>
/// <b>Split out of <see cref="JoinFlowView"/> by DMXENG-75, and it is a PURE MOVE.</b> No behaviour
/// changes here and no criterion is claimed. <c>JoinFlowView.Draw</c> was 121 lines against a
/// 60-line method block, and R-1.3g's client half has to add to that surface.
/// </para>
/// <para>
/// <b>The three constants are what prove this is a seam rather than a slice.</b> All of the copy
/// <see cref="JoinFlowView"/> carried belonged to this one concern — read your code aloud, there is
/// no code to read, and you were admitted without ever having one — so the view it left behind now
/// holds no wording at all. Prose moving with the code it explains is the test an extraction has to
/// pass; a slice would have left the constants stranded above a caller.
/// </para>
/// <para>
/// <b>TWO MOMENTS, ONE SUBJECT, WHICH IS WHY THEY TRAVELLED TOGETHER.</b> The comparison happens
/// while the decision is pending; the warning is said once it is made, and only to a joiner who
/// never had anything to compare. Splitting them would put the question in one file and its
/// aftermath in another.
/// </para>
/// <para>
/// <b>An instance holding nothing, which is the house pattern rather than an oversight.</b> Every
/// view under <c>Windows/</c> is an instance owned by its parent — <see cref="AdmissionPromptView"/>,
/// <see cref="HostCampaignPicker"/>, <see cref="JoinRequestForm"/> — and <c>RosterView</c> is the
/// SINGLE static one. <c>BothRosterViewsRenderThroughOnePlaceTests</c> asserts exactly one
/// <c>static void Draw(</c> exists beneath <c>Windows/</c>, which is how it catches a second roster
/// renderer appearing elsewhere. A static Draw here would have tripped that guard for a reason that
/// has nothing to do with rosters, and weakening someone else's control to admit this file would
/// have cost more than a field costs.
/// </para>
/// </remarks>
internal sealed class JoinComparisonView
{
    // Not R-1.7a copy — R-1.7a covers the session window, the admission prompt and settings, and does
    // not supply wording for these. Written here under the same constraint: no phrasing from its
    // forbidden list, and no claim that a session is protected when nobody checked.
    //
    // Travelled with the constants it governs (DMXENG-15). It sat above all four in SessionWindow;
    // three came here and CodeChangedWarning stayed, so it is stated in both places rather than
    // left behind pointing at copy that had moved.

    // The joiner's side of CompareOutOfBand. Same instruction, same constraint, addressed to the
    // person who until now was told to read out a code their client never showed them (BUG-31).
    private const string ReadYourCodeAloud =
        "Read this code to your DM over voice or chat while they decide, and check it matches what "
        + "they see. Do not send it through the plugin - a channel someone has tampered with cannot "
        + "prove it has not been tampered with.";

    // R-1.3a-i: the honest rendering when there is nothing to compare. Never a blank space where a
    // code would go, and never a placeholder that could be mistaken for one.
    private const string NoCodeToCompare =
        "Your DM's client has not sent a code to compare. You cannot check who you are talking to, "
        + "and being admitted will not tell you.";

    private const string AdmittedUncompared =
        "You were admitted without ever having a code to compare. Nothing here proves the DM is who "
        + "you think - it only proves someone admitted you.";

    /// <summary>Draws the comparison while a decision is pending, and the warning once it is made.</summary>
    /// <param name="join">The attempt being rendered.</param>
    public void Draw(JoinAttempt join)
    {
        if (join.Phase == JoinPhase.AwaitingDecision)
        {
        // R-1.3a-i: the joiner's half of the comparison, and it must be here rather than after
        // admission. The DM is shown the same value in their prompt; whichever side reads it
        // out, the other confirms.
        if (join.Fingerprint is { } fingerprint)
        {
            ImGui.TextUnformatted($"Code to compare: {fingerprint}");
            ImGui.TextWrapped(ReadYourCodeAloud);
        }
        else
        {
            ImGui.TextWrapped(NoCodeToCompare);
        }
        }

        // Said once the decision is made, because by then the code on screen is no longer something
        // that could have informed it. Reads from the snapshot taken at admission, not from whether
        // a fingerprint exists now - the host's key arrives again in the acceptance envelope, so
        // "do we have one?" is true a moment later and answers the wrong question (A-1.3f-1).
        if (join.Phase == JoinPhase.Admitted && !join.FingerprintWasComparableAtDecision)
        {
            ImGui.TextWrapped(AdmittedUncompared);
        }    }
}
