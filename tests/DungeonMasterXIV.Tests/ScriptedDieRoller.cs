using System;
using System.Collections.Generic;
using DungeonMasterXIV.Rolls;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A die source that hands back faces the test chose, in order.
/// </summary>
/// <remarks>
/// <b>THIS IS WHAT MAKES A-2.1'S INDEPENDENT CHECK POSSIBLE.</b> The criterion refuses a test that
/// asks the evaluator for the dice AND the total, because both answers come from one computation and
/// a roller that sums wrongly reports them consistently. With scripted faces the test already KNOWS
/// what was rolled before it calls anything, so the expected total is arithmetic the test did
/// itself — a second, independent source for the same number.
/// </remarks>
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

/// <summary>A die source that always returns the same face, for tests that do not care.</summary>
internal sealed class FixedDieRoller(int face) : IDieRoller
{
    /// <summary>How many times a die was asked for.</summary>
    public int Rolls { get; private set; }

    /// <inheritdoc/>
    public int Roll(int sides)
    {
        Rolls++;
        return face;
    }
}
