using System.Collections.Generic;
using System.Linq;

namespace DungeonMasterXIV.Rolls;

/// <summary>Gives the notice a roll carries when every rolled die was dropped or rerolled away.</summary>
public static class RollSurvival
{
    public const string NothingSurvived =
        "Every die was dropped or rerolled away, so no die counted towards the total.";

    public static string? NoticeFor(IReadOnlyList<RolledDie> dice) =>
        dice.Count > 0 && !dice.Any(die => die.Kept) ? NothingSurvived : null;
}
