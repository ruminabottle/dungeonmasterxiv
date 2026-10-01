using Dalamud.Plugin.Services;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Services;

public sealed class LocalCharacterName
{
    private readonly IObjectTable _objects;

    public LocalCharacterName(IObjectTable objects) => _objects = objects;

    public DisplayName Current() => DisplayName.OrNone(_objects.LocalPlayer?.Name.TextValue);
}
