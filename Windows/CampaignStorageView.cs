using System;
using System.Collections.Generic;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Campaigns;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>Lists stored campaigns and unreadable campaign files in settings, each with a delete that asks to confirm.</summary>
internal sealed class CampaignStorageView
{
    private readonly CampaignStore _store;

    private readonly DeletionPrompt _prompt;

    private IReadOnlyList<CampaignRow> _rows = Array.Empty<CampaignRow>();
    private IReadOnlyList<UnreadableRow> _unreadable = Array.Empty<UnreadableRow>();
    private int _rowsBuiltAtRevision = -1;

    public CampaignStorageView(CampaignStore store, CampaignDeletion deletion)
    {
        _store = store;
        _prompt = new DeletionPrompt(id => deletion.Delete(id), name => _store.DeleteUnreadable(name));
    }

    public void Draw()
    {
        RefreshRowsIfStale();

        if (_rows.Count == 0)
        {
            ImGui.TextColored(Palette.TextMuted, "No campaigns stored yet.");
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
            else if (ActionRow.Secondary("Delete file"))
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
        ImGui.TextColored(Palette.TextMuted, row.Detail);
        ImGui.SameLine();

        if (_prompt.IsAwaiting(row.CampaignId))
        {
            DrawConfirmation();
        }
        else if (ActionRow.Secondary("Delete"))
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

        if (ActionRow.Edged("Yes, delete", Palette.Danger, Palette.Danger))
        {
            _prompt.Confirm();
        }

        ImGui.SameLine();

        if (ActionRow.Secondary("Cancel"))
        {
            _prompt.Cancel();
        }
    }
}
