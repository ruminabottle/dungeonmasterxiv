using System;

namespace DungeonMasterXIV.Net;

/// <summary>Decides the join form's name field, refreshing it from settings while the player has not edited it.</summary>
public static class JoinFlowName
{
    public static PreFilledName Resolve(string fromSettings, string lastSeeded, string typed)
    {
        ArgumentNullException.ThrowIfNull(fromSettings);
        ArgumentNullException.ThrowIfNull(lastSeeded);
        ArgumentNullException.ThrowIfNull(typed);

        var sourceMoved = !string.Equals(fromSettings, lastSeeded, StringComparison.Ordinal);
        var fieldIsStillOurs = string.Equals(typed, lastSeeded, StringComparison.Ordinal);

        return sourceMoved && fieldIsStillOurs
            ? new PreFilledName(fromSettings, fromSettings)
            : new PreFilledName(typed, lastSeeded);
    }
}

/// <summary>The join form's name field text and the settings value it was last filled from.</summary>
public readonly record struct PreFilledName(string Typed, string SeededFrom);
