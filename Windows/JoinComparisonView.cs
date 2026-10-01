using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Windows;

internal sealed class JoinComparisonView
{
    private const string ReadYourCodeAloud =
        "Read this code to your DM over voice or chat while they decide, and check it matches what "
        + "they see. Do not send it through the plugin - a channel someone has tampered with cannot "
        + "prove it has not been tampered with.";

    private const string NoCodeToCompare =
        "Your DM's client has not sent a code to compare. You cannot check who you are talking to, "
        + "and being admitted will not tell you.";

    private const string AdmittedUncompared =
        "You were admitted without ever having a code to compare. Nothing here proves the DM is who "
        + "you think - it only proves someone admitted you.";

    public void Draw(JoinAttempt join)
    {
        if (join.Phase == JoinPhase.AwaitingDecision)
        {
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

        if (join.Phase == JoinPhase.Admitted && !join.FingerprintWasComparableAtDecision)
        {
            ImGui.TextWrapped(AdmittedUncompared);
        }    }
}
