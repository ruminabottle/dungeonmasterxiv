using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DungeonMasterXIV.Campaigns;

namespace DungeonMasterXIV.Windows;

/// <summary>
/// Lists the campaigns this machine holds and deletes one outright (R-1.6, A-1.10).
/// </summary>
/// <remarks>
/// Drawing only. The rows are built by <see cref="CampaignListView"/> and cached against the
/// store's revision, because a draw callback runs every frame and may not allocate in a loop.
/// The only state here is which delete is awaiting confirmation, which is a property of the
/// window rather than of the campaigns.
/// </remarks>
public sealed class CampaignListWindow : Window
{
    private readonly CampaignStore _store;

    private IReadOnlyList<CampaignRow> _rows = Array.Empty<CampaignRow>();
    private int _rowsBuiltAtRevision = -1;
    private Guid? _awaitingConfirmation;

    /// <param name="store">The campaigns this window lists and deletes.</param>
    public CampaignListWindow(CampaignStore store)
        : base("Dungeon Master XIV campaigns###dmx-campaigns")
    {
        _store = store;

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    /// <summary>Opens this window, for the campaign list command.</summary>
    public void Open() => IsOpen = true;

    /// <inheritdoc />
    public override void Draw()
    {
        RefreshRowsIfStale();

        ImGui.TextWrapped(
            "Campaigns stored on this machine. A campaign is identified by itself, not by its " +
            "session code — if a code is taken when you resume, you take a new code and keep the " +
            "campaign.");
        ImGui.Separator();

        if (_rows.Count == 0)
        {
            ImGui.TextDisabled("No campaigns stored yet.");
            return;
        }

        foreach (var row in _rows)
        {
            DrawRow(row);
        }
    }

    private void RefreshRowsIfStale()
    {
        if (_rowsBuiltAtRevision == _store.Revision)
        {
            return;
        }

        _rows = CampaignListView.Build(_store.Campaigns);
        _rowsBuiltAtRevision = _store.Revision;
    }

    private void DrawRow(CampaignRow row)
    {
        ImGui.PushID(row.CampaignId.ToString());
        ImGui.TextUnformatted(row.Label);
        ImGui.TextDisabled(row.Detail);
        ImGui.SameLine();

        if (_awaitingConfirmation == row.CampaignId)
        {
            DrawConfirmation(row.CampaignId);
        }
        else if (ImGui.Button("Delete"))
        {
            _awaitingConfirmation = row.CampaignId;
        }

        ImGui.Separator();
        ImGui.PopID();
    }

    private void DrawConfirmation(Guid campaignId)
    {
        ImGui.TextUnformatted("Delete permanently?");
        ImGui.SameLine();

        if (ImGui.Button("Yes, delete"))
        {
            _store.Delete(campaignId);
            _awaitingConfirmation = null;
        }

        ImGui.SameLine();

        if (ImGui.Button("Cancel"))
        {
            _awaitingConfirmation = null;
        }
    }
}
