using System;

namespace DungeonMasterXIV.Net;

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

public readonly record struct PreFilledName(string Typed, string SeededFrom);
