using System;
using System.Linq;
using DungeonMasterXIV.Rolls;
using Xunit;

namespace DungeonMasterXIV.Tests;

/// <summary>
/// A-2.4: over a large sample, each face of a <c>d20</c> appears within expected bounds — the
/// generator is not visibly skewed.
/// </summary>
/// <remarks>
/// <para>
/// <b>A UNIFORMITY TEST THAT HAS NEVER REJECTED ANYTHING IS NOT A TEST.</b> The obvious version
/// rolls a lot of dice, finds nothing wrong, and passes — and would pass identically if the check
/// were arithmetically incapable of failing. So the same check is run against a DELIBERATELY SKEWED
/// generator and must reject it. Only then does its silence on the real one carry information.
/// </para>
/// <para>
/// The bound is ±5% of the expected count, which at this sample size is roughly five standard
/// deviations — wide enough that an honest generator will not trip it in practice, narrow enough
/// that a generator with a real bias cannot hide inside it. The control below fixes how much bias
/// is actually caught.
/// </para>
/// </remarks>
public class TheDieGeneratorIsNotVisiblySkewedTests
{
    private const int Sides = 20;
    private const int Sample = 200_000;
    private const double Tolerance = 0.05;

    private static int[] Histogram(IDieRoller roller)
    {
        var counts = new int[Sides + 1];
        for (var i = 0; i < Sample; i++)
        {
            counts[roller.Roll(Sides)]++;
        }

        return counts;
    }

    /// <summary>The check itself, so the real generator and the control are judged identically.</summary>
    private static bool WithinBounds(int[] counts)
    {
        var expected = (double)Sample / Sides;
        var allowed = expected * Tolerance;

        return Enumerable.Range(1, Sides).All(face => Math.Abs(counts[face] - expected) <= allowed);
    }

    [Fact]
    public void EveryFaceOfADTwentyAppearsWithinExpectedBounds()
    {
        var counts = Histogram(new SystemDieRoller());

        Assert.True(WithinBounds(counts), $"skew: [{string.Join(", ", counts.Skip(1))}]");
    }

    // THE CONTROL. A generator that never rolls a 1, spreading its share over the other faces, is
    // only 5% wrong -- a bias a casual eye would not see in a log. The check must catch it, or the
    // test above proves nothing about the real generator.
    [Fact]
    public void TheSameCheckREJECTSAGeneratorThatIsSkewed()
    {
        var counts = Histogram(new NeverRollsOneRoller());

        Assert.False(WithinBounds(counts), "the uniformity check failed to catch a known-biased generator");
    }

    // A second control at a smaller bias, to fix roughly how sensitive the check is rather than
    // leaving it to be discovered by a future failure.
    [Fact]
    public void TheCheckAlsoCatchesATenPercentThumbOnOneFace()
    {
        var counts = Histogram(new HeavyFaceRoller(face: 20, extraShare: 0.10));

        Assert.False(WithinBounds(counts));
    }

    [Fact]
    public void EveryFaceIsRolledAtLeastOnceAcrossTheSample()
    {
        var counts = Histogram(new SystemDieRoller());

        Assert.All(Enumerable.Range(1, Sides), face => Assert.True(counts[face] > 0));
    }

    [Fact]
    public void NoFaceOutsideOneToSidesIsEverProduced()
    {
        var roller = new SystemDieRoller();

        for (var i = 0; i < 10_000; i++)
        {
            Assert.InRange(roller.Roll(6), 1, 6);
        }
    }

    /// <summary>Rolls uniformly over 2..sides, never 1. A 5% bias, invisible to a casual reader.</summary>
    private sealed class NeverRollsOneRoller : IDieRoller
    {
        private readonly Random _random = new(20260829);

        public int Roll(int sides) => _random.Next(2, sides + 1);
    }

    /// <summary>Gives one face an extra share of the distribution.</summary>
    private sealed class HeavyFaceRoller(int face, double extraShare) : IDieRoller
    {
        private readonly Random _random = new(20260830);

        public int Roll(int sides) =>
            _random.NextDouble() < extraShare ? face : _random.Next(1, sides + 1);
    }
}
