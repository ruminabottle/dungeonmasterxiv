using System;
using System.Collections.Generic;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Windowing;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;

namespace DungeonMasterXIV.Windows;

/// <summary>Lists stored campaigns and unreadable campaign files, each with a delete that asks to confirm.</summary>
public sealed class CampaignListWindow : Window
{
    private readonly CampaignStore _store;

    private readonly DeletionPrompt _prompt;

    private IReadOnlyList<CampaignRow> _rows = Array.Empty<CampaignRow>();
    private IReadOnlyList<UnreadableRow> _unreadable = Array.Empty<UnreadableRow>();
    private int _rowsBuiltAtRevision = -1;

    public CampaignListWindow(CampaignStore store, CampaignDeletion deletion)
        : base("Dungeon Master XIV campaigns###dmx-campaigns")
    {
        _store = store;

        _prompt = new DeletionPrompt(id => deletion.Delete(id), name => _store.DeleteUnreadable(name));

        SizeConstraints = new WindowSizeConstraints
        {
            MinimumSize = new Vector2(420, 200),
            MaximumSize = new Vector2(float.MaxValue, float.MaxValue),
        };
    }

    public void Open() => IsOpen = true;

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

            if (_prompt.IsAwaiting(row.FileName))
            {
                DrawConfirmation();
            }
            else if (ImGui.Button("Delete file"))
            {
                _prompt.Request(row.FileName);
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

        if (_prompt.IsAwaiting(row.CampaignId))
        {
            DrawConfirmation();
        }
        else if (ImGui.Button("Delete"))
        {
            _prompt.Request(row.CampaignId);
        }

        ImGui.Separator();
        ImGui.PopID();
    }

    private void DrawConfirmation()
    {
        ImGui.TextUnformatted("Delete permanently?");
        ImGui.SameLine();

        if (ImGui.Button("Yes, delete"))
        {
            _prompt.Confirm();
        }

        ImGui.SameLine();

        if (ImGui.Button("Cancel"))
        {
            _prompt.Cancel();
        }
    }
}
