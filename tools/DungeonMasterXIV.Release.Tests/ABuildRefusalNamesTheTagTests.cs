using System;
using DungeonMasterXIV.Release;
using Xunit;

namespace DungeonMasterXIV.Release.Tests;

/// <summary>
/// BUG-30: every tag the build refuses is refused by a message naming ReleaseTag and the tag.
/// </summary>
/// <remarks>
/// <para>
/// <b>The property, not the two shapes that were reported.</b> A five-component tag died in restore
/// as <c>MSB4181</c> — byte-for-byte the message BUG-23 was filed to remove — and a twenty-digit
/// component overflowed <c>Int64.Parse</c> as <c>MSB4184</c>, the crash the range target's own
/// comment says it was shaped to avoid. Both got past guards that enumerated the shapes we had hit
/// rather than the shape we accept. Asserting the property over every refusal is what stops a third
/// shape arriving; asserting the two would be the same mistake one layer up.
/// </para>
/// <para>
/// <b>Through a real build.</b> The claim is about what the operator sees, and the operator runs
/// <c>dotnet build</c> — the two failures above are emitted by MSBuild and NuGet, which no
/// single-target invocation reaches. That distinction is BUG-25, and this file is written to it.
/// </para>
/// </remarks>
public class ABuildRefusalNamesTheTagTests
{
    // Every refusal an operator can reach, not just the two that were filed: the spelling guard, the
    // range guard, and the shape gate. One property over all of them.
    [Theory]
    [InlineData("V0.1.0")]
    [InlineData("v65535.0.0")]
    [InlineData("v1.2.3.4.5")]
    [InlineData("v99999999999999999999.0.0")]
    public void ARefusedTagIsNamedInTheRefusal(string tag)
    {
        var refusal = TheBuild.RefusalFromARealBuild(tag);

        Assert.Contains("ReleaseTag", refusal, StringComparison.Ordinal);
        Assert.Contains(tag, refusal, StringComparison.Ordinal);
    }

    // Stated as the absence of the specific internal messages, because "names the tag" would still
    // be satisfied by a build that printed our error AND then died inside MSBuild anyway.
    [Theory]
    [InlineData("v1.2.3.4.5")]
    [InlineData("v99999999999999999999.0.0")]
    public void TheBuildDoesNotFallThroughIntoMSBuildsOwnErrors(string tag)
    {
        var refusal = TheBuild.RefusalFromARealBuild(tag);

        Assert.DoesNotContain("MSB4181", refusal, StringComparison.Ordinal);
        Assert.DoesNotContain("MSB4184", refusal, StringComparison.Ordinal);
    }

    // The half that keeps the shape gate from absorbing the tool's job. Each of these is a tag the
    // RELEASE TOOL refuses -- as an alias (BUG-22), or for naming no version -- and every one must
    // still BUILD. A gate that judged canonical form here would be the third parser BUG-23 exists
    // to prevent, and these are exactly the tags that would show it had.
    [Theory]
    [InlineData("v0.1.0.0")]
    [InlineData("v01.2.3")]
    [InlineData("vv0.1.0")]
    [InlineData("v2.3")]
    [InlineData("v1")]
    [InlineData("v0.1.0-rc1")]
    public void ATagOnlyTheToolRefusesStillBuilds(string tag)
    {
        Assert.Throws<ArgumentException>(() => TaggedVersion.Of(tag));

        Assert.False(
            TheBuild.FailsToBuild(tag),
            $"'{tag}' is the release tool's to refuse, but the build refused it — the shape gate has " +
            "taken on canonical-form judgement, which is the third parser BUG-23 forbids.");
    }
}
