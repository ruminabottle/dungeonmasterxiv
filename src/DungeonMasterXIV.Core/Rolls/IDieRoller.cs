using System;
using System.Security.Cryptography;

namespace DungeonMasterXIV.Rolls;

/// <summary>
/// Produces one die face. The seam that lets a test drive the evaluator with known dice.
/// </summary>
/// <remarks>
/// <b>This interface is what makes A-2.1's independent check possible.</b> The criterion requires the
/// total to be verified against something that is NOT the roller — so a test supplies the faces, and
/// then the total it expects is arithmetic the test did itself rather than a second answer from the
/// same computation. Without a seam here, every test would have to ask the evaluator both questions
/// and would pass an evaluator that sums wrongly but reports consistently.
/// </remarks>
public interface IDieRoller
{
    /// <summary>Rolls one die with <paramref name="sides"/> faces, returning 1..sides inclusive.</summary>
    int Roll(int sides);
}

/// <summary>
/// The real roller. Uses the cryptographic generator, which is not for secrecy here but because it
/// is the one source in the framework that is unbiased across a range without further work.
/// </summary>
/// <remarks>
/// <b>A-2.4 is a property of THIS type</b> — over a large sample each face of a <c>d20</c> must
/// appear within expected bounds. <see cref="RandomNumberGenerator.GetInt32(int, int)"/> is
/// documented to be uniform over its half-open range, which is why the modulo bias that a naive
/// <c>Next() % sides</c> introduces does not arise. The test measures it rather than trusting it.
/// </remarks>
public sealed class SystemDieRoller : IDieRoller
{
    /// <inheritdoc/>
    public int Roll(int sides)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(sides, 1);

        return RandomNumberGenerator.GetInt32(1, sides + 1);
    }
}
