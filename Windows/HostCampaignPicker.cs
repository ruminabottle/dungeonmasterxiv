using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Campaigns;

namespace DungeonMasterXIV.Windows;

internal sealed class HostCampaignPicker
{
    public const string NewCampaignLabel = "Start a new campaign";

    public const string ResumeDisclosure =
        "Resuming keeps this campaign, but not its players. Everyone arrives as someone new, and the "
        + "roster stays empty until recognising returning players is built. Nothing has been lost.";

    private readonly HostingCampaign _hosting;

    public HostCampaignPicker(HostingCampaign hosting) => _hosting = hosting;

    public void Draw()
    {
        var resumable = _hosting.Resumable;
        if (resumable.Count == 0)
        {
            return;
        }

        ImGui.TextWrapped(ResumeDisclosure);

        var chosen = _hosting.Chosen is { } id ? _hosting.Resumable.FirstOrDefaultById(id) : null;

        if (ImGui.BeginCombo("Campaign", chosen is null ? NewCampaignLabel : CampaignName.For(chosen)))
        {
            if (ImGui.Selectable(NewCampaignLabel, chosen is null))
            {
                _hosting.Chosen = null;
            }

            foreach (var campaign in resumable)
            {
                if (ImGui.Selectable(CampaignName.For(campaign), chosen?.CampaignId == campaign.CampaignId))
                {
                    _hosting.Chosen = campaign.CampaignId;
                }
            }

            ImGui.EndCombo();
        }
    }
}
