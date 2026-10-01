using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Rolls;

/// <summary>Rolls one die with a given number of sides.</summary>
public interface IDieRoller
{
    int Roll(int sides);
}

/// <summary>Rolls a die using the cryptographic random number generator.</summary>
public sealed class SystemDieRoller : IDieRoller
{
    public int Roll(int sides)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 1);

        return RandomNumberGenerator.GetInt32(1, sides + 1);
    }
}
