using Dalamud.Plugin.Services;
using DungeonMasterXIV.Net;

namespace DungeonMasterXIV.Services;

/// <summary>
/// Reads the local player's character name, which is what a display name defaults to (R-1.3e).
/// </summary>
/// <remarks>
/// <para>
/// <b>In <c>Services/</c> because it is a game-state read.</b> The standards put Dalamud reads
/// here, not in a window and not in <c>Plugin.cs</c>, and this is the whole of the surface: one
/// property, one Dalamud call, no state. A window asking <c>IClientState</c> directly would be a
/// window making a game API call, and <c>Plugin.cs</c> doing it would be wiring reading game state.
/// </para>
/// <para>
/// <b>Core cannot do this itself and must not learn how.</b> <see cref="SessionCoordinator"/> takes
/// the name as a value for the same reason <c>CampaignStoreLog</c> exists: naming a Dalamud type in
/// Core would put the session layer behind a dependency its tests cannot resolve.
/// </para>
/// <para>
/// <b>It reads <c>IObjectTable</c>, not <c>IClientState</c>, and that is measured rather than
/// remembered.</b> In Dalamud 15 <c>LocalPlayer</c> lives on <see cref="IObjectTable"/>;
/// <c>IClientState</c> no longer carries it and offers only <c>IsLoggedIn</c>, <c>TerritoryType</c>
/// and similar. The obvious <c>IClientState.LocalPlayer</c> does not compile against the shipped
/// reference assemblies - same shape as the ImGuiNET move the standards already record.
/// </para>
/// <para>
/// <b>Not cached.</b> The name is read at the moment it is needed. A player can be logged out, or
/// between characters, when the session window is open — a value captured at construction would be
/// stale exactly then, and a stale name is one shown to a DM as though it were current.
/// </para>
/// </remarks>
public sealed class LocalCharacterName
{
    private readonly IObjectTable _objects;

    /// <summary>Reads names from <paramref name="objects"/>.</summary>
    /// <param name="objects">Dalamud's object table, which carries the local player.</param>
    public LocalCharacterName(IObjectTable objects) => _objects = objects;

    /// <summary>
    /// The current character's name, or <see cref="DisplayName.None"/> when there is not one.
    /// </summary>
    /// <remarks>
    /// Nothing is invented when nobody is logged in. <see cref="DisplayName.None"/> renders as a
    /// stated "gave no name" rather than a blank, so a DM sees an unnamed requester instead of a
    /// prompt that looks broken — and the fingerprint beside it is unaffected either way.
    /// </remarks>
    public DisplayName Current() => DisplayName.OrNone(_objects.LocalPlayer?.Name.TextValue);
}
