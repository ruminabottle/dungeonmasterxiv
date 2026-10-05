using System;
using System.Linq;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>Lists the participant ids remembered per session code, each with a Forget that asks to confirm.</summary>
internal sealed class RelinkMemoryView
{
    private readonly Func<RelinkMemory> _relink;
    private readonly Action _persist;

    private string _confirming = string.Empty;

    public RelinkMemoryView(Func<RelinkMemory> relink, Action persist)
    {
        ArgumentNullException.ThrowIfNull(relink);
        ArgumentNullException.ThrowIfNull(persist);

        _relink = relink;
        _persist = persist;
    }

    public void Draw()
    {
        var remembered = _relink().All();

        if (remembered.Count == 0)
        {
            ImGui.TextWrapped("Your client is not storing any participant ids right now.");
            return;
        }

        foreach (var entry in remembered.ToArray())
        {
            ImGui.Separator();

            ImGui.TextUnformatted($"Session code {entry.SessionCode}");
            ImGui.TextUnformatted($"Participant {entry.ParticipantId:D}");
            ImGui.TextUnformatted(entry.StoredUtc is { } stored
                ? $"Stored {stored.ToLocalTime():d}"
                : "Stored before dates were kept");

            if (_confirming == entry.SessionCode)
            {
                DrawConfirmation(entry.SessionCode);
                continue;
            }

            if (ActionRow.Secondary($"{RelinkDisclosure.BeginForgetting}##{entry.SessionCode}"))
            {
                _confirming = entry.SessionCode;
            }
        }
    }

    private void DrawConfirmation(string sessionCode)
    {
        ImGui.TextWrapped(RelinkDisclosure.BeforeForgetting(sessionCode));

        if (ActionRow.Secondary($"{RelinkDisclosure.KeepIt}##keep-{sessionCode}"))
        {
            _confirming = string.Empty;
        }

        ImGui.SameLine();

        if (!ActionRow.Edged($"{RelinkDisclosure.ConfirmForget}##forget-{sessionCode}", Palette.Danger, Palette.Danger))
        {
            return;
        }

        if (_relink().Forget(SessionCode.FromValid(sessionCode)))
        {
            _persist();
        }

        _confirming = string.Empty;
    }
}
