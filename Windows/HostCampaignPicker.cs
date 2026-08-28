using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Campaigns;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// The campaign a DM is about to host under, offered beside the host button (A-1.9i, A-1.9j).
/// </summary>
/// <remarks>
/// <para>
/// <b>Hosting stays ONE ACTION and this control never stands in its way (A-1.9i).</b> There is no
/// confirm step, no modal and no disabled host button: a DM who ignores this entirely presses
/// "Start session" and gets a new campaign. The criterion says blocking, prompting-and-waiting or
/// refusing FAILS, so the picker is an OFFER placed before the button rather than a gate placed in
/// front of it.
/// </para>
/// <para>
/// <b>Absent, not empty, when there is nothing to resume.</b> On a first run the whole control is
/// not drawn — an empty dropdown reading "no campaigns" is a thing to read and dismiss, and it would
/// make the first-run host flow two things instead of one.
/// </para>
/// <para>
/// <b>Why it is here at all rather than in the campaign list (A-1.9j).</b> Resuming must be
/// reachable WITHOUT NAVIGATING AWAY. The Spec Owner's reason is the defect it prevents: pure
/// auto-create means <i>"a DM resuming last week's game silently gets a NEW campaign and loses the
/// roster"</i> — and a DM who has to leave the session window to avoid that will not know they
/// needed to.
/// </para>
/// <para>
/// <b>Campaigns are listed by <see cref="CampaignName"/> and never by their code (A-1.9k-3).</b>
/// This is the surface that makes the naming half load-bearing rather than cosmetic: a list of
/// session codes would satisfy A-1.9j's letter while failing the recognition it exists for.
/// </para>
/// </remarks>
internal sealed class HostCampaignPicker
{
    /// <summary>What the "do not resume anything" option says.</summary>
    /// <remarks>
    /// Names the OUTCOME rather than the absence — "None" would leave a DM wondering what hosting
    /// then does, and the answer is that it starts a new campaign.
    /// </remarks>
    public const string NewCampaignLabel = "Start a new campaign";

    private readonly HostingCampaign _hosting;

    /// <param name="hosting">The campaign association this picker sets.</param>
    public HostCampaignPicker(HostingCampaign hosting) => _hosting = hosting;

    /// <summary>Draws the picker, or nothing when there is nothing to resume.</summary>
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
