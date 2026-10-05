using System;
using Dalamud.Bindings.ImGui;
using DungeonMasterXIV.Data;
using DungeonMasterXIV.Net;
using DungeonMasterXIV.Windows.Ui.Components;

namespace DungeonMasterXIV.Windows;

/// <summary>The form where a joiner enters a session code and the name to send, then asks to join.</summary>
internal sealed class JoinRequestForm
{
    private const string NameFieldIsFull =
        "This box is full and will not take any more. If you were still typing, the rest did not go "
        + "in - use a shorter name.";

    private readonly SessionCoordinator _coordinator;

    private readonly Func<DisplayName> _displayName;

    private readonly Func<RelinkMemory> _relink;

    private string _codeEntry = string.Empty;
    private string _nameEntry = string.Empty;
    private string _seededFrom = string.Empty;

    public JoinRequestForm(
        SessionCoordinator coordinator,
        Func<DisplayName> displayName,
        Func<RelinkMemory> relink)
    {
        _coordinator = coordinator;
        _displayName = displayName;
        _relink = relink;
    }

    public void Draw()
    {
        ImGui.InputText("Session code", ref _codeEntry, 16);

        SeedNameFromSettings();
        ImGui.InputText("Name they will see", ref _nameEntry, DisplayName.MaxUtf8Bytes);

        var willSend = DisplayName.OrNone(_nameEntry);

        if (NameInputCapacity.IsFull(_nameEntry))
        {
            ImGui.TextWrapped(NameFieldIsFull);
        }

        ImGui.TextWrapped(willSend.WasStated
            ? $"They will see: {willSend.Value}"
            : $"That name cannot be sent, so they will see \"{DisplayName.Unstated}\". Letters, "
              + "digits, spaces, apostrophes and hyphens work.");

        if (ActionRow.Primary("Request to join") && JoinFlowCode.Accepts(_codeEntry, out var code))
        {
            _coordinator.RequestJoin(code, willSend, _relink().IdFor(code));
        }
    }

    private void SeedNameFromSettings() =>
        (_nameEntry, _seededFrom) = JoinFlowName.Resolve(_displayName().Value, _seededFrom, _nameEntry);
}
