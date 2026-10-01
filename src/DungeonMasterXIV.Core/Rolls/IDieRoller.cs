using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Rolls;

public interface IDieRoller
{
    int Roll(int sides);
}

public sealed class SystemDieRoller : IDieRoller
{
    public int Roll(int sides)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 1);

        return RandomNumberGenerator.GetInt32(1, sides + 1);
    }
}
