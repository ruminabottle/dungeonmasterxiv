using Dalamud.Plugin.Services;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Services;

/// <summary>Reads the local player's character name from the game, or none when there is no local player.</summary>
public sealed class LocalCharacterName
{
    private readonly IObjectTable _objects;

    public LocalCharacterName(IObjectTable objects) => _objects = objects;

    public DisplayName Current() => DisplayName.OrNone(_objects.LocalPlayer?.Name.TextValue);
}
