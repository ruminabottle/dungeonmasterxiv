using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Campaigns;

namespace DungeonMasterXIV.Windows;

/// <summary>Lets the host choose a stored campaign to resume, or a new one, before starting a session.</summary>
internal sealed class HostCampaignPicker
{
    public const string NewCampaignLabel = "Start a new campaign";

    private readonly HostingCampaign _hosting;

    public HostCampaignPicker(HostingCampaign hosting) => _hosting = hosting;

    public void Draw()
    {
        var resumable = _hosting.Resumable;
        if (resumable.Count == 0)
        {
            return;
        }

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
