using System;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-23: the tag is the single authored source, and it is read by two parsers. This is what stops
/// them drifting.
/// </summary>
/// <remarks>
/// <para>
/// <b>One author, two readers.</b> The build derives <c>Version</c> from <c>$(ReleaseTag)</c> in
/// <c>DungeonMasterXIV.csproj</c>; the tool parses the same tag in <see cref="TaggedVersion"/>.
/// D-16's whole argument for BUG-14 was "one authored source, everything else computed from it" —
/// and the moment the tag became that source it was immediately computed from twice.
/// </para>
/// <para>
/// <b>Parsing it once is not available, and that is a fact rather than a preference.</b> MSBuild
/// needs <c>Version</c> during property evaluation, before any of this repository's code has been
/// compiled, so it cannot call into the tool. What is available is proving the two agree, which is
/// what these tests are.
/// </para>
/// <para>
/// <b>The invariant is not "the two readers accept the same tags".</b> They do not, and forcing that
/// would mean a third parser in MSBuild — the very thing BUG-23 warns about. It is:
/// </para>
/// <list type="number">
/// <item>no tag both accept yields two different versions — the dangerous case, silent by nature;</item>
/// <item>no tag the tool accepts is one the build refuses — you can always build what you may release;</item>
/// <item>a tag only the build accepts still cannot produce a release, because the tool refuses it.</item>
/// </list>
/// </remarks>
public class TheBuildAndTheToolReadTheTagAlikeTests
{
    // Every canonical spelling, through BOTH readers, compared. This is the assertion whose absence
    // BUG-23 identified: VersionHasOneAuthorTests exercised each reader, and nothing compared them.
    [Theory]
    [InlineData("v0.1.0")]
    [InlineData("v0.1.1")]
    [InlineData("v1.2.3")]
    [InlineData("v1.2.3.4")]
    [InlineData("v0.0.0.1")]
    [InlineData("v0.0.0")]
    [InlineData("v12.4.0")]
    public void TheTwoReadersComputeOneVersionForATagBothAccept(string tag)
    {
        var fromTheBuild = TaggedVersion.Pad(Version.Parse(TheBuild.VersionStampedFor(tag)));
        var fromTheTool = TaggedVersion.Of(tag);

        Assert.Equal(fromTheBuild, fromTheTool);
    }

    // Direction 2, the one that would strand an operator: a tag the tool is happy to release but the
    // build refuses to produce. That set must be empty.
    //
    // THIS ASKS A REAL BUILD (BUG-25). It used to ask the spelling guard, which runs one target and
    // never reaches the compiler -- so v70000.0.0 passed it four times over while the real build
    // exited 1 with CS7034. A test whose reach is narrower than the claim it makes reports success
    // about a case it never visited.
    //
    // Scoped to representative tags rather than every spelling, because a real build is ~0.7s
    // against a few milliseconds for the guard: the two ordinary shapes, the boundary that must
    // still work, and BUG-25's counter-example.
    [Theory]
    [InlineData("v0.1.0")]
    [InlineData("v1.2.3.4")]
    [InlineData("v65534.0.0")]
    public void NoTagTheToolAcceptsFailsARealBuild(string tag)
    {
        TaggedVersion.Of(tag);

        Assert.False(TheBuild.FailsToBuild(tag), $"The tool accepts '{tag}' but a real build of it fails.");
    }

    // The control for the helper above. Without it, a FailsToBuild that answered false for
    // everything -- a mistyped argument, a swallowed exit code -- would make that theory pass over
    // nothing, which is precisely how BUG-25 got here.
    [Fact]
    public void TheRealBuildHelperCanSeeAFailure()
    {
        Assert.True(TheBuild.FailsToBuild("V0.1.0"));
    }

    // BUG-25's counter-example, from both sides. 65534 is the largest component an assembly version
    // can carry, so a tag above it names a version no artefact can ever hold. Making only the
    // build's refusal prettier would have left invariant 2 false while this file asserted it, so the
    // tool refuses it too and the two agree.
    [Theory]
    [InlineData("v65535.0.0")]
    [InlineData("v70000.0.0")]
    public void ATagNoAssemblyCouldCarryIsRefusedByBothReaders(string tag)
    {
        var failure = Assert.Throws<ArgumentException>(() => TaggedVersion.Of(tag));
        Assert.Contains("65534", failure.Message, StringComparison.Ordinal);

        Assert.True(TheBuild.FailsToBuild(tag), $"The tool refuses '{tag}' but the build accepts it.");
    }

    // The boundary itself, on the legal side, so the cap is asserted as a boundary rather than as
    // "big numbers are refused".
    [Fact]
    public void TheLargestVersionAnAssemblyCanCarryIsStillReleasable()
    {
        Assert.Equal(new Version(65534, 0, 0, 0), TaggedVersion.Of("v65534.0.0"));
    }

    // BUG-23's headline. Before the guard this died inside NuGet restore as
    // "MSB4181: The RestoreTask task returned false but did not log an error" -- naming neither the
    // tag nor the version nor the csproj. Git tags are case-sensitive and a capital V is a real
    // convention, so this is reachable with an unreadable failure.
    [Fact]
    public void ACapitalVIsRefusedByTheBuildRatherThanFailingInsideRestore()
    {
        Assert.True(TheBuild.GuardRefusesTag("V0.1.0"));

        // And the tool refuses it too, naming the spelling that works -- so whichever the operator
        // reaches first, they are told the same thing.
        var failure = Assert.Throws<ArgumentException>(() => TaggedVersion.Of("V0.1.0"));
        Assert.Contains("'v0.1.0'", failure.Message, StringComparison.Ordinal);
    }

    // Direction 3, recorded rather than closed. `v1` is a tag the BUILD accepts -- it evaluates
    // Version to "1", which the SDK stamps as 1.0.0.0 -- and the TOOL refuses, because
    // Version.TryParse needs major.minor. The divergence is real and it is in the safe direction:
    // the refusal stops the release, so nothing is ever advertised from it.
    //
    // Closing it would mean teaching MSBuild what a version is, which is a third parser and the
    // defect this file exists to prevent. Pinned here instead, so that if either side moves the
    // change is deliberate.
    [Theory]
    [InlineData("v1", "1")]
    [InlineData("v0.1.0-rc1", "0.1.0-rc1")]
    public void ATagOnlyTheBuildAcceptsStillCannotProduceARelease(string tag, string whatTheBuildStamps)
    {
        Assert.Equal(whatTheBuildStamps, TheBuild.VersionStampedFor(tag));

        Assert.Throws<ArgumentException>(() => TaggedVersion.Of(tag));
    }
}
