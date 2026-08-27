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
    private IReadOnlyList<UnreadableRow> _unreadable = Array.Empty<UnreadableRow>();
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
        }

        // Iterating the cached snapshot, NOT _store.Campaigns. This is what makes the Delete
        // button below safe: Delete mutates the store's list while this loop is running, and
        // iterating the live collection here would throw. The safety is not incidental — do not
        // "simplify" this to walk the store directly.
        foreach (var row in _rows)
        {
            DrawRow(row);
        }

        DrawUnreadable();
    }

    private void RefreshRowsIfStale()
    {
        if (_rowsBuiltAtRevision == _store.Revision)
        {
            return;
        }

        _rows = CampaignListView.Build(_store.Campaigns);
        _unreadable = CampaignListView.BuildUnreadable(_store.Unreadable);
        _rowsBuiltAtRevision = _store.Revision;
    }

    // A-1.10, as extended on 2026-08-27: the DM must be able to list and delete EVERY campaign the
    // machine holds, including files the plugin cannot read or parse. An unreadable file is exactly
    // the one a user cannot reason about, so it is the one that most needs to be visible — and it
    // sits in the folder people zip into a bug report.
    private void DrawUnreadable()
    {
        if (_unreadable.Count == 0)
        {
            return;
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Files that cannot be read");

        foreach (var row in _unreadable)
        {
            ImGui.PushID(row.FileName);
            ImGui.TextUnformatted(row.FileName);
            ImGui.TextWrapped(row.Detail);

            if (ImGui.Button("Delete file"))
            {
                _store.DeleteUnreadable(row.FileName);
            }

            ImGui.Separator();
            ImGui.PopID();
        }
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
