using System;
using System.Collections.Generic;
using DungeonMasterXIV.Rolls;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A die source that hands back faces the test chose, in order.
/// </summary>
internal sealed class ScriptedDieRoller(params int[] faces) : IDieRoller
{
    private readonly Queue<int> _faces = new(faces);

    /// <summary>How many times a die was asked for, so a test can pin the work done.</summary>
    public int Rolls { get; private set; }

    /// <inheritdoc/>
    public int Roll(int sides)
    {
        Rolls++;

        if (_faces.Count is 0)
        {
            throw new InvalidOperationException(
                $"The evaluator asked for more dice than the test scripted ({Rolls} so far).");
        }

        return _faces.Dequeue();
    }
}
