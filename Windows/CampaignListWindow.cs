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
    private IReadOnlyList<string> _preserved = Array.Empty<string>();
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

        DrawPreserved();
    }

    // A-1.10 says no trace of a deleted campaign remains on disk. A campaign whose data sits in a
    // preserved unreadable file is a trace, and those files hold participant labels — so they are
    // listed and removable here rather than accumulating unseen in a folder people zip into bug
    // reports.
    private void DrawPreserved()
    {
        if (_preserved.Count == 0)
        {
            return;
        }

        ImGui.Spacing();
        ImGui.TextUnformatted("Unreadable files kept aside");
        ImGui.TextWrapped(
            "These were kept instead of being overwritten, so nothing was lost. They still contain " +
            "whatever the unreadable file held, including participant names. Delete one once you no " +
            "longer need it.");

        foreach (var name in _preserved)
        {
            ImGui.PushID(name);
            ImGui.TextUnformatted(name);
            ImGui.SameLine();

            if (ImGui.Button("Delete file"))
            {
                _store.DeletePreserved(name);
            }

            ImGui.PopID();
        }
    }

    private void RefreshRowsIfStale()
    {
        if (_rowsBuiltAtRevision == _store.Revision)
        {
            return;
        }

        _rows = CampaignListView.Build(_store.Campaigns);
        _preserved = _store.PreservedFiles();
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
